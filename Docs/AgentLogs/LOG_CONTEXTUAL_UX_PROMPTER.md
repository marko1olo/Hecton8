# CONTEXTUAL_UX_PROMPTER Log

## 2026-05-14 Diegetic Input Tooltip Pass
What was wrong: The existing input prompt path was coupled to managed hover events and a tooltip singleton, which made a diegetic hatch prompt behave like scene UI state instead of a signal-fed presentation system. The old rendering path did not meet the requested indirect draw, integer glyph index, AUP shift, Low-tier snap, or black-box telemetry requirements.

What was done: Added `PlayerLookTargetSignal` and signal-bus setup in `GlobalSignals.cs`; published hash-only look-target packets from `PlayerInteraction`; added fixed `PlayerLookTargetPromptCache` sidecar storage; rewired `DiegeticTooltipSystem` to consume signals, stage fixed char buffers, resolve glyphs from `GlobalRegistry.InputDeterminism`, draw icon/text quads through `Graphics.DrawMeshInstancedIndirect`, survive `AupShiftSignal`, dither fade on non-Low tiers, snap alpha on Low, and dump a 300-frame black box on bad anchors. Removed `DiegeticTooltipSystem.ActiveRuntimeInstance`; repair diagnostics now resolve through `GlobalRegistry.Renderables`.

Cinematic cheats used: One atlas quad per glyph instead of true 3D text, shader dither instead of Animator/Canvas alpha, hash-only prompt packets instead of managed UI payloads, VR 0.1m depth bias using `rsqrt`, Low-tier instant alpha instead of fade, and per-instance integer UV selection instead of TMP rich text.

Exact microseconds saved: Estimates only, not profiler-measured. Expected low-end savings: 18-45 us by avoiding Canvas/TMP object rendering for the prompt, 3-7 us by skipping dither/fade on Low, 2-6 us by using bounded char cache instead of text assignment, 1-3 us by direct glyph array lookup, and 3-8 us by removing singleton/scene-search diagnostic routing from the normal hot path.

Scalability matrix: Low snaps alpha and disables shader dither; Middle uses 0.2s dither fade; High can improve atlas sharpness and SDF tuning; Ultra can add richer per-glyph visual treatment on the same indirect payload without changing gameplay authority.

Verification: Filtered `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned no touched-file errors for `GlobalSignals`, `PlayerLookTargetPromptCache`, `PlayerInteraction`, `DiegeticTooltipSystem`, `RepairTool`, or tooltip shader references after the final cache collision fix. Unfiltered broad build did not complete inside the tool timeout in the dirty multi-agent workspace. Unity MCP refresh failed because `http://127.0.0.1:8088/mcp` was unreachable, so Editor console verification remains pending.

Final diff evidence: Focused tracked diff covers `GlobalSignals.cs`, `PlayerInteraction.cs`, `RepairTool.cs`, `DiegeticTooltipSystem.cs`, `Status_CONTEXTUAL_UX_PROMPTER.md`, and `Rationale_CONTEXTUAL_UX_PROMPTER.md` with 946 insertions and 410 deletions. Created/untracked files are `PlayerLookTargetPromptCache.cs`, `Hecton_DiegeticTooltipIndirect.shader`, `DiegeticTooltipContracts.cs`, and their Unity `.meta` files.

## 2026-05-14 Recheck Upgrade
What was wrong: The fixed prompt cache still had a direct `hash & 63` placement path during recheck, which is faster but can drop a valid prompt on hash collision.

What was done: Replaced direct placement with bounded linear lookup, first-free-slot insertion, deterministic rollover, and subsystem reset. Re-ran the touched-file build filter; no errors were emitted for `PlayerLookTargetPromptCache`, `GlobalSignals`, `PlayerInteraction`, `DiegeticTooltipSystem`, `RepairTool`, or tooltip shader references.

Cinematic cheats used: Kept hash-only signal payloads and bounded char slab storage instead of introducing managed prompt objects or dictionaries.

Exact microseconds saved: No new savings claimed. This trade spends a small signal-time compare budget for prompt correctness while keeping render-time cost unchanged at fixed-array reads.

## 2026-05-15 Continuation Restore And H-Phi Polish
What was wrong: Recheck found the disk status/rationale had stale loop records, and the runtime renderer had reverted several hot-path fixes: UI `IUpdatable.Tick` signal consumption, shared icon/text indirect buffers, shader `round()` and Bayer division expressions, and per-glyph `Quaternion.LookRotation`/`Matrix4x4.TRS`.

What was done: Restored `ILateFrameTickable` signal resolve before post-simulation snapshot clear, restored separate icon/text instance and args buffers, restored contract-sourced input scheme/glyph constants, restored direct billboard matrix writes, restored shader constant Bayer LUT and branch-gated dither, and added a fail-closed SRP camera gate through `GlobalRenderContext.CurrentCamera`.

Cinematic cheats used: Same physical fake: one atlas quad per glyph, integer atlas lookup, alpha-test dither instead of blended Canvas/UI, Low-tier snap instead of fade, and camera-facing billboard math instead of real 3D text.

Exact microseconds saved: Estimates only. Avoids one duplicate indirect submission per non-target camera pass, removes one quaternion/TRS helper path per glyph, avoids one blank space quad already present from the prior pass, and avoids repeated material/dither writes after warmup. No profiler capture available.

Scalability matrix: Low snaps alpha, disables dither, uses minimal quads, and now avoids auxiliary-camera submission. Middle keeps 0.2s Bayer dither. High/Ultra can spend the preserved CPU/GPU budget on richer glyph materials and atlas quality without changing gameplay authority.

Verification: No dotnet rebuilds were run because the user explicitly forbade them. Static scans on touched tooltip/cache/interaction files returned no `foreach`, `string.Format`, `.ToString(`, interpolated strings, managed collection construction, LINQ markers, exact sqrt, or normalize calls. Static scans on tooltip/shader returned no old shared `_instanceBuffer`/`_argsBuffer`, `_registeredUpdate`, tooltip `public void Tick`, tooltip `TryRegisterUpdatable`, `Quaternion.LookRotation`, `Matrix4x4.TRS`, shader `round(`, or Bayer `/ 16` expressions. `git diff --check` passed with CRLF warnings only.

## 2026-05-15 Scoped H-Phi Micro Pass
What was wrong: The diegetic tooltip still had two avoidable render-adjacent costs after the restore pass: Low-tier checks still reached through `GlobalRegistry.ScalabilityTierProfileByte`, and black-box telemetry used modulo for a fixed 300-entry ring cursor.

What was done: Cached the Low-tier flag during lifecycle and late-frame update, made `IsLowTier()` a local boolean read, and replaced the black-box modulo cursor with increment plus branch wrap. Re-ran static scans after the change.

Cinematic cheats used: Preserved the same fake-first prompt model: fixed atlas quads, alpha-test dither on non-Low tiers, instant Low-tier snap, and telemetry only as a bounded ring.

Exact microseconds saved: Estimate only, not profiler-measured. Expected gain is below 1 us per visible tooltip frame on i3/MX350, but it removes avoidable registry/modulo work from a path that can run every frame.

Verification: No dotnet rebuilds were run by instruction. Post-pass scans found no forbidden hot-path text/allocation/LINQ patterns, no old update/shared-buffer/matrix/shader symbols, no shader `round(` or Bayer `/ 16`, and no `% BlackBoxCapacity` cursor modulo. `git diff --check` on the tooltip and shader files produced no errors.

## 2026-05-15 Render Basis Consolidation
What was wrong: The renderer sampled `camera.transform` basis vectors inside each indirect batch and ran the UV dirty upload check from inside `DrawBatch`, duplicating work for the normal icon-plus-text prompt.

What was done: Moved camera position/right/up/forward sampling to `Render`, passed the basis into both batch submissions, changed XR depth offset to use the sampled camera position, and moved `UploadUvTablesIfDirty()` to render scope.

Cinematic cheats used: Same diegetic prompt fake: atlas quads and integer UV lookup, one frame-consistent camera basis, and shader dither rather than Canvas alpha.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us for a single visible prompt, but the duplicated transform/property path is gone and both batches now use the same camera sample.

Verification: No dotnet rebuilds were run. Static scans remained clean for forbidden hot-path allocation/text/LINQ patterns and old renderer/update/shader markers. `git diff --check` produced only repository CRLF warnings.

## 2026-05-15 Resource And Material Hardening
What was wrong: The tooltip still performed full resource-object readiness checks in the visible render path, used `Marshal.SizeOf` in buffer allocation, and retained runtime `Shader.Find` plus `new Material` fallback code.

What was done: Added explicit buffer strides and `_resourceObjectsReady`; split resource creation from material/property binding; added authored glyph and icon material assets in `Assets/_Project/Resources/UI`; replaced runtime material clone/search fallback with cold material resource loading; moved texture, buffer, SDF tuning, and dither binding into persistent per-draw `MaterialPropertyBlock`s; added a fail-closed shader-contract check.

Cinematic cheats used: Same fake-first implementation: one atlas quad per glyph, integer UV lookup, dithered alpha-test fade, Low-tier snap, and no Canvas overlay.

Exact microseconds saved: Estimate only. Expected steady-frame gain is sub-1 us from readiness and stride cleanup; cold path removes two material allocations and one shader lookup fallback. No runtime profiler proof.

Verification: No dotnet rebuilds were run. Static scans returned no forbidden hot-path text/allocation/LINQ patterns, no old update/shared-buffer/matrix/shader markers, and no `Marshal.SizeOf`, `Shader.Find`, or `new Material(` matches in the tooltip/shader scope. `git diff --check` produced only repository CRLF warnings. `Tools/Architecture/HectonPhiAudit.ps1 -Json` completed at `2026-05-15 01:32:33 +04:00`; the second score-summary extraction timed out, so no exact H-Phi score delta is claimed here.

## 2026-05-15 Material Readiness Latch
What was wrong: Missing or mismatched authored tooltip materials could still cause repeated material readiness checks in visible frames, even though the renderer would fail closed and submit nothing.

What was done: Added cached material-ready, material-resolve-attempted, and material-resolve-failed states. The tooltip now skips material setup after warmup and skips repeated resolve attempts after a failed authored material contract until resources are released.

Cinematic cheats used: Same indirect atlas-quad prompt with dithered fade and Low-tier snap; this pass only hardens setup gates.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us in normal frames and prevents repeated cold-resource lookup/check work when authoring is invalid. No runtime profiler proof.

Verification: No dotnet rebuilds were run. Static scans stayed clean for forbidden allocation/text/LINQ patterns and old renderer/update/shader markers. `git diff --check` returned only repository CRLF warnings.

## 2026-05-15 MPB Dirty Binding
What was wrong: The indirect tooltip renderer still cleared and rebound the same `MaterialPropertyBlock` state for each icon/text batch, even when texture, compute buffers, SDF tuning, and dither state had not changed.

What was done: Added per-batch bound-state caches and a dirty binding gate. The renderer now uploads per-instance glyph payloads every visible draw, but only calls `MaterialPropertyBlock.Clear`, `SetTexture`, `SetFloat`, and `SetBuffer` when binding state changes.

Cinematic cheats used: Preserved the same fake-first diegetic prompt model: atlas quads, integer UV lookup, dithered alpha-test fade for non-Low tiers, and Low-tier snap. This pass removes redundant CPU binding traffic without changing the visual contract.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per visible prompt on i3/MX350, with the practical benefit that High/Ultra material polish can be layered without resetting identical MPB bindings every frame.

Verification: No dotnet rebuilds were run. Static scans returned no forbidden hot-path text/allocation/LINQ patterns, no old update/shared-buffer/matrix/shader markers, and no runtime `Marshal.SizeOf`, `Shader.Find`, or `new Material(` markers in the tooltip/shader scope. `git diff --check` returned CRLF normalization warnings only on the tooltip/status/rationale/log scope. `Tools/Architecture/HectonPhiAudit.ps1 -Summary` was retried and timed out after 120 seconds without output, so no H-Phi score claim is made.

## 2026-05-15 Registry Camera Resolution
What was wrong: The diegetic tooltip renderer still had a fallback path that reached through `GameBootstrapper` and `GetComponentInChildren<Camera>()` if the authored camera and registry camera were missing. That hides broken registry wiring and keeps a component search in a renderer-owned camera resolver.

What was done: Removed the bootstrap/component-search fallback and the `Time.unscaledTime` retry gate. Tooltip camera resolution is now authored `interactionCamera` first, then cached `GlobalRegistry.Player.PlayerCamera`, with cache reset on player service hot-swap.

Cinematic cheats used: No visual change. This preserves the same indirect atlas-quad prompt and fail-closed camera gating; the change removes discovery work rather than adding rendering truth.

Exact microseconds saved: Estimate only. Steady-frame savings are negligible when the camera is already cached. Worst-case cold-path search is eliminated, and missing registry wiring now fails closed instead of scanning hierarchy.

Verification: No dotnet rebuilds were run. Focused scans found no `Hecton8.Bootstrap`, `GameBootstrapper`, `GetComponentInChildren`, `Time.unscaledTime`, `CameraResolveRetryIntervalSeconds`, `_nextCameraResolveTime`, or `interactionCamera = _cachedRenderCamera` markers in `DiegeticTooltipSystem.cs`. Existing forbidden hot-path allocation/text/LINQ and old renderer/update/shader scans stayed clean. `git diff --check` returned CRLF normalization warnings only.

## 2026-05-15 Indirect Args Dirty Count
What was wrong: The renderer still uploaded indirect argument buffers every visible icon/text draw even when the glyph count had not changed.

What was done: Added cached icon/text args counts. The tooltip now updates indirect args only on count changes, while still uploading per-instance glyph transforms/tints every visible draw.

Cinematic cheats used: Same fake-first prompt path: atlas quads, integer UV lookup, dithered alpha-test fade on non-Low tiers, Low-tier snap, and fail-closed camera ownership.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per steady visible prompt, mostly reduced CPU-to-GPU argument buffer traffic and driver-side work.

Verification: No dotnet rebuilds were run. Static scans stayed clean for forbidden hot-path text/allocation/LINQ and old renderer/update/shader markers. Args scan shows `argsBuffer.SetData(_indirectArgs)` remains in buffer initialization and the new count-dirty branch only. `git diff --check` returned CRLF normalization warnings only.

## 2026-05-15 Input Determinism Cache
What was wrong: The tooltip scheme resolver still fetched `GlobalRegistry.InputDeterminism` during scheme checks instead of using a cached service reference maintained by lifecycle/hot-swap.

What was done: Added `_inputDeterminism`, refreshed it on enable/start and input service hot-swap, cleared it on disable, and changed `ResolveCurrentSchemeHash()` to read the cached interface.

Cinematic cheats used: No visual contract change. The same atlas-quad input prompt remains, with device glyphs resolved from deterministic scheme state.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per active prompt frame by removing one registry access from scheme checks.

Verification: No dotnet rebuilds were run. Static scans stayed clean for forbidden allocation/text/LINQ and old renderer/update/shared-buffer/matrix/shader markers. Input scan confirmed `GlobalRegistry.InputDeterminism` remains only in lifecycle refresh, while `ResolveCurrentSchemeHash()` reads `_inputDeterminism`. `git diff --check` returned CRLF normalization warnings only.

## 2026-05-15 Render Resource Fail-Closed Gate
What was wrong: Missing compute buffers or authored materials could still let `Render()` perform camera resolution, anchor math, bounds creation, and telemetry before the batch calls failed closed.

What was done: Added an immediate readiness gate after `EnsureResources()`. If resource objects, materials, or the quad mesh are not ready, the renderer returns before camera/anchor work.

Cinematic cheats used: No visual change. The same atlas-quad diegetic prompt path remains; invalid resource states now fail closed earlier.

Exact microseconds saved: Estimate only. No normal ready-frame gain is claimed; invalid authoring states avoid unnecessary camera/anchor/bounds work.

Verification: No dotnet rebuilds were run. Static scans stayed clean for forbidden hot-path allocation/text/LINQ and old renderer/update/shared-buffer/matrix/shader markers. Render gate scan confirmed readiness checks happen before `ResolveRenderCamera()`. `git diff --check` returned CRLF normalization warnings only.

## 2026-05-15 Render Cache Tightening
What was wrong: Visible prompt frames still read `camera.transform` and recomputed derived max-distance/bounds values every render.

What was done: Cached the render camera transform with the resolved camera reference, including explicit-camera stale-cache handling and player hot-swap reset. Added derived caches for max visible distance squared and bounds size, refreshed only when `maxVisibleDistance` changes.

Cinematic cheats used: No visual change. The same atlas-quad prompt, dither fade, Low-tier snap, and fail-closed material/camera gates remain.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per visible prompt frame on i3/MX350 from fewer render-side property/math operations.

Verification: No dotnet rebuilds were run. Static scans stayed clean for forbidden allocation/text/LINQ and old renderer/update/shared-buffer/matrix/shader markers. Cache scan confirmed render uses `_cachedRenderCameraTransform`, `_cachedMaxVisibleDistanceSq`, and `_cachedBoundsSize`; the only remaining `camera.transform` access is inside `CacheRenderCamera()`. `git diff --check` returned CRLF normalization warnings only.

## 2026-05-15 Atlas Texture And Layer Cache
What was wrong: Icon/text indirect submissions still read atlas texture properties at render time and fetched `gameObject.layer` inside each batch.

What was done: Cached active font and sprite atlas textures during layout rebuild, then passed one sampled render layer into both icon/text indirect draw calls.

Cinematic cheats used: No visual contract change. The same integer-index atlas quads, dither fade on non-Low tiers, Low-tier snap, and fail-closed camera/material gates remain.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per visible prompt frame from fewer render-side property reads; no profiler proof is claimed.

Verification: No dotnet rebuilds were run. Static scans stayed clean for forbidden allocation/text/LINQ and old renderer/update/shared-buffer/matrix/shader markers. Atlas/layer scan confirmed `_runtimeSpriteAtlasTexture`, `_runtimeFontAtlasTexture`, and `renderLayer` feed draw submission, with only one render-time `gameObject.layer` sample.

## 2026-05-15 Diegetic Panel Phosphor Material
What was wrong: `DiegeticPanelController` still used runtime shader lookup and material construction for the PDA/panel phosphor decay compositor.

What was done: Added authored `Resources/UI/MAT_DiegeticPanelPhosphorDecay` and changed the controller to resolve, validate, and cache that material instead of calling `Shader.Find` or `new Material`.

Cinematic cheats used: Preserved the same phosphor-history fake: previous RT decays into current RT, buying CRT persistence without simulating display electronics.

Exact microseconds saved: Estimate only. No steady-frame win is claimed; cold path removes one shader lookup and one material allocation, and invalid authoring now fails closed instead of constructing fallback state.

Verification: No dotnet rebuilds were run. Static scans found no `PhosphorDecayShaderPath`, `AssetDatabase`, `Shader.Find`, or `new Material(` markers in `DiegeticPanelController.cs`; hot-path text/LINQ marker scan for the same file returned no matches. `git diff --check` returned the repository CRLF warning only on the edited panel file.

## 2026-05-15 Diegetic Panel Camera Ownership
What was wrong: Physical panel camera resolution still reached through `GameBootstrapper.TryGetCurrentPlayerTransform` and carried a one-second retry timer.

What was done: Removed the bootstrap fallback and retry field. `DiegeticPanelController` now resolves an authored interaction camera first, then `GlobalRegistry.Player.PlayerCamera`, and fails closed when no active camera exists.

Cinematic cheats used: No visual change. The same physical panel cursor projection and RT presentation remain; the change removes ownership ambiguity from the camera source.

Exact microseconds saved: Estimate only. Steady-frame gain is small; cold/worst-case path avoids one bootstrap call chain, one component probe, and one retry-timer branch.

Verification: No dotnet rebuilds were run. Static scans found no `Hecton8.Bootstrap`, `GameBootstrapper`, `_cameraRetryTimer`, `TryGetCurrentPlayerTransform`, old phosphor fallback markers, or hot-path text/LINQ markers in `DiegeticPanelController.cs`.

## 2026-05-15 Diegetic Panel Tick Time Cache
What was wrong: Physical panel tick work read unscaled time separately for interaction freshness, proxy-light flicker, and queued input-event timestamps.

What was done: Sampled dispatcher unscaled time once per active panel tick into `_tickUnscaledTime` and reused it across the tick call stack.

Cinematic cheats used: No visual change. The same proxy-light flicker fake remains; it now uses the same sampled frame timestamp as panel input events.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per active panel tick by removing repeated native time property reads.

Verification: No dotnet rebuilds were run. Static scan found no direct `Time.unscaledTime` or `Time.realtimeSinceStartup` markers in `DiegeticPanelController.cs`.

## 2026-05-15 Diegetic Panel Camera Transform Cache
What was wrong: Physical panel distance refresh and ray projection still read `resolvedCamera.transform` directly after camera ownership was moved to the registry path.

What was done: Cached the resolved interaction camera transform with the camera reference, tracked explicit-camera ownership, and cleared the cache on disable.

Cinematic cheats used: No visual change. The same physical panel projection math remains; the change removes repeated transform property access and stale explicit-camera ownership risk.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per active physical panel frame, with cleaner camera ownership for future panel effects.

Verification: No dotnet rebuilds were run. Static scan found no `resolvedCamera.transform` markers; the only remaining `camera.transform` marker in `DiegeticPanelController.cs` is inside `CacheInteractionCamera()`.

## 2026-05-15 Diegetic Panel Input Service Cache
What was wrong: `DiegeticPanelController` still refreshed `GlobalRegistry.Input` during every runtime-state check. A naive cache would also be wrong because `GlobalRegistry.Input` can return the no-op fallback before the real input dispatcher registers, and first registration from null does not broadcast a hot-swap event.

What was done: Added hot-swap listener ownership to the panel controller, cached the real registered `IInputService`, and kept a narrow `_inputAwaitingRegistration` fallback probe only while `GlobalRegistry.RegisteredInput` is empty. Player service hot-swap now refreshes the cached panel camera when no authored interaction camera is assigned.

Cinematic cheats used: No visual contract change. The same physical panel projection, RT presentation, proxy-light fake, and phosphor-history CRT persistence remain.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per active physical panel tick after input registration, with correctness preserved for startup ordering.

Verification: No dotnet rebuilds were run. Static scans confirmed listener registration/unregistration, `GlobalRegistry.Input` isolated to `RefreshServices()`, stale no-op protection through `_inputAwaitingRegistration`, and no forbidden hot-path allocation/text/LINQ, bootstrap fallback, direct `Time`, old phosphor fallback, or `resolvedCamera.transform` markers.

## 2026-05-15 Diegetic Panel Material Property Cache
What was wrong: Phosphor-enabled physical panels refresh the output texture every late frame, but the material path still repeated `HasProperty` checks and rewrote `_PanelPowerLevel` during texture-only updates.

What was done: Cached panel output material property support when the material reference changes, routed texture/float writes through cached flags, and separated the material-written power value from the logical panel power state.

Cinematic cheats used: Preserved the phosphor-history CRT persistence fake. The change spends less CPU/API traffic on the same authored material effect.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per phosphor-enabled panel late frame on i3/MX350; no profiler proof is claimed.

Verification: No dotnet rebuilds were run. Static scans confirmed `HasProperty` is isolated to `RefreshPanelOutputMaterialPropertyCache()`, material writes use cached flags, and no forbidden allocation/text/LINQ, bootstrap fallback, direct `Time`, old phosphor fallback, or `resolvedCamera.transform` markers returned.

## 2026-05-15 Diegetic Panel Phosphor Decay Dirty Scalar
What was wrong: The phosphor composite pass correctly rebinds previous/current RT textures every frame, but it also wrote the stable `_Decay` scalar every frame.

What was done: Added `_appliedPhosphorDecay`, reset it on phosphor material cache reset, and dirty-gated `_Decay` writes so only decay changes touch that scalar.

Cinematic cheats used: Preserved the same RT-history phosphor fake; only removed redundant API traffic around the stable decay coefficient.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per phosphor-enabled panel late frame on i3/MX350; no profiler proof is claimed.

Verification: No dotnet rebuilds were run. Static scans confirmed `_Decay` writes pass through `_appliedPhosphorDecay` and no forbidden allocation/text/LINQ, bootstrap fallback, direct `Time`, old phosphor fallback, or `resolvedCamera.transform` markers returned.

## 2026-05-15 Diegetic Panel Interface Source Cache
What was wrong: Runtime-state validation called `ResolveInterfaces()` every active tick, and that method recast the same serialized panel receiver and power-source components each time.

What was done: Added cached source references for `panelInteractable` and `panelPowerSource`; interface casts now run only when those serialized sources change.

Cinematic cheats used: No visual change. This preserves the same physical panel receiver and power hooks while removing repeated type checks from active panels.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per active physical panel tick; no profiler proof is claimed.

Verification: No dotnet rebuilds were run. Static scans confirmed source-change-driven casts and no forbidden allocation/text/LINQ, bootstrap fallback, direct `Time`, old phosphor fallback, or `resolvedCamera.transform` markers returned.

## 2026-05-15 Tooltip Input Determinism Startup Guard
What was wrong: The tooltip input scheme cache could hold the no-op input determinism fallback if the renderer enabled before the real input dispatcher registered.

What was done: Changed tooltip input refresh to prefer `GlobalRegistry.RegisteredInput`, keep `_inputDeterminismAwaitingRegistration` only while the slot is empty, and retry through that narrow startup path before scheme reads.

Cinematic cheats used: No visual change. This protects correct diegetic glyph selection for keyboard, gamepad, Steam Deck, and XR prompts.

Exact microseconds saved: Estimate only. Primary gain is correctness; steady-state avoids registry polling after input registration.

Verification: No dotnet rebuilds were run. Static scans confirmed registered-slot input caching, no-op fallback guard, and no forbidden allocation/text/LINQ, bootstrap fallback, direct `Time`, old phosphor fallback, or `resolvedCamera.transform` markers.

## 2026-05-15 Tooltip Scalability Event Cache
What was wrong: Tooltip late-frame work still read `GlobalRegistry.ScalabilityTierProfileByte` every frame to update Low-tier dither behavior.

What was done: Implemented `IScalabilityChangedEventListener`, registered with `ScalabilityEvents`, and moved `_lowTierActive` updates to enable/start refresh plus scalability-change events.

Cinematic cheats used: Preserved the Low-tier snap and non-Low dither fade. The visual fake is unchanged; the tier decision is just event-driven now.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per active tooltip late frame on i3/MX350; no profiler proof is claimed.

Verification: No dotnet rebuilds were run. Static scans confirmed no late-frame scalability registry poll remains, `ScalabilityEvents` owns runtime tier updates, and existing no-bootstrap/no-direct-Time/no-old-phosphor/no-hot-path scans stayed clean.

## 2026-05-15 Tooltip Active Camera Fail-Closed Gate
What was wrong: Tooltip camera resolution could retain inactive authored/player camera references and still draw when the render context did not provide a current SRP camera.

What was done: Required `isActiveAndEnabled` for authored and player cameras, cleared inactive cached registry cameras, and kept the `GlobalRenderContext.CurrentCamera` comparison as the final render submission gate.

Cinematic cheats used: No visual change. The same indirect quad prompt remains; invalid camera ownership now fails closed instead of spending draw work on wrong views.

Exact microseconds saved: Estimate only. Steady single-camera frames are neutral; invalid/auxiliary camera paths avoid unnecessary render preparation.

Verification: No dotnet rebuilds were run. Static scans confirmed active-camera checks in tooltip resolution and no forbidden allocation/text/LINQ, bootstrap fallback, direct `Time`, old phosphor fallback, or `resolvedCamera.transform` markers.

## 2026-05-15 Tooltip Scheme Read Gating
What was wrong: Tooltip late-frame work resolved the input scheme even when no signal prompt was active or when diagnostics were hiding the signal prompt and no binding icon could use the scheme.

What was done: Gated scheme refresh to active non-diagnostic signal prompts and made prompt layout rebuilds refresh the scheme once before binding-icon selection.

Cinematic cheats used: No visual change. The same integer-index glyph atlas remains; input scheme reads now happen only when the diegetic prompt can display a device glyph.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per idle tooltip late frame on i3/MX350; no profiler proof is claimed.

Verification: No dotnet rebuilds were run. Static scans confirmed gated scheme reads and no forbidden allocation/text/LINQ, bootstrap fallback, direct `Time`, old phosphor fallback, or `resolvedCamera.transform` markers.

## 2026-05-15 Tooltip Render-Path Scheme Read Removal
What was wrong: `ResolveAnchorPosition()` still had a render-path fallback call that could read the input scheme when the cached scheme hash was zero.

What was done: Removed the fallback from anchor resolution, refreshed the scheme on input hot-swap and diagnostic show, and reused the refreshed scheme for hot-swap layout rebuilds.

Cinematic cheats used: Preserved the XR 0.1m comfort depth offset while keeping render submission deterministic and input-free.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us on active tooltip render frames when the scheme cache would otherwise be cold; no profiler proof is claimed.

Verification: No dotnet rebuilds were run. Static scans confirmed `ResolveAnchorPosition()` uses cached `_activeSchemeHash` only and no forbidden allocation/text/LINQ, bootstrap fallback, direct `Time`, old phosphor fallback, or `resolvedCamera.transform` markers returned.

## 2026-05-15 Tooltip UV Dirty-Gate
What was wrong: Prompt layout rebuilds marked font and sprite UV compute buffers dirty even when the atlas rect for a glyph was unchanged.

What was done: Added exact UV rect comparison before writing font/sprite UV table slots, so full UV-table uploads happen only after real table changes.

Cinematic cheats used: No visual change. The same atlas-driven glyph fake remains; the patch only removes redundant buffer-upload traffic during repeated layout rebuilds.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us on i3/MX350 during device hot-swap or prompt layout rebuilds with unchanged atlas rects; no profiler proof is claimed.

Verification: No dotnet rebuilds were run. Static scans confirmed UV dirty flags now pass through `WriteUvRectIfChanged()`, render-path scheme reads remain removed, and forbidden allocation/text/LINQ plus bootstrap/direct-time fallback scans stayed clean.

## 2026-05-15 Tooltip Normalized-Span Layout
What was wrong: Tooltip prompt staging already writes normalized uppercase/safe characters into the fixed prompt buffer, but layout measurement and glyph building normalized those same characters again.

What was done: Removed duplicate normalization from `MeasureAdvance()` and `BuildTextRun()`; normalization remains only at prompt staging boundaries.

Cinematic cheats used: No visual change. The same fixed-buffer, atlas-driven prompt fake remains; the layout path just stops paying for redundant text sanitation.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per prompt layout rebuild on i3/MX350; no profiler proof is claimed.

Verification: No dotnet rebuilds were run. Static scans confirmed `NormalizeTooltipCharacter()` is isolated to staging, layout reads the staged span directly, and forbidden allocation/text/LINQ plus bootstrap/direct-time fallback scans stayed clean.

## 2026-05-15 Tooltip Layout Math Consistency
What was wrong: Font/text/icon layout clamps still used `Mathf.Max` while the renderer's math path uses `Unity.Mathematics`.

What was done: Replaced the remaining tooltip layout `Mathf.Max` calls with `math.max` without changing clamp thresholds or glyph sizing.

Cinematic cheats used: No visual change. The same diegetic glyph atlas layout remains; the pass only keeps math cheap and consistent.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per layout rebuild on i3/MX350; no profiler proof is claimed.

Verification: No dotnet rebuilds were run. Static scans confirmed no `Mathf.` calls remain in `DiegeticTooltipSystem.cs`, and forbidden allocation/text/LINQ plus bootstrap/direct-time fallback scans stayed clean.

## 2026-05-15 Tooltip Sprite Asset Local Cache
What was wrong: Binding-icon layout read `spriteAsset`, `spriteSheet`, and `spriteCharacterTable` through repeated property chains.

What was done: Cached the sprite asset, sheet texture, and character table in locals before count and index access.

Cinematic cheats used: No visual change. The same integer-index TMP sprite atlas binding remains; the resolver just stops repeating asset property reads.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per binding-icon layout rebuild on i3/MX350; no profiler proof is claimed.

Verification: No dotnet rebuilds were run. Static scans confirmed the local-cache shape in `TryResolveBindingIcon()` and forbidden allocation/text/LINQ plus bootstrap/direct-time fallback scans stayed clean.

## 2026-05-15 Tooltip Single-Pass Text Layout
What was wrong: Prompt layout measured text with one TMP character lookup pass and then built glyph payloads with a second pass over the same staged buffer.

What was done: Removed `MeasureAdvance()`. Text glyphs are now built once at zero, the returned advance is used for centering, and finished glyph centers are shifted in a tight numeric loop.

Cinematic cheats used: No visual change. The same centered diegetic glyph layout remains; this only reduces CPU work before indirect draw submission.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per prompt layout rebuild on i3/MX350, with larger savings on longer fixed-buffer prompts; no profiler proof is claimed.

Verification: No dotnet rebuilds were run. Static scans confirmed `MeasureAdvance()` is removed, `BuildTextRun()` is the only TMP text glyph traversal, and forbidden allocation/text/LINQ plus bootstrap/direct-time fallback scans stayed clean.

## 2026-05-15 Tooltip Advance-Scale Hoist
What was wrong: Text layout recomputed `glyphScale * glyphAdvanceScale` per glyph and read glyph metrics around the space/non-space branch.

What was done: Hoisted the advance-scale product once per `BuildTextRun()` call and cached `GlyphMetrics` before branching.

Cinematic cheats used: No visual change. Prompt spacing and atlas output are identical; the layout loop just pays less arithmetic/property cost.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per prompt layout rebuild on i3/MX350; no profiler proof is claimed.

Verification: No dotnet rebuilds were run. Static scans confirmed the hoisted `advanceScale`, cached metrics, and forbidden allocation/text/LINQ plus bootstrap/direct-time fallback scans stayed clean.

## 2026-05-15 Tooltip Icon-Scale Hoist
What was wrong: Binding-icon layout multiplied `glyphScale * IconScaleMultiplier` separately for width and height.

What was done: Hoisted that product once into `iconScale` before applying icon width and height clamps.

Cinematic cheats used: No visual change. The same authored icon sizing remains; the branch just avoids repeated scalar math.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per binding-icon layout rebuild on i3/MX350; no profiler proof is claimed.

Verification: No dotnet rebuilds were run. Static scans confirmed the hoisted `iconScale` and forbidden allocation/text/LINQ plus bootstrap/direct-time fallback scans stayed clean.

## 2026-05-15 Diegetic Panel Output Texture Dirty Cache
What was wrong: Physical panel forced material refreshes could write the same output texture into `_BaseMap` and `_MainTex` repeatedly when the resolved RT reference had not changed.

What was done: Added `_appliedPanelOutputTexture`, reset it on material/RT/phosphor ownership changes, and gated output texture property writes by texture reference.

Cinematic cheats used: Preserved the phosphor front/back history fake. Swapped phosphor textures still rebind every composite because the output texture reference changes.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per forced material refresh on i3/MX350 when the output texture is unchanged; no profiler proof is claimed.

Verification: No dotnet rebuilds were run. Static scans confirmed output texture writes are gated by `_appliedPanelOutputTexture`, cache resets exist on material/texture release paths, and forbidden allocation/text/LINQ plus bootstrap/direct-time fallback scans stayed clean.

## 2026-05-15 Diegetic Panel Phosphor Material Texture Cache
What was wrong: The phosphor composite material rebound `_PreviousTex` and `_CurrentTex` every composite frame, even though `_CurrentTex` usually remains the same panel RT.

What was done: Added previous/current phosphor texture reference caches and reset them on material resolve, validation reset, phosphor texture release, and material release paths.

Cinematic cheats used: Preserved the phosphor history fake. `_PreviousTex` still updates on front/back swaps because its texture reference changes.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per phosphor composite on i3/MX350; no profiler proof is claimed.

Verification: No dotnet rebuilds were run. Static scans confirmed gated phosphor texture writes and cache resets on material/texture ownership paths, and forbidden allocation/text/LINQ plus bootstrap/direct-time fallback scans stayed clean.

## 2026-05-15 Diegetic Panel Interaction Distance Hoist
What was wrong: The desktop panel ray path resolved the same effective interaction distance twice in one tick: once for AUP range validation and once for panel projection.

What was done: Resolved the clamped interaction distance once and passed it to both range and projection checks.

Cinematic cheats used: No visual change. The same physical panel ray fake remains; the path just avoids duplicate range clamp math before cursor projection.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per desktop panel interaction tick on i3/MX350; no profiler proof is claimed.

Verification: No dotnet rebuilds were run. Static scans confirmed `ResolveEffectiveInteractionDistance()` is called once in the desktop ray path, `IsRayOriginWithinAupInteractionRange()` consumes the cached distance, and forbidden allocation/text/LINQ plus bootstrap/direct-time fallback scans stayed clean.

## 2026-05-15 Diegetic Panel Projection Reciprocal Cache
What was wrong: Panel projection helpers rebuilt inverse canvas/reference sizes from already-clamped panel data during projection calls.

What was done: Added cached inverse canvas and reference sizes to `PanelData`, populated them in `RefreshPanelData()`, and reused them in canvas-to-world, pixel-basis, and local-hit-to-canvas projection helpers.

Cinematic cheats used: No visual change. The same diegetic panel surface projection remains; this only moves stable derived math out of the projection path.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per active panel projection on i3/MX350, most visible on fingertip hover paths that project every tick; no profiler proof is claimed.

Verification: No dotnet rebuilds were run. Static scans confirmed the cached reciprocal fields are written from clamped panel data and consumed by projection helpers.

## 2026-05-15 Diegetic Panel Projection Direction Math
What was wrong: The panel ray projection helper recomputed direction length even for the normalized desktop ray path and rebuilt a panel-normal fallback despite already caching a safe panel normal during panel-data refresh.

What was done: Kept direction-length validation for non-normalized public ray projections only, switched plane projection to `_panelData.PanelNormal`, and cached `maxDistanceSq` before comparing travel distance.

Cinematic cheats used: No visual change. The same physical panel ray projection fake remains; the hot desktop path just pays less validation math.

Exact microseconds saved: Estimate only. Expected gain is sub-1 us per desktop panel ray projection on i3/MX350; no profiler proof is claimed.

Verification: No dotnet rebuilds were run. Static scans confirmed normalized desktop projection skips the duplicate `math.lengthsq(rayDirection)` path, public non-normalized projection still validates length, and panel projection uses the cached safe panel normal.

## 2026-05-15 Diegetic Panel Cursor Margin Clamp
What was wrong: The physical cursor clamp used serialized margins directly, so tiny panels or over-authored margins could invert local clamp bounds and make the cursor pin or jump.

What was done: Sanitized cursor margins to the `[0, panel half-size]` range inside `UpdateCursor()` before building clamp bounds.

Cinematic cheats used: No visual change for normal authoring. The physical cursor remains a cheap transform-following diegetic marker; the patch keeps that marker stable on edge-case panel geometry.

Exact microseconds saved: None claimed. Expected cost is sub-1 us per cursor update on i3/MX350; the gain is correctness and stable UX under bad authoring bounds.

Verification: No dotnet rebuilds were run. Static scans confirmed cursor clamp now uses sanitized `cursorMarginLocal` bounds before `math.clamp`.

## 2026-05-15 Diegetic Panel Finger Mode Release
What was wrong: Switching a panel to `RaycastOnly` while a fingertip press was latched returned from the finger path without emitting an Up event.

What was done: Routed active finger presses through `ResolveFingerRelease()` before leaving the finger path, and cleared stale active finger ownership when no press is active.

Cinematic cheats used: No visual change. The same hybrid finger/raycast panel interaction remains; the patch prevents a latent input-state artifact during runtime mode changes.

Exact microseconds saved: None claimed. Runtime cost is one branch in the finger path; the gain is deterministic input release behavior.

Verification: No dotnet rebuilds were run. Static scans confirmed the `RaycastOnly` branch now emits pending release events and clears stale finger ownership.

## 2026-05-15 Diegetic Panel Clear-State Release
What was wrong: `ClearHoverState()` reset desktop and finger pressed flags without notifying the receiver, so focus/range loss, presentation pause, or disable transitions could leave a panel receiver latched.

What was done: Added `DispatchReleaseBeforeClear()` and call it before clearing pressed flags or the input queue, sending a final Up event at the last clamped canvas position.

Cinematic cheats used: No visual change. This preserves the physical panel event contract during cinematic state changes instead of relying on timeout behavior.

Exact microseconds saved: None claimed. Normal hover frames do not pay; clear-state calls add one guarded branch and optional direct Up dispatch.

Verification: No dotnet rebuilds were run. Static scans confirmed clear-state release runs before pressed flags and queued input state are reset.

## 2026-05-15 Diegetic Panel Clear-State Event Ordering
What was wrong: The clear-state release path could send the synthetic Up event before older queued events if the bounded event queue still had pending input.

What was done: Split panel input dispatch into a bounded overload and made clear-state release drain queued events in FIFO order before emitting the final Up.

Cinematic cheats used: No visual change. This keeps diegetic panel receivers deterministic during abrupt focus/range/pause transitions.

Exact microseconds saved: None claimed. Normal frames keep the same four-event cap; clear-state calls can drain the existing 16-event ring once.

Verification: No dotnet rebuilds were run. Static scans confirmed `DispatchReleaseBeforeClear()` drains queued events through the ordered dispatch overload before sending Up.

## 2026-05-15 Tooltip Text-Sink Stale Payload Clear
What was wrong: The optional world-space TMP validation sink received prompt text but was not cleared when signal or diagnostic payloads disappeared, leaving stale prompt text in-world.

What was done: Added a sink payload latch, captured the non-UGUI sink that received text, and clear it once with `SetCharArray(_promptBuffer, 0, 0)` when no payload is active or layout cannot produce glyphs.

Cinematic cheats used: No change to primary indirect glyph rendering. This keeps the auxiliary authoring surface from becoming a non-diegetic stale overlay.

Exact microseconds saved: None claimed. Normal frames pay a boolean check; the clear runs once per payload loss without string assignment.

Verification: No dotnet rebuilds were run. Static scans confirmed `ClearTextSink()` is gated and called from no-payload, diagnostic clear, hard clear, and missing-font paths.

## 2026-05-15 Tooltip Culling Authoring Clamp
What was wrong: Tooltip culling and XR depth offset trusted serialized/runtime floats, allowing bad values to poison visible-distance bounds or move XR prompts away from the camera.

What was done: Added finite clamps for visible distance cache writes and a finite non-negative clamp for VR depth offset application.

Cinematic cheats used: No visual change for valid authoring. Invalid values now fail to predictable culling/offset ranges instead of creating non-diegetic prompt behavior.

Exact microseconds saved: None claimed. This is correctness hardening; added scalar checks are tiny and only on visible render paths.

Verification: No dotnet rebuilds were run. Static scans confirmed visible distance is clamped to finite `[0.5, 20]` and XR depth offset is finite/non-negative before use.

## 2026-05-15 Tooltip Black-Box Chronological Dump
What was wrong: Tooltip telemetry used a circular NativeArray but dumped raw storage order, so after wraparound the file did not read as the actual last-frame sequence.

What was done: Added valid-sample tracking and changed `DumpBlackBox()` to write entries oldest-to-newest, wrapping from `_blackBoxCursor` when the ring is full.

Cinematic cheats used: No visual change. This improves the crash evidence trail for diegetic prompts instead of spending runtime budget on extra diagnostics.

Exact microseconds saved: None claimed. Normal render path adds one bounded counter increment; dump path is cold and writes only valid samples.

Verification: No dotnet rebuilds were run. Static scans confirmed `_blackBoxWrittenCount` bounds the dump length, wrapped export starts at `_blackBoxCursor`, and release resets cursor/count.

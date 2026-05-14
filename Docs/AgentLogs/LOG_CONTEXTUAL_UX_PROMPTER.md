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

What was done: Added explicit buffer strides and `_resourceObjectsReady`; split resource creation from material/property binding; added authored glyph and icon material assets in `Assets/_Project/Resources/UI`; replaced runtime material clone/search fallback with cold material resource loading; moved texture, buffer, SDF tuning, and dither binding into persistent per-draw `MaterialPropertyBlock`s.

Cinematic cheats used: Same fake-first implementation: one atlas quad per glyph, integer UV lookup, dithered alpha-test fade, Low-tier snap, and no Canvas overlay.

Exact microseconds saved: Estimate only. Expected steady-frame gain is sub-1 us from readiness and stride cleanup; cold path removes two material allocations and one shader lookup fallback. No runtime profiler proof.

Verification: No dotnet rebuilds were run. Static scans returned no forbidden hot-path text/allocation/LINQ patterns, no old update/shared-buffer/matrix/shader markers, and no `Marshal.SizeOf`, `Shader.Find`, or `new Material(` matches in the tooltip/shader scope. `git diff --check` produced only repository CRLF warnings.

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

## 2026-05-14 Render Hot-Path Hardening
What was wrong: Icon and text indirect draws shared one instance buffer and one args buffer, which risks GPU-side overwrite if the render backend consumes the first draw after the CPU prepares the second. The shader used a hash dither and `round()` where fixed integer/lut math is enough.

What was done: Split icon/text into separate compute buffers and indirect args buffers; skipped space glyph quads; dirty-gated material texture/buffer/SDF/dither state; replaced shader hash dither with a 4x4 Bayer LUT; clamped glyph UV indices to 0-127; removed shader `round()` and division notation from the glyph path.

Cinematic cheats used: Bayer clip fade instead of alpha blending, atlas quads instead of real 3D text, uniform Low-tier branch to skip dither, and direct integer glyph UV lookup instead of TMP rich-text parsing.

Exact microseconds saved: Estimates only. Expected savings are one skipped blank quad for `"OPEN HATCH"`, avoided repeated material property writes after warmup, and cheaper per-pixel dither math on non-Low tiers. The larger correctness gain is eliminating the shared-buffer indirect draw race.

Verification: Captured `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned `DOTNET_EXIT=0`; touched-file filter emitted no matches. `git diff --check` on the renderer, shader, and cache passed with CRLF warnings only. Unity Editor shader import remains pending because MCP is unavailable.

## 2026-05-14 CPU Matrix Polish
What was wrong: Per-glyph billboard setup still used `Quaternion.LookRotation` and `Matrix4x4.TRS`.

What was done: Replaced it with direct `Matrix4x4` column writes from camera right/up/forward vectors and local glyph scale.

Cinematic cheats used: Camera-facing atlas quads remain the core lie; the transform setup is now a direct billboard matrix instead of general-purpose transform construction.

Exact microseconds saved: Not profiler-measured. Expected gain is small per glyph but deterministic on weak CPUs; it removes one quaternion/TRS helper path per submitted glyph.

Verification: Quiet `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:q -clp:ErrorsOnly` returned `DOTNET_EXIT=0`. Static C# scan found no forbidden string/collection/sqrt/normalize patterns in touched runtime files.

## 2026-05-14 Late-Frame Resolve Correction
What was wrong: The prompt resolver was registered in the UI update lane. That is dispatcher-owned and after the player lane, but it is not the strict late-frame/POST_SIMULATION window used by the project's VISUAL_SYNC UI examples.

What was done: Converted `DiegeticTooltipSystem` from `IUpdatable`/`ITickable` to `ILateFrameTickable`, moved signal consume, AUP-shift consume, scheme refresh, and fade progression into `LateFrameTick()`, and registered through `GlobalRegistry.TryRegisterLateFrameTickable(..., PriorityLayer.UI)`. Kept drawing in `IRenderable.Render` under the SRP render dispatcher. Tooltip scheme and sprite-index constants now point at `Hecton8.UI.Diegetic.Contracts`.

Cinematic cheats used: Same atlas-quad diegetic fake, same Bayer/dither fade, same Low-tier snap. The change is phase correctness, not a visual expansion.

Exact microseconds saved: No measurable savings claimed. Expected cost is neutral; the value is correct ordering and fresher current-frame look-target consumption before `GlobalSignals.ClearPostSimulationSnapshots()`.

Verification: Quiet `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:q -clp:ErrorsOnly` returned `DOTNET_EXIT=0`. Static scan found no tooltip `TryRegisterUpdatable(..., PriorityLayer.UI)` or `public void Tick(`, and no forbidden string/collection/sqrt/normalize patterns in the touched tooltip/cache/interaction files. `git diff --check` returned only repository CRLF warnings. Current `Docs/Tasks/CURRENT_BATCH.md` no longer contains `CONTEXTUAL_UX_PROMPTER`; persisted status/rationale remain the active assignment evidence.

## 2026-05-14 SRP Camera Submission Gate
What was wrong: The SRP render dispatcher invokes renderables once per camera. The tooltip draw also provides an explicit camera to `Graphics.DrawMeshInstancedIndirect`, so auxiliary camera passes could submit duplicate player-camera draws.

What was done: Added `ResolveRenderCamera()` in `DiegeticTooltipSystem`. It resolves the intended interaction camera, compares it against `GlobalRenderContext.CurrentCamera`, and skips non-target camera passes. The fallback path is unchanged when no render context is active.

Cinematic cheats used: No new visual cheat. This protects the existing atlas-quad prompt from redundant submission in multi-camera/editor frames.

Exact microseconds saved: No profiler measurement. Expected savings are zero in a single player-camera frame and one avoided indirect submission per auxiliary camera pass in editor or multi-camera scenes.

Verification: First post-change build returned `DOTNET_EXIT=1` with `CS0006` missing Unity-generated metadata DLLs under `Temp/bin/Debug`. The metadata was present on immediate check and the retry returned `DOTNET_EXIT=0`. Static scan found only the expected `ResolveRenderCamera`/`GlobalRenderContext` references and no forbidden string/collection/sqrt/normalize patterns in `DiegeticTooltipSystem.cs`. `git diff --check` returned only repository CRLF warnings.

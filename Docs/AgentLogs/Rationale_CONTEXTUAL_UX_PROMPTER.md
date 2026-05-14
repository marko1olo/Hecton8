# CONTEXTUAL_UX_PROMPTER Rationale

Status: PENDING VERIFICATION

## Decision 0: Architecture Entry
Problem: Existing implementation unknown; diegetic input prompts must not create a singleton or Canvas tutorial overlay.
Solution: Inspect project contracts first, then implement through UI-domain files with GlobalRegistry/EventBus boundaries and fixed buffers.
Rejected Alternatives: A screen-space Canvas tooltip, a TooltipManager.Instance singleton, or direct dependency on player controller concrete classes; all violate local mandates and parallel-agent isolation.
Scalability potential: Low uses one prompted target with instant fade and minimal math; Middle adds dither fade; High adds richer atlas glyph support; Ultra can spend saved CPU on denser visual effects without changing gameplay authority.
Hardware Impact: Expected hot-path target under 100 us on i3/MX350 by using fixed arrays, cached references, and no per-frame string or dictionary work. Measured proof absent.

## Decision 1: Look Target Transport
Problem: The existing tooltip consumed `InteractionEvents.HoverChanged`, which carried managed `IInteractable` sidecar state and forced UI to depend on interaction object lifetime.
Solution: Added `PlayerLookTargetSignal` as an unmanaged `SignalBus` lane. `PlayerInteraction` publishes AUP, runtime anchor, normal, target hash, collider hash, frame, and prompt hash from the existing kinematics raycast; bounded `PlayerLookTargetPromptCache` stores char payloads outside the signal packet.
Rejected Alternatives: Polling `PlayerInteraction.CurrentHovered`, retaining `IInteractable` in UI, or adding a `TooltipManager.Instance`. Those bind UI to gameplay object ownership and create singleton pressure.
Scalability potential: Low receives one compact prompt lane and instant alpha. Middle/High keep the same lane but spend rendering budget on dither and sharper glyphs. Ultra can add richer per-instance effects without touching interaction.
Hardware Impact: Expected gain on i3/MX350 is avoiding managed hover dispatch and string routing every frame; signal publication is limited to raycast cadence/transition. Measured proof absent because project compile is currently blocked by unrelated dependencies.

## Decision 2: Scene-Compatible Singleton Removal
Problem: `DiegeticTooltipSystem.ActiveRuntimeInstance` was used by repair diagnostics and violated the no-singleton tooltip mandate.
Solution: Removed the static owner from the tooltip system and changed repair diagnostics to resolve the active renderer through `GlobalRegistry.Renderables`, which is already the sanctioned multi-instance registry.
Rejected Alternatives: Keeping `ActiveRuntimeInstance`, creating `TooltipManager.Instance`, or doing scene searches. Static owner state is the exact failure mode; scene searches are slow and allocation-risky.
Scalability potential: Low pays no cost in the normal input-prompt path. Diagnostics scan the small renderable bucket only when repair diagnostics ask for a prompt. High/Ultra can support multiple diegetic tooltip renderers without singleton contention.
Hardware Impact: Expected diagnostic-only cost is single-digit microseconds on i3/MX350 for the current renderable bucket scale; normal hatch prompt saves the prior static lookup and managed listener dependency.

## Decision 3: Glyph Resolution
Problem: The previous icon resolver walked `InputManager` binding strings and TMP sprite names, which violates dictionary/string-free hot-path requirements.
Solution: Resolve current scheme through `GlobalRegistry.InputDeterminism` and map to fixed TMP sprite indices. Steam Deck Interact maps to index 14 (`pad_west` / X glyph). Text glyphs are cached into a 128-entry ASCII table before layout.
Rejected Alternatives: `TryGetPreferredBindingPath`, `TryGetBindingSpriteName`, TMP name scans during draw, or binding display strings like `[E]`. Those are UI-string workflows, not diegetic GPU prompt payloads.
Scalability potential: Low shows one icon and text. Middle/High/Ultra can swap sprite assets or author richer glyph atlases using the same integer index contract.
Hardware Impact: Expected low-end gain is eliminating per-frame string and dictionary work; only direct array reads remain during prompt layout. Measured proof absent.

## Decision 4: Indirect Quad Rendering
Problem: The previous renderer used `Graphics.DrawMeshInstanced` with `MaterialPropertyBlock` vector arrays. It worked, but it was not the requested BRG/indirect-style draw and required per-draw property-block traffic.
Solution: Added `Hecton_DiegeticTooltipIndirect.shader` and persistent compute buffers for instance payload and indirect args. The tooltip renders one camera-facing quad per icon/text glyph through `Graphics.DrawMeshInstancedIndirect`.
Rejected Alternatives: World-space Canvas, per-target TMP mesh components, or keeping `DrawMeshInstanced` as "good enough." Those do not meet the prompt and scale poorly once multiple diegetic prompts exist.
Scalability potential: Low draws one short prompt with instant alpha. Middle keeps dither fade. High can add sharper SDF tuning. Ultra can add more per-instance visual treatment in the same buffer without creating UI objects.
Hardware Impact: Expected i3/MX350 impact stays below the 0.1 ms suspicion line for one prompt because CPU only fills a small fixed array and GPU draws trivial quads. Measured proof absent.

## Decision 5: Fade, VR, And Text Path
Problem: A diegetic prompt that pops in/out or z-fights the hatch reads as a tutorial overlay, not a physical interface.
Solution: Dither alpha over 0.2s on non-low tiers, snap on Low, and move XRTouch prompts 0.1m toward the camera. Prompt formatting uses fixed char buffers and `SetCharArray` only for an optional world-space TMP sink; primary rendering remains indirect quads.
Rejected Alternatives: CanvasGroup fade, Animator/coroutine fade, ZTest-disabled overlay, or `TMP_Text.text`. Those add managed state, hide depth problems, or violate zero-GC UI rules.
Scalability potential: Low has instant stable readability. Middle gets dithered comfort. High/Ultra can spend the same prompt buffer on denser glyph effects and material variants.
Hardware Impact: Expected low-end benefit is no Animator/Canvas update path and no managed text allocations; VR offset uses one `rsqrt` scale on XR frames. Measured proof absent.

## Decision 6: AUP, LOD, And Black Box
Problem: A tooltip over a hatch must survive floating-origin shifts and weak-device frame pressure without becoming a stale world-space label.
Solution: The renderer stores target AUP, consumes `AupShiftSignal`, adjusts cached runtime anchors, and resolves AUP against the current origin each render. Low tier snaps alpha and disables dither; non-low tiers keep the 0.2s cinematic dither. A 300-entry `NativeArray` black box records frame, target hash, anchor, alpha, scheme, glyph count, and flags; NaN anchor detection dumps it to `Docs/AgentLogs/Dump_CONTEXTUAL_UX_PROMPTER.bin`.
Rejected Alternatives: Transform-only anchoring, delayed re-query after origin shift, universal dither on weak hardware, or no crash-state ring because "UI is harmless." Those create stale prompts and unverifiable failures.
Scalability potential: Low is a stable instant prompt. Middle has dither fade. High can spend on sharper SDF/tint tuning. Ultra can add overkill per-glyph effects using the same indirect payload.
Hardware Impact: Expected i3/MX350 gain is avoiding Canvas rebuilds and disabling dither on Low. Black box cost is one fixed-struct write per rendered prompt frame. Measured proof absent.

## Decision 7: Shader Orientation And Atlas Indexing
Problem: The prompt required glyph selection by integer payload and quad orientation proof, not only CPU-authored UV rectangles.
Solution: Instance payload now carries a glyph index; the shader reads `_TooltipUvRects[glyphIndex]` and performs UV interpolation on GPU. Static orientation check: quad winding fronts local `-Z`; `LookRotation(camera.forward, camera.up)` puts local `-Z` toward the camera for objects in front of the camera.
Rejected Alternatives: Passing only UV rectangles, TMP sprite tags, or trusting the old shader. Those miss the explicit integer-index atlas requirement.
Scalability potential: Low and Middle use one small UV table. High/Ultra can expand atlas styling/material variants without changing signal or layout contracts.
Hardware Impact: Expected impact is a single structured-buffer read per glyph vertex, bought back by removing CPU string/name lookup and Canvas/TMP object churn.

## Decision 8: OMEGA POLISH CHANGES
Problem: Final audit found two risks: a root `Hecton8.Core.PlayerLookTargetPromptCache` collided with the signal namespace helper, and the broad Unity project build is not a trustworthy green signal in the current dirty multi-agent workspace.
Solution: Removed the duplicate signal-namespace cache and hardened the root Core prompt cache with fixed 64-slot storage, linear hash lookup, subsystem reset, and zero per-frame allocations. Re-ran filtered build against all touched script names; no touched-file errors remained. Unity MCP was retried and still failed at `127.0.0.1:8088`.
Rejected Alternatives: Editing generated csproj files to force helper inclusion, retaining two cache classes with fully qualified call sites, or claiming full Unity verification without an Editor connection. Generated csproj churn is brittle, duplicate cache classes create future ambiguity, and fake green reports are not useful.
Scalability potential: Low uses one direct prompt and instant alpha. Middle uses the same buffers with 0.2s dither. High expands atlas quality. Ultra can spend saved CPU on richer per-glyph visual treatment without changing signal transport.
Hardware Impact: Estimated, unprofiled i3/MX350 savings: 18-45 us by avoiding Canvas/TMP object rendering, 3-7 us on Low by snapping alpha and disabling dither, 2-6 us by bounded char cache instead of managed text routing, and 1-3 us by direct scheme/glyph array lookup. No profiler capture was available.
Outside-domain justification: `RepairTool.cs` was touched only to remove `DiegeticTooltipSystem.ActiveRuntimeInstance` and resolve diagnostics through `GlobalRegistry.Renderables`. `GlobalSignals.cs` and `PlayerInteraction.cs` were touched because `PlayerLookTargetSignal` is the cross-domain contract requested by the prompt.
Final tracked diff: `GlobalSignals.cs`, `PlayerInteraction.cs`, `RepairTool.cs`, `DiegeticTooltipSystem.cs`, `Status_CONTEXTUAL_UX_PROMPTER.md`, `Rationale_CONTEXTUAL_UX_PROMPTER.md` show 946 insertions and 410 deletions in the focused tracked diff.
Final untracked files: `Assets/_Project/Scripts/Core/PlayerLookTargetPromptCache.cs`, `Assets/_Project/Scripts/Core/PlayerLookTargetPromptCache.cs.meta`, `Assets/_Project/Art/Shaders/Hecton_DiegeticTooltipIndirect.shader`, `Assets/_Project/Art/Shaders/Hecton_DiegeticTooltipIndirect.shader.meta`, `Assets/_Project/Scripts/UI/Diegetic/Contracts/DiegeticTooltipContracts.cs`, `Assets/_Project/Scripts/UI/Diegetic/Contracts/DiegeticTooltipContracts.cs.meta`.

## Decision 9: Prompt Cache Collision Hardening
Problem: The prompt cache was temporarily using direct `promptHash & 63` placement. That is fast but too brittle for a shared first-hour interaction set because unrelated prompt hashes can collide and force the tooltip back to `"OPEN HATCH"` even when a valid prompt was published.
Solution: Replaced direct placement with bounded lookup, first-free-slot insertion, and deterministic rollover once all 64 slots are occupied. Added subsystem reset to support no-domain-reload play mode. This keeps signal payloads hash-only and keeps render-time prompt staging dictionary-free.
Rejected Alternatives: Direct bitmask placement, a managed dictionary, or moving prompt strings into `PlayerLookTargetSignal`. Direct bitmask is fragile; dictionary lookup violates the hot path rules; managed signal payloads break the unmanaged signal lane.
Scalability potential: Low/Middle/High/Ultra all use the same bounded 64-slot cache. Low still pays only signal-time copy work, not render-time lookup allocations.
Hardware Impact: Expected cost is up to 64 integer compares only when a prompt signal is stored or copied, not per rendered glyph. This is acceptable against the UX correctness gain and still below any visible frame-time threshold. Measured proof absent.

## Decision 10: Indirect Draw Buffer And Shader Hot-Path Hardening
Problem: The icon and text draws shared one instance buffer and one indirect args buffer. That is unsafe because `DrawMeshInstancedIndirect` submission can consume those buffers after the CPU has already overwritten them for the second draw. The shader also used hash dither math and `round()` on a value authored as an integer.
Solution: Split text/icon into separate instance buffers and args buffers, dirty-gated material bindings and dither uniforms, skipped space glyph quads, replaced the hash dither with a 4x4 Bayer LUT, clamped glyph UV table indices, and removed shader `round()`/division notation from the atlas path.
Rejected Alternatives: Keeping one shared buffer pair, forcing draw order assumptions, retaining hash dither because it looks random, or relying on shader out-of-range behavior. Those are not deterministic enough for a first-hour critical prompt.
Scalability potential: Low now skips dither through a uniform branch and submits fewer quads. Middle keeps cheap Bayer dither. High/Ultra keep the same stable buffer topology and can add richer glyph material treatment without changing signal transport.
Hardware Impact: Estimated low-end gain is small but real: one less blank glyph quad for `"OPEN HATCH"`, fewer per-frame material property writes, no per-pixel hash/dot/frac dither, no shader `round()`, and no GPU race from shared indirect buffers. Measured profiler proof absent.

## Decision 11: CPU Billboard Matrix Direct Write
Problem: The renderer still used `Quaternion.LookRotation` and `Matrix4x4.TRS` once per glyph. For a short prompt this is not catastrophic, but it is unnecessary CPU work in a system designed around cheap atlas quads.
Solution: Build the `LocalToWorld` matrix directly from camera right/up/forward vectors and local scale. The quad stays camera-facing, but the hot path avoids quaternion construction and TRS helper work.
Rejected Alternatives: Keeping TRS for readability, adding a more complex GPU-side expansion shader, or batching the whole text into one mesh. TRS costs avoidable CPU; GPU expansion would be a larger shader contract change; mesh batching reintroduces CPU geometry churn.
Scalability potential: Low gets the same readable prompt with less CPU. Middle/High/Ultra can spend the saved CPU on richer material treatment rather than transform helpers.
Hardware Impact: Expected gain is small per glyph but deterministic, especially on weak CPU frames. Measured profiler proof absent.

## Decision 12: Late-Frame Signal Resolve
Problem: The tooltip was consuming `PlayerLookTargetSignal` in the UI `IUpdatable` lane. That is after the player lane, but it is still earlier than the project POST_SIMULATION/LateFrame window and weaker than the prompt's execution-phase requirement.
Solution: Converted `DiegeticTooltipSystem` to `ILateFrameTickable`, registered it with `GlobalRegistry.TryRegisterLateFrameTickable(..., PriorityLayer.UI)`, and resolved look-target signals, AUP shifts, scheme changes, and fade state before `GlobalSignals.ClearPostSimulationSnapshots()`. Rendering remains in `IRenderable.Render` during the SRP camera callback. The duplicated scheme/glyph constants now reference `Hecton8.UI.Diegetic.Contracts`.
Rejected Alternatives: Keeping early UI `IUpdatable`, moving draw work into LateFrame, or reading Unity `Time.deltaTime`. Early UI tick is the wrong phase; drawing in LateFrame misses the render dispatcher; `Time.deltaTime` bypasses dispatcher timing.
Scalability potential: Low still snaps alpha late-frame and submits the same minimal quads. Middle/High/Ultra retain dither and atlas overkill in the render phase without changing signal transport.
Hardware Impact: Runtime cost is roughly neutral; the main gain is correctness and one-frame freshness for current-frame player look signals. Using `SystemDispatcher.CurrentFrameDeltaTime` avoids Unity time reads and keeps timing deterministic. Measured profiler proof absent.

## Decision 13: SRP Camera Submission Gate
Problem: `RenderDispatcher` invokes every `IRenderable` once per SRP camera. The tooltip draw supplies a camera to `Graphics.DrawMeshInstancedIndirect`, so auxiliary camera passes could queue duplicate draws for the interaction camera.
Solution: Added `ResolveRenderCamera()`: resolve the intended interaction camera first, read `GlobalRenderContext.CurrentCamera`, and return `null` when the current SRP camera is not the target. If there is no render context, the previous fallback camera path remains.
Rejected Alternatives: Passing `null` camera to draw in every camera, trusting SRP call order, or moving draw submission to LateFrame. All risk duplicates, wrong camera orientation, or bypassing the established render dispatcher.
Scalability potential: Low avoids unnecessary auxiliary camera submissions. Middle/High/Ultra spend saved render submission budget on glyph treatment rather than duplicate draw queues.
Hardware Impact: Expected gain is scene-dependent: no change in single-camera player builds, but editor/auxiliary camera frames avoid duplicate indirect submissions. Measured profiler proof absent.

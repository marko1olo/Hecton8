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

## Decision 10: Restored Indirect Draw Buffer Separation
Problem: The continuation audit found the tooltip renderer had reverted to one shared instance buffer and one shared indirect args buffer for both icon and text draws. That risks GPU-side overwrite when the backend consumes the first indirect draw after the CPU has prepared the second.
Solution: Restored separate text/icon `ComputeBuffer` instance payloads and separate indirect args buffers. Material bindings now bind the correct buffer per material, and dither uniform writes are dirty-gated.
Rejected Alternatives: Keeping one shared buffer pair, relying on draw submission order, or switching back to Canvas/TMP objects. Shared buffers are not deterministic enough; Canvas/TMP objects violate the prompt and hot-path policy.
Scalability potential: Low submits the minimum icon/text quads without duplicated state. Middle keeps cheap dither fade. High/Ultra can spend saved CPU/GPU budget on atlas/material treatment without changing signal transport.
Hardware Impact: Expected low-end gain is mostly correctness plus fewer redundant material writes. Measured profiler proof absent; no dotnet rebuild was run per user instruction.

## Decision 11: Restored Late-Frame Signal Resolve
Problem: Tooltip signal consumption had reverted to UI `IUpdatable.Tick`, which is dispatcher-owned but earlier than the project VISUAL_SYNC/POST_SIMULATION UI signal pattern.
Solution: Converted `DiegeticTooltipSystem` to `ILateFrameTickable`, registered through `GlobalRegistry.TryRegisterLateFrameTickable(..., PriorityLayer.UI)`, and resolved look-target signals, AUP shifts, scheme changes, and fade state before `GlobalSignals.ClearPostSimulationSnapshots()`.
Rejected Alternatives: Keeping early UI `IUpdatable`, moving draw work into LateFrame, or reading Unity `Time.deltaTime`. Early UI tick is the wrong phase; drawing in LateFrame bypasses SRP ownership; Unity time reads break dispatcher timing.
Scalability potential: Low snaps alpha late-frame. Middle/High/Ultra keep the same render payload and can scale visual quality independently from interaction truth.
Hardware Impact: Runtime cost is roughly neutral; the gain is current-frame signal correctness and no native Unity update method. Measured profiler proof absent.

## Decision 12: Restored Shader And Matrix Hot Path
Problem: The shader had reverted to Bayer division expressions and `round()` on an integer-authored glyph index, and CPU glyph setup had reverted to `Quaternion.LookRotation` plus `Matrix4x4.TRS`.
Solution: Replaced Bayer entries with constants, removed shader `round()`, restored branch-gated dither threshold, and built billboard matrices directly from camera right/up/forward vectors.
Rejected Alternatives: Keeping helper math for readability, adding GPU expansion now, or returning to alpha-blended UI. Helper math costs avoidable CPU; GPU expansion is a larger contract change; alpha blend/floating UI breaks the diegetic depth policy.
Scalability potential: Low gets fewer shader/CPU operations. Middle keeps dither fade. High/Ultra can spend saved budget on richer glyph effects.
Hardware Impact: Expected gain is small per glyph but deterministic on weak CPUs and cheap GPUs. Measured profiler proof absent.

## Decision 13: SRP Camera Fail-Closed Gate
Problem: `RenderDispatcher` invokes renderables per SRP camera. Submitting the tooltip draw with an explicit player camera during auxiliary camera passes can duplicate draw work or orient the prompt from the wrong camera.
Solution: Added `ResolveRenderCamera()` to resolve the intended interaction camera, compare it to `GlobalRenderContext.CurrentCamera`, skip non-target camera passes, and fail closed when no interaction camera is resolved during a render context.
Rejected Alternatives: Drawing through every current camera, passing `null` camera, or trusting authoring order. Those create duplicate submission or wrong camera-facing math.
Scalability potential: Low avoids extra editor/auxiliary submissions. Middle/High/Ultra preserve budget for visual overkill instead of duplicated indirect draws.
Hardware Impact: No change in single-camera player frames; avoids one indirect submission per non-target camera pass in multi-camera/editor contexts. Measured profiler proof absent.

## Decision 14: Low-Tier Cache And Black-Box Cursor Wrap
Problem: The tooltip still read `GlobalRegistry.ScalabilityTierProfileByte` through the `IsLowTier()` helper and advanced the 300-frame black-box ring with a modulo operation. Both were tiny, but they lived in the presentation path the user asked to keep tightening.
Solution: Cache `_lowTierActive` in `OnEnable`, `Start`, and `LateFrameTick`, then make render/layout checks read the local boolean. Replace `_blackBoxCursor = (_blackBoxCursor + 1) % BlackBoxCapacity` with increment plus branch wrap.
Rejected Alternatives: Keeping a registry read inside `IsLowTier()`, keeping modulo for readability, or claiming a project-wide H-Phi improvement from one UI micro pass. The change is scoped and deterministic; global H-Phi remains monitor-owned.
Scalability potential: Low uses the cached tier for snap/no-dither decisions. Middle keeps dither fade. High and Ultra keep identical behavior while avoiding the extra branchless division path in telemetry.
Hardware Impact: Estimated low-end gain is sub-microsecond per tooltip frame, mostly from removing a registry property read and integer modulo from the render-adjacent path. No profiler proof because the user forbade rebuild/runtime verification.

## Decision 15: Render Basis Single Sample
Problem: Icon and text batches each re-read `camera.transform` basis vectors and checked UV-table dirtiness inside `DrawBatch`. That duplicated render-side property access and branch work in the exact two-batch tooltip path.
Solution: Sample camera position/right/up/forward once in `Render`, pass the basis into both indirect batches, resolve XR depth offset from the sampled camera position, and upload dirty UV tables once before submissions.
Rejected Alternatives: Keeping per-batch transform reads, caching a `Transform` across frames, or pushing camera basis through a global singleton. Per-batch reads are waste; cross-frame transform caching risks stale scene ownership; globals violate the prompt isolation policy.
Scalability potential: Low benefits from fewer render-side checks. Middle keeps dither fade. High and Ultra get identical visuals with a cleaner basis handoff for future per-glyph effects.
Hardware Impact: Estimated i3/MX350 gain is sub-microsecond for one icon plus text prompt, with slightly better determinism because both batches share one sampled camera basis. No profiler proof because runtime verification is still pending.

## Decision 16: Resource Readiness Split
Problem: Visible tooltip frames still entered the full resource-object validation path and cold buffer creation used `Marshal.SizeOf<T>()`.
Solution: Split resource-object creation into `EnsureResourceObjects()`, added `_resourceObjectsReady`, and replaced reflection-like stride queries with fixed explicit strides for glyph instances, UV rects, and indirect args.
Rejected Alternatives: Leaving all null checks in the render path or relying on `Marshal.SizeOf` for readability. The struct layout is fixed and already owned here, so explicit stride is cheaper and easier to audit.
Scalability potential: Low avoids repeated object-readiness checks. Middle keeps the same dither fade. High and Ultra preserve CPU budget for richer glyph treatment.
Hardware Impact: Estimated gain is sub-microsecond per visible prompt after warmup, with cleaner cold allocation evidence. No runtime profiler proof.

## Decision 17: Authored Tooltip Materials With MPB Draw Binding
Problem: The tooltip still used runtime `Shader.Find` and `new Material` fallback, directly violating the diegetic UI/URP material mandates.
Solution: Added authored glyph/icon material assets under `Assets/_Project/Resources/UI`, loaded them cold when serialized materials are absent, moved per-draw texture/buffer/dither binding into persistent `MaterialPropertyBlock`s, and added a fail-closed shader-contract check before property blocks are considered ready. Runtime no longer clones materials or searches shaders.
Rejected Alternatives: Keeping runtime material clones, mutating shared material assets directly, or requiring all existing scenes to be manually rewired before the renderer can draw. MPBs are accepted for UI in the mandate and avoid shared asset mutation.
Scalability potential: Low gets the same minimal draw path without material clone allocation. Middle keeps dither fade. High and Ultra can author richer material variants while the renderer still uses the same indirect payload contract.
Hardware Impact: Expected low-end gain is cold-start and memory hygiene, not large frame-time reduction. Removes two runtime material allocations and one shader lookup fallback from the tooltip path.

## Decision 18: Material Readiness Failure Latch
Problem: After the authored material pass, the success path was clean, but missing or mismatched material assets would still re-enter material resolution checks every visible frame.
Solution: Added `_materialsReady`, `_materialResolveAttempted`, and `_materialResolveFailed` gates. Material setup now runs once until success, and authoring failures fail closed once per resource lifetime instead of repeating `Resources.Load`/shader comparisons.
Rejected Alternatives: Rechecking every frame, logging every failure, or falling back to runtime shader/material creation. Rechecking burns hot-path budget; repeated logging allocates/noises; runtime creation violates mandates.
Scalability potential: Low avoids repeated failure work. Middle, High, and Ultra keep the same draw path once materials are ready.
Hardware Impact: Expected gain is small but deterministic: zero repeated material-resolution work after warmup or after a failed material contract. No runtime profiler proof.

## Decision 19: MaterialPropertyBlock Dirty Binding
Problem: The tooltip still cleared and rebound the same `MaterialPropertyBlock` texture, buffer, SDF, and dither properties for every icon/text batch even when the binding state was unchanged.
Solution: Added per-batch bound-state caches for texture, instance buffer, UV buffer, gradient scale, face dilate, and dither flag. `BindPropertyBlockIfDirty` now skips `Clear`/`Set*` calls unless one of those bindings changes; per-instance compute-buffer payload upload remains per visible draw.
Rejected Alternatives: Rebinding every draw for simplicity, mutating shared material assets directly, or moving tint into the property block. Rebinding is avoidable CPU traffic; shared material mutation breaks authored asset ownership; tint must stay per instance.
Scalability potential: Low removes repeated MPB setter work from the normal prompt. Middle keeps dither fade. High and Ultra can add authored material variants or extra per-instance visual treatment without paying redundant binding resets every frame.
Hardware Impact: Expected gain is sub-microsecond per prompt on i3/MX350, but deterministic: after warmup the icon/text batches only rebind when texture, buffer, SDF tuning, or dither tier state changes. No runtime profiler proof.

## Decision 20: Registry-Only Render Camera Resolution
Problem: The tooltip renderer still had a cold fallback through `GameBootstrapper.TryGetCurrentPlayerTransform` and `GetComponentInChildren<Camera>()`, guarded by `Time.unscaledTime`. That is weak for a registry-owned diegetic UI system and leaves a component-search path in the render camera resolver.
Solution: Removed the bootstrap/component-search fallback and retry timer. `ResolveCamera()` now uses only an authored `interactionCamera` or `GlobalRegistry.Player.PlayerCamera`; player service hot-swap clears the cached camera and accepts the new registry camera if present.
Rejected Alternatives: Keeping the bootstrap fallback for convenience, using `Camera.main`, or doing a scene search when the registry is missing. Those hide broken player context wiring and add forbidden discovery work to a presentation system.
Scalability potential: Low fails closed instead of searching scene hierarchy. Middle keeps the same prompt visuals. High and Ultra keep deterministic camera ownership for richer material/effect work without camera-pass ambiguity.
Hardware Impact: Expected gain is cold-path hygiene and lower worst-case stall risk, not a measurable steady-frame win. It removes the component-search fallback and `Time.unscaledTime` retry branch from the tooltip camera resolver. No runtime profiler proof.

## Decision 21: Indirect Args Dirty Count
Problem: The tooltip still uploaded indirect argument buffers every visible icon/text draw even when the instance count was unchanged. For stable prompts, only the glyph payload changes per frame; the args count usually does not.
Solution: Added `_boundTextArgsCount` and `_boundIconArgsCount`, reset them on args-buffer creation and resource release, and changed `DrawBatch` to call `argsBuffer.SetData(_indirectArgs)` only when the batch count changes.
Rejected Alternatives: Updating args every draw for simplicity, splitting one args array per batch without dirty gating, or moving count into shader-side branching. Repeated uploads are unnecessary; shader-side count branching is the wrong layer for indirect draw submission state.
Scalability potential: Low removes redundant CPU-to-GPU args traffic for the single normal prompt. Middle keeps dither fade. High and Ultra preserve upload budget for richer per-instance glyph visuals while keeping indirect draw state stable.
Hardware Impact: Expected gain is sub-microsecond per visible prompt on i3/MX350, with lower driver/API traffic in steady hover. No runtime profiler proof.

## Decision 22: Cached Input Determinism Service
Problem: The tooltip checked input scheme every late frame and still fetched `GlobalRegistry.InputDeterminism` inside the resolver.
Solution: Added `_inputDeterminism`, refreshed it on enable/start and `GlobalRegistryServiceSlot.Input` hot-swap, cleared it on disable, and made `ResolveCurrentSchemeHash()` read the cached interface.
Rejected Alternatives: Polling `GlobalRegistry.InputDeterminism` every scheme check, subscribing to managed input events, or freezing the scheme after first resolve. Registry polling is avoidable; managed events add lifecycle coupling; frozen scheme would break device changes.
Scalability potential: Low removes one registry access from steady hover checks. Middle, High, and Ultra preserve scheme responsiveness for richer glyph/material variants without adding managed routing.
Hardware Impact: Expected gain is sub-microsecond per frame on i3/MX350, but deterministic: scheme checks use a cached interface and hot-swap keeps the cache correct. No runtime profiler proof.

## Decision 23: Render Resource Fail-Closed Gate
Problem: If tooltip buffers or authored materials were unavailable, `Render()` could still resolve camera, anchor, bounds, tint, and telemetry before both batch submissions failed closed.
Solution: Added an immediate `_resourceObjectsReady`, `_materialsReady`, and `_runtimeQuadMesh` gate after `EnsureResources()` so missing resources return before camera/anchor work.
Rejected Alternatives: Letting `DrawBatch` null-check late, logging failures every frame, or creating runtime fallback materials. Late null-checks waste render CPU; repeated logs allocate/noise; runtime fallback materials violate authored material policy.
Scalability potential: Low avoids wasted work in invalid authoring states. Middle, High, and Ultra keep the same visuals once resources are ready and preserve CPU for actual prompt rendering.
Hardware Impact: Expected gain is only in degraded/missing-resource states, but deterministic: no camera/anchor/bounds work when the prompt cannot draw. No runtime profiler proof.

## Decision 24: Render Camera Transform Cache
Problem: Even after registry-only camera resolution, visible prompt frames still read `camera.transform` before sampling position/right/up/forward.
Solution: Cached the camera transform together with the authored/registry camera reference. The cache handles explicit-camera changes, player service hot-swap, and disable cleanup.
Rejected Alternatives: Reading `camera.transform` every render, caching a global transform singleton, or ignoring explicit-camera changes. Per-frame property access is avoidable; globals violate ownership; stale explicit cameras break scene authoring.
Scalability potential: Low removes one render-side native property access. Middle, High, and Ultra keep deterministic camera ownership for richer prompt materials/effects.
Hardware Impact: Expected gain is sub-microsecond per visible prompt frame on i3/MX350. No runtime profiler proof.

## Decision 25: Visible Distance Derived Cache
Problem: Each visible prompt frame recomputed max visible distance squared and the bounds-size expression from the serialized distance value.
Solution: Added `_cachedMaxVisibleDistance`, `_cachedMaxVisibleDistanceSq`, and `_cachedBoundsSize`, refreshed only when `maxVisibleDistance` changes.
Rejected Alternatives: Recomputing every render for simplicity or hardcoding a bounds size. Recompute is tiny but avoidable; hardcoding removes designer control.
Scalability potential: Low removes small repeated math. Middle, High, and Ultra preserve runtime tuning while keeping derived values stable.
Hardware Impact: Expected gain is sub-microsecond per visible prompt frame. No runtime profiler proof.

## Decision 26: Cached Atlas Textures And Render Layer
Problem: The indirect submit path still read serialized font/sprite atlas texture properties at draw time and queried `gameObject.layer` inside each icon/text batch.
Solution: Cached the active font atlas and sprite atlas during layout rebuild, then passed one sampled render layer into both indirect submissions.
Rejected Alternatives: Leaving per-submit property reads for simplicity or caching the layer only at enable time. Repeated property reads are unnecessary; enable-only layer cache can become stale if runtime layer ownership changes.
Scalability potential: Low removes tiny repeated render-side property traffic. Middle, High, and Ultra keep the same atlas contract while freeing budget for richer glyph/material treatment.
Hardware Impact: Expected gain is sub-microsecond per visible prompt frame on i3/MX350; measured proof absent because no rebuild/runtime verification was allowed.

## Decision 27: Authored Diegetic Panel Phosphor Material
Problem: `DiegeticPanelController` still resolved the phosphor compositor through runtime `Shader.Find` and constructed a material in code. That is cold-path work, but it violates the authored-material direction used for diegetic presentation systems.
Solution: Added authored `Resources/UI/MAT_DiegeticPanelPhosphorDecay` and changed phosphor setup to resolve that material once, validate its shader contract, and fail closed if authoring is missing or mismatched.
Rejected Alternatives: Keeping the runtime shader/material fallback, adding another per-panel material clone, or disabling phosphor decay entirely. Runtime lookup hides authoring faults; clones add memory churn; disabling the effect removes the intended CRT persistence cheat.
Scalability potential: Low/MX350 keeps the same cheap RT history fake without runtime material creation. Middle/High/Ultra can tune the authored material or shader while preserving the same validation gate.
Hardware Impact: Expected low-end gain is cold-start and memory hygiene, not a measurable steady-frame win. Removes one runtime shader lookup and one runtime material allocation from the physical panel phosphor path. No profiler proof.

## Decision 28: Registry-Owned Diegetic Panel Camera Resolution
Problem: `DiegeticPanelController` still reached through `GameBootstrapper.TryGetCurrentPlayerTransform` and throttled camera discovery with `Time.unscaledTime`. That creates hidden coupling to bootstrap ownership and a retry branch in a presentation controller.
Solution: Removed the bootstrap fallback and retry timer. Physical panels now resolve an authored `interactionCamera` first, then `GlobalRegistry.Player.PlayerCamera`, and return null when neither active camera exists.
Rejected Alternatives: Keeping the bootstrap fallback for convenience, using `Camera.main`, or searching player children for a camera. Those hide broken player context wiring and add discovery work to a diegetic UI system.
Scalability potential: Low fails closed instead of searching. Middle/High/Ultra keep deterministic camera ownership for richer physical panel effects without camera-source ambiguity.
Hardware Impact: Expected gain is cold-path hygiene and lower worst-case discovery work, not a large steady-frame win. Removes one bootstrap call chain, one component probe, and one retry timer from panel camera resolution. No profiler proof.

## Decision 29: Diegetic Panel Tick Time Cache
Problem: Physical panel `Tick` read Unity unscaled time separately for last-interact state, proxy-light flicker, and queued input timestamps.
Solution: Sampled `SystemDispatcher.CurrentUnscaledTimeSeconds` once per active panel tick into `_tickUnscaledTime`, then reused that value through the panel tick call stack.
Rejected Alternatives: Keeping repeated `Time.unscaledTime` property reads or moving panel timing to a new service. Repeated reads are avoidable; a new timing service would be disproportionate when the dispatcher already publishes frame time.
Scalability potential: Low removes tiny repeated native time reads. Middle/High/Ultra keep deterministic panel timing and can spend saved budget on richer CRT/panel effects.
Hardware Impact: Expected gain is sub-microsecond per active panel tick on i3/MX350. No profiler proof.

## Decision 30: Diegetic Panel Interaction Camera Transform Cache
Problem: After registry camera resolution, distance refresh and ray projection still read `resolvedCamera.transform` directly.
Solution: Cached the resolved interaction camera transform with the authored/registry camera reference, including explicit-camera ownership tracking and disable cleanup.
Rejected Alternatives: Reading `resolvedCamera.transform` in each panel path, caching a global camera transform, or leaving stale explicit camera references after authoring changes. Per-path property reads are avoidable; globals violate ownership; stale explicit cameras break physical panel authoring.
Scalability potential: Low removes small render/input-side property traffic. Middle/High/Ultra keep deterministic camera ownership for richer physical panel projection and CRT effects.
Hardware Impact: Expected gain is sub-microsecond per active physical panel frame and removes a stale-camera edge case. No profiler proof.

## Decision 31: Diegetic Panel Input Service Hot-Swap Cache
Problem: `DiegeticPanelController.EnsureRuntimeState()` still fetched `GlobalRegistry.Input` every active tick, but blindly caching that property can freeze the no-op fallback when the panel starts before the real input dispatcher registers.
Solution: Registered the panel as an `IGlobalRegistryHotSwapListener`, cached `GlobalRegistry.RegisteredInput` when present, kept `_inputAwaitingRegistration` true only while the registry slot is empty, and used hot-swap notifications for later input/player service replacement.
Rejected Alternatives: Polling `GlobalRegistry.Input` forever, subscribing to managed input events, or caching the no-op fallback as final. Forever polling wastes steady-state budget; managed events add lifecycle coupling; stale no-op input breaks panel interaction after startup ordering changes.
Scalability potential: Low removes the steady-state registry read while preserving startup correctness. Middle/High/Ultra keep deterministic input and camera ownership for richer panel projection, CRT, and physical cursor effects.
Hardware Impact: Expected gain is sub-microsecond per active physical panel tick after input registration. Startup-only fallback probing remains until the real service exists; no profiler proof.

## Decision 32: Diegetic Panel Output Material Property Cache
Problem: Phosphor decay forces panel output texture rebinding every late frame, and `ApplyMaterialState()` was repeatedly calling `Material.HasProperty` plus rewriting `_PanelPowerLevel` during those texture-only refreshes.
Solution: Cached panel output material property support when the material reference changes, routed material writes through cached flags, and added `_appliedPanelMaterialPowerLevel` so power is written only when the material needs it.
Rejected Alternatives: Keeping per-refresh `HasProperty` calls, mutating shader keywords, or assuming every authored material has all properties. Repeated property checks waste steady-state budget; shader mutation is unrelated; unchecked property writes would break material variants.
Scalability potential: Low keeps the cheap phosphor-history fake with less CPU/API traffic. Middle/High/Ultra can use richer authored panel materials while the controller pays property discovery only when material ownership changes.
Hardware Impact: Expected gain is sub-microsecond per phosphor-enabled panel late frame on i3/MX350; no profiler proof.

## Decision 33: Diegetic Panel Phosphor Decay Dirty Scalar
Problem: The phosphor composite pass must rebind previous/current RT textures every frame, but `_Decay` was also being set every frame even when the authored decay value did not change.
Solution: Added `_appliedPhosphorDecay`, reset it on phosphor material cache reset, and dirty-gated `_Decay` writes while preserving per-frame RT texture binding and blit order.
Rejected Alternatives: Leaving the scalar write in the per-frame blit path, moving decay into a global material, or skipping texture rebinding. The scalar is stable most frames; global material mutation is unsafe; textures swap every frame and must still be rebound.
Scalability potential: Low reduces API traffic for the same CRT persistence fake. Middle/High/Ultra can spend the saved overhead on richer authored panel effects without changing the compositing contract.
Hardware Impact: Expected gain is sub-microsecond per phosphor-enabled panel late frame on i3/MX350; no profiler proof.

## Decision 34: Diegetic Panel Interface Source Cache
Problem: `ResolveInterfaces()` is called during runtime-state validation and recast the same serialized `MonoBehaviour` references to `IPanelInteractable` and `IPanelPowerSource` every active tick.
Solution: Added cached source references and recast only when `panelInteractable` or `panelPowerSource` changes.
Rejected Alternatives: Removing runtime resolution entirely, caching only in `Awake`, or using scene searches. Runtime overrides still need to work; `Awake`-only would miss injected sources; scene searches violate ownership and cost rules.
Scalability potential: Low removes tiny repeated cast work from active panels. Middle/High/Ultra keep the same extension hooks for richer physical panel receivers and power visualization.
Hardware Impact: Expected gain is sub-microsecond per active physical panel tick; no profiler proof.

## Decision 35: Tooltip Input Determinism No-Op Fallback Guard
Problem: `DiegeticTooltipSystem` cached `GlobalRegistry.InputDeterminism`, which aliases `GlobalRegistry.Input` and can return the no-op fallback before the real input dispatcher registers. First registration from an empty slot does not necessarily produce the hot-swap notification this cache relied on.
Solution: Cache `GlobalRegistry.RegisteredInput` when present, mark `_inputDeterminismAwaitingRegistration` only while the slot is empty, and refresh through that narrow startup path before scheme reads.
Rejected Alternatives: Polling `GlobalRegistry.InputDeterminism` forever, freezing the first no-op fallback, or routing through managed input events. Polling wastes steady-frame budget; frozen no-op breaks glyph selection after startup ordering changes; managed events add coupling.
Scalability potential: Low keeps correct snap/no-dither input glyphs after delayed input registration. Middle/High/Ultra keep dynamic device glyph resolution for richer authored prompt materials.
Hardware Impact: Expected gain is correctness plus sub-microsecond steady-frame hygiene after input registration; no profiler proof.

## Decision 36: Tooltip Scalability Event Cache
Problem: Tooltip late-frame work still refreshed `_lowTierActive` from `GlobalRegistry.ScalabilityTierProfileByte` every frame even though the project has a dispatcher-flushed scalability event lane.
Solution: Implemented `IScalabilityChangedEventListener`, registered with `ScalabilityEvents`, updated `_lowTierActive` from `ScalabilityChangedEvent`, and kept enable/start refresh for cold correctness.
Rejected Alternatives: Per-frame registry polling, ignoring runtime tier changes, or adding a bespoke UI tier signal. Polling is avoidable; ignoring runtime tier changes breaks thermal/battery downgrades; a bespoke signal duplicates the existing lane.
Scalability potential: Low immediately snaps fade and disables dither when the event switches low. Middle/High/Ultra keep fade/dither without polling the registry every late frame.
Hardware Impact: Expected gain is sub-microsecond per active tooltip late frame on weak hardware; no profiler proof.

## Decision 37: Tooltip Active Camera Fail-Closed Gate
Problem: Tooltip camera resolution accepted cached or authored camera references without proving they were active, leaving a path where disabled cameras could still drive render math when the render context did not provide a current camera.
Solution: Require `isActiveAndEnabled` for authored and player cameras, clear inactive cached registry cameras, and keep the SRP current-camera comparison as the final render gate.
Rejected Alternatives: Trusting serialized camera state, using `Camera.main`, or drawing with a null/current fallback. Serialized cameras can be disabled at runtime; `Camera.main` is a scene search; drawing against an arbitrary current camera duplicates or misorients prompts.
Scalability potential: Low avoids duplicate or invalid submissions on weak devices. Middle/High/Ultra keep deterministic camera ownership for richer prompt materials and glyph effects.
Hardware Impact: Expected gain is mostly correctness and avoiding invalid draw attempts; worst-case auxiliary camera passes skip earlier. No profiler proof.

## Decision 38: Tooltip Scheme Read Gating
Problem: `LateFrameTick()` resolved the current input scheme every tooltip tick even when no signal prompt was visible or when diagnostics were masking the signal prompt and no binding icon could use the result.
Solution: Gate scheme refresh to active non-diagnostic signal prompts, and make layout rebuilds refresh the scheme once before resolving the binding icon.
Rejected Alternatives: Keeping unconditional scheme reads, moving scheme reads into render, or freezing the scheme until prompt changes. Unconditional reads waste idle frames; render-time reads couple input to draw submission; frozen schemes miss live device swaps while hovering.
Scalability potential: Low avoids input-service reads during idle tooltip frames. Middle/High/Ultra keep dynamic glyph swaps for richer prompt materials while paying only when a prompt can display them.
Hardware Impact: Expected gain is sub-microsecond per idle tooltip late frame on i3/MX350; no profiler proof.

## Decision 39: Tooltip Render-Path Scheme Read Removal
Problem: `ResolveAnchorPosition()` still had a fallback scheme read during render to decide XR depth offset if `_activeSchemeHash` was zero.
Solution: Make render use cached `_activeSchemeHash` only, refresh the scheme during input hot-swap and diagnostic show, and use the already refreshed scheme for hot-swap layout rebuilds.
Rejected Alternatives: Keeping the render-time fallback, skipping XR depth offset permanently, or moving input reads into `DrawBatch`. Render-time input reads couple device state to draw submission; removing XR offset hurts VR comfort; per-batch reads multiply the problem.
Scalability potential: Low removes input-service work from the draw path. Middle/High/Ultra keep XR comfort and dynamic glyph swaps while preserving render determinism.
Hardware Impact: Expected gain is sub-microsecond on active tooltip render frames when the scheme cache was cold; no profiler proof.

## Decision 40: Tooltip UV Dirty-Gate
Problem: Prompt layout rebuilds rewrote atlas UV table entries and marked the full font/sprite UV compute buffers dirty even when the glyph rects were unchanged.
Solution: Added exact `WriteUvRectIfChanged()` gating for font and sprite UV tables, so the upload flag flips only when a table slot changes.
Rejected Alternatives: Uploading the full UV table after every layout rebuild, keeping a managed set of changed glyph indices, or adding partial buffer uploads now. Full uploads waste API traffic; a managed set violates zero-GC goals; partial uploads are unnecessary for the current 128-slot table and would add complexity.
Scalability potential: Low avoids redundant buffer uploads when repeated prompt layouts reuse the same atlas rects. Middle/High/Ultra keep the same atlas contract and can spend saved CPU/API budget on richer glyph materials.
Hardware Impact: Expected gain is sub-microsecond on i3/MX350 during device hot-swap or prompt layout rebuilds with unchanged atlas rects; no profiler proof.

## Decision 41: Tooltip Normalized-Span Layout
Problem: Prompt staging already normalizes characters into the fixed prompt buffer, but `MeasureAdvance()` and `BuildTextRun()` normalized the same characters again during layout.
Solution: Treat layout input as a normalized private span and read characters directly in measurement/build loops; keep normalization only in `StagePrompt()` and `StagePromptFromHash()`.
Rejected Alternatives: Keeping duplicate normalization for defensive programming, adding a second normalized buffer, or accepting arbitrary raw spans in layout. Defensive duplication costs per layout glyph; a second buffer is unnecessary memory; raw spans would weaken the fixed-buffer contract.
Scalability potential: Low removes small repeated layout work when prompts rebuild. Middle/High/Ultra keep the same glyph atlas path and preserve budget for richer material treatment.
Hardware Impact: Expected gain is sub-microsecond per prompt layout rebuild on i3/MX350; no profiler proof.

## Decision 42: Tooltip Layout Math Consistency
Problem: The tooltip layout path still used `Mathf.Max` for font, text, and sprite clamp math while the surrounding renderer already uses `Unity.Mathematics` primitives.
Solution: Replaced remaining layout clamps with `math.max`, keeping the same clamp values and behavior.
Rejected Alternatives: Leaving mixed math APIs, replacing more unrelated Unity API calls, or folding the clamps into serialized validation. Mixed APIs are needless inconsistency; broader replacements risk touching unrelated behavior; serialized validation cannot protect runtime font/sprite metrics.
Scalability potential: Low keeps clamps cheap and predictable. Middle/High/Ultra preserve identical glyph sizing while keeping the layout path coherent for future vectorized/predictable math.
Hardware Impact: Expected gain is sub-microsecond per layout rebuild on i3/MX350; no profiler proof.

## Decision 43: Tooltip Sprite Asset Local Cache
Problem: `TryResolveBindingIcon()` read `spriteAsset`, `spriteSheet`, and `spriteCharacterTable` through the asset property chain multiple times during binding-icon layout.
Solution: Cache the sprite asset, sheet texture, and character table in locals before count/index access.
Rejected Alternatives: Leaving repeated property reads, caching the sprite table across frames, or adding a dictionary from scheme to sprite character. Repeated reads are avoidable; cross-frame table caching risks stale authored assets; a dictionary violates the integer-index contract.
Scalability potential: Low removes tiny repeated property traffic during layout. Middle/High/Ultra preserve the same TMP sprite atlas contract for richer device glyphs.
Hardware Impact: Expected gain is sub-microsecond per binding-icon layout rebuild on i3/MX350; no profiler proof.

## Decision 44: Tooltip Single-Pass Text Layout
Problem: Tooltip layout measured prompt text with one TMP character lookup loop, then built the same text with a second lookup loop.
Solution: Removed `MeasureAdvance()`. `BuildTextRun()` now builds glyph payloads at zero, returns the final advance, and `OffsetTextGlyphCenters()` shifts completed glyph centers after total icon+text width is known.
Rejected Alternatives: Keeping the duplicate measure/build loops, adding per-character temporary metric arrays, or centering with a transform-scale trick. Duplicate loops waste layout CPU; temporary arrays add memory/state; transform tricks would complicate icon/text batching and bounds.
Scalability potential: Low removes one prompt-length traversal per rebuild. Middle/High/Ultra keep the same atlas visual quality while preserving budget for richer material effects.
Hardware Impact: Expected gain is sub-microsecond per prompt layout rebuild on i3/MX350, larger for long fixed-buffer prompts; no profiler proof.

## Decision 45: Tooltip Advance-Scale Hoist
Problem: `BuildTextRun()` recomputed `glyphScale * glyphAdvanceScale` for every text glyph and read glyph metrics separately around the space/non-space branch.
Solution: Hoisted the advance scale once per run and cached `GlyphMetrics` before branching, reusing it for spaces and visible glyph quads.
Rejected Alternatives: Leaving repeated multiplications, precomputing per-character advances in another table, or changing authored glyph advance scale semantics. Repeated multiplication is avoidable; another table adds cache invalidation; semantics must stay designer-authored.
Scalability potential: Low removes tiny repeated arithmetic from layout. Middle/High/Ultra keep identical spacing while preserving budget for richer prompt treatment.
Hardware Impact: Expected gain is sub-microsecond per prompt layout rebuild on i3/MX350; no profiler proof.

## Decision 46: Tooltip Icon-Scale Hoist
Problem: Binding-icon layout multiplied `glyphScale * IconScaleMultiplier` separately for width and height.
Solution: Hoisted the icon scale once before applying width/height clamps.
Rejected Alternatives: Leaving repeated scalar multiplication, caching icon dimensions across frames, or quantizing icon scale by tier. Repeated multiplication is trivial but avoidable; cross-frame dimension caching risks stale sprite assets; tier quantization would change authored layout.
Scalability potential: Low removes tiny repeated arithmetic from binding-icon rebuilds. Middle/High/Ultra keep the same authored icon sizing and atlas contract.
Hardware Impact: Expected gain is sub-microsecond per binding-icon layout rebuild on i3/MX350; no profiler proof.

## Decision 47: Diegetic Panel Output Texture Dirty Cache
Problem: Physical panel material refreshes could rebind the same `_BaseMap` and `_MainTex` texture during forced refresh paths, especially non-phosphor refreshes where the output RT reference is unchanged.
Solution: Added `_appliedPanelOutputTexture`, reset it on material/RT/phosphor texture ownership changes, and only write texture properties when the resolved output texture reference changes.
Rejected Alternatives: Keeping unconditional forced texture writes, skipping forced refreshes entirely, or caching only phosphor state. Unconditional writes waste API traffic; skipping forced refreshes can miss material changes; phosphor-only caching misses non-phosphor RT refreshes.
Scalability potential: Low avoids redundant material texture API calls on simple panels. Middle/High/Ultra keep phosphor front/back swaps correct because swapped textures still force a reference change.
Hardware Impact: Expected gain is sub-microsecond per forced material refresh on i3/MX350 when the output texture is unchanged; no profiler proof.

## Decision 48: Diegetic Panel Phosphor Material Texture Cache
Problem: The phosphor composite material rebound `_PreviousTex` and `_CurrentTex` every composite frame even when the current panel RT reference did not change.
Solution: Added `_appliedPhosphorPreviousTexture` and `_appliedPhosphorCurrentTexture`; texture properties are written only when their source reference changes, and material/texture release paths reset the cache.
Rejected Alternatives: Keeping unconditional phosphor texture writes, caching only `_CurrentTex`, or skipping `_PreviousTex` updates. Unconditional writes waste API traffic; current-only caching leaves asymmetry; previous texture must still update when front/back swaps.
Scalability potential: Low removes redundant composite material writes for stable current RTs. Middle/High/Ultra preserve phosphor history correctness because swapped previous textures still rebind.
Hardware Impact: Expected gain is sub-microsecond per phosphor composite on i3/MX350; no profiler proof.

## Decision 49: Diegetic Panel Interaction-Distance Hoist
Problem: The desktop panel ray path resolved the same effective interaction distance once for AUP range validation and again for panel ray projection.
Solution: Resolve the clamped interaction distance once per ray tick and pass it into both checks.
Rejected Alternatives: Keeping duplicate clamp calls, caching distance across frames, or removing the AUP range check. Duplicate calls are avoidable; cross-frame caching risks stale designer tuning; the AUP range check is required for floating-origin safety.
Scalability potential: Low removes tiny repeated math from panel interaction. Middle/High/Ultra keep identical interaction behavior while retaining AUP correctness.
Hardware Impact: Expected gain is sub-microsecond per desktop interaction tick on i3/MX350; no profiler proof.

## Decision 50: Diegetic Panel Projection Reciprocal Cache
Problem: Panel projection helpers repeatedly rebuilt inverse canvas/reference sizes even though `RefreshPanelData()` already clamps those values and owns the derived panel math state.
Solution: Cache `InvCanvasSize` and `InvReferenceSize` in `PanelData` during panel transform refresh, then reuse them in canvas-to-world, pixel-basis, and local-hit-to-canvas projection helpers.
Rejected Alternatives: Keeping per-projection reciprocal math, caching global static sizes, or changing the authored reference-resolution contract. Per-projection reciprocal work is avoidable; global state breaks multi-panel ownership; changing reference resolution semantics would risk UI placement.
Scalability potential: Low removes tiny repeated math from simple physical panels. Middle/High/Ultra keep the same projection contract while preserving budget for richer cursor, phosphor, and panel-surface visuals.
Hardware Impact: Expected gain is sub-microsecond per active panel projection on i3/MX350, with the largest benefit on fingertip hover paths that can project every tick; no profiler proof.

## Decision 51: Diegetic Panel Projection Direction Math
Problem: The private panel ray projection helper recomputed ray direction length even for the normalized desktop path that `TryResolveRay()` had already validated, and it rebuilt a panel-normal fallback instead of using the sanitized cached normal.
Solution: Keep direction-length validation only for non-normalized public ray projections, use `_panelData.PanelNormal` from `RefreshPanelData()`, and cache `maxDistanceSq` before the travel-distance comparison.
Rejected Alternatives: Leaving the duplicate dot product, trusting all public rays as normalized, or re-normalizing every ray. Duplicate math wastes the hot desktop path; public rays still need validation; per-call normalization changes distance semantics and adds unnecessary cost.
Scalability potential: Low removes tiny repeated math from cursor projection. Middle/High/Ultra preserve the same physical panel hit behavior while keeping CPU budget available for richer diegetic surface effects.
Hardware Impact: Expected gain is sub-microsecond per desktop ray projection on i3/MX350; no profiler proof.

## Decision 52: Diegetic Panel Cursor Margin Clamp
Problem: The local cursor clamp used serialized margins directly. On very small panels or over-authored margins, clamp min/max bounds could invert and make the physical cursor jump or pin incorrectly.
Solution: Sanitize the cursor margin to `[0, panel half-size]` inside `UpdateCursor()` before constructing clamp bounds.
Rejected Alternatives: Trusting `OnValidate()` only, rejecting tiny panels, or hiding the cursor when margins exceed panel extents. Runtime systems can still mutate serialized values; tiny panels are legitimate UX targets; hiding the cursor would be worse feedback than clamping safely.
Scalability potential: Low keeps stable cursor feedback on compact panels. Middle/High/Ultra preserve exact normal-panel behavior while protecting richer physical cursor effects from bad authoring bounds.
Hardware Impact: Expected cost is sub-microsecond per cursor update on i3/MX350; gain is correctness and fewer pathological cursor snaps, not measured frame time.

## Decision 53: Diegetic Panel Finger-Mode Release
Problem: If a panel switched to `RaycastOnly` while a fingertip press was latched, `TryResolveFingerInteraction()` returned false without emitting an Up event or clearing finger ownership.
Solution: Route the `RaycastOnly` branch through `ResolveFingerRelease()` when a finger press is active, and clear `_activeFingerIndex` when no press is active.
Rejected Alternatives: Letting desktop input overwrite the state, clearing without an Up event, or blocking mode switches during contact. Overwrite is nondeterministic UX; clearing without Up can leave receivers latched; blocking mode switches is too heavy for runtime panel configuration.
Scalability potential: Low gets deterministic release behavior on simple panels. Middle/High/Ultra keep the same finger/raycast hybrid contract without input latches during richer panel mode changes.
Hardware Impact: Runtime cost is a branch only in the finger path; expected gain is correctness, not measured frame time.

## Decision 54: Diegetic Panel Clear-State Release
Problem: `ClearHoverState()` reset desktop and finger pressed flags without notifying the receiver. Losing focus/range, pausing presentation, or disabling the panel during an active press could leave an `IPanelInteractable` latched.
Solution: Add `DispatchReleaseBeforeClear()` and call it before pressed flags and queued input are reset, using the last clamped canvas position for the final Up event.
Rejected Alternatives: Dropping the press silently, requiring receivers to time out, or queueing the release before immediately clearing the queue. Silent drops cause stuck panel state; receiver timeouts are nondeterministic; queueing then clearing loses the event.
Scalability potential: Low gets deterministic release behavior without extra objects. Middle/High/Ultra keep the same event contract while richer panels avoid hidden stuck-input state during cinematic transitions.
Hardware Impact: One guarded branch on clear-state calls; normal hover frames do not pay. The gain is correctness, not measured frame time.

## Decision 55: Diegetic Panel Clear-State Event Ordering
Problem: The clear-state release path could send the synthetic Up event before older queued events if the bounded input queue still held events from a previous tick.
Solution: Split `DispatchInputEvents()` into a bounded overload and drain the queued events in FIFO order before emitting the final clear-state Up.
Rejected Alternatives: Keeping direct Up dispatch first, dropping queued events, or increasing `MaxInputEventsPerTick`. Up-first can invert receiver state; dropping queued events hides input; raising the per-tick cap changes normal-frame budget instead of solving clear-state ordering.
Scalability potential: Low keeps deterministic event order without extra allocations. Middle/High/Ultra preserve richer panel interactions because receivers see the same ordered Down/Hold/Scroll/Up stream even during abrupt state changes.
Hardware Impact: Normal frames keep the same dispatch cap. Clear-state calls can drain up to the existing 16-event ring once, which is bounded and off the steady hover path.

## Decision 56: Tooltip Text-Sink Stale Payload Clear
Problem: The optional world-space TMP validation sink received prompt text through `SetCharArray()` but was not cleared when signal/diagnostic payloads disappeared, leaving stale in-world authoring text.
Solution: Track whether a non-UGUI text sink currently owns payload, capture the sink reference that received it, and clear it once with `SetCharArray(_promptBuffer, 0, 0)` on no-payload, diagnostic clear, hard clear, or missing-font layout paths.
Rejected Alternatives: Assigning `TMP_Text.text`, clearing every frame, or ignoring the optional sink because indirect glyphs are primary. `.text` risks managed string churn; repeated clears waste UI work; stale validation text breaks the diegetic UX during authoring and QA.
Scalability potential: Low avoids stale validation overlays on simple prompts. Middle/High/Ultra preserve the indirect-render path while keeping auxiliary authoring surfaces deterministic.
Hardware Impact: Normal frames pay one boolean branch when no payload is visible; the actual TMP clear runs once per payload loss and allocates no new string.

## Decision 57: Tooltip Culling Authoring Clamp
Problem: Tooltip culling and XR depth offset trusted serialized floats. Bad runtime/serialized values such as NaN, negative visible distance, or negative XR offset could poison bounds/culling math or push XR prompts away from the camera.
Solution: Add finite clamps to the visible-distance cache and clamp VR depth offset to a finite non-negative value before applying it.
Rejected Alternatives: Relying on `[Range]` attributes, mutating serialized fields every render, or ignoring bad values until a NaN dump. Range does not protect runtime mutation; render-time serialization mutation is noisy; preventing NaN/culling poison is cheaper than post-crash analysis.
Scalability potential: Low fails to a predictable compact range. Middle/High/Ultra preserve the same authored defaults while keeping richer tooltip rendering from inheriting invalid culling state.
Hardware Impact: Adds small scalar checks on render-visible paths; prevents expensive bad-state debugging and keeps culling deterministic. No profiler proof.

## Decision 58: Tooltip Black-Box Chronological Dump
Problem: Tooltip black-box telemetry writes into a circular NativeArray, but `DumpBlackBox()` exported raw storage order. After wraparound, the binary file no longer read as the actual last-frame sequence.
Solution: Track valid black-box sample count, write that count into the dump, and export entries oldest-to-newest by starting at `_blackBoxCursor` once the ring is full.
Rejected Alternatives: Keeping raw storage order, sorting by frame on dump, or dumping all 300 slots even before they are valid. Raw order slows post-mortem analysis; sorting allocates/complicates cold path; invalid zero slots obscure first-fault evidence.
Scalability potential: Low/Middle/High/Ultra all get the same bounded 300-frame evidence trail without runtime allocation. Better dumps reduce time spent reproducing rare prompt faults.
Hardware Impact: Normal render path adds one bounded counter increment. Dump path is cold and writes at most the valid telemetry count in chronological order.

## Decision 59: Tooltip Black-Box One-Shot Dump Latch
Problem: A persistent non-finite tooltip anchor could call `DumpBlackBox()` every render after the signal republished, turning a cold crash artifact into repeated disk I/O.
Solution: Add `_blackBoxDumped` as a lifecycle-reset latch. `DumpBlackBox()` exits after the first dump until a valid `RecordBlackBox()` sample proves recovery or the black-box lifetime resets.
Rejected Alternatives: Dumping every bad frame, suppressing all future dumps until scene reload, or clearing the target without evidence. Repeated dumps damage frame pacing; permanent suppression hides later distinct faults; clearing without dump violates black-box requirements.
Scalability potential: Low avoids disk-write spikes during persistent bad signals. Middle/High/Ultra keep the same evidence trail while preserving render budget for richer prompt effects.
Hardware Impact: Normal valid frames pay one boolean clear before telemetry write; fault frames avoid repeated file creation/writes after the first dump.

## Decision 60: Tooltip AUP Shift Wrap-Safe Frame Check
Problem: Tooltip AUP shift consumption used `ShiftFrameId <= _lastAupShiftFrame`, which fails when the unsigned shift sequence wraps and can leave prompts anchored to stale runtime positions.
Solution: Replace the raw comparison with `IsNewAupShift()` using the project-standard unsigned delta check and a zero-id guard.
Rejected Alternatives: Keeping raw ordering, resetting the last frame every origin shift, or applying all shift packets blindly. Raw ordering breaks at wrap; resets can duplicate shifts; blind application double-shifts cached anchors.
Scalability potential: Low keeps prompt anchors correct across long sessions. Middle/High/Ultra preserve stable diegetic prompt placement while richer visual treatment remains independent of floating-origin churn.
Hardware Impact: One unsigned delta check per AUP shift packet; no steady render cost when no shift packets exist.

## Decision 61: Tooltip SDF Scalar Clamp
Problem: Tooltip material dirty binding compared `gradientScale` and `faceDilate` directly. Runtime-mutated NaN or out-of-range values could bypass the cache forever and push invalid SDF constants into the glyph shader.
Solution: Resolve both SDF tuning scalars through finite clamps before dirty comparison and property-block upload. Range attributes now share constants with the runtime clamps.
Rejected Alternatives: Trusting `[Range]`, mutating serialized values during render, or clearing the property block every draw. Range does not protect runtime mutation; serialized mutation is noisy; unconditional clearing wastes steady render budget.
Scalability potential: Low fails to stable default SDF values if authoring is bad. Middle/High/Ultra keep richer glyph sharpness control while invalid data cannot poison the draw path.
Hardware Impact: Two finite clamp checks only when the property-block bind path runs; expected gain is avoiding repeated property-block churn and invalid shader constants after bad authoring. No profiler proof.

## Decision 62: Tooltip Layout/Fade Scalar Clamp
Problem: `glyphWorldHeight`, `glyphAdvanceScale`, and `fadeDurationSeconds` were used directly in layout and alpha math. Runtime-mutated NaNs could create invalid glyph geometry or make `_visibleAlpha` non-finite before the anchor black-box guard noticed anything.
Solution: Add shared default/min/max constants and resolve all three scalars through finite clamps before glyph-scale, icon-gap, advance, and fade calculations.
Rejected Alternatives: Trusting inspector `[Range]`, clamping only in `OnValidate`, or clearing the prompt after NaN alpha appears. Runtime mutation bypasses inspector range; `OnValidate` is editor-only; preventing invalid geometry is cheaper than trying to recover after shader payloads are poisoned.
Scalability potential: Low keeps prompt geometry compact and predictable after bad authoring. Middle/High/Ultra retain authored sizing/fade control while invalid values fall back to stable defaults.
Hardware Impact: Three scalar checks on layout/fade paths; expected gain is correctness and avoiding invalid indirect payloads rather than measurable frame-time savings. No profiler proof.

## Decision 63: Panel Effect Scalar Clamp
Problem: Physical panel phosphor decay, depth fade, damage-glitch duration, and proxy-light tuning trusted runtime floats. NaNs could bypass material dirty checks or enter proxy-light registry payloads.
Solution: Add finite resolver helpers for those scalar lanes and route material/property/registry writes through the sanitized values. Public phosphor overrides now use the same resolver as the composite pass.
Rejected Alternatives: Relying on editor validation, clearing the effect when a NaN appears, or writing every property unconditionally. Runtime mutation bypasses editor validation; clearing effects hides authoring faults; unconditional writes waste the steady panel path.
Scalability potential: Low keeps CRT/panel feedback stable on weak devices. Middle/High/Ultra keep richer phosphor, glare, glitch, and proxy-light visuals without accepting invalid scalar payloads.
Hardware Impact: Small finite checks on effect update paths; expected gain is preventing repeated dirty-check failure and invalid registry/material writes, not measurable frame-time savings. No profiler proof.

## Decision 64: Panel Power And Glare Finite Inputs
Problem: External panel power sources and flashlight glare could provide NaN values. Those values could propagate into material writes, powered-state tests, and proxy-light intensity math.
Solution: Resolve power through a finite saturate helper that fails invalid source values to off, and resolve glare through a finite saturate helper that fails invalid glare to zero.
Rejected Alternatives: Trusting every power source, clamping only serialized glare, or suppressing proxy-light registration after NaN reaches intensity. Source contracts can drift; serialized clamps do not protect runtime calls; late suppression still leaves material state vulnerable.
Scalability potential: Low avoids invalid light/material payloads on weak devices. Middle/High/Ultra keep the same richer panel lighting while invalid inputs collapse to deterministic non-emissive states.
Hardware Impact: Two finite saturate checks on state-change/effect paths; correctness gain only, no frame-time saving claimed.

## Decision 65: Panel Damage-Glitch Duration Cap
Problem: `TriggerDamageGlitch()` accepted arbitrary finite durations through public calls. A bad caller could pin CRT glitch visuals for minutes or longer, bypassing the authored one-second maximum.
Solution: Clamp public finite durations to the same `[0.02, 1]` second range used by the serialized authoring field.
Rejected Alternatives: Trusting all callers, only clamping `ReceiveDamage()`, or adding a separate timeout watchdog. Public callers are not all trustworthy; `ReceiveDamage()` is not the only entry point; a watchdog adds state for a scalar validation problem.
Scalability potential: Low avoids stuck glitch effects on cheap devices. Middle/High/Ultra keep dramatic CRT damage feedback while preventing invalid duration latches.
Hardware Impact: One clamp on damage-glitch trigger only; no steady-frame cost.

## Decision 66: UI Runtime Resources.Load Purge
Problem: Tooltip and panel material resolution still used `Resources.Load` fallback paths. `AGENTS.md` and the asset lifecycle mandate forbid first-party runtime Resources loading because it hides dependency ownership and can hitch.
Solution: Removed those fallbacks. Tooltip glyph/icon materials and panel phosphor material now resolve only from authored serialized references and fail closed if missing or shader-mismatched.
Rejected Alternatives: Keeping a cold fallback, moving the load behind a latch, or introducing Addressables from this UI pass. A latched fallback is still a forbidden hidden runtime load; Addressables would widen ownership and asset-group scope.
Scalability potential: Low avoids unexpected sync disk work. Middle/High/Ultra keep deterministic authored material ownership for richer glyph and phosphor visuals.
Hardware Impact: Removes synchronous runtime asset lookup risk; no microsecond saving claimed without profiler capture.

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

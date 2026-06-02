Status: STATIC_COMPLETE_BUILD_BLOCKED_BY_CONTENTION
Agent: 1611

Problem: Main menu/pause UI may still depend on flat screen-space presentation, default UI raycasting, and instantaneous state changes.
Solution: First prove actual source/scene state, then replace only the required menu-domain pieces with world-space panels, cached ray-to-panel math, explicit camera transition DTOs, and shader-side presentation fakes.
Rejected Alternatives: Blind scene YAML mutation, direct dependency on absent systems from sibling agents, DOTween/Cinemachine assumptions, and black loading screen handoff.
Scalability potential: Low = static world-space terminal, cheap raycast, minimal scanlines. Middle = stronger CRT flicker, fog/DoF bounded by GlobalQualityWeight. High = richer glitch, higher panel resolution, cinematic camera parallax. Ultra = visual overkill on menu only, damped before prologue.
Hardware Impact: i3/MX350 target requires O(1) raycast math, no GraphicRaycaster hot sweep, no per-frame managed allocations, and no expensive compile runs while cluster load is active.

Decision 0:
Problem: Prompt extraction initially returned PROMPT_NOT_FOUND.
Solution: Use attribute-aware regex matching AGENT_PROMPT where id="1611" appears anywhere in the opening tag.
Rejected Alternatives: Neighbor prompt context, exact literal `<AGENT_PROMPT id="1611">` parser.
Scalability potential: Prevents prompt bleed across 20+ agents; no runtime impact.
Hardware Impact: Static CLI scan only; no Unity import/build.

Decision 1:
Problem: 01_MAIN_MENU contained a Canvas configured as Screen Space Camera and an enabled GraphicRaycaster, which violates diegetic UI ownership.
Solution: Convert the scene Canvas to RenderMode.WorldSpace, disable GraphicRaycaster, set 1920x1080 rect at 0.00105 m/px, and add cold runtime enforcement through DiegeticMenuCanvasUtility.
Rejected Alternatives: Keeping Screen Space Camera for convenience; creating a fake terminal mesh dependency that does not exist in scene; relying on import-time editor mutation only.
Scalability potential: Low = static world-space panel with cheap collider. Middle = same panel with stronger existing shader scanlines. High = richer camera parallax. Ultra = visual overkill from shader/lighting without changing UI truth.
Hardware Impact: Removes GraphicRaycaster hot sweep; i3/MX350 gets predictable O(1)-style panel route instead of Unity UI traversal.

Decision 2:
Problem: Menu state changes were visual alpha swaps with no camera embodiment.
Solution: Add MenuCameraController with cubic Bezier position interpolation and normalized quaternion interpolation advanced in LateFrameTick. Main, Saves, Settings, Loading, and Handoff routes are explicit.
Rejected Alternatives: Linear interpolation, camera cuts, DOTween, Cinemachine dependency.
Scalability potential: Low = short parallax. Middle = wider drift. High/Ultra = GlobalQualityWeight expands parallax and handoff drama continuously.
Hardware Impact: Estimated 3 us active-frame solve; no jobs, no allocations, no hidden Complete.

Decision 3:
Problem: Button interaction had to survive world-space conversion without Unity GraphicRaycaster.
Solution: Add DiegeticMenuRaycastReceiver as IPanelInteractable. It caches Button[96] cold, maps canvas hit points to RectTransform space, invokes only matching down/up target, and clears fail-closed on null/miss/inactive groups.
Rejected Alternatives: EventSystem.RaycastAll, GraphicRaycaster, per-frame GetComponents, LINQ.
Scalability potential: Low = 96 fixed button cap. Middle/High/Ultra = same truth route; visual fidelity changes do not affect click ownership.
Hardware Impact: Worst case scans 96 cached references; no managed collection growth on MX350-class systems.

Decision 4:
Problem: UI clicks felt head-locked and weightless.
Solution: Publish HapticRequest and AcousticPingSignal from the physical RectTransform center. RuntimeOriginRoute converts button world position to AUP; invalid origin fails closed.
Rejected Alternatives: String events, HectonEventBus managed path, direct SpatialAudioManager scene lookup in hot input.
Scalability potential: Low = micro haptic + small acoustic radius. Middle = stronger click ping. High/Ultra = downstream audio can occlude/muffle by position without changing UI code.
Hardware Impact: Estimated 4-5 us only on hover target changes and clicks, zero continuous audio polling.

Decision 5:
Problem: 01_MAIN_MENU -> 01_ORBIT would otherwise use the existing world-only transition gate.
Solution: Extend SceneRuntimeService cinematic predicate to accept OrbitSceneName and replace linear camera pan with cubic Bezier plus bounded heave.
Rejected Alternatives: Black loading screen, immediate SceneManager activation, screen fade as the sole visual.
Scalability potential: Low = simple descent. Middle/High = dither and drone crossfade. Ultra = heavier menu-only effects can taper through existing transition.
Hardware Impact: Estimated 4 us/frame during transition; no prologue dependency invented.

Decision 6:
Problem: Prompt requested JSON reports and builds, but user explicitly rejected unread dumps and forbade dotnet build after small edits.
Solution: Use Docs/Tasks/Status_1611.md, Docs/AgentLogs/Rationale_1611.md, and LOG_1611.md as primary proof. Sample CPU/compiler state before build; block build because CPU=62 and dotnet pid=31512 exists.
Rejected Alternatives: Running dotnet build under load; producing JSON as authoritative output.
Scalability potential: Protects 20+ parallel agents from compile contention.
Hardware Impact: Avoided a full project build while host compiler process was already active.

Decision 7:
Problem: APEX integrator verification required proof that menu refactor did not create hot dependency lookups, unsafe phase presentation, or DataVault write-lock nesting.
Solution: Added ValidateApexIntegratorProtocol() to DiegeticMenu1611SmokeTester and ran comment-stripped static scans over modified runtime files plus a broad Scripts hot-method pass. Presentation motion remains LateFrameTick-only; Tick transfers a float delta and cached flags only.
Rejected Alternatives: dotnet build under CPU=100, runtime JSON proof files, and trust-based checklist closure.
Scalability potential: Low/Middle/High/Ultra all keep the same zero-GC route; quality scales visuals, not ownership or lookup cadence.
Hardware Impact: Prevents per-frame registry/component search and build contention; i3/MX350 path stays cache-only while high-end can spend saved time on CRT/noir presentation.

Decision 8:
Problem: Pause menu camera spline advanced from SystemDispatcher.CurrentFrameDeltaTime, which becomes zero when time dilation pauses gameplay. This could freeze diegetic menu camera motion exactly when the pause menu is open.
Solution: Accumulate _pauseMenuPresentationDeltaTime from UnscaledFastTick and consume it in LateFrameTick, with a capped CurrentFrameUnscaledDeltaTime fallback. Added APEX smoke assertion rejecting scaled pause presentation delta.
Rejected Alternatives: Driving camera from UnscaledFastTick, using Unity Time.unscaledDeltaTime directly in LateFrameTick, or leaving the route scaled because it passes token scans.
Scalability potential: Low = pause camera still moves on weak hardware. Middle/High/Ultra = same timing truth, richer visual style can run without changing phase ownership.
Hardware Impact: One float accumulator and one clamp per unscaled tick; no allocation, no registry lookup, no extra scene query.

Decision 9:
Problem: DiegeticPanelController.LateFrameTick manually reset _applyingLateFramePresentation. A thrown editor/dev exception inside panel view, cursor, proxy light, or material flush could leave the flag stuck true and poison later presentation ownership.
Solution: Wrap the late-frame presentation body in try/finally and add APEX source assertion that the flag is reset in finally.
Rejected Alternatives: Assuming no exception path, adding broader catch/log noise, or moving presentation work out of LateFrameTick.
Scalability potential: Low/Middle/High/Ultra get deterministic cleanup independent of visual complexity; richer ultra effects can fail closed in dev without corrupting phase state.
Hardware Impact: try/finally is no allocation and negligible on the non-throw path; prevents expensive state corruption debugging on weak machines.

Decision 10:
Problem: DiegeticMenuRaycastReceiver.ReceiveCanvasInput called ResolveButton, which called IsButtonEligible, which walked parent Transforms and used TryGetComponent<CanvasGroup> during hover/down/up. The direct method token scan missed this helper dependency path.
Solution: Replace hot hierarchy probing with cold fixed-array metadata: Button[96], RectTransform[96], flattened CanvasGroup[96 * 8], and byte group counts. Input now resolves visibility by index and never searches components in the hot helper graph. Strengthen the editor smoke verifier to scan raycaster helpers and require real method declarations, not call-site matches.
Rejected Alternatives: Leaving CanvasGroup checks uncached because button count is small; globally banning TryGetComponent in the entire file, which would reject legitimate cold cache construction; using EventSystem/GraphicRaycaster as fallback.
Scalability potential: Low = same 96-button cap with no hierarchy search. Middle/High/Ultra = richer CRT/audio/haptic presentation can scale independently while click ownership remains cache-only.
Hardware Impact: Removes per-input parent component lookup from MX350/i3 path. Worst-case ray hit remains fixed-index button/rect/group scans with no managed allocation.

Decision 11:
Problem: The cold button cache retained old references when RebuildButtonCache found fewer buttons than the previous build. Also, more than eight parent CanvasGroups were silently truncated, which could ignore a hidden or non-interactable ancestor and let a deep UI chain click through.
Solution: Clear all previously cached Button, RectTransform, and flattened CanvasGroup references before rebuilding. Track CanvasGroup overflow with a byte sentinel and make IsButtonEligible return false on overflow. Add editor smoke coverage for CanvasGroup overflow fail-closed behavior.
Rejected Alternatives: Raising the CanvasGroup cap, which hides authoring mistakes and adds hot scan cost; accepting stale references because menu topology is usually static; doing dynamic parent scans in the hot path.
Scalability potential: Low = deterministic fixed cache with stale-reference cleanup. Middle/High/Ultra = same input truth while visual richness scales elsewhere.
Hardware Impact: Cold rebuild clears at most 96 buttons and 768 group slots. Hot path remains unchanged fixed-index reads; no extra work on compact hardware.

Decision 12:
Problem: MainMenuController locks panel CanvasGroups during fade/spline transitions, but PauseMenuController ShowSection made the new section interactable immediately while the pause camera route was still active. Rapid input could select or trigger controls on a section before the camera finished moving to that physical screen.
Solution: Add a pause-section interaction gate. ShowSection makes the section visible, starts the camera route, then locks the active section CanvasGroup. LateFrameTick advances the camera and releases the gate only after MenuCameraController.IsActive is false.
Rejected Alternatives: Adding a fixed timer independent of spline state; disabling the whole pause canvas; moving input lock into UnscaledFastTick before presentation settles.
Scalability potential: Low/Middle/High/Ultra keep identical authority timing. Camera path duration/visual richness can scale later without changing interaction truth.
Hardware Impact: One bool check and one CanvasGroup reference resolve in LateFrameTick while a gate is active. No allocation, no registry lookup, no component search.

Decision 13:
Problem: DiegeticPanelController.LateFrameTick protected the pending presentation flush with _applyingLateFramePresentation, but the earlier AdvancePanelInteractionPresentation call also mutates cursor/panel presentation state. The flag did not cover the full visual-sync body.
Solution: Move _applyingLateFramePresentation = true before AdvancePanelInteractionPresentation and keep the existing finally reset. Extend smoke validation to reject flag placement after the advance call.
Rejected Alternatives: Keeping the flag only around flushes; adding a second flag; moving input projection into a different phase without a broader contract rewrite.
Scalability potential: Low/Middle/High/Ultra get one coherent presentation guard independent of CRT/phosphor/proxy-light complexity.
Hardware Impact: No new hot cost beyond the existing try/finally scope. The change removes a phase-state ambiguity, not CPU.

Decision 14:
Problem: RebuildButtonCache cleared arrays but left _hoverButtonIndex and _pressedButtonIndex alive. After a hierarchy rebuild, the same integer slot could refer to a different Button, allowing stale hover/press ownership to survive.
Solution: Add ClearInteractionState to reset hover, press, and EventSystem selection before repopulating the fixed cache. Add editor smoke source for Down -> Rebuild -> Up not clicking, followed by a normal click still working.
Rejected Alternatives: Comparing cached Button object identity on every hot input event; keeping state because menu rebuilds are cold; relying on IsButtonEligible to catch stale ownership.
Scalability potential: Low/Middle/High/Ultra keep deterministic cache ownership. Rebuild cost stays cold and bounded.
Hardware Impact: One cold EventSystem selection clear and two int resets per rebuild. Hot input path unchanged.

Decision 15:
Problem: MenuCameraController.Advance accepted non-finite delta. A single NaN could poison _elapsed, make t non-finite forever, and keep the route active indefinitely, blocking interaction gates and camera handoff sequencing.
Solution: Sanitize unscaledDeltaTime through math.isfinite and repair non-finite _elapsed before integration. Add APEX smoke source assertion for both guards.
Rejected Alternatives: Relying on SmoothStep01 to mask NaN visually; clamping only t while leaving _elapsed poisoned; deactivating the route on NaN and snapping the camera.
Scalability potential: Low/Middle/High/Ultra get the same deterministic camera route; visual overkill cannot deadlock on bad timing input.
Hardware Impact: Two finite checks in active camera advance. Negligible relative to transform write; prevents route stalls.

Decision 16:
Problem: The spline completion branch deactivated the route after writing the eased Bezier/nlerp pose. Position math reaches the target at t=1, but nlerp can still normalize into a non-bit-exact quaternion. Physical menu anchors and pause-section interaction gates should close on exact authored target state, not "within tolerance."
Solution: On t >= 1, write _targetPosition/_targetRotation directly, then clear _active and return before the interpolated SetPositionAndRotation path. Add editor smoke source assertion that the target snap precedes _active = false.
Rejected Alternatives: Trusting tolerance-based drift tests only; snapping by recomputing ResolveRoutePose in Advance; adding an epsilon threshold that could end routes early.
Scalability potential: Low/Middle/High/Ultra keep identical camera truth. Stronger high-tier parallax still lands on exact physical screens.
Hardware Impact: One final-frame branch and transform write. No allocation, no registry lookup, no extra steady-state cost.

Decision 17:
Problem: DiegeticMenu1611SmokeTester used cold GetComponent<Button>() in editor-only setup. It was not a runtime violation, but Unity MCP validate_script emitted a warning, weakening the proof artifact.
Solution: Replace both smoke Button lookups with TryGetComponent and explicit InvalidOperationException failures.
Rejected Alternatives: Ignoring the warning because the code is editor-only; suppressing diagnostics; adding broader reflection-based component checks.
Scalability potential: Editor proof stays deterministic and strict without changing runtime paths.
Hardware Impact: Editor-only cold setup; 0 runtime impact.

Decision 18:
Problem: SceneRuntimeService still created a Screen Space Overlay canvas for the transition blackout/boot layer. The scene itself was diegetic, but the start-game handoff reintroduced a flat overlay exactly at the highest-visibility moment.
Solution: Convert the transition overlay to a WorldSpace canvas. Cache its RectTransform/Canvas, place it in front of the current cinematic camera each transition tick, and cold-resolve the loaded scene camera once via preallocated scene-root/camera lists before the dissolve phase.
Rejected Alternatives: Keeping Screen Space because it is reliable across scenes; using Camera.main every dissolve frame; making the overlay a child of the menu camera, which would die when 01_MAIN_MENU unloads; adding a new global camera registry route.
Scalability potential: Low = single world-space dither plane and boot text. Middle = same plane with existing IGN dither. High/Ultra = stronger material/post presentation can happen without changing transition ownership.
Hardware Impact: Per-frame transition cost is cached camera transform, FOV/aspect math, and one RectTransform pose/scale write. Cold camera resolution scans scene roots once after load using reused List buffers.

Decision 19:
Problem: World-space transition overlay construction assigned RectTransform/Canvas/CanvasGroup fields before the final root reference. If a child Image or TextMeshProUGUI creation step failed, later transition code could retain references to a destroyed partial overlay and a leaked dither material.
Solution: Add AbortTransitionOverlayCreation for all partial-construction exits. It destroys the root, clears overlay object references, and destroys the transition dither material. EndMainMenuCinematicTransition now uses the same cleanup primitives.
Rejected Alternatives: Trusting Unity GameObject construction to never fail; leaving cleanup duplicated in EndMainMenuCinematicTransition; assigning _transitionOverlayRoot early and relying on later teardown.
Scalability potential: Low/Middle/High/Ultra keep the same world-space dither plane behavior. Failure cleanup does not alter quality scaling, only prevents stale presentation ownership.
Hardware Impact: Cold-path only. No active-frame cost. Prevents material/reference leaks during scene handoff authoring failures on low-memory machines.

Decision 20:
Problem: The menu camera used GlobalQualityWeight, but SceneRuntimeService handoff still held constant transition dither and heave. That meant the prologue bridge did not continuously taper menu-only visual overkill toward the gameplay quality scalar.
Solution: Add UpdateTransitionVisualOverkill01 and ResolveGlobalQualityWeight01. SceneRuntimeService now consumes HomeostasisBrain.GlobalQualityWeight and damps only local transition presentation: dither coverage scale and cinematic heave amplitude.
Rejected Alternatives: Writing _H8GlobalQualityWeight directly from SceneRuntimeService, which would steal ownership from HomeostasisBrain/ScalabilityDictator; binary low/high quality tiers; adding a new post-process/volume dependency outside the menu domain.
Scalability potential: Low = reduced dither density and steadier camera heave through handoff. Middle = moderate dither/heave. High/Ultra = full menu overkill at start, tapering smoothly to gameplay scalar by the dissolve.
Hardware Impact: Two finite checks, one lerp, and two scalar multiplies during active handoff only. No allocations, no registry lookup, no scene search.

Decision 21:
Problem: Transition visual-overkill damping recomputed against the current normalized phase. During dissolve, normalized restarts at zero, which could push the local scalar upward toward 1.0 after the menu dive already damped it down.
Solution: Clamp the scalar monotonically with math.min(_transitionVisualOverkill01, desiredVisualOverkill01). The handoff may spend less visual overkill as it approaches gameplay, but it cannot rebound once the async/dissolve phase starts.
Rejected Alternatives: Sharing one normalized value across both async routines, which would entangle load progress with presentation timing; binary quality cutover; writing the global quality owner.
Scalability potential: Low = stable reduced dither/heave through dissolve. Middle = gradual transition without rebound. High/Ultra = strong menu effects at start that taper one-way into gameplay.
Hardware Impact: One min operation during active handoff. Prevents a low-end machine from getting a surprise second full-strength dither/heave burst when the new scene activates.

Decision 22:
Problem: SceneRuntimeService async load and dissolve routines directly applied camera, world-space overlay, dither material, terminal text, and drone crossfade while Awaitable.NextFrameAsync drove scene loading. That mixes simulation/load progress with presentation writes outside VISUAL_SYNC.
Solution: Make SceneRuntimeService implement ILateFrameTickable. Async routines now queue only finite scalar fields. LateFrameTick consumes those fields once and applies all camera/overlay/material/audio-visual presentation after the frame state has settled.
Rejected Alternatives: Leaving direct presentation calls in async loops; scheduling a managed event; allocating DTOs; using Update as a presentation bridge.
Scalability potential: Low = exact same cheap world-space plane and scalar fields. Middle/High/Ultra = richer transition visuals can be added behind the same LateFrameTick presentation gate without changing load truth.
Hardware Impact: Active transition adds fixed scalar field writes in async and one LateFrameTick apply. No managed allocation, no collection growth, no scene search, no GetComponent, and no DataVault lock.

Decision 23:
Problem: 01_MAIN_MENU contains settings Toggles and Sliders, but the physical receiver only cached Button targets. After GraphicRaycaster removal, those controls were visible on the terminal but not physically operable.
Solution: Replace the button-only cache with a fixed Selectable[128] cache and typed side caches for Button/Toggle/Slider. Shared RectTransform and CanvasGroup metadata remain cold-cached; slider drag maps panel-space hit coordinates to normalized values.
Rejected Alternatives: Re-enabling GraphicRaycaster for settings, adding per-control Unity event synthesis, or scanning components during input. All three reintroduce hot traversal or allocation risk.
Scalability potential: Low = 38 current controls fit in fixed cache and stay tactile. Middle = same input truth with stronger CRT/audio. High/Ultra = richer visual overkill around controls without changing interaction ownership.
Hardware Impact: Worst-case hit test rises from 96 buttons to 128 selectables, still fixed-index and allocation-free. MX350/i3 avoids GraphicRaycaster traversal while settings remain usable.

Decision 24:
Problem: Empty-panel Down events reset pressed state but still published haptic feedback. That is not a compile failure, but it makes misses feel like valid physical hits and weakens fail-closed interaction semantics.
Solution: Add an early targetIndex < 0 return before PublishHaptic in ReceiveCanvasInput, and add an editor smoke source assertion that guard and return precede the haptic call.
Rejected Alternatives: Leaving the miss haptic as "ambient terminal feel"; routing miss haptics through a separate signal; adding runtime logging. The correct diegetic behavior is silence on invalid physical contact.
Scalability potential: Low/Middle/High/Ultra get the same truth route: valid controls produce tactile feedback, empty terminal space does not. Visual richness can still scale independently.
Hardware Impact: One branch in the Down path. Saves unnecessary SignalBus publication on misses and avoids false haptic work on weak devices.

Decision 25:
Problem: The cold CanvasGroup cache skipped the root canvas transform and IsControlEligible ignored CanvasGroup.blocksRaycasts. That could let a root-level section lock or transition gate look disabled to UGUI but remain clickable through the physical receiver.
Solution: Include the root canvas transform in CacheCanvasGroups, stop after caching it, and fail closed on alpha, interactable, or blocksRaycasts=false. Added smoke coverage that verifies blocksRaycasts=false blocks a click and blocksRaycasts=true recovers after cache rebuild.
Rejected Alternatives: Trusting section code to use interactable only; adding a hot parent walk; re-enabling GraphicRaycaster for CanvasGroup semantics. Cold-cached root group ownership is the narrow fix.
Scalability potential: Low = root gates reliably lock simple panels. Middle/High/Ultra = richer camera transitions and effects can use CanvasGroup locks without changing physical input code.
Hardware Impact: One extra cached CanvasGroup slot per control when a root group exists and one bool read in eligibility. No allocation, no hot GetComponent, no scene search.

Decision 26:
Problem: MainMenuController consumed cancel signals into _cancelRequested during transitions, scene loads, save/load busy windows, or debounce, but returned without clearing the flag. A blocked cancel could fire after the camera settled and auto-close the next panel.
Solution: HandleCancelInput now returns early only when there is no request. Blocked states consume and clear _cancelRequested before returning; valid states clear it before routing to panel actions.
Rejected Alternatives: Keeping deferred cancel as user-friendly buffering; adding another timestamp queue; clearing only on transition end. The contract says ignore during transitions, not replay after them.
Scalability potential: Low/Middle/High/Ultra get deterministic input gating independent of camera route duration or quality-scaled presentation.
Hardware Impact: One bool branch in Tick. Prevents accidental panel churn and avoids extra modal/transition work caused by stale input.

Decision 27:
Problem: MainMenuController.RefreshSelectionIfNeeded applied EventSystem selection from Tick. Selection/highlight is presentation state and should execute after simulation/input and panel/camera phase has settled.
Solution: Tick now only consumes input and transfers _panelTransitionDeltaTime. LateFrameTick runs panel transition, camera advance, style/concept sync, then RefreshSelectionIfNeeded.
Rejected Alternatives: Leaving selection in Tick because it is cheap; moving all input to LateFrameTick; adding a managed event. A bool flag transfer is enough.
Scalability potential: Low = stable highlight after section swap. Middle/High/Ultra = richer visual highlights can be layered later behind the same LateFrame gate.
Hardware Impact: No added work. Same selection call moved to VISUAL_SYNC timing.

Decision 28:
Problem: DiegeticMenuRaycastReceiver.UpdateHover wrote EventSystem.SetSelectedGameObject directly from the physical input callback. That mixes input truth with visual focus presentation.
Solution: UpdateHover now only stores a fixed int pending selection index and publishes hover audio. MainMenuController and PauseMenuController call FlushPendingSelection in LateFrameTick to apply EventSystem selection.
Rejected Alternatives: Removing EventSystem selection entirely; applying it immediately because hover feels responsive; allocating an event payload. Fixed int transfer preserves responsiveness without phase contamination.
Scalability potential: Low/Middle/High/Ultra keep identical input truth. Visual hover/selection effects can scale later while input remains zero-GC and cache-only.
Hardware Impact: One int write on hover target change and one LateFrame guard. No allocation, no hot component lookup, no managed event.

Decision 29:
Problem: Panel_ModalConfirm keeps non-selectable Graphic raycast targets for its backdrop/window. With Unity GraphicRaycaster disabled and the physical receiver only resolving Selectable controls, a click outside modal buttons could fall through to a lower menu control.
Solution: Cache a combined visual raycast stack in DiegeticMenuRaycastReceiver: Selectable controls and non-interactive Graphic blockers. ResolveControlIndex scans from topmost to bottommost; an eligible blocker returns miss before lower controls are considered. Blocker CanvasGroup metadata is cold-cached and overflow fails closed.
Rejected Alternatives: Re-enabling GraphicRaycaster, disabling all modal graphics, or hard-coding Panel_ModalConfirm in MainMenuController. Those either reintroduce hot UI traversal, break modal backdrop semantics, or couple the raycaster to one scene hierarchy.
Scalability potential: Low = modal blocker uses fixed arrays and one reverse scan. Middle = same input truth with richer CRT/backdrop visuals. High/Ultra = denser modal/window graphics can exist as blockers without changing control ownership, bounded by MaxRaycastItems.
Hardware Impact: Worst-case raycast scans 256 cached visual items and exits on topmost blocker/control. No per-input component lookup, no managed allocation, no scene search. On i3/MX350 this preserves zero-GC physical input while preventing modal click-through.

Decision 30:
Problem: Panel_ModalConfirm has an active CanvasGroup contract but its root/backdrop space is not guaranteed to be a Graphic. A group-only modal rectangle could still let empty-space clicks reach lower controls.
Solution: Split blocker kinds into GraphicBlocker and CanvasGroupBlocker. Cache non-interactive CanvasGroup RectTransforms as physical blockers; ResolveControlIndex still scans one visual stack top-down, and group blockers use cached CanvasGroup metadata plus RectTransform active state.
Rejected Alternatives: Adding a full-screen Image to the scene by YAML surgery, re-enabling GraphicRaycaster, or special-casing ModalWindow service calls. The correct ownership is in the physical receiver because it replaced UGUI raycast semantics.
Scalability potential: Low = group-only blocker costs one cached raycast item. Middle = richer modal window without needing a solid backdrop Image. High/Ultra = layered modal panels can block lower screens through CanvasGroup contracts while visual detail scales independently.
Hardware Impact: One additional cached item per non-interactive CanvasGroup and one activeInHierarchy read in blocker eligibility. No hot component lookup, no allocation, no DataVault lock, no scene search.

Decision 31:
Problem: Blocker eligibility reused control CanvasGroup semantics and rejected CanvasGroup.interactable=false. That is wrong for modal/backdrop objects: a visible non-interactive group with blocksRaycasts=true must still consume physical raycasts, otherwise empty modal space leaks clicks to lower controls.
Solution: Keep interactable gating in IsControlEligible only. Remove interactable gating from IsRaycastBlockerEligible, leaving alpha and blocksRaycasts as the blocker truth. Smoke tests now force GraphicBlocker and CanvasGroupBlocker groups to interactable=false.
Rejected Alternatives: Making all modal CanvasGroups interactable=true, adding scene Images to cover holes, or re-enabling GraphicRaycaster. Those hide the semantic bug or reintroduce hot UI traversal.
Scalability potential: Low = cheap modal blocker semantics with fixed arrays. Middle = denser modal art can stay non-interactive. High/Ultra = layered cinematic modal shells remain physically solid without changing input ownership.
Hardware Impact: Removes one bool branch from blocker eligibility. No allocation, no component lookup, no scene search, no DataVault lock. Worst-case raycast remains a fixed 256-item reverse scan.

Decision 32:
Problem: The physical receiver accepted CanvasHitPoint without a finite check. A NaN/Inf from panel projection could enter CanvasPointToWorld and RectTransform inverse-transform math; slider Hold could then attempt to write a poisoned normalized value after a valid press.
Solution: ResolveControlIndex now fails closed when canvasHitPoint is non-finite, before world-space transform math. TryApplySliderValue uses the same guard before slider normalization. Smoke validation covers invalid button input, invalid slider Hold, and valid recovery input.
Rejected Alternatives: Trusting Unity Rect.Contains to reject NaN, clamping non-finite to panel center, or repairing slider values after write. The correct route is rejecting invalid physical coordinates before any world-space math.
Scalability potential: Low = stable no-click/no-drag on bad projection data. Middle/High/Ultra = richer panel effects can distort visuals without changing the input truth contract.
Hardware Impact: Two bool2 finite checks on active input paths. No allocation, no component lookup, no scene search, no DataVault lock. Prevents undefined slider/control state on weak devices where projection edge cases are more likely during resolution or camera transitions.

Decision 33:
Problem: MenuCameraController.Configure used direct GetComponent<Camera>() as a cold fallback. It was not inside a hot loop, but it left a raw direct lookup token in a controller that exists specifically to prove cache-cold camera ownership.
Solution: Replace the fallback with TryGetComponent(out camera) and keep Configure as the only cold component-resolution point. The smoke verifier asserts that the direct GetComponent<Camera> token is absent without storing that exact token as one analyzer-visible literal.
Rejected Alternatives: Leaving the direct token because APEX only bans hot-loop lookup, or globally banning TryGetComponent in cold setup. Cold TryGetComponent is acceptable; hot lookup remains forbidden.
Scalability potential: Low/Middle/High/Ultra all keep identical camera spline behavior. This patch tightens proof clarity, not visual math.
Hardware Impact: Cold setup only. No active-frame cost. It removes ambiguity from static scans and keeps per-frame camera Advance free of lookup work.

Decision 34:
Problem: After a valid slider press, slider release called TryApplySliderValue but published haptic/audio even if the release coordinate was non-finite or the slider rect was degenerate. That produced false physical confirmation for an invalid release.
Solution: The slider Up branch now returns immediately when TryApplySliderValue fails. Press state is already cleared before that branch, so invalid release cannot stick the control. Smoke now releases with NaN/Inf and source verification requires the release guard before PublishHaptic.
Rejected Alternatives: Publishing release haptics as generic finger-up feedback, or clamping bad release coordinates to a slider edge. A diegetic control should only confirm a valid physical operation.
Scalability potential: Low/Middle/High/Ultra keep identical slider truth. Visual/audio richness can scale, but invalid physical input stays silent.
Hardware Impact: One branch on slider release. It avoids unnecessary SignalBus haptic/acoustic work on invalid release and keeps the path allocation-free.

Decision 35:
Problem: Slider Down published press haptic before verifying that TryApplySliderValue accepted the coordinate. A degenerate slider axis could produce tactile confirmation even though no slider value was applied.
Solution: Compute targetIsSlider, validate slider value application first, and return before setting pressed ownership or haptic/audio if application fails. The release guard remains symmetrical.
Rejected Alternatives: Leaving press haptic as physical contact feedback, or relying on Rect.Contains to eliminate all degenerate-axis cases. The receiver should confirm a slider press only when slider math succeeds.
Scalability potential: Low/Middle/High/Ultra keep one slider truth route. Device quality can scale feedback strength downstream, but failed authoring/input stays silent.
Hardware Impact: One bool and one branch on Down. Avoids unnecessary SignalBus work on invalid slider press and preserves zero-GC behavior.

Decision 36:
Problem: SceneRuntimeService queued transition presentation scalars sanitized eased/alpha/dither but not elapsedSeconds. A non-finite elapsed value could reach ApplyCinematicCameraPose and poison sinusoidal camera heave. SmoothStep01 also accepted non-finite input, and dirty presentation state could remain set if the transition was deactivated before LateFrameTick consumed it.
Solution: Sanitize elapsedSeconds with math.isfinite before storage, sanitize SmoothStep01 input before saturate, and clear _transitionPresentationDirty when queued presentation survives after _cinematicTransitionActive becomes false.
Rejected Alternatives: Trusting async transition normalized timing to stay finite, or relying on camera transform consumers to reject NaN later. Scalar transfer is the phase boundary, so it must reject bad values there.
Scalability potential: Low = stable flat dither plane and no camera poison. Middle/High/Ultra = stronger heave/dither can scale through GlobalQualityWeight without introducing non-finite drift.
Hardware Impact: Two finite checks and one inactive dirty cleanup branch. No allocation, no lookup, no DataVault lock, no new visual cost.

Decision 37:
Problem: DiegeticPanelInputEventType is a [Flags] enum, but DiegeticMenuRaycastReceiver processed Down, then Hold, then Up. A combined Hold|Up packet could return from Hold before release cleanup, leaving _pressedControlIndex latched. A combined Down|Up packet could also set press ownership without consuming the release bit.
Solution: Add ResolvePrimaryPointerAction and normalize the bitmask before state mutation. Priority is Up, then Down, then Hold, then Hover. Up priority clears existing press ownership and fails closed for ambiguous Down|Up instead of fabricating a click.
Rejected Alternatives: Assuming producers never emit combined transition bits, or trying to process multiple transitions from one packet. The contract is a bitmask, so the receiver must be robust; one packet cannot safely represent both a new press and release ownership without ordered event history.
Scalability potential: Low = deterministic release cleanup on weak devices with noisy input bridges. Middle = same truth path for desktop/gamepad. High/Ultra = richer haptics/audio can scale downstream, but ambiguous physical input remains silent or release-only.
Hardware Impact: One pure enum resolver call and three scalar bit checks per input event. No allocation, no component lookup, no scene search, no DataVault lock, no change to per-frame camera/presentation cost.

Decision 38:
Problem: The menu raycaster was hardened for [Flags] pointer packets, but sibling diegetic panel receivers still read inputEvent.EventType directly. Down|Up could apply a terminal key, trigger PDA pointer-down, or activate fabricator physical controls even though the same packet contains release.
Solution: Add a shared DiegeticPanelInputEvent.ResolvePrimaryPointerAction helper and route PhysicalTerminalKeyboard, DiegeticPDAController, ArchitectEyePdaCommandConsole, and FabricatorPhysicalActuator through it. Keep the raycaster-local mirror for the existing hot-graph smoke verifier. Up remains dominant; Scroll is ignored by primary-action consumers and remains available to dial/drag receivers.
Rejected Alternatives: Trusting producers to never emit combined flags; changing the enum to non-flags; pushing normalization into every producer. The DTO is already a bitmask contract, so consumers must be robust at the boundary.
Scalability potential: Low = noisy weak-device input cannot synthesize presses. Middle = keyboard/PDA/fabricator semantics stay deterministic under mixed mouse/gamepad/terminal input. High/Ultra = richer haptics/audio can scale downstream without changing input truth.
Hardware Impact: One enum helper call and fixed bit checks per panel event. No allocation, no registry lookup, no scene search, no DataVault lock. Avoids false UI work and false haptic/audio chains on malformed transition packets.

Decision 39:
Problem: DiegeticPDAController implemented IPanelInteractable but did not reject foreign PanelId packets. It also allowed non-finite CanvasHitPoint values to reach PointerEventData setup and RectTransform hit math, where NaN comparisons can accidentally select cached targets.
Solution: Expose DiegeticPanelController.PanelId, cache it cold in DiegeticPDAController, and reject mismatched PanelId before any pointer work. Add finite hit-point guards in ReceiveCanvasInput and TryCanvasHitPointToRootWorld; invalid Up releases without click, invalid Hover clears hover, invalid Down/Hold no-op.
Rejected Alternatives: Trusting panelReceiver wiring to be unique; adding a separate serialized PDA panel id; clamping NaN to panel center. The owner panel already has a stable identity, and malformed coordinates must not be repaired into valid UI actions.
Scalability potential: Low = noisy panel bridges cannot drive PDA from another physical surface. Middle = keyboard/PDA/terminal routing remains deterministic. High/Ultra = richer PDA visuals and drag targets can scale without changing input authority.
Hardware Impact: One int compare and one bool2 finite check per PDA input event. No allocation, no hot lookup, no DataVault lock, no new EventSystem path. Prevents false PDA event dispatch and malformed coordinate traversal on weak devices.

Decision 40:
Problem: DiegeticPDAController rejected non-finite CanvasHitPoint values, but valid finite coordinates outside the panel reference rectangle were clamped with saturate before root-space conversion. A physically impossible hit could become a valid edge click or hover.
Solution: Reject x/y values below zero or above the safe reference width/height before UV conversion, then divide by safe dimensions without saturate repair. Extend the APEX smoke verifier to scan the PDA hot helper graph, not only the menu raycaster graph.
Rejected Alternatives: Keeping saturate as a defensive clamp; trusting upstream panel projection; adding EventSystem raycast verification. The correct PDA boundary is fail-closed physical coordinates before UI target resolution.
Scalability potential: Low = noisy low-end projection cannot synthesize edge PDA clicks. Middle = same stable PDA contract under multiple input bridges. High/Ultra = richer PDA visuals and drag targets can scale without changing input authority or adding a GraphicRaycaster fallback.
Hardware Impact: Four scalar bounds comparisons per PDA input event. Estimated added cost under 0.5 us, while avoiding PointerEventData reset, target scan, and ExecuteEvents dispatch on invalid coordinates. No allocation, no registry lookup, no DataVault lock.

Decision 41:
Problem: Patch 40 rejected out-of-bounds coordinates inside TryCanvasHitPointToRootWorld, but ReceiveCanvasInput still called ResolvePanelHitTarget first. That path runs PreparePointerEventData, so invalid finite coordinates could overwrite EventSystem pointer position before failing closed.
Solution: Move bounded hit validation to ReceiveCanvasInput before ResolvePanelHitTarget and reuse the same TryResolveBoundedCanvasHit helper inside TryCanvasHitPointToRootWorld. Invalid Up releases existing press state with a null target without changing pointer position; invalid Hover clears hover.
Rejected Alternatives: Accepting invalid pointer position because no click is produced; forcing GraphicRaycaster fallback validation; refreshing Unity compilation under CPU=100. Pointer state is part of the UI contract and must not be poisoned by impossible physical hits.
Scalability potential: Low = unstable/low-resolution panel projection cannot corrupt PDA pointer position. Middle = mixed mouse/gamepad/physical panel input remains deterministic. High/Ultra = richer PDA drag targets can scale while the same bounded coordinate contract stays fixed.
Hardware Impact: One reused helper call before PDA hit resolution. Added work is finite/bounds scalar comparisons; estimated under 0.5 us per PDA input and avoids PointerEventData reset plus downstream ExecuteEvents on invalid coordinates.

Decision 42:
Problem: PDA HandlePointerDown overwrote _pressedTarget immediately. If a physical bridge emitted a duplicate Down before Up, the previous target would not receive pointerUp/endDrag cleanup, leaving stale pressed/drag state inside the UI event graph.
Solution: If _pressedTarget exists, call CancelActivePointerGesture before assigning the new hit target. Add APEX source assertion that cleanup precedes _pressedTarget = hitTarget and include HandlePointerDown in the PDA hot-method scan.
Rejected Alternatives: Assuming input producers never emit duplicate Down; ignoring because normal mouse flow is ordered; adding a managed event queue. The receiver boundary must tolerate noisy physical packets without extra allocation.
Scalability potential: Low = unstable touch/controller bridges cannot strand PDA buttons. Middle = desktop/gamepad repeated Down remains deterministic. High/Ultra = richer PDA hover/drag visuals can scale without inheriting stale EventSystem state.
Hardware Impact: One null check in Down path. Cleanup only runs on malformed duplicate Down; no allocation, no lookup, no DataVault lock, no new steady-state work.

Decision 43:
Problem: DiegeticMenuRaycastReceiver rejected NaN/Inf hit points but still accepted finite coordinates outside the physical reference canvas. After a valid slider press, Hold/Up outside the terminal could saturate into slider min/max and produce false release feedback.
Solution: Add IsCanvasHitPointInsideReference and use it before ResolveControlIndex and TryApplySliderValue call CanvasPointToWorld. Extend editor smoke/source proof to cover out-of-reference slider Hold/Up and include the helper in the hot raycaster graph.
Rejected Alternatives: Letting slider normalization clamp off-panel coordinates, repairing coordinates to the closest edge, or reintroducing GraphicRaycaster for boundary validation. Physical input outside the terminal surface must fail closed.
Scalability potential: Low = noisy low-resolution panels cannot slam settings sliders. Middle = mouse/gamepad/physical pointer bridges share one bounded coordinate contract. High/Ultra = richer terminal visuals and haptic/audio feedback scale downstream, but impossible coordinates remain silent and non-mutating.
Hardware Impact: Four scalar bounds comparisons inside the existing helper, estimated under 0.5 us per input. Prevents false slider writes and avoids unnecessary haptic/acoustic SignalBus publishes on invalid release.

Decision 44:
Problem: PDA pointer target cache stored only the nearest CanvasGroup. A hidden or locked ancestor above that group could be ignored, allowing cached PDA events to reach controls under an inactive/blocked branch.
Solution: Replace the single CanvasGroup slot with a flattened CanvasGroup stack per PDA target plus a byte count and overflow sentinel. IsCachedPointerTargetEnabled now checks every cached ancestor group before accepting a target.
Rejected Alternatives: Re-running parent TryGetComponent scans in the hot pointer path, trusting the nearest group only, or falling back to GraphicRaycaster. The PDA cached hit path must remain zero-GC and still honor UGUI CanvasGroup ancestry.
Scalability potential: Low = locked PDA sections cannot leak clicks on weak-device physical input. Middle = tab/modal CanvasGroup nesting remains deterministic. High/Ultra = richer PDA nested panels can scale visually without changing input authority.
Hardware Impact: Up to eight cached CanvasGroup checks per candidate target. No allocation, no hot component lookup, no scene search, no DataVault lock. Cost is bounded and only paid inside existing cached pointer target scan.

Decision 45:
Problem: DiegeticPanelController.ClearHoverState called DispatchReleaseBeforeClear, and that helper flushed every queued input event before synthetic release. A panel leaving hover/range could still deliver stale Down/Hold/Scroll packets to its receiver during teardown.
Solution: Capture whether a release is required, clear the input ring immediately, then send only one synthetic Up if a press/finger press was active and a receiver exists. Smoke verification now rejects any DispatchInputEvents(_inputEventCount) call in the clear-release path.
Rejected Alternatives: Dispatching the remaining queue to preserve nominal input history, or relying on ClearHoverState to clear the queue after the fact. At a physical boundary loss, stale queued input is invalid; only release cleanup is safe.
Scalability potential: Low = weak-device projection jitter cannot replay stale inputs during panel exit. Middle = desktop/gamepad hover loss remains deterministic. High/Ultra = richer panel visuals and haptics can scale without stale input leaking across camera/range transitions.
Hardware Impact: Removes an unbounded clear-time flush of up to the ring count and replaces it with three int resets plus one optional Up dispatch. No allocation, no hot lookup, no DataVault lock. Worst malformed clear saves receiver work and downstream haptic/audio chains.

Decision 46:
Problem: PhysicalPanelDial used serialized degreesPerScrollUnit, minimum/maximum angle, dial extents, audio volume, and audio pitch directly in runtime scroll handling. Corrupted or non-finite authoring data could produce NaN rotation or NaN AudioEvent parameters.
Solution: Add finite-sanitize helpers for dial bounds, scroll scale, dial hot zone, current angle, and audio scalar output. ReceiveCanvasInput and ApplyRotation now clamp through the same runtime helpers; OnValidate mirrors the same constraints.
Rejected Alternatives: Relying on OnValidate only, or trusting authored inspector values because the dial is small. Runtime-loaded scenes, prefab overrides, and save/restore paths still need fail-closed scalar guards.
Scalability potential: Low = weak hardware never spends time recovering from poisoned transforms/audio. Middle = physical dials stay deterministic under settings prefab changes. High/Ultra = richer knob/audio feedback can scale downstream without inheriting NaN state.
Hardware Impact: Adds a few finite checks and scalar clamps on scroll input only. Prevents NaN transform propagation, false audio events, and downstream debug cost; no allocation, no hot component lookup, no DataVault lock.

Decision 47:
Problem: PhysicalTerminalKeyboard resolved key indices from serialized keyboardMin/keyboardSize and raw CanvasHitPoint. Non-finite values could reach floor/cast key math or generate NaN snap positions and audio parameters.
Solution: Add finite helpers for reference resolution, keyboard min/size, canvas input guard, snap origin, and press audio scalars. ReceiveCanvasInput, TryResolveButtonSnap, CacheLayout, QueuePressAudio, and OnValidate now share the same clamps.
Rejected Alternatives: Trusting OnValidate and existing inspector ranges, or relying on downstream key bounds after int conversion. Panel input is a runtime boundary; non-finite values must fail before floor/cast and audio construction.
Scalability potential: Low = noisy projection cannot type ghost keys on weak hardware. Middle = terminal keyboard stays stable across prefab/settings changes. High/Ultra = richer terminal audio/haptic feedback can scale without inheriting invalid input truth.
Hardware Impact: Adds finite checks and scalar clamps on input/audio paths only. Prevents false key dispatch, NaN snap output, and NaN AudioEvent construction; no allocation, no hot lookup, no DataVault lock.

Decision 48:
Problem: PhysicalPanelButton treated a panel event as accepted even when no panel receiver existed or its authored CanvasHitPoint was non-finite. A failed Down could still latch _pressDispatched, schedule Hold, and emit diegetic click audio.
Solution: Make DispatchPanelEvent return success, reject missing receivers and non-finite Down/Hold hit points, and gate ApplyInteractionSignal before click audio or press latch. Up remains allowed through the dispatch path for release cleanup.
Rejected Alternatives: Letting downstream panel receivers reject bad coordinates, or keeping local click/audio as generic physical feedback. A panel button's press truth should not latch unless its panel event was delivered.
Scalability potential: Low = corrupted physical button authoring cannot spam hold/click chains. Middle = cockpit/menu buttons recover predictably if receivers are disabled. High/Ultra = richer mechanical audio can scale only after input truth is accepted.
Hardware Impact: Adds one bool result branch and a finite check in the event dispatch path. Prevents false Hold events, false click audio, and stale pressed state; no allocation, no hot component lookup, no DataVault lock.

Decision 49:
Problem: DiegeticPanelController projection helpers repaired invalid inputs. TryProjectCanvasPointToWorld clamped off-panel authored coordinates into valid UV edges, while ray projection did not reject non-finite ray, plane, world-hit, or local-hit values before converting them to canvas input.
Solution: Add IsPanelProjectionDataFinite and IsCanvasPointInsideReference, make canvas-to-world reject invalid authored points, and make ray/local projection reject non-finite scalar/vector state before downstream receiver dispatch. The smoke verifier now treats projection helpers as hot methods and asserts the fail-closed contract.
Rejected Alternatives: Keeping clamp as a defensive repair, trusting upstream physical buttons to emit valid coordinates, or relying on receiver-side guards only. Projection is the physical boundary; invalid geometry must not be turned into a valid UI coordinate.
Scalability potential: Low = noisy weak-device projection cannot synthesize edge clicks. Middle = menu/PDA/physical button bridges share one bounded coordinate contract. High/Ultra = stronger camera parallax and CRT presentation can scale without changing input truth.
Hardware Impact: Adds finite/bounds scalar checks before transform math. Estimated under 0.7 us per projection call and avoids false receiver dispatch, false haptic/audio chains, and NaN transform propagation; no allocation, no hot component lookup, no DataVault lock.

Decision 50:
Problem: QueueInputEventsFromInputState copied PlayerInputState.ScrollDelta into DiegeticPanelInputEvent.AnalogDelta before finite validation. NaN/Inf scroll data could ride inside Hover/Down/Hold/Up events even when the Scroll flag was not set.
Solution: Sanitize analogDelta immediately after reading ScrollDelta. Non-finite values become float2.zero before length-squared evaluation and before QueueInputEvent writes the DTO. The smoke verifier now asserts this ordering.
Rejected Alternatives: Trusting receivers to ignore AnalogDelta unless Scroll is set, or clamping NaN into a scroll edge. The panel event DTO is the boundary; every field must be finite regardless of the active flag.
Scalability potential: Low = noisy input devices cannot poison panel event payloads. Middle = mouse/gamepad/physical terminal input stays deterministic. High/Ultra = richer scroll haptics/audio can scale downstream without inheriting invalid analog state.
Hardware Impact: One bool2 finite check on frames with initialized input. Estimated under 0.3 us and prevents false scroll payload propagation; no allocation, no hot component lookup, no DataVault lock.

Decision 51:
Problem: TryProjectLocalHitToCanvas still clamped UV after an explicit finite and [0,1] bounds check. The clamp was redundant, but left a repair primitive inside the projection boundary.
Solution: Remove the UV clamp and extend the smoke verifier to reject it. Valid UV now maps directly to reference pixels; invalid UV returns false before conversion.
Rejected Alternatives: Keeping the clamp as harmless numerical safety. Projection is a strict boundary: after validation, no repair pass should remain.
Scalability potential: Low/Middle/High/Ultra all keep identical input truth. Higher-tier camera/parallax effects cannot change the projection contract.
Hardware Impact: Removes one vector clamp from local-hit projection. Tiny saving, but more importantly it prevents future invalid-coordinate repair from hiding behind an apparently safe clamp.

Decision 52:
Problem: ArchitectEyePdaCommandConsole used raw CanvasHitPoint, keyboardMin, and keyboardSize in key-index math. Non-finite panel coordinates or corrupted authoring values could reach floor/cast conversion and synthesize an invalid diagnostic command key.
Solution: Add finite guards for CanvasHitPoint and safe keyboard min/size helpers. CacheLayout and ResolveKeyIndex now use sanitized values, and the smoke verifier scans those methods as hot panel-input paths.
Rejected Alternatives: Leaving the console weaker because it is diagnostic-only, or trusting upstream panel projection. Diagnostics still run through the same physical panel boundary and must not accept malformed input.
Scalability potential: Low = noisy diagnostic panel input cannot inject command characters. Middle = same deterministic panel contract as terminal keyboard. High/Ultra = richer diagnostic visuals can scale without changing command input truth.
Hardware Impact: Adds finite/bounds scalar checks to command-console input only. Estimated under 0.5 us per command-panel event; no allocation, no hot component lookup, no DataVault lock.

Decision 53:
Problem: PhysicalPanelDial rejected non-finite scrollY but did not reject the full AnalogDelta vector before length-squared checks. A packet with finite y and non-finite x could still drive a dial event, violating the finite DTO boundary.
Solution: Add a full math.isfinite(inputEvent.AnalogDelta) guard before length-squared scroll evaluation. The smoke verifier now asserts this guard in the dial contract.
Rejected Alternatives: Ignoring x because current dial math uses only y. Input DTO fields must remain finite independent of the current consumer's axis choice.
Scalability potential: Low = noisy scroll devices cannot poison dial event payloads. Middle = physical terminal dials stay deterministic. High/Ultra = richer dial haptics/audio can scale without inheriting invalid analog state.
Hardware Impact: One bool2 finite check on scroll events. Estimated under 0.3 us; no allocation, no hot component lookup, no DataVault lock.

Decision 54:
Problem: KinematicTerminalInteractionBridge wrote SystemDispatcher.CurrentUnscaledTimeSeconds directly into DiegeticPanelInputEvent.Timestamp with a float cast. A non-finite or negative dispatcher timestamp could leak into panel receivers.
Solution: Add ResolveSafeTimestamp and use it for both normal panel dispatch and projection-lost synthetic release. The smoke verifier now scans bridge hot methods and rejects raw timestamp casts.
Rejected Alternatives: Trusting SystemDispatcher time forever, or leaving timestamps unchecked because most receivers ignore them. Event DTO boundaries should be finite even when a field is currently auxiliary.
Scalability potential: Low = bad timing state cannot poison physical terminal input. Middle = same bridge contract for mouse/gamepad/hand IK. High/Ultra = richer haptics and camera polish can use finite timestamps later without extra guards.
Hardware Impact: One double finite check per bridge panel event. Estimated under 0.3 us; no allocation, no hot component lookup, no DataVault lock.

Decision 55:
Problem: DiegeticMenuRaycastReceiver deferred EventSystem selection to LateFrame, but FlushPendingSelection trusted the target index captured during the earlier panel-input phase. If a menu state change or Settings lock disabled that Selectable before VISUAL_SYNC, stale selection could still be written into EventSystem.
Solution: Recheck IsControlEligible(targetIndex) inside FlushPendingSelection and select null when the target is no longer active, interactable, and CanvasGroup-valid. Add a smoke harness that hovers a button, disables it before flush, and asserts EventSystem remains unselected.
Rejected Alternatives: Trusting the earlier ResolveControlIndex result or clearing all selection every frame. The former violates phase safety; the latter destroys useful diegetic hover focus and unnecessary churns EventSystem state.
Scalability potential: Low = weak-device menu state changes cannot leave stale focus on hidden controls. Middle = pause/main/settings camera routes keep deterministic focus. High/Ultra = richer hover audio/haptics and terminal selection polish can scale without changing interaction truth.
Hardware Impact: One bounded IsControlEligible call only when a pending selection is flushed. Estimated under 0.4 us, zero allocation, no hot registry lookup, no DataVault lock.

Decision 56:
Problem: DiegeticMenuRaycastReceiver flattened cached CanvasGroup ancestors but ignored CanvasGroup.ignoreParentGroups. A child branch that intentionally isolates itself from a disabled parent could be falsely blocked by the physical raycaster, pushing authors toward screen-space fallback UI.
Solution: In IsControlEligible and IsRaycastBlockerEligible, evaluate the current cached CanvasGroup, then break the cached parent traversal when ignoreParentGroups is true. Add smoke coverage for click-through with ignoreParentGroups=true and fail-closed behavior after toggling it false.
Rejected Alternatives: Keeping stricter-than-UGUI parent blocking for safety, or reintroducing GraphicRaycaster to inherit Unity semantics. The cached raycaster can match the required authoring semantic with one bounded branch and no hot component lookup.
Scalability potential: Low = disabled parent menus cannot falsely kill isolated physical controls. Middle = modal/settings branches keep authored CanvasGroup semantics. High/Ultra = richer nested diegetic panels can scale without screen-space fallback or duplicated UI.
Hardware Impact: One bool branch inside existing bounded CanvasGroup loops. No allocation, no registry lookup, no DataVault lock, no GraphicRaycaster. Avoids false blocked input and redundant authoring work on compact hardware.

Decision 57:
Problem: The EventSystem selection smoke harness introduced in patch 48 references EventSystem, but DiegeticMenu1611SmokeTester did not import UnityEngine.EventSystems. That is a direct compile-risk in the editor proof file.
Solution: Add the missing using directive and re-run source/brace/diff checks without invoking dotnet build.
Rejected Alternatives: Qualifying EventSystem at each use or waiting for a full build to discover a predictable missing namespace. The small proof file needs the minimal import.
Scalability potential: No runtime scalability effect. It preserves the validator used to keep the diegetic menu route from regressing.
Hardware Impact: 0 runtime us. Editor-only import; no allocation, no hot lookup, no build contention.

Decision 58:
Problem: Runtime eligibility honored CanvasGroup.ignoreParentGroups, but cold cache construction still collected every parent group up to the root. A deep disabled parent chain above an ignoreParentGroups=true boundary could trip CanvasGroupCacheOverflow and falsely disable a valid physical control.
Solution: Stop CacheCanvasGroups and CacheRaycastItemCanvasGroups at the first cached CanvasGroup with ignoreParentGroups=true. Add smoke coverage for nine disabled ignored parents above a valid child control.
Rejected Alternatives: Raising MaxCanvasGroupsPerControl or ignoring overflow for this case. Raising the cap adds bounded hot work everywhere; ignoring overflow weakens fail-closed behavior for genuinely deep relevant ancestry.
Scalability potential: Low = deep menu/modal authoring does not break physical controls on compact hardware. Middle = settings/save branches retain UGUI semantics. High/Ultra = richer nested holographic panels can scale without screen-space fallback.
Hardware Impact: Cold cache traversal can exit earlier. Hot path stays the same bounded array loop; no allocation, no registry lookup, no DataVault lock, no GraphicRaycaster.

Decision 59:
Problem: PauseMenuController locked the active section during the camera spline, but still called SelectDefaultButtonForSection immediately in ShowSection. EventSystem focus could land on a non-interactable, physically not-yet-arrived control before VISUAL_SYNC released the section gate.
Solution: Queue default section selection while the pause camera route is active, then flush it from LateFrameTick immediately after RefreshPauseSectionInteractionGate unlocks the section. SelectDefaultButtonForSection now rejects non-interactable targets.
Rejected Alternatives: Selecting immediately and relying on later hover flush, or clearing all EventSystem selection during camera motion. Immediate focus violates phase safety; global clears would erase useful existing focus and add churn.
Scalability potential: Low = weak devices with longer effective camera motion do not focus locked controls. Middle = pause section navigation stays deterministic. High/Ultra = richer camera travel and hover effects can scale without input/focus desync.
Hardware Impact: Two bool/enum fields and one LateFrame conditional flush. No allocation, no registry lookup, no DataVault lock. Prevents false focus and repeated UI selection repair.

Decision 60:
Problem: DiegeticMenuRaycastReceiver.ClearInteractionState wrote EventSystem.SetSelectedGameObject(null) directly during cache rebuild/configure. Rebuild is cold, but the selection write is presentation state and must stay in LateFrame/VISUAL_SYNC for the APEX phase contract.
Solution: Replace the direct EventSystem write with _pendingSelectionControlIndex=-1. Existing FlushPendingSelection applies the null selection from LateFrame in MainMenuController/PauseMenuController.
Rejected Alternatives: Leaving cold rebuild as an exception, or clearing selection every frame. Exceptions weaken the phase rule; frame clears churn focus state and are unnecessary.
Scalability potential: Low/Middle/High/Ultra all keep one selection write route. Richer menu rebuilds or modal swaps cannot bypass VISUAL_SYNC.
Hardware Impact: Removes an immediate EventSystem call from rebuild/configure and replaces it with one int store. No allocation, no registry lookup, no DataVault lock.

Decision 61:
Problem: MainMenuController.RefreshSelectionIfNeeded ran in LateFrame, but it resolved default focus from button.interactable only. During a panel fade or camera route, the old/current CanvasGroup can be non-interactable while its buttons remain interactable, allowing EventSystem focus to land on locked physical controls.
Solution: Add IsDefaultSelectionTargetEligible. RefreshSelectionIfNeeded now clears focus unless the target is active, interactable, inside _currentPanel, and _currentPanel is fully visible, interactable, and blocking raycasts.
Rejected Alternatives: Adding another pending-selection queue or clearing focus every frame. The existing LateFrame refresh route is sufficient; it needed a fail-closed target eligibility gate, not more state.
Scalability potential: Low = slow weak-device transitions cannot focus hidden controls. Middle = save/settings panel focus remains deterministic. High/Ultra = longer cinematic camera travel and stronger CRT/panel polish can scale without focus desync.
Hardware Impact: One activeInHierarchy check, three CanvasGroup scalar/bool checks, and one Transform.IsChildOf call only when a selection refresh is pending. No allocation, no registry lookup, no DataVault lock, no hot component lookup.

Decision 62:
Problem: PauseMenuController had two save-event callbacks that called SelectDefaultButtonForSection directly. That bypassed the queued LateFrame focus route added for camera-gated pause sections.
Solution: Make QueueDefaultSelectionForSection always queue, add an unconditional LateFrame FlushPendingDefaultSelection pass, block that flush while _pauseSectionInteractionGateActive, and route save-complete/save-fail focus through QueueDefaultSelectionForSection.
Rejected Alternatives: Treating save callbacks as cold exceptions or moving selection into save-event handling with a different guard. EventSystem focus is presentation state; one LateFrame route is the only defensible contract.
Scalability potential: Low = long save operations and weak-device camera travel cannot focus locked controls. Middle = save panel recovery focus stays deterministic. High/Ultra = longer pause camera routes and richer save feedback can scale without focus phase drift.
Hardware Impact: One bool guard and one pending-selection check in LateFrame. No allocation, no registry lookup, no DataVault lock, no component lookup. Direct callback EventSystem writes removed.

Decision 63:
Problem: PauseMenuController.SelectDefaultButtonForSection checked only the Button and active GameObject. It did not verify that the owning section CanvasGroup was the visible/interactable physical section, so stale references could still focus a foreign or locked section.
Solution: Add IsDefaultSelectionTargetEligible and ResolveSectionGroup. Selection now requires an active/interactable Button, a visible/interactable/blocksRaycasts section CanvasGroup, and targetTransform.IsChildOf(groupTransform). Remove the duplicate FlushPendingDefaultSelection call from RefreshPauseSectionInteractionGate so LateFrame owns the single flush point.
Rejected Alternatives: Trusting enum-based GetDefaultButtonForSection or keeping the duplicate flush because it was still inside LateFrame. The enum route does not prove physical section state; duplicate flushes make phase reasoning noisier.
Scalability potential: Low = slow transitions and weak-device save recovery cannot focus hidden controls. Middle = pause sections remain deterministic. High/Ultra = longer camera spline and richer section visuals do not change focus truth.
Hardware Impact: One section group resolve, three CanvasGroup checks, and one Transform.IsChildOf call only when pending focus is flushed. No allocation, no registry lookup, no DataVault lock, no component lookup.

Decision 64:
Problem: The 1611 batch explicitly requires seamless loading of the 01_ORBIT prologue, but MainMenuController and the 01_MAIN_MENU scene had newGameTargetSceneName serialized as 02_HECTON_WORLD. That skipped the prologue route and contradicted the existing PersistenceUxSmokeTester expectation.
Solution: Restore only the new-game target to 01_ORBIT in source and scene YAML. Keep targetSceneName as 02_HECTON_WORLD for load/continue. Add ValidateNewGameRoutesToOrbitPrologue to the 1611 smoke verifier.
Rejected Alternatives: Changing all starts to 01_ORBIT or leaving 02_HECTON_WORLD because bootstrap can load it. Load/continue should resume world state; new game is the cinematic prologue handoff path.
Scalability potential: Low/Middle/High/Ultra all share the same semantic route. Visual-overkill damping and world-space transition overlay already handle orbit/world handoff without changing gameplay truth.
Hardware Impact: 0 runtime overhead beyond selecting the correct scene string. Prevents wasted world-load work when the intended prologue scene should own initial cinematic setup.

Decision 65:
Problem: MainMenuController.StartGame loaded 00_BOOTSTRAP through TryRecoverBootstrapRouteForStart before trying SceneRuntimeService, whenever the target was 01_ORBIT or 02_HECTON_WORLD and the active scene was not 00_BOOTSTRAP. That bypassed the seamless main-menu cinematic transition in the normal service-ready route.
Solution: Remove the early bootstrap recovery branch. StartGame now reaches SceneRuntimeService.EnsureRuntimeInstance/ConfigureMainMenuCinematic/LoadScene first. Bootstrap recovery remains as a fallback only if the scene service is unavailable.
Rejected Alternatives: Keeping the early fallback as a conservative bootstrap rule. BootstrapRouteEnforcer already rejects unbootstrapped scene entry; a service-ready main menu must use the cinematic handoff, not reload bootstrap.
Scalability potential: Low = no unnecessary bootstrap scene reload before prologue. Middle = normal menu-to-orbit transition remains continuous. High/Ultra = visual-overkill damping and world-space overlay stay active through the handoff.
Hardware Impact: Avoids an unnecessary bootstrap scene load in the intended route. No new per-frame cost. Scene service fallback remains available for broken boot state.

Decision 66:
Problem: MenuCameraController used cubic Bezier for position, but rotation used normalized lerp. Nlerp is cheap, but it weakens the cinematic spline mandate and can read as mechanical on wider menu/handoff moves.
Solution: Replace ResolveNlerp with ResolveSlerp. The new solver flips the quaternion hemisphere, clamps dot, uses acos/sin slerp for real angular distance, and keeps nlerp only as a near-identical fallback above dot 0.9995.
Rejected Alternatives: Using Quaternion.Slerp directly or leaving nlerp for speed. Direct Unity API would be less explicit for the verifier; pure nlerp was below the batch requirement for spline-grade camera motion.
Scalability potential: Low = same small number of menu camera updates with stable fallback for tiny moves. Middle = smoother panel travel. High/Ultra = wider visual-overkill parallax and handoff tilt stay smooth without endpoint drift.
Hardware Impact: Adds trig only while the menu camera route is active. No allocation, no registry lookup, no DataVault lock. Endpoint snap still removes accumulated drift.

Decision 67:
Problem: DiegeticMenuRaycastReceiver slider clicks could leave stale pressed ownership when a slider Down hit resolved to a target but TryApplySliderValue failed because the coordinate was invalid. The write path also needed an explicit finite-only proof before Slider.normalizedValue mutation.
Solution: On invalid slider Down, clear _pressedControlIndex and return. In TryApplySliderValue, reject non-finite local slider coordinates and non-finite normalized values before writing math.saturate(normalized). Add AssertRaycasterSliderWritesFiniteOnly to make the source contract enforceable.
Rejected Alternatives: Trusting IsCanvasHitPointInsideReference alone or letting Hold/Up repair a bad Down. A physical slider press is an ownership event; if the value cannot be computed, the press must fail closed immediately.
Scalability potential: Low = noisy or edge-case pointer data cannot latch a slider on weak devices. Middle = settings sliders stay deterministic during camera/panel transitions. High/Ultra = richer slider haptics/audio and CRT polish can scale without changing input truth.
Hardware Impact: Two finite checks and one failure branch in the slider path only. No allocation, no registry lookup, no component search, no DataVault lock. Prevents stale press state and invalid value propagation.

Decision 68:
Problem: DiegeticMenuRaycastReceiver.ReceiveCanvasInput returned immediately when _canvasRoot was null or _controlCount was zero. That preserved old hover/press/pending-selection state across an empty cache, disabled panel, or partial rebuild.
Solution: Call ClearInteractionState before the early return and include ClearInteractionState in the raycaster hot graph scan. The same LateFrame null-selection route now handles missing canvas/cache just like a physical miss.
Rejected Alternatives: Treating missing canvas/cache as a cold impossible state or clearing only _pressedControlIndex. A dead panel must not preserve any stale physical control ownership.
Scalability potential: Low = weak-device rebuild timing cannot leave stale menu focus. Middle = panel enable/disable transitions stay deterministic. High/Ultra = richer nested diegetic panels can rebuild without stale hover/click leakage.
Hardware Impact: Only executes on an invalid/empty panel event. It is three integer stores and no allocation, no registry lookup, no component search, no DataVault lock.

Decision 69:
Problem: PauseMenuController.ClearPauseSelection wrote EventSystem.SetSelectedGameObject(null) immediately during close/exit paths. Normal close is command-driven outside the LateFrame presentation flush, so focus clearing did not share the same phase contract as default selection.
Solution: Add _hasPendingPauseSelectionClear and FlushPendingPauseSelectionClear. ClearPauseSelection now cancels pending default focus, queues the clear, and only invokes the flush immediately when the controller cannot receive LateFrame anymore.
Rejected Alternatives: Leaving clear as a cold exception or clearing all EventSystem focus every frame. Close/exit is still presentation state; it needs one flush lane, not another direct write.
Scalability potential: Low = slow weak-device pause close cannot race stale UI focus against player input restoration. Middle = save/settings section focus remains deterministic. High/Ultra = richer pause camera routes can scale without changing focus ownership.
Hardware Impact: Normal path adds one bool check in LateFrame and removes immediate EventSystem writes from close commands. No allocation, no registry lookup, no component search, no DataVault lock.

Decision 70:
Problem: MenuCameraController assumed camera Transform position/rotation and authored route targets were finite. A corrupted transform or serialized state could push NaN into Bezier controls, slerp output, and interaction gates.
Solution: Add ResolveSafePosition and ResolveSafeRotation. Configure sanitizes the base transform, BeginRoute sanitizes start/target poses, Advance repairs invalid duration and sanitizes interpolated pose before writing the camera Transform.
Rejected Alternatives: Trusting scene authoring or only sanitizing delta time. Camera pose is the physical menu authority; if it becomes non-finite, focus gates and handoff routes can stall.
Scalability potential: Low = corrupted/weak-device timing cannot break the physical menu camera. Middle = panel travel remains deterministic. High/Ultra = wider parallax and handoff tilt can scale without NaN propagation.
Hardware Impact: A few finite checks only during configure, route start, and active route advance. No allocation, no registry lookup, no component search, no DataVault lock.

Decision 71:
Problem: MainMenuValidator still encoded the old screen-space mental model by treating GraphicRaycaster presence as a successful UI-input check. That editor validator could guide future authors back toward the exact non-diegetic route the 1611 batch removed.
Solution: Rewrite MainMenuValidator as a diegetic ownership validator: EventSystem is only input-module support, Canvas must be WorldSpace, and enabled GraphicRaycaster is a failure. Add a smoke-test source assertion so the validator policy is locked.
Rejected Alternatives: Leaving the validator stale because it is editor-only. Editor-only guidance is still production risk when it rewards forbidden authoring.
Scalability potential: Low = weak-device main menu remains on the physical panel raycaster without Unity GraphicRaycaster cost. Middle = authoring checks match the runtime path. High/Ultra = richer CRT/panel polish can scale without reintroducing screen-space input ownership.
Hardware Impact: 0 runtime us. Editor validation only. It prevents a future enabled GraphicRaycaster regression that would add per-input canvas traversal and duplicate hit ownership.

Decision 72:
Problem: DiegeticMenuRaycastReceiver deferred EventSystem focus to LateFrame, but still called SetSelectedGameObject even when EventSystem already held the same target. That is not an allocation issue, but it is unnecessary presentation churn and can trigger avoidable UI selection callbacks.
Solution: Compare eventSystem.currentSelectedGameObject with the resolved targetObject inside FlushPendingSelection and return without writing when they match. Lock the behavior with a smoke source assertion.
Rejected Alternatives: Clearing/reselecting every LateFrame for simplicity, or treating duplicate focus writes as harmless. The focus route is VISUAL_SYNC state; unnecessary writes weaken deterministic phase reasoning.
Scalability potential: Low = weak machines avoid redundant EventSystem callback traffic during hover hold. Middle = stable focus on pause/settings panels. High/Ultra = richer hover audio/haptics can scale without duplicate focus churn.
Hardware Impact: One reference compare only when a pending selection flush exists. Estimated under 0.1 us; zero allocation, no registry lookup, no DataVault lock.

Decision 73:
Problem: MainMenuController.Start() seeded _lastUnscaledTickTime directly from SystemDispatcher.CurrentUnscaledTimeSeconds while later delta/visual sync paths used finite guards. A non-finite dispatcher sample at startup could poison presentation delta before GetUnscaledDeltaTime had a chance to repair it.
Solution: Route Start(), retry timing, cancel debounce, delta calculation, and main/pause visual sync through ResolveCurrentUnscaledTimeSeconds helpers. Clamp main-menu presentation delta to MaxMenuPresentationDeltaSeconds and add smoke assertions that reject direct dispatcher seeding.
Rejected Alternatives: Guarding only GetUnscaledDeltaTime or using Unity Time directly. Presentation timing needs one local sanitization route and should stay independent from gameplay simulation time.
Scalability potential: Low = no camera/menu freeze from invalid time on weak hardware. Middle = smoother panel travel under load. High/Ultra = longer camera splines and CRT style sync can scale without NaN propagation.
Hardware Impact: A few finite checks and one min clamp on presentation paths. No allocation, no hot component lookup, no GlobalRegistry.Get<T>, no DataVault lock.

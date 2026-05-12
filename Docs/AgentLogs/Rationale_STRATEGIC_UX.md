# Rationale_STRATEGIC_UX

## Session Start

Problem: UX/input audit must identify cross-platform failures without mutating runtime architecture during parallel agent work.
Solution: Treat this as a source-evidence report. Use GlobalRegistry/EventBus contracts as the target decoupling pattern; flag concrete hardware API checks and layout rigidity as violations.
Rejected Alternatives: Runtime refactor in this pass; direct dependencies on systems that may be under active rewrite; Unity scene mutation without a specific implementation task.
Scalability potential: Low uses fixed action state, low-cadence UI RTs, matrix/font scale clamps; Middle uses higher RT cadence and 60Hz cursor; High/Ultra use richer visor layers, foveated rendering, 120Hz standardized input/replay capture, and device-specific haptics only behind PAL.
Hardware Impact: i3/MX350 gains are expected from removing per-tool hardware polling and avoiding per-frame UI layout/render churn. Exact gains remain PENDING VERIFICATION until profiler captures exist.

Mandates followed: CTRL_Device_Abstraction_Haptics; UI_Diegetic_Physical_Interfaces; UI_Localization_Babel_RTL_FontSwap_ZeroAlloc; UI_Data_Streaming_ZeroGC_Optimization; REND_VR_Stencil_Masking; REND_Foveated_Simulation_LOD; PHYS_Kinematic_Interaction_Hands; OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.

## Decision 1 - Input Boundary

Problem: Tools must not branch on hardware presence. Direct XR state reads outside Core make mouse, Deck, and VR flows diverge.
Solution: Keep XR/OpenXR/InputSystem crossing inside Core PAL/InputDispatcher. Downstream systems read `PlayerInputState`, action bitmasks, pointer capability, and hand-pose snapshots from `GlobalRegistry.Input` or a zero-GC pose provider.
Rejected Alternatives: `XRDevice.isPresent`, `XRSettings`, or `HectonXRRuntimeState.IsXRActive` inside tools/panels; per-tool mouse/VR branches; action logic inside controller-specific scripts.
Scalability potential: Low uses action bits and one cursor ray; Middle adds Deck trackpad/gyro flags; High adds XR controller pose; Ultra adds richer hand skeleton/haptic lanes without changing tool code.
Hardware Impact: i3/MX350 estimate is 5-20 us saved per active tool/UI frame by removing repeated hardware-mode branching and device polling outside the bridge. More importantly, this prevents divergent replay and QA matrices.

## Decision 2 - Dynamic Diegetic HUD Layout

Problem: `DiegeticHudManualLayout` is fixed-offset lane math. It does not know screen safe area, 800p Deck constraints, VR comfort cone, language expansion, or angular glyph size.
Solution: Add a deterministic `HudViewportMetrics` layout pass that consumes physical resolution, aspect, FOV, reference pixels per meter, comfort cone, language bucket, and scale tier. Output fixed-array slot transforms and text scale tiers only when the metrics hash changes.
Rejected Alternatives: Manual per-platform bone offsets; Unity layout groups on the runtime hot path; relying on projection canvas scale alone to solve text overlap.
Scalability potential: Low collapses optional visor elements and uses coarse slots; Middle keeps standard slots at 60 Hz data cadence; High adds curved comfort-band variants; Ultra adds richer peripheral layers and extra accent glyphs.
Hardware Impact: i3/MX350 estimate is 22-60 us saved per layout refresh versus generic dynamic layout, while preventing unreadable overlaps that force later emergency rebuilds.

## Decision 3 - VR Somatic Latency Cheat

Problem: VR comfort cannot wait on the same simulation cadence as semantic HUD data. Current code has late-frame lanes but no before-render HUD reprojection.
Solution: Use the cinematic cheat: freeze semantic HUD data at fixed cadence, then update only visor pose/shader reprojection from latest head pose in the latest presentation hook available. Keep the simulation deterministic and make presentation feel fresh.
Rejected Alternatives: Full HUD RT rerender every frame under sim pressure; tying visor canvas pose strictly to simulation tick; treating 16.6 ms flat-screen cadence as acceptable VR somatics.
Scalability potential: Low uses pose-only canvas late update and coarse RT cadence; Middle adds shader UV offset; High adds per-eye stencil/foveated visor layers; Ultra adds richer head-pose matrix reprojection and extra overdraw only inside visor mask.
Hardware Impact: i3/MX350 estimate is 300-800 us equivalent budget preserved during VR stress by avoiding full HUD rebuild/rerender on missed sim frames. Runtime proof is still required.

## Decision 4 - Contextual Babel Readability

Problem: Babel supports locale scaling and zero-GC font swaps, but not physical readability. Deck 800p and VR angular text size need different decisions than desktop font size.
Solution: Introduce `HudTextReadabilityContext` and pooled SDF readability buckets. Font scale must account for display class, projected pixel height, angular glyph height, language expansion, foveal/peripheral zone, and user readability preference.
Rejected Alternatives: Static NASA-punk font sizes; per-frame TMP preferred-size scanning; unique material instances per label; using locale overflow scaling as a substitute for device readability.
Scalability potential: Low uses larger body text, fewer labels, coarse SDF buckets; Middle uses standard buckets; High uses distance/foveal buckets; Ultra adds sharper critical glyph passes while keeping bucketed material writes.
Hardware Impact: i3/MX350 estimate is 10-40 us saved per 100 labels by bucketed SDF updates versus unbounded per-label writes. Existing SetCharArray paths already avoid string allocation spikes.

## Decision 5 - Standardized Input Tick

Problem: `DodReplayRecorder` records raw hardware event cadence. A 1000 Hz mouse and 60 Hz VR controller produce different ring pressure and different replay semantics.
Solution: Record authoritative input as a 120 Hz normalized `PlayerInputState` tick stream with action bits, move/look deltas, trigger/grip, pointer ray, hand pose validity, platform flags, and raw event sequence ranges for diagnostics.
Rejected Alternatives: Treating raw `InputSystem.onEvent` as authoritative replay; trusting `Time.frameCount` plus precision timestamp to normalize device sampling; increasing only raw event capacity.
Scalability potential: Low records action/move/look only; Middle adds Deck trackpad/gyro flags; High adds XR pose validity; Ultra adds hand skeleton/haptic lanes as optional sidecars.
Hardware Impact: i3/MX350 estimate is 50-300 us spike avoidance during high-rate input bursts. A 512 raw-event journal lasts 0.512 s at 1000 Hz mouse input but 8.53 s at 60 Hz VR input; a 2048 normalized 120 Hz tick ring lasts 17.07 s deterministically.

# Status_UI_DIEGETIC_INPUT

PROMPT: UI_DIEGETIC_INPUT
ROLE: INTERACTION_MASTER
DOMAIN: ECHELON 8 PRESENTATION & UX / Diegetic Terminals (3D UI)
TASK COUNT: 20
STATUS: PENDING VERIFICATION

Mandates loaded:
- UI_Diegetic_Physical_Interfaces
- UI_Data_Streaming_ZeroGC_Optimization
- CORE_Tools_Equipment_Interaction_Raycast_Heat
- PHYS_Kinematic_Interaction_Hands
- CTRL_Device_Abstraction_Haptics
- REND_VR_Stencil_Masking
- OPT_Zero_GC_Policy_AllocFree_Mandate
- MATH_Coordinate_Precision_AUP_FloatingOrigin

## Loop 0 - Setup
- [x] Extract XML prompt from CURRENT_BATCH | DOD: strict id-scoped CLI extraction to `_extracted_UI_DIEGETIC_INPUT.txt`; task count verified as 20 | Rejected: neighbor prompt inference, MCP truncation | Estimate: 55 us
- [x] Verify status/rationale hygiene | DOD: Test-Path showed both files missing before creation | Rejected: appending to stale batch state | Estimate: 20 us
- [x] Load relevant mandates | DOD: 8 domain mandates loaded before coding | Rejected: broad registry ingestion, because prompt requires relevant mandates only | Estimate: 210 us

## Loop 1 - Tasks 1-5
- [x] Task 1: Mouse-to-world screen projection | DOD: `TryProjectRayToPanel` projects gaze/mouse ray into panel-local UV/canvas space without physics cursor raycast | Rejected: `GraphicRaycaster`/2D cursor/event-system hit testing | Estimate: saves 80-180 us per active terminal frame
- [x] Task 2: Reciprocal UV math | DOD: projection uses `math.rcp(denom)` and `math.rcp(safeCanvasSize)`; no division in Mouse-to-UV path | Rejected: direct `/` per cursor solve | Estimate: saves 1-3 us per hot panel solve
- [x] Task 3: Hand-IK targeting | DOD: `KinematicTerminalInteractionBridge` resolves canvas snap/world point and emits physical hand target through existing IK sink/hand controller boundary | Rejected: direct Animation Lead concrete dependency | Estimate: saves integration churn; runtime cost bounded to existing bridge tick
- [x] Task 4: Virtual keyboard 0-GC | DOD: `PhysicalTerminalKeyboard` uses fixed `char[128]`, fixed key map, `TMP_Text.SetCharArray` | Rejected: string concatenation/input-field text mutation | Estimate: saves 30-120 us and managed garbage per keypress
- [x] Task 5: Platform abstraction layer | DOD: terminal logic reads `IInputService.GetState()` with action flags and new cached `ScrollDelta`; New Input System stays hidden in dispatcher/input manager | Rejected: per-control `InputAction` reads/string lookups | Estimate: saves 5-25 us per terminal frame

## Loop 2 - Tasks 6-10
- [x] Task 6: Haptic feedback bridge | DOD: keyboard, kinematic terminal bridge, physical buttons, and snap switches enqueue fixed `ToolHapticsRuntime` commands | Rejected: direct controller calls per widget | Estimate: saves 15-60 us and avoids haptic allocation paths per press
- [x] Task 7: Stencil UI clipping | DOD: diegetic panel shader exposes `_StencilComp/_StencilRef/_StencilReadMask` and uses a Stencil block to keep monitor UI inside physical masks | Rejected: rectangular Canvas clipping as physical-frame substitute | Estimate: saves overdraw/bleed correction passes; GPU proof pending
- [x] Task 8: CRT distortion shader | DOD: shader applies CRT curvature, scanlines, analog jitter, and damage glitch ALU on sampled RT | Rejected: mesh deformation or second camera shake | Estimate: buys visual distortion for shader ALU only, no CPU cost
- [x] Task 9: Interaction reach gate | DOD: `ResolveEffectiveInteractionDistance()` hard-caps panel ray interaction at 2 m and `IsRayOriginWithinAupInteractionRange` uses AUP distance squared | Rejected: runtime-world `Vector3.Distance` and serialized >2 m reach | Estimate: saves sqrt and prevents cross-origin precision drift
- [x] Task 10: Lever dragging | DOD: `PhysicalSnapSwitch.TryQueueHandPress` maps repeated hand-local positions to switch state and publishes queued interaction signals | Rejected: UI click toggle with no physical hand position | Estimate: saves EventSystem path; physical hand probe is existing NonAlloc overlap path

## Loop 3 - Tasks 11-15
- [x] Task 11: Dial rotation | DOD: `PhysicalPanelDial` maps PAL scroll delta to clamped local knob rotation and ships with `.meta` | Rejected: UI slider/scrollbar or raw New Input dependency | Estimate: saves 40-120 us versus EventSystem slider path per interaction
- [x] Task 12: Screen glitch on damage | DOD: `DiegeticPanelController` implements `IDamageReceiver`, drives `_TerminalDamageGlitch`, and shader applies hashed CRT UV bands | Rejected: combat concrete registration, camera shake, Rigidbody screen motion | Estimate: saves 50-200 us CPU versus camera/UI rebuild shake
- [x] Task 13: Zero-GC tooltips | DOD: `DiegeticTooltipSystem.ShowDiagnostic` accepts `ReadOnlySpan<char>` and renders fixed glyph buffers | Rejected: TMP string rebuild/world-space canvas per target | Estimate: saves 25-150 us and avoids per-tooltip GC
- [x] Task 14: Hand proximity hover | DOD: `KinematicTerminalInteractionBridge` sends hover/snap world targets to IK sink or hand controller while cursor resolves panel hit | Rejected: cursor-only hover with no physical hand movement | Estimate: existing bridge cadence amortizes IK targeting to 0.1-0.2 s on low tier
- [x] Task 15: Terminal audio | DOD: keyboard presses, dial ticks, snap switches, and panel buttons route mechanical feedback through `IAudioService.QueueAudioEvent` or existing global audio service | Rejected: spawning `AudioSource`/direct clip mutation per press | Estimate: saves 50-300 us and transient GC per mechanical interaction

## Loop 4 - Tasks 16-20
- [x] Task 16: Avoid PointerOverGameObject | DOD: terminal path contains no `EventSystem.current.IsPointerOverGameObject`; panel disables cached `GraphicRaycaster` | Rejected: Unity pointer-over-UI query | Estimate: saves 20-80 us and allocation risk per pointer test
- [x] Task 17: Button highlight states | DOD: `PhysicalTerminalKeyboard.ResolveButtonHighlightState` uses `math.select` bit assembly | Rejected: branch-heavy color state object updates | Estimate: saves 1-5 us per keyboard hover update
- [x] Task 18: Terminal boot log | DOD: `HectonSubmarineOsDisplay` uses fixed log/history/render buffers, `Span<char>.TryFormat`, and `SetCharArray` | Rejected: string log concatenation | Estimate: saves 40-200 us and managed garbage per log refresh
- [x] Task 19: Flashlight glare | DOD: shader exposes `_FlashlightGlare`; `DiegeticPanelController.SetFlashlightGlare` drives material float with saturation | Rejected: real light/camera exposure mutation per terminal | Estimate: buys readability penalty with shader ALU only
- [BLOCKED BY DEPENDENCY] Task 20: Omega compile check | DOD: `PhysicalPanelDial.cs.meta` generated and edited files have no Cyrillic matches; compile cannot fully pass because unrelated World/Fauna/Voxel/package errors block `Hecton8.Core`/solution | Rejected: editing non-domain compile blockers | Estimate: 0 us runtime; verification wall external

## Loop 5 - Self-Inquisition
- [x] Read own code pass 1 | Finding: kinematic bridge events lacked scroll/analog payload for dials; fixed by forwarding `ScrollDelta` and ORing `DiegeticPanelInputEventType.Scroll`
- [x] Read own code pass 2 | Finding: physical panel button audio was clip-first; fixed with NativeQueue `AudioEvent` preference when event id is authored
- [x] Read own code pass 3 | Finding: terminal code still references `GraphicRaycaster`, but only to cache and disable it; no `EventSystem.current.IsPointerOverGameObject` use found
- [x] Read own code pass 4 | Finding: edited terminal files contain no Cyrillic matches and `PhysicalPanelDial.cs.meta` exists
- [x] Read own code pass 5 | Finding: targeted builds still pass for `Assembly-CSharp`/`Hecton8.Input`; `Hecton8.Core` remains blocked by unrelated construction/save/physics compile errors

## Verification
- [ ] Compile check | STATUS: PENDING VERIFICATION; `Hecton8.Input.csproj` and `Assembly-CSharp.csproj` passed after final edits, `Hecton8.Core.csproj` blocked by external Construction/Physics errors
- [ ] GC proof | STATUS: measured proof absent
- [ ] Unity runtime/MCP proof | STATUS: PENDING VERIFICATION

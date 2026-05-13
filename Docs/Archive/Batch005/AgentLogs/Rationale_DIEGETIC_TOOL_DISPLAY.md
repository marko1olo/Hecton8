# Rationale: DIEGETIC_TOOL_DISPLAY

Status: PENDING VERIFICATION

## Initial Mandate Selection
Problem: Floating screen-space tool HUD violates VR/diegetic UI requirements and creates hot-path UI allocation risk.
Solution: Use local tool-surface rendering, shared RT budget, shader bars/flashes, and preallocated char buffers with `TMP_Text.SetCharArray()`.
Rejected Alternatives: Screen-space overlay and `CanvasScaler` HUD are rejected because they break VR presence and trigger Canvas rebuild paths; per-tool RT allocation is rejected because it burns VRAM on MX350.
Scalability potential: Low disables RT camera and uses static emissive plus visor fallback; Middle uses shared 256 RT; High can raise update cadence; Ultra can spend saved cycles on stronger scanline/glitch/readability shaders.
Hardware Impact: Estimated low-end gain is avoiding canvas rebuild and per-tool RT churn, target under 0.1 ms CPU and about 0.13 MB color RT plus depth for active tool at 256x256 RGB565/D16. Exact profiler data absent.

## Decision: Tool State Signal Lane
Problem: Tool UI needed ammo/heat/distance without reaching into singleton UI managers or tool owner internals.
Solution: Added `ToolStateChangedSignal` as a 32-byte `GlobalSignals` packet with latest-sequence access and NativeQueue support; `ModularEquipmentEngine` publishes equipment state mirrors and holster/overcharge exits.
Rejected Alternatives: Direct `WeaponUIManager.Instance`, scene search, or UI-side polling of each tool were rejected because they couple presentation to ownership and create unstable dependencies between parallel agent domains.
Scalability potential: Low uses latest signal only for visor scalar fallback; Middle/High/Ultra can add richer shader response without changing the cross-domain contract.
Hardware Impact: Estimated 3-8 us CPU for <=16 tool state packets; no managed allocation; queue capacity fixed at 64.

## Decision: Local RT Tool Display
Problem: Floating HUD breaks VR presence and forces screen-space canvas rebuild paths.
Solution: `ToolDiegeticDisplayController` drives an orthographic local camera into one 256x256 RT and binds it to the 3D tool screen through a persistent `MaterialPropertyBlock`.
Rejected Alternatives: Keeping corner HUD or allocating permanent per-tool RTs was rejected because it wastes VRAM and makes VR unreadable.
Scalability potential: Low disables the RT camera and uses static emissive fallback; Middle renders on dirty state; High can raise update cadence; Ultra can layer stronger scanline/overheat shader effects.
Hardware Impact: Active cost is one 256 RT camera only when equipped and visible; low tier saves the camera pass and approximately 128 KB RGB565 color plus depth residency.

## Decision: Shader Bars and Critical Flash
Problem: UGUI `Image.fillAmount` creates canvas dirtiness for bars and CPU-side color animation risks allocations.
Solution: `Hecton_ToolScreenDiegetic.shader` reads `_ToolHeat01` and draws heat/battery bars in fragment code, with mathematical inversion when heat is critical.
Rejected Alternatives: UI Images, animators, and CPU color tweens were rejected because they touch the canvas/rebuild lane.
Scalability potential: Low displays static emissive tint plus visor scalars; Middle uses shader bars; High/Ultra can increase overheat pulse detail without C# changes.
Hardware Impact: Moves bar updates to cheap ALU; expected CPU saving is the avoided Canvas rebuild path, exact profiler data blocked by current compile errors.

## Decision: Zero-GC Text Path
Problem: Ammo, heat, and distance labels can become frame-rate garbage if formatted through strings.
Solution: Preallocated two `char[96]` buffers, wrote numbers through `ZeroGCFormatter.FastIntToChars`, and pushed labels through `TMP_Text.SetCharArray()`.
Rejected Alternatives: `string.Format`, interpolation, `ToString`, `TMP_Text.SetText`, and hot `StringBuilder` were rejected because they allocate or hide boxing/format provider paths.
Scalability potential: Low can disable text RT entirely; Middle/High/Ultra keep labels with no GC pressure, freeing frame budget for visual treatment.
Hardware Impact: Estimated 0 B GC per update and <10 us CPU for both labels on i3-class hardware.

## Decision: Prefab Canvas Boundary
Problem: Task required eradication of standard Canvas/CanvasScaler from the player HUD overlay, but existing suit HUD still uses a root Canvas as its projection surface.
Solution: Removed `CanvasScaler` from `Suit_HUD_Canvas.prefab` and kept `HectonUIScaler`; blocked root Canvas deletion until a replacement non-UGUI visor presenter exists.
Rejected Alternatives: Raw prefab YAML surgery and deleting the root Canvas were rejected because they would break existing visor projection and hide the failure under asset corruption.
Scalability potential: Low avoids scaler relayout now; future full replacement can move visor projection to mesh/RT while keeping this tool screen independent.
Hardware Impact: Removes CanvasScaler layout work from the prefab; exact microseconds saved require profiler after unrelated compile blockers clear.

## Decision: Contracts-Only Assembly Boundary
Problem: Prompt demanded `Hecton8.UI.Tools -> Contracts`, but the required signal bus, formatter, and RT pool are currently defined in `Hecton8.Core`.
Solution: Created `Hecton8.UI.Tools.asmdef` with `Hecton8.Core.Contracts` plus the existing `Hecton8.Core` dependency and marked full contract-only isolation blocked.
Rejected Alternatives: Duplicating signal structs/formatter logic inside UI was rejected because it would fork contracts and create silent desync.
Scalability potential: After contract migration, UI tools can compile against a thinner contract surface without changing the runtime behavior.
Hardware Impact: No runtime cost; compile graph hygiene only.

## Decision: Blackbox N/A
Problem: Blackbox buffer mandate applies to critical Physics, Voxel, and AI systems; this work is presentation UI.
Solution: Recorded N/A and used deterministic fallback states instead: holster disables camera, low tier disables RT, missing RT binds static emissive.
Rejected Alternatives: Adding a NativeArray dump for non-critical UI was rejected because it burns memory and does not explain simulation crashes.
Scalability potential: Low/Middle/High/Ultra all fail closed to visible fallback rather than simulation divergence.
Hardware Impact: Avoids pointless NativeArray residency and dump IO in UI.

## Decision: Compile Wall Boundary
Problem: Unity compile is currently blocked outside this work by duplicate methods in `HectonFluidEngine.cs` and duplicate `Hecton8.Vehicles.Physics.Contracts` asmdef reference.
Solution: Stopped after recording evidence; no unrelated fluid/vehicle asmdef code was edited from this UX task.
Rejected Alternatives: Fixing unrelated compile blockers was rejected because it violates domain boundary and risks stepping on other active agents.
Scalability potential: Once blockers clear, the tool display can be profiled for exact microseconds and OpenXR readability.
Hardware Impact: Current savings remain static estimates; objective profiler numbers are blocked by the cross-domain compile wall.

## Decision: Pool-Only Render Texture Hardening
Problem: The first pass had a single-instance emergency `new RenderTexture` fallback when `GlobalRegistry.RenderTexturePool` was absent. That violates the stricter interpretation of the shared RT pool mandate and could hide pool bootstrap failures.
Solution: Removed direct RT construction from `ToolDiegeticDisplayController`; the controller now caches the pool reference, records the owning pool for return, degrades to static emissive when the pool is unavailable, and retries pool resolution every 2 seconds instead of polling every frame.
Rejected Alternatives: Keeping emergency local allocation was rejected because it weakens VRAM accounting; retrying the registry every visible tick was rejected because it adds pointless hot-path work when the pool is missing.
Scalability potential: Low/MX350 never pays the RT pass; Middle rents one 256 RT on dirty visible frames; High/Ultra use the same pool contract while spending only shader ALU for visual overkill.
Hardware Impact: Low-end silicon avoids the emergency RT allocation entirely; missing-pool failure mode becomes 0 camera pass, static emissive, and one registry retry every 2 seconds.

## Decision: Tiered Shader Visual Overkill
Problem: The first shader had one visual path for all non-low tiers, which left no explicit way to spend saved cycles on high-end devices.
Solution: Added `_ToolVisualOverkill01` as a global tier scalar: 0 for Unknown/Low/MX350, 0.33 for Mid, 0.66 for High, 1.0 for Ultra. The shader adds a light grid and data sweep only when this scalar is non-zero and fallback is off.
Rejected Alternatives: Higher-resolution RTs and extra camera passes were rejected because the prompt caps the display at shared 256x256 and the camera must stay dirty-render only.
Scalability potential: Low = static emissive; Middle = restrained scan/grid; High = stronger data sweep; Ultra = full overkill shader layer with no extra CPU allocation.
Hardware Impact: MX350 cost remains unchanged; high-end spends a few fragment ALU instructions on a tiny 256 texture surface instead of adding CPU/UI work.

## Decision: Invisible RT Residency Hysteresis
Problem: An equipped tool that stopped being renderer-visible could keep its pooled 256x256 RT resident indefinitely, even while the camera was disabled.
Solution: Added a 0.75 second invisible-release timer. Holstered and low-tier states still release immediately; transient renderer visibility flicker keeps the RT briefly to avoid pool churn; sustained non-renderability returns the RT and binds static emissive fallback.
Rejected Alternatives: Immediate release on every `Renderer.isVisible == false` was rejected because culling flicker can cause rent/return churn; retaining the RT forever was rejected because it wastes MX350 VRAM budget.
Scalability potential: Low/MX350 still has no RT residency; Middle/High/Ultra keep the 256 RT only while the physical screen is actually renderable or briefly between culling transitions.
Hardware Impact: Saves approximately one active 256 RGB565+D16 RT residency per hidden equipped tool after 0.75s, while keeping CPU work to one float accumulator and two branch checks per tick.

## Decision: Status Mask and Tool Type Display
Problem: The latest state packet carried `_statusMask` and `_toolTypeId`, but the physical screen still rendered only ammo, heat, battery, and distance. That left fault state invisible and made the type signal dead data.
Solution: Added deterministic status-bucket resolution over `ToolRuntimeStatusMasks`, wrote compact `OK/PWR/HOT/BRK/DPT/OFF` tokens into the existing secondary char buffer, drove `_ToolFault01` for shader warning tint/pulse, and drove `_ToolTypeHue01` only through the existing high-tier overkill shader lane.
Rejected Alternatives: Extra TMP labels, localized strings, material instancing, and CPU color animation were rejected because they increase layout surfaces, introduce string pressure, or clone renderer materials.
Scalability potential: Low/MX350 still sees static emissive plus visor scalar fallback; Middle gets the compact status token; High/Ultra spend a few fragment ALU instructions on fault pulse and type hue without raising RT resolution or camera count.
Hardware Impact: Text path remains 0 B GC; added CPU work is bit tests and one cached global-float update on dirty state. MX350 shader cost remains bypassed by fallback/overkill gates; high-end buys clearer fault readability on the 256 surface.

## Decision: Active Tool Signal Gating
Problem: `ModularEquipmentEngine.Tick()` calls `WriteSlotMirrors()` for every tracked tool. Because the diegetic controller consumes the latest signal, an unequipped tool in a later slot could overwrite the equipped tool packet and disable the physical screen even while the active tool was still in-hand.
Solution: Gate `PublishToolStateChanged()` so normal packets are emitted only for equipped tools; explicit force-holster/failure/unregister packets still publish. Force-holster packets now set `Disabled` and clear `Active` in the emitted status mask.
Rejected Alternatives: Polling all queued signals in the UI was rejected because it moves ownership filtering into presentation and increases per-frame queue drain work. Adding a new direct dependency on `PlayerToolManager` from UI was rejected because it breaks the signal migration boundary.
Scalability potential: Low/MX350 receives only the active fallback telemetry instead of N tracked-tool packets per tick; Middle/High/Ultra keep one authoritative active packet for the 256 RT surface.
Hardware Impact: Worst-case signal traffic drops from up to `MaxTrackedTools` packets per tick to the equipped tool plus explicit shutdown packets. Estimated saving is a few microseconds per additional tracked inactive tool and prevents false camera shutdowns.

## Decision: Tiered Active Signal Delta Gate
Problem: After active-tool gating, the producer still emitted one packet every tick for an unchanged equipped tool. That kept `ToolDiegeticDisplayController` waking its dirty-state path even when the visible ammo/heat/battery/status/distance payload had not materially changed.
Solution: Build the next `ToolStateChangedSignal`, then compare it against the current latest packet for the same tool before publishing. Exact changes to flags, status, ammo, type, or tool hash publish immediately. Scalar battery/heat/durability changes use tier-aware deltas: 0.02 Low/MX350/Unknown, 0.01 Mid, 0.005 High, 0.0025 Ultra. Force-holster packets bypass this drop gate.
Rejected Alternatives: UI-side throttling was rejected because it still fills the signal queue and dirties the presentation path. A fixed threshold was rejected because Low/MX350 and Ultra should not pay or animate at the same precision.
Scalability potential: Low/MX350 gets coarse, stable fallback telemetry; Middle keeps 1 percent resolution; High/Ultra get finer signal cadence for smoother bars and warning response without increasing RT size or adding a camera pass.
Hardware Impact: Idle active-tool signal traffic becomes 0 packets after the first stable value. Estimated saving is 3-8 us per unchanged active tick plus avoided dirty-state processing in the UI controller; Ultra intentionally spends more packets only when scalar motion is visible.

## Decision: One-Shot Holster Transition Signal
Problem: The active-signal gate correctly stopped inactive tools from spamming, but it could also hide a plain `owner.IsEquipped` transition to false if the tool did not immediately unregister. The latest packet could remain flagged as equipped until a force-holster path ran.
Solution: Added `_lastPublishedEquippedMask` as a 16-slot bitmask. A normal equipped publish sets the slot bit. If the same slot later reaches `PublishToolStateChanged()` unequipped without `forceHolstered`, it emits exactly one disabled packet, clears `Active`, clears the bit, and then suppresses repeat inactive packets.
Rejected Alternatives: Publishing every unequipped tick was rejected because it reintroduces inactive-slot signal spam. Moving lifecycle checks into `ToolDiegeticDisplayController` was rejected because presentation should not infer owner lifecycle outside the signal contract.
Scalability potential: Low/MX350 still avoids repeated inactive traffic; Middle/High/Ultra get deterministic physical-screen shutdown on any equip-state transition.
Hardware Impact: Adds one uint bitmask and simple bit tests. Saves the previous packet reduction while preventing stale equipped state; steady unequipped cost returns to 0 packets after the one-shot transition.

## OMEGA POLISH CHANGES
Problem: Final mandate required anti-bloat inquisition after core tasks were checked or blocked.
Solution: Scanned touched display scope for managed `foreach`, `math.sqrt`, `Mathf.Sqrt`, `math.normalize`, `.normalized`, `string.Format`, `$"..."`, `.ToString()`, `new RenderTexture`, `SetText`, `new string`, and `Image.fillAmount`; no hits were found. Re-ran `dotnet build Hecton8.Core.csproj --no-restore -v:q`; failure remains cross-domain and unrelated to the new tool display lane, with 153 errors led by missing fluids, scheduling, memory layout, CCD, audio propagation, tether/acoustic, macro database, and save-layout contracts. Post-resume `validate_script` on the tool controller returned 0 errors / 0 warnings; after the status/fault patch the same validator again returned 0 errors / 0 warnings. After active-signal, tiered delta, and holster-transition gating, `validate_script` returned 0 errors / 0 warnings for `ModularEquipmentEngine.cs`, `ToolDiegeticDisplayController.cs`, and `VisorHUDController.cs`. Unity refresh reached idle, but console still reports unrelated duplicate `HectonUnderwaterVisuals.cs` methods and Burst/Hecton8.Prologue.Space resolution blockers, so full build verification remains pending.
Rejected Alternatives: Expanding into scanner/localization/string-format cleanup was rejected because those files are outside this prompt's UX tool-display domain and the project is running 20+ parallel agents.
Scalability potential: Low = static emissive + visor scalar fallback; Middle = one shared 256 RT camera on dirty visible state; High = higher update cadence if profiling allows; Ultra = spend saved CPU on shader scanline/critical flash overkill.
Hardware Impact: New hot display code remains 0 B GC by static proof; expected low-end gain is removal of CanvasScaler relayout and no camera pass while holstered or low-tier.

Exact cinematic cheats used:
- 256x256 local RT instead of a full screen-space HUD.
- Shader heat/battery bars from `_ToolHeat01` / `_ToolBattery01` instead of `Image.fillAmount`.
- Triangle-wave shader pulse for critical flash instead of CPU animation.
- Static emissive fallback texture on low tier instead of honest live RT rendering.
- Pool-missing fallback to static emissive instead of constructing a private RT outside the shared pool.
- Latest-signal snapshot instead of UI graph traversal or singleton ownership lookup.
- `math.rcp(capacity)` battery normalization in new code instead of direct division.
- Tier scalar `_ToolVisualOverkill01` lets high-end hardware buy extra shader scan/grid motion without changing CPU or RT cost.

Final Git Diff:
```text
Tracked diff stat for owned/touched paths:
Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab     |  50 +--
Assets/_Project/Scripts/Core/GlobalSignals.cs      | 364 ++++++++++++++++++++-
Assets/_Project/Scripts/ModularEquipmentEngine.cs  |  54 +++
Assets/_Project/Scripts/Visor/VisorHUDController.cs| 100 +++++-
Docs/AgentLogs/Rationale_DIEGETIC_TOOL_DISPLAY.md  |  56 ++++
Docs/Tasks/Status_DIEGETIC_TOOL_DISPLAY.md         |  61 ++--
ProjectSettings/TagManager.asset                   |   3 +-

Untracked owned files:
Assets/_Project/Scripts/UI/Tools/Hecton8.UI.Tools.asmdef
Assets/_Project/Scripts/UI/Tools/Hecton8.UI.Tools.asmdef.meta
Assets/_Project/Scripts/UI/Tools/ToolDiegeticDisplayController.cs
Assets/_Project/Scripts/UI/Tools/ToolDiegeticDisplayController.cs.meta
Assets/_Project/Art/Shaders/Hecton_ToolScreenDiegetic.shader
Assets/_Project/Art/Shaders/Hecton_ToolScreenDiegetic.shader.meta

Intentional tracked hunks:
- Suit_HUD_Canvas.prefab: removed CanvasScaler component reference and serialized block; root RectTransform restored after MCP mutation.
- TagManager.asset: added ToolUI layer at slot 23.
- GlobalSignals.cs: added ToolStateChangedSignal queue/latest lane, writer, publish/dequeue/latest accessors, 32-byte signal struct.
- ModularEquipmentEngine.cs: publishes equipped/visible/low-tier tool state and holster/overcharge shutdown signals.
- VisorHUDController.cs: low-tier fallback receives tool heat/ammo/distance/battery scalars through material properties.
```

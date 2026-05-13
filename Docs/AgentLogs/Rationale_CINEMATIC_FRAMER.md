# CINEMATIC_FRAMER Rationale

## Initial Technical Boundary

Problem: Narrative framing currently has no verified first-party soft look-at path, while the prompt forbids hard camera theft and heavyweight Cinemachine dialogue tracks.

Solution: Add an event-bus focus contract and consume it inside the existing player camera state composition so player input remains authoritative. The visual effect is a procedural camera target/fov bias, not a separate cutscene controller.

Rejected Alternatives: A Cinemachine virtual camera or direct CutsceneManager dependency would add ownership conflict, extra update ordering, and a hard-control failure mode. A new narrative camera singleton would violate the GlobalRegistry/EventBus boundary.

Scalability potential: Low disables FOV narrowing and keeps only cheap direction math; Middle uses soft nlerp only; High and Ultra can spend saved cycles on richer world-subtitle and audio response consumers once their owners exist.

Hardware Impact: Estimated normal-frame cost on i3/MX350 is below 10 microseconds for one active focus target because it is one AUP delta, one normalized direction, one fast nlerp, and no allocations. STATUS: PENDING VERIFICATION.

## Loop 1 Decisions

Problem: The prompt demanded purge of cutscene singletons and Cinemachine dialogue tracks, but first-party project scan found neither path under `Assets/_Project`.

Solution: Treat the scan as the purge evidence and avoid deleting unrelated archived/vendor code. The actual missing runtime piece was the focus signal lane and KCC camera consumer.

Rejected Alternatives: Removing unrelated `Quaternion.Slerp` or non-Cinemachine camera code would be scope creep and could damage other domains. Adding a `CutsceneManager` shim would recreate the dependency being purged.

Scalability potential: Low/MX350 pays only signal drain and one active focus vector; Middle keeps soft nlerp; High/Ultra can consume the same signal for richer world text and audio layers.

Hardware Impact: Baseline active-frame estimate remains under 10 microseconds on i3/MX350, compile proof blocked by existing asmdef dependency failures outside this domain. STATUS: PENDING VERIFICATION.

## Loop 2 Decisions

Problem: Narrative focus needed to influence view composition without stealing input or adding a heavyweight camera stack.

Solution: Drain `NarrativeFocusSignal` in the player camera path, compute the AUP target direction, and blend the existing camera rotation through `CinematicMath.FastNlerp`. Player look delta above threshold immediately breaks the focus and emits a signal. FOV narrowing is just a scalar target bias and is skipped by tier/VR gates.

Rejected Alternatives: `Quaternion.Slerp` was rejected for the focus path because it was explicitly banned and costs more than needed for this soft bias. Cinemachine/TMP subtitle fallback was rejected because it reintroduces camera ownership and string/canvas allocations. A root-space distance fade was rejected because origin shifts make it untrustworthy.

Scalability potential: Low uses the focus vector only and leaves FOV unchanged; Middle uses soft nlerp and squared-distance subtitle alpha; High can render BRG world text from the carried hashes; Ultra can layer richer focus effects without changing the camera consumer.

Hardware Impact: i3/MX350 active focus estimate is 4-8 microseconds: one signal drain budget, one AUP delta, one normalize, one nlerp, one FOV lerp when allowed. Distance fade avoids one sqrt per active frame. STATUS: PENDING VERIFICATION.

## Loop 3 Decisions

Problem: Creature head targeting and world subtitles cross into fauna/rendering ownership, while the prompt forbids invented dependencies and the domain file limits this agent to presentation/narrative camera.

Solution: The camera contract carries resolved target AUP, subtitle hash, world-subtitle flag, and creature/head-bone flags. Camera code consumes only the resolved AUP. Head-bone matrix resolution remains blocked until fauna exposes a stable head AUP/matrix provider or publishes the signal itself. BRG subtitle projection remains blocked until a renderer owner exposes the text-quad path.

Rejected Alternatives: Camera-side `Transform.Find("Head")`, direct dependency on `SargassumMicroFaunaBoids`, or TMP world text allocation would violate the zero-GC and boundary mandates. Editing fauna internals without an agreed interface would create cross-domain sabotage risk.

Scalability potential: Low/MX350 can ignore subtitle rendering and use only view bias; Middle can draw one BRG quad; High can add richer glyph material response; Ultra can add artifact-specific visual overkill while the camera remains the same cheap consumer.

Hardware Impact: Implemented camera path remains 0 B/frame. The black box is a fixed 300-entry NativeArray, cold allocated once and dumped only on fault. STATUS: PENDING VERIFICATION.

## Loop 4 Decisions

Problem: Focus state needed observable exits, audio ducking, VR safety, and proof that the implementation did not silently fall back to banned interpolation.

Solution: `FocusBrokenSignal` and `MixerStateSignal` were added as NativeQueue lanes. Focus lifecycle publishes the active focus hash to telemetry and writes fixed ring entries. VR comfort exits before any FOV or rotation bias. Static scan verifies the focus path calls `CinematicMath.FastNlerp`.

Rejected Alternatives: Direct AudioMixer edits, string telemetry, `Debug.Log` state dumps, or VR-specific camera overrides were rejected because they allocate, cross ownership, or hide behavior from the black-box dump.

Scalability potential: Low keeps audio/camera state changes edge-triggered; Middle consumes mixer ducking; High/Ultra can add layered ambient mix or focus bloom consumers without changing the signal contract.

Hardware Impact: Focus edges enqueue two small signals. Runtime telemetry cost is one fixed struct write per active frame. Expected low-end cost remains under 10 microseconds/frame for one focus. STATUS: PENDING VERIFICATION.

## OMEGA POLISH CHANGES

Problem: Anti-bloat audit required proof that the focus implementation did not use expensive honest simulation, hot allocations, or cross-domain shortcuts.

Solution: Kept the system as a visual fake: one AUP direction, nlerp rotation bias, scalar FOV bias, squared-distance subtitle alpha, bitmask flags, edge-triggered mixer/focus signals, and fixed-size NativeArray black box. Low tier disables FOV narrowing; VR exits before any focus rotation/FOV mutation. Fault dumps are binary and cold path only.

Rejected Alternatives: `math.normalize`, `Quaternion.Slerp`, `Vector3.Distance`, managed world subtitles, camera-side creature transform search, direct AudioMixer writes, and Cinemachine tracks were rejected. Existing BRG and fauna code were not modified because no public `UI_LOCALIZATION_BABEL` BRG text or head-bone AUP provider exists.

Scalability potential: Low = nlerp pull only and no FOV; Middle = nlerp plus squared-distance subtitle alpha for a future BRG renderer; High = BRG spatial glyph material response; Ultra = richer artifact/creature visual overkill driven by the same signal contract.

Hardware Impact: Low-end estimate remains under 10 microseconds per active focus frame. Exact cheats used: rsqrt-backed nlerp, squared-distance fade, bitmask flags, NativeQueue decoupling, edge-only audio ducking, and fixed ring telemetry. `dotnet build Hecton8.Core.csproj` remains blocked by global baseline contract errors; Unity MCP validation is blocked by `no_unity_session`. STATUS: PENDING DUE GLOBAL COMPILE DEPENDENCIES.

Final Git Diff: Current task footprint is `Assets/_Project/Scripts/HectonPlayerMovement.cs`, `Assets/_Project/Scripts/Core/GlobalSignals.cs`, `Assets/_Project/Scripts/Core/CinematicMath.cs`, `Assets/_Project/Scripts/HectonNarrativeDirector.cs`, `Assets/_Project/Scripts/Hecton8.Core.asmdef`, `Assets/_Project/Scripts/Narrative/Camera/CinematicMath.cs`, `Assets/_Project/Scripts/Narrative/Camera/Hecton8.Narrative.Camera.asmdef`, `Docs/Tasks/Status_CINEMATIC_FRAMER.md`, and `Docs/AgentLogs/Rationale_CINEMATIC_FRAMER.md`. `git diff --name-only` returned no pending local diff in the current workspace snapshot.

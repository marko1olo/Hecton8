# LOG_KINETIC_IMPACT_ACOUSTICS

## 2026-05-14 - DSP_ACOUSTIC_LEAD - Procedural Collision Audio
Status: PENDING VERIFICATION

What was wrong:
- High-speed collision energy had no procedural acoustic route through the central audio contract.
- Collision audio risked becoming another singleton or authored-clip path instead of using the project GlobalRegistry/EventBus lanes.
- Underwater impact tone needed a deterministic 800 Hz muffle without synchronous water/physics queries.
- Impact energy could be extreme or non-finite, which is unsafe for gain, distortion, and telemetry.
- The Burst oscillator compile surface initially used exact exponential filter decay, which was unnecessary for a short cinematic thud.

What was done:
- Extended `IAudioService` with `QueueHighSpeedImpactSignal(in HighSpeedImpactSignal signal)`.
- Implemented service routing in `SpatialAudioManager`: finite guards, AUP runtime conversion, passive radar emitter queueing, and forwarding to `GlobalRegistry.PlayerCriticalAudio`.
- Confirmed `PlayerCriticalProceduralAudioRenderer` owns the high-speed snapshot path, derives mass from `LostKineticEnergy` and `ImpactSpeed`, recalculates `0.5 * mass * speedSq`, clamps to `KineticImpactMaximumSafeEnergyJoules`, and maps the result into thud, distortion, low-pass, echo tap, and telemetry.
- Confirmed low-tier/MX350 fallback exits to `lowTierKineticImpactClip` through the existing pooled `PlayAtPoint` API, not `AudioSource.PlayClipAtPoint`.
- Confirmed procedural path uses 150 Hz -> 40 Hz thud over 0.2 s, hard clipping at extreme energy, 800 Hz underwater low-pass, `NativeQueue<SonarEchoTap>` echo routing, and `PeakImpactEnergyJoules` black-box telemetry.
- Added/kept Burst compile surface `KineticImpactSineOscillatorJob` and replaced exact `math.exp` low-pass coefficient with `ApproximateExpNegPositive` reciprocal approximation.

Cinematic cheats used:
- One pitch-descending sine thud stands in for structural deformation and collision acoustics.
- Existing metallic clang/granular bed supplies perceived material bite instead of a material-accurate solver.
- Native sonar echo tap is reused for impact reflection instead of a new acoustic ray/portal simulation.
- Underwater muffling is one scalar waterline comparison plus 800 Hz low-pass, not volume tracing.
- Low tier uses one baked clip; Middle uses thud+clang; High uses thud+echo; Ultra keeps bounded stronger distortion/echo with the same contract.

Exact microseconds saved:
- Singleton/audio-source avoidance: estimated 8-20 us per accepted impact admission and 0 B/frame hot path.
- Bounded signal scan: 32 high-speed signals, target under 20 us worst-case scan.
- Low-tier baked fallback: avoids the full 0.2 s oscillator/LPF window on i3/MX350; cost is one pooled source setup.
- Echo reuse: one `NativeQueue<SonarEchoTap>` enqueue instead of a new managed queue/path, target under 10 us admission.
- Energy clamp/math guard: <1 us scalar ALU, prevents unsafe gain and telemetry corruption.
- Omega polish: removes one exact exponential from Burst oscillator setup; micro-level CPU saved, no allocation change.

Verification:
- `rg -n -F 'PlayClipAtPoint' Assets/_Project/Scripts` returned no matches.
- Owned-file scans found no managed `foreach`, no `math.exp` in synthesis after polish, no unconditional `math.normalize`; `.ToString()` hits are editor/cold bootstrap reporting only.
- `git diff --check` passed except CRLF normalization warnings.
- Unity MCP `validate_script` failed with `Unity session not available; reason no_unity_session`.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -p:UseSharedCompilation=false -m:1` failed in 32 s with 132 unrelated missing namespace/type errors, including `Hecton8.Environment.Fluids`, `Hecton8.Audio.Propagation`, `Hecton8.Audio.Virtualization`, `Hecton8.Physics.CCD`, `MacroSwarm`, and `SoundEmissionSignal`.

Integrator note:
- Do not treat this as verified green. Unity compile remains PENDING VERIFICATION until the editor session and global asmdef dependency wall are fixed.

## 2026-05-14 - DSP_ACOUSTIC_LEAD - Loop 6 Material/Mass Upgrade
Status: PENDING VERIFICATION

What was wrong:
- The renderer still inferred high-speed material from source kind, which made player/vehicle/leviathan impacts too generic.
- Player and vehicle high-speed packets were not writing authored material IDs, effective mass, or material hash even though the signal contract supports them.
- Mass reconstruction from lost energy was acceptable fallback behavior, but not the best path for AAA scaling when actual rigidbody mass exists.

What was done:
- `HectonPlayerMotor` and `VehicleMotor` now resolve target impact material through `IPhysicsImpactMaterialProvider`, set source material as metal, write `EffectiveMass`, and compose `MaterialHash`.
- `PlayerCriticalProceduralAudioRenderer` now prefers `signal.EffectiveMass` for `0.5 * mass * speedSq`, keeps lost-energy fallback for legacy packets, and routes material IDs into clang, echo, hollow resonance, pitch, and duplicate hashing.
- `AdvancedAcousticsSmokeTester` now asserts effective-mass and high-speed material consumption.
- Verified `FaunaBrain` already writes equivalent high-speed material/mass fields in HEAD.

Cinematic cheats used:
- Material is a compact byte family, not a surface-accurate contact solver.
- Organic/metal/glass switches scale existing clang/echo/pitch multipliers instead of adding new PCM layers.
- Low tier still exits to one baked clip; material work only improves high-speed packet admission and DSP scalar mapping.

Exact microseconds saved:
- Avoided a new material resolver service: 0 extra persistent allocations and no new queue.
- Reused existing `IPhysicsImpactMaterialProvider`: one event-only lookup per emitted high-speed impact.
- Renderer material blend: byte switches and scalar multipliers, estimated <2 us per accepted impact.
- Kept signal size at 96 bytes: no lane memory growth.

Verification:
- `git diff --check` passed except CRLF normalization warnings.
- `rg PlayClipAtPoint` returned no matches.
- Owned kinetic scans found no new `foreach`, `math.exp`, `math.normalize`, `.ToString()`, `string.Format`, or interpolation hits.
- Unity MCP validation failed at transport level: `http://127.0.0.1:8088/mcp`.
- First `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -p:UseSharedCompilation=false -m:1` failed with `CS2001` because `Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs` was deleted while still referenced by the project file.
- After another process restored that UI file, the rerun reached the existing 132-error global namespace/asmdef wall: examples include `Hecton8.Environment.Fluids`, `Hecton8.Physics.CCD`, `Hecton8.Audio.Propagation`, `Hecton8.Audio.Virtualization`, `MacroSwarm`, and `AcousticAup`.

## 2026-05-14 - DSP_ACOUSTIC_LEAD - Loop 7 Echo Tap Queue Churn Re-Audit
Status: PENDING VERIFICATION

What was wrong:
- Kinetic impact echoes generated one tap but still cleared, enqueued, and drained through the shared sonar upload queue.
- That path added unnecessary native queue traffic and could discard unrelated pending active-sonar upload work if both paths touched the queue in the same frame.
- `CURRENT_BATCH.md` has rotated and no longer contains `KINETIC_IMPACT_ACOUSTICS`; persistent task files remain the active memory for this prompt.

What was done:
- `TryPublishKineticImpactEchoTap` now writes the generated `SonarEchoTap` directly into `inactiveTapBuffer[0]` and publishes `tapCount = 1`.
- Active sonar batching remains unchanged on `NativeQueue<SonarEchoTap>`.
- `AdvancedAcousticsSmokeTester` now asserts the direct kinetic tap write.

Cinematic cheats used:
- Collision echo remains a single authored procedural tap into the existing binaural/portal echo lane.
- No new acoustic ray solver, new queue, or material-specific PCM layer was added.
- Low tier remains baked clip only; high/ultra spend the saved work on the existing material-colored thud/clang/echo stack.

Exact microseconds saved:
- Saves up to 32 guarded queue dequeue attempts from `ClearSonarEchoTapUploadQueue`.
- Saves one `NativeQueue.Enqueue` plus one `TryDequeue` per accepted high-tier kinetic echo.
- Removes one shared-queue contention/stomp surface for collision echo admission.
- Runtime allocation delta: 0 B/frame.

Verification:
- `git diff --check -- Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs` passed except CRLF normalization warnings.
- `rg -n "PlayClipAtPoint" Assets/_Project/Scripts` returned no matches.
- Targeted owned-file scan found only editor/cold `AdvancedAcousticsSmokeTester` assertion text for `math.exp` and `builder.ToString()`.
- Source readback after a shared-workspace overwrite confirms `inactiveTapBuffer[0] = tap` exists in both renderer and smoke tester.
- Historical note: one `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -p:UseSharedCompilation=false -m:1` pass succeeded before the overwrite was detected, but that is not final proof.
- Final `Hecton8.Core.csproj` rerun after reapplying the patch failed with `CS2012` because `Unity.RenderPipelines.Universal.Runtime.dll` was locked by another process.
- Unity MCP resources are empty/unavailable in this context.
- `Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj` builds timed out; spawned dotnet build-server/processes were shut down or stopped. Unity compile remains PENDING VERIFICATION.

## 2026-05-15 - DSP_ACOUSTIC_LEAD - Loop 8 Duplicate Impact Admission H-Phi Pass
Status: PENDING VERIFICATION

What was wrong:
- Same-frame high-speed collision dedupe remembered only the immediately previous packet, so interleaved A/B/A producer output could play the same impact twice.
- Accepted impacts computed the FNV signature twice: once for duplicate check and once when recording the last packet.
- Invalid impact packets were hashed before finite rejection.

What was done:
- `PlayerCriticalProceduralAudioRenderer` now owns an 8-entry fixed `HighSpeedImpactDuplicateEntry` ring plus the existing last-packet fast path.
- Each duplicate-ring entry has a valid byte so zeroed cold entries cannot suppress a legitimate frame 0 or zero-signature packet.
- `TryHandleHighSpeedImpactSignal` finite-checks before hashing, computes `ResolveHighSpeedImpactSignature(in signal)` once, and records that precomputed signature on both low-tier and procedural accepted paths.
- `AdvancedAcousticsSmokeTester` now asserts the fixed ring, precomputed-signature record call, and valid-entry guard.

Cinematic cheats used:
- Duplicate handling is packet-signature based, not a spatial contact solver.
- The ring is fixed at 8 entries because the scan cap is 32 and the purpose is repeated packet suppression, not global collision history.
- Low tier remains one baked clip; high/ultra get fewer false duplicate thuds/echo taps inside the same DSP lane.

Exact microseconds saved:
- Saves one 10-field FNV mix on every accepted impact.
- Saves all signature hashing for invalid speed/energy packets.
- Adds at most 8 struct comparisons per scanned signal, estimated under 2 us worst-case for the 32-signal cap on i3/MX350.
- Prevents full duplicate thud/clang/echo render windows when a producer repeats A/B/A in one frame; runtime allocation delta remains 0 B/frame.

Verification:
- `git diff --check -- Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs` passed except CRLF normalization warnings.
- `rg -n -F "KineticImpactDuplicateHistoryCapacity = 8"` found renderer and smoke tester anchors.
- `rg -n -F "RecordHighSpeedImpactSignal(signal.Frame, signalSignature)"` found low-tier/procedural record calls and the smoke anchor.
- `rg -n -F "entry.Valid != 0"` found renderer and smoke tester anchors.
- `rg -n -F "IsDuplicateHighSpeedImpactSignal(in signal)"` returned no matches.
- `rg -n -F "PlayClipAtPoint" Assets/_Project/Scripts/Audio Assets/_Project/Scripts/Gameplay Assets/_Project/Scripts/Core` returned no matches.
- Targeted owned-file scan found only pre-existing cold/editor `Debug.Log`/`ToString()`/assertion text, not new hot-path allocation.
- Dotnet build/rebuild was not run by explicit user order. Unity compile remains PENDING VERIFICATION until Editor console/MCP validation is available.

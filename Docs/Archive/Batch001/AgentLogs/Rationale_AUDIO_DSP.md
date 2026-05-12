# Rationale_AUDIO_DSP

Status: PENDING VERIFICATION

## Decision 0 - Prompt Source

Problem: The active batch source moved during the session; the first disk-backed source was `Docs/Tasks/CURRENT_BATCH.txt`, and the current authoritative source is `Docs/Tasks/CURRENT_BATCH.md`.
Solution: Used PowerShell line extraction for `<AGENT_PROMPT id="AUDIO_DSP">` from `Docs/Tasks/CURRENT_BATCH.md` and updated the persisted status source path.
Rejected Alternatives: Chat-only prompt parsing was rejected because the batch protocol requires disk-backed extraction. Reading adjacent prompt blocks for architecture cues was rejected because strict parsing says neighboring tasks must not influence this domain.
Scalability potential: Low uses fixed cheap DSP fakes and hard buffer masks; Middle/High/Ultra can spend saved CPU on richer reverb and binaural details.
Hardware Impact: Estimated gain on i3/MX350 is not measured yet; structural Po2 enforcement prevents DSP-thread crash paths rather than adding frame cost.

## Decision 1 - Mandate Scope

Problem: Audio DSP work crosses queueing, native memory, occlusion, psychoacoustics, and telemetry; loading every mandate would pollute task scope.
Solution: Loaded seven relevant mandates: DSP SPSC, acoustic occlusion, HRTF, zero-GC, native memory/jobs, post-mortem telemetry, and cinematic fake-first.
Rejected Alternatives: Loading all registry files was rejected as unnecessary context and likely to introduce cross-domain assumptions.
Scalability potential: Low = Sabine/reverb zone, AUP muffle zones, dominant-axis pan; Middle = more sources and filtered occlusion; High = async raycast for loudest sources and lightweight convolution/FDN; Ultra = richer IR/convolution if profiler accepts it.
Hardware Impact: Expected low-end benefit is bounded audio CPU and zero GC; exact microsecond deltas require Unity profiler/GCMonitor and remain PENDING VERIFICATION.

## Decision 2 - Po2 Ring Buffer Enforcement

Problem: DSP ring buffers use `index & mask` wrapping; a serialized or computed non-power-of-two capacity would alias writes and can corrupt the audio thread.
Solution: Added `AudioBufferCapacity = 65536` with a compile-time divide-by-zero guard, centralized runtime capacity resolution through `ResolvePowerOfTwoCapacity`, and short-circuited ring reads/writes when capacity/mask invariants are invalid.
Rejected Alternatives: Silent modulo fallback was rejected because it hides authoring errors and adds hot-path cost. Trusting inspector ranges was rejected because serialized values can be edited outside the inspector.
Scalability potential: Low/Middle/High/Ultra all share the same branch-free bitmask path after cold initialization; higher tiers spend saved cycles on reverb and psychoacoustic detail rather than buffer safety checks.
Hardware Impact: Runtime gain is structural rather than measured; avoiding modulo on MX350-class hardware keeps the DSP path branch-light. Exact microseconds remain PENDING VERIFICATION until Unity profiler capture.

## Decision 3 - Loop 1 Compile Boundary

Problem: The full `dotnet build Assembly-CSharp.csproj` dependency graph fails in `VoxelDeltaProcessor.cs` with missing `UniformFlag` and `DebrisSpawnSignal`, outside the audio DSP domain.
Solution: Verified the audio assembly itself with `dotnet build Assembly-CSharp.csproj --no-dependencies -v:minimal`, which succeeded with 0 warnings and 0 errors; recorded the full graph failure as a dependency blocker.
Rejected Alternatives: Editing voxel code was rejected because it violates AUDIO_DSP domain boundaries and would risk another agent's ownership. Ignoring compile output was rejected because the SOP requires explicit verification evidence.
Scalability potential: Audio DSP changes remain isolated and compile-clean; voxel blocker does not alter Low/Middle/High/Ultra acoustic scalability paths.
Hardware Impact: No measured audio runtime delta from compile verification. Exact microseconds remain PENDING VERIFICATION; dependency compile wall is unrelated to audio CPU cost.

## Decision 4 - DSP Approximation Pass

Problem: Thruster, binaural, oscillator, and saturation paths can silently drift back into exact math when multiple agents edit the audio renderer.
Solution: Verified the hot DSP path keeps coefficients at block scope, uses dominant-axis basis selection, preserves compile-time Po2 ring guards, routes oscillators through `FastSine01`, and clips with `FastSoftClip`.
Rejected Alternatives: Per-sample coefficient recompute, normalized source vectors, `math.sin`, and `math.tanh` were rejected because they spend CPU on precision the player cannot inspect in a noisy underwater mix.
Scalability potential: Low uses the same cheap approximations with fewer active emitters; Middle/High/Ultra can layer more emitters, reverb wetness, and convolution detail without changing the hot primitive cost.
Hardware Impact: Expected i3/MX350 gain is reduced scalar/transcendental pressure in sample loops; exact microseconds remain PENDING VERIFICATION until Unity profiler capture.

## Decision 5 - Runtime Audio Dispatch And Survival Cues

Problem: Gameplay audio events, hull stress, far-source rolloff, imported clip formats, and panic heartbeat can all create allocation or CPU spikes if handled through generic Unity defaults.
Solution: Kept event dispatch in a capped `NativeQueue<AudioEvent>` drained through authored pooled sources, verified procedural hull creaks and heartbeat are parameter-driven, and verified importer policy keeps ambient compressed in memory while SFX are ADPCM mono.
Rejected Alternatives: `PlayClipAtPoint`, random creak one-shots, full-band far sources, stereo 3D SFX, and static heartbeat loops were rejected because they waste voice count, memory, or CPU without buying stronger underwater read.
Scalability potential: Low uses the same capped queue and cheaper source tiers; Middle/High/Ultra can spend headroom on more active sources, richer hull layers, and higher reverb tiers without changing the dispatch contract.
Hardware Impact: Expected i3/MX350 gain is bounded voice count, lower SFX decode/memory pressure, and no per-event managed allocation. Exact microseconds remain PENDING VERIFICATION until Unity profiler capture.

## Decision 6 - Log And Compile Hygiene

Problem: Editor audio validation still used interpolated logs, while final DSP verification required strict struct size, no managed callback fallback, cast rounding, and Burst fast-mode evidence.
Solution: Replaced scoped audio editor interpolated logs with fixed literal messages carrying hex error codes, verified `AudioEvent` has explicit 32-byte layout, verified scoped runtime scans for `OnAudioFilterRead` and `math.round` are clean, and verified the DSP job Burst attribute.
Rejected Alternatives: Keeping path/count interpolation was rejected because it violates literal logging. Adding more runtime logging was rejected because audio validation evidence belongs in deterministic files and compile output, not hot paths.
Scalability potential: Low/Middle/High/Ultra tiers keep the same fixed queue contract and fast DSP job setup; editor validation remains deterministic and does not add runtime overhead.
Hardware Impact: Runtime impact is 0 us/frame for log changes. Burst fast-mode and cast rounding protect low-end scalar cost; exact runtime savings remain PENDING VERIFICATION.

## OMEGA POLISH CHANGES

Problem: Final polish required a top-down anti-bloat audit, scalability confirmation, domain-boundary check, final diff evidence, and a mandated `dotnet build Hecton8.Core.csproj`.
Solution: Read the OMEGA polish tag after all 20 core tasks were checked, re-read the domain map, scanned touched audio/editor files for `foreach`, `$"..."`, `string.Format`, `.ToString()`, `math.sqrt`, `math.normalize`, `.normalized`, `Mathf.Sqrt`, and `Vector3.Distance`, and ran the required Core build.
Rejected Alternatives: Faking `VERIFIED MASTER GRADE` was rejected because `Hecton8.Core.csproj` fails in non-audio files. Editing `GlobalSignals.cs`, `FaunaBrain.cs`, or `ConstructionManager.cs` was rejected because those are outside the AUDIO_DSP domain.
Scalability potential: Low uses Unity profile/Sabine-derived decay, AUP muffle zones, dominant-axis binaural, 32-source queue cap, ADPCM mono SFX, and Po2 masks. Middle keeps the same queue and adds richer spatial filtering. High/Ultra allow async occlusion and native convolution cave reverb while retaining the same branch-light DSP primitives.
Hardware Impact: Exact profiler microseconds saved remain PENDING VERIFICATION because no Unity Profiler capture was run. Static savings: no modulo fallback in ring wrapping, no per-sample thruster coefficient recompute, no `math.sin`/`math.tanh` in the checked DSP helpers, no managed `OnAudioFilterRead` fallback.
Honest Calculations Replaced: Po2 `& mask` guarded the ring instead of modulo fallback; parabolic `FastSine01` stands in for transcendental sine; `FastSoftClip` stands in for `tanh`; dominant-axis basis stands in for normalized binaural direction; fixed editor log codes stand in for interpolated log strings.
Final Git Diff: scoped diff contains `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs`, `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`, and `Assets/_Project/Scripts/Editor/HectonAudioPostprocessor.cs`; current stat is 3 files changed, 341 insertions, 38 deletions. Note: `PlayerCriticalProceduralAudioRenderer.cs` already contained other uncommitted audio changes before this pass and they were preserved.
Build Health: `Assembly-CSharp.csproj --no-dependencies`, `Assembly-CSharp-Editor-firstpass.csproj --no-dependencies`, and `Assembly-CSharp-Editor.csproj --no-dependencies` pass with 0 warnings/errors. `Hecton8.Core.csproj` fails outside audio with missing signal/fauna types and an origin-shift interface mismatch.

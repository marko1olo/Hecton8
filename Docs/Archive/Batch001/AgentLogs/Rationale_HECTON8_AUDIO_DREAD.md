# HECTON8_AUDIO_DREAD Rationale

Status: PENDING VERIFICATION

## Initial Authority Decision

Problem: The prompt requests physical-sounding granular/acoustic behavior, but AGENTS and mandates reject expensive physical simulation and managed audio callbacks.
Solution: Keep runtime work in first-party procedural audio code, use deterministic presentation fakes, NativeArray/Burst jobs, power-of-two rings, and block-level parameter snapshots.
Rejected Alternatives: Runtime AudioClip slicing, `AudioSource.PlayOneShot`, underwater full HRTF convolution, and per-source managed filters were rejected for GC, CPU, and architecture violations.
Scalability potential: Low uses cheap mono/near-stereo filtering and sparse occlusion; Middle adds selected procedural cues; High casts capped loudest-source occlusion rays; Ultra can spend saved cycles on denser acoustic presentation after profiler proof.
Hardware Impact: MX350/i3 path avoids per-frame managed allocations and caps audio work to block/job math. Expected gain is hitch avoidance rather than a claimed measured millisecond delta.

## Task Batch Decision

Problem: The user supplied a chat prompt, but `CURRENT_BATCH.md` and `<AGENT_PROMPT id="HECTON8_AUDIO_DREAD">` are absent.
Solution: Treat the chat-delivered master prompt as the authoritative assignment and record the missing batch file as a verification boundary.
Rejected Alternatives: Inventing a batch file or scanning unrelated prompts would violate strict parsing and contaminate scope.
Scalability potential: Scope remains audio-only; cross-domain work only through existing interfaces/events.
Hardware Impact: Prevents unrelated broad edits in a dirty 20-agent workspace.

## Iteration 1 Decisions

Problem: Hull creak needed a true granular source window, but runtime `.wav` slicing would allocate, bind managed `AudioClip` state, and make burst/audio-thread ownership ambiguous.
Solution: Expand the existing metallic grain bank to a fixed 131072-sample power-of-two source window and select 50 ms grains from the first two seconds using hash/LCG seeds, pitch jitter, and masked indices.
Rejected Alternatives: `AudioClip.GetData`, `UnityEngine.Random.Range`, per-grain managed objects, or clip loop swapping were rejected as GC and determinism violations.
Scalability potential: Low keeps sparse grain triggers; Middle increases overlap by stress scalar; High/Ultra can raise event density after profiler proof without changing source ownership.
Hardware Impact: i3/MX350 path spends a few integer ops per grain trigger instead of managed sample access; expected benefit is hitch prevention, not a measured ms claim.

Problem: Panic audio artifact needed to follow `Player.Stress > 0.8` without adding a master bus plugin or managed random stream.
Solution: Reuse the existing heartbeat stress scalar and add deterministic held granular jitter/noise before the limiter using `FastSoftClip`.
Rejected Alternatives: AudioMixer DSP plugin, `math.tanh`, and global random jitter were rejected for CPU/GC/determinism reasons.
Scalability potential: Low uses amplitude jitter only at tiny gain; High/Ultra can increase density or add separate HUD synchronization after the HUD contract exposes a stable scalar.
Hardware Impact: No allocations; expected cost is sub-2 us per 512-frame block on low-end silicon, pending profiler verification.

Problem: Fauna-swarm Doppler needs 500-lane batching and the formula must not run as a managed per-source `AudioSource.pitch` loop.
Solution: Add `DopplerShiftBatchJob` as a Burst `IJobParallelFor` with a precomputed reciprocal of speed of sound and clamped relative velocity.
Rejected Alternatives: Manual MonoBehaviour loop and per-source pitch calls were rejected for scale and cache behavior.
Scalability potential: Low schedules fewer lanes or larger batches; Middle/High run the full 500; Ultra can add material-dependent propagation after data contract stabilization.
Hardware Impact: 500 scalar frequency shifts become contiguous Burst math; expected <20 us, PENDING VERIFICATION.

Problem: Reverb decay was computed from Sabine math but lacked the requested 1D precomputed decay lookup based on depth/module volume.
Solution: Add a cold `float[64]` Sabine RT60 LUT in `SpatialAudioManager`, sample it by world depth and room volume, then blend with the existing Sabine equation by cave interior factor.
Rejected Alternatives: One global reverb profile and per-frame dynamic table generation were rejected for repetition and hot-path allocation.
Scalability potential: Low can use mixer-only profile; Middle uses LUT/equation blend; High/Ultra can enable native Sabine/convolution tails.
Hardware Impact: One static array and two indexed reads replace repeated profile authoring; per-update cost is negligible, PENDING VERIFICATION.

## Iteration 2 Decisions

Problem: Several requested systems already exist behind shared contracts, but their exact scheduling differs from the prompt.
Solution: Document source-backed capabilities and mark exact mismatches as blocked rather than rewriting dispatcher/occlusion contracts in a dirty 20-agent workspace.
Rejected Alternatives: Renaming `LateFrameTick` drain into Frost/Fast or replacing `AcousticOcclusionUtility` scheduling would create cross-domain risk.
Scalability potential: Low keeps cached occlusion; High/Ultra can spend one capped probe per dominant emitter once the shared occlusion owner exposes that budget.
Hardware Impact: Avoids destabilizing global dispatcher and shared occlusion cache; no fake microsecond savings claimed.

## Iteration 3 Decisions

Problem: Some requested audio cues depend on content-domain signals that are absent or not exposed to the audio renderer.
Solution: Implement the pressure scrubber pitch coupling locally and mark biolum coral, water ingress procedural mix, seismic pre-roll, and VWS ducking as blocked by missing contracts.
Rejected Alternatives: Directly scraping biome/module/VWS internals from audio was rejected as architectural sabotage.
Scalability potential: Low uses existing authored clips where present; Middle adds procedural overlays when events exist; High/Ultra can layer granular beds after contracts are formalized.
Hardware Impact: Pressure scrubber pitch adds only existing sine oscillator frequency scaling; no added allocations.

## Iteration 4 Decisions

Problem: Tool, leviathan, breathing, and HUD sync features span tools/fauna/player/HUD ownership.
Solution: Keep audio changes inside `PlayerCriticalProceduralAudioRenderer` and report exact source-backed coverage versus missing contracts.
Rejected Alternatives: Editing tool heat, fauna attenuation, or HUD glitch systems without stable audio interfaces was rejected.
Scalability potential: Low keeps existing cheap procedural cues; High/Ultra can add richer cross-domain modulation once signal surfaces are explicit.
Hardware Impact: No additional cross-domain polling was introduced.

## Iteration 5 Decisions

Problem: Airlock hiss, asset stripping, and meta generation are editor/content-pipeline tasks with partial existing infrastructure.
Solution: Preserve existing editor import/stripping behavior, avoid creating unneeded Unity assets, and mark Source-folder stripping/hiss synthesis as contract-blocked.
Rejected Alternatives: Creating new assets or deleting plugin folders without build-pipeline authority was rejected.
Scalability potential: Low build strips obvious demo/example/docs payload; High/Ultra packaging can add stricter plugin source pruning after ownership sign-off.
Hardware Impact: Import compression affects RAM footprint; no runtime frame claim.

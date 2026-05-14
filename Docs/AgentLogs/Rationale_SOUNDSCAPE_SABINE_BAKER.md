# Rationale - SOUNDSCAPE_SABINE_BAKER

## Session Initialization

Problem: Reverb coefficient generation was assigned as an offline DSP precompute task. The i3/MX350 target cannot pay real-time FDN/Sabine coefficient cost for many acoustic zones.

Solution: Bake deterministic float32 LUT data into `Data/Precomputed/Reverb_LUT.bin` and document a direct C# read-map. Use numpy offline where allocation is irrelevant, then keep runtime consumption as fixed-size binary reads.

Rejected Alternatives: Runtime FDN coefficient generation, AudioMixer curve evaluation, and per-zone `AnimationCurve` evaluation were rejected because mandates require precomputed filter/LUT data and no runtime division/sqrt in DSP callbacks.

Scalability potential: Low uses LUT lookup and cheap Schroeder/zone reverb; Middle uses the same LUT with denser zone updates; High adds hybrid early reflections; Ultra spends saved CPU on richer convolution tails and binaural detail.

Hardware Impact: Expected gain on i3/MX350 is removal of per-zone Sabine/division work from runtime DSP paths. Exact microseconds are source-estimated until Unity/Profiler proof exists.

Mandates followed: AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC, AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation, AUDIO_Hrtf_Binaural_Spatialization, OPT_Zero_GC_Policy_AllocFree_Mandate, OPT_Performance_Budgets_FrameTime_VRAM_Limits, MATH_Deterministic_RNG_SlotMachine.

Traceability: Batch prompt tag casing is `SOUNDSCAPE_Sabine_BAKER`; override ID is `SOUNDSCAPE_SABINE_BAKER`. Files use the override ID.

## Decision 1 - Dedicated Reverb_LUT.bin Instead of Existing sabine_reverb_rt60.bin

Problem: Existing `sabine_reverb_rt60.bin` is `40 x 25`, headerless, and capped at `10,000m3`; the batch requires `256 x 256`, a `100,000m3` maximum, and a header-aware byte-size check.

Solution: Create `Tools/AcousticValidator.py` to bake a separate `Data/Precomputed/Reverb_LUT.bin` with a fixed 256-byte header and a `256 x 256` little-endian float32 payload.

Rejected Alternatives: Mutating `Tools/MathLUTGenerator.py` would change an existing contract used by other agents. Runtime reader invention was rejected because the task asked for binary baking and a spec for the C# audio agent.

Scalability potential: Low/Middle perform one nearest lookup. High/Ultra can bilerp outside the DSP sample loop and spend saved CPU on richer early-reflection or convolution tails.

Hardware Impact: Expected low-end gain is removing per-zone RT60 math from runtime. Offline bake measured 17,950.40us on this machine; runtime microsecond gain remains source-estimated until profiler proof.

## Decision 2 - Log-Spaced Volume Axis

Problem: A linear `10m3..100000m3` volume axis wastes precision on giant spaces and undersamples lockers/corridors where audible transitions are dense.

Solution: Use log-spaced volume samples and linear absorption samples. Document exact index mapping in `Docs/Design/Acoustic_Binary_Specs.md`.

Rejected Alternatives: Linear volume spacing was rejected because it preserves arithmetic simplicity while degrading perceptual control in the small-room range. Arbitrary authored presets were rejected because the prompt requires a full matrix.

Scalability potential: Low uses nearest index. High/Ultra can bilerp the log axis during cold parameter updates.

Hardware Impact: Same byte count as linear spacing. Better low-end quality per byte because near-field zones get useful resolution without runtime math beyond `log10` at cold update time.

## Decision 3 - Seawater-Biased Material Damping Curves

Problem: The batch requires Steel, Rock, Coral, and Water high-frequency damping. Underwater audio cannot use air-only material brightness without sounding wrong and wasting high-frequency DSP detail.

Solution: Store four `float32[4]` damping curves in the header for `500, 2000, 8000, 16000Hz`. Formula combines material base absorption, high-frequency slope, and a seawater loss term.

Rejected Alternatives: Single scalar damping values were rejected because the task says filter curve generation. Full impulse responses were rejected because MX350 mandates cheap perceptual fakes and no real-time convolution on low tier.

Scalability potential: Low consumes one material row. Middle/High can interpolate bands. Ultra may layer convolution tails while preserving this RT60 authority.

Hardware Impact: Header cost is 64 bytes for all material curves. Runtime avoids calculating exponentials or per-material curves in DSP paths.

## Decision 4 - Recursive Validator

Problem: A binary LUT can pass size checks while containing wrong axis math or corrupt edge values.

Solution: Validator reads the binary back, verifies header constants, CRC32, finite range, exact byte size `262400`, and recursively validates five edge cases including Mega-Cave and Giant Void.

Rejected Alternatives: Hash-only validation was rejected because it cannot prove the Sabine formula. Manual-only spreadsheet validation was rejected because it is not executable evidence.

Scalability potential: Validator stays offline. Runtime reader only needs fixed constants and optional cold-load CRC checks.

Hardware Impact: No runtime cost. Prevents shipping a corrupt LUT that would cause overlong tails or dead reverb on low-end hardware.

## Decision 5 - Compile and VCS Boundary

Problem: The batch asks for compile verification and push, but this workspace has many unrelated concurrent changes and the shell does not expose `dotnet`.

Solution: Run Python validation and `py_compile` for the authored script. Attempt VCS staging only for this agent's files. Do not stage unrelated agent work.

Rejected Alternatives: Blind full-worktree commit was rejected because it would capture other agents' changes. Claiming Unity compile success was rejected because no Unity Console/PlayMode/compiler log was produced.

Scalability potential: The binary/spec are cold data; no runtime scalability gate is altered by VCS handling.

Hardware Impact: None. Toolchain limitation only affects verification evidence.

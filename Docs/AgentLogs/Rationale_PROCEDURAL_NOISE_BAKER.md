# Rationale_PROCEDURAL_NOISE_BAKER

## Decision 001 - Batch Source Alias

Problem: User requested `CURRENT_BATCH_OSHINO.md`, but repository search found no such file. The active task prompt exists in `Docs/Tasks/CURRENT_BATCH.md`.
Solution: Extracted only `<AGENT_PROMPT id="PROCEDURAL_NOISE_BAKER">` from the active batch with CLI regex and recorded the missing alias.
Rejected Alternatives: Reading archive prompts, borrowing adjacent tasks, or blocking despite the exact prompt being present in the active batch.
Scalability potential: No runtime impact. Preserves deterministic task authority for cheap and high-end targets.
Hardware Impact: 0 us runtime. Intake-only file scan.

## Decision 002 - Offline Texture Bake Instead Of Runtime Noise

Problem: MX350 should not spend runtime ALU generating high-quality blue noise for dither/fog jitter.
Solution: Bake deterministic 2D texture data offline. Runtime consumers can sample one texture or use existing shader IGN formula.
Rejected Alternatives: Per-pixel runtime high-quality noise, extra shader loops, or importing third-party noise assets.
Scalability potential: Low uses baked lookup and one sample. Middle uses same data with more passes. High can combine baked blue noise with temporal rotation. Ultra can spend saved ALU on richer post detail.
Hardware Impact: Estimated low-end gain: avoids runtime blue-noise synthesis cost entirely; exact microseconds require profiler capture.

## Decision 003 - Deterministic Generator

Problem: Noise bake must be reproducible and must not depend on wall-clock, Unity random, or external asset state.
Solution: Use an explicit integer seed and deterministic Python math. IGN uses the exact formula from the prompt.
Rejected Alternatives: `System.Random`, Unity random, or uncontrolled random package defaults.
Scalability potential: Same texture content across devices; quality tiers choose sample strategy, not different authority data.
Hardware Impact: 0 us runtime. Offline CPU bake cost only.

## Decision 004 - Flow Lookup Is Presentation Data

Problem: Prompt asks for a low-tier Abyssal Flow Field slice, but live flow simulation is owned by runtime world/flow systems.
Solution: Generate a deterministic 128x128 2D RGBA lookup as an offline presentation fake, not a runtime authority replacement. RG encodes normalized XY flow, B encodes magnitude/energy, A encodes turbulence/detail.
Rejected Alternatives: Editing live flow compute shaders or adding CPU runtime flow orchestration.
Scalability potential: Low samples baked flow for cheap visual drift; Middle/High can blend it with live flow; Ultra can ignore it or use it as fallback.
Hardware Impact: Estimated low-end gain: one texture lookup replaces per-pixel procedural current math in low-tier visual effects; profiler proof absent.

## Decision 005 - Persisted Evidence Only

Problem: A first bake attempt produced passing transient output, then all newly created untracked files disappeared before the next readback.
Solution: Recreated the owned files and invalidated that transient pass as evidence. Only artifacts present on disk at final readback will be reported.
Rejected Alternatives: Trusting console output for files that no longer existed.
Scalability potential: No runtime effect; protects integration from fake reports.
Hardware Impact: 0 us runtime.

## Decision 006 - Void-And-Cluster Thresholds

Problem: The first persisted bake needed an objective way to reject low-frequency noise without pretending mathematical zero after floating-point FFT.
Solution: Use toroidal void-and-cluster relaxation and a verifier gate: DC power <= 0.0001 after mean removal, low_mean_to_mid_mean <= 0.12, low_peak_to_mid_mean <= 0.5, max seam ratio <= 1.35, and IGN max quantized delta == 0.
Rejected Alternatives: Human eyeballing, exact-zero FFT comparison below floating-point noise, or accepting arbitrary noise because it is offline.
Scalability potential: Low uses one baked threshold sample. Middle/High can reuse the same texture with temporal R2 offsets. Ultra can combine the saved ALU budget with richer post effects.
Hardware Impact: Estimated gain on i3/MX350: runtime high-quality noise synthesis avoided; exact microseconds remain PENDING PROFILER.

## Decision 007 - Packed RGBA Instead Of Separate Assets

Problem: Four small noise roles are required, but separate textures increase asset count, binding churn, and importer surface.
Solution: Pack BlueNoise, IGN, jitter, and dither into one 256x256 RGBA PNG.
Rejected Alternatives: Four single-channel PNGs or relying on third-party noise textures under vendor folders.
Scalability potential: Low samples one packed texture. High and Ultra can use extra channels for film grain, crossfade, and fog jitter without extra texture residency.
Hardware Impact: One 256x256 RGBA8 uncompressed residency footprint is 256 KB before platform compression/import settings; PNG on disk is 262737 bytes.

## Decision 008 - PNG Optimizer Fallback

Problem: The task requested `optipng` or similar via CLI, but `optipng`, `oxipng`, `zopflipng`, and ImageMagick `magick` are not installed on this machine.
Solution: Keep optimizer discovery in the CLI baker and fall back to Pillow `optimize=True, compress_level=9`, with the chosen optimizer written to `NoiseBakeMetrics.json`.
Rejected Alternatives: Installing external packages during the batch, downloading binaries, or falsely claiming optipng ran.
Scalability potential: Same runtime texture data; external optimizer can later reduce disk bytes without changing pixels.
Hardware Impact: 0 us runtime. Disk bytes after fallback: BlueNoise 262737, Flow 26919.

## Decision 009 - Omega Polish Boundary

Problem: Batch protocol requires reading `<POLISH_MANDATE>` only after core task closure, but the active batch file has no such tag.
Solution: Recorded the absent polish tag and ran a scoped anti-bloat pass against owned files: syntax compile, verifier, ASCII scan, deterministic/network/runtime scan, pycache cleanup, and whitespace check.
Rejected Alternatives: Inventing a polish mandate or reading archived batch polish text.
Scalability potential: No runtime effect; keeps the offline asset path narrow and reproducible.
Hardware Impact: 0 us runtime.

## Decision 010 - Determinism Rebuild Review

Problem: Existing PNG metrics prove the artifacts pass thresholds, but they do not prove `GenerateBlueNoise.py` can reproduce the committed bytes.
Solution: Rebuilt the PNGs into `%TEMP%\h8_noise_review_PROCEDURAL_NOISE_BAKER` with `python -B`, compared SHA256 hashes against repository assets, then removed the temp directory after confirming it resolved under `%TEMP%`.
Rejected Alternatives: Treating existing JSON metrics as determinism proof, or leaving review artifacts in the repository.
Scalability potential: Deterministic source bytes mean all hardware tiers consume identical source data; tier differences remain sampling/import/runtime choices.
Hardware Impact: 0 us runtime. Offline-only review pass.

## Decision 011 - Portable Metrics Paths

Problem: Tracked JSON metrics embedded machine-local absolute workspace paths, which makes evidence noisy and non-portable across clone locations.
Solution: Added `artifact_path()` to serialize repository-owned artifacts as POSIX-style repository-relative paths, then regenerated `NoiseBakeMetrics.json`, `NoiseBakeMetrics.verify.json`, `NoiseBakeMetrics.verify2.json`, and `NoiseBakeMetrics.final.json`.
Rejected Alternatives: Leaving local absolute paths in tracked metrics, or hand-editing JSON without fixing the baker that produces it.
Scalability potential: No runtime effect. Low, Middle, High, and Ultra tiers consume the same PNG bytes; this only hardens evidence portability.
Hardware Impact: 0 us runtime. Offline metadata correction only.

## Decision 012 - Volatile Timing Opt-In

Problem: `NoiseBakeMetrics.json` stored `bake_seconds`, so every full bake changed tracked evidence even when texture bytes and verification metrics were identical.
Solution: Removed timing from default metrics and added `--include-timing` for explicit local benchmarking. Regenerated tracked metrics without volatile timing.
Rejected Alternatives: Keeping noisy timing in committed metrics, rounding the timer, or hand-editing JSON while leaving the generator unstable.
Scalability potential: No runtime effect. Stable metrics reduce integration noise for all hardware tiers while preserving optional local benchmark data when needed.
Hardware Impact: 0 us runtime. Offline metadata stability only.

## Decision 013 - Bounded Optimizer Subprocess

Problem: The evidence wording implied `subprocess` was absent, while the baker legitimately invokes optional external PNG optimizers. The call also lacked a timeout.
Solution: Keep `subprocess` only in the offline optimizer path, add `OPTIMIZER_TIMEOUT_SECONDS = 120`, catch `TimeoutExpired`, and record the boundary explicitly.
Rejected Alternatives: Removing optimizer support entirely was rejected because the prompt requested `optipng` or similar when available. Leaving the unbounded call was rejected because a broken external optimizer can stall offline batch execution.
Scalability potential: Runtime data is unchanged. Low, Middle, High, and Ultra tiers still consume the same PNG bytes; only offline bake reliability improves.
Hardware Impact: 0 us runtime. Offline batch worst-case wait is now bounded per optimizer attempt.

## Decision 014 - Independent Spectrum Verifier

Problem: `VerifyBlueNoiseSpectrum.py` imported `verify_assets()` from the generator, so the producer and verifier shared formula and threshold logic. A bad shared implementation could pass itself.
Solution: Rewrote the verifier as a standalone PNG readback tool with its own IGN formula, Fourier spectrum thresholds, seam metrics, repository-relative path serializer, and JSON writer.
Rejected Alternatives: Keeping the generator-backed verifier, or adding a second wrapper that still imported generator internals.
Scalability potential: No runtime effect. Stronger offline proof keeps Low, Middle, High, and Ultra tiers consuming verified source pixels.
Hardware Impact: 0 us runtime. Offline verification only.

## Decision 015 - CLI Input Validation And Seed Consistency

Problem: Bad CLI values could fail as Python tracebacks or be silently clamped; custom seeds also did not affect the void/cluster tie-break jitter because that path used `DEFAULT_SEED`.
Solution: Added argparse validators for uint32 seed values and non-negative swap counts. Passed the validated seed into void/cluster tie-break jitter so non-default seeds are internally consistent.
Rejected Alternatives: Silent `max(0, swaps)` clamping, accepting out-of-range seeds, or leaving custom-seed output partially tied to the default seed.
Scalability potential: No runtime effect. Offline baker behavior is deterministic and explicit for every hardware tier consuming the generated pixels.
Hardware Impact: 0 us runtime. Offline CLI failure behavior only.

## Decision 016 - PNG Optimizer Temp Output And Pillow Mode Hygiene

Problem: `zopflipng` was configured with the same PNG as input and output, and the Pillow save path passed an explicit `mode="RGBA"` even though the array already carries RGBA shape/type.
Solution: Make `zopflipng` write to `*.zopfli.tmp.png` and replace the source only after success; delete temp output on failure/timeout; continue to later optimizers after a failure. Remove the redundant Pillow mode parameter.
Rejected Alternatives: Same-file zopfli input/output, stopping after the first failing external optimizer, or keeping redundant mode arguments in the save path.
Scalability potential: No runtime effect. Low, Middle, High, and Ultra tiers still consume identical texture bytes; offline optimizer reliability improves when external tools are installed.
Hardware Impact: 0 us runtime. Offline file safety only.

## Decision 017 - External Optimizer Candidate Replacement

Problem: `optipng` and `oxipng` mutate files in place, so running them directly against source PNGs risks larger output or source mutation before size checks.
Solution: Run each external optimizer against a temporary candidate copy. Replace the source only if the candidate/output exists and is not larger than the original. Delete candidate/output files after success or failure.
Rejected Alternatives: Trusting in-place optimizer mutation, accepting larger optimized output, or limiting candidate protection to `zopflipng`.
Scalability potential: No runtime effect. All hardware tiers consume the same verified PNG bytes; offline optimizer installation cannot silently bloat source textures.
Hardware Impact: 0 us runtime. Offline file safety only.

## Decision 018 - Flow Field Content Verification

Problem: The flow lookup verifier only proved the PNG shape and byte size. A flat or low-variety 128x128 RGBA texture could pass despite not representing a useful low-tier flow field.
Solution: Add per-channel dynamic range and unique-value thresholds to both the baker verifier and the independent verifier. Regenerate all tracked metrics with flow channel stats.
Rejected Alternatives: Shape-only proof, visual inspection, or relying on the generator formula without PNG readback gates.
Scalability potential: No runtime effect. Low tier gets a proven non-flat lookup; Middle, High, and Ultra tiers keep the same source data for fallback or blending.
Hardware Impact: 0 us runtime. Offline verification only.

## Decision 019 - Verifier Negative Self-Test

Problem: The independent verifier proved that current assets pass, but did not prove its rejection gates fail bad in-memory data.
Solution: Added `--self-test` to run negative verifier cases for flat noise, bad noise shape, flat flow, and bad flow shape without writing files.
Rejected Alternatives: Positive-only verification, or adding tracked bad PNG fixtures that increase repository noise.
Scalability potential: No runtime effect. Stronger offline confidence for all hardware tiers consuming the baked source textures.
Hardware Impact: 0 us runtime. Offline verifier self-test only.

## Decision 020 - Active Batch Rotation

Problem: The current `Docs/Tasks/CURRENT_BATCH.md` no longer contains `<AGENT_PROMPT id="PROCEDURAL_NOISE_BAKER">`, so the anti-amnesia re-extraction cannot be repeated from the rotated batch file.
Solution: Record the drift and keep the persisted `Status_PROCEDURAL_NOISE_BAKER.md`, `Rationale_PROCEDURAL_NOISE_BAKER.md`, and `LOG_PROCEDURAL_NOISE_BAKER.md` as the active task memory for this already-started assignment.
Rejected Alternatives: Reading unrelated current-batch prompts, pulling from deprecated archive prompts, or pretending the XML tag still exists in the active batch.
Scalability potential: No runtime effect. Prevents cross-agent prompt contamination for all hardware tiers.
Hardware Impact: 0 us runtime. Documentation integrity only.

## Decision 021 - Metrics Byte Identity Binding

Problem: The JSON metrics proved thresholds but did not carry the exact PNG SHA256 inside the machine-readable verification payload; hashes existed only in status/log text.
Solution: Add `sha256_file()` to the baker and independent verifier, then embed `sha256` and `bytes` under both `noise` and `flow` sections in every metrics JSON.
Rejected Alternatives: Keeping hashes only in chat/log text, or hand-editing metrics without making both tools produce the same evidence fields.
Scalability potential: No runtime effect. Low, Middle, High, and Ultra tiers consume the same verified source bytes; the evidence now binds those bytes directly to pass/fail status.
Hardware Impact: 0 us runtime. Offline evidence hardening only.

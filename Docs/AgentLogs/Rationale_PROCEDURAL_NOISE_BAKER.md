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

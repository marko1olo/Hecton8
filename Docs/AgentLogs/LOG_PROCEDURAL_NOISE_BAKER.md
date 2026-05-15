# LOG_PROCEDURAL_NOISE_BAKER

## 2026-05-14 - Noise Bake

What was wrong:
- Runtime-quality blue noise and low-tier flow lookup data were absent.
- `CURRENT_BATCH_OSHINO.md` was not present; the exact prompt was found in `Docs/Tasks/CURRENT_BATCH.md`.
- External PNG optimizers (`optipng`, `oxipng`, `zopflipng`, ImageMagick `magick`) are not installed.

What was done:
- Added `Tools/NoiseBaker/GenerateBlueNoise.py`.
- Added `Tools/NoiseBaker/VerifyBlueNoiseSpectrum.py`.
- Baked `Data/Textures/BlueNoise_RGBA.png` as 256x256 RGBA: R=BlueNoise, G=exact IGN, B=Jitter, A=Dither.
- Baked `Data/Textures/AbyssalFlowField_LowTier_RGBA.png` as 128x128 RGBA low-tier presentation flow lookup.
- Wrote metrics to `Data/Textures/NoiseBakeMetrics.json`, `NoiseBakeMetrics.verify.json`, `NoiseBakeMetrics.verify2.json`, and `NoiseBakeMetrics.final.json`.

Cinematic Cheats used:
- Offline blue-noise threshold data replaces runtime high-quality noise generation on MX350.
- Low-tier Abyssal Flow Field PNG is a presentation fake, not physics authority.
- Packed RGBA channels reduce texture/binding surface versus four separate textures.

Exact Microseconds saved:
- Runtime measurement absent; profiler/GCMonitor not available in this pass.
- Static estimate: saved runtime cost is the avoided per-pixel procedural blue-noise synthesis and low-tier visual-flow math. Reported as `PENDING PROFILER`, not a fake number.

Evidence:
- `python -m py_compile Tools\NoiseBaker\GenerateBlueNoise.py Tools\NoiseBaker\VerifyBlueNoiseSpectrum.py` passed.
- `python Tools\NoiseBaker\GenerateBlueNoise.py` exited 0 with `status: NOISE BAKED`.
- `python Tools\NoiseBaker\VerifyBlueNoiseSpectrum.py --metrics Data\Textures\NoiseBakeMetrics.final.json` exited 0.
- Blue-noise spectrum: low_mean_to_mid_mean `0.07326260954141617`, low_peak_to_mid_mean `0.3122076988220215`, dc_power `2.0642223717004526e-06`.
- IGN verification: `ign_max_quantized_delta=0`.
- Tileability metric: max seam ratio `1.3395100831985474` against threshold `1.35`.
- File readback: BlueNoise `RGBA 256x256`, Flow `RGBA 128x128`.
- SHA256 BlueNoise: `AD6F279C6D9AF828D3E1E808896C11F9EB159AC6F560A412E2B87D9F6BD1F902`.
- SHA256 Flow: `32CCB138852E75017B9645CD138C1072D7193C8855D4D127FF3C58AB706C76AA`.

Verification boundary:
- STATIC/CLI verified.
- Unity import settings, texture wrap mode, no-mip/no-aniso, shader binding, runtime frame time, GCMonitor, and visual capture remain PENDING VERIFICATION.

## 2026-05-14 - Review Addendum

What was reviewed:
- Task status and rationale were re-read from disk before response.
- Repository-owned baker scripts and baked PNGs were checked for deterministic rebuild behavior.

What was done:
- Rebuilt BlueNoise and Flow PNGs into `%TEMP%\h8_noise_review_PROCEDURAL_NOISE_BAKER` with `python -B`.
- Compared SHA256 hashes against repository PNGs.
- Removed the temp directory after validating it resolved under `%TEMP%`.

Evidence:
- BlueNoise deterministic match: `AD6F279C6D9AF828D3E1E808896C11F9EB159AC6F560A412E2B87D9F6BD1F902`.
- Flow deterministic match: `32CCB138852E75017B9645CD138C1072D7193C8855D4D127FF3C58AB706C76AA`.
- Temp cleanup result: `TEMP_REMOVED`.

Verification boundary:
- Unity import settings, texture wrap mode, no-mip/no-aniso, shader binding, runtime frame time, GCMonitor, and visual capture remain PENDING VERIFICATION.

## 2026-05-14 - Review Addendum: Byte Identity Metrics

What was wrong:
- Metrics passed threshold checks but did not embed the exact PNG SHA256 values inside the JSON payload.
- The active `Docs/Tasks/CURRENT_BATCH.md` has rotated and no longer contains `PROCEDURAL_NOISE_BAKER`; using it now would contaminate this task with unrelated prompts.

What was done:
- Added `sha256_file()` to both `Tools/NoiseBaker/GenerateBlueNoise.py` and `Tools/NoiseBaker/VerifyBlueNoiseSpectrum.py`.
- Metrics now include `noise.bytes`, `noise.sha256`, `flow.bytes`, and `flow.sha256`.
- Regenerated `NoiseBakeMetrics.json`, `NoiseBakeMetrics.verify.json`, `NoiseBakeMetrics.verify2.json`, and `NoiseBakeMetrics.final.json`.
- Recorded the active batch drift in status/rationale instead of using unrelated current-batch prompts.

Cinematic Cheats used:
- No new runtime cheat. This hardens evidence for the already baked blue-noise and flow-field visual-fake textures.

Exact Microseconds saved:
- 0 us runtime. Offline evidence hardening only.

Evidence:
- `python -W error::DeprecationWarning -B Tools\NoiseBaker\GenerateBlueNoise.py` exited 0 with `status: NOISE BAKED`.
- `python -W error::DeprecationWarning -B Tools\NoiseBaker\VerifyBlueNoiseSpectrum.py --metrics Data\Textures\NoiseBakeMetrics.final.json` exited 0.
- `python -B Tools\NoiseBaker\VerifyBlueNoiseSpectrum.py --self-test` exited 0.
- BlueNoise metrics SHA256: `AD6F279C6D9AF828D3E1E808896C11F9EB159AC6F560A412E2B87D9F6BD1F902`.
- Flow metrics SHA256: `32CCB138852E75017B9645CD138C1072D7193C8855D4D127FF3C58AB706C76AA`.

Verification boundary:
- Unity import settings, texture wrap mode, no-mip/no-aniso, shader binding, runtime frame time, GCMonitor, and visual capture remain PENDING VERIFICATION.

## 2026-05-14 - Review Addendum: Metrics Portability

What was wrong:
- The baked PNGs were byte-stable, but tracked metrics JSON used machine-local absolute workspace paths.

What was done:
- Added repository-relative artifact path serialization to `Tools/NoiseBaker/GenerateBlueNoise.py`.
- Regenerated `NoiseBakeMetrics.json`, `NoiseBakeMetrics.verify.json`, `NoiseBakeMetrics.verify2.json`, and `NoiseBakeMetrics.final.json`.
- Re-ran the independent Fourier verifier after the metadata rewrite.

Cinematic Cheats used:
- No new runtime cheat. This was evidence hygiene around the existing offline visual-fake textures.

Exact Microseconds saved:
- 0 us runtime. The change affects offline metadata only.

Evidence:
- `python -B Tools\NoiseBaker\GenerateBlueNoise.py` exited 0 with `status: NOISE BAKED`.
- `python -B Tools\NoiseBaker\VerifyBlueNoiseSpectrum.py --metrics Data\Textures\NoiseBakeMetrics.final.json` exited 0.
- Absolute-path scan over owned baker, texture metrics, status, rationale, and log files returned no matches after documentation cleanup.
- BlueNoise hash unchanged: `AD6F279C6D9AF828D3E1E808896C11F9EB159AC6F560A412E2B87D9F6BD1F902`.
- Flow hash unchanged: `32CCB138852E75017B9645CD138C1072D7193C8855D4D127FF3C58AB706C76AA`.

Verification boundary:
- Unity import settings, texture wrap mode, no-mip/no-aniso, shader binding, runtime frame time, GCMonitor, and visual capture remain PENDING VERIFICATION.

## 2026-05-14 - Review Addendum: Stable Metrics

What was wrong:
- `NoiseBakeMetrics.json` stored local `bake_seconds`, causing tracked evidence churn on every full bake.

What was done:
- Removed default timing from generated tracked metrics.
- Added `--include-timing` for explicit local benchmarking when timing is wanted.
- Regenerated all tracked metrics and cleaned untracked verification scratch output plus Python bytecode cache.

Cinematic Cheats used:
- No new runtime cheat. This hardens the offline evidence path for the existing baked visual-fake textures.

Exact Microseconds saved:
- 0 us runtime. The change removes metadata churn only.

Evidence:
- `python -B Tools\NoiseBaker\GenerateBlueNoise.py` exited 0 with `status: NOISE BAKED`.
- `python -B Tools\NoiseBaker\VerifyBlueNoiseSpectrum.py --metrics Data\Textures\NoiseBakeMetrics.final.json` exited 0.
- Tracked metrics contain no volatile timing key.
- BlueNoise hash unchanged: `AD6F279C6D9AF828D3E1E808896C11F9EB159AC6F560A412E2B87D9F6BD1F902`.
- Flow hash unchanged: `32CCB138852E75017B9645CD138C1072D7193C8855D4D127FF3C58AB706C76AA`.

Verification boundary:
- Unity import settings, texture wrap mode, no-mip/no-aniso, shader binding, runtime frame time, GCMonitor, and visual capture remain PENDING VERIFICATION.

## 2026-05-14 - Review Addendum: Optimizer Boundary

What was wrong:
- Status wording said the generated script scan found no `subprocess`, but `GenerateBlueNoise.py` intentionally uses `subprocess.run` for optional offline PNG optimizers.
- The optimizer call had no timeout, so a broken external optimizer could stall an offline batch run.

What was done:
- Added `OPTIMIZER_TIMEOUT_SECONDS = 120`.
- Wrapped optimizer `subprocess.run` with `timeout=OPTIMIZER_TIMEOUT_SECONDS` and a `TimeoutExpired` fallback result.
- Corrected status wording so evidence says `subprocess` is offline optimizer-only, not absent.

Cinematic Cheats used:
- No runtime cheat added. This is offline tooling hardening for the existing baked visual-fake textures.

Exact Microseconds saved:
- 0 us runtime. The change only bounds offline optimizer execution.

Evidence:
- `python -m py_compile Tools\NoiseBaker\GenerateBlueNoise.py Tools\NoiseBaker\VerifyBlueNoiseSpectrum.py` passed.
- `python Tools\NoiseBaker\VerifyBlueNoiseSpectrum.py --metrics Data\Textures\NoiseBakeMetrics.final.json` exited 0.

Verification boundary:
- Unity import settings, texture wrap mode, no-mip/no-aniso, shader binding, runtime frame time, GCMonitor, and visual capture remain PENDING VERIFICATION.

## 2026-05-14 - Review Addendum: Independent Verifier

What was wrong:
- `VerifyBlueNoiseSpectrum.py` imported the generator's `verify_assets()` function, so producer and verifier shared the same formula/threshold implementation.

What was done:
- Rewrote `VerifyBlueNoiseSpectrum.py` as a standalone PNG readback verifier.
- Added independent IGN formula evaluation, Fourier spectrum thresholds, seam metrics, repository-relative path serialization, and JSON writing in the verifier.
- Regenerated verifier metrics with the standalone script.

Cinematic Cheats used:
- No new runtime cheat. This strengthens offline proof for the existing baked blue-noise and flow-field visual fakes.

Exact Microseconds saved:
- 0 us runtime. The change affects offline verification only.

Evidence:
- `python Tools\NoiseBaker\VerifyBlueNoiseSpectrum.py --metrics Data\Textures\NoiseBakeMetrics.final.json` exited 0.
- A normal verifier run did not create a local `Tools\NoiseBaker\__pycache__`.
- Independent verifier still reports `ign_max_quantized_delta=0`, low_mean_to_mid_mean `0.07326260954141617`, and low_peak_to_mid_mean `0.3122076988220215`.

Verification boundary:
- Unity import settings, texture wrap mode, no-mip/no-aniso, shader binding, runtime frame time, GCMonitor, and visual capture remain PENDING VERIFICATION.

## 2026-05-14 - Review Addendum: CLI Validation

What was wrong:
- Invalid `--seed` values could fail as raw Python conversion errors.
- Negative `--swaps` was silently clamped to zero.
- Custom seeds did not affect void/cluster tie-break jitter because that path used `DEFAULT_SEED`.

What was done:
- Added `parse_uint32_seed()` and `parse_nonnegative_int()` argparse validators.
- Rejected seed overflow, non-integer seed text, and negative swaps with argparse exit code 2.
- Passed the selected seed through `void_cluster_relax()` for deterministic custom-seed tie-break jitter.

Cinematic Cheats used:
- No new runtime cheat. This hardens the offline generator that produces the existing baked visual-fake textures.

Exact Microseconds saved:
- 0 us runtime. The change affects offline CLI failure behavior only.

Evidence:
- `python Tools\NoiseBaker\GenerateBlueNoise.py --seed 0x100000000 --verify-only` exits with code 2.
- `python Tools\NoiseBaker\GenerateBlueNoise.py --seed not_a_seed --verify-only` exits with code 2.
- `python Tools\NoiseBaker\GenerateBlueNoise.py --swaps -1 --verify-only` exits with code 2.
- Default `python -B Tools\NoiseBaker\GenerateBlueNoise.py` still exits 0 with `status: NOISE BAKED`.
- Independent verifier still exits 0 for final, verify, and verify2 metrics.
- BlueNoise hash unchanged: `AD6F279C6D9AF828D3E1E808896C11F9EB159AC6F560A412E2B87D9F6BD1F902`.
- Flow hash unchanged: `32CCB138852E75017B9645CD138C1072D7193C8855D4D127FF3C58AB706C76AA`.

Verification boundary:
- Unity import settings, texture wrap mode, no-mip/no-aniso, shader binding, runtime frame time, GCMonitor, and visual capture remain PENDING VERIFICATION.

## 2026-05-14 - Review Addendum: PNG Optimizer Safety

What was wrong:
- `zopflipng` was configured with the same input and output PNG path.
- The baker stopped after the first external optimizer failure instead of trying the next available optimizer.
- `Image.fromarray()` passed a redundant explicit `mode="RGBA"`.

What was done:
- Changed `zopflipng` to write `*.zopfli.tmp.png` and replace the source only after success.
- Added temp-output cleanup for zopfli failure/timeout.
- Continued optimizer discovery after external optimizer failure.
- Removed the redundant Pillow mode argument from the save path.

Cinematic Cheats used:
- No new runtime cheat. This hardens offline PNG output for the existing baked visual-fake textures.

Exact Microseconds saved:
- 0 us runtime. The change affects offline optimizer safety only.

Evidence:
- `python -W error::DeprecationWarning -B Tools\NoiseBaker\GenerateBlueNoise.py` exited 0 with `status: NOISE BAKED`.
- `python -W error::DeprecationWarning -B Tools\NoiseBaker\VerifyBlueNoiseSpectrum.py --metrics Data\Textures\NoiseBakeMetrics.final.json` exited 0.
- No `*.zopfli.tmp*` files exist under owned baker/texture folders.
- External optimizers remain absent on this host, so optimizer metric remains `pillow_optimize_compress_level_9`.

Verification boundary:
- Unity import settings, texture wrap mode, no-mip/no-aniso, shader binding, runtime frame time, GCMonitor, and visual capture remain PENDING VERIFICATION.

## 2026-05-14 - Review Addendum: Optimizer Candidate Replacement

What was wrong:
- `optipng` and `oxipng` can mutate files in place, so running them directly against source PNGs leaves no chance to reject larger or bad output before source mutation.

What was done:
- External optimizers now run against temporary candidate PNGs.
- The source PNG is replaced only if the candidate/output exists and is not larger than the original.
- Candidate and output temp files are deleted after success, failure, timeout, or larger-output rejection.

Cinematic Cheats used:
- No new runtime cheat. This protects the offline baked visual-fake texture artifacts.

Exact Microseconds saved:
- 0 us runtime. The change affects offline optimizer file safety only.

Evidence:
- Monkeypatched optimizer simulation rejected larger output with `pillow_optimize_compress_level_9_after_optipng_larger_output`.
- The simulation preserved source bytes and left no temp files.
- `python -W error::DeprecationWarning -B Tools\NoiseBaker\GenerateBlueNoise.py` exited 0 with `status: NOISE BAKED`.
- Independent verifier exited 0 after the full bake.
- BlueNoise hash unchanged: `AD6F279C6D9AF828D3E1E808896C11F9EB159AC6F560A412E2B87D9F6BD1F902`.
- Flow hash unchanged: `32CCB138852E75017B9645CD138C1072D7193C8855D4D127FF3C58AB706C76AA`.

Verification boundary:
- Unity import settings, texture wrap mode, no-mip/no-aniso, shader binding, runtime frame time, GCMonitor, and visual capture remain PENDING VERIFICATION.

## 2026-05-14 - Review Addendum: Flow Content Verification

What was wrong:
- Flow-field verification only proved `128x128 RGBA` shape and file size.
- A flat or low-variety flow lookup could pass that proof.

What was done:
- Added flow channel dynamic-range and unique-value gates to the baker verifier.
- Added the same independent flow gates to `VerifyBlueNoiseSpectrum.py`.
- Regenerated `NoiseBakeMetrics.json`, `NoiseBakeMetrics.verify.json`, `NoiseBakeMetrics.verify2.json`, and `NoiseBakeMetrics.final.json`.

Cinematic Cheats used:
- The flow lookup remains a low-tier visual fake, but now the fake is verified as non-flat presentation data.

Exact Microseconds saved:
- 0 us runtime. The change affects offline verification only.

Evidence:
- Full bake exited 0 with warnings-as-errors.
- Independent verifier exited 0 for final, verify, and verify2 metrics.
- Flow RGBA dynamic ranges: `121, 79, 250, 255`.
- Flow RGBA unique counts: `122, 80, 251, 256`.
- BlueNoise hash unchanged: `AD6F279C6D9AF828D3E1E808896C11F9EB159AC6F560A412E2B87D9F6BD1F902`.
- Flow hash unchanged: `32CCB138852E75017B9645CD138C1072D7193C8855D4D127FF3C58AB706C76AA`.

Verification boundary:
- Unity import settings, texture wrap mode, no-mip/no-aniso, shader binding, runtime frame time, GCMonitor, and visual capture remain PENDING VERIFICATION.

## 2026-05-14 - Review Addendum: Verifier Self-Test

What was wrong:
- The independent verifier proved positive cases, but did not prove that bad in-memory data is rejected.

What was done:
- Added `--self-test` to `VerifyBlueNoiseSpectrum.py`.
- Self-test covers flat noise, bad noise shape, flat flow, and bad flow shape.

Cinematic Cheats used:
- No new runtime cheat. This validates offline gates for the existing baked visual-fake textures.

Exact Microseconds saved:
- 0 us runtime. The change affects offline verifier confidence only.

Evidence:
- `python -B Tools\NoiseBaker\VerifyBlueNoiseSpectrum.py --self-test` exited 0.
- Self-test reported all four negative cases rejected.
- Normal independent verifier still exited 0 against `NoiseBakeMetrics.final.json`.

Verification boundary:
- Unity import settings, texture wrap mode, no-mip/no-aniso, shader binding, runtime frame time, GCMonitor, and visual capture remain PENDING VERIFICATION.

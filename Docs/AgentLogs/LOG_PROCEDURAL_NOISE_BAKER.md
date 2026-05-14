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

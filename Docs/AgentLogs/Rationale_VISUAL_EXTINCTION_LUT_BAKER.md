# VISUAL_EXTINCTION_LUT_BAKER Rationale

STATUS: OPTICS CALCULATED

## Decision 0 - Prompt Identity, Hygiene, And Scope
Problem: The user supplied role `DATA_SCIENTIST` and prompt ID `VISUAL_EXTINCTION_LUT_BAKER`; the XML tag contains 8 numbered tasks, while the section title claims 15 tasks.
Solution: Use `VISUAL_EXTINCTION_LUT_BAKER` as disk identity. Track 8 executable tasks because only 8 numbered task records exist inside the extracted tag. Treat the title count as stale batch text.
Rejected Alternatives: Guessing seven missing tasks was rejected. Reading neighboring XML prompts was rejected by the strict parsing rule.
Scalability potential: Low uses one R16F packed texture and one fog density LUT. Middle uses the same data with shader interpolation. High uses richer fog/color response from the same matrix. Ultra can spend saved runtime cost on stronger volumetric-style presentation without simulating water optics.
Hardware Impact: 0 runtime us. Correct prompt isolation avoids cross-domain churn.

## Decision 1 - Offline Beer-Lambert Fake Instead Of Runtime Optics
Problem: Deep water color needs believable extinction without runtime volumetric light transport.
Solution: Bake transmittance offline with Beer-Lambert `T = exp(-depthMeters * extinctionCoefficient)`. Store the result as raw little-endian float16 data for a single R16F 2D texture pack.
Rejected Alternatives: Runtime raymarch optics, per-light spectral simulation, or per-pixel water chemistry were rejected. The visual-fake mandate orders static LUTs before shader/raymarch truth.
Scalability potential: Low/MX350 samples one R16F texture and a 1D fog LUT. Middle/High can use bilinear filtering and stronger fog response. Ultra can add visual overkill in post/fog while keeping the same deterministic lookup.
Hardware Impact: Runtime sample cost replaces exponential math. Estimated 8-20 us/frame saved on MX350 if several water/fog shaders sample the LUT instead of evaluating exp/log per pixel.

## Decision 2 - Coefficients And External Reality Check
Problem: Red must die before the deep-ocean range; guessing by eye violates the prompt.
Solution: Anchor the spectral curve at 700nm red = 0.6240 m^-1, 530nm green = 0.0434 m^-1, and 470nm blue = 0.0106 m^-1. Interpolate absorption in log space across the 470-700nm axis. NOAA deep-sea color guidance confirms red is filtered early and absent by deep-ocean depths.
Rejected Alternatives: Artistic RGB ramps were rejected. Linear RGB-only three-entry ramps were rejected because the prompt demands a 256x256x256 depth/turbidity/wavelength matrix.
Scalability potential: Low gets the same physical anchors as High; tier changes alter sampling and art response, not base data.
Hardware Impact: Offline cost only. Runtime impact is one texture load or three channel loads instead of per-channel exponential math.

## Decision 3 - Matrix Packing
Problem: A 256x256x256 matrix is too large for a naive 256x65536 texture height on MX350/D3D-class texture limits.
Solution: Keep raw C-order data and document upload as a 4096x4096 R16F texture. HLSL maps `flatIndex = ((depth * 256) + turbidity) * 256 + wavelength`, then `x = flatIndex & 4095`, `y = flatIndex >> 12`.
Rejected Alternatives: Texture3D was rejected because the prompt says single 2D texture. 256x65536 packing was rejected because it risks exceeding practical texture dimension limits.
Scalability potential: Low/MX350 uses point or bilinear R16F sampling. Middle can use filtered depth/turbidity sampling. High/Ultra can add overkill fog/ray cues on top without changing packed data.
Hardware Impact: 32 MiB matrix as R16F. Float32 would be 64 MiB, so half precision saves exactly 33,554,432 bytes.

## Decision 4 - Silt And Fog Density Source
Problem: The task requires fog density per meter accounting for silt values from the climate side, but no single `ClimateSimulator` file was found in the active prompt context.
Solution: Scan the current RuntimeVisualProfiles for `turbidityMultiplier` and use `Profile_Underwater.asset` fogDensity 0.0021 as the base. The script records the scanned silt count/range/mean in `Water_Extinction_Matrix.json`.
Rejected Alternatives: Hardcoding one silt multiplier was rejected. Runtime scene lookup was rejected because this is offline data and would create direct dependency risk.
Scalability potential: Low uses the representative half LUT. Middle/High can feed biome turbidity into the matrix sampler. Ultra can layer extra presentation fog without changing the source matrix.
Hardware Impact: Fog LUT is 512 bytes. Estimated shader-side savings: 2-6 us/frame where depth fog would otherwise recompute density response.

## Decision 5 - Preview Without External Image Libraries
Problem: The previewer must run on this workspace without assuming Pillow or a GUI.
Solution: Wrote a direct PNG encoder using `zlib` and PNG chunks. The preview is a 1024x512 vertical depth gradient with 100m reference lines.
Rejected Alternatives: Pillow dependency and manual image export were rejected. PPM fallback was rejected because the prompt asked for a gradient preview and PNG is directly usable.
Scalability potential: Preview is offline only; it documents Low-to-Ultra color progression without runtime cost.
Hardware Impact: 0 runtime us. Offline script ran successfully.

## Decision 6 - Verification Result
Problem: The recursive verification requires exact red channel zero at 500m.
Solution: Ran `python Tools/WaterColorPreview.py --force`, then direct binary validation. `matrix[85,0,255]` is `0.0`, where depth index 85 equals 500m on the 0-1500m/255 axis. Metadata status is `OPTICS CALCULATED`.
Rejected Alternatives: Relying on console text alone was rejected; direct memmap verification checked the raw binary artifact.
Scalability potential: All tiers consume the same verified red extinction base; tier changes affect sampling/art response only.
Hardware Impact: Prevents shader-side red compensation and color-grade hacks that would cost runtime and break deep-sea noir.

## Decision 7 - Compilation Boundary
Problem: The project asks for compile verification, but this task changed only Python/offline data/HLSL snippet text and no C#.
Solution: Ran `python -m py_compile Tools/WaterColorPreview.py`. Unity C# compile was not launched because no Unity runtime script changed; the HLSL is a snippet file, not inserted into a shader include.
Rejected Alternatives: Launching Unity compile for an offline Python/data-only bake was rejected as noise in a concurrent multi-agent workspace.
Scalability potential: No runtime assembly risk.
Hardware Impact: 0 runtime us.

## Decision 8 - OMEGA Polish Boundary
Problem: The protocol requires reading `<POLISH_MANDATE>` only after status is fully checked.
Solution: Re-read status/rationale, then extracted `<POLISH_MANDATE>` from `Docs/Tasks/CURRENT_BATCH.md`; the tag is absent. Ran targeted anti-bloat checks anyway: `git diff --check` on new text artifacts, banned Unity runtime pattern scan, and output size listing.
Rejected Alternatives: Inventing a polish mandate was rejected. Running repo-wide destructive cleanup was rejected as cross-agent sabotage.
Scalability potential: No runtime effect. The generated matrix remains the Low/Middle/High/Ultra data base.
Hardware Impact: 0 runtime us. Anti-bloat confirmed no accidental Unity hot-path code was introduced.

## Decision 9 - Per-Meter Fog Correction
Problem: Follow-up audit found the fog LUT evidence was weaker than the prompt wording. The first pass wrote 256 depth samples, but the prompt says fog density per meter.
Solution: Changed `Water_Fog_Density_LUT.bin` to 1501 half entries for exact integer depths 0-1500m. Metadata now records fog axis count, min, max, and step.
Rejected Alternatives: Keeping 256 samples was rejected because it is an approximation. Float32 fog was rejected because task 7 and the MX350 target favor half storage; density precision is sufficient for fog presentation.
Scalability potential: Low samples nearest or linearly filters the per-meter table. Middle/High can use the same table with biome turbidity. Ultra can layer volumetric-style presentation while using this deterministic base.
Hardware Impact: Fog LUT grew from 512 bytes to 3002 bytes, still negligible. It remains a shader lookup and saves the estimated 2-6 us/frame that runtime depth/silt density math would consume.

## Decision 10 - HLSL Index Polish
Problem: The generated snippet used `round()`, which is unnecessary ALU for discrete lookup indexing.
Solution: Replaced `round(x)` with `(uint)(x + 0.5)` after saturating axes, and cast the packed texture coordinate to `int2` for `LOAD_TEXTURE2D`.
Rejected Alternatives: Keeping `round()` was rejected as wasteful shader ALU. Switching to 3D texture sampling was rejected because the prompt requires a single 2D texture on MX350.
Scalability potential: Same packed data path across all tiers.
Hardware Impact: Sub-us shader ALU reduction per sample; exact frame gain requires GPU capture.

## Decision 11 - Metadata Evidence Closure
Problem: The generated metadata proved the binary math but did not preserve the GI relay log inputs or the external red-extinction reference used in the self-audit.
Solution: Added `inputSources.giRelayLogsReadByCli` and `selfAudit.referenceChecks` to `Water_Extinction_Matrix.json`, then regenerated all artifacts so the hash block reflects the final snippet and metadata.
Rejected Alternatives: Leaving evidence only in chat was rejected. Copying long external text was rejected; metadata stores compact source identifiers and applied checks.
Scalability potential: Integration agents can wire the same LUT to Low through Ultra without re-discovering source assumptions.
Hardware Impact: 0 runtime us. Metadata-only evidence improves integration safety.

## Decision 12 - Executable Artifact Clean Scan
Problem: A broad text scan reported `round(` in log prose and one offline preview guide-line calculation.
Solution: Removed Python preview `round()` and reran the baker. Targeted scan over executable artifacts `Tools/WaterColorPreview.py` and `Data/Visuals/Water_Extinction_Hecton_CoreLit_Snippet.hlsl` now reports no banned Unity runtime patterns and no `round(`.
Rejected Alternatives: Treating all broad-scan hits as runtime defects was rejected because the docs intentionally describe removed code. Leaving the Python `round()` was also rejected; cheaper integer `+0.5` is trivial.
Scalability potential: No runtime effect. Keeps generated artifact path cleaner for future automation gates.
Hardware Impact: 0 runtime us; offline preview generation change is negligible.

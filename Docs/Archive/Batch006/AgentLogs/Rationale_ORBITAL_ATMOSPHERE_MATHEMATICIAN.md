# Rationale_ORBITAL_ATMOSPHERE_MATHEMATICIAN

Status: ATMOSPHERE BAKED - HARDENED PY_CLI VERIFIED; UNITY PENDING VERIFICATION  
Evidence boundary: STATIC_SOURCE / PY_CLI after generation. Unity import, shader consumption, profiler, GCMonitor, Frame Debugger, player build, and visual QA are not proven by this file.

## Decision 0 - Offline LUT Instead Of Runtime Atmosphere Integration

Problem: The prompt asks for Rayleigh/Mie atmosphere visuals for the space prologue. Full runtime atmospheric integration would burn shader ALU and invite quality-tier divergence on MX350.

Solution: Use the DOD/offline-data pattern already present in `Tools/MathLUTGenerator.py`: bake deterministic lookup tables and load them cold. This obeys the Cinematic Cheat Protocol: visible sky belief is carried by a LUT, not by simulating atmospheric truth per pixel.

Rejected Alternatives: Full shader raymarching, per-frame atmospheric integration, or runtime CPU integration. Standard Unity skybox scattering is too opaque for strict binary proof and does not satisfy the exact half-float output request.

Scalability potential: Low uses the baked LUT directly. Middle increases sampling precision or blends weather tint. High adds extra atmospheric detail using the same LUT contract. Ultra spends saved cycles on richer post/exposure and optional volumetric shafts without changing base data.

Hardware Impact: Estimated low-end i3/MX350 gain is presentation ALU avoidance, not measured runtime proof. Static estimate: replacing 16-32 scatter steps with a texture/LUT sample can save hundreds of shader instructions per sky pixel; profiler proof remains required.

## Decision 1 - Isolated Atmosphere Output Directory

Problem: `Data/Precomputed/math_lut_manifest.json` is owned by an existing offline precompute task and is dirty in the current worktree. Mutating it risks cross-agent conflict.

Solution: Create atmosphere-specific files under `Data/Precomputed/Atmosphere/` with a separate `atmosphere_lut_manifest.json`.

Rejected Alternatives: Editing the shared math manifest or overwriting existing binaries. That would create avoidable coupling with another agent's ownership.

Scalability potential: The isolated manifest can later be imported by an atmosphere-specific runtime loader or merged by the Integrator without blocking current binary generation.

Hardware Impact: No runtime cost from directory layout. Avoids integration churn and preserves deterministic file ownership on low-end build validation machines.

## Decision 2 - Half-Float Binary Contract

Problem: The prompt requires exact 16-bit half-float LUT output. Existing `MathLUTGenerator.py` uses float32 for older math tables, so copying that scalar format would violate the task and double VRAM consumption.

Solution: Use Python `struct.pack("<e", value)` for every binary scalar. The manifest records `scalarFormat=float16`, `scalarBytes=2`, `structPackFormat=<e`, exact byte counts, and SHA-256.

Rejected Alternatives: Float32 arrays, PNG-only data, EXR output, or Unity Texture2D authoring from an editor script. Float32 costs 2x memory; image formats hide scalar proof; editor import is unnecessary for this offline math task.

Scalability potential: Low/Middle use this same compact half-float payload. High/Ultra can layer extra shafts/exposure/stars without changing the base binary contract.

Hardware Impact: Static VRAM delta versus float32 is 263168 bytes saved for the two current payloads. This is small alone, but it preserves the project habit of not wasting MX350 memory. Runtime proof is absent until Unity imports/samples the data.

## Decision 3 - Smooth Visual Altitude For Seam Removal

Problem: A uniform altitude gradient from ocean horizon to space can produce a visible first-row seam where dense near-surface scatter transitions into the higher atmosphere.

Solution: Keep the 128-layer density matrix uniform for deterministic data, but use a smoothstep visual altitude remap in the 2D sky LUT. Python audit measured `maxSurfaceSeamDelta=0.004634528095092683` against the `0.030` threshold.

Rejected Alternatives: Hand-painted seam blur or post-generated image filtering. Those would hide the math failure instead of making the LUT deterministic and reproducible.

Scalability potential: Low uses the smooth LUT directly. Middle/High/Ultra can add weather or shaft overlays without reintroducing the seam because the base gradient is continuous.

Hardware Impact: Estimated low-end gain is avoided runtime seam-fix code and no extra shader branch. Measured artifact: `Docs/AgentLogs/AtmoValidation_ORBITAL_ATMOSPHERE_MATHEMATICIAN.json`; profiler proof remains absent.

## Decision 4 - Relativity Fake From Visual Perspective

Problem: A 5000 m orbital/space-prologue mesh is too small to read as a planet without visual curvature, but true planet-scale geometry would collide with AUP/floating-origin discipline and create expensive far-field precision problems.

Solution: Use logarithmic depth plus a bounded horizon drop:

`remap = saturate(log2(1 + distanceMeters * 0.0022) / log2(1 + 5000 * 0.0022))`

`horizonDropMeters = (distanceMeters^2 / (2 * 280000)) * (0.25 + 0.75 * remap)`

The viewer reads the horizon as planetary because the far edge bends faster than the near field while near-field gameplay geometry remains stable.

Rejected Alternatives: True spherical planet mesh, real orbital scale, camera far-clip expansion, or per-vertex physics displacement. Standard large-world geometry is too costly for this presentation-only prologue effect.

Scalability potential: Low keeps only the vertex curvature drop. Middle adds LUT sky tint and fixed exposure. High adds richer sun shafts and stars. Ultra adds stronger cinematic exposure, denser shafts, and optional higher-resolution sky media while keeping the same base mesh and LUT contract.

Hardware Impact: On i3/MX350, the fake avoids planet-scale mesh density, precision mitigation, and runtime atmospheric integration. Static estimate only: one logarithmic remap plus vertex offset is cheaper than additional planetary geometry, but Unity GPU timing is PENDING VERIFICATION.

## Regression Model

CPU: Offline Python generation only. Runtime CPU cost is unchanged until a loader/shader consumer is implemented.

GC: Offline Python allocations do not affect Unity hot paths. Unity GC proof is absent.

Memory: Half-float payloads total `263168` bytes for the two binaries, plus a `21495` byte preview PNG outside runtime. Float32 equivalent would be `526336` bytes for the binaries.

Cadence: No Tick/Update/FixedUpdate code was introduced.

Correctness: Python tests verify formulas, finite density rows, gradient thresholds, half-float packing, manifest hashes, and curvature monotonicity. Unity shader binding is not verified.

Failure Modes: Corrupt binary hash mismatch, wrong scalar format, visible horizon seam, non-finite scattering value, overly bright void black, or weak golden-hour signal. Current Python validation catches these; Unity import/runtime failures remain outside this task's proof.

WHY KEPT: The LUT approach gives controlled visuals with exact binary proof and no runtime system coupling.

WHY REJECTED: Runtime physical atmosphere, planet-scale mesh, and float32 LUTs waste performance/memory for a presentation effect.

## Final Evidence

Problem: The batch required all atmosphere LUT tasks complete with disk evidence.

Solution: Final verification commands completed:

- `git diff --check -- Tools/AtmoPreview.py Tools/test_atmo_preview.py Docs/Design/Atmosphere_Scattering_LUT.md Docs/Tasks/Status_ORBITAL_ATMOSPHERE_MATHEMATICIAN.md Docs/AgentLogs/Rationale_ORBITAL_ATMOSPHERE_MATHEMATICIAN.md Docs/AgentLogs/LOG_ORBITAL_ATMOSPHERE_MATHEMATICIAN.md`
- `python -m py_compile Tools/AtmoPreview.py Tools/test_atmo_preview.py`
- `python Tools/test_atmo_preview.py`
- `python Tools/AtmoPreview.py`
- `python Tools/AtmoPreview.py --verify`

Rejected Alternatives: Unity Editor import claims, profiler claims, or GC claims without artifacts.

Scalability potential: The generated base data supports low/middle/high/ultra presentation layering without changing the binary layout.

Hardware Impact: CLI evidence proves exact compact payloads. Runtime hardware impact remains PENDING VERIFICATION until Unity consumes the files.

## Decision 5 - Decode The Quantized Payload, Not Just The Source Math

Problem: The earlier validation proved source math, byte counts, hashes, and pre-quantized gradient metrics. It did not prove the decoded RGBA16F binary payload still passed seam/finite/density checks after half-float quantization.

Solution: `validate_existing_output()` now decodes both binary files with `struct.unpack_from("<e", ...)`. It audits the decoded sky LUT with the same seam/void/golden-hour thresholds and audits the decoded density matrix for row count, altitude bounds, finite values, monotonic density, and absorption range.

Rejected Alternatives: Trusting SHA-256 and byte count alone. That proves identity, not semantic payload quality if the manifest was regenerated over bad data.

Scalability potential: The validation contract remains cold/offline. Low-tier runtime stays unchanged, while High/Ultra can depend on the same validated base payload.

Hardware Impact: No runtime cost. Added Python validation time only. It prevents shipping a compact but invalid binary that would waste QA/profiler time on MX350.

## Final Hardened Evidence

Problem: User escalation demanded stronger certainty after completion.

Solution: Added two tests and reran the gate:

- `test_verify_decodes_quantized_half_payloads`
- `test_verify_rejects_hashed_but_nonfinite_half_payload`
- `python Tools/test_atmo_preview.py`: PASS, 9 tests
- `python Tools/AtmoPreview.py`: PASS
- `python Tools/AtmoPreview.py --verify`: PASS

Rejected Alternatives: Re-running the same previous tests without improving the failure model.

Scalability potential: Same as above; this is validation hardening, not runtime bloat.

Hardware Impact: Runtime impact remains zero because the added work is offline validation only.

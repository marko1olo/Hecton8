# Rationale - HYDRODYNAMIC_DRAG_MATRIX_BAKER

## Session Start

Problem: Submarine motion feel is undefined; runtime systems need deterministic scalar/tensor data for drag, added mass, cavitation, and angular resistance.
Solution: Generate offline coefficient data and validation plots. Keep runtime integration out of scope; exported constants can be consumed later by Burst/FixedTick systems using preallocated arrays.
Rejected Alternatives: Direct Unity Rigidbody tuning by inspector trial was rejected because it hides hydrodynamic intent, creates non-repeatable feel, and encourages direct `AddForce` misuse outside PhysicsApplySystem.
Scalability potential: Low uses diagonal scalar coefficients; Middle adds per-axis translational drag; High adds angular tensor and cavitation state; Ultra can spend saved CPU on richer wake, acoustic, and cockpit feedback visuals.
Hardware Impact: On i3/MX350, offline baking shifts coefficient fitting out of play mode. Runtime benefit is expected to be microsecond-scale because consumers read constants instead of solving hull hydrodynamics per frame. Measured proof absent.

## Tasks 1-5 Decisions

Problem: Runtime submarine physics needs directional water resistance without a CFD dependency.
Solution: Export diagonal CdA tensors for surge/sway/heave and Cl values for pitch/yaw control authority. The diagonal form matches the current mandate for scalar mass and `force * rcp(mass + addedMass)`.
Rejected Alternatives: A mesh-derived runtime wetted-area calculator was rejected because it would allocate or require Native mesh buffers, add import fragility, and waste frame time for coefficients that are stable per hull.
Scalability potential: Low reads one axial drag coefficient. Middle reads XYZ CdA. High reads angular damping. Ultra spends the saved CPU on richer wake trails, cockpit vibration, sonar noise bloom, and depth-dependent cavitation visuals.
Hardware Impact: Estimated 8-20 us saved per vehicle fixed-step versus fitting projected area and Cd at runtime on i3/MX350. Measured proof absent.

Problem: Hull acceleration felt at risk of becoming spacecraft motion because rigidbody mass alone ignores the water accelerated with the vehicle.
Solution: Added-mass tensors are derived from displaced seawater mass with hull-shape coefficients. Runtime consumers use effective mass per local axis.
Rejected Alternatives: Full 6x6 added-mass coupling was rejected for this batch because no roll/sway coupling gameplay requirement exists and diagonal tensors are deterministic, readable, and cheap.
Scalability potential: Low uses X-axis added mass only. Middle uses XYZ. High/Ultra can add cross-coupling later only if profiler and control feel demand it.
Hardware Impact: Estimated 3-8 us saved per vehicle fixed-step versus a coupled matrix solve. Measured proof absent.

Problem: Cavitation must create acoustic threat and visual feedback without simulating vapor bubbles.
Solution: Sigma-threshold tables by depth produce deterministic onset speeds for VFX/audio/noise state.
Rejected Alternatives: Per-bubble particle physics and acoustic bubble cloud simulation were rejected by the Cinematic Cheat Protocol. The player needs noise, vibration, sonar risk, and wake visuals, not vapor truth.
Scalability potential: Low uses binary cavitation state. Middle adds intensity ramps. High adds wake distortion and filtered audio. Ultra adds dense visual overkill while keeping gameplay threshold identical.
Hardware Impact: Estimated >100 us saved during cavitation events versus dynamic bubble simulation on low-end silicon. Measured proof absent.

Problem: Unity `angularDrag` cannot make pitch, yaw, and roll feel like separate underwater axes.
Solution: Export roll/pitch/yaw quadratic torque resistance and added angular inertia tensors.
Rejected Alternatives: Single scalar angular drag was rejected because it makes a box tug and manta-shaped sub rotate with the same fake feel.
Scalability potential: Low clamps roll/yaw with scalar fallback. Middle uses 3-axis damping. High/Ultra bind damping state to cockpit shake and external wake visuals.
Hardware Impact: Estimated 2-5 us saved per vehicle fixed-step by using precomputed tensor constants. Measured proof absent.

## Tasks 6-8 Decisions

Problem: A submarine that reaches 50 m/s quickly would read as a spacecraft, not a water-displacing pressure vessel.
Solution: The simulator validates terminal speed, 90-second acceleration, `time_to_50_mps_seconds`, and stop distance per hull. All current hulls stay below 50 m/s and stop over more than 3 hull lengths.
Rejected Alternatives: Subjective feel-only review was rejected because it cannot protect future coefficient changes from reintroducing arcade acceleration.
Scalability potential: Low uses the same physical gate with cheaper visuals. Middle adds cockpit warnings. High/Ultra add stronger cavitation, wake, sonar, and vibration responses near terminal-speed regimes.
Hardware Impact: Offline gate has 0 runtime cost. Future runtime impact remains constant lookup plus analytical drag. Measured in-Unity proof absent.

Problem: Data must survive context loss and parallel-agent handoff.
Solution: Exported `Data/Physics/Submarine_Specs.json`, `Submarine_SpeedPower.csv`, `Submarine_SpeedPower.svg`, and `Submarine_Verification.json` from a reproducible Python tool.
Rejected Alternatives: Chat-only hydro tables and spreadsheet-only formulas were rejected because the CTO reads disk artifacts and runtime teams need machine-readable constants.
Scalability potential: Low consumes axial constants only. Middle consumes full linear tensors. High consumes angular tensors and cavitation thresholds. Ultra turns threshold slack into visual overkill.
Hardware Impact: JSON is load-time only; hot-path JSON parsing is forbidden. Runtime consumers should bake into structs/NativeArrays during initialization. Measured proof absent.

Problem: "Expensive Weight" needed a technical basis, not narrative language.
Solution: Weight is enforced by added mass, cubic power demand, depth-dependent cavitation, per-axis drag, and stop-distance validation.
Rejected Alternatives: Raising rigidbody mass alone was rejected because it slows acceleration but does not create lateral water resistance, power curve cost, or acoustic consequences.
Scalability potential: Toaster path keeps the same control truth with minimal effects; top-tier path spends saved CPU/GPU on wake sheets, hull creak audio, sonar bloom, and cockpit vibration.
Hardware Impact: On i3/MX350 the core math remains scalar/tensor constant access. High-tier visual overkill is gated outside the physics truth path.

## Polish / Anti-Bloat

Problem: The mandatory `<POLISH_MANDATE>` tag is absent from `Docs/Tasks/CURRENT_BATCH.md`.
Solution: Searched the batch file for `POLISH|Polish|polish|MANDATE`, confirmed no tag, then executed local anti-bloat checks after core tasks were complete.
Rejected Alternatives: Ignoring polish was rejected; inventing a fake mandate was rejected because the batch file is the authority.
Scalability potential: The baker stays deterministic and offline; runtime scalability remains in the exported data contract, not in extra simulator complexity.
Hardware Impact: No runtime impact. Removed unused import from the Python tool. Deterministic rerun hashes were stable. Measured Unity Player proof absent.

Problem: Verification could drift if the script used time, randomness, network calls, or process shelling.
Solution: Scanned the baker for `datetime`, `random`, `time.`, `uuid`, `requests`, `urllib`, `subprocess`, and `os.system`; none found.
Rejected Alternatives: Timestamped generation was rejected because it breaks reproducible hashes and creates noisy diffs.
Scalability potential: Deterministic data can be cached, compared, and consumed by low/high-tier runtime paths without hidden state.
Hardware Impact: Offline only. No hot-path cost.

## Persistent Regression Tests

Problem: The previous validator was a one-off shell command and would not protect future coefficient edits.
Solution: Added `Tools/test_submarine_physics_sim.py` with unittest coverage for output contract, 50 m/s gate, stop-distance gate, diagonal positive tensors, cavitation threshold monotonicity, power-curve monotonicity, and cross-directory byte determinism.
Rejected Alternatives: Leaving validation as command history was rejected because context compression and parallel agents make chat-only verification disposable.
Scalability potential: The test locks the cheap runtime contract while allowing future high-tier visual overkill to change outside physics truth.
Hardware Impact: No runtime impact. Test is offline. It prevents future data drift before Unity integration.

Problem: `Submarine_Verification.json` included output-directory-specific paths, breaking cross-directory deterministic bytes.
Solution: Changed verification summary fields to stable artifact file names.
Rejected Alternatives: Ignoring path-dependent bytes was rejected because deterministic artifacts are easier to diff and cache.
Scalability potential: Stable artifacts can be compared by CI or batch integrators without workspace path noise.
Hardware Impact: Offline only. No hot-path cost.

## Self-Contained Plotter

Problem: PNG plot output depended on optional matplotlib. On this machine the earlier run produced `power_png: null`, which is a weak result for a task that explicitly asked to run the physics plotter.
Solution: Replaced optional matplotlib output with a deterministic pure-Python PNG writer using explicit PNG chunks, zlib compression, and a fixed 1200x720 RGB pixel buffer. SVG remains available for readable vector inspection.
Rejected Alternatives: Installing matplotlib was rejected because adding a dependency to satisfy an offline plot is unnecessary. Leaving SVG-only output was rejected because the verification summary had a `power_png` field and the artifact can be produced without third-party code.
Scalability potential: Offline plot artifacts can be inspected by low/high-tier designers without affecting runtime. Runtime still consumes only baked constants.
Hardware Impact: Runtime impact is 0 us. Offline PNG generation cost is outside frame time. Initial deterministic PNG SHA256 was `BC7A8882B8A3F861648E883677BCF30FFA516CC384413E2B822A315BE6D886D7`; current canonical plot PNG SHA256 is `E9CE89566C80CC19CF6149F52D3CEFA2D67CB4D7B7A3F8498F02CFC11EAAA792`.

## Artifact Manifest

Problem: `Submarine_Verification.json` reported pass/fail but did not prove exact artifact payloads.
Solution: Added deterministic byte counts and SHA256 hashes for `Submarine_Specs.json`, `Submarine_SpeedPower.csv`, `Submarine_SpeedPower.svg`, and `Submarine_SpeedPower.png`.
Rejected Alternatives: Relying on git status or chat summaries was rejected because integrators need machine-readable evidence on disk.
Scalability potential: CI and batch integrators can compare manifest hashes without re-running the full Unity project.
Hardware Impact: Offline only. Manifest hashing has 0 runtime cost and no gameplay memory impact.

## Verify-Only CLI

Problem: The baker could regenerate artifacts, but an integrator could not validate existing artifacts without rewriting them.
Solution: Added `--verify-only`, which validates file presence, verification status, manifest byte counts, manifest hashes, hull gates, CSV columns/row count/monotonic power curves, SVG header, and PNG header/dimensions.
Rejected Alternatives: Asking integrators to run unittest or trust the manifest manually was rejected because batch gates need a single direct CLI command.
Scalability potential: CI can use verify-only on cheap machines without Unity, then reserve Unity/Profiler work for runtime integration.
Hardware Impact: Offline only. No runtime impact. Same-size artifact corruption is now detected by SHA256.

## Lift Curves and Tier Contract

Problem: Static Cl slopes were correct but not enough evidence for a "calculator" task; consumers needed explicit sample behavior.
Solution: Exported deterministic pitch/yaw lift curve samples from -15 to +15 degrees for every hull, bounded by each hull's max control-surface Cl.
Rejected Alternatives: Runtime lift-curve generation was rejected because the curve is stable per hull and belongs in the offline data bake.
Scalability potential: Low ignores lift curves and uses axial damping only. Middle consumes lift curves for predictable control response. High/Ultra can map lift/cavitation ranges to stronger wake, sonar, and cockpit feedback without changing physics truth.
Hardware Impact: Offline table generation has 0 runtime cost. Runtime consumers can read fixed arrays during initialization. New specs SHA256 is `F5990771914EE80DA73FFEBC5B7568C9A79C697394AD1B3753ABAB130D7E63EE`.

Problem: Scalability guidance existed in rationale but not in the machine-readable export.
Solution: Added `quality_tier_runtime_usage` with Low/Middle/High/Ultra usage paths.
Rejected Alternatives: Keeping tier guidance only in prose was rejected because runtime integrators need the intended tier split next to the constants.
Scalability potential: Toaster path remains cheap; top-tier path spends saved budget on visual overkill while using identical physics gates.
Hardware Impact: JSON metadata only. No runtime hot-path impact if loaded once.

## Runtime Binary Pack

Problem: JSON is acceptable for authoring but forbidden in hot paths; runtime teams needed a fixed-layout import option.
Solution: Added `Submarine_RuntimePack.bin`, a deterministic little-endian binary pack with magic `H8HYDRO\0`, version 1, 5 hull records, 53 floats per record, and 220-byte record stride. Records include stable FNV-1a hull IDs and core runtime constants.
Rejected Alternatives: C# runtime codegen was rejected because this batch is offline data bake only. Keeping JSON-only output was rejected because it leaves the no-hot-path-JSON contract incomplete for future import work.
Scalability potential: Low-tier runtime can load only the axial subset from each fixed record. Middle/High/Ultra can consume additional tensor/lift/cavitation ranges without changing file layout.
Hardware Impact: Runtime hot-path impact is 0 us if loaded once during initialization. Binary size is 1124 bytes. Runtime pack SHA256 is `872091D8234D11848BAEC016A0C526717840F507F68C464E5317D2D48D534350`.

## Runtime Layout Document

Problem: A binary pack without explicit field offsets forces future consumers to reverse-engineer Python order.
Solution: Added `Submarine_RuntimePackLayout.json` with header metadata, record stride, shape hash/index fields, all 53 float field names, byte offsets, formats, and hull order.
Rejected Alternatives: A prose-only layout in chat or logs was rejected because runtime import code needs machine-readable offset authority.
Scalability potential: Low/Middle/High/Ultra importers can selectively load fields by index while preserving one file format.
Hardware Impact: Offline JSON layout only. No runtime hot-path impact if used during import-code generation or editor validation. Layout SHA256 is `320C8DA303174F9DCFA9C7FC47CF212498334413F192782984E49CD5AC3274E1`.

## Runtime Pack Round-Trip Validation

Problem: The binary pack validator proved header shape and record stride, but a shifted float field could still pass structural checks.
Solution: Added `read_runtime_pack()` and verify-only cross-checking against `Submarine_Specs.json` for all 5 hulls and all 53 float fields per record, with finite-value enforcement and field-level failure messages.
Rejected Alternatives: Trusting SHA256 alone was rejected because hashes detect mutation but do not explain field drift. Trusting layout JSON alone was rejected because it documents offsets but does not prove payload parity.
Scalability potential: Low-tier importers can still consume a small axial subset, while Middle/High/Ultra importers can trust that all additional tensor/lift/cavitation fields are aligned to the JSON authority.
Hardware Impact: Offline validation only. Runtime hot-path impact remains 0 us if the pack is loaded once during initialization. Regression suite now passes 14 tests with runtime pack payload-corruption coverage.

## Runtime Layout Drift Gate

Problem: The runtime layout file documented offsets, but verify-only only checked schema/version/stride/field count. A renamed or shifted field could survive as long as the skeleton remained valid.
Solution: Verify-only now compares the current layout header, hull order, and every field definition against `runtime_pack_layout_document()`.
Rejected Alternatives: Manifest hash-only detection was rejected because it proves bytes changed but not which layout contract broke. Partial schema checks were rejected because field order is the actual runtime safety contract.
Scalability potential: Low/Middle/High/Ultra importers can trust the same canonical layout and fail fast if the offline pack field authority drifts.
Hardware Impact: Offline validation only. Runtime hot-path impact remains 0 us. Regression suite now passes 15 tests with layout drift corruption coverage.

## Power CSV Semantic Gate

Problem: `Submarine_SpeedPower.csv` could be made monotonic and have a matching manifest while still not matching the JSON hull specs that generated it.
Solution: Verify-only now recomputes the speed/power rows from `Submarine_Specs.json` and compares every speed row and every hull power column.
Rejected Alternatives: Manifest-only validation was rejected because it can be updated with bad bytes. Monotonic-only validation was rejected because physically wrong curves can remain monotonic.
Scalability potential: Designers and integrators can trust the graph source data across Low/Middle/High/Ultra tuning without guessing whether CSV and JSON drifted apart.
Hardware Impact: Offline validation only. Runtime hot-path impact remains 0 us. Regression suite now passes 16 tests with manifest-updated CSV corruption coverage.

## Non-Finite Numeric Gate

Problem: Python JSON can carry `NaN`/`Inf` tokens, and ordinary comparison checks can miss `NaN` because comparisons against it are false.
Solution: Verify-only now recursively rejects non-finite numeric values in `Submarine_Specs.json` before downstream tensor/pack/CSV validation can trust them.
Rejected Alternatives: Relying on JSON parsing was rejected because Python accepts non-finite constants by default. Relying on runtime importers was rejected because the offline authority should fail before poisoning NativeArray constants.
Scalability potential: All Low/Middle/High/Ultra import paths get the same finite data authority and no tier inherits a poisoned coefficient.
Hardware Impact: Offline validation only. Runtime hot-path impact remains 0 us. Regression suite now passes 17 tests with manifest-updated NaN corruption coverage.

## Strict JSON and Manifest-Updated Runtime Corruption Gates

Problem: Generated JSON still used Python's default permissive encoder, and some corruption tests could pass primarily because the artifact manifest hash changed.
Solution: `write_json()` now uses `allow_nan=False`. Runtime pack and runtime layout corruption tests now refresh the manifest after mutation, forcing semantic validators to catch the drift.
Rejected Alternatives: Trusting hashes alone was rejected because a bad actor or broken tool can update hashes with bad payloads. Permissive JSON output was rejected because non-standard `NaN`/`Inf` tokens are poison for deterministic importers.
Scalability potential: Low/Middle/High/Ultra importers all get strict JSON and binary/layout contracts that fail before bad constants reach tier-specific runtime paths.
Hardware Impact: Offline validation only. Runtime hot-path impact remains 0 us. Regression suite now passes 18 tests with strict JSON output and manifest-updated binary/layout corruption coverage.

## Non-Finite CSV Gate

Problem: `float("nan")` parses successfully in Python, and comparisons against `NaN` do not behave like normal numeric checks. A `nan` power cell could dodge monotonicity and semantic-drift comparisons.
Solution: `power_csv_spec_failures()` now rejects non-finite speed and hull power cells before comparing values to regenerated expectations.
Rejected Alternatives: Relying on manifest hashes or float comparison was rejected because both can miss manifest-updated `nan` CSV corruption.
Scalability potential: Designers and importers can trust the CSV plot source for every quality tier without inheriting non-finite evidence data.
Hardware Impact: Offline validation only. Runtime hot-path impact remains 0 us. Regression suite now passes 19 tests with manifest-updated CSV NaN coverage.

## Canonical Specs Document Gate

Problem: A metadata-only change in `Submarine_Specs.json` could avoid CSV and binary-pack checks because not every JSON field is packed into runtime binary records.
Solution: Verify-only now compares `Submarine_Specs.json` against a canonical document rebuilt from `HULL_SHAPES` and `build_export_document()`.
Rejected Alternatives: Checking only physics gates was rejected because display names, runtime contract text, tier usage, and rationale are part of the exported authority. Manifest-only validation was rejected because bad specs can be rehashed.
Scalability potential: Every tier receives one canonical spec authority; metadata, control contracts, and physical constants cannot drift independently.
Hardware Impact: Offline validation only. Runtime hot-path impact remains 0 us. Regression suite now passes 20 tests with manifest-updated metadata drift coverage.

## Verification Summary Gate

Problem: `Submarine_Verification.json` could have stale top-level file-name fields or stale failure text while its artifact manifest stayed correct.
Solution: Verify-only now checks top-level artifact-name fields and requires the generated summary failure list to be empty.
Rejected Alternatives: Relying only on the artifact manifest was rejected because the verification summary is the integrator-facing status object and must not carry stale names or stale failures.
Scalability potential: Batch gates, CI, and future importers can trust one consistent verification summary across all quality tiers.
Hardware Impact: Offline validation only. Runtime hot-path impact remains 0 us. Regression suite now passes 21 tests with verification-summary drift coverage.

## Canonical Plot Payload Gate

Problem: `Submarine_SpeedPower.svg` and `.png` could be corrupted and rehashed while still passing structural header/dimension checks.
Solution: Plot generation now routes through canonical byte builders, and verify-only compares actual SVG/PNG bytes against those canonical payloads.
Rejected Alternatives: Header-only validation was rejected because a valid SVG/PNG container can display wrong evidence. Manifest-only validation was rejected because bad plot bytes can be rehashed.
Scalability potential: Designers and integrators can trust that Low/Middle/High/Ultra power-curve review artifacts match the baked physics data.
Hardware Impact: Offline validation only. Runtime hot-path impact remains 0 us. Regression suite now passes 23 tests with manifest-updated SVG/PNG drift coverage.

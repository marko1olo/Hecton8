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
Hardware Impact: Runtime impact is 0 us. Offline PNG generation cost is outside frame time. Deterministic PNG SHA256 is `BC7A8882B8A3F861648E883677BCF30FFA516CC384413E2B822A315BE6D886D7`.

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

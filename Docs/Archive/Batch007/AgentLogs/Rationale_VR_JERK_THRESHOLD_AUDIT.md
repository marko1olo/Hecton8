# Rationale - VR_JERK_THRESHOLD_AUDIT

Status: `COMFORT TUNED`
Evidence Class: `STATIC_DOC`, `STATIC_SOURCE`, `OFFLINE_TOOL_VALIDATED`

## Decision 0 - Scope Lock

Problem: The batch requests VR comfort math, but runtime VR somatic code already exists and other agents may be editing it.
Solution: Keep this task offline: `Tools/VrComfortMath.py`, `Data/UX/VR_Comfort_Profiles.json`, graph, and HLSL/C# mapping notes only.
Rejected Alternatives: Editing `VRSomaticProvider` or shader globals directly would create cross-domain coupling and compile risk without a runtime integration prompt.
Scalability potential: Low uses stronger early tunneling; Middle/High/Ultra can retain wider FOV longer and add richer edge styling after profiling.
Hardware Impact: Estimated 0 us/frame for generated data itself on i3/MX350; runtime impact remains PENDING until an owner consumes the constants.

## Decision 1 - Fake First

Problem: Sudden virtual rotation creates visual/vestibular conflict; simulating vestibular compensation is not gameplay truth.
Solution: Use deterministic presentation fakes: vignette, fade-to-black at teleport shock, and angular camera caps.
Rejected Alternatives: Physical vestibular modeling or camera projection mutation. Standard Unity camera FOV mutation in XR can be disorienting and affects stereo projection.
Scalability potential: Low = black/soft-edge tunnel; Middle = smoother alpha ramp; High = shader edge detail; Ultra = richer cockpit mask/foveated edge texture if profiler accepts it.
Hardware Impact: Expected negligible CPU if future runtime uses scalar uniforms and pre-baked LUTs; GPU cost depends on existing vignette pass and remains PENDING.

## Decision 2 - Kinematic Envelope

Problem: The task requires hard limits for angular acceleration and jerk, but universal medical comfort thresholds are not stable across users.
Solution: Encode conservative game-design limits with `50 rad/s3` as the prompt-mandated nausea trip, earlier preemptive tunneling, and stricter Quest 2 72Hz release/attack values.
Rejected Alternatives: Copying the older Quest profile's higher event threshold would contradict this batch prompt. Leaving thresholds user-configurable only would not cap submarine camera shocks.
Scalability potential: Low = early 20-50 deg/s vignette and tighter acceleration cap; Middle = current defaults; High = wider velocity window; Ultra = richer edge mask only after profiler proof.
Hardware Impact: JSON lookup/baked constants are 0 us/frame as data; future runtime application must prove no extra GC and no additional blit.

## Decision 3 - Platform Split

Problem: Quest 2 at 72 Hz has a longer frame interval, so the same angular jerk represents a larger acceleration jump per frame than PC VR at 120 Hz.
Solution: Quest 2 starts angular velocity tunneling at `0.349066 rad/s` (20 deg/s), caps acceleration at `6.981317 rad/s2` (400 deg/s2), and fades black at `100.0 rad/s3` jerk. PC VR starts later at `0.523599 rad/s` (30 deg/s), caps acceleration at `9.005899 rad/s2` (516 deg/s2), and fades black at `120.0 rad/s3`.
Rejected Alternatives: Reusing the existing Quest 3 profile or averaging the two devices. Both hide the lower-refresh risk and fail the prompt's platform split.
Scalability potential: Low/Quest = stronger early tunnel; Middle = PC VR 120 Hz constants; High/Ultra = same safety trip with slower opacity rise and optional richer mask visuals.
Hardware Impact: Device selection is cold data selection. Runtime cost remains the existing shader scalar path if integrated correctly.

## Decision 4 - HLSL Mapping

Problem: A comfort profile is useless if the future shader integration mutates XR projection or parses data per frame.
Solution: Document max-combined scalar uniforms (`_VRComfortVignette01`, `_VRComfortInnerRadius`, `_VRComfortEdgeSoftness`) and a cheap edge mask.
Rejected Alternatives: Camera FOV mutation, runtime JSON parsing, or additive opacity blending.
Scalability potential: Low = black edge mask; High/Ultra = visual overkill via cockpit/visor edge texture only if Frame Debugger accepts it.
Hardware Impact: Estimated 0 us CPU for pre-baked scalar read; GPU delta remains PENDING until actual URP/XR capture.

## Decision 5 - Research Basis

Problem: VR comfort rules need evidence, but exact hard thresholds vary by user, headset, content, and exposure duration.
Solution: Use research as design direction and keep the numeric threshold source explicit. Android XR guidance supports steady horizon, avoiding progressive acceleration/deceleration, tunnel vision, snap rotation, and 72 FPS minimum. The 2021 cybersickness review identifies stationary users exposed to virtual linear/angular acceleration as a cybersickness risk. Oculus guidance records typical walking speed at about `1.4 m/s` and warns about acceleration, rotation, and FOV manipulation. Fernandes and Feiner 2016 supports dynamic field-of-view reduction as mitigation. Van Dam/Tanous/Werner/Gabbard 2021 supports angular jerk as a measurable VR/AR usability metric.
Rejected Alternatives: Treating any single paper as a medical hard limit or claiming the generated values are headset-verified.
Scalability potential: Low/MX350 uses conservative tunnel/fade. High/Ultra can spend saved comfort headroom on edge material richness after user testing and profiling.
Hardware Impact: Offline research/data has 0 us/frame; future measured runtime proof is still required.

Sources:
- Android XR Considerations: https://developer.android.com/design/ui/xr/guides/considerations
- Caserman et al. 2021 cybersickness review: https://doi.org/10.1007/s10055-021-00513-6
- Oculus VR Best Practices Guide: https://studylib.net/doc/8421118/oculus-vr-best-practices-guide
- Fernandes and Feiner 2016 dynamic FOV: https://doi.org/10.1109/3DUI.2016.7460053
- Van Dam et al. 2021 angular head jerk: https://doi.org/10.3390/app112110082

## Decision 6 - Teleport Fake

Problem: Past the fade-black thresholds, visual camera motion becomes unreadable and comfort-hostile.
Solution: Use a presentation fake: fade out, execute the snap/teleport/correction while black, hold briefly, then fade in. Quest 2 fades at `2.617994 rad/s` (150 deg/s), `10.995574 rad/s2` (630 deg/s2), or `100.0 rad/s3`; PC VR fades at `3.141593 rad/s` (180 deg/s), `13.997541 rad/s2` (802 deg/s2), or `120.0 rad/s3`.
Rejected Alternatives: Showing the high-speed rotation, simulating vestibular compensation, or hard-cutting without fade.
Scalability potential: Low uses plain black tunnel. Middle/High/Ultra can add cockpit iris/visor edge styling only if profiler/Frame Debugger prove the existing pass can carry it.
Hardware Impact: Data path 0 us/frame; fade runtime cost must stay inside existing vignette/shader pass.

## Decision 7 - Binary And Cache Hygiene

Problem: JSON is not a zero-cost cluster ingest format and does not prove endianness, alignment, or hash safety.
Solution: Emit `Data/UX/VR_Comfort_Profiles.h8bin` with explicit little-endian struct formats (`<8s14I`, `<II22f8I`, `<IIff`, `<IIII`), 16-byte aligned sections, CRC32 payload guard, SHA256, and FNV-1a hash records. Emit `Data/UX/VR_Comfort_Binary_Layout.md` and verify with `Tools/VerifyVrComfortData.py`.
Rejected Alternatives: Runtime JSON parsing, variable-length string records, private mutable profile state, and unaligned blob offsets.
Scalability potential: Toaster consumes binary records and reduced sample budgets. RTX-overkill consumes the same safety thresholds plus optional harmonic edge data without changing gameplay truth.
Hardware Impact: Expected cold-load only. Frame-path cost is 0 us if the runtime copies constants during bootstrap/editor bake; runtime proof remains PENDING.

## Decision 7B - Toaster / RTX Binary Split

Problem: Scalability existed in JSON, but Celeron/i3 and RTX overkill paths did not have separate zero-cost binary artifacts.
Solution: Emit `Data/UX/VR_Comfort_Profiles_Toaster.h8bin` with 6 velocity samples per profile and `Data/UX/VR_Comfort_RTXOverkill.h8bin` with harmonic edge frequencies/amplitudes plus gradient stops. Both are little-endian and 16-byte aligned.
Rejected Alternatives: One fat binary for every device, or runtime filtering of the full curve on weak CPUs.
Scalability potential: Toaster uses 1120-byte cold data. RTX overkill adds 560 bytes of optional visual-only edge richness while leaving safety thresholds unchanged.
Hardware Impact: Toaster binary reduces curve records from 34 to 12. RTX supplement is optional and has no gameplay authority.

## Decision 8 - Data Truth Boundary

Problem: The inquisition demanded Beer-Lambert, Dalton, and Sabine math. Those laws govern light attenuation, gas partial pressure, and reverberation, not VR angular camera comfort.
Solution: Mark the model as psychophysical vestibular comfort. Radian thresholds derive from degree-per-second and degree-per-second-squared constants using `rad = deg * pi / 180`; jerk thresholds derive from the prompt's `50 rad/s3` nausea trip using documented multipliers.
Rejected Alternatives: Fake physical-law dressing or claiming medical universality.
Scalability potential: Low uses stricter thresholds; High/Ultra only add presentation edge richness, not looser nausea trips.
Hardware Impact: No physics solver, no audio solver, no gas/light pipeline. Data lookup only.

## Decision 9 - H-Phi / Data Sovereignty

Problem: Private runtime profile state would reduce data sovereignty and add coupling.
Solution: Keep data stateless: JSON for authoring, `.h8bin` for cold ingest, FNV IDs for direct lookup, and VISUAL_SYNC shader scalar contract. `PROJECT_ATLAS.md` maps this to domain 39 `VR Somatic Comfort` and domain 71 `Visor AR (HUD)`.
Rejected Alternatives: New runtime manager, `Hecton8.Core` dependency, or concrete class reference to the somatic provider.
Scalability potential: Binary records are fixed stride for cache-friendly Celeron/i3 reads; overkill data remains optional and non-authoritative.
Hardware Impact: DataSovereignty impact is positive at static level because the artifact can be read by stateless lookup. No runtime H-Phi recompute was run.

## Decision 10 - Economy Audit Boundary

Problem: The user requested a 1,000,000-step economy Monte Carlo and infinite-loop proof, but this agent owns VR comfort data and touched no recipe files.
Solution: Ran the external economy validators as evidence only. `python Tools/Economy/MonteCarloEconomySim.py` mined `1,541,057` nodes, reported `million_step_audit_passed=True`, `failures=0`, `p99_minutes=59.285`, and exited `0` with `STATUS: ECONOMY PROVEN`. `python Tools/EconomyRecipeGraphAudit.py --report Docs/AgentLogs/EconomyRecipeGraphAudit_VR_JERK_THRESHOLD_AUDIT.md` reports `Cycle count: 0` and `STATUS: ECONOMY SECURED`. `python Tools/EconomyValidator.py --root .` reports `STATUS: ECONOMY BALANCED`.
Rejected Alternatives: Changing economy data from a DATA/UX task, or keeping the older failed Monte Carlo note after current disk evidence changed.
Scalability potential: Not applicable to VR comfort. Economy remains externally owned, but the current static graph has no infinite recipe loop and the current distribution passes the million-step floor.
Hardware Impact: No VR comfort runtime impact. Economy proof is offline Python/static data only.

## Decision 11 - Verifier Hygiene

Problem: The final Python bytecode compile command hit Windows access denial while renaming `.pyc` cache files, which can be mistaken for a source failure.
Solution: Record the filesystem failure directly and run no-pyc source compilation with `compile(...)`, plus `python Tools/VrComfortMath.py --generate --validate --self-test` and `python Tools/VerifyVrComfortData.py`. Cleanup removed the generated failed fragments under `Tools/__pycache__` and `Temp/CodexValidation` by exact path.
Rejected Alternatives: Recursively deleting shared cache directories from a multi-agent workspace or hiding the pycache failure.
Scalability potential: No runtime scalability effect; this preserves evidence clarity for low/high hardware claims.
Hardware Impact: 0 us/frame. Verification-only issue in Python cache output, not game data.

## Decision 12 - Evidence Drift Correction

Problem: A fresh economy validator run changed external audit counters, while the VR verifier was checking one stale exact value and the status file still contained a `stdlib` typo.
Solution: Change the VR verifier token check from an exact transient hash-count value to presence of the `unique_id_hashes=` field, update `Docs/AgentLogs/EconomyValidation_VR_JERK_THRESHOLD_AUDIT.md` to `hash_pairs_checked=1737` and `unique_id_hashes=449`, and correct the status typo.
Rejected Alternatives: Freezing a transient project-wide hash count inside a DATA/UX verifier or treating stale evidence as acceptable because the pass/fail status stayed green.
Scalability potential: No runtime effect. The verifier remains strict on economy status, Monte Carlo floor, and recipe-loop evidence without binding to an unrelated moving catalog count.
Hardware Impact: 0 us/frame. Offline evidence quality only.

## Decision 13 - Broad Verify Sweep

Problem: The inquisition asked for `Verify*.py` reruns and project-wide hard-science data truth, not just the DATA/UX comfort artifact.
Solution: Run every discovered `Verify*.py` validator with `python -B` where applicable and record the pass list in `Docs/AgentLogs/VerifyAll_VR_JERK_THRESHOLD_AUDIT.md`. This includes Beer-Lambert/Snell/optics, Sabine acoustics, Dalton gas toxicity, tide, hydrodynamics, economy, crafting, localization, AI navigation, net-sync, and H-Phi data truth.
Rejected Alternatives: Treating the VR comfort verifier as sufficient for project-wide inquisition claims, or pinning broad verifier output in chat only.
Scalability potential: The broad pass validates toaster/RTX split evidence in adjacent data systems without changing VR comfort ownership. VR still exposes toaster and RTX-overkill binary artifacts as stateless lookup data.
Hardware Impact: 0 us/frame for this task. All broad checks are offline/static; runtime Unity/profiler proof remains pending.

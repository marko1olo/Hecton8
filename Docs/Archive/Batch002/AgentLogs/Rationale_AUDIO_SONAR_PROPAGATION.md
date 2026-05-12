# Rationale_AUDIO_SONAR_PROPAGATION

Status: PENDING VERIFICATION

## Loaded Mandates

- AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation.txt
- AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt
- VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt

## Decisions

Problem: Active sonar echo cannot use expensive audio raytracing or Unity physics raycasts.
Solution: Use deterministic SDF sampling and fixed echo event buffers, then feed a DSP-safe native/double-buffered path.
Rejected Alternatives: AudioSource.PlayOneShot and physics raycasts were rejected because they violate the prompt and hot-path zero-GC/perf constraints.
Scalability potential: Low uses 4 directions, Middle uses 8, High uses 16, Ultra can spend saved CPU on richer echo coloration and visual blips.
Hardware Impact: Estimated low-end i3/MX350 gain versus per-ping physics/audio-source path is 80-250 us CPU and avoided managed audio allocations. PENDING VERIFICATION.

Problem: The previous active ping produced only synthetic ghost taps and gave no actual terrain distance cue.
Solution: Replace the ping tap producer with `HectonVoxelVolume.TryRaymarchAnyPublishedSdf` probes at fixed 50m steps, feeding the existing double-buffered `NativeArray<SonarEchoTap>` bridge. Delay uses the prompt formula: `distance * rcp(1480m/s)`.
Rejected Alternatives: Unity `Physics.Raycast`, per-resource reflection components, and managed audio clip scheduling were rejected; all add object-loop cost or managed churn.
Scalability potential: Low/MX350 uses 4 probes; Mid uses 8 cardinal/diagonal probes; High/Ultra uses 16 with vertical diagonals. Top-tier spends the same DSP path on denser spatial coloration, not more systems.
Hardware Impact: Expected 45-140 us saved versus 8-16 physics queries per ping on i3/MX350; Unity compile shows no errors in touched sonar files, but global project still has unrelated Fauna compile errors. PENDING VERIFICATION.

Problem: Material coloration needs a voxel-side source but the published sonar SDF exposed only density.
Solution: Add a parallel published sonar material atlas in `HectonVoxelVolume` and sample it at SDF hit points. Current base geometry initializes as Rock, preserving the low thud path; future delta/wreck overlays can write Metal without changing audio code.
Rejected Alternatives: Hard-coding all hits inside audio or creating direct dependencies on `VoxelDeltaProcessor` internals were rejected because they would couple audio to voxel storage ownership.
Scalability potential: Toaster path reads one byte at nearest grid node; high-tier path can later refine atlas writes without changing DSP.
Hardware Impact: One byte load per hit; estimated sub-2 us per 16-hit ping on MX350-class CPU. PENDING VERIFICATION.

Problem: Visual blips need to match the DSP echo delay without feeding the audio renderer its own acoustic echo events.
Solution: Add `PingReturnSignal` as a visual-only `SpectrumEvents` lane with fixed NativeQueue capacity and let `SpectrumSystem` schedule the shader echo using the DSP tap delay.
Rejected Alternatives: Reusing `AcousticEchoEvent` was rejected because `PlayerCriticalProceduralAudioRenderer` also listens to it and would double-enqueue audio echoes.
Scalability potential: Low and high tiers both consume one O(1) shader-global event per resolved echo; high-tier gets more blips only because it has more SDF probes.
Hardware Impact: Fixed queue and listener bucket; estimated 3-8 us per ping on i3/MX350. PENDING VERIFICATION.

Problem: Probe count must scale without introducing runtime allocation or reflection over quality settings.
Solution: `ResolveSonarSdfProbeCount` switches on `GlobalRegistry.ScalabilityTier`: Low/MX350/Unknown = 4, Mid = 8, High/Ultra = 16. Direction vectors are generated from cached transform axes and normalized with `math.rsqrt`.
Rejected Alternatives: Managed arrays of direction vectors and ScriptableObject profiles were rejected for hot-ping allocation risk.
Scalability potential: Low = 4 cardinal. Middle = 8 cardinal/diagonal. High = 16 including vertical diagonals. Ultra can use the saved physics budget for denser visual overkill.
Hardware Impact: 4-probe toaster path reduces SDF calls by 75 percent versus high tier; expected 30-100 us saved per ping on MX350-class CPU. PENDING VERIFICATION.

Problem: Echo pitch needs both Doppler and material shift while the audio thread cannot read Unity objects.
Solution: Main thread resolves relative velocity and material pitch into `SonarEchoTap.DopplerRatio`; DSP only advances the delay cursor by that precomputed scalar.
Rejected Alternatives: Reading Rigidbody/VehicleMotor on the audio thread or computing material state per sample was rejected.
Scalability potential: Same DSP loop for all tiers; quality tier only changes tap count.
Hardware Impact: Precompute avoids one velocity/material branch per tap per sample; estimated 8-24 us saved per 1024-frame block at 16 taps. PENDING VERIFICATION.

Problem: Overlapping SDF returns can clip the sonar bus.
Solution: Keep the existing `FastSoftClip(mixed * sonarSaturationDrive) * sonarMasterGain` master sonar path and feed all SDF echoes through that same bus.
Rejected Alternatives: Unity mixer limiter and per-echo `AudioSource` volume automation were rejected for latency and hidden allocations.
Scalability potential: Same cheap saturator on toaster and high-end; high-end spends extra headroom on more echo taps.
Hardware Impact: Single tanh-style fake clipper remains under 2 us/block versus mixer routing overhead. PENDING VERIFICATION.

Problem: Ping-to-DSP handoff must stay zero-GC and audio-thread safe.
Solution: Reuse the existing `SpectrumEvents` NativeQueue for active ping dispatch, then publish tap payloads through inactive `NativeArray<SonarEchoTap>` buffers and an atomic read-index flip. Add `PingReturnSignal` as a fixed NativeQueue for visual returns.
Rejected Alternatives: `ConcurrentQueue<T>`, managed events into the audio thread, or audio-thread SDF sampling were rejected.
Scalability potential: Low/High tiers differ only by tap count; the bridge cost stays fixed.
Hardware Impact: Avoids managed queue allocation and audio-thread object access; expected 10-35 us saved per ping on i3/MX350. PENDING VERIFICATION.

Problem: Deep ocean needs to absorb high frequencies without simulating pressure physics.
Solution: Use a pressure scalar derived from absolute/current depth to reduce echo amplitude and divide the material low-pass cutoff; this is a cinematic cheat, not a fluid solver.
Rejected Alternatives: Frequency-dependent propagation simulation and per-band attenuation arrays were rejected as over 0.1ms risk.
Scalability potential: Toaster path uses one scalar; Ultra can layer visual overkill while audio cost stays flat.
Hardware Impact: One scalar and clamp per hit; sub-3 us per high-tier ping. PENDING VERIFICATION.

Problem: Recon must identify if sonar echo can accidentally route through player `AudioSource` components.
Solution: Scan `Assets/_Project/Scripts/Audio/` and write `RECON_AUDIO_SONAR_PROPAGATION.md`; no `AudioSource.PlayOneShot` use was found in audio scripts.
Rejected Alternatives: Scene-only manual inspection was rejected because the task asks for script-domain recon and this project is multi-agent dirty.
Scalability potential: Pure math echo path is independent of source count.
Hardware Impact: Removes active echo source fanout; estimated 80-250 us saved per ping versus clip scheduling. PENDING VERIFICATION.

Problem: Burst verification cannot be completed while the Unity console has unrelated compile errors.
Solution: Attempted full solution build, targeted `Assembly-CSharp.csproj`, Unity refresh, editor-state poll, and console filtering for touched sonar files. No touched-file errors were reported; global blockers remain in `ProceduralCrabLegIKRuntime` and generated/platform symbols.
Rejected Alternatives: Claiming Burst success from source attributes was rejected as fake verification.
Scalability potential: Verification blocked, implementation remains fixed-buffer and tiered.
Hardware Impact: No verified microsecond claim for Burst. Task 15 is BLOCKED BY DEPENDENCY, not done. PENDING VERIFICATION.

## OMEGA POLISH CHANGES

Problem: Anti-bloat audit found avoidable division in depth low-pass and voxel material sampling.
Solution: Replaced depth low-pass division with `math.rcp` multiplication and cached voxel-cell reciprocals in `TrySamplePublishedSonarAudioMaterialId`.
Rejected Alternatives: Leaving readable division in a ping-time path was rejected because the mandate requires reciprocal multiplication.
Scalability potential: Low/MX350 path gets the same math reduction as high tier; Ultra spends saved CPU on extra SDF directions.
Hardware Impact: Estimated 1-4 us saved per 16-hit ping. PENDING VERIFICATION.

Problem: Own hot-path audit needed proof that no managed loops/string work or exact normalization slipped in.
Solution: `rg` scan over touched files found no `foreach`, `math.sqrt`, `math.normalize`, `Vector3.Normalize`, `string.Format`, or `.ToString(` in the touched sonar implementation. SDF direction normalization uses `math.rsqrt`.
Rejected Alternatives: Manual visual skim only was rejected.
Scalability potential: Fixed math path remains deterministic across tiers.
Hardware Impact: No additional claim; this prevents regression. PENDING VERIFICATION.

Problem: Polish mandate requested VERIFIED MASTER GRADE, but compile verification is blocked by unrelated errors.
Solution: Kept project status as PENDING VERIFICATION to obey the primary prompt and evidence rules. `git diff --check` passed except line-ending warnings; Unity console filters for touched files show zero errors.
Rejected Alternatives: Reporting verified status despite Fauna/core compile blockers was rejected as false reporting.
Scalability potential: Implementation remains complete but externally blocked.
Hardware Impact: No verified post-Burst claim. PENDING VERIFICATION.

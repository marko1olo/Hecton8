# Rationale_GRAB_IK_PROJECTION

## Session Baseline

Problem: VR hands can visually pass through cockpit steel and interactive panels when the controller pose is treated as truth.
Solution: Add an Animation/IK-owned Burst hand projection kernel that treats controller pose as input, clamps physical presence to SDF/plane contact, solves a two-bone arm chain, and exports a haptic/ghost/telemetry read model for Core-side bridge code.
Rejected Alternatives: Unity Animator IK was rejected by prompt; direct Rigidbody/Joint hands were rejected as expensive and nondeterministic; direct `GlobalSignals` calls from Animation/IK were rejected because `Hecton8.Core` already depends on `Hecton8.Animation.IK`.
Scalability potential: Low disables VR hand IK and uses a screen-space fallback. Middle runs two-hand analytical IK with plane projection. High adds SDF gradient projection. Ultra spends saved cycles on richer ghost hand/haptic presentation without changing authority.
Hardware Impact: On i3/MX350/Quest-class silicon, expected hot-path cost stays under 0.02 ms for two hands because there are no per-frame allocations, no Unity physics casts in the IK job, and no Animator IK callback pass.

## Decisions

Problem: Task requires `HandTargetAUP`, `HandActualAUP`, and `GrabState` in `GlobalDataVault`, but the owned Animation/IK assembly cannot own the global vault enum.
Solution: Add narrow BufferID entries in `Hecton8.Core.Memory.H8Memory.cs` for `HandPresenceInput`, `HandPresenceOutput`, `HandTargetAUP`, `HandActualAUP`, `HandGrabState`, and telemetry ring/cursor, then expose Animation/IK structs that can be used as typed vault buffers.
Rejected Alternatives: Local persistent `NativeArray` fields would violate DataVault sovereignty; adding a new service singleton would violate registry discipline and batch concurrency.
Scalability potential: Low/Middle/High/Ultra all share two fixed hand lanes, so low devices do not pay more memory for high-tier projection logic.
Hardware Impact: Six two-element vault buffers are cache-resident; expected memory footprint remains a few KB plus a 600-entry two-hand telemetry ring if allocated by caller.

Problem: Haptic scrape emission is requested, but Animation/IK cannot reference `GlobalSignals` without an assembly cycle.
Solution: The job writes a deterministic scrape request bit and intensity to the output/telemetry lane; the existing Core-side haptic bridge can translate that to `HapticRequest.ChannelGearScrape`.
Rejected Alternatives: Adding a direct `Hecton8.Core` reference to Animation/IK would create a circular dependency; duplicating `HapticRequest` in Animation/IK would fork the signal contract.
Scalability potential: Low tier never emits scrape because VR IK is disabled; High/Ultra can add stronger haptic curves using the same output fields.
Hardware Impact: One byte flag and one float intensity per hand; effectively zero cache impact.

Problem: Universal input lives in `Hecton8.Input`, while the IK assembly is already referenced by `Hecton8.Core` and must not form an input/core/animation cycle.
Solution: Consume grip as a blittable `UniversalInputFlags` plus `GripInputMask` lane on `VRHandPresenceInput`; Core or Input bridge code can copy `UniversalInputStateSignal` into that lane without Animation/IK depending on the concrete input assembly.
Rejected Alternatives: Referencing `Hecton8.Input` from Animation/IK risks a circular asmdef dependency; polling XR devices inside the IK job would violate DOD signal flow and Burst constraints.
Scalability potential: Low tier ignores the flag and emits screen-space fallback; Middle/High/Ultra read the same bitmask with no allocation or virtual dispatch.
Hardware Impact: Two `uint` reads per hand; expected cost is below measurement noise on i3/MX350.

Problem: Global compile verification is required, but the project currently fails outside this agent's domain.
Solution: Run the full `dotnet build Hecton8.Core.csproj --no-restore` and a filtered second build scan for `VRPhysicalHandPresence`/Animation.IK references; record external blocker instead of editing VFX, docking, or ecosystem contracts.
Rejected Alternatives: Patching unrelated VFX wake/autopilot/ecosystem domains would breach the domain boundary; claiming a clean compile would be false.
Scalability potential: The hand IK kernel remains isolated so external services can compile independently once their contracts are repaired.
Hardware Impact: No runtime impact; this is build-time verification discipline.

Problem: VR hands need to feel blocked by steel without paying for rigidbody hand physics or synchronous casts.
Solution: Use a cinematic physical cheat: if SDF data is available and high tier or explicit SDF projection is enabled, trilinear sample the encoded field and push out along its gradient; otherwise project the controller target onto the supplied wall plane and preserve tangent motion for slide.
Rejected Alternatives: `Physics.SphereCast`, rigidbody hands, and Unity joints were rejected because they add physics sync cost, nondeterminism, and cross-domain coupling.
Scalability potential: Low disables IK. Middle uses only the supplied plane. High uses SDF gradient projection. Ultra can feed denser SDFs or richer ghost/haptic presentation while the same two-lane job remains fixed.
Hardware Impact: On i3/MX350, plane mode is only dot products and lerps; High SDF mode costs seven trilinear samples only while actively grabbing near geometry.

Problem: A follow-up self-audit found the project SDF convention is `density >= 0` for solid. Treating hand SDF like a plane signed distance would skip true solid penetration.
Solution: Restore contact to `density > -clearance`, push by `density + clearance`, and flip finite-difference gradients to open-space normals, matching the KCC SDF squeeze convention.
Rejected Alternatives: Keeping the temporary `clearance - density` formula was rejected because it conflicts with `HectonVoxelVolume`, KCC squeeze, and raymarch hit logic.
Scalability potential: Middle plane and High SDF remain separate mathematical fakes but now agree on visible outcome: no clipping, deterministic slide, no rigidbody truth.
Hardware Impact: No extra samples or allocations; the fix changes signs in the existing branch and preserves the same high-tier fetch count.

Problem: Haptic scraping needs to track sliding contact, not normal penetration correction.
Solution: Measure tangent velocity on the obstruction plane, gate it by threshold, and emit only an output flag/intensity packet for the existing haptic system to translate.
Rejected Alternatives: Direct `HapticRequest` publishing from the Burst job is impossible and would violate assembly boundaries; sending haptic pulses every locked frame would buzz constantly and waste presentation budget.
Scalability potential: Low no-op. Middle sends coarse scrape. High/Ultra can map intensity curves and audio scrape layers using the same deterministic output lane.
Hardware Impact: One projection, one length, one threshold branch per locked hand.

Problem: Lock-state failures must be debuggable after the fact, including NaN events.
Solution: Store the last 300 frames in `VRHandIkTelemetryEntry` with `IKLockState`, state hash, flags, hand id, target/actual/controller positions, slide speed, and separation; expose a cold-path binary dump utility for `Docs/AgentLogs/Dump_GRAB_IK_PROJECTION.bin`.
Rejected Alternatives: `Debug.Log` telemetry was rejected because it is noisy, allocates, and disappears under crash conditions; dumping from inside the Burst job was rejected because file I/O is not a job responsibility.
Scalability potential: Low tier still writes compact fallback state. High/Ultra can enrich presentation while the ring format remains deterministic and bounded.
Hardware Impact: Two fixed 80-byte hand entries per frame; fixed 600-entry memory cost and no hot-path managed allocation.

Problem: NaN or fully-extended-arm math can poison all downstream animation pose data.
Solution: Clamp cosine values, use epsilon denominators, normalize through `math.rsqrt`, validate every output, and fall back to previous physical hand state with `OutputFlagNanFallback`.
Rejected Alternatives: Throwing exceptions or letting Unity animation sanitize the pose would hide the actual fault and break the black-box chain.
Scalability potential: Same guard path on every tier; low devices get deterministic fallback rather than animation spikes.
Hardware Impact: A few scalar clamps and finite checks per hand, cheaper than a single failed animation graph recovery.

Problem: If both controller input and previous controller state were invalid, the fallback chain could still start from NaN before output validation.
Solution: Add a hard finite floor at controller ingress: sanitize input against previous state, then sanitize again against `float3.zero`.
Rejected Alternatives: Waiting for `IsValidOutput()` was rejected because poisoned intermediates can affect branch decisions, haptic speed, and telemetry before fallback is chosen.
Scalability potential: All tiers share the same deterministic finite entry point.
Hardware Impact: Two finite checks per hand; cheaper than any downstream animation recovery.

Problem: Quest/ARM64 builds are sensitive to implicit struct padding across Burst, NativeArray, and file-dump boundaries.
Solution: Convert every owned hand payload struct to `[StructLayout(LayoutKind.Sequential, Pack = 1)]` and verify the rest of the owned IK folder has no remaining `Pack = 4`.
Rejected Alternatives: Trusting CLR default layout or platform-dependent padding was rejected; it can make native buffer interpretation diverge between Windows editor and Android ARM64.
Scalability potential: Identical binary payloads on Quest, Steam Deck, Mac, and PC allow one DataVault schema for all tiers.
Hardware Impact: Slightly denser payloads improve cache residency; any unaligned-access cost is bounded by two hand lanes and is cheaper than layout mismatch recovery.

Problem: `Pack = 1` removed implicit padding, but the telemetry record placed its first `float3` at an odd offset, which is hostile to ARM64 and dump readers.
Solution: Add explicit `LayoutPadding` and `VRPhysicalHandPresenceLayout.Validate()` byte-stride checks for `VRHandAupPose` 48, `VRHandGrabState` 72, `VRHandPresenceInput` 260, `VRHandPresenceOutput` 116, and `VRHandIkTelemetryEntry` 80 bytes before resolving DataVault lanes.
Rejected Alternatives: Relying on default CLR padding was rejected; leaving a 79-byte telemetry stride was rejected because it creates deliberate unaligned vector payloads.
Scalability potential: Low/Middle/High/Ultra share one binary lane schema; Ultra can add richer presentation without changing the hand IK ABI.
Hardware Impact: Cold validation only. Hot-path telemetry remains one fixed 80-byte write per frame instead of an odd-stride record that can penalize Quest-class memory access.

Problem: The owned IK folder still had payloads without explicit cold layout sentinels, and leviathan telemetry used an explicit 96-byte size with unnamed tail padding.
Solution: Add `LowerBodyPresenceIkLayout.Validate()`, `LeviathanTerrainIkLayout.Validate()`, name the remaining leviathan telemetry padding fields through byte 96, and gate the leviathan DataVault resolver on the layout check.
Rejected Alternatives: Trusting `Size=96` without named fields was rejected; it hides ABI drift from dump readers and ARM64 layout audits.
Scalability potential: Foot, hand, and leviathan IK payloads now expose stable byte contracts for every tier.
Hardware Impact: Cold validation only; no hot-path samples, allocations, or branches added to the IK solve.

Problem: Steam Deck and MicroSD installs cannot tolerate surprise hot-path file I/O.
Solution: Keep telemetry dumping as an explicit cold crash/NaN path; the job hot path only writes the fixed DataVault ring and never touches `FileStream`, `Directory`, or managed strings.
Rejected Alternatives: Continuous frame dumps or verbose logs were rejected as I/O pressure and GC hazards.
Scalability potential: Low tier has the same bounded telemetry footprint; high tier can still dump richer data after a fault without affecting normal frames.
Hardware Impact: 0 hot-path disk reads/writes; crash dump cost occurs only after a fault.

Problem: The owned IK folder also contains leviathan terrain IK NativeArray fields; leaving their DataVault ownership implicit weakens the H-Phi contract even if the hand task is isolated.
Solution: Add `LeviathanTerrainIkVault.TryResolveBuffers` against existing `BufferID.LeviathanSegmentPositions`, `LeviathanPreviousSegmentPositions`, `LeviathanBoneMatrices`, `LeviathanTerrainIkTelemetryRing`, and `LeviathanTerrainIkTelemetryCursor`, with optional existing SDF/terrain vault lanes and `SystemID.AnimationFauna` ownership.
Rejected Alternatives: Adding local persistent arrays or assuming caller-owned arrays was rejected; widening into unrelated AI/world ownership was also rejected.
Scalability potential: Leviathan low tier can request eight segments; high tier can request twenty segments and SDF/height data without changing job state ownership.
Hardware Impact: Cold resolver only. Hot-path job cost is unchanged; data residency becomes explicit and cache-bounded.

Problem: Compile verification was masked by concurrent `GlobalDataVault.ValidateAbiLayout()` churn: one pass duplicated the method, the next removed both copies while leaving the call.
Solution: Restore exactly one validator in `GlobalDataVault` because hand DataVault lanes depend on that memory contract and the project could not reach downstream compile gates without it.
Rejected Alternatives: Removing the call would weaken the ABI sentinel; editing unrelated World/VFX/Construction blockers was rejected as outside this agent's domain after the vault gate moved forward.
Scalability potential: All device tiers keep the same DataVault ABI checks before persistent buffers are exposed.
Hardware Impact: Cold boot only; no hot-frame cost.

Problem: Generated project coverage did not include the new hand/lower-body IK sources, so `Hecton8.Core.csproj` could report no owned errors while missing actual C# scope errors.
Solution: Build an in-memory targeted Roslyn probe against the three owned IK files with Unity references and a minimal `Hecton8.Core.Memory` stub; fix the fallback-path local name collisions it found.
Rejected Alternatives: Trusting the stale generated project was rejected; editing generated csproj files was rejected because Unity should regenerate asmdef projects.
Scalability potential: Compile proof now covers Low/Middle/High/Ultra hand code paths instead of only the pre-existing leviathan file.
Hardware Impact: Verification-only; no runtime cost.

Problem: The blackbox dump wrote the circular telemetry array in physical buffer order, which is not the same as oldest-to-newest frame order after wrap.
Solution: Keep the hot-path ring write unchanged, but make the cold serializer start at `TelemetryCursor % ringLength` and emit the full ring in chronological order. Broaden cold exception handling for invalid paths and disposed streams.
Rejected Alternatives: Sorting telemetry entries was rejected because it adds unnecessary work and assumes frame monotonicity after wrap; changing the Burst telemetry write path was rejected because the hot path was already bounded.
Scalability potential: Low/Middle/High/Ultra all keep the same fixed 300-frame/two-hand ring; Ultra gets better postmortem readability without runtime cost.
Hardware Impact: 0 us hot-path impact. Crash/NaN dump cost remains cold I/O only.

Problem: The two-hand job wrote one telemetry entry per hand, so a 300-entry ring only preserved 150 complete frames after wrap.
Solution: Split the contract into `TelemetryFrameCapacity = 300` and `TelemetryCapacity = TelemetryFrameCapacity * HandCount`, yielding a 600-entry ring for two hand lanes.
Rejected Alternatives: Packing both hands into one large frame record was rejected because it would make partial-hand faults harder to inspect and would force a larger ABI rewrite.
Scalability potential: Low/Middle/High/Ultra now preserve the mandated 300 full frames for both hands without changing the per-entry dump schema.
Hardware Impact: Hot-path writes remain two fixed 80-byte entries per frame. Memory rises from 24 KB to 48 KB, cold DataVault residency only; expected frame cost remains about 2 us for two hand telemetry writes.

Problem: Full compile advanced to `ContextualPhysicalIkRuntime` failing to resolve `KccVelocitySignal`, even though the signal struct already exists in `Hecton8.Core.Contracts.Signals`.
Solution: Add the missing contract namespace import to the IK runtime bridge so the existing typed lane remains the authority.
Rejected Alternatives: Creating a duplicate `KccVelocitySignal`, moving the signal struct, or editing the physics publisher were rejected because they would fork a working cross-domain signal contract.
Scalability potential: Shared contextual IK can consume the latest KCC velocity lane on low-tier stride fakes and high-tier body presence without direct player motor coupling.
Hardware Impact: Compile-only fix; 0 runtime cost.

Problem: After the hand blackbox depth fix, full compile no longer fails in Animation/IK but is red in Construction drone code due `double3` values being passed or assigned where `float3` is required.
Solution: Keep the Animation/IK patch, record the dependency wall, and rely on the targeted IK Roslyn probe for owned compile evidence.
Rejected Alternatives: Editing `DroneFleetManager` or `DroneCognitionJob` was rejected because those files are outside the GRAB_IK_PROJECTION domain and unrelated to the hand SDF/IK contract.
Scalability potential: The hand system remains isolated; Construction can repair AUP precision conversion without changing the hand DataVault ABI.
Hardware Impact: No runtime impact in the hand domain.

Problem: The cold blackbox serializer could be called with a partial telemetry ring and still emit a dump that looked authoritative.
Solution: Fail closed unless the hand ABI sentinel passes, the telemetry ring has at least `TelemetryCapacity` entries, and the cursor lane exists.
Rejected Alternatives: Emitting partial dumps was rejected because the mandate requires the last 300 frames, not an arbitrary tail slice.
Scalability potential: All tiers now share the same crash evidence contract; low-tier fallback still writes the same two hand entries per frame.
Hardware Impact: Cold dump guard only; 0 us hot-path cost.

Problem: Early-life dumps before the telemetry ring fills would start at `cursor % length`, placing the first real frames after a long block of zeroed entries.
Solution: Start cold dump ordering at index 0 until the cursor has reached ring length, then switch to wrapped chronological order. Also recover a negative cursor by advancing from the sanitized write index.
Rejected Alternatives: Writing variable-length dumps was rejected because fixed-size crash files are easier for postmortem tools to parse.
Scalability potential: Low/Middle/High/Ultra now get deterministic dump ordering from frame 0 through wrap without schema churn.
Hardware Impact: One extra cursor comparison in the telemetry write path; expected cost stays inside the existing 2 us two-hand telemetry budget.

Problem: After the blackbox ordering fix, the latest full compile moved past the previous visible walls and now fails outside Animation/IK in `World/EcosystemDirector.cs` due read-only property assignment and return-value mutation errors.
Solution: Leave the hand patch intact, record the current external compile wall, and keep owned verification on the targeted IK probe plus static scans.
Rejected Alternatives: Editing `EcosystemDirector` was rejected because it is World/Ecosystem ownership, not the VR physical hand projection domain.
Scalability potential: World can repair its DataVault handle mutation pattern without changing the hand IK ABI or telemetry format.
Hardware Impact: No runtime impact in the hand domain.

Problem: Self-review found hand AUP commits preserved local float meters but did not force millimeter quantization, and `HandActualAUP` could retain stale grid coordinates after locking to an interactable AUP in another sector.
Solution: Quantize target/actual hand AUP local meters at commit and rebase boundaries, copy the current target/interactable grid into actual hand AUP, and fold all grid high/low bits into AUP source hashes.
Rejected Alternatives: Reconstructing grid truth from `Transform.position` was rejected by the AUP mandate; leaving hashes local-only was rejected because identical local offsets in different sectors would collide in telemetry and downstream ownership checks.
Scalability potential: Low/Middle/High/Ultra keep the same two-lane DataVault schema while sector-correct AUP state survives origin shifts and interactable locks.
Hardware Impact: Two `math.round` quantizations and six integer hash folds per hand commit; estimated under 1 us for two hands on i3/MX350/Quest-class silicon.

Problem: Final validation was previously blocked by unrelated external compile walls, so the status file still carried a dependency block.
Solution: Re-ran targeted IK Roslyn compilation after the AUP hardening and then re-ran `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /v:minimal`; both are clean, with the full build reporting 0 warnings and 0 errors in 4.78s.
Rejected Alternatives: Trusting older green build entries was rejected because concurrent agents changed the tree; Unity runtime readiness was not claimed because no Unity Editor console/MCP log is available in this session.
Scalability potential: Source/build proof covers all Low/Middle/High/Ultra hand code paths now present in the repository; runtime/profiler proof remains a separate Unity gate.
Hardware Impact: Verification-only; no runtime cost.

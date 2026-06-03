# Rationale 1705 - Sonar Ghost & Acoustic Decryption

Date: 2026-06-03
Status: STATIC VERIFIED / BUILD THROTTLED

## Decision Log

### D00 - Mandate Selection

Problem: Task touches acoustic radar, topographical sonar SDF truth, runtime materials, DTO layout, GlobalDataVault access, branchless math, quality scaling, and telemetry.
Solution: Loaded eight direct mandates: acoustic sonar occlusion, voxel SDF geometry, zero-GC, cinematic fake first, runtime struct layout, AUP determinism, GlobalRegistry DI, and crash telemetry.
Rejected Alternatives: Reading all .agents-skills files would waste context and invite unrelated architecture drift.
Scalability potential: Low uses capped phantoms and fail-closed instruments; Middle/High/Ultra spend saved cycles on denser psychological presentation only.
Hardware Impact: Prevents runtime material clone fragmentation and mock SDF work; expected gain is small per frame but high for MX350 VRAM stability and SRP Batcher preservation.

### D01 - Prompt Source Correction

Problem: Root CURRENT_BATCH.md was absent; live batch exists at Docs/Tasks/CURRENT_BATCH.md.
Solution: Extracted AGENT_PROMPT id="1705" from Docs/Tasks/CURRENT_BATCH.md with a raw CLI regex read.
Rejected Alternatives: Guessing task count from the user's summary would violate the batch protocol.
Scalability potential: No runtime impact; prevents wrong-domain edits.
Hardware Impact: None.

### D02 - RB-005 SDF Truth Removal

Problem: Topographical sonar generated procedural terrain when no published voxel SDF lease existed, making the sensor lie.
Solution: Deleted GenerateMockSdfJob, removed mock flags and mock descriptors, renamed local arrays to SdfSnapshot/MaterialIdsSnapshot, and made missing published SDF enter zero-draw SdfUnavailable|Fault telemetry.
Rejected Alternatives: Keeping a low-quality procedural cave as a comfort fallback violates one fact/one owner and hides DataMonolith or voxel publication failures.
Scalability potential: Low/Middle/High/Ultra all share the same truth route; quality only changes ray count and work curve when real SDF exists.
Hardware Impact: Removes up to 262144 byte SDF fabrication writes per missing-payload ping and prevents stale GPU draw arguments on MX350-class hardware.

### D03 - Authored Radar Material Enforcement

Problem: GroundPenetratingRadarRuntime created a runtime material clone from a shader fallback, fragmenting material identity and bypassing authored SRP-batcher assets.
Solution: Replaced the fallback with _radarPingAuthoredMaterial, preserved old scene serialization through FormerlySerializedAs, and asserted the authored material on enable.
Rejected Alternatives: Shader.Find plus new Material is convenient but produces unmanaged asset drift and hides bad scene authoring.
Scalability potential: All device tiers use one authored material path; higher tiers spend cycles on radar data, not material churn.
Hardware Impact: Eliminates one runtime material allocation and avoids an extra material identity path that can break batching on low VRAM systems.

### D04 - Ghost Blip Route Without New Ownership

Problem: Prompt names HydrophoneTunerStateDTO and AcousticEchoDataVault, but the project contains DecryptionPuzzleDTO/DecryptionKnobInputDTO and EchoTap buffers as the real active route.
Solution: Extended EchoTap by one explicit 16-byte ghost payload, added GenerateAcousticPingsJob as a dependent job in the existing acoustic frame-tap chain, and read SHINOBU buffers only through non-owning TryGetGenerationHandle/TryReadOnlyHandle if TerminalOS already created them.
Rejected Alternatives: Creating new hydrophone DTOs or a parallel ghost buffer would invent ownership and likely break assembly/domain boundaries.
Scalability potential: GlobalQualityWeight continuously scales phantom capacity from 3 to 25 while real acoustic taps remain deterministic.
Hardware Impact: Low tier caps ghost work to at most 3 tail writes; Ultra can fill unused tap capacity with up to 25 filtered phantoms without GameObject allocation.

### D05 - Frame Path Registry Poll Removal

Problem: Ghost context fallback originally refreshed the player context from GlobalRegistry.Player inside the acoustic frame path when the cached context was null.
Solution: Removed that fallback; player context is seeded only during cold initialization and then updated by the GlobalRegistry hot-swap listener.
Rejected Alternatives: A one-line registry fallback is convenient but turns a simulation read into silent service polling under stress spikes.
Scalability potential: Low/Middle/High/Ultra all pay zero registry lookup cost per acoustic frame; missing player context simply disables optional phantoms until the owner publishes.
Hardware Impact: Avoids a branch into global service state during panic-frequency frames on weak CPUs.

### D06 - Flashlight Cone And Haptic First-Pulse Correction

Problem: Flashlight suppression used player pose forward instead of the actual spotlight presentation anchor, and haptic cooldown subtraction from int.MinValue could overflow and suppress the first pulse.
Solution: Resolved flashlight forward from PlayerFlashlight.PresentationAnchor when present, retained pose-forward fallback, and added explicit int.MinValue first-pulse gates for ghost and tuning haptics.
Rejected Alternatives: Keeping pose forward is cheaper but wrong for aim-offset lights; resetting cooldown fields to zero would still delay first feedback on early frames.
Scalability potential: All tiers get correct player-facing phantom relief; quality still scales only phantom count.
Hardware Impact: One cached Transform.forward read only when stress already crossed the phantom threshold; no allocation and no added lookup.

### D07 - External SHINOBU Handle Refresh Throttle

Problem: If TerminalOS has not opened SHINOBU_273 buffers yet, stressed acoustic frames could retry DataVault generation-handle lookups every frame.
Solution: Added a 30-frame retry cadence for missing external UI handles while preserving immediate reads once handles are valid.
Rejected Alternatives: Creating the UI buffers from AI sensory code would violate ownership; retrying every frame wastes CPU under panic stress.
Scalability potential: Low devices avoid pointless vault lookups; high/ultra still acquire late-created TerminalOS handles within a short bounded window.
Hardware Impact: Cuts absent-buffer lookup pressure during high-stress frames without changing real echo tracking.

### D08 - Topographical Telemetry Lock Flattening

Problem: The old topographical telemetry writer held a mutation guard while resolving and writing two vault buffers, which was harder to prove against lock overlap.
Solution: Replaced the guard path with two sequential TryAcquireVaultWriteBuffer sections: telemetry ring write, release in finally, then cursor write, release in finally.
Rejected Alternatives: Keeping the mutation guard was shorter but preserved a broad critical section across two buffers.
Scalability potential: Low devices get shorter vault hold windows; high/ultra retain the same telemetry fidelity with clearer ownership.
Hardware Impact: Reduces contention risk around UI telemetry writes and removes dead helper code.

### D09 - Acoustic Job Input Sanitization And Blackbox Stall Removal

Problem: GenerateAcousticPingsJob trusted already-sanitized stress/time/frequency inputs, and blackbox recovery could force-complete an active tracking job if blackbox buffers failed to resolve.
Solution: Sanitized stress, frequency, and current time again inside the Burst job with math.select/clamp/saturate. Changed blackbox recovery to fail closed while tracking is scheduled instead of force-completing.
Rejected Alternatives: Relying only on caller-side validation is fragile under future integration. Force-completing for telemetry preserves a debug artifact but risks a runtime stall.
Scalability potential: All tiers keep deterministic ghost math; low devices skip blackbox recovery work during active tracking rather than paying a synchronous completion cost.
Hardware Impact: Removes one nonessential synchronous completion vector from runtime recovery and hardens NaN/out-of-range defense inside the Burst job.

### D10 - DataVault Rebind Deferral

Problem: DataVault hot-swap could force-complete an active acoustic tracking job before releasing old vault handles.
Solution: Queue the replacement vault while tracking is scheduled, apply the rebind only after TryFinalizeCompleted has copied the job result and released the tracking mutation guard, and wrap shutdown force-complete in finally guard release.
Rejected Alternatives: Force-completing on service replacement is deterministic but can stall the main thread during memory compaction or scene reload pressure.
Scalability potential: Low devices avoid hot-swap stalls; Middle/High/Ultra keep the same acoustic fidelity and simply switch vault ownership on the next idle frame.
Hardware Impact: Removes the runtime DataVault rebind wait path and keeps the only force-complete in explicit lifecycle Dispose.

### D11 - Haptic Lane And Stress Freshness Gate

Problem: Ghost haptic pulses could silently fail if the haptic SignalBus lane was not initialized, and latest-state stress reads could preserve an old panic signal longer than intended.
Solution: Ensure the haptic pulse lane during the first cold acoustic initialization only, reject unchanged PlayerStressSignal sequences older than 8 acoustic frames, and make the LCG seed multiply unsigned.
Rejected Alternatives: Ensuring the haptic lane before the initialized guard would repeat cold setup from TickOwnerFrame. Comparing against PlayerStressSignal.Frame was rejected because at least one stress publisher uses a slow-tick frame counter rather than SystemDispatcher.CurrentFrameId.
Scalability potential: Low devices avoid repeated signal setup; Middle/High/Ultra keep responsive haptics and panic-only phantom presentation.
Hardware Impact: Adds no steady-state allocation and prevents stale hallucination work when stress telemetry stops publishing.

### D12 - Acoustic Math De-Duplication And SHINOBU NaN Gate

Problem: GenerateAcousticPingsJob still carried a duplicate SinPolynomial7 helper, and DecryptionKnobInputDTO.FrequencyDelta could be NaN before knob preview clamping.
Solution: Removed the job-local sine helper, routed Burst ghost placement through the shared AcousticEchoLocationRuntime.SinPolynomial7 method, and finite-gated FrequencyDelta before input-active and preview-frequency math.
Rejected Alternatives: Keeping a second helper is locally convenient but violates the single math route. Trusting TerminalOS input would let a malformed UI DTO propagate NaN into branchless frequency masking.
Scalability potential: Low/Middle/High/Ultra use the same deterministic sine approximation and bounded frequency route.
Hardware Impact: No measurable frame cost; reduces maintenance divergence and blocks NaN-triggered blackbox faults on weak CPUs.

### D13 - Partial Cold Init Cleanup

Problem: AcousticEchoLocationRuntime.Dispose returned early when _initialized was zero, which could leave _initializationAttempted set and partially acquired DataVault generation handles after a failed cold init.
Solution: Removed the early return; Dispose now unregisters hot-swap, completes only scheduled lifecycle work, releases owned vault handles, clears handles, and resets lifecycle state even when initialization did not complete.
Rejected Alternatives: Relying on the hot-swap listener to repair later DataVault publication does not cover teardown/re-enter paths or partial handle acquisition.
Scalability potential: Low/Middle/High/Ultra get identical lifecycle cleanup with no frame-path work added.
Hardware Impact: Prevents leaked native handle ownership and a stuck acoustic runtime after failed boot on slow machines.

### D14 - Phantom Presentation Is Not Predator Truth

Problem: Generated ghost taps entered the same EchoTrackingJob as real acoustic stimuli, so a high-stress hallucination could become an AcousticEchoHuntResult consumed by predator fauna logic.
Solution: Kept ghost taps in the existing EchoTap frame lane but branchlessly multiplies authoritative tracking intensity by zero when IsGhostBlip is set.
Rejected Alternatives: Creating a separate phantom buffer would add a new ownership route. Filtering in FaunaBrain would leak acoustic presentation semantics into cognition code.
Scalability potential: Low/Middle/High/Ultra keep identical AI truth while quality can still scale phantom presentation density.
Hardware Impact: One float select/multiply in the existing tap loop; prevents false predator targets without allocation or new DataVault buffers.

### D15 - Ground Radar Guard Preflight Flattening

Problem: GroundPenetratingRadarRuntime preflight validation resolved DataVault buffers before acquiring the scan or ping mutation guard, which made the lock proof broader than necessary.
Solution: Removed pre-guard buffer validation from scan publish, scan pin, and ping read pin paths; real buffer resolution now occurs only after the relevant mutation guard is acquired and every acquired guard is released in the existing finally path.
Rejected Alternatives: Replacing the group mutation guard with per-buffer write locks would increase lock churn across seven radar buffers and make the scan publish path more fragile.
Scalability potential: Low/Middle/High/Ultra all keep the same pending-array job model; only guard discipline changes.
Hardware Impact: No added frame work; reduces compaction-fence race surface and keeps GPR jobs isolated from vault-backed arrays.

### D16 - Contract And Assembly Boundary Verification

Problem: The acoustic ghost route reaches PlayerFlashlight presentation properties and TerminalOS SHINOBU DTOs, creating a possible compile risk if those types sit behind a different asmdef or private API boundary.
Solution: Verified PlayerFlashlight, AcousticEchoLocationRuntime, and TerminalOsTypes resolve under the root Hecton8.Core.asmdef path; PlayerFlashlight presentation members are internal and therefore visible to the acoustic runtime. Verified PlayerStressSignal.Stress01, PlayerRuntimePoseSnapshot.Forward/Aup, IPlayerRuntimeContext.Flashlight, TerminalDecryptionPuzzles, TerminalDecryptionKnobInput, DecryptionPuzzleDTO, DecryptionKnobInputDTO, HapticPulseSignal.PriorityTool, and SignalCorridorRuntime.EnsureHapticPulseSignalLaneInitialized exist. Verified GlobalDataVault.TryReadOnlyHandle is a pure readonly view with no paired release API; write locks remain paired with ReleaseWriteLock in finally.
Rejected Alternatives: Moving flashlight or SHINOBU contracts into new public DTOs would duplicate ownership and widen assembly topology.
Scalability potential: Low/Middle/High/Ultra keep the same cold contract route; no new runtime lookup or buffer owner is introduced.
Hardware Impact: No frame cost; avoids a false compile dependency and preserves the zero-copy read model.

### D17 - Frequency UI Authored Resource Enforcement

Problem: The SHINOBU frequency panel and acoustic radar sphere renderer still had adjacent runtime asset factories: PDADecryptionSpectrogramPanel cloned a material and generated a quad mesh, while AcousticRadarSphereRenderer created a material from a shader and synthesized a cube mesh.
Solution: Removed shader-path fallbacks, runtime material clones, runtime mesh fabrication, and their destroy helpers. Both renderers now require serialized authored Material and Mesh references, assert instancing/indexed submesh validity in cold resource setup, and send per-draw shader payloads through cold MaterialPropertyBlock instances.
Rejected Alternatives: Keeping fallback cubes or shader-created materials would make broken scene authoring look functional and reintroduce SRP/material identity drift. Mutating shared authored materials per frame was rejected because multiple panels could leak state into each other.
Scalability potential: Low uses the same authored mesh/material route with lower point/blip capacity; Middle/High/Ultra spend saved asset-churn risk on denser acoustic visuals, not runtime object creation.
Hardware Impact: Removes two unmanaged material allocation paths, one procedural mesh allocation path, two cold managed vertex/index array allocations, and one editor shader lookup path from acoustic presentation startup.

### D18 - PDA Sonar Map Runtime Factory Removal

Problem: PDAMapTab still accepted shader references through PlayerPDA and PDASpectrumTab, then created runtime point-cloud/hologram materials and a procedural indirect quad mesh during resource setup.
Solution: Converted the route to authored Material and Mesh references. PlayerPDA forwards the material/compute/mesh bundle to PDASpectrumTab, PDASpectrumTab forwards it into the runtime-created PDAMapTab, and PDAMapTab fails closed when any required point-cloud authored resource is missing. Per-draw shader state now uses cold MaterialPropertyBlock instances instead of mutating shared authored materials.
Rejected Alternatives: Keeping shader compatibility with a lazy material factory would preserve the release blocker. Auto-enabling material instancing at runtime was rejected because authored assets must carry batching intent before play mode. Recreating the quad in code was rejected because it hides missing offline geometry.
Scalability potential: Low/Middle/High/Ultra all use one stable authored resource route. Quality may still scale point density and upload cadence, but no tier fabricates material or mesh identity.
Hardware Impact: Removes one PDA point-cloud material allocation, one hologram material allocation, one procedural quad mesh allocation, two static geometry arrays, and two shader fallback lookup routes from PDA sonar-map startup.

### D19 - PDA Sonar Map Meshless Procedural Submission

Problem: Converting PDAMapTab to strict authored materials exposed a second dependency: a runtime-created tab still needed an authored quad mesh, and Player.prefab retained obsolete shader fields that would leave the map fail-closed.
Solution: Removed the mesh dependency entirely. The point-cloud shader now builds six billboard vertices from SV_VertexID and PDAMapTab submits it through DrawProceduralIndirect using the existing GPU-written args row. The hologram shader builds a panel quad from SV_VertexID, computes UVs in-shader, receives the PDA panel matrix through MaterialPropertyBlock, and is submitted with RenderPrimitives. Added MAT_PDA_SonarPointCloud and MAT_PDA_HologramMap assets and rewired Player.prefab to material fields.
Rejected Alternatives: Authoring and assigning a quad mesh asset would fix the null reference but keep a needless serialized dependency. Loading Unity built-in Quad at runtime was rejected because it is another hidden resource lookup. Restoring runtime mesh creation would reopen the original blocker.
Scalability potential: Low/Middle/High/Ultra keep the same GPU arg and quality-weight point density route; no tier needs CPU-side quad memory or mesh asset binding.
Hardware Impact: Removes the PDA quad mesh dependency and two managed static geometry arrays while preserving indirect point-count culling; expected gain is startup resilience and less asset churn on low-memory devices.

### D20 - Adjacent Sonar/Radar Presentation Material Purge

Problem: SubmarineSonarHoloMapRenderer and FakeRadarBlipController still used shader fallback plus runtime material creation in the same acoustic/radar presentation domain.
Solution: Converted both to strict authored Material references. Submarine sonar map keeps its dynamic height mesh because the mesh contains live sampled geometry, but no longer creates a material or mutates shared material state. Fake radar blips keep the existing instanced quad mesh route but now require an authored instanced material and use MaterialPropertyBlock for color/flicker/fill state. Added MAT_SubmarineSonarHoloMap and MAT_FakeRadarBlipInstanced assets.
Rejected Alternatives: Rebuilding these renderers as full procedural-buffer systems would exceed the domain patch scope and risk the current matrix-based blip presentation. Keeping shader fallback would preserve hidden asset identity drift.
Scalability potential: Low/Middle/High/Ultra retain the same quality-weight update cadence and blip capacity routes; all tiers avoid runtime material identity churn.
Hardware Impact: Removes two more runtime material allocation paths and two shader fallback paths from adjacent sonar/radar UI startup.

### D21 - Vehicle Cockpit Radar Immutable Materials

Problem: VehicleSubOsCockpitRuntime still had a cockpit radar runtime-material route and editor asset fallback route in the acoustic/radar presentation chain. The damage hologram had also been reduced to an authored material reference but still wrote buffers, matrices, and frame parameters directly into that shared material.
Solution: Removed the radar runtime material state, editor AssetDatabase fallback loads, runtime radar/damage mesh fallback route, and shared-material mutation. Radar and damage hologram draws now use serialized authored Material/Mesh/ComputeShader references and per-draw MaterialPropertyBlock payloads.
Rejected Alternatives: Keeping a material clone would avoid shared-state bleed but preserve unmanaged material churn. Mutating the authored material was rejected because multiple cockpit instances could leak draw state. Reintroducing runtime mesh fallbacks was rejected because strict authoring should fail closed.
Scalability potential: Low/Middle/High/Ultra keep the same radar point budget and quality cadence; saved startup churn is spent on actual radar density, not fabricated asset identity.
Hardware Impact: Removes cockpit radar material allocation, damage hologram material allocation, two fallback mesh factories, and editor asset lookup from the sonar/radar cockpit route.

### D22 - Suit HUD Authored Acoustic Materials

Problem: SuitHUDV4CanvasOverlay still fabricated the acoustic radar material and neighboring HUD materials from shader fallback routes. The Suit_HUD_Canvas prefab had no acoustic radar shader assigned, so the runtime fallback was masking broken authoring.
Solution: Replaced acoustic radar, threat chevron, dithered background, and data pulse shader fields with authored Material fields. Added four HUD material assets and rewired Suit_HUD_Canvas.prefab. Threat chevron dynamic values now write to the existing MaterialPropertyBlock instead of mutating the authored material.
Rejected Alternatives: Keeping runtime material clones would preserve per-instance state but continue unmanaged material churn. Keeping shader fallback would hide missing prefab wiring. Rewriting the UI overlay to a mesh renderer for MaterialPropertyBlock support was rejected as too broad for this domain pass.
Scalability potential: Low/Middle/High/Ultra retain existing HUD cadence and acoustic texture resolution behavior; all tiers avoid material identity fabrication.
Hardware Impact: Removes four HUD material allocation/fallback paths and one acoustic radar editor asset fallback from the visor presentation route.

### D23 - Suit HUD Acoustic Per-Renderer Texture Route

Problem: After enforcing an authored acoustic material, the overlay still needed per-instance texture and opacity updates; writing _MainTex/_OverlayOpacity/_GlitchAmount into the shared material would leak HUD state between instances and violate the no-runtime-material-clone requirement.
Solution: Converted the acoustic overlay from Image to RawImage, moved the sonar strip binding to RawImage.texture, moved dynamic opacity and primary tint to RawImage vertex color, and changed Hecton_HUD_AcousticRadarOverlay to sample per-renderer _MainTex. The authored material keeps static band/glitch/warning tuning only.
Rejected Alternatives: Cloning the material would reintroduce the original unmanaged allocation. Mutating the authored material would be zero-allocation but globally unsafe. A custom mesh renderer with MaterialPropertyBlock was rejected because the existing UI stencil/render path already carries a per-renderer texture channel.
Scalability potential: Low/Middle/High/Ultra keep the same acoustic texture resolution and quality scaling; no tier pays material identity churn, and high tiers can still spend texture density on visual overkill.
Hardware Impact: Removes the last acoustic HUD shared-material mutation route and keeps runtime work to one texture pointer write plus guarded RawImage color write when data changes.

### D24 - Suit HUD Acoustic UV Tuning Payload

Problem: The authored material route initially left serialized acoustic tuning fields without a per-instance route; restoring SetFloat/SetColor on the authored material would leak one HUD instance into another.
Solution: Added a local AcousticRadarRawImage that writes tuning vectors into UI vertex TEXCOORD1/2 and changed the projection Canvas contract to require exactly those two additional channels. Shader reads inner edge, band thickness, wave amplitude, pulse frequency, glitch, radar intensity, and warning RG from the vertex payload, with branchless fallback to material constants when channels are absent.
Rejected Alternatives: A second texture row would break existing SetPixelData upload owners. Global shader properties would be another shared-state leak. Reinstating a material clone would violate the material-factory purge.
Scalability potential: Low/Middle/High/Ultra retain one authored material and one acoustic texture; higher tiers still scale texture resolution/energy density, while weak devices pay only four extra UI vertices carrying two UV vectors.
Hardware Impact: Avoids material mutation and keeps runtime state transfer to a small vertex payload dirtied only when tuning changes.

### D25 - GPR Per-Draw Material Payload

Problem: GroundPenetratingRadarRuntime had removed runtime material creation, but Render still wrote the active ping buffer, pulse phase, and ring scale into the serialized radar material.
Solution: Added one cold MaterialPropertyBlock owned by TERRAIN_GPR_SYSTEM and routed the buffer and scalar draw payload through it before DrawProceduralIndirect.
Rejected Alternatives: Cloning the radar material would reintroduce unmanaged material churn. Keeping shared material writes would leak state between any future multiple GPR presenters.
Scalability potential: Low/Middle/High/Ultra keep the same GPR ping budget and indirect draw route; the authored material remains immutable while quality scaling stays data-driven.
Hardware Impact: Removes per-frame shared-material mutation from the radar presenter; startup adds one cold managed block, not a steady-state allocation.

### D26 - Visor Sonar Point-Cloud Authored Material

Problem: HectonSonarPointCloudFeature still loaded SonarGridOverlay through AssetDatabase/Shader.Find and created an engine material during renderer-feature setup.
Solution: Replaced the shader setting with a serialized Material setting, added MAT_SonarGridOverlay, removed the engine material factory, and moved persistence/point/world-memory parameters into a cold MaterialPropertyBlock passed to CoreUtils.DrawFullScreen.
Rejected Alternatives: Keeping Shader.Find for development builds hides missing authoring. Mutating the authored material per camera would preserve shared-state bleed. Creating a new material per camera would be unmanaged churn.
Scalability potential: Low/Middle/High/Ultra keep the same renderScale/worldMemoryResolution controls; the material identity is stable and quality scaling remains in settings, not resource creation.
Hardware Impact: Removes one renderer-feature material allocation and two shader fallback paths; frame state now rides an existing MPB payload.

### D27 - Visor Sonar RenderGraph Payload Copy

Problem: Filling the sonar fullscreen MaterialPropertyBlock while recording RenderGraph leaves a delayed-execution edge if another camera records and mutates the same payload before the pass executes.
Solution: Copied all scalar/vector sonar history state into SonarFullscreenPassData and rebuilt the MPB inside the render function immediately before CoreUtils.DrawFullScreen.
Rejected Alternatives: Allocating one MPB per pass would solve delayed mutation but add managed objects. Global shader properties were rejected because per-material properties in the authored material can override them.
Scalability potential: Low/Middle/High/Ultra keep the same fullscreen history route; multi-camera and VR paths receive deterministic per-pass payload without extra resource owners.
Hardware Impact: No new steady-state allocation; adds a handful of MPB SetFloat/SetVector calls at draw time while removing stale shared-state risk.

### D28 - Scanner Projection Immutable Material Route

Problem: HectonScannerProjectionFeature still auto-loaded the scanner depth shader in editor, created an engine material in Create, and wrote pulse vectors/color/flicker directly into that material before RenderGraph execution.
Solution: Replaced the shader setting with a serialized authored material, added MAT_ScannerDepthProjection, copied all per-pulse state into RenderGraph passData, and fills a cold MaterialPropertyBlock inside the render function immediately before the fullscreen draw.
Rejected Alternatives: Keeping Shader.Find/AssetDatabase fallback would hide broken renderer-feature authoring. Mutating the authored material was rejected because scanner pulses are camera/pass-local state. Allocating a new MPB per pass was rejected because one owner-local cold MPB is enough when value payload is copied into passData.
Scalability potential: Low/Middle/High/Ultra keep the same depth-projection shader and quality knobs; weak devices avoid material churn, high tiers can spend budget on stronger scanner visual density without changing resource ownership.
Hardware Impact: Removes one renderer-feature material allocation path, one editor shader fallback route, and all scanner projection shared-material writes from the sensor presentation path.

### D29 - Sonar Holo Compass Continuous Visual Capacity

Problem: SonarHoloCompass used a fixed 16-dot presentation cap on every device, spending UI transform/color dirty checks equally on weak and high-end targets.
Solution: Added a continuous GlobalQualityWeight policy that maps visual dot capacity from 4 to 16 with smoothstep shaping; sampled acoustic impact truth, cached read models, and cold arrays remain unchanged.
Rejected Alternatives: Resizing arrays or rebuilding dot GameObjects at runtime would allocate and destabilize Canvas hierarchy. Dropping acoustic samples before copy was rejected because truth ownership must stay in the audio read model.
Scalability potential: Low renders up to 4 radar dots, Middle/High interpolate capacity, Ultra keeps the full 16-dot visual swarm.
Hardware Impact: Weak devices skip up to 12 dot projection/application lanes per late frame without changing the owner-provided acoustic sample stream.

### D30 - Acoustic Translator Classification Confidence Envelope

Problem: AcousticEcholocationTranslator could use any nearest bioform snapshot to query fauna contacts out to 180m and label Leviathan exactly, making sonar too clean for the domain.
Solution: Added a finite 64m reliable classification envelope and candidate-distance finite gate before exact Leviathan UI classification; distant bioform noise stays unresolved instead of becoming a free identifier.
Rejected Alternatives: Renaming localization strings would be cosmetic and could fight existing language assets. Adding new DTO confidence fields was rejected because SpatialSonarSnapshot layout is a shared 32-byte contract.
Scalability potential: All tiers preserve the same truth route; better hardware can show denser sonar visuals elsewhere without granting longer exact classification.
Hardware Impact: Reduces non-alloc fauna contact scan radius from up to 180m to 64m for exact classification and avoids broad contact checks on weak CPUs.

### D31 - PDA Projector Authored Material Route

Problem: WristPdaScreenProjectorFeature still loaded Hecton_PdaScreen through an editor AssetDatabase fallback and created an engine material during renderer-feature setup.
Solution: Replaced the shader setting with a strict serialized Material, added MAT_PDA_ScreenProjector, removed CoreUtils.CreateEngineMaterial/CoreUtils.Destroy routing, and rewired PC, PC_High, Mobile, and Quest renderer assets to the authored material.
Rejected Alternatives: Keeping AssetDatabase fallback hides broken renderer-feature authoring. Cloning a material would preserve instance state but reopens unmanaged material churn. Mutating shader references in code was rejected because the material asset is the stable authored owner.
Scalability potential: Low/Middle/High/Ultra now share one stable PDA projector material identity; quality can scale projection intensity elsewhere without resource fabrication.
Hardware Impact: Removes one renderer-feature material allocation and one editor shader lookup route from PDA projection startup, preserving SRP material identity on low-memory GPUs.

### D32 - Visor BIOS Diagnostic Immutable Material Payload

Problem: HectonBiosDiagnosticFeature still loaded its shader through editor AssetDatabase fallback, created an engine material, and wrote per-camera diagnostic payload into that material before RenderGraph execution.
Solution: Replaced the shader setting with a strict serialized Material, added MAT_BiosDiagnostic, removed the engine material factory/destructor, copied diagnostic state into RenderGraph passData, and applies it through a cold MaterialPropertyBlock inside the render function.
Rejected Alternatives: Keeping a runtime material clone would preserve per-camera state but retain unmanaged material churn. Mutating the authored material was rejected because delayed RenderGraph execution and multiple cameras can leak BIOS payload between passes. Touching renderer assets was rejected because no active asset currently serializes this feature.
Scalability potential: Low/Middle/High/Ultra keep one material identity; quality can alter intensity and diagnostic cadence without fabricating resources.
Hardware Impact: Removes one renderer-feature material allocation path, one editor shader lookup path, and shared-material mutation from a visor diagnostic pass.

### D33 - VR Brownout Authored Material Route

Problem: HectonVRBrownoutFeature still used an editor shader fallback and CoreUtils.CreateEngineMaterial for the XR brownout/focus blur pass, while active PC, High, Mobile, and Quest renderer assets serialized the old shader field.
Solution: Replaced the shader setting with a strict serialized Material, added MAT_VRBrownout, rewired the four active renderer assets, removed AssetDatabase/CreateEngineMaterial/CoreUtils.Destroy routing, and kept dynamic brownout/comfort payload in the existing 64-byte GraphicsBuffer constant route.
Rejected Alternatives: Keeping a runtime material clone would preserve local material identity but continue unmanaged asset churn. Mutating an authored material was unnecessary because this feature already owns a pass-local constant buffer. Leaving renderer assets on the old shader field was rejected because it would silently fail-closed after the C# field migration.
Scalability potential: Low/Middle/High/Ultra keep the same authored pass identity; mobile tiers keep lower serialized blur/scanline values, while high tiers can spend comfort/brownout intensity through the constant buffer without changing resource ownership.
Hardware Impact: Removes one renderer-feature material allocation path and one editor shader lookup route from the XR visor stack; steady-state draw payload remains a fixed 64-byte copy.

### D34 - Retina Distortion Authored Material Route

Problem: HectonRetinaDistortionFeature still used an editor shader fallback and CoreUtils.CreateEngineMaterial for the health/narcosis retina pass. The feature is serialized in every renderer tier, so a future enable would revive the material factory.
Solution: Replaced the shader setting with a strict serialized Material, added MAT_RetinaDistortion, rewired PC/High/Mobile/Quest renderer assets, removed AssetDatabase/CreateEngineMaterial/CoreUtils.Destroy routing, and kept health/narcosis payload in the existing 32-byte GraphicsBuffer constant route.
Rejected Alternatives: Leaving the disabled feature alone was rejected because serialized disabled features still become active debt when a profile flips m_Active. Runtime material cloning was rejected for the same SRP/VRAM fragmentation reason as sonar/radar presentation. Shared material mutation was unnecessary because the feature already has a pass-local constant buffer.
Scalability potential: Low/Middle/High/Ultra keep one authored material identity; mobile/Quest keep weaker serialized offsets, high tiers can spend quality through RetinaQualityWeight and constant-buffer values without creating resources.
Hardware Impact: Removes one dormant renderer-feature material allocation path and one editor shader lookup route; steady-state remains a fixed 32-byte copy when the feature is enabled.

### D35 - Atmosphere Soot Authored Material Route

Problem: HectonAtmosphereSootFeature is active in every renderer tier and still created its overlay material from Hidden_Hecton_AtmosphereSootOverlay through editor/runtime shader fallback code.
Solution: Replaced the shader setting with a strict serialized Material, added MAT_AtmosphereSootOverlay, rewired PC/High/Mobile/Quest renderer assets, removed AssetDatabase/CreateEngineMaterial/CoreUtils.Destroy routing, and kept soot intensity/radius/camera payload in the existing 32-byte GraphicsBuffer constant route.
Rejected Alternatives: Leaving a universally active feature on a runtime material factory was rejected because it preserves startup VRAM churn in every profile. Runtime clones were rejected because this feature already owns per-camera state through a constant buffer. Shared material mutation was unnecessary and avoided.
Scalability potential: Low/Middle/High/Ultra keep one stable soot overlay material identity; tier-specific radius/quality settings still scale the visual density without changing resource ownership.
Hardware Impact: Removes one active renderer-feature material allocation path and one editor shader lookup route across all device tiers; steady-state remains a fixed 32-byte copy.

### D36 - Noir Depth Fog Authored Material Route

Problem: HectonNoirDepthFogFeature is active on PC/High/Mobile and serialized on Quest, but still loaded Hecton_NoirDepthFog through editor fallback, RuntimeShaderReferenceCatalog/Shader.Find fallback, and CoreUtils.CreateEngineMaterial.
Solution: Replaced the shader setting with a strict serialized Material, added MAT_NoirDepthFog, rewired PC/High/Mobile/Quest renderer assets, removed all shader fallback and material factory/destructor routing, and kept the fog ramp payload in the existing 64-byte GraphicsBuffer constant route.
Rejected Alternatives: Keeping RuntimeShaderReferenceCatalog fallback would keep hidden resource identity drift in a core atmospheric pass. Runtime clones were rejected because this feature already owns per-camera fog state through a constant buffer. New DTOs or new manager classes were unnecessary.
Scalability potential: Low/Middle/High/Ultra keep one stable fog material identity; quality still scales dither/ramp intensity through existing settings and GlobalQualityWeight.
Hardware Impact: Removes one active material allocation path and all shader fallback lookups from the depth fog pass; steady-state remains a fixed 64-byte copy.

### D37 - Half-Res Particle Composite Authored Material Route

Problem: HectonHalfResParticlesFeature is active on PC/High/Mobile and serialized on Quest, but still loaded Hecton_HalfResParticleComposite through editor fallback, RuntimeShaderReferenceCatalog/Shader.Find fallback, and CoreUtils.CreateEngineMaterial.
Solution: Replaced the composite shader setting with a strict serialized Material, added MAT_HalfResParticleComposite, rewired PC/High/Mobile/Quest renderer assets, removed all shader fallback and material factory/destructor routing, and kept composite strength/depth-scale payload in the existing 16-byte GraphicsBuffer constant route.
Rejected Alternatives: Keeping the fallback would preserve hidden asset identity fabrication in a pass that touches transparent FX every profile can enable. Runtime clones were rejected because the pass already owns dynamic state through a constant buffer. Rebuilding the half-res renderer was unnecessary for this ownership fix.
Scalability potential: Low/Middle/High/Ultra keep one stable composite material identity; existing GlobalQualityWeight and survival visual weight still scale renderScale/composite cost continuously.
Hardware Impact: Removes one material allocation path and shader fallback lookup chain from half-resolution FX setup; steady-state remains a fixed 16-byte copy.

### D38 - Deferred Caustics Immutable Material Payload

Problem: HectonDeferredCausticsFeature is active in all renderer tiers and still created a runtime material from Hecton_DeferredCaustics; baked atlas and waterline parameters were written into that material.
Solution: Replaced the shader setting with a strict serialized Material, added MAT_DeferredCaustics, rewired PC/High/Mobile/Quest renderer assets, removed shader fallback/material factory/destructor routing, and moved baked atlas/waterline texture/vector payload into a cold MaterialPropertyBlock applied inside the RenderGraph render function.
Rejected Alternatives: Keeping material.SetTexture/SetVector on an authored material was rejected because renderer profiles or cameras can leak state through shared material assets. Runtime material clones were rejected because they preserve unmanaged resource churn.
Scalability potential: Low/Middle/High/Ultra keep one stable caustics material identity; baked atlas and waterline weights remain continuous settings without changing draw ownership.
Hardware Impact: Removes one active material allocation path and shared-material mutation from deferred caustics; per-frame state transfer is a small MPB payload next to the existing constant buffer draw.

### D39 - Abyssal SSDO Immutable Pass Payload

Problem: HectonAbyssalSsdoFeature was active in PC, PC_High, and Mobile profiles but created four runtime materials from one shader and wrote pass parameters into those shared material instances before RenderGraph execution.
Solution: Replaced the shader setting with a strict serialized Material, added MAT_AbyssalSSDO, rewired PC/High/Mobile/Quest renderer assets, removed all material factory/destructor routing, and moved per-pass SSDO values into RenderGraph passData plus cold MaterialPropertyBlock instances.
Rejected Alternatives: Four authored materials would remove factories but duplicate one material family without need. Mutating one authored material was rejected because delayed RenderGraph execution and multi-camera rendering can leak pass mode and size payload.
Scalability potential: Low/Middle/High/Ultra keep one stable SSDO material identity; existing GlobalQualityWeight and survivalVisualWeight still scale render scale, radius, intensity, and composite strength continuously.
Hardware Impact: Removes four renderer-feature material allocation paths from an active pass and eliminates shared material parameter churn; frame payload is four small MPB applications inside render functions.

### D40 - Visor Trauma Authored Material Route

Problem: DeferredDecalPass was active in PC and PC_High profiles but loaded Hecton_VisorTrauma through editor fallback and created a runtime engine material during feature setup.
Solution: Replaced the deferredDecalShader field with a strict serialized Material, added MAT_VisorTraumaDeferredDecal, rewired active PC/High renderer assets, and left the existing LateFrameTick/VISUAL_SYNC double-buffer upload route intact.
Rejected Alternatives: Runtime material cloning was rejected because the pass already uses RenderGraph passData and globals for draw payload. Rewriting DynamicDecalVaultRuntime was rejected as unrelated blast radius.
Scalability potential: Low/Middle/High/Ultra keep one trauma material identity; capacity and fade settings remain serialized and continuous while the current visual-sync upload policy owns the dynamic data.
Hardware Impact: Removes one active material allocation path and one editor shader lookup route; steady-state buffer upload, try/finally GraphicsBuffer lock release, and render pass cadence remain unchanged.

### D41 - Scooter Volumetric Shafts Authored Material Route

Problem: HectonScooterVolumetricShaftsFeature created four materials from one shader and kept an editor shader fallback in a feature active on PC, PC_High, and Mobile.
Solution: Replaced the shader setting with a strict serialized Material, added MAT_ScooterVolumetricShafts, rewired PC/High/Mobile/Quest renderer assets, and preserved the existing pass-index fullscreen route.
Rejected Alternatives: Four authored materials were rejected because the shader already exposes separate pass indices. Runtime clones were rejected because per-pass payload already lives in render pass data and global textures.
Scalability potential: Low/Middle/High/Ultra keep one shaft material identity; existing quality and render-scale settings still control raymarch/blur cost continuously.
Hardware Impact: Removes four material allocation paths from a serialized shafts pass and avoids shader fallback lookup on low-memory profiles.

### D42 - Volumetric Fog Dear Lie Authored Material Route

Problem: HectonVolumetricParticulateFogFeature created the Dear Lie proxy material from a shader fallback while active renderer profiles serialized a shader field.
Solution: Replaced the proxy shader setting with a strict serialized Material, added MAT_VolumetricFogDearLie, rewired PC/High/Mobile renderer assets, and kept the compute path serialized-resource driven.
Rejected Alternatives: Keeping a runtime proxy material was rejected because the fallback path hides broken profile authoring. Replacing compute behavior was rejected as unrelated blast radius.
Scalability potential: Low/Middle/High/Ultra keep one proxy material identity; existing compute/proxy quality pressure still scales volumetric cost without resource fabrication.
Hardware Impact: Removes one active proxy material allocation path and one editor shader fallback route from volumetric fog startup.

### D43 - UberPost Authored Material Route

Problem: HectonVisorUberPostFeature is active in every renderer profile but still converted serialized shaders into runtime materials and overwrote the Noir shader through editor AssetDatabase fallback.
Solution: Split ownership into three authored materials: MAT_VisorGlitchACES for the active Noir pass, MAT_VisorUberPost for legacy post mode, and MAT_UberNoirBilateralUpsample for reconstruction. Removed AssetDatabase, shader fields, material factories, and material destruction from the feature.
Rejected Alternatives: One overloaded material field was rejected because Noir and legacy post are different shader contracts. Keeping editor assignment was rejected because it masks bad player-build serialization.
Scalability potential: Low/Middle/High/Ultra keep explicit material identities while existing constant buffers and GlobalQualityWeight continue to scale grain, reconstruction, and visual-overkill intensity.
Hardware Impact: Removes two runtime material allocation paths and editor shader fallback from the active visor post stack; steady-state keeps fixed constant-buffer/passData transfers.

### D44 - Fluid Visor Authored Material Route

Problem: HectonVisorFluidDistortionFeature is disabled but serialized in every renderer profile and would create a runtime material plus editor shader/compute fallback if enabled.
Solution: Replaced the shader setting with a strict serialized Material, added MAT_VisorFluidDistortion, rewired PC/High/Mobile/Quest renderer assets, and removed the runtime material factory/destructor.
Rejected Alternatives: Ignoring disabled serialized debt was rejected because profile toggles are common during platform tuning. Replacing the compute lens-mask route was rejected because the current issue is resource ownership, not effect math.
Scalability potential: Low/Middle/High/Ultra keep one fluid material identity; existing lens-mask render scale and continuous quality pressure still control the visual cheat.
Hardware Impact: Removes one dormant material allocation path and editor fallback route before the feature is re-enabled on cheap devices or VR profiles.

### D45 - Acoustic Survival Snapshot Ownership

Problem: TerminalBootSequence tried to read HectonSurvivalSystem from its local UI GameObject during sonar ping presentation, and FakeRadarBlipController kept a concrete survival/component fallback for thermal-noise depth. Both bypassed owner-published player snapshots.
Solution: TerminalBootSequence now registers as a GlobalRegistry hot-swap listener, caches IPlayerRuntimeContext cold, and formats hull/power/stress from sanitized PlayerSurvivalRuntimeState. FakeRadarBlipController now derives thermal-noise depth from PlayerMovementRuntimeState through cached IPlayerRuntimeContext.
Rejected Alternatives: Keeping TryGetComponent fallback was rejected because player survival ownership is already centralized. Adding a new UI survival manager was rejected as duplicate topology.
Scalability potential: Low/Middle/High/Ultra all read the same survival truth route; visual cadence stays in ping/LateFrameTick presentation without gameplay authority changes.
Hardware Impact: Removes concrete survival/component fallback routes from sonar/radar presentation ownership and keeps text generation on the existing fixed char buffer.

### D46 - Topographical Sonar LUT Lock Flattening

Problem: The topographical sonar editor material-color CSV import parsed text while holding the GlobalDataVault material LUT write lock.
Solution: CSV parsing now applies to the existing job-owned MaterialColorLut outside the vault lock. The old LUT is snapshotted to a 256-uint stack buffer and restored if the vault publish fails. The DataVault write lock now contains only a bounded uint copy.
Rejected Alternatives: Parsing directly into the vault buffer was rejected because parser work is not lightweight direct assignment. Adding a second persistent LUT was rejected as duplicate data ownership.
Scalability potential: Low/Middle/High/Ultra keep the same authored material-color semantics; cheap devices avoid editor/import stalls leaking into runtime ownership, high tiers keep full LUT fidelity.
Hardware Impact: Reduces DataVault lock hold time during material-color import from parse-duration to 256 integer assignments and preserves zero heap allocation through stackalloc rollback.

### D47 - Ground Radar Runtime Layout Gate

Problem: GroundPenetratingRadarRuntime owned explicit telemetry and indirect-args DTOs but had no local UnsafeUtility.SizeOf gate before allocating vault/GPU state.
Solution: Added a cold ValidateGroundRadarRuntimeLayouts check in the existing runtime and wired AllocatePersistentStateCold to fail closed when telemetry or indirect args stride drifts or loses 8-byte alignment.
Rejected Alternatives: Adding a separate editor verifier was rejected because runtime state must fail closed in player builds too. Leaving struct layout as comments was rejected because it does not catch drift.
Scalability potential: Low/Middle/High/Ultra all keep the same GPR behavior; layout validation prevents hidden ARM64/cache-line regressions before any radar buffers are allocated.
Hardware Impact: One cold UnsafeUtility.SizeOf check pair during enable/rebind; prevents misaligned telemetry/indirect-args payloads on low-end ARM64 and avoids invalid GPU argument buffer writes.

### D48 - Ground Radar SDF Snapshot Prewarm

Problem: GroundPenetratingRadarRuntime reused pending arrays, but the encoded SDF snapshot could still allocate or grow when a larger lease arrived during a scan.
Solution: Added a fixed 64^3 encoded-SDF snapshot capacity to GroundRadarConstants, allocated it with the existing persistent RadarPendingJob buffers during cold preparation, made scan scheduling require prewarmed buffers, and changed SDF staging to fail closed when the lease exceeds capacity.
Rejected Alternatives: Allowing dynamic growth was rejected because scanner spam can turn changing SDF payload size into runtime native memory churn. Adding a second SDF cache owner was rejected because the pending job already owns the scan-local copy.
Scalability potential: Low uses bounded 256 KB scan scratch and no runtime growth; Middle/High/Ultra keep the same GPR fidelity while saved allocation budget can be spent on stronger visual ping presentation.
Hardware Impact: Removes SDF snapshot allocation/growth from scanner cadence; one fixed cold 256 KB native buffer replaces variable persistent allocation on low-end memory systems.

### D49 - Ground Radar DataVault Rebind Phase Gate

Problem: GroundPenetratingRadarRuntime could apply a DataVault hotswap immediately, and the new cold prewarm hook could retire the pending job handle if invoked while a scan job was scheduled.
Solution: QueueDataVaultRebind now leaves the rebind pending while a job is active; LateFrameTick applies the queued rebind only after CompleteRadarJob(false) and the scheduled flag is clear; TryEnsureRadarPendingJobCold returns without resetting active job handles.
Rejected Alternatives: Force-completing the job on hotswap was rejected because hotswap is not a simulation fence. Ignoring rebind until the next SlowTick was rejected because it leaves stale vault descriptors longer than necessary.
Scalability potential: Low avoids main-thread stalls from forced job completion; Middle/High/Ultra keep deterministic GPR scan publication while DataVault replacement remains a post-fence operation.
Hardware Impact: No steady-state cost; removes a hotswap race where low-end frames could lose the active job handle or publish into stale vault state.

### D50 - Acoustic Flashlight Snapshot Route

Problem: AcousticEchoLocationRuntime built ghost blip context from PlayerFlashlight.PresentationAnchor.forward, pulling presentation Transform state into simulation tap assembly, and its static self-audit did not validate AcousticEchoHuntResult or AcousticEchoBlackBoxEntry layout drift.
Solution: Flashlight cone direction now comes from IPlayerRuntimeContext.TryGetLookRuntimeState(PlayerLookState) with pose-forward fallback, while PlayerFlashlight is used only for beam-active and spot-angle scalar presentation state. TryRunStaticSelfAudit now checks hunt-result and black-box DTO sizes plus 8-byte alignment.
Rejected Alternatives: Adding a new flashlight-context interface was rejected as wider cross-domain surface than needed. Keeping PresentationAnchor was rejected because scene Transform reads are not an owner-published acoustic truth route.
Scalability potential: Low/Middle/High/Ultra all consume the same look snapshot; quality can still scale ghost visual budget without changing player look authority or DTO layout.
Hardware Impact: Removes one scene Transform forward read from ghost-context assembly and catches acoustic result/black-box layout drift before runtime ownership paths use those buffers.

### D51 - Acoustic Guard Mask Constant Fold

Problem: Unity standard script validation flagged repeated AcousticMutationGuardBit calls in static field initialization as a duplicate one-parameter method signature, even though the file had only one helper method.
Solution: Replaced the helper with compile-time guard-bit constants for frame taps, pending taps, trail state, and black box; masks now combine constants directly.
Rejected Alternatives: Suppressing the validator finding was rejected because the constant-folded route is simpler and removes cold initializer calls. Replacing the validator was rejected as unrelated tooling work.
Scalability potential: Low/Middle/High/Ultra keep identical DataVault guard masks; no gameplay truth or buffer ownership changed.
Hardware Impact: Removes four cold helper invocations from static initialization and produces a Unity standard script validation pass with 0 errors/0 warnings.

### D52 - Acoustic Reverb Trigger Snapshot Route

Problem: AcousticReverbPresetTrigger.Tick used PlayerRuntimePoseSnapshot first but could fall back to IPlayerRuntimeContext.PlayerTransform.position, pulling scene Transform state into the hot reverb trigger test.
Solution: TryResolvePlayerPosition now fails closed unless the player runtime context publishes a finite PlayerRuntimePoseSnapshot. The authored trigger volume and LateFrameTick presentation transition remain unchanged.
Rejected Alternatives: Keeping the transform fallback was rejected because owner-published player pose already exists. Adding a new trigger-position service was rejected as duplicate ownership for one scalar position route.
Scalability potential: Low/Middle/High/Ultra all use the same pose truth; quality can still scale audio transition polish without changing zone detection authority.
Hardware Impact: Removes one hot scene Transform fallback from reverb trigger evaluation; no new allocations or persistent structures.

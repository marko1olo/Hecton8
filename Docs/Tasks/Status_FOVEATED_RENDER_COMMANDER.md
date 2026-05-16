# Status_FOVEATED_RENDER_COMMANDER

Prompt: FOVEATED_RENDER_COMMANDER
Domain: GRAPHICS/VR
Task Count: 18
State: PENDING VERIFICATION / FINAL_VALIDATION BLOCKED BY DEPENDENCY

Hygiene: Status file was missing at session start. Initialized for current batch only. Prompt block was extracted with CLI from `Docs/Tasks/CURRENT_BATCH.md`.

## Mandates Selected Before Coding
- READ: REND_Foveated_Simulation_LOD.txt
- READ: REND_VRS_MX350_Reality_Check.txt
- READ: REND_VR_Stencil_Masking.txt
- READ: ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- READ: ARCH_Signal_Lane_Segregation.txt
- READ: DBG_Telemetry_Crash_Reporting_PostMortem.txt
- READ: OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- READ: OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt

## Tasks
- [x] 1. PURGE_SINGLETONS | Justification: `rg` found no `VRSManager.Instance`; new runtime uses bootstrap singleton only as duplicate guard and uses `GlobalRegistry`/XR subsystem APIs for service contact. | Alternatives Rejected: scene-driven `VRSManager.Instance` or direct prefab dependency. | Estimate: 0 us steady-state, 1 cold GameObject allocation.
- [x] 2. DEBT_CLEANUP | Justification: no manual render-target edge downscale was added; hardware foveation uses Unity XR display state. Existing DRS remains separate, and legacy Core `OculusFfrEnforcer` hardware writes were reduced to a disabled compatibility shim to prevent a second foveation driver. | Alternatives Rejected: edge blit/downscaled RT mask competing with URP/XR or parallel Quest-only foveation ownership. | Estimate: saves 60-250 us GPU versus edge blit path depending eye RT.
- [x] 3. DATA_EVICTION | Justification: `SystemStress01`, `GpuUtil01`, pressure level, and foveated pressure tier are read from `SystemHealthSignal` snapshots. | Alternatives Rejected: polling `HomeostasisBrain` statics or inspector curves. | Estimate: 2-6 us CPU per dispatcher tick.
- [x] 4. BURST_ALGORITHM | Justification: N/A by prompt; Unity hardware API call only. | Alternatives Rejected: compute kernel for API-managed foveation. | Estimate: 0 us.
- [x] 5. AUP_INTEGRITY | Justification: N/A by prompt; no world-space simulation math. | Alternatives Rejected: adding AUP dependency to render settings. | Estimate: 0 us.
- [x] 6. DOD_SOA_LAYOUT | Justification: stress maps to byte codes Low/Med/High and fixed scalar levels 0.35/0.62/0.85, then reports scalar state to `HectonXRRuntimeState`. | Alternatives Rejected: ScriptableObject quality table or managed dictionary lookup. | Estimate: 1 us CPU, fill-rate saving is GPU dependent.
- [x] 7. SIGNAL_FLOW | Justification: consumes `SignalBus<SystemHealthSignal>` and `SignalBus<ThermalStateChangedSignal>` through `ReadOnlySpan<T>`. | Alternatives Rejected: `UnityEvent`, `Action<T>`, or per-system direct dependency. | Estimate: 2-8 us CPU.
- [x] 8. LOW_TIER_FAKE | Justification: Quest 2/Oculus Quest class Android XR runtimes are forced to High FFR constantly without requiring Vulkan classification; Quest 3/Quest Pro are explicitly excluded from the low-tier lock; Unity caps still gate actual hardware foveation writes. | Alternatives Rejected: per-frame aesthetic search, eye-tracking assumption on weak mobile silicon, Vulkan-only Quest detection, or misclassifying Quest 3/Pro as toaster hardware. | Estimate: saves 0.2-1.0 ms GPU fill-rate on mobile VR; CPU cost under 3 us per sample.
- [x] 9. HIGH_END_OVERKILL | Justification: PC standalone-like VR enables `GazeAllowed` only when XR eye fixation is valid and caps expose foveation image/non-uniform raster; High/Ultra PC VR without gaze and without pressure clears fixed foveation instead of applying mobile-style edge loss. | Alternatives Rejected: blind gaze flag without eye data, package-specific OpenXR calls absent from manifest, or forcing fixed FFR on 4090-class no-pressure VR. | Estimate: 5-15 us CPU sample path, GPU benefit hardware dependent.
- [x] 10. REACTIVE_VFX | Justification: N/A by prompt; foveation must not create new VFX coupling. | Alternatives Rejected: non-requested VFX events. | Estimate: 0 us.
- [x] 11. STP_STABILIZATION | Justification: N/A by prompt; no STP vector or render-scale mutation. | Alternatives Rejected: touching STP/DRS systems from VR foveation. | Estimate: 0 us.
- [x] 12. NAN_VACCINATION | Justification: finite guards cover foveation level, XR display returned level, GPU time, stress, eye dimensions, and eye fixation point; invalid state writes telemetry, dumps blackbox, and clears hardware foveation. | Alternatives Rejected: trusting XR provider floats or dumping without fail-safe hardware clear. | Estimate: 1-3 us CPU.
- [x] 13. BLACKBOX_LOGGING | Justification: 300-frame `GlobalDataVault` buffer `BufferID.FoveatedRenderBlackBox` writes level, caps, flags, GPU time, stress, display count, vault generation, and severity; binary dump writes 64-byte padded records matching the pack-1 struct size; legacy Quest FFR private `NativeArray` blackbox was removed with the disabled shim. | Alternatives Rejected: private persistent `NativeArray`, managed list/log spam, unpadded binary records, duplicate Quest dump ownership, or chat-only debugging. | Estimate: 1-5 us CPU; fixed 19.2 KB vault memory.
- [x] 14. TRIPLE_STRIKE_REPAIR | Justification: Unity 6 core XR APIs compile path chosen: `SystemInfo.foveatedRenderingCaps`, `XRDisplaySubsystem.foveatedRenderingLevel`, `FoveatedRenderingFlags`. Three build attempts found only external compile walls. | Alternatives Rejected: `UnityEngine.XR.OpenXR` dependency because package is absent from manifest. | Estimate: 0 us runtime.
- [x] 15. HOMEOSTASIS_ADAPTATION | Justification: thermal throttling, GPU utilization >=0.78, GPU app time >=10.75ms, pressure level, or foveated pressure tier escalates FFR to High; GPU app time is sampled even when the target foveation state is unchanged and thermal severity can recover from lower service/signal snapshots. | Alternatives Rejected: static Quest-only foveation for all platforms, stale one-shot GPU timing, or permanent high-severity thermal latch. | Estimate: 2-5 us CPU.
- [x] 16. UI_EXEMPTION | Justification: SRP camera callbacks fail closed; cameras rendering UI layer mask force foveation level 0 and a depth counter restores only after the last matching UI camera exits. | Alternatives Rejected: shader-side text compensation, assuming separate UI camera always exists, or a boolean suppression latch that can restore too early. | Estimate: 3-12 us per UI camera; protects text legibility.
- [x] 17. PC_FALLBACK | Justification: flat-screen PC path disables hardware foveation unless explicitly allowed. | Alternatives Rejected: applying VRS to non-XR camera by default. | Estimate: 0-2 us CPU.
- [BLOCKED BY DEPENDENCY] 18. FINAL_VALIDATION | Justification: `dotnet build` remains red outside `Assets/_Project/Scripts/Graphics/VR/`. Current full attempt reports 16 external errors in `SargassumMicroFaunaBoids.cs`, `VehicleDockingModule.cs`, and `HectonMarineSnowRenderer.cs`; filtered VR/legacy foveation diagnostics remain empty. | Alternatives Rejected: editing unrelated domains or reverting other agents. | Estimate: 0 us runtime; compile blocked.

## Compile Attempts
- Attempt 1: `dotnet build Hecton8.Core.csproj` failed in `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs` with missing `ConsumeAupPreShiftSignals`, `PublishAupPreShiftHaltState`, `_lastSyncFenceHash`, `_lastSyncFenceFrame`.
- Attempt 2: `dotnet build Hecton8.Core.csproj --no-restore` failed in `Assets/_Project/Scripts/Core/GlobalSignals.cs(580,50)` because `SignalBus<T>.SignalLaneAdapter` does not implement `ISignalLane.FlushPreSimulation(bool, int)`.
- Attempt 3: same `GlobalSignals.cs(580,50)` external compile wall.
- Attempt 4: `dotnet build Hecton8.Core.csproj --no-restore` failed with 105 errors outside `Graphics/VR`: missing assembly references for `Hecton8.Core.Contracts`/`Hecton8.Core.Memory`, missing `HectonShaderGlobalDataVaultBridge`, missing voxel debris constants, missing signal types (`VisualFlareSignal`, `AnomalyProximitySignal`, `CompassCalibratedSignal`, `FluidImpulseSignal`, `DebrisSpawnSignal`), and unrelated Gameplay/Fauna conversion/helper errors.
- Attempt 5: `dotnet build Hecton8.Core.csproj --no-restore 2>&1 | Select-String -Pattern 'FoveatedRenderCommander|FoveatedRenderBlackBox|Graphics\\VR|Graphics/VR'` produced no matching diagnostics; full build still exits red from external errors.
- Attempt 6: repeated filtered build diagnostic scan after thermal/blackbox/NaN hardening produced no matching VR-domain diagnostics; global build still exits red externally.
- Attempt 7: repeated filtered build diagnostic scan after invalid-state shader-global suppression produced no matching VR-domain diagnostics; global build still exits red externally.
- Attempt 8: repeated filtered build diagnostic scan after canonical cold-allocation comment cleanup timed out after 147 seconds; no `dotnet` process remained afterward. This is not counted as a green validation.
- Attempt 9: `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /clp:ErrorsOnly` filtered for VR-domain symbols produced no matching diagnostics; full build still exits red externally.
- Attempt 10: same filtered build scan after legacy `OculusFfrEnforcer` quarantine produced no matching `FoveatedRenderCommander`, `FoveatedRenderBlackBox`, `OculusFfrEnforcer`, or `Graphics/VR` diagnostics; full build still exits red externally.
- Attempt 11: same filtered build scan after duplicate-guard hardening produced no matching VR/legacy foveation diagnostics; full build still exits red externally.
- Attempt 12: same filtered build scan after Quest Android detection hardening produced no matching VR/legacy foveation diagnostics; full build still exits red externally.
- Attempt 13: unfiltered `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /clp:ErrorsOnly` failed with 16 external errors: missing `EnsureVaultBufferHandle` in `World/SargassumMicroFaunaBoids.cs`, missing `CacheFluidRuntime`/`ResetDockingRuntimeCaches` in `Construction/VehicleDockingModule.cs`, and missing `_vehicleWakeJobResult`/`_telemetryRing` in `VFX/HectonMarineSnowRenderer.cs`.
- Attempt 14: filtered build scan after Quest 3/Pro low-tier exclusion produced no matching VR/legacy foveation diagnostics; full build status remains blocked by the external 16-error set above.
- Attempt 15: filtered build scan after High/Ultra fixed-foveation suppression and XR scratch capacity increase produced no matching VR/legacy foveation diagnostics; full build remains externally blocked.

## Loop Log
- Loop 0: Prompt extracted from `CURRENT_BATCH.md`. Status/Rationale were missing. No code touched.
- Loop 1: Mandates and domain read; old `OculusFfrEnforcer` and absent `VRSManager.Instance` audited.
- Loop 2: Implemented `FoveatedRenderCommander` in assigned `Graphics/VR` domain with hardware FFR/VRS, signal flow, PC fallback, and blackbox.
- Loop 3: Re-read own code for hot-path allocations, UI fail-closed behavior, and XR API drift. No `Update`, no LINQ, no per-frame collections.
- Loop 4: Build attempt hit external `PlayerKinematicsRuntime.cs` compile wall; did not modify Gameplay domain.
- Loop 5: Re-read commander and fixed stale GPU-time latch plus disabled-state telemetry display counts.
- Loop 6: Build attempts 2-3 hit external `GlobalSignals.cs` compile wall; marked final validation blocked by dependency.
- Loop 7: Re-opened after CTO escalation. Evicted private telemetry `NativeArray` to `GlobalDataVault`, added `BufferID.FoveatedRenderBlackBox`, switched telemetry struct to `[StructLayout(Pack = 1, Size = 64)]`, and re-ran build. No `Graphics/VR` diagnostics surfaced; build is blocked by external assembly/contract errors.
- Loop 8: Re-read commander under multiplatform/H-Phi pressure. Fixed stale GPU app-time sampling when foveation target state is unchanged, replaced UI suppression boolean with a matching UI-camera depth counter, and verified filtered build output contains no VR-domain diagnostics.
- Loop 9: Fixed permanent thermal severity latch, padded blackbox binary records to the actual 64-byte struct size, and made invalid XR display/eye state write telemetry before dumping and clearing hardware foveation.
- Loop 10: Prevented invalid eye/display state from being reported as active hardware foveation to XR shader globals before the fail-safe clear runs.
- Loop 11: Re-read `AGENTS.md` and `Docs/Actual Domains of Project.txt`; corrected state wording to `PENDING VERIFICATION` because runtime readiness cannot be claimed from static scans or blocked builds.
- Loop 12: Corrected cold-allocation comments in the VR file to the project canonical `COLD ALLOC` format and re-ran the static debt scan.
- Loop 13: Purged the duplicate legacy `OculusFfrEnforcer` execution path to a disabled compatibility shim, removing its private `NativeArray` blackbox, managed XR-state event subscription, texture mip clamp, and direct hardware foveation writes.
- Loop 14: Hardened duplicate commander handling so a duplicate component destroys only itself instead of destroying the whole host GameObject.
- Loop 15: Removed the Vulkan-only dependency from Quest 2 fixed-FFR detection; Android XR Quest-family runtimes now take the low-tier high-FFR fake when memory/device evidence matches.
- Loop 16: Added explicit Quest 3/Quest Pro exclusion before memory-gate fallback so high-end standalone headsets are not downgraded to Quest 2 fixed-high policy by reserved-memory reporting.
- Loop 17: Added High/Ultra PC VR no-pressure fixed-foveation suppression and increased XR display scratch capacity from 4 to 8 to reduce hot-path list growth risk.

## Omega Polish Inquisition
- Polish mandate read only after tasks were complete/blocked: `[VI. OMEGA POLISH MANDATE] STATUS: MUST BE "VERIFIED MASTER GRADE".`
- Anti-bloat scan: no `Update()`, no `GameObject.Find`, no `FindObject*`, no `foreach`, no LINQ, no `VRSManager.Instance` in `FoveatedRenderCommander.cs`; legacy `OculusFfrEnforcer` no longer owns hardware foveation or blackbox data.
- Allocation scan: hot path has no managed collection creation; cold allocations are the bootstrap GameObject/component and one static `List<XRDisplaySubsystem>` pre-sized to 8 display subsystems; blackbox storage is a vault-owned 300-entry buffer and dump `FileStream/BinaryWriter` allocation is dump-only.
- Render-target scan: new commander does not allocate or downscale edge render targets; it uses XR display hardware foveation state only.
- Stability scan: GPU timing remains fresh without rewriting XR display state every sample; UI VRS disable is nested-camera safe and only decremented by matching UI-camera end callbacks; thermal severity can recover; non-finite XR display state clears hardware foveation after writing evidence and is not reported as active to shader globals; duplicate components do not destroy scene rigs.
- Final status: assigned-domain static scans pass, but runtime readiness is pending verification and project build cannot be honestly marked green until Core/Gameplay dependency walls are fixed.

## Multiplatform Inquisition
- ARM64/Quest/Android: `FoveatedRenderTelemetryEntry` is `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`; the heartbeat buffer is vault-owned and 64-byte arena aligned by `GlobalDataVault`; Quest 2/Oculus Quest fixed-high FFR detection does not depend on Vulkan and excludes Quest 3/Quest Pro.
- Metal/Mac: no shader, compute kernel, thread group, or DirectX-only path was introduced in `Graphics/VR`; PC/Mac standalone-like VR only uses Unity XR foveation flags when hardware caps and gaze data exist.
- Steam Deck: normal runtime performs no disk reads and no per-frame file writes; blackbox dump is one-shot only on non-finite/crash path.
- PC God-Mode: PC VR is not clamped to mobile fixed FFR; High/Ultra no-pressure VR without gaze clears fixed foveation, while valid eye fixation uses gaze-allowed VRS and reports hardware foveation state into existing XR shader globals for downstream visual overkill; the old Quest-only enforcer can no longer overwrite the PC/Quest 3 path.

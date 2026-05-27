# Unity Signal Audit Classifier And Seam MPB Pass - UNKNOWN - 2026-05-27

Date: 2026-05-27
Status: PENDING UNITY VERIFICATION
Owner: UNKNOWN
Evidence class: STATIC_SOURCE_CLASSIFIED

## Scope

- Rechecked current signal/native/hot-path audit output after the queue diagnostics pass.
- Fixed false-positive classes in `SignalBusContractAuditCli`.
- Applied one clean runtime render-parameter fix in `SeamGapDitherRenderer`.
- Removed clean duplicate signal-like names where local DTOs shadowed unrelated runtime contracts.
- Continued the safe duplicate cleanup only for clean owner-contained carriers.
- Reclassified Burst job structs and `Execute()` carrier structs so they stop inflating payload-layout debt.
- Fixed stale multiline method tracking that misclassified dump/fatal file I/O as hot runtime I/O.
- Did not edit dirty files owned by concurrent agents.

## Source Changes

| File | Change |
|---|---|
| `Tools/SignalBusContractAuditCli/Program.cs` | Added constructor detection so `TickList` constructors are not classified as hot methods. |
| `Tools/SignalBusContractAuditCli/Program.cs` | Added `#if UNITY_EDITOR` branch tracking for line-level editor-only classification. |
| `Tools/SignalBusContractAuditCli/Program.cs` | Split direct material mutation, cached `MaterialPropertyBlock`, and `ComputeShader` dispatch parameter updates into separate rules. |
| `Tools/SignalBusContractAuditCli/Program.cs` | Classified owner-local bounded telemetry rings separately from non-Vault persistent/global telemetry rings. |
| `Tools/SignalBusContractAuditCli/Program.cs` | Added cold/save/telemetry/async allocation classification, multiline method-state tracking, and fully-qualified `StructLayout` detection. |
| `Tools/SignalBusContractAuditCli/Program.cs` | Classified `IJob`/`IJobParallelFor` structs as `JOB_STRUCT_LAYOUT_REVIEW` info instead of signal payload layout warnings. |
| `Tools/SignalBusContractAuditCli/Program.cs` | Classified structs with `Execute()` bodies as executable carriers instead of serialized payloads. |
| `Tools/SignalBusContractAuditCli/Program.cs` | Fixed pending multiline method brace accounting so dump/fatal I/O uses the correct owning method. |
| `Assets/_Project/Scripts/SeamGapDitherRenderer.cs` | Replaced per-draw `drawMaterial.Set*` mutation with an owner-cached `MaterialPropertyBlock` passed to `Graphics.DrawMeshInstancedIndirect`. |
| `Assets/_Project/Scripts/Input/Determinism/DeterministicInputContracts.cs` | Renamed local deterministic mock DTOs so they do not shadow core input DTO names. |
| `Assets/_Project/Scripts/Core/Contracts/Physics/HabitatFluidIncursionContracts.cs` | Renamed core-only telemetry aliases so they do not shadow the active physics fluid telemetry owner names. |
| `Assets/_Project/Scripts/Audio/Virtualization/Contracts/AudioVirtualizationContracts.cs` | Renamed voice-virtualization telemetry to `AcousticOcclusionTelemetryEntry`, leaving portal propagation `AcousticTelemetryEntry` as the portal owner. |
| `Assets/_Project/Scripts/SpatialAudioManager.cs` | Updated acoustic telemetry aliases after the virtualization payload rename. |
| `Assets/_Project/Scripts/Audio/Synthesis/DepthStressGranularSynthesisKernel.cs` | Renamed blind synth validation DTOs to `DepthStressMockPressureSignal` and `DepthStressMockTensionSignal`. |
| `Assets/_Project/Scripts/Gameplay/EclipseGameplaySystem.cs` | Renamed the private deferred queue payload to `DeferredEclipseGameplayEventPayload`, leaving the public seismic `EclipseGameplayEventPayload : ISignal` route intact. |
| `Assets/_Project/Scripts/UI/BaseIntegrityHUD.cs` | Renamed the 8-byte UI queue payload to `UiBaseIntegrityEventPayload`, leaving the 64-byte habitat `BaseIntegrityEventPayload : ISignal` route intact. |
| `Assets/_Project/Scripts/Progression/PDAContextualAdvisorySystem.cs` | Updated UI listener signature for `UiBaseIntegrityEventPayload`. |
| `Assets/_Project/Scripts/Progression/NarrativeProgressionBridge.cs` | Updated UI listener signature for `UiBaseIntegrityEventPayload`. |
| `Assets/_Project/Scripts/UI/SuitAdvisoryController.cs` | Updated UI listener signature for `UiBaseIntegrityEventPayload`. |
| `Assets/_Project/Scripts/Editor/SignalPayloadLayoutValidator.cs` | Updated the UI payload layout validator target type name. |
| `Assets/_Project/Scripts/CrashTelemetryBuffer.cs` | Renamed local telemetry payload to `CrashTelemetryEntry`. |
| `Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs` | Renamed pager-local telemetry payload to `H8BinaryWorldPagerTelemetryEntry`. |
| `Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs` | Renamed executable job carrier to `SaveMerkleTelemetryWriteJob`. |
| `Assets/_Project/Scripts/Physics/HarpoonTensionSolver328.cs` | Renamed tether telemetry job carrier to `RecordHarpoonTetherTelemetryJob`. |
| `Assets/_Project/Scripts/Physics/Cable132/CablePhysicsSolver132.cs` | Renamed cable telemetry job carrier to `RecordCableTetherTelemetryJob`. |
| `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyData.cs` | Renamed physiology-local mock damage payload to `PhysiologyMockCombatDamageSignal`. |
| `Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs` | Renamed biolum-local predator and combat mock payloads. |
| `Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityTypes.cs` | Renamed hull-local mock damage/depth payloads and stride constants. |
| `Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs` | Updated hull mock payload call sites. |
| `Assets/_Project/Scripts/Habitat/Deformation/Editor/HullIntegrityTunerWindow.cs` | Updated hull editor mock damage type reference. |
| `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.AcousticSdf.cs` | Added explicit 64-byte layout to `AcousticSensoryTelemetrySnapshot`. |

## Verified Rechecks

| Recheck | Result |
|---|---|
| Final classifier tool build | `Docs/Reports/BUILD_UNKNOWN_SIGNAL_CLI_EXEC_CARRIER_RECHECK_20260527.log` exits `0`. |
| Latest verified full audit | `Docs/Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_EXEC_CARRIER_RECHECK.json`. |
| Latest verified full audit counters | `files=2441`, `shaders=71`, `errors=0`, `confirmedErrors=0`, `warnings=171`, `infos=826`, `reviewOnlyFindings=915`. |
| Material split | `SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW=73`, `MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW=38`, `GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW=114`. |
| Telemetry split | `LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT=5`, `LOCAL_NATIVE_TELEMETRY_RING_OWNER_LOCAL=2`. |
| Seam dither proof | Old `drawMaterial.Set*` calls removed; brace delta `0`; full audit moved two seam draw parameter lines from SRP warnings to MPB info. |
| Duplicate-name proof | First pass `DUPLICATE_SIGNAL_LIKE_NAME_REVIEW=48`; contract-rename pass `30`; latest recheck `14`. |
| Layout proof | `SIGNAL_LAYOUT_REVIEW=2` warnings remain; `JOB_STRUCT_LAYOUT_REVIEW=69` and `EXECUTABLE_STRUCT_LAYOUT_REVIEW=2` are info-only carriers. |
| I/O classifier proof | `RUNTIME_SYNC_FILE_IO_REVIEW=69` warnings; `COLD_OR_FATAL_SYNC_IO_REVIEW=549` info after method tracker fix. |
| Hot allocation proof | `ZERO_GC_HOT_PATH_ALLOCATION_REVIEW=0`; `COLD_OR_ASYNC_ALLOCATION_REVIEW=2` is info-only. |

## Remaining Debt

| Area | Current state |
|---|---|
| Direct material mutation | `73` warnings remain after better method-state tracking. Many current high-value rows are in dirty files or non-owned render domains: boids, celestial, fabricator UI, construction drones, submarine structural grid, vehicle cockpit, visor features, GPR, vegetation, ore. |
| Non-Vault telemetry rings | `5` warnings remain: `SaveManager` x3, `GlobalTelemetryBus._snapshotBuffer`, `ModEventProjectionBridge._cullTelemetry`. These are not owner-local by current scan. |
| Owner-local telemetry rings | `EncounterDirector._blackBox` and `ContextualPhysicalIkRuntime._telemetryRing` are classified as owner-local because they have sentinel ownership, bounded lifetime, and owner dump routes. Do not migrate them unless another domain consumes the buffer or the ring becomes persistent authority. |
| Duplicate signal-like names | `14` review warnings remain. Remaining pairs are dirty-owner or route-decision cases: ocean/atmosphere, structure, toxic/thermal jobs, audio/UI depth mocks, graphics quality mocks, and volumetric/acoustic mocks. |
| Payload layout | `2` warnings remain: dirty `FaunaDirector.AcousticPanicCommand` and dirty `VocalWarningSystem.VocalWarningTelemetrySnapshot`. |
| Sync runtime I/O | `69` warnings remain. The scanner no longer counts cold/fatal dump routes as runtime warnings, but real save/profile/runtime I/O still needs owner review. |
| Fluid DTO duplication | Some physics/fluid owner decisions remain outside this pass. Active physics/construction files were dirty under other agents, so no broad migration was attempted. |
| Zero-GC hot allocation | Latest verified audit shows no `ZERO_GC_HOT_PATH_ALLOCATION_REVIEW` findings. Two cold/async allocation contexts remain info-only. |

## Build Boundary

- `BUILD_UNKNOWN_SIGNAL_CLI_EXEC_CARRIER_RECHECK_20260527.log`: CLI build exits `0`.
- `BUILD_UNKNOWN_EXEC_CARRIER_RECHECK_20260527.log`: guarded `Hecton8.slnx` build launched at CPU `43` with no active compiler process.
- Full solution build failed after `00:06:27.39` with `83` warnings and `3` errors.
- Errors: `MSB4006` circular `ResolveProjectReferences` in `Unity.RenderPipelines.Core.Editor.csproj` and `Unity.ShaderGraph.Editor.csproj`; `CS0006` missing `Temp/CodexBuild/Unity.ShaderGraph.Editor/Unity.ShaderGraph.Editor.dll`.
- Classification: generated Unity project graph boundary before changed gameplay/tool source was proven. Do not report green build.

## Documentation Gates

- `python Tools/VerifyDocStructure.py`: `pass=true`, `activeDocCount=684`, `encodingWithoutUtf8Sig=0`.
- `python Tools/OOP_Doc_Scanner.py`: `finalPass=true`, `activeFileCount=684`, `sourceSyncPass=true`, `wordReductionPercent=35.75030475858767`.

## Non-Claims

- No Unity import, Play Mode, profiler, GCMonitor, player build, or visual capture was run.
- Runtime microseconds saved claimed: `0`.
- This is static source and CLI evidence only.

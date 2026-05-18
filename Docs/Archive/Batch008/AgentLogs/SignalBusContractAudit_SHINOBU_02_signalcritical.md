# SHINOBU_02 Signal Bus Contract Audit

Evidence Class: STATIC_SOURCE_CLASSIFIED
Scope: SignalCritical
Generated UTC: 2026-05-17T23:02:30.5282838Z

## Summary

- Files scanned: 7 C# / 61 compute
- Signal-like definitions found: 188
- Signal definitions still in Core/GlobalSignals.cs: 173
- Pack=1 layouts: 164
- Runtime signal Pack=1 layouts: 164
- Signal-like definitions without nearby StructLayout: 1
- Managed event surface hits: 0
- Local native telemetry ring hits: 1
- Registered local telemetry rings: 0
- Hot-path heuristic hits: 0
- Compute 1024-thread-group hits: 0
- Errors: 164
- Warnings: 9
- Infos: 1
- Confirmed/probable errors at confidence >= 90: 164
- Review-only findings below confidence 75: 1

## Rule Breakdown

- RUNTIME_SIGNAL_PACK1_FORBIDDEN: total 164, errors 164, warnings 0, infos 0, avg confidence 95.9
- RUNTIME_SYNC_FILE_IO_REVIEW: total 9, errors 0, warnings 9, infos 0, avg confidence 76
- EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW: total 1, errors 0, warnings 0, infos 1, avg confidence 56

## Classification Breakdown

- CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL: 164
- IO_PRESSURE_HEURISTIC: 9
- EDITOR_ONLY_REVIEW: 1

## Findings

- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:42 | ScalabilityChangedEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:123 | AudioEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 144)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:150 | DataVaultUpdateSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:164 | PrefabAcousticSignatureSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:178 | PrefabLoreLinkSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:213 | DebugSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:230 | SystemHealthSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:246 | FrameTimeSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:260 | KillSwitchSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:272 | ReentryVfxStateSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:292 | VisorDropletSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:307 | InputSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:320 | StateCorrectionSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 128)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:335 | DesyncDetectedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:346 | SyncFenceSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 128)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:375 | TetherTensionSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 144)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:392 | TetherSnappedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:407 | TetherFiredSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:423 | DockingRequestSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:438 | DockingCompleteSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:453 | DockingFailedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:468 | VoxelCarveEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 128)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:489 | VisualFlareSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:504 | CameraJuiceImpactSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][90%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:554 | SignalLaneTelemetry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:571 | InputStateSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:584 | LockstepSnapshotSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:599 | SystemGlitchSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:629 | LaserCutterEventPayload
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:658 | SplashEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:713 | PhysicsEventPayload
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:734 | DeferredSubmarineImpactSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:770 | PlayerInputSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:789 | PlayerLookTargetSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 160)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][90%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6474 | SpscSignalRingBuffer
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6576 | ImpactSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6593 | HighSpeedImpactSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 96)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6655 | PlayerStateSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6694 | SurvivalVitalsChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6764 | InventoryCommandSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6775 | InventoryChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6788 | ItemDurabilityChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6808 | ItemAcquiredSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6821 | RadiationDoseSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6833 | TemperatureChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6849 | RadiationSourceSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6864 | ResourceDepletionDeltaSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6897 | DropPodLandedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6921 | WakeGeneratedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6930 | FluidImpulseSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6943 | BubbleSpawnSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6959 | ProgressionEventSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6971 | GlobalWorldStateSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6992 | BiomeChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7003 | BiomeGradientSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7027 | NarrativeFocusSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7075 | NarrativeHudWaypointSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7087 | SoundscapeProfileSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7098 | NarrativePoiStateSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7110 | BrownoutSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7123 | DebrisSpawnSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7143 | DeflectSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7157 | EntityDeathSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7168 | EntitySpawnSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7189 | SolarFlareSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7200 | RebaseSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7210 | ControlSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7223 | AnomalySignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7235 | AnomalyProximitySignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7247 | CompassCalibratedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7257 | TelemetryAnomalySignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7269 | CrashTelemetrySignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7283 | HabitatConstructionSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7295 | DeconstructRequestSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 128)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7310 | DeconstructResultSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7323 | ModuleDeconstructSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7336 | VitalWarningSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7349 | CrushWarningSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7363 | SubtitleSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7375 | VocalWarningSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7387 | DataReloadSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7398 | MemoryPressureSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7426 | ResolutionChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7447 | SystemHealthIndexSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7464 | CpuStarvationSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7477 | AcousticPingSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7516 | SwarmDispersedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7529 | SectorResidencyHydratedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7544 | SectorDehydratedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7559 | ChunkDehydratedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7574 | SonarPingSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7585 | HypoxiaSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7597 | OxygenCriticalSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7609 | InteractionUiSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7620 | UIRescaleRequestSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7632 | FluidIncursionSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7643 | SubmarineFloodStateSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7664 | FluidDensityChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7677 | PipeRuptureSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7690 | SpectrumScanSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7702 | RigidbodySleepSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7713 | ScannerToolActiveSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7729 | ScanCompleteSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7741 | LoreFragmentScannedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7751 | BlueprintUnlockedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7763 | CraftingStartedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7775 | CraftingCompletedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7788 | ToolStateChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7808 | ToolLoadoutChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7829 | ToolAcousticSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7846 | PowerDrainSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7859 | ToolTriggerSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7872 | StorageDebtSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7891 | StreamingTurbulenceSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7903 | AtmosphericReentrySignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7922 | PrologueCompleteSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7939 | ManualOverridePulledSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7962 | HUDNotificationSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7974 | DiegeticHudSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7990 | ScanLogChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8010 | PdaExchangeStateChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8031 | VehicleUpgradesChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8048 | ThermalStateChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8064 | BatteryLevelSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8077 | ReconDataSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8088 | SaveLifecycleSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8100 | MacroDatabaseSectorHydrationSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8112 | WfcOutpostGeneratedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 128)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8129 | WfcOutpostStateChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8142 | WfcOutpostDoorPowerSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 96)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8158 | SaveRequestSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8171 | SaveCompletedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8184 | SaveStatusSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8202 | SaveMetadataReadySignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8226 | ComplianceViolationSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8238 | GlobalTimeSyncSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8249 | SeismicSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8263 | TimeDilationSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8275 | SimulationPauseSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8310 | ItemDecaySignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8332 | LightLevelSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8357 | SubmarineLightsChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8384 | FaunaStateChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8397 | PhysiologyStateSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8409 | PlayerStressSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8421 | TraumaSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8434 | CameraPositionSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8444 | CameraFrustumSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8457 | CombatDamageSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8478 | HullDeformedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8501 | HullRepairedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8518 | BaseModuleCompromisedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8540 | PlayerBaseEnterSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8555 | PlayerBaseExitSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8583 | SystemPauseSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8608 | FramePacingWarningSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][90%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8628 | CombatDamageSignalAupShiftTransformer
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:2629 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:2630 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (System.IO.FileStream stream = System.IO.File.Open(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:2737 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3250 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3267 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (System.IO.FileStream stream = System.IO.File.Open(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:7 | PlayerFootstepSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:16 | PlayerWaterSplashSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:28 | WaterTransitionSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 96)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:44 | PlayerExhaleSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:52 | PlayerSprintStateSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:61 | PlayerFatalPressureSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:70 | PlayerTransportBailoutSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:161 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:333 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:335 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:422 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs:18 | _telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<SignalLaneTelemetry> _telemetry;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.

## Non-Claims

- This audit does not prove Unity import, player build, IL2CPP, runtime GC, profiler, scene wiring, or actual struct sizeof(T).
- Static confidence is not semantic proof. The next precision step is an out-of-band Roslyn runner using Assets/Plugins/Roslyn without wiring analyzers into Unity projects.
- This audit intentionally reports legacy/shared ownership debt instead of silently modifying cross-domain contracts.


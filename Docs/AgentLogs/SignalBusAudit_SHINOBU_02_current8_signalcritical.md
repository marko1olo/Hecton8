# SHINOBU_02 Signal Bus Contract Audit CLI

Evidence Class: STATIC_SOURCE_CLASSIFIED
Scope: SignalCritical
Generated UTC: 2026-05-18T20:23:35.3134545Z

## Summary

- Files scanned: 7 C# / 61 compute
- Signal-like definitions found: 177
- Signal definitions still in Core/GlobalSignals.cs: 162
- Pack=1 layouts: 0
- Runtime signal Pack=1 layouts: 0
- Runtime signal transitive Pack=1 field hits: 64
- Signal-like definitions without nearby StructLayout: 1
- Managed event surface hits: 0
- Local native telemetry ring hits: 1
- Registered local telemetry rings: 0
- Local native signal queue hits: 0
- Compute 1024-thread-group hits: 0
- Hot-path heuristic hits: 0
- Cold/fatal sync I/O review hits: 9
- Assembly contract boundary hits: 0
- Errors: 0
- Warnings: 64
- Infos: 10
- Confirmed/probable errors at confidence >= 90: 0
- Review-only findings below confidence 75: 10

## Rule Breakdown

- TRANSITIVE_PACK1_FIELD_REVIEW: total 64, errors 0, warnings 64, infos 0, avg confidence 88
- COLD_OR_FATAL_SYNC_IO_REVIEW: total 9, errors 0, warnings 0, infos 9, avg confidence 64
- EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW: total 1, errors 0, warnings 0, infos 1, avg confidence 56

## Classification Breakdown

- PROBABLE_ARM64_ALIGNMENT_RISK: 64
- COLD_OR_FATAL_IO_BOUNDARY: 9
- EDITOR_ONLY_REVIEW: 1

## Findings

- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:248 | ReentryVfxStateSignal.CapsuleAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `public AbsoluteUniversePosition CapsuleAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:265 | VisorDropletSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:290 | StateCorrectionSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:316 | SyncFenceSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:333 | KccVelocitySignal.BodyAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `public AbsoluteUniversePosition BodyAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:345 | TetherTensionSignal.AnchorAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `public AbsoluteUniversePosition AnchorAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:346 | TetherTensionSignal.PayloadAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `public AbsoluteUniversePosition PayloadAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:362 | TetherSnappedSignal.SnapAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `public AbsoluteUniversePosition SnapAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:395 | DockingRequestSignal.DockAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(8)] public AbsoluteUniversePositionBlit DockAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:410 | DockingCompleteSignal.DockAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(8)] public AbsoluteUniversePositionBlit DockAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:425 | DockingFailedSignal.LastAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(8)] public AbsoluteUniversePositionBlit LastAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:742 | PlayerLookTargetSignal.TargetAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition TargetAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:6622 | ImpactSignal.PointAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PointAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:6648 | HighSpeedImpactSignal.PointAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PointAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:6714 | PlayerStateSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:6854 | ItemAcquiredSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:6867 | RadiationDoseSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:6883 | TemperatureChangedSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:6898 | RadiationSourceSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:6943 | DropPodLandedSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:6953 | WakeGeneratedSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:6962 | FluidImpulseSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:6978 | BubbleSpawnSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:6991 | ProgressionEventSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7011 | GlobalWorldStateSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7024 | BiomeChangedSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7042 | BiomeGradientSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7065 | NarrativeFocusSignal.TargetAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition TargetAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7107 | NarrativeHudWaypointSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7119 | SoundscapeProfileSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7162 | DebrisSpawnSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7189 | EntityDeathSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7206 | EntitySpawnSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7267 | AnomalyProximitySignal.SourceAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `public AbsoluteUniversePosition SourceAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7315 | HabitatConstructionSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7327 | DeconstructRequestSignal.TargetAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition TargetAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7328 | DeconstructRequestSignal.RayOriginAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(48)] public AbsoluteUniversePosition RayOriginAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7342 | DeconstructResultSignal.TargetAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition TargetAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7355 | ModuleDeconstructSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7523 | AcousticPingSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7535 | MovementAcousticSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7548 | SwarmDispersedSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7564 | SectorResidencyHydratedSignal.CenterAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition CenterAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7579 | SectorDehydratedSignal.CenterAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition CenterAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7594 | ChunkDehydratedSignal.CenterAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition CenterAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7606 | SonarPingSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7641 | InteractionUiSignal.TargetAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition TargetAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7664 | FluidIncursionSignal.LeakAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition LeakAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7696 | FluidDensityChangedSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7709 | PipeRuptureSignal.RuptureAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition RuptureAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7734 | RigidbodySleepSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7761 | ScanCompleteSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7941 | AtmosphericReentrySignal.CapsuleAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition CapsuleAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:7958 | PrologueCompleteSignal.CapsuleAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition CapsuleAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:8109 | ReconDataSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:8144 | WfcOutpostGeneratedSignal.OriginAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition OriginAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:8174 | WfcOutpostDoorPowerSignal.DoorAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition DoorAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:8251 | ItemDecaySignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:8298 | SubmarineLightsChangedSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:8325 | FaunaStateChangedSignal.PositionAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition PositionAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:8445 | HullRepairedSignal.HitAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition HitAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:8484 | PlayerBaseEnterSignal.BaseCenterAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition BaseCenterAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/GlobalSignals.cs:8499 | PlayerBaseExitSignal.BaseCenterAup
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(0)] public AbsoluteUniversePosition BaseCenterAup;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:2629 | DumpMasterPipelineTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:2630 | DumpMasterPipelineTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (System.IO.FileStream stream = System.IO.File.Open(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:2737 | ParseMasterExecutionPriorityCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3250 | DumpDispatcherBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3267 | DumpDispatcherBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (System.IO.FileStream stream = System.IO.File.Open(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs:18 | _telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<SignalLaneTelemetry> _telemetry;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [WARN][88%][PROBABLE_ARM64_ALIGNMENT_RISK] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:73 | WaterTransitionSignal.AbsolutePosition
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `[FieldOffset(40)] public AbsoluteUniversePosition AbsolutePosition;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:161 | TryLoadFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:333 | DumpToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:335 | DumpToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:422 | TryLoad
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.

## Non-Claims

- This audit does not prove Unity import, player build, IL2CPP, runtime GC, profiler, scene wiring, or actual struct sizeof(T).
- Static confidence is not semantic proof. This CLI intentionally stays outside Unity and uses standard .NET only.
- This audit reports contract debt only. It does not modify runtime contracts.

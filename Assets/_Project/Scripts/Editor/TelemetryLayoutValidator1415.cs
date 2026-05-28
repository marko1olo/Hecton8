#if UNITY_EDITOR
using System;
using System.Reflection;
using Hecton8.Core;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.Callbacks;

namespace Hecton8.Editor.Validation
{
    [InitializeOnLoad]
    internal static class TelemetryLayoutValidator1415
    {
        private const string Spec = @"PathFunnelTelemetryEntry|LastSectorHash:0,Reserved1:8,Frame:16,PathInvalidationCount:20,LastPathId:24,LastCorridorHash:28,Stress01:32,Reserved0:36,LastCellIndex:40,ActivePathCount:42,InvalidatedPathCount:44,Flags:46,_pad0:48,_pad1:49,_pad2:50,_pad3:51,_pad4:52,_pad5:53,_pad6:54,_pad7:55,_pad8:56,_pad9:57,_pad10:58,_pad11:59,_pad12:60,_pad13:61,_pad14:62,_pad15:63
EncounterDirectorBlackBoxEntry|FrameIndex:0,DirectorStateHash:4,ActiveThreatCount:8,Flags:12,Stress01:16,Intensity01:20,SpawnCredits:24,PlayerSpeed:28,PlayerPosition:32,Padding0:44,_pad0:48,_pad1:49,_pad2:50,_pad3:51,_pad4:52,_pad5:53,_pad6:54,_pad7:55,_pad8:56,_pad9:57,_pad10:58,_pad11:59,_pad12:60,_pad13:61,_pad14:62,_pad15:63
RetinalTelemetryEntry|Frame:0,MaxExposure:4,HottestLightPosition:8,SourceId:20,Reserved:24,TotalBlindPredators:28,ActiveLightCount:30,Flags:31,_pad0:32,_pad1:33,_pad2:34,_pad3:35,_pad4:36,_pad5:37,_pad6:38,_pad7:39,_pad8:40,_pad9:41,_pad10:42,_pad11:43,_pad12:44,_pad13:45,_pad14:46,_pad15:47,_pad16:48,_pad17:49,_pad18:50,_pad19:51,_pad20:52,_pad21:53,_pad22:54,_pad23:55,_pad24:56,_pad25:57,_pad26:58,_pad27:59,_pad28:60,_pad29:61,_pad30:62,_pad31:63
DispatcherPipelineTelemetryEntry|Frame:0,PreSimulationTimeMs:4,SimWaitTimeMs:8,PostSimulationTimeMs:12,VisualSyncTimeMs:16,ActiveBucket:20,SystemCount:24,Flags:28,_pad0:32,_pad1:33,_pad2:34,_pad3:35,_pad4:36,_pad5:37,_pad6:38,_pad7:39,_pad8:40,_pad9:41,_pad10:42,_pad11:43,_pad12:44,_pad13:45,_pad14:46,_pad15:47,_pad16:48,_pad17:49,_pad18:50,_pad19:51,_pad20:52,_pad21:53,_pad22:54,_pad23:55,_pad24:56,_pad25:57,_pad26:58,_pad27:59,_pad28:60,_pad29:61,_pad30:62,_pad31:63
HardwareThermalTelemetryEntry|Frame:0,Sequence:4,ActionMask:8,TemperatureTenthsCelsius:12,Severity:14,BatteryPercent:15,BatteryStatus:16,ThermalStatus:17,Flags:18,Reserved0:19,Reserved1:20,Reserved2:21,Reserved3:22,Reserved4:23,ReservedPadding0:24,ReservedPadding1:32,ReservedPadding2:40,ReservedPadding3:48,ReservedPadding4:56
SeismicTideTelemetryEntry|TimeSeconds:0,TideLevel:8,LastTremorIntensity:12,Direction:16,Flags:28,Sequence:32,Padding0:36,_pad0:40,_pad1:41,_pad2:42,_pad3:43,_pad4:44,_pad5:45,_pad6:46,_pad7:47,_pad8:48,_pad9:49,_pad10:50,_pad11:51,_pad12:52,_pad13:53,_pad14:54,_pad15:55,_pad16:56,_pad17:57,_pad18:58,_pad19:59,_pad20:60,_pad21:61,_pad22:62,_pad23:63
FuzzerTelemetryEntry|TotalOperations:0,CompactionPasses:8,Reserved0:16,Reserved1:24,Reserved2:32,Sequence:40,BufferId:44,Operation:48,Flags:52,ActiveLockMask:56,_pad0:60,_pad1:61,_pad2:62,_pad3:63
JobAdmissionBlackboxEntry|FrameSequence:0,JobHash:4,EstimatedCostMs:8,RemainingBudgetMs:12,CriticalDebtFrames:16,KillSwitchMask:20,Lane:24,Flags:25,Reserved:26,StateHash:28,_pad0:32,_pad1:33,_pad2:34,_pad3:35,_pad4:36,_pad5:37,_pad6:38,_pad7:39,_pad8:40,_pad9:41,_pad10:42,_pad11:43,_pad12:44,_pad13:45,_pad14:46,_pad15:47,_pad16:48,_pad17:49,_pad18:50,_pad19:51,_pad20:52,_pad21:53,_pad22:54,_pad23:55,_pad24:56,_pad25:57,_pad26:58,_pad27:59,_pad28:60,_pad29:61,_pad30:62,_pad31:63
ScalabilityTelemetryEntry|Timestamp:0,RawFrameMs:8,SmoothedFrameMs:12,GlobalQualityWeight:16,VramPressure:20,Flags:24,_pad0:28,_pad1:32,_pad2:33,_pad3:34,_pad4:35,_pad5:36,_pad6:37,_pad7:38,_pad8:39,_pad9:40,_pad10:41,_pad11:42,_pad12:43,_pad13:44,_pad14:45,_pad15:46,_pad16:47,_pad17:48,_pad18:49,_pad19:50,_pad20:51,_pad21:52,_pad22:53,_pad23:54,_pad24:55,_pad25:56,_pad26:57,_pad27:58,_pad28:59,_pad29:60,_pad30:61,_pad31:62,_pad32:63
DrsTelemetryEntry|Frame:0,CurrentScale01:4,TargetScale01:8,FrameTimeEwmaMs:12,SystemStress01:16,SystemStressEwma01:20,SharpenIntensity01:24,Flags:28,Sequence:32,UpscalerComputeTimeMsBits:36,HysteresisCounters:40,FramesBelowTarget:42,PressureLevel:44,ThermalSeverity:45,StpActive:46,AupLockFrames:47,_pad0:48,_pad1:49,_pad2:50,_pad3:51,_pad4:52,_pad5:53,_pad6:54,_pad7:55,_pad8:56,_pad9:57,_pad10:58,_pad11:59,_pad12:60,_pad13:61,_pad14:62,_pad15:63
TBDRPipelineTelemetryEntry|Frame:0,TotalSubmittedVertices:4,MaxVisibleVertices:8,TileSpillWarnings:12,SortComputeTimeMs:16,TilePressure:20,Flags:24,StateHash:28,_pad0:32,_pad1:33,_pad2:34,_pad3:35,_pad4:36,_pad5:37,_pad6:38,_pad7:39,_pad8:40,_pad9:41,_pad10:42,_pad11:43,_pad12:44,_pad13:45,_pad14:46,_pad15:47,_pad16:48,_pad17:49,_pad18:50,_pad19:51,_pad20:52,_pad21:53,_pad22:54,_pad23:55,_pad24:56,_pad25:57,_pad26:58,_pad27:59,_pad28:60,_pad29:61,_pad30:62,_pad31:63
HabitatDeconstructionTelemetryEntry|Frame:0,TargetEntityId:4,RequesterEntityId:8,DistanceMeters:12,DfsVisitedCount:16,DfsExpectedCount:18,Result:20,Reason:21,Flags:22,Reserved:23,_pad0:24,_pad1:25,_pad2:26,_pad3:27,_pad4:28,_pad5:29,_pad6:30,_pad7:31,_pad8:32,_pad9:33,_pad10:34,_pad11:35,_pad12:36,_pad13:37,_pad14:38,_pad15:39,_pad16:40,_pad17:41,_pad18:42,_pad19:43,_pad20:44,_pad21:45,_pad22:46,_pad23:47,_pad24:48,_pad25:49,_pad26:50,_pad27:51,_pad28:52,_pad29:53,_pad30:54,_pad31:55,_pad32:56,_pad33:57,_pad34:58,_pad35:59,_pad36:60,_pad37:61,_pad38:62,_pad39:63
DataArchaeologyTelemetryEntry|Frame:0,Hash:4,Position:8,Match01:20,Reserved1:24,ProgressPermille:28,Flags:30,Reserved0:31,_pad0:32,_pad1:33,_pad2:34,_pad3:35,_pad4:36,_pad5:37,_pad6:38,_pad7:39,_pad8:40,_pad9:41,_pad10:42,_pad11:43,_pad12:44,_pad13:45,_pad14:46,_pad15:47,_pad16:48,_pad17:49,_pad18:50,_pad19:51,_pad20:52,_pad21:53,_pad22:54,_pad23:55,_pad24:56,_pad25:57,_pad26:58,_pad27:59,_pad28:60,_pad29:61,_pad30:62,_pad31:63
HabitatFloodBlackBoxEntry|Frame:0,BaseTotalStress:4,MaxWaterLevel01:8,TotalWaterVolumeM3:12,PeakModuleStress:16,Flags:20,StateHash:24,DeformationSequence:28,NodeCount:32,EdgeCount:34,FloodedRoomCount:36,Reserved0:38,_pad0:40,_pad1:41,_pad2:42,_pad3:43,_pad4:44,_pad5:45,_pad6:46,_pad7:47,_pad8:48,_pad9:49,_pad10:50,_pad11:51,_pad12:52,_pad13:53,_pad14:54,_pad15:55,_pad16:56,_pad17:57,_pad18:58,_pad19:59,_pad20:60,_pad21:61,_pad22:62,_pad23:63
FluidPipeTelemetryEntry|FrameIndex:0,NodeCount:4,RuptureCount:8,NanCount:12,TotalWater:16,TotalOxygen:20,MaxPressureKPa:24,StateHash:28,_pad0:32,_pad1:33,_pad2:34,_pad3:35,_pad4:36,_pad5:37,_pad6:38,_pad7:39,_pad8:40,_pad9:41,_pad10:42,_pad11:43,_pad12:44,_pad13:45,_pad14:46,_pad15:47,_pad16:48,_pad17:49,_pad18:50,_pad19:51,_pad20:52,_pad21:53,_pad22:54,_pad23:55,_pad24:56,_pad25:57,_pad26:58,_pad27:59,_pad28:60,_pad29:61,_pad30:62,_pad31:63
SalinityCorrosionTelemetryEntry|Frame:0,InventoryVersion:4,AverageEquipmentDurability01:8,RustScalar01:12,SalinityFactor:16,CurrentBiomeHash:20,InventoryMaskLow:24,Flags:28,_pad0:32,_pad1:33,_pad2:34,_pad3:35,_pad4:36,_pad5:37,_pad6:38,_pad7:39,_pad8:40,_pad9:41,_pad10:42,_pad11:43,_pad12:44,_pad13:45,_pad14:46,_pad15:47,_pad16:48,_pad17:49,_pad18:50,_pad19:51,_pad20:52,_pad21:53,_pad22:54,_pad23:55,_pad24:56,_pad25:57,_pad26:58,_pad27:59,_pad28:60,_pad29:61,_pad30:62,_pad31:63
PrologueSequenceTelemetryEntry|UniverseSpeedMetersPerSecond:0,PlanetDistanceMeters:8,Frame:16,StateHash:20,Sequence:24,Stage:26,Flags:27,_pad0:28,_pad1:29,_pad2:30,_pad3:31,_pad4:32,_pad5:33,_pad6:34,_pad7:35,_pad8:36,_pad9:37,_pad10:38,_pad11:39,_pad12:40,_pad13:41,_pad14:42,_pad15:43,_pad16:44,_pad17:45,_pad18:46,_pad19:47,_pad20:48,_pad21:49,_pad22:50,_pad23:51,_pad24:52,_pad25:53,_pad26:54,_pad27:55,_pad28:56,_pad29:57,_pad30:58,_pad31:59,_pad32:60,_pad33:61,_pad34:62,_pad35:63
MetaCampaignBlackBoxEntry|Frame:0,StageHash:4,VariableHash:8,Value:12,Toxicity01:16,Sequence:20,ChangeKind:22,Flags:23,_pad0:24,_pad1:25,_pad2:26,_pad3:27,_pad4:28,_pad5:29,_pad6:30,_pad7:31,_pad8:32,_pad9:33,_pad10:34,_pad11:35,_pad12:36,_pad13:37,_pad14:38,_pad15:39,_pad16:40,_pad17:41,_pad18:42,_pad19:43,_pad20:44,_pad21:45,_pad22:46,_pad23:47,_pad24:48,_pad25:49,_pad26:50,_pad27:51,_pad28:52,_pad29:53,_pad30:54,_pad31:55,_pad32:56,_pad33:57,_pad34:58,_pad35:59,_pad36:60,_pad37:61,_pad38:62,_pad39:63
ModCullTelemetryEntry|ModHash:0,EventHash:4,Frame:8,Scalar:12,Reason:16,ActiveSubscriptions:20,_pad0:24,_pad1:25,_pad2:26,_pad3:27,_pad4:28,_pad5:29,_pad6:30,_pad7:31,_pad8:32,_pad9:33,_pad10:34,_pad11:35,_pad12:36,_pad13:37,_pad14:38,_pad15:39,_pad16:40,_pad17:41,_pad18:42,_pad19:43,_pad20:44,_pad21:45,_pad22:46,_pad23:47,_pad24:48,_pad25:49,_pad26:50,_pad27:51,_pad28:52,_pad29:53,_pad30:54,_pad31:55,_pad32:56,_pad33:57,_pad34:58,_pad35:59,_pad36:60,_pad37:61,_pad38:62,_pad39:63
WorldRegrowthTelemetryEntry|DayIndex:0,StateHash:4,MatureCells:8,SeedCells:12,TombstoneCells:16,AverageNutrientQ:20,AverageApexRespawnDays:24,Flags:28,Reserved0:32,Reserved1:36,Reserved2:40,Reserved3:44,_pad0:48,_pad1:49,_pad2:50,_pad3:51,_pad4:52,_pad5:53,_pad6:54,_pad7:55,_pad8:56,_pad9:57,_pad10:58,_pad11:59,_pad12:60,_pad13:61,_pad14:62,_pad15:63
RtgTelemetryEntry|Frame:0,SourceId:4,OutputWatts:8,NormalizedOutput01:12,AverageHealth01:16,ActiveRtgs:20,Flags:22,_pad0:23,_pad1:24,_pad2:25,_pad3:26,_pad4:27,_pad5:28,_pad6:29,_pad7:30,_pad8:31,_pad9:32,_pad10:33,_pad11:34,_pad12:35,_pad13:36,_pad14:37,_pad15:38,_pad16:39,_pad17:40,_pad18:41,_pad19:42,_pad20:43,_pad21:44,_pad22:45,_pad23:46,_pad24:47,_pad25:48,_pad26:49,_pad27:50,_pad28:51,_pad29:52,_pad30:53,_pad31:54,_pad32:55,_pad33:56,_pad34:57,_pad35:58,_pad36:59,_pad37:60,_pad38:61,_pad39:62,_pad40:63
AsyncPersistenceTelemetryEntry|Frame:0,OperationId:4,SaveDurationMs:8,CompressedSizeBytes:12,RawPayloadBytes:16,Flags:20,SlotHash:24,Reserved:28,_pad0:32,_pad1:33,_pad2:34,_pad3:35,_pad4:36,_pad5:37,_pad6:38,_pad7:39,_pad8:40,_pad9:41,_pad10:42,_pad11:43,_pad12:44,_pad13:45,_pad14:46,_pad15:47,_pad16:48,_pad17:49,_pad18:50,_pad19:51,_pad20:52,_pad21:53,_pad22:54,_pad23:55,_pad24:56,_pad25:57,_pad26:58,_pad27:59,_pad28:60,_pad29:61,_pad30:62,_pad31:63
HeadlessTelemetryEntry|GridX:0,GridY:8,GridZ:16,Frame:24,Day:28,StateHash:32,Local:36,PreyBiomass:48,PredatorBiomass:52,NativeBytesMb:56,Flags:60
ReentryVfxTelemetryEntry|Frame:0,Heat01:4,Opacity01:8,AltitudeMeters:12,VelocityMetersPerSecond:16,AmbientBlend01:20,OverlayDistanceMeters:24,StateHash:28,SectorHashLo:32,Reserved2:36,Sequence:40,HydrationSequence:42,Phase:44,QualityWeightByte:45,Flags:46,Reserved:47,_pad0:48,_pad1:49,_pad2:50,_pad3:51,_pad4:52,_pad5:53,_pad6:54,_pad7:55,_pad8:56,_pad9:57,_pad10:58,_pad11:59,_pad12:60,_pad13:61,_pad14:62,_pad15:63
DiegeticHudTelemetryEntry|Frame:0,Power01:4,Brownout01:8,DamageGlitch01:12,Humidity01:16,LocalX:20,LocalY:24,LocalZ:28,Flags:32,_pad0:36,_pad1:37,_pad2:38,_pad3:39,_pad4:40,_pad5:41,_pad6:42,_pad7:43,_pad8:44,_pad9:45,_pad10:46,_pad11:47,_pad12:48,_pad13:49,_pad14:50,_pad15:51,_pad16:52,_pad17:53,_pad18:54,_pad19:55,_pad20:56,_pad21:57,_pad22:58,_pad23:59,_pad24:60,_pad25:61,_pad26:62,_pad27:63
FloraGrowthTelemetryEntry|FrameIndex:0,InstanceCount:4,SampleCount:8,NegativeAgeCount:12,NanAgeCount:16,DirtyUpload:20,MinAge01:24,MaxAge01:28,AgeHash:32,Reserved0:36,_pad0:40,_pad1:41,_pad2:42,_pad3:43,_pad4:44,_pad5:45,_pad6:46,_pad7:47,_pad8:48,_pad9:49,_pad10:50,_pad11:51,_pad12:52,_pad13:53,_pad14:54,_pad15:55,_pad16:56,_pad17:57,_pad18:58,_pad19:59,_pad20:60,_pad21:61,_pad22:62,_pad23:63
ScatterCullTelemetryEntry|FrameIndex:0,TotalInstances:4,FrustumCulledCount:8,OcclusionCulledCount:12,VisibleCount:16,DensityDecimationStep:20,OverdrawWarning:24,SystemStress01:28,MaxDensity01:32,Reserved0:36,_pad0:40,_pad1:41,_pad2:42,_pad3:43,_pad4:44,_pad5:45,_pad6:46,_pad7:47,_pad8:48,_pad9:49,_pad10:50,_pad11:51,_pad12:52,_pad13:53,_pad14:54,_pad15:55,_pad16:56,_pad17:57,_pad18:58,_pad19:59,_pad20:60,_pad21:61,_pad22:62,_pad23:63
TooltipBlackBoxEntry|Frame:0,TargetHash:4,Anchor:8,Alpha:20,SchemeHash:24,GlyphCount:28,Flags:30,TierFlags:31,_pad0:32,_pad1:33,_pad2:34,_pad3:35,_pad4:36,_pad5:37,_pad6:38,_pad7:39,_pad8:40,_pad9:41,_pad10:42,_pad11:43,_pad12:44,_pad13:45,_pad14:46,_pad15:47,_pad16:48,_pad17:49,_pad18:50,_pad19:51,_pad20:52,_pad21:53,_pad22:54,_pad23:55,_pad24:56,_pad25:57,_pad26:58,_pad27:59,_pad28:60,_pad29:61,_pad30:62,_pad31:63
ManualOverrideLeverTelemetryEntry|HandLocalPosition:0,PivotLocalPosition:12,AngleDegrees:24,TargetAngleDegrees:28,VelocityDegreesPerSecond:32,Frame:36,Flags:40,_pad0:41,_pad1:42,_pad2:43,_pad3:44,_pad4:45,_pad5:46,_pad6:47,_pad7:48,_pad8:49,_pad9:50,_pad10:51,_pad11:52,_pad12:53,_pad13:54,_pad14:55,_pad15:56,_pad16:57,_pad17:58,_pad18:59,_pad19:60,_pad20:61,_pad21:62,_pad22:63
SwayTelemetryEntry|Frame:0,Flags:4,WrappedTime:8,FlowMagnitude:12,GlobalQualityWeight:16,AmplitudeMeters:20,StateHash:24,SourceHash:28,_pad0:32,_pad1:33,_pad2:34,_pad3:35,_pad4:36,_pad5:37,_pad6:38,_pad7:39,_pad8:40,_pad9:41,_pad10:42,_pad11:43,_pad12:44,_pad13:45,_pad14:46,_pad15:47,_pad16:48,_pad17:49,_pad18:50,_pad19:51,_pad20:52,_pad21:53,_pad22:54,_pad23:55,_pad24:56,_pad25:57,_pad26:58,_pad27:59,_pad28:60,_pad29:61,_pad30:62,_pad31:63
FrequencyTuningTelemetryEntry|Frame:0,ArtifactHash:4,TargetFrequency:8,TargetAmplitude:12,PlayerFrequency:16,PlayerAmplitude:20,Error01:24,HoldPermille:28,Stage:30,Flags:31,_pad0:32,_pad1:33,_pad2:34,_pad3:35,_pad4:36,_pad5:37,_pad6:38,_pad7:39,_pad8:40,_pad9:41,_pad10:42,_pad11:43,_pad12:44,_pad13:45,_pad14:46,_pad15:47,_pad16:48,_pad17:49,_pad18:50,_pad19:51,_pad20:52,_pad21:53,_pad22:54,_pad23:55,_pad24:56,_pad25:57,_pad26:58,_pad27:59,_pad28:60,_pad29:61,_pad30:62,_pad31:63
MacroSwarmTelemetryEntry|FrameIndex:0,StateHash:4,ActiveMacroSwarms:8,ArrivalCount:12,BiomassSum:16,Flags:20,Reserved0:24,Reserved1:28,_pad0:32,_pad1:33,_pad2:34,_pad3:35,_pad4:36,_pad5:37,_pad6:38,_pad7:39,_pad8:40,_pad9:41,_pad10:42,_pad11:43,_pad12:44,_pad13:45,_pad14:46,_pad15:47,_pad16:48,_pad17:49,_pad18:50,_pad19:51,_pad20:52,_pad21:53,_pad22:54,_pad23:55,_pad24:56,_pad25:57,_pad26:58,_pad27:59,_pad28:60,_pad29:61,_pad30:62,_pad31:63
FaunaMutationTelemetryEntry|FrameIndex:0,StateHash:4,TotalMutatedEntities:8,HeadlessMutatedCount:12,MacroSwarmMutatedCount:16,LastMutationFlags:20,LastRadiationRads:24,LastToxicity01:28,LastBrineDepth01:32,Reserved0:36,Reserved1:40,Reserved2:44,_pad0:48,_pad1:49,_pad2:50,_pad3:51,_pad4:52,_pad5:53,_pad6:54,_pad7:55,_pad8:56,_pad9:57,_pad10:58,_pad11:59,_pad12:60,_pad13:61,_pad14:62,_pad15:63
BiomassTelemetryEntry|FrameIndex:0,StateHash:4,ActiveCellCount:8,Flags:12,GlobalBiomassSum:16,PreyBiomassSum:20,PredatorBiomassSum:24,FloraOvergrowth01:28,_pad0:32,_pad1:33,_pad2:34,_pad3:35,_pad4:36,_pad5:37,_pad6:38,_pad7:39,_pad8:40,_pad9:41,_pad10:42,_pad11:43,_pad12:44,_pad13:45,_pad14:46,_pad15:47,_pad16:48,_pad17:49,_pad18:50,_pad19:51,_pad20:52,_pad21:53,_pad22:54,_pad23:55,_pad24:56,_pad25:57,_pad26:58,_pad27:59,_pad28:60,_pad29:61,_pad30:62,_pad31:63
BiolumTelemetryEntry|Frame:0,CameraPositionX:4,CameraPositionY:8,CameraPositionZ:12,Intensity:16,Phase:20,PredatorDim:24,PredatorHits:28,ActiveRipples:30,Flags:31,_pad0:32,_pad1:33,_pad2:34,_pad3:35,_pad4:36,_pad5:37,_pad6:38,_pad7:39,_pad8:40,_pad9:41,_pad10:42,_pad11:43,_pad12:44,_pad13:45,_pad14:46,_pad15:47,_pad16:48,_pad17:49,_pad18:50,_pad19:51,_pad20:52,_pad21:53,_pad22:54,_pad23:55,_pad24:56,_pad25:57,_pad26:58,_pad27:59,_pad28:60,_pad29:61,_pad30:62,_pad31:63
ActiveSonarGeoTelemetryEntry|Frame:0,ActiveRingCount:4,PrimaryRadius:8,PrimaryCenter:12,Flags:24,_pad0:28,_pad1:29,_pad2:30,_pad3:31,_pad4:32,_pad5:33,_pad6:34,_pad7:35,_pad8:36,_pad9:37,_pad10:38,_pad11:39,_pad12:40,_pad13:41,_pad14:42,_pad15:43,_pad16:44,_pad17:45,_pad18:46,_pad19:47,_pad20:48,_pad21:49,_pad22:50,_pad23:51,_pad24:52,_pad25:53,_pad26:54,_pad27:55,_pad28:56,_pad29:57,_pad30:58,_pad31:59,_pad32:60,_pad33:61,_pad34:62,_pad35:63
WaterlineTelemetryEntry|Frame:0,Sequence:4,RoomId:8,Fill01:12,CurrentWaterlineY:16,TargetWaterlineY:20,CameraY:24,Droplets01:28,StateHash:32,Reserved1:36,Flags:38,Reserved0:39,_pad0:40,_pad1:41,_pad2:42,_pad3:43,_pad4:44,_pad5:45,_pad6:46,_pad7:47,_pad8:48,_pad9:49,_pad10:50,_pad11:51,_pad12:52,_pad13:53,_pad14:54,_pad15:55,_pad16:56,_pad17:57,_pad18:58,_pad19:59,_pad20:60,_pad21:61,_pad22:62,_pad23:63
VisorRefractionTelemetryEntry|FrameIndex:0,Flags:4,EffectIntensity01:8,Wetness01:12,HullStress01:16,WaterDensitySignal01:20,HomeostasisFallback01:24,LocalVelocitySq:28,StateHash:32,VaultGeneration:36,QualityWeightQ16:40,CameraPixelWidth:44,CameraPixelHeight:46,_pad0:48,_pad1:49,_pad2:50,_pad3:51,_pad4:52,_pad5:53,_pad6:54,_pad7:55,_pad8:56,_pad9:57,_pad10:58,_pad11:59,_pad12:60,_pad13:61,_pad14:62,_pad15:63
UberNoirShaderTelemetryEntry|Frame:0,FeatureMask:4,SystemStress01:8,HighCostAllowed01:12,VisualOverkill01:16,QualityWeightByte:20,Flags:24,StateHash:28,PomEnabled01:32,ReservedVisualDetail01:36,Refraction01:40,Reserved0:44,_pad0:48,_pad1:49,_pad2:50,_pad3:51,_pad4:52,_pad5:53,_pad6:54,_pad7:55,_pad8:56,_pad9:57,_pad10:58,_pad11:59,_pad12:60,_pad13:61,_pad14:62,_pad15:63
BiolumPulseTelemetryEntry|Frame:0,ActiveGlowingInstances:4,OscillatorComputeTimeMs:8,GlobalDarknessScalar:12,Group0Phase:16,FrequencyMultiplier:20,PrimaryAmplitudeHdr:24,WavePulsesActive:28,QualityTier:30,Flags:31,_pad0:32,_pad1:33,_pad2:34,_pad3:35,_pad4:36,_pad5:37,_pad6:38,_pad7:39,_pad8:40,_pad9:41,_pad10:42,_pad11:43,_pad12:44,_pad13:45,_pad14:46,_pad15:47,_pad16:48,_pad17:49,_pad18:50,_pad19:51,_pad20:52,_pad21:53,_pad22:54,_pad23:55,_pad24:56,_pad25:57,_pad26:58,_pad27:59,_pad28:60,_pad29:61,_pad30:62,_pad31:63";
        private static readonly MethodInfo SizeOfGenericMethod = ResolveSizeOfGenericMethod();

        static TelemetryLayoutValidator1415()
        {
            ValidateAll();
        }

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            ValidateAll();
        }

        private static void ValidateAll()
        {
            string[] rows = Spec.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < rows.Length; i++)
            {
                ValidateRow(rows[i]);
            }
        }

        private static void ValidateRow(string row)
        {
            int split = row.IndexOf('|');
            if (split <= 0 || split >= row.Length - 1)
                Throw("Malformed telemetry layout row: " + row);

            string typeName = row.Substring(0, split);
            string fields = row.Substring(split + 1);
            Type type = ResolveType(typeName, fields, out string failureDetail);
            if (type == null)
                Throw("Missing telemetry type " + typeName + ": " + failureDetail);

            if (!type.IsExplicitLayout)
                Throw(type.FullName + " must use LayoutKind.Explicit.");

            AssertEqual(64, SizeOf(type), type.FullName + " size");

            string[] fieldRows = fields.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < fieldRows.Length; i++)
            {
                int colon = fieldRows[i].IndexOf(':');
                if (colon <= 0 || colon >= fieldRows[i].Length - 1)
                    Throw("Malformed telemetry field row: " + fieldRows[i]);

                string fieldName = fieldRows[i].Substring(0, colon);
                int expected = ParseInt(fieldRows[i].Substring(colon + 1), fieldRows[i]);
                FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null)
                    Throw(type.FullName + " missing field " + fieldName);

                AssertEqual(expected, UnsafeUtility.GetFieldOffset(field), type.FullName + "." + fieldName + " offset");
            }
        }

        private static Type ResolveType(string typeName, string fields, out string failureDetail)
        {
            failureDetail = string.Empty;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int a = 0; a < assemblies.Length; a++)
            {
                Type[] types;
                try
                {
                    types = assemblies[a].GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }

                if (types == null)
                    continue;

                for (int t = 0; t < types.Length; t++)
                {
                    Type type = types[t];
                    if (type == null || type.Name != typeName)
                        continue;

                    string detail;
                    if (MatchesLayout(type, fields, out detail))
                        return type;

                    failureDetail = detail;
                }
            }

            return null;
        }

        private static bool MatchesLayout(Type type, string fields, out string detail)
        {
            detail = string.Empty;
            if (!type.IsExplicitLayout)
            {
                detail = type.FullName + " is not explicit layout.";
                return false;
            }

            if (SizeOf(type) != 64)
            {
                detail = type.FullName + " size=" + SizeOf(type);
                return false;
            }

            string[] fieldRows = fields.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < fieldRows.Length; i++)
            {
                int colon = fieldRows[i].IndexOf(':');
                if (colon <= 0 || colon >= fieldRows[i].Length - 1)
                {
                    detail = "Malformed field row " + fieldRows[i];
                    return false;
                }

                string fieldName = fieldRows[i].Substring(0, colon);
                int expected = ParseInt(fieldRows[i].Substring(colon + 1), fieldRows[i]);
                FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null)
                {
                    detail = type.FullName + " missing " + fieldName;
                    return false;
                }

                int actual = UnsafeUtility.GetFieldOffset(field);
                if (actual != expected)
                {
                    detail = type.FullName + "." + fieldName + " expected=" + expected + " actual=" + actual;
                    return false;
                }
            }

            return true;
        }

        private static MethodInfo ResolveSizeOfGenericMethod()
        {
            MethodInfo[] methods = typeof(UnsafeUtility).GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name == nameof(UnsafeUtility.SizeOf) &&
                    method.IsGenericMethodDefinition &&
                    method.GetParameters().Length == 0)
                {
                    return method;
                }
            }

            Throw("UnsafeUtility.SizeOf<T>() generic method not found.");
            return null;
        }

        private static int SizeOf(Type type)
        {
            MethodInfo method = SizeOfGenericMethod.MakeGenericMethod(type);
            return (int)method.Invoke(null, null);
        }

        private static int ParseInt(string value, string context)
        {
            if (!int.TryParse(value, out int parsed))
                Throw("Invalid integer in telemetry layout spec: " + context);
            return parsed;
        }

        private static void AssertEqual(int expected, int actual, string label)
        {
            if (expected != actual)
                Throw(label + " expected=" + expected + " actual=" + actual);
        }

        private static void Throw(string message)
        {
            throw new FatalArchitectureException("TELEMETRY_LAYOUT_1415: " + message);
        }
    }
}
#endif

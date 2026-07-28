using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Core.Memory
{
    /// <summary>
    /// Stable native-memory owner identifiers. Values below 256 are reserved for registry service slots.
    /// </summary>
    public enum SystemID : ushort
    {
        Unknown = 0,
        CoreDataVault = 1,
        H8Memory = 2,
        Bootstrap = 3,
        CoreDeterminism = 4,
        CoreBridge = 5,
        SystemDispatcher = 30,
        HardwareHomeostasis = 31,
        GlobalPhysicsStateManager = 32,
        Physics = 64,
        VehiclesPhysics = 65,
        Fluid = 66,
        GameplayLoot = 67,
        HabitatAtmosphere = 68,
        GameplayPlayer = 69,
        GameplayTools = 70,
        Construction = 71,
        Power = 72,
        HullIntegrity = 73,
        GameplayCombat = 74,
        Crafting = 75,
        WorldStreaming = 128,
        TerrainSeams = 129,
        WorldSargassum = 130,
        FloraGenomics = 131,
        SavePersistence = 132,
        WorldSpatialHash = 163,
        AICognition = 144,
        AnimationFauna = 145,
        AIPathfinding = 146,
        AIEcology = 147,
        AISensory = 148,
        JobAdmission = 149,
        AnimationLocomotion = 150,
        TradeMarauders = 151,
        WorldResourceSpawnerRuntime = 157,
        SimulationBucketer = 161,
        AmbientBiota = 162,
        Vfx = 192,
        GraphicsScalability = 193,
        ContentAuthority = 194,
        CoreDiagnostics = 195,
        QuestDag = 196,
        NarrativePoiTriggers = 349,
        PrologueSequence = 350,
        QAEndurance = 351,
        GameplayDebris = 352,
        LoreDatabase = 353,
        QAHeadless = 354,
        MetaCampaign = 355,
        WorldProceduralFieldSampler = 356,
        WorldOutposts = 357,
        GameplayHazards = 358,
        EndgameAnomaly = 197,
        GraphicsMaterials = 198,
        ModSandbox = 199,
        Thermodynamics = 200,
        UI = 224,
        Audio = 258,
        AudioVocalWarning = 259,
        AudioFrameRing = 260,
        AudioPlayerCritical = 261,
        AudioStemMixer = 262,
        AudioDynamicSynth = 263,
        AudioVocalSynthesis = 264,
        GameplayRadiation = 274,
        External = 65534
    }

    /// <summary>
    /// Allocation-free global data-vault buffer identifiers.
    /// </summary>
    public enum BufferID : int
    {
        Unknown = 0,
        Silt = 1,
        RigidbodyAUPs = 2,
        RigidbodyCullingState = 3,
        RigidbodyAwakeResults = 4,
        RigidbodyCullingCommands = 5,
        RigidbodyDistanceSq = 6,
        PhysicsCullingTelemetry = 7,
        DispatcherRaycastHits = 8,
        H8Time = 9,
        TerrainSeamHeightmap = 10,
        PlayerKinematicState = 11,
        PlayerHandIkStates = 315730,
        PlayerHandIkTargets = 315731,
        PlayerHandIkBoneMatrices = 315732,
        PlayerHandIkTelemetryRing = 315733,
        PlayerHandIkTelemetryCursor = 315734,
        PlayerHandIkConfig = 315735,
        PlayerHandIkPublishedStates = 315736,
        AtmosphereLogisticsCellsFront = 71500,
        AtmosphereLogisticsCellsBack = 71501,
        AtmosphereLogisticsNodes = 71502,
        AtmosphereLogisticsConnections = 71503,
        AtmosphereLogisticsEdgeOffsets = 71504,
        AtmosphereLogisticsEdgeDestinations = 71505,
        AtmosphereLogisticsEdgeConductance = 71506,
        AtmosphereLogisticsEdgeWriteCursor = 71507,
        AtmosphereLogisticsConsumers = 71508,
        AtmosphereLogisticsToxicSources = 71509,
        AtmosphereLogisticsVents = 71510,
        AtmosphereLogisticsCounters = 71511,
        AtmosphereLogisticsTuning = 71512,
        AtmosphereLogisticsTelemetryRing = 71513,
        AtmosphereLogisticsOxygenDeltaUnits = 71514,
        AtmosphereLogisticsCarbonDioxideDeltaUnits = 71515,
        AtmosphereLogisticsNitrogenDeltaUnits = 71516,
        AtmosphereLogisticsToxinDeltaUnits = 71517,
        AtmosphereLogisticsTemperatureDeltaMilli = 71518,
        AtmosphereLogisticsGasRemainders = 71519,
        AtmosphereLogisticsShaderPayload = 71520,
        AtmosphereLogisticsCsvScratch = 71521,
        AtmosphereLogisticsProfiles = 71522,
        RoomWaterLevels = 12,
        EntityAUPs = 13,
        VoxelSdfTexture3D = 14,
        RoomVolumes = 15,
        RoomLocalAUPs = 16,
        OceanGerstnerWaves = 17,
        OceanGerstnerWaveMeta = 18,
        VoxelSurfaceNetsColliderVertices = 81000,
        VoxelSurfaceNetsColliderIndices = 81001,
        VoxelSurfaceNetsColliderCellVertexMap = 81002,
        WfcOutpostGrid = 19,
        LoreEntityAUPs = 20,
        LoreEntityHashes = 21,
        SubmarineBallastFill01 = 22,
        SubmarineBallastTankLocalPositions = 23,
        SubmarineBallastPidOutput = 24,
        SubmarineDynamicFloodMassOutput = 25,
        SubmarinePidTelemetry = 26,
        CarveDebris = 27,
        CarveDebrisVelocity = 28,
        EntityFlags = 29,
        EntityVelocities = 30,
        EntityItemHashes = 31,
        EntityQuantities = 32,
        EntityLootMagnetTelemetry = 33,
        EntityLootMagnetSignalEvents = 34,
        SubmarineFluidCompartmentFloodVolumes = 35,
        SubmarineFluidCompartmentViscosity01 = 36,
        SubmarineFluidCompartmentBaseMaxVolumes = 37,
        SubmarineFluidCompartmentMaxVolumes = 38,
        SubmarineFluidCompartmentBreachAreas = 39,
        SubmarineFluidCompartmentLocalCentroids = 40,
        SubmarineFluidCompartmentFlags = 41,
        SubmarineFluidBulkheadPairs = 42,
        SubmarineFluidBulkheadSealed = 43,
        SubmarineFluidBulkheadDoorAreas = 44,
        SubmarineFluidComAccumulatorFront = 45,
        SubmarineFluidComAccumulatorBack = 46,
        SubmarineFluidMassPropertiesFront = 47,
        SubmarineFluidMassPropertiesBack = 48,
        SubmarineFluidAngularVelocityHistoryLocal = 49,
        SubmarineFluidPreviousExteriorSampleSubmersionFactors = 50,
        SubmarineFluidJobFloodVolumes = 51,
        SubmarineFluidJobCompartmentFlags = 52,
        SubmarineFluidBulkheadTransferDeltas = 53,
        SubmarineHydroKinematicInput = 54,
        SubmarineHydroKinematicOutput = 55,
        SubmarineHydroBlackBox = 56,
        PlayerKinematicPositions = 57,
        PlayerKinematicVelocities = 58,
        PlayerKinematicIntendedMovements = 59,
        PlayerKinematicDragSolvedVelocities = 60,
        PlayerKinematicTelemetryRing = 61,
        PlayerCinematicFocusBlackBox = 62,
        HabitatBaseAwakeState = 63,
        CarveDebrisJobState = 64,
        CarveDebrisRequests = 65,
        CarveDebrisBlackBox = 66,
        TetherCablePositions = 67,
        TetherCablePreviousPositions = 68,
        TetherCableVelocities = 69,
        TetherCableMasses = 70,
        TetherCableSegmentTensions = 71,
        TetherCableBlackBox = 72,
        FloraScatterMatrices = 73,
        FloraScatterMetadata = 74,
        FloraScatterMotionVectors = 75,
        HullDents = 76,
        CompassState = 77,
        CompassHeadingOutput = 78,
        CompassBlackBox = 79,
        JawIkTargets = 80,
        CurrentJawPos = 81,
        BiteIkSolveEvents = 82,
        BiteIkTelemetryCursor = 83,
        WakeSources = 84,
        AlphaLeviathanCognitionState = 85,
        AlphaLeviathanSensoryStimulus = 86,
        AlphaLeviathanSteeringOutput = 87,
        AlphaLeviathanTelemetryRing = 88,
        AlphaLeviathanTelemetryCursor = 89,
        ResolutionScaleState = 90,
        BiotaAUPs = 91,
        BiotaVelocities = 92,
        BiotaStates = 93,
        ToolRuntimeHeat01 = 94,
        ToolRuntimeBatteryCharge = 95,
        WfcDoorCutProgress01 = 96,
        WfcLaserCutBlackBox = 97,
        SimulationBucketEntityFront = 98,
        SimulationBucketEntityWork = 99,
        SimulationBucketEntityCostEwma = 100,
        SimulationBucketLoadEwma = 101,
        SimulationBucketRebalanceResult = 102,
        SimulationBucketFrameState = 103,
        SimulationBucketRebalanceLoads = 104,
        SargassumStaticObstacleCache = 105,
        SargassumBoidState = 106,
        SargassumLeviathanPathScratch = 107,
        SargassumLeviathanNodeFront = 108,
        SargassumLeviathanNodeBack = 109,
        SargassumLeviathanNodeCount = 110,
        SargassumFoveatedSimulationInput = 111,
        SargassumFoveatedSimulationFront = 112,
        SargassumFoveatedSimulationBack = 113,
        SargassumSimulationFrame = 114,
        SargassumFoodChainTelemetryRing = 115,
        SargassumThreatGridUpload = 116,
        SargassumThreatVoxelUpload = 117,
        SargassumInactiveSwarmRing = 118,
        SargassumInactiveSwarmCenterRing = 119,
        SargassumBoidSensoryThreats = 120,
        WorldSpatialAcousticDensityMap = 1419042,
        LadderAUPs = 121,
        HardwareMetrics = 122,
        ShaderGlobalState = 123,
        HandTargetAUP = 124,
        HandActualAUP = 125,
        HandGrabState = 126,
        HandIkTelemetryRing = 127,
        HandIkTelemetryCursor = 128,
        FoveatedRenderBlackBox = 129,
        PredatorRetinalExposure = 130,
        PredatorRetinalBlindnessState = 131,
        PredatorRetinalLastPublishedBlindnessState = 132,
        PredatorRetinalLightSources = 133,
        PredatorRetinalTelemetryRing = 134,
        LockstepArrayHashes = 135,
        LockstepMasterStateHash = 136,
        LockstepMasterFlags = 137,
        LockstepTelemetryRing = 138,
        LockstepReplayInputRing = 139,
        LockstepRigidbodyElementHashes = 140,
        LockstepPlayerElementHashes = 141,
        LockstepRoomElementHashes = 142,
        LockstepEntityElementHashes = 143,
        LockstepRigidbodyElementFlags = 144,
        LockstepPlayerElementFlags = 145,
        LockstepRoomElementFlags = 146,
        LockstepEntityElementFlags = 147,
        LockstepGhostReplayHeaders = 148,
        LockstepGhostReplayInputs = 149,
        VehicleDockingActiveSplines = 150,
        VisorRefractionBlackBox = 151,
        WakeGlobalBuffer = 152,
        WakeVectorBuffer = 153,
        WakeBlackBox = 154,
        LadderClimbIkInput = 155,
        LadderClimbIkOutput = 156,
        LadderClimbIkTelemetryRing = 157,
        LadderClimbIkTelemetryCursor = 158,
        BiotaTelemetryRing = 159,
        BiotaTelemetryCursor = 160,
        FloraScatterBlackBox = 161,
        FloraScatterCpuFrustumPlanes = 162,
        FloraScatterCpuVisibilityMask = 163,
        HardwareFrameTimes = 164,
        HomeostasisBlackBox = 165,
        HardwareThermalSeverity = 166,
        HardwareThermalBlackBox = 167,
        PlayerKinematicFlowVelocity = 168,
        PlayerKinematicLastValidPositions = 169,
        PlayerKinematicSyncReadState = 170,
        PlayerKinematicSyncWriteState = 171,
        PlayerKinematicHandTargets = 172,
        PlayerKinematicSmoothedHandTargets = 173,
        PlayerKinematicRuntimeTelemetryRing = 174,
        PlayerKinematicRuntimeTelemetryCursor = 175,
        PlayerKinematicFaultFlags = 176,
        PlayerKinematicHandProbeCommands = 177,
        PlayerKinematicHandProbeHits = 178,
        PlayerKinematicSdfSqueezeResults = 179,
        LeviathanSegmentPositions = 180,
        LeviathanPreviousSegmentPositions = 181,
        LeviathanBoneMatrices = 182,
        LeviathanTerrainIkTelemetryRing = 183,
        LeviathanTerrainIkTelemetryCursor = 184,
        SargassumBoidSensoryBlackBox = 185,
        PlayerMotorReserved186 = 186,
        PlayerMotorReserved187 = 187,
        PlayerMotorReserved188 = 188,
        PlayerMotorReserved189 = 189,
        HandPresenceInput = 190,
        HandPresenceOutput = 191,
        LockstepMasterHashHistory = 192,
        LockstepMasterHashHistoryCursor = 193,
        SimulationBucketBlackBox = 194,
        PathFunnelActivePaths = 195,
        PathFunnelCellMasks = 196,
        PathFunnelInvalidations = 197,
        PathFunnelTelemetryRing = 198,
        PathFunnelRuntimeState = 199,
        BiolumProfileFloats = 200,
        BiolumGlobalStates = 201,
        BiolumBlackBox = 202,
        SubmarineStructuralBreaches = 203,
        SubmarineDamageControlBlackBox = 204,
        EcosystemPopulationCoefficients = 205,
        EcosystemPopulationSectorState = 206,
        EcosystemPopulationCullEvents = 207,
        EcosystemPopulationTelemetryRing = 208,
        EcosystemPopulationFreeRing = 209,
        EcosystemPopulationCounters = 210,
        ResolutionScaleTelemetry = 211,
        DrsState = 70669,
        TetherCableBlackBoxHead = 212,
        MarineSnowWakeJobResult = 213,
        MarineSnowTelemetryRing = 214,
        EcosystemMacroSwarms = 215,
        EcosystemMacroSwarmArrivals = 216,
        EcosystemMacroSwarmCounters = 217,
        EcosystemMacroSwarmBlackBox = 218,
        EcosystemMacroSwarmMutationRadiation = 219,
        EcosystemMacroSwarmMutationToxicity = 220,
        EcosystemMacroSwarmMutationBrine = 221,
        EcosystemMacroSwarmMutationResults = 222,
        EcosystemMacroHydrationScratch = 223,
        EcosystemMacroDehydrationScratch = 224,
        BiotaMacroHydrationCounters = 225,
        ContentAuthorityBlackBox = 226,
        ContentAuthorityTelemetryCursor = 227,
        WakeTrailStampCommands = 228,
        AcousticEchoFrameTaps = 229,
        AcousticEchoTrailState = 230,
        AcousticEchoBlackBox = 231,
        TetherManagerBlackBox = 232,
        TetherManagerBlackBoxHead = 233,
        ToolHapticFrontCommands = 234,
        ToolHapticBackCommands = 235,
        PredatorCognitionCores = 236,
        PredatorCognitionControls = 237,
        PredatorCognitionInputs = 238,
        PredatorCognitionOutputs = 239,
        PredatorCognitionMemoryBank = 240,
        PredatorCognitionAcousticMemoryBank = 241,
        PredatorCognitionSlotUsed = 242,
        PredatorCognitionAmbientThreats = 243,
        PredatorCognitionSwarmCenters = 244,
        PredatorCognitionSwarmDirections = 245,
        PredatorCognitionSwarmAvoidances = 246,
        PredatorCognitionSwarmCounts = 247,
        PredatorCognitionClaimedBoidIndices = 248,
        PredatorCognitionClaimedBoidPositions = 249,
        PredatorCognitionChosenStates = 250,
        PredatorCognitionStalkingPhases = 251,
        PredatorCognitionStalkingPhaseStartTimes = 252,
        PredatorCognitionPackTargets = 253,
        PredatorCognitionPackWeights = 254,
        PredatorCognitionPackBaitPositions = 255,
        PredatorCognitionPackSharedPlayerPositions = 256,
        PredatorCognitionPackTargetAups = 257,
        PredatorCognitionPackRoles = 258,
        PredatorCognitionBoidClaimTable = 259,
        PredatorCognitionPackBaitClaimTable = 260,
        PredatorCognitionPackFlankerClaimTable = 261,
        PredatorCognitionHabitatSiegeTargets = 262,
        PredatorCognitionBaseSiegeRammerClaimTable = 263,
        PredatorCognitionBaseSiegeDistractorClaimTable = 264,
        PredatorCognitionBaseSiegeLoitererClaimTable = 265,
        PredatorCognitionEvaluationDueFlags = 266,
        PredatorCognitionNextEvaluationTimes = 267,
        PredatorCognitionEvaluationIntervals = 268,
        ContentAuthorityBundleRefs = 269,
        ContentAuthorityBundleRefCount = 270,
        VehicleDockingTelemetryRing = 271,
        CameraJuiceTelemetryRing = 272,
        MaterialDecayBlackBox = 273,
        FloraScatterAge01 = 274,
        FloraScatterPhaseSeeds = 275,
        ShaderFeatureTelemetryRing = 276,
        ArchitectEyeQuadInstances = 277,
        ArchitectEyeSignalTelemetry = 278,
        ArchitectEyeBlackBox = 279,
        ArchitectEyeSectorHashes = 280,
        ArchitectEyeRuntimeState = 281,
        ArchitectEyeSdfSamples = 282,
        JobAdmissionLaneBudgets = 283,
        JobAdmissionBaseRefill = 284,
        JobAdmissionJobHashes = 285,
        JobAdmissionEwmaCosts = 286,
        JobAdmissionBlackBox = 287,
        EcosystemSectorFrontStates = 288,
        EcosystemSectorBackStates = 289,
        EcosystemPreyFrontCounts = 290,
        EcosystemPreyBackCounts = 291,
        EcosystemPredatorFrontCounts = 292,
        EcosystemPredatorBackCounts = 293,
        EcosystemPreyBiomassFront = 294,
        EcosystemPreyBiomassBack = 295,
        EcosystemPredatorBiomassFront = 296,
        EcosystemPredatorBiomassBack = 297,
        EcosystemBiomassCarryingCapacity = 298,
        EcosystemBiomassSumScratch = 299,
        EcosystemBiomassMacroCellCoords = 300,
        EcosystemBiomassCellFlags = 301,
        EcosystemPendingBiomassImpacts = 302,
        EcosystemBiomassBlackBox = 303,
        EcosystemFaunaMutationBlackBox = 304,
        EcosystemHeadlessPositions = 305,
        EcosystemHeadlessSpeciesId = 306,
        EcosystemHeadlessHunger = 307,
        EcosystemHeadlessSectorCoord = 308,
        EcosystemHeadlessSectorId = 309,
        EcosystemHeadlessFaunaGenomes = 310,
        EcosystemHeadlessMutationRadiation = 311,
        EcosystemHeadlessMutationToxicity = 312,
        EcosystemHeadlessMutationBrine = 313,
        EcosystemHeadlessMutationStableHashes = 314,
        EcosystemHeadlessMutationResults = 315,
        EcosystemFaunaGeneticsTelemetry = 70510,
        EcosystemFaunaGeneticsTuning = 70511,
        EcosystemFaunaGeneticsProfiles = 70512,
        EcosystemFaunaGeneticsCsvScratch = 70513,
        EcosystemApexTerritorySamples = 316,
        EcosystemApexTerritoryOverlapResults = 317,
        EcosystemApexSpawnGateCommands = 318,
        EcosystemApexSpawnGateHits = 319,
        EcosystemFloraPredatorAupUpload = 320,
        ContentAuthorityPendingLoads = 321,
        ContentAuthorityPendingLoadCount = 322,
        TetherVisualSegmentPositions = 323,
        TetherVisualAnchorPositions = 324,
        TetherVisualSegmentLengths = 325,
        TetherVerletPositions = 326,
        TetherVerletPreviousPositions = 327,
        TetherVerletVelocities = 328,
        TetherVerletPinnedPositions = 329,
        TetherVerletPinnedMask = 330,
        TetherVerletSegmentRestLengths = 331,
        TetherVerletSegmentTensions = 332,
        TetherVerletCorrections = 333,
        TetherVerletCorrectionWeights = 334,
        TetherVerletSolverStats = 335,
        TetherVerletSolverFlags = 336,
        TetherVerletNodeFaultFlags = 337,
        FaunaCorpseSinkKinematicInput = 338,
        FaunaCorpseSinkKinematicOutput = 339,
        RepairToolBlackBox = 340,
        ToolDurabilityItemStates = 341,
        ToolDurabilityPendingDecay = 342,
        ToolDurabilityWearMultipliers = 343,
        ToolDurabilitySlotActive = 344,
        ToolDurabilityBreakdownFlags = 345,
        VehicleDockingTelemetryCursor = 346,
        UnderwaterBiomeFogSamples = 347,
        UnderwaterBiomeFogSources = 348,
        UnderwaterBiomeFogFromAup = 349,
        UnderwaterBiomeFogToAup = 350,
        UnderwaterBiomeFogPlayerAup = 351,
        UnderwaterBiomeFogResults = 352,
        BridgePrefabMapping = 353,
        BridgeDesignFacadeValues = 354,
        BridgeDesignFacadeTelemetryRing = 355,
        BridgeInputFacadeBindings = 356,
        BridgePrefabLoreLinks = 357,
        BridgeFacadeMacroHeader = 358,
        EcosystemSaveSnapshotSectors = 359,
        EcosystemSaveSnapshotBiomassRuns = 360,
        SargassumKillSignals = 361,
        SargassumKillSignalCount = 362,
        PredatorCognitionActiveSlots = 363,
        SpatialAudioRadarIntensityBins = 364,
        SpatialAudioRadarGrid = 365,
        SpatialAudioVirtualVoiceSelections = 366,
        SpatialAudioVirtualVoiceStatistics = 367,
        SpatialAudioVirtualVoiceBlackBox = 368,
        SpatialAudioPortalNodes = 369,
        SpatialAudioPortalEdges = 370,
        SpatialAudioPortalResult = 371,
        SpatialAudioPortalCosts = 372,
        SpatialAudioPortalCameFrom = 373,
        SpatialAudioPortalStates = 374,
        SpatialAudioPortalBlackBox = 375,
        AudioVocalWarningQueue = 376,
        AudioVocalWarningFlags = 377,
        AudioVocalWarningCooldowns = 378,
        AudioVocalWarningSeverity = 379,
        AudioVocalWarningSourceIds = 380,
        AudioVocalWarningTelemetry = 381,
        FloraScatterVisualPayload = 382,
        AudioFrameRingFrames = 383,
        AudioFrameRingSharedState = 384,
        AudioFrameRingTelemetry = 72700,
        AudioStemState = 70800,
        AudioStemCommands = 70801,
        AudioStemMixFrame = 70802,
        AudioStemRules = 70803,
        AudioStemMockPredator = 70804,
        AudioStemMockDepth = 70805,
        AudioStemTelemetry = 70806,
        AudioStemTelemetryCursor = 70807,
        AudioStemCsvScratch = 70808,
        AudioStemMockTension = 70809,
        AudioDynamicSynthVoices = 71700,
        AudioDynamicSynthScalar = 71701,
        AudioDynamicSynthTuning = 71702,
        AudioDynamicSynthOutputA = 71703,
        AudioDynamicSynthOutputB = 71704,
        AudioDynamicSynthBiquad = 71705,
        AudioDynamicSynthTelemetry = 71706,
        AudioDynamicSynthTelemetryCursor = 71707,
        AudioDynamicSynthCsvScratch = 71708,
        AudioDynamicSynthPresetRules = 71709,
        AudioDynamicSynthGrainBank = 71710,
        AudioDynamicSynthSharedState = 71711,
        AudioVocalSynthesisState = 72420,
        AudioVocalSynthesisCodecState = 72421,
        AudioVocalSynthesisTelemetry = 72422,
        AudioVocalSynthesisTelemetryCursor = 72423,
        AudioVocalSynthesisWaveform = 72424,
        AudioVocalSynthesisWaveformCursor = 72425,
        AudioVocalSynthesisMockBankBytes = 72426,
        AudioVocalSynthesisMockBankRecords = 72427,
        AudioVocalSynthesisCsvMetadata = 72428,
        AudioVocalSynthesisCsvScratch = 72429,
        SpatialAudioVirtualVoiceTuning = 72430,
        SpatialAudioVirtualVoiceWritePool = 72431,
        SpatialAudioVirtualVoiceSortPool = 72432,
        SpatialAudioVirtualVoiceDtoPool = 72433,
        SpatialAudioVirtualVoiceSortKeyPool = 72434,
        SpatialAudioAcousticSourceWritePool = 72435,
        SpatialAudioAcousticSourceSortPool = 72436,
        SpatialAudioAcousticPreviousAupWritePool = 72437,
        SpatialAudioAcousticPreviousAupSortPool = 72438,
        SpatialAudioAcousticDspOutputPool = 72439,
        SpatialAudioAcousticMaterialRows = 72440,
        SpatialAudioAcousticSelectedSourcePool = 72441,
        SpatialAudioAcousticSelectedPreviousAupPool = 72442,
        SpatialAudioPortalOpenSet = 72443,
        SpatialAudioPortalClosedSet = 72444,
        SpatialAudioPreviousVelocityAups = 72445,
        SpatialAudioPreviousVelocityAupFrames = 72446,
        Shinobu303SteeringParams = 72500,
        Shinobu303SteeringAvoidance = 72501,
        Shinobu303SteeringWhiskers = 72502,
        Shinobu303KinematicStates = 72503,
        Shinobu303SteeringTelemetryRing = 72504,
        Shinobu303SteeringTelemetryCursor = 72505,
        Shinobu303MockSdf = 72506,
        Shinobu303SdfConfig = 72507,
        Shinobu303SteeringProfiles = 72508,
        Shinobu303CsvScratch = 72509,
        ShinobuSuitIntegrityStates = 72510,
        ShinobuSuitIntegrityProfiles = 72511,
        ShinobuSuitIntegrityTuning = 72512,
        ShinobuSuitIntegrityTelemetryRing = 72513,
        ShinobuSuitIntegrityVisuals = 72514,
        ShinobuSuitIntegrityMockAups = 72515,
        ShinobuSuitIntegrityCsvScratch = 72516,
        ShinobuSuitIntegrityDumpScratch = 72517,
        Shinobu220BulkheadStates = 72000,
        Shinobu220BulkheadAups = 72001,
        Shinobu220BulkheadPlanes = 72002,
        Shinobu220BulkheadCsrEdges = 72003,
        Shinobu220BulkheadEdgeConductivity = 72004,
        Shinobu220BulkheadFluidFlow = 72005,
        Shinobu220BulkheadTuning = 72006,
        Shinobu220BulkheadTelemetryRing = 72007,
        Shinobu220BulkheadTelemetryCursor = 72008,
        Shinobu220BulkheadCollisionResults = 72009,
        Shinobu220BulkheadProfiles = 72010,
        Shinobu220BulkheadCsvScratch = 72011,
        Shinobu220BulkheadShaderUpload = 72012,
        Shinobu220BulkheadModuleIntegrity = 72013,
        Shinobu220BulkheadIntentRing = 72014,
        Shinobu220BulkheadIntentControl = 72015,
        Shinobu336TeardownTransactions = 72016,
        Shinobu336RefundCommands = 72017,
        Shinobu336LootCaches = 72018,
        Shinobu336TelemetryRing = 72019,
        Shinobu336TelemetryCursor = 72020,
        Shinobu336RefundProfiles = 72021,
        Shinobu336CsvScratch = 72022,
        Shinobu336Counters = 72023,
        Shinobu343HatchStates = 72024,
        Shinobu343HatchTelemetryRing = 72025,
        Shinobu343HatchTelemetryCursor = 72026,
        Shinobu343HatchTuning = 72027,
        Shinobu343HatchProfiles = 72028,
        Shinobu343HatchCsvScratch = 72029,
        Shinobu343HatchShaderUpload = 72030,
        Shinobu343HatchMockFluidCompartments = 72031,
        ShinobuModSandboxBlackboxMemory = 70900,
        ShinobuModSandboxTuning = 70901,
        ShinobuModSandboxTelemetryRing = 70902,
        ShinobuModSandboxTelemetryCursor = 70903,
        ShinobuModSandboxCsvScratch = 70904,
        ShinobuModSandboxPendingRing = 70905,
        ShinobuModSandboxDevNullRing = 70906,
        ShinobuModSandboxStaging = 70907,
        ShinobuModSandboxStats = 70908,
        ShinobuModSandboxOpcodeRecords = 70909,
        ShinobuModSandboxModCounters = 70910,
        ShinobuModSandboxMemoryLeases = 70911,
        ShinobuModSandboxApprovedAssets = 70912,
        ShinobuModSandboxRingState = 70913,
        ShinobuModProjectionCullTelemetryRing = 70921,
        InteractionRaycastScheduledCommands = 385,
        InteractionRaycastScheduledHits = 386,
        InteractionRaycastStagingCommands = 387,
        InteractionSignalQueue = 388,
        PredatorCognitionSpeciesTargetIds = 389,
        PredatorCognitionSpeciesTargetPositions = 390,
        PredatorCognitionSpeciesTargetCount = 391,
        PredatorCognitionSpeciesTuningIds = 392,
        PredatorCognitionSpeciesTuningValues = 393,
        PredatorCognitionSpeciesTuningCount = 394,
        EcosystemSectorFoodHeatmapR8 = 395,
        PlayerCriticalHullScratch = 396,
        PlayerCriticalSonarScratch = 397,
        PlayerCriticalImpactEchoScratch = 398,
        PlayerCriticalThrusterScratch = 399,
        PlayerCriticalHeartbeatScratch = 400,
        PlayerCriticalHeartbeatDuckScratch = 401,
        PlayerCriticalBubbleScratch = 402,
        PlayerCriticalMixScratch = 403,
        PlayerCriticalStereoMixScratch = 404,
        PlayerCriticalSonarEchoDelay = 405,
        PlayerCriticalPendingSonarEchoTapsA = 406,
        PlayerCriticalPendingSonarEchoTapsB = 407,
        PlayerCriticalWorkerSonarEchoTaps = 408,
        PlayerCriticalSonarEchoReadCursors = 409,
        PlayerCriticalSonarEchoFilterInput1 = 410,
        PlayerCriticalSonarEchoFilterInput2 = 411,
        PlayerCriticalSonarEchoFilterOutput1 = 412,
        PlayerCriticalSonarEchoFilterOutput2 = 413,
        PlayerCriticalSonarEchoCompositeCandidatesA = 414,
        PlayerCriticalSonarEchoCompositeCandidatesB = 415,
        PlayerCriticalSonarEchoCompositeGroups = 416,
        PlayerCriticalSonarEchoCompositeGroupCount = 417,
        PlayerCriticalSonarEcholocationHits = 418,
        PlayerCriticalImpactClangDelay = 419,
        PlayerCriticalThrusterCombDelay = 420,
        PlayerCriticalSabineReverbDelay = 421,
        PlayerCriticalCaveConvolutionImpulse = 422,
        PlayerCriticalCaveConvolutionDelay = 423,
        PlayerCriticalInteriorFdnDelay = 424,
        PlayerCriticalBinauralDelayRing = 425,
        PlayerCriticalBinauralShadowHistory = 426,
        PlayerCriticalLowPassInputHistory1 = 427,
        PlayerCriticalLowPassInputHistory2 = 428,
        PlayerCriticalLowPassOutputHistory1 = 429,
        PlayerCriticalLowPassOutputHistory2 = 430,
        PlayerCriticalMetallicGrainBank = 431,
        PlayerCriticalGranularVoiceActive = 432,
        PlayerCriticalGranularVoiceElapsed = 433,
        PlayerCriticalGranularVoiceLength = 434,
        PlayerCriticalGranularVoiceStart = 435,
        PlayerCriticalGranularVoiceSeed = 436,
        PlayerCriticalGranularVoiceCursor = 437,
        PlayerCriticalGranularVoicePlaybackRate = 438,
        PlayerCriticalGranularVoiceGain = 439,
        PlayerCriticalGranularTelemetryRing = 440,
        PlayerCriticalPrologueTransitionTelemetryRing = 441,
        PlayerCriticalVwsClipSamplesA = 442,
        PlayerCriticalVwsClipSamplesB = 443,
        SubmarineFluidCompartmentStates = 444,
        DroneFleetCullingStates = 445,
        LeviathanTentaclePositions = 446,
        LeviathanTentaclePreviousPositions = 447,
        LeviathanTentacleRadius = 448,
        LeviathanTentacleSegmentMatrices = 449,
        LeviathanTentacleStretchFractions = 450,
        LeviathanTentacleConstraintCorrections = 451,
        LeviathanTentacleConstraintCorrectionCounts = 452,
        LeviathanTentacleRootPositions = 453,
        LeviathanTentacleTargetPositions = 454,
        LeviathanTentacleRootAups = 455,
        LeviathanTentacleTargetAups = 456,
        LeviathanTentacleStates = 457,
        LeviathanTentacleTelemetryRing = 458,
        LeviathanProceduralBoneConstraints = 71000,
        LeviathanCreatureColliderProxies = 71001,
        LeviathanRigCsvScratch = 71002,
        LeviathanProceduralRigState = 71003,
        PhysicsForceCommandFront = 459,
        PhysicsForceCommandBack = 460,
        PhysicsForceValidationPackets = 461,
        PhysicsForceValidationMask = 462,
        SystemDispatcherRaycastPendingCommands = 463,
        SystemDispatcherRaycastScheduledCommands = 464,
        SystemDispatcherBlackBox = 465,
        SystemDispatcherBlackBoxCursor = 466,
        CompassPresentationState = 467,
        RigidbodyLastValidPositions = 468,
        PhysicsImpactEvents = 469,
        StaticDataTelemetryRing = 470,
        StaticDataTelemetryCursor = 471,
        EcosystemSectorIndexEntries = 472,
        EcosystemBiomassIndexEntries = 473,
        FloatingOriginDriftRuntimePositions = 474,
        FloatingOriginDriftAbsolutePositions = 475,
        FloatingOriginDriftInvalidMask = 476,
        ProceduralCrabLegEntities = 477,
        ProceduralCrabLegFootPositions = 478,
        ProceduralCrabLegTargetFootPositions = 479,
        ProceduralCrabLegStepStates = 480,
        ProceduralCrabLegSurfaceProbeScratchA = 481,
        ProceduralCrabLegSurfaceProbeScratchB = 482,
        ProceduralCrabLegSurfaceProbeMask = 483,
        ProceduralCrabBodyPoses = 484,
        ProceduralCrabSolvedJointMatrices = 485,
        ProceduralCrabIkTelemetryRing = 486,
        SubmarineFluidExteriorThermalCenters = 487,
        SubmarineFluidExteriorThermalTemperatures = 488,
        SubmarineFluidExteriorThermalLifetimes = 489,
        SubmarineFluidExteriorThermalHazardIds = 490,
        SubmarineFluidExteriorBuoyancySampleLocalPoints = 491,
        SurfaceWeatherJobOutput = 492,
        SurvivalPhysiologyScalarResult = 493,
        SargassumGrazingAnchors = 494,
        SargassumMassiveThreats = 495,
        SargassumFormationBeacons = 496,
        SargassumFormationObstacles = 497,
        BiolumLegacyPredatorPositions = 498,
        BiolumLegacyPredatorScores = 499,
        BiolumLegacyRipplePositions = 500,
        BiolumLegacyRippleDistances = 501,
        BiolumLegacyTelemetryRing = 502,
        SuitUpgradeResolverResult = 504,
        DeployableSdfDrillExtractionResult = 505,
        LightShaftTopContributions = 506,
        LightShaftHistoryContributions = 507,
        LightShaftTelemetryRing = 508,
        DataArchaeologyUnlockedLoreWords = 509,
        DataArchaeologyNotifications = 510,
        DataArchaeologyTelemetryRing = 511,
        PdaFrequencyTargetWave = 512,
        PdaFrequencyPlayerWave = 513,
        PdaFrequencyErrorOutput = 514,
        PdaFrequencyGpuSegments = 515,
        PdaFrequencyStageTargets = 516,
        PdaFrequencyTelemetryRing = 517,
        TerminalDecryptionPuzzles = 71376,
        TerminalDecryptionTerminals = 71377,
        TerminalDecryptionKnobInput = 71378,
        TerminalDecryptionTelemetryRing = 71379,
        DataMonolithPayload = 71103,
        DataMonolithTelemetryRing = 71104,
        DataMonolithTelemetryCursor = 71105,
        SurvivalDatabaseStableHashes = 518,
        SurvivalDatabaseMassKilograms = 519,
        SurvivalDatabaseVolumeLiters = 520,
        SurvivalDatabaseEnergyDensityMegajoulesPerKilogram = 521,
        SurvivalDatabaseBaseDurability = 522,
        GroundRadarHits = 523,
        GroundRadarSignalStrength = 524,
        GroundRadarAgeSeconds = 525,
        GroundRadarOreTypes = 526,
        GroundRadarPingGpu = 527,
        GroundRadarCounters = 528,
        GroundRadarMaxSignalStrength = 529,
        GroundRadarTelemetryRing = 530,
        FaunaSimulationPoolSlots = 531,
        FaunaSimulationLinearVelocities = 532,
        FaunaSimulationFlags = 533,
        FaunaSimulationFreeSlots = 534,
        DeployableSdfDrillSlotOwners = 535,
        DeployableSdfDrillInventoryQuantities = 536,
        DeployableSdfDrillInventoryCapacities = 537,
        DeployableSdfDrillInventoryItemHashes = 538,
        DeployableSdfDrillInventoryOreHashes = 539,
        DeployableSdfDrillBlackBox = 540,
        DeployableSdfDrillSnapCommands = 541,
        DeployableSdfDrillSnapHits = 542,
        RtgStartTimes = 543,
        RtgHalfLives = 544,
        RtgBaseOutput = 545,
        RtgCurrentOutput = 546,
        RtgOutputNormalized = 547,
        RtgFlags = 548,
        RtgTelemetryRing = 549,
        VaultMemoryLayoutConfig = 550,
        VaultHotEntityData = 551,
        VaultColdEntityData = 552,
        VaultAup64 = 553,
        VaultEntityBucketMap = 554,
        VaultSharedTransformMatrices = 555,
        VehicleMotorSubmarineStates = 556,
        VehicleMotorReserved557 = 557,
        VehicleMotorReserved558 = 558,
        VaultSovereigntyTelemetryRing = 559,
        VoxelSdfPayloadDescriptor = 620,
        VoxelSdfAudioMaterialIds = 621,
        VoxelMarchingCubesEdgeTable = 644,
        VoxelMarchingCubesTriTable = 645,
        MarineSnowTuningConstants = 622,
        MarineSnowDynamicWakes = 623,
        MarineSnowMockFlowField = 624,
        ShinobuCrashBlackboxBytes = 625,
        ShinobuCrashMmfScratch = 626,
        ShinobuCrashDumpHeader = 627,
        ShinobuCrashTelemetryEvents = 628,
        ShinobuCrashSourceSlots = 629,
        ShinobuCrashLoggingMasks = 630,
        ShinobuCrashAtomicState = 631,
        ShinobuCrashWatchdogCounters = 632,
        ShinobuCrashWatchdogSamples = 633,
        ShinobuCrashWatchdogStaleProbes = 634,
        ShinobuCrashWatchdogActive = 635,
        AcousticEchoPendingTaps = 636,
        VaultAupSectorLocal32 = 637,
        VaultSovereigntyActiveEntityCount = 638,
        VaultMemoryProfileCsvScratch = 639,
        VaultMemoryAddressShiftRecords = 640,
        VaultMemoryAddressShiftCount = 641,
        Arm64AlignmentTelemetryRing = 642,
        Arm64AlignmentTelemetryCursor = 643,
        WristHudState = 560,
        WristHudQuads = 561,
        WristHudFontAtlas = 562,
        WristHudTelemetryRing = 563,
        WristHudCounters = 564,
        WristHudAcousticTaps = 565,
        FloraGenomeRawBytes = 566,
        FloraGenomeDtos = 567,
        FloraGenomeExpandedSymbols = 568,
        FloraGenomeBranchMatrices = 569,
        FloraGenomeHazardZones = 570,
        FloraGenomeStats = 571,
        FloraGenomeBlackBox = 572,
        FloraGenomeBlackBoxCursor = 573,
        FloraGenomePlantSeeds = 574,
        FloraGenomeScratchSymbols = 70500,
        FloraGenomeTurtleStack = 70501,
        FloraGenomeCsvScratch = 70502,
        HullIntegrityDents = 70080,
        HullIntegrityDentUploadScratch = 70081,
        HullIntegrityBaseModules = 70082,
        HullIntegrityLedger = 70083,
        HullIntegrityTelemetryRing = 70084,
        HullIntegrityTelemetryCursor = 70085,
        HullIntegrityMockDepth = 70086,
        HullIntegrityCounters = 70087,
        HullIntegrityTuning = 70088,
        HullIntegrityDamageSignals = 70089,
        StructuralIntegrityStates = 70488,
        StructuralIntegrityNodeAups = 70489,
        StructuralIntegrityCsrOffsets = 70490,
        StructuralIntegrityCsrDestinations = 70491,
        StructuralIntegrityEdgeFlags = 70492,
        StructuralIntegrityTelemetryRing = 70493,
        StructuralIntegrityTelemetryCursor = 70494,
        StructuralIntegrityTuning = 70495,
        StructuralIntegrityMaterialStrengths = 70496,
        StructuralIntegrityCsvScratch = 70497,
        BaseStructuralWarningRawWarnings = 70498,
        BaseStructuralWarningGroups = 70499,
        BaseStructuralWarningTimers = 70503,
        BaseStructuralWarningCounters = 70504,
        BaseStructuralWarningTelemetryRing = 70505,
        BaseStructuralWarningTelemetryCursor = 70506,
        BaseStructuralWarningTuning = 70507,
        BaseStructuralWarningProfiles = 70508,
        BaseStructuralWarningCsvScratch = 70509,
        VerletCableNodes = 575,
        VerletCableConstraints = 576,
        VerletCableSystems = 577,
        VerletCableGpuSplinePoints = 578,
        VerletCableAabbs = 579,
        VerletCableTensionForces = 580,
        VerletCableBlackBox = 581,
        VerletCableBlackBoxHead = 582,
        VerletCableTuning = 583,
        VerletCableMaterials = 584,
        VerletCableSnapSignals = 585,
        VerletCableSnapSignalCount = 586,
        Shinobu143TetherAupNodes = 71280,
        Shinobu143TetherConstraints = 71281,
        Shinobu143TetherEndpoints = 71282,
        Shinobu143TetherSplineVertices = 71283,
        Shinobu143TetherForcePackets = 71284,
        Shinobu143TetherTelemetryRing = 71285,
        Shinobu143TetherTelemetryHead = 71286,
        Shinobu143CableMaterials = 71287,
        Shinobu143CableMaterialCsvScratch = 71288,
        Shinobu143TetherBootstrapState = 71289,
        Shinobu143TetherSegmentTensions = 71290,
        Shinobu143TetherSolverStats = 71291,
        Shinobu143TetherPinnedAups = 71292,
        Shinobu143TetherPinnedMask = 71293,
        SubmarineKinematicStates = 587,
        SubmarineKinematicControls = 588,
        SubmarineKinematicPidStates = 589,
        SubmarineKinematicMassProperties = 590,
        SubmarineKinematicForces = 591,
        SubmarineKinematicTelemetry = 592,
        SubmarineKinematicConfig = 593,
        SubmarineKinematicDragLut = 594,
        ShinobuInventoryHashes = 595,
        ShinobuInventoryQuantities = 596,
        ShinobuInventoryDurabilities = 597,
        ShinobuRecipeDtos = 598,
        ShinobuRecipeMasks = 599,
        ShinobuHotbarRoutes = 600,
        ShinobuPhysicalConstants = 601,
        ShinobuEconomyTelemetryRing = 602,
        ShinobuRleScratch = 603,
        ShinobuTransactionResults = 604,
        ToolKinematicsStates = 605,
        ToolKinematicsFrameInputs = 606,
        ToolKinematicsHitResults = 607,
        ToolKinematicsIkOutputs = 608,
        ToolKinematicsRecoilStates = 609,
        ToolKinematicsTuning = 610,
        ToolKinematicsScreenExports = 611,
        ToolKinematicsTelemetryRing = 612,
        ToolKinematicsTriggerSignals = 613,
        ToolKinematicsCarveRequests = 614,
        ToolKinematicsHeatSignals = 615,
        ToolKinematicsSparkRequests = 616,
        ToolKinematicsBeamVertices = 617,
        ToolKinematicsBeamVertexCounts = 618,
        ToolKinematicsPoseOutputs = 619,
        ShinobuPlasmaBeamStates = 71120,
        ShinobuPlasmaBeamVertices = 71121,
        ShinobuPlasmaBeamTrigLut = 71122,
        ShinobuPlasmaBeamRuntimeScalars = 71123,
        ShinobuPlasmaBeamIndirectArgs = 71124,
        ShinobuPlasmaBeamTelemetryRing = 71125,
        ShinobuPlasmaBeamMockSignals = 71126,
        ShinobuPlasmaBeamAcousticTaps = 71127,
        ShinobuPlasmaBeamCsvScratch = 71128,
        ShinobuWaterOpticsTuning = 71129,
        ShinobuVolumetricFogParams = 71130,
        ShinobuVolumetricFogPointLights = 71131,
        ShinobuVolumetricFogTelemetryRing = 71132,
        ShinobuVolumetricFogExtinctionProfiles = 71133,
        ShinobuVolumetricFogCsvScratch = 71134,
        ShinobuWaterOpticsParams = 71135,
        ShinobuWaterOpticsProfiles = 71136,
        ShinobuWaterOpticsTelemetryRing = 71137,
        ShinobuWaterOpticsTelemetryCursor = 71138,
        ShinobuWaterOpticsCsvScratch = 71139,
        ShinobuFabricationJobs = 71140,
        ShinobuFabricationRuntime = 71141,
        ShinobuFabricationGpuPayload = 71142,
        ShinobuFabricationTelemetryRing = 71143,
        ShinobuFabricatorInventoryCountPairs = 71144,
        ShinobuFabricationTuning = 71145,
        ShinobuFabricationTimingLookup = 71146,
        ShinobuFabricationCsvScratch = 71147,
        ShinobuFabricatorRecipeCosts = 71148,
        ShinobuFabricatorRecipeEvaluationResult = 71149,
        ShinobuFabricatorDeconstructionRecipeOutputs = 71169,
        ShinobuFabricatorDeconstructionOutputCount = 71170,
        ShinobuFabricatorComplexRecipeGraphNodes = 71171,
        ShinobuFabricatorComplexRecipeGraphEdges = 71172,
        ShinobuFabricatorComplexRecipeGraphInDegrees = 71173,
        ShinobuFabricatorComplexRecipeGraphQueue = 71174,
        ShinobuFabricatorComplexRecipeRawCosts = 71175,
        ShinobuFabricatorComplexRecipeRawCostCount = 71176,
        ShinobuFabricatorComplexRecipeGraphStatus = 71177,
        ShinobuFabricatorUnlockedRecipes = 71178,
        ShinobuFabricatorMemoryTelemetryRing = 71179,
        ShinobuFastFailRequirementDtos = 71203,
        ShinobuFastFailCraftableWords = 71204,
        ShinobuFastFailTelemetryRing = 71205,
        ShinobuFastFailTelemetryCursor = 71206,
        ShinobuFastFailTransactionResults = 71207,
        ShinobuMesofaunaStates = 71180,
        ShinobuMesofaunaMockPreyTargets = 71181,
        ShinobuMesofaunaVisualSync = 71182,
        ShinobuMesofaunaTelemetryRing = 71183,
        ShinobuMesofaunaTuning = 71184,
        ShinobuMesofaunaTargetHashBucketHeads = 71185,
        ShinobuMesofaunaTargetHashNext = 71186,
        ShinobuMesofaunaSpeciesProfiles = 71187,
        ShinobuMesofaunaSpeciesProfileCount = 71188,
        ShinobuMesofaunaCsvScratch = 71189,
        ShinobuStressDirectorRules = 71190,
        ShinobuStressDirectorRuleLinks = 71191,
        ShinobuStressDirectorCandidates = 71192,
        ShinobuStressDirectorSelection = 71193,
        ShinobuStressDirectorInput = 71194,
        ShinobuStressDirectorTuning = 71195,
        ShinobuStressDirectorTelemetry = 71196,
        ShinobuStressDirectorCounters = 71197,
        ShinobuStressDirectorCsvScratch = 71198,
        ShinobuStressDirectorFrustumPlanes = 71199,
        ShinobuStressDirectorOwnedSlots = 71200,
        ShinobuStressDirectorInventoryTickets = 71201,
        ShinobuStressDirectorSpawnDebug = 71202,
        SaveWorldPagerWriteArena = 70200,
        SaveWorldPagerReadArena = 70201,
        SaveWorldPagerReadSlotStates = 70202,
        SaveWorldPagerCompressionScratch = 70203,
        SaveWorldPagerHotState = 70204,
        SaveWorldPagerTelemetryRing = 70205,
        SaveWorldPagerReadStaging = 70206,
        SaveWorldPagerWriteCommands = 70207,
        SaveWorldPagerReadCommands = 70208,
        SaveWorldPagerReadResults = 70209,
        SaveMerkleNodeFront = 70270,
        SaveMerkleNodeBack = 70271,
        SaveMerkleLeafDescriptors = 70272,
        SaveMerkleDeltaRecords = 70273,
        SaveMerkleDeltaBytes = 70274,
        SaveMerkleCompressedBytes = 70275,
        SaveMerkleLz4BlockHeaders = 70276,
        SaveMerkleTelemetryRing = 70277,
        SaveMerkleMockInventory = 70278,
        SaveMerkleCounters = 70279,
        SaveMerkleTombstoneScratch = 70280,
        SaveMerkleCsvOverrideBytes = 70281,
        SaveMerkleLz4HashTable = 70282,
        SaveMerklePrunedDeltaBytes = 70283,
        SaveVoxelDeltaSchemaBytes = 70284,
        SaveVoxelDeltaRuntimeDensity = 70285,
        SaveVoxelDeltaBaselineDensity = 70286,
        SaveVoxelDeltaMaterialIds = 70287,
        SaveVoxelDeltaCellFlags = 70288,
        SaveVoxelDeltaRleRuns = 70289,
        SaveVoxelDeltaBlockCounters = 70290,
        SaveVoxelDeltaRleBytes = 70291,
        SaveVoxelDeltaCompressedBytes = 70292,
        SaveVoxelDeltaLz4HashTable = 70293,
        SaveVoxelDeltaHeaders = 70294,
        SaveVoxelDeltaCounters = 70295,
        SaveVoxelDeltaTelemetryRing = 70296,
        SaveVoxelDeltaTelemetryCursor = 70297,
        SaveVoxelDeltaTuning = 70298,
        SaveVoxelDeltaSectorStats = 70299,
        SaveEntityDeltaSchemaBytes = 70340,
        SaveEntityDeltaCurrentRecords = 70341,
        SaveEntityDeltaBaselineRecords = 70342,
        SaveEntityDeltaRecords = 70343,
        SaveEntityDeltaBlockCounters = 70344,
        SaveEntityDeltaDenseBytes = 70345,
        SaveEntityDeltaRleBytes = 70346,
        SaveEntityDeltaCompressedBytes = 70347,
        SaveEntityDeltaLz4HashTable = 70348,
        SaveEntityDeltaHeaders = 70349,
        SaveEntityDeltaCounters = 70350,
        SaveEntityDeltaTelemetryRing = 70351,
        SaveEntityDeltaTelemetryCursor = 70352,
        SaveEntityDeltaTuning = 70353,
        SaveEntityDeltaSectorStats = 70354,
        SaveEntityDeltaCsvScratch = 70355,
        SaveEntityDeltaProfiles = 70356,
        SaveEntityDeltaWalPayloadBytes = 70357,
        SaveMacroDatabaseDirtyPayloadSlots = 70370,
        SaveMacroDatabaseDirtyPayloadKeys = 70371,
        SaveMacroDatabaseSectorCoordSlots = 70372,
        SaveMacroDatabaseSectorWindowScratch = 70373,
        SaveMacroDatabaseSectorCoordScratch = 70374,
        SaveMacroDatabaseHydrationScratch = 70375,
        SaveMacroDatabaseBlackBox = 70376,
        SaveMacroDatabasePayloadCopyScratch = 70377,
        SaveVoxelDeltaCompactionSourceSdfScratch = 70380,
        SaveVoxelDeltaCompactionDirtyMaskScratch = 70381,
        SaveVoxelDeltaCompactionDeltaSdfScratch = 70382,
        SaveVoxelDeltaCompactionMaterialScratch = 70383,
        SaveVoxelDeltaCompactionFlagsScratch = 70384,
        SaveVoxelDeltaCompactionOutputSdfScratch = 70385,
        SaveVoxelDeltaCompactionOutputMaterialsScratch = 70386,
        SaveVoxelDeltaCompactionOutputFlagsScratch = 70387,
        SaveVoxelDeltaCompactionUniformFlagScratch = 70388,
        SaveVoxelDeltaNativeSnapshotScratch = 70389,
        BiolumGlowStates = 70300,
        BiolumGlowGpuColorFront = 70301,
        BiolumGlowGpuColorBack = 70302,
        BiolumGlowAupOrigins = 70303,
        BiolumSyncPulses = 70304,
        BiolumSyncPulseAges = 70305,
        BiolumMockWeatherSignal = 70306,
        BiolumMockPredatorSignal = 70307,
        BiolumMockDamageSignal = 70308,
        BiolumSpeciesTuning = 70309,
        BiolumCsvScratch = 70310,
        ShinobuSomaticKinematicState = 70120,
        ShinobuSomaticBoundingSphere = 70121,
        ShinobuSomaticHandStrokeHistory = 70122,
        ShinobuSomaticTuning = 70123,
        ShinobuSomaticDragLut = 70124,
        ShinobuSomaticSignalScratch = 70125,
        ShinobuSomaticBlackBox = 70126,
        ShinobuSomaticBlackBoxCursor = 70127,
        ShinobuSomaticCsvScratch = 70128,
        ShinobuVRSomaticBlackBox = 70142,
        ShinobuVRSomaticHeadCollisionCommands = 70143,
        ShinobuVRSomaticHeadCollisionHits = 70144,
        ShinobuVRSomaticHeadCollisionSamples = 70145,
        ShinobuVRSomaticRootSyncInput = 70146,
        ShinobuVRSomaticRootSyncOutput = 70147,
        ShinobuVRSomaticHandTargets = 70148,
        ShinobuVRSomaticHandPhysicalPositions = 70149,
        ShinobuVRSomaticComfortWrite = 70166,
        ShinobuVRSomaticComfortRead = 70167,
        ShinobuVRSomaticDerivatives = 70168,
        ShinobuVRSomaticHistory = 70169,
        ShinobuVRSomaticProfile = 70170,
        ShinobuVRSomaticComfortTelemetry = 70171,
        ShinobuVRSomaticMockSickness = 70172,
        ShinobuVRSomaticCsvScratch = 70173,
        ShinobuVRSomaticProfileLookup = 70174,
        ShinobuVRSomaticKccStateMirror = 70175,
        ShinobuVRSomaticRawRotation = 70176,
        ShinobuVRSomaticHorizonWrite = 70177,
        ShinobuVRSomaticHorizonRead = 70178,
        ShinobuVRSomaticHorizonTelemetry = 70179,
        ShinobuDeltaCrusherVoxelBlackBox = 70130,
        ShinobuDeltaCrusherCarveWrites = 70131,
        ShinobuDeltaCrusherDirtyMaskPool = 70132,
        ShinobuDeltaCrusherSdfBitsPool = 70133,
        ShinobuDeltaCrusherMaterialPool = 70134,
        ShinobuDeltaCrusherCellFlagsPool = 70135,
        ShinobuDeltaCrusherCarveEventQueue = 70136,
        ShinobuAmbientEntities = 70400,
        ShinobuAmbientAups = 70401,
        ShinobuAmbientEntitySnapshot = 70402,
        ShinobuAmbientAupSnapshot = 70403,
        ShinobuEcosystemSectors = 70404,
        ShinobuEcosystemTuning = 70405,
        ShinobuEcosystemCounters = 70406,
        ShinobuEcosystemTelemetryRing = 70407,
        ShinobuSpatialHashDebugCells = 70408,
        ShinobuRenderMatrices = 70409,
        ShinobuRenderCustomData = 70410,
        ShinobuSpatialHashBucketHeads = 70411,
        ShinobuSpatialHashNext = 70412,
        ShinobuEcosystemCsvScratch = 70413,
        ShinobuEcosystemLegacyScratch = 70414,
        ShinobuSymbiosisFlora = 70415,
        ShinobuSymbiosisFloraAups = 70416,
        ShinobuSymbiosisLinks = 70417,
        ShinobuSymbiosisExchanges = 70418,
        ShinobuSymbiosisTelemetryRing = 70419,
        ShinobuSymbiosisCounters = 70420,
        ShinobuSymbiosisCsvScratch = 70421,
        ShinobuSymbiosisScannerVfx = 70422,
        ShinobuSymbiosisOxygenEmitters = 70423,
        ShinobuSymbiosisAdherence = 70424,
        ShinobuSymbiosisSeeds = 70425,
        ShinobuSymbiosisAcousticTaps = 70426,
        ShinobuSymbiosisTuning = 70427,
        ShinobuSymbiosisFloraHashBucketHeads = 70428,
        ShinobuSymbiosisFloraHashNext = 70429,
        ShinobuSymbiosisMockBoids = 70430,
        ShinobuSymbiosisLegacyScratch = 70431,
        ShinobuSymbiosisMockFish = 70432,
        ShinobuMacroEcosystemSectorFront = 70433,
        ShinobuMacroEcosystemSectorBack = 70434,
        ShinobuMacroEcosystemRemainders = 70435,
        ShinobuMacroEcosystemSectorCoords = 70436,
        ShinobuMacroEcosystemIndexEntries = 70437,
        ShinobuMacroEcosystemBiomeSpecs = 70438,
        ShinobuMacroEcosystemTuning = 70439,
        ShinobuMacroEcosystemCounters = 70440,
        ShinobuMacroEcosystemTelemetryRing = 70441,
        ShinobuMacroEcosystemCsvScratch = 70442,
        ShinobuMacroEcosystemFaultFlags = 70447,
        ShinobuSpatialGridEntries = 70448,
        ShinobuSpatialGridSortScratch = 70449,
        ShinobuSpatialGridBucketRanges = 70450,
        ShinobuSpatialGridTelemetryRing = 70451,
        ShinobuSpatialGridTelemetryCursor = 70452,
        ShinobuSpatialGridTuning = 70453,
        ShinobuSpatialGridProfiles = 70454,
        ShinobuSpatialGridCsvScratch = 70455,
        ShinobuSpatialGridMockCoordinates = 70456,
        ShinobuFlockingThreats = 70457,
        ShinobuFlockingThreatCount = 70458,
        ShinobuFlockingTelemetryRing = 70459,
        ShinobuFlockingCounters64 = 70474,
        ShinobuSpatialGridDumpSnapshot = 70475,
        ShinobuEcosystemDumpSnapshot = 70476,
        ShinobuNutrientDriftCellFront = 70460,
        ShinobuNutrientDriftCellBack = 70461,
        ShinobuNutrientDriftFlowField = 70462,
        ShinobuNutrientDriftInjection = 70463,
        ShinobuNutrientDriftSources = 70464,
        ShinobuNutrientDriftSourceCount = 70465,
        ShinobuNutrientDriftTuning = 70466,
        ShinobuNutrientDriftTelemetryRing = 70467,
        ShinobuNutrientDriftTelemetryCursor = 70468,
        ShinobuNutrientDriftDensityUpload = 70469,
        ShinobuNutrientDriftGridHeader = 70470,
        ShinobuNutrientDriftCsvScratch = 70471,
        ShinobuNutrientDriftProfiles = 70472,
        ShinobuNutrientDriftFaultFlags = 70473,
        ShinobuSwarmSpeciesProfiles = 70443,
        ShinobuBoidIndirectArgs = 70444,
        ShinobuBoidStates = 70445,
        ShinobuBoidStateSnapshot = 70446,
        ShinobuScalabilitySystemHealth = 70480,
        ShinobuScalabilityState = 70481,
        ShinobuScalabilityMockHeavyLoad = 70482,
        ShinobuScalabilityMockScatterDensity = 70483,
        ShinobuScalabilityCsvScratch = 70484,
        ShinobuScalabilityTunerState = 70485,
        ShinobuScalabilityDumpScratch = 70486,
        ShinobuScalabilityOscilloscope = 70487,
        ShinobuMathLodConfig = 74400,
        ShinobuMathLodTelemetryRing = 74401,
        ShinobuMathLodTelemetryCursor = 74402,
        ShinobuInputCurrentDto = 70520,
        ShinobuInputJournalRing = 70521,
        ShinobuInputButtonMaskWindow = 70522,
        ShinobuInputBlockMask = 70523,
        ShinobuInputProfile = 70524,
        ShinobuInputTelemetryRing = 70525,
        ShinobuInputReplaySnapshot = 70526,
        ShinobuInputHapticCommands = 70527,
        ShinobuInputMockSignals = 70528,
        ShinobuInputOscilloscope = 70529,
        ShinobuInputStateBridgeRing = 70530,
        ShinobuInputXRInputStates = 70531,
        ShinobuInputXRLookAtRayCommands = 70532,
        ShinobuInputCsvScratch = 70533,
        ShinobuPredictedInputRing = 75000,
        ShinobuPredictedInputAupTargets = 75001,
        ShinobuInputPredictionTelemetry = 75002,
        ShinobuInputReplayFrames = 75008,
        ShinobuInputReplayTelemetry = 75009,
        ShinobuInputReplayValidationResults = 75010,
        BabelUtf8Blob = 70541,
        BabelTelemetryRing = 70542,
        BabelStagedLocale = 70543,
        BabelIndexTable = 70544,
        BabelDecryptionMask = 70545,
        BabelLinkedAudioHashes = 70546,
        BabelOverrideCsvScratch = 70547,
        BabelErrorUtf8 = 70548,
        BabelDictionaryMappedBytes = 70549,
        BabelTelemetryCursor = 70551,
        BabelBTreeTelemetryRing = 70552,
        BabelBTreeTelemetryCursor = 70553,
        BabelBTreeTelemetryAccumulator = 70554,
        ShinobuInventoryCarryTotals = 70137,
        ShinobuInventoryCsvMonitor = 70138,
        ShinobuInventorySignalScratch = 70139,
        ShinobuInventoryDumpScratch = 70140,
        ShinobuRecipeIngredients = 70141,
        ShinobuInventorySlots = 73120,
        ShinobuInventoryActiveSlotCount = 73121,
        ShinobuInventoryQueryResults = 73122,
        ShinobuInventoryQueryCounters = 73123,
        ShinobuInventoryRoutingTelemetry = 73124,
        ShinobuInventoryRoutingTelemetryCursor = 73125,
        ShinobuInventoryRoutingTuning = 73126,
        ShinobuInventoryUiSnapshotA = 73127,
        ShinobuInventoryUiSnapshotB = 73128,
        ShinobuInventoryStackLimits = 73129,
        ShinobuInventoryContainerRanges = 73130,
        ShinobuInventoryContainerRangeCount = 73131,
        ShinobuInventoryContainerSyncResult = 73132,
        ShinobuInventorySoaTelemetry = 73133,
        ShinobuInventorySoaTelemetryCursor = 73134,
        ShinobuInventorySoaCapacityProfiles = 73135,
        ShinobuCargoTransactions = 73136,
        ShinobuCargoLootCaches = 73137,
        ShinobuCargoSyncTelemetry = 73138,
        ShinobuCargoSyncTelemetryCursor = 73139,
        ShinobuCargoSyncProgress = 73140,
        ShinobuCargoSyncTuning = 73141,
        ShinobuCargoFilterProfiles = 73142,
        ShinobuCargoOverflowCounter = 73143,
        QuestDagGlobalStateMasks = 70150,
        QuestDagOldStateMasks = 70151,
        QuestDagNodes = 70152,
        QuestDagNodeRuntime = 70153,
        QuestDagTriggerVolumes = 70154,
        QuestDagRequiredItemHashes = 70155,
        QuestDagRequiredItemQuantities = 70156,
        QuestDagPlayerItemHashes = 70157,
        QuestDagPlayerItemQuantities = 70158,
        QuestDagFactionStandings = 70159,
        QuestDagTelemetryRing = 70160,
        QuestDagTelemetryCursor = 70161,
        QuestDagCounters = 70162,
        QuestDagTriggerNodeIndices = 70163,
        QuestDagCsvMonitor = 70164,
        QuestDagNoTriggerNodeIndices = 70165,
        QuestDagQuestStates = 70100,
        QuestDagDependencyLinks = 70101,
        NarrativePoiTriggers = 74000,
        NarrativePoiBucketRanges = 74001,
        NarrativePoiBucketIndices = 74002,
        NarrativePoiStateMasks = 74003,
        NarrativePoiTelemetryRing = 74004,
        NarrativePoiTelemetryCursor = 74005,
        NarrativePoiCounters = 74006,
        NarrativePoiCsvScratch = 74007,
        NarrativePoiPresentation = 74008,
        PrologueSequenceTelemetryRing = 74009,
        OrbitalDropReentryVfxTelemetryRing = 74010,
        PrologueReentryState = 74011,
        QAEnduranceBlackBoxRing = 74200,
        SargassumCutStampCommands = 74300,
        SargassumCutDamageVolumeStampCommands = 74301,
        GameplayDebrisFrontStates = 74310,
        GameplayDebrisBackStates = 74311,
        InternalFloodWaterlineTelemetryRing = 74312,
        DiegeticVisorHudBlackBox = 74313,
        DiegeticTooltipBlackBox = 74314,
        HudNotificationQueue = 74315,
        VoxelMeshPipelineBlackBox = 74316,
        LoreDatabaseUnlockedWords = 74317,
        QAHeadlessStressFractureBlackBoxRing = 74318,
        QAHeadlessStressFractureScratchBlock = 74319,
        InstanceCullingIndirectArgsReadback = 74320,
        InstanceCullingTelemetryRing = 74321,
        TraumaDispatcherParasiteSporeLosCommands = 74322,
        TraumaDispatcherParasiteSporeLosHits = 74323,
        RaycastBatchHelperCommands = 74324,
        RaycastBatchHelperHits = 74325,
        FontStreamingVisibleHashPrefetch = 74326,
        FontStreamingVisibleSlicePrefetch = 74327,
        VehicleSubOsButtonStates = 74328,
        VehicleSubOsButtonTargets = 74329,
        VehicleSubOsButtonProgress = 74330,
        VehicleSubOsButtonOffsets = 74331,
        VehicleSubOsButtonBaseLocalPositions = 74332,
        VehicleSubOsButtonMatrices = 74333,
        VehicleSubOsTelemetryRing = 74334,
        AbyssalPathTelemetryRing = 74335,
        CaveVoxelLightingOccupancyVolume = 74336,
        CaveVoxelLightingSdfVolume = 74337,
        ResourceDistributionMetamorphismInputs = 74338,
        ResourceDistributionMetamorphismResults = 74339,
        SubmarineCoreHullIntegritySummary = 74340,
        SubmarineCorePhysicsBinding = 74341,
        SubmarineCoreGridState = 74342,
        GpuScatterTelemetryRing = 74343,
        ProximityColliderPositions = 74344,
        ProximityColliderJobResults = 74345,
        ProximityColliderPrevStatus = 74346,
        OpenXrManualOverrideLeverBlackBox = 74347,
        HeadlessSimulationGhostState = 74348,
        HeadlessSimulationGhostNextState = 74349,
        HeadlessSimulationBlackBox = 74350,
        HeadlessSimulationMemoryWindowBytes = 74351,
        HeadlessSimulationMemoryWindowH8Bytes = 74352,
        HeadlessSimulationMemoryWindowAllocationCounts = 74353,
        MetaCampaignVariables = 74354,
        MetaCampaignRules = 74355,
        MetaCampaignBlackBox = 74356,
        AutonomousExtractorJobInputs = 74357,
        AutonomousExtractorJobResults = 74358,
        AutonomousExtractorCycleTimers = 74359,
        AutonomousExtractorBufferedItemHashIds = 74360,
        AutonomousExtractorBufferedUnitCounts = 74361,
        AutonomousExtractorCompletedCycleCounts = 74362,
        WorldProceduralFieldZones = 74363,
        WorldProceduralFieldBiomeMatrices = 74364,
        WorldProceduralFieldBiomeMatrixIndex = 74365,
        WorldProceduralFieldBiomeFamilies = 74366,
        WorldProceduralFieldCaveEntranceHints = 74367,
        WorldProceduralFieldNoiseLookup = 74368,
        WorldScatterMigratorySargassumIslands = 74369,
        WorldScatterMigratorySargassumScratchIslands = 74370,
        WorldScatterMigratorySargassumSelectedSources = 74371,
        WorldScatterMigratorySargassumFlowSamples = 74372,
        WorldScatterMigratorySargassumSpatialHandles = 74373,
        WorldScatterMigratorySargassumScratchSpatialHandles = 74374,
        MarauderOutpostWfcGrid = 74375,
        MarauderOutpostShellMatrices = 74376,
        MarauderOutpostShellCellTypes = 74377,
        MarauderOutpostInteractableSpawns = 74378,
        MarauderOutpostMutableStateGrid = 74379,
        MarauderOutpostCounters = 74380,
        MarauderOutpostTelemetryRing = 74381,
        CrashTelemetryRing = 74382,
        CrashTelemetryExportSnapshot = 74383,
        CrashTelemetryExportScratch = 74384,
        HectonWorldGeneratorWestSlopeLut = 74385,
        HectonWorldGeneratorEastSlopeLut = 74386,
        HectonWorldGeneratorBiomeLut = 74387,
        HazardZoneVolumes = 74388,
        HazardZoneVolumeIds = 74389,
        HazardZoneSpatialHandles = 74390,
        HazardZoneCurveLutSamples = 74391,
        HazardZoneJobVolumes = 74392,
        HazardZoneCandidateVolumeFlags = 74393,
        HazardZoneSpatialQueryHandles = 74394,
        HazardZoneTelemetryRing = 74523,
        HazardZoneTelemetryCursor = 74524,
        HazardExposureJobResult = 74525,
        GasDynamicsTelemetryRing = 74395,
        IndirectVegetationFloraGrowthTelemetryRing = 74396,
        IndirectVegetationScatterCullTelemetryRing = 74397,
        VegetationMemoryPoolTelemetryRing = 74398,
        VegetationMemoryPoolTelemetryCursor = 74399,
        SargassumGlobalDragDensityBuildSources = 74403,
        SargassumGlobalDragScavengerMatrices = 74404,
        SargassumGlobalDragBatchMetadata = 74405,
        VegetationSurfaceDefragMoves = 74406,
        VegetationUnderwaterDefragMoves = 74407,
        VegetationSurfaceAggregateCopyRecords = 74408,
        VegetationUnderwaterAggregateCopyRecords = 74409,
        VegetationMegaWreckStreamSnapshot = 74410,
        VegetationCanopyHeightGrid = 74411,
        VegetationVisibleHlodSnapshot = 74412,
        VegetationHlodRegistrySnapshot = 74413,
        VegetationPredatorFearNodeSnapshot = 74414,
        VegetationAbyssalPathSnapshot = 74415,
        VegetationAbyssalAnchorPositions = 74416,
        VegetationAbyssalAnchorAupPositions = 74417,
        VegetationAbyssalNavNodeSnapshot = 74418,
        VegetationAbyssalNavConduitVectors = 74419,
        VegetationAbyssalNavConduitStrengths = 74420,
        VegetationAbyssalNavNodeTypes = 74421,
        VegetationTerrainHoleStreamingRecords = 74422,
        VegetationDensityQueryScratch = 74423,
        VegetationDensityQueryChunks = 74424,
        VegetationDensityQueryGrid = 74425,
        VegetationThreatAttractorGrid = 74426,
        VegetationTerrainHoleRecords = 74427,
        VegetationArtificialStructureRecords = 74428,
        VegetationEcosystemFlowField = 74429,
        VegetationAbyssalThermalGrid = 74430,
        VegetationAbyssalFlowVolume = 74431,
        VegetationEcosystemThreatGrid = 74432,
        VegetationEcosystemThreatGridCompressed = 74433,
        VegetationEcosystemThreatVoxel = 74434,
        VegetationEcosystemThreatEcho = 74435,
        VegetationAbyssalPathStagingPacked = 74615,
        VegetationThreatPropagationStagingPacked = 74616,
        VegetationFlowFieldStagingPacked = 74617,
        VegetationThermalGridStagingPacked = 74618,
        VegetationSurfaceAggregateFrontMatrices = 74440,
        VegetationSurfaceAggregateFrontMetadata = 74441,
        VegetationSurfaceAggregateFrontTypes = 74442,
        VegetationSurfaceAggregateFrontSemanticTypes = 74443,
        VegetationSurfaceAggregateFrontBiomeLayers = 74444,
        VegetationSurfaceAggregateFrontFlowDirections = 74445,
        VegetationSurfaceAggregateFrontFlowVectors = 74446,
        VegetationSurfaceAggregateBackMatrices = 74447,
        VegetationSurfaceAggregateBackMetadata = 74448,
        VegetationSurfaceAggregateBackTypes = 74449,
        VegetationSurfaceAggregateBackSemanticTypes = 74450,
        VegetationSurfaceAggregateBackBiomeLayers = 74451,
        VegetationSurfaceAggregateBackFlowDirections = 74452,
        VegetationSurfaceAggregateBackFlowVectors = 74453,
        VegetationUnderwaterAggregateFrontMatrices = 74454,
        VegetationUnderwaterAggregateFrontMetadata = 74455,
        VegetationUnderwaterAggregateFrontTypes = 74456,
        VegetationUnderwaterAggregateFrontSemanticTypes = 74457,
        VegetationUnderwaterAggregateFrontBiomeLayers = 74458,
        VegetationUnderwaterAggregateFrontFlowDirections = 74459,
        VegetationUnderwaterAggregateFrontFlowVectors = 74460,
        VegetationUnderwaterAggregateBackMatrices = 74461,
        VegetationUnderwaterAggregateBackMetadata = 74462,
        VegetationUnderwaterAggregateBackTypes = 74463,
        VegetationUnderwaterAggregateBackSemanticTypes = 74464,
        VegetationUnderwaterAggregateBackBiomeLayers = 74465,
        VegetationUnderwaterAggregateBackFlowDirections = 74466,
        VegetationUnderwaterAggregateBackFlowVectors = 74467,
        VegetationSurfaceChunkPoolMatrices = 74480,
        VegetationSurfaceChunkPoolMetadata = 74481,
        VegetationSurfaceChunkPoolTypes = 74482,
        VegetationSurfaceChunkPoolSemanticTypes = 74483,
        VegetationSurfaceChunkPoolBiomeLayers = 74484,
        VegetationSurfaceChunkPoolEdgeDistances = 74485,
        VegetationSurfaceChunkPoolFlowDirections = 74486,
        VegetationSurfaceChunkPoolFlowVectors = 74487,
        VegetationUnderwaterChunkPoolMatrices = 74488,
        VegetationUnderwaterChunkPoolMetadata = 74489,
        VegetationUnderwaterChunkPoolTypes = 74490,
        VegetationUnderwaterChunkPoolSemanticTypes = 74491,
        VegetationUnderwaterChunkPoolBiomeLayers = 74492,
        VegetationUnderwaterChunkPoolEdgeDistances = 74493,
        VegetationUnderwaterChunkPoolFlowDirections = 74494,
        VegetationUnderwaterChunkPoolFlowVectors = 74495,
        VegetationSurfaceDefragScratchMatrices = 74496,
        VegetationSurfaceDefragScratchMetadata = 74497,
        VegetationSurfaceDefragScratchTypes = 74498,
        VegetationSurfaceDefragScratchSemanticTypes = 74499,
        VegetationSurfaceDefragScratchBiomeLayers = 74500,
        VegetationSurfaceDefragScratchEdgeDistances = 74501,
        VegetationSurfaceDefragScratchFlowDirections = 74502,
        VegetationSurfaceDefragScratchFlowVectors = 74503,
        VegetationUnderwaterDefragScratchMatrices = 74504,
        VegetationUnderwaterDefragScratchMetadata = 74505,
        VegetationUnderwaterDefragScratchTypes = 74506,
        VegetationUnderwaterDefragScratchSemanticTypes = 74507,
        VegetationUnderwaterDefragScratchBiomeLayers = 74508,
        VegetationUnderwaterDefragScratchEdgeDistances = 74509,
        VegetationUnderwaterDefragScratchFlowDirections = 74510,
        VegetationUnderwaterDefragScratchFlowVectors = 74511,
        VoxelDynamicNavGridRecordBufferBase = 79000,
        VoxelDynamicNavGridRecordBufferEnd = 82071,
        VoxelDynamicNavGridTelemetryRing = 82072,
        VoxelDynamicNavGridTelemetryCursor = 82073,
        VegetationTileNativeCacheDynamicBase = 83000,
        VegetationTileNativeCacheDynamicEnd = 85047,
        ShinobuLogisticsNodes = 70180,
        ShinobuLogisticsEdges = 70181,
        ShinobuLogisticsStateFlags = 70182,
        ShinobuLogisticsOxygenFront = 70183,
        ShinobuLogisticsOxygenBack = 70184,
        ShinobuLogisticsInternalPressure = 70185,
        ShinobuLogisticsExternalPressure = 70186,
        ShinobuLogisticsYieldThreshold = 70187,
        ShinobuLogisticsReinforcement = 70188,
        ShinobuLogisticsNodeAup = 70189,
        ShinobuLogisticsLocalPositions = 70190,
        ShinobuLogisticsPriorityTier = 70191,
        ShinobuLogisticsVisited = 70192,
        ShinobuLogisticsCellToNode = 70193,
        ShinobuLogisticsCounters = 70194,
        ShinobuLogisticsTuning = 70195,
        ShinobuLogisticsBlackBox = 70196,
        ShinobuLogisticsComponentIds = 70534,
        ShinobuLogisticsPressureFront = 70535,
        ShinobuLogisticsPressureBack = 70536,
        ShinobuLogisticsEdgeRemainderMilli = 70537,
        ShinobuLogisticsCsrEdgeCapacities = 70538,
        ShinobuLogisticsCsrEdgeFlow01 = 70539,
        ShinobuLogisticsComponentSpecs = 70540,
        ShinobuLogisticsCsvScratch = 70550,
        ConstructionBuilderTuning = 70197,
        ConstructionBuilderTelemetry = 70198,
        ConstructionBuilderBounds = 70199,
        ConstructionBuilderOccupancy = 70319,
        ConstructionPreviewWrite = 70320,
        ConstructionPreviewBuild = 70321,
        ConstructionPreviewMatrices = 70322,
        ConstructionSocketStates = 70358,
        ConstructionSocketAup = 70359,
        ConstructionGhostSocketStates = 70360,
        ConstructionGhostSocketAup = 70361,
        ConstructionSocketSnapResults = 70362,
        ConstructionSocketTelemetry = 70363,
        ConstructionSocketTuning = 70364,
        ConstructionSocketModules = 70365,
        ConstructionSocketCounters = 70366,
        ConstructionSocketBounds = 70367,
        ConstructionSocketConnections = 70368,
        ConstructionSocketCsvScratch = 70369,
        FoundationSnappingModules = 70960,
        FoundationSnappingPylonMatrices = 70961,
        FoundationSnappingPylonSurfaces = 70962,
        FoundationSnappingPerModuleCounters = 70963,
        FoundationSnappingFrameCounters = 70964,
        FoundationSnappingTelemetryRing = 70965,
        FoundationSnappingTelemetryCursor = 70966,
        FoundationSnappingTuning = 70967,
        FoundationSnappingMockSdfDistances = 70968,
        FoundationSnappingSdfConfig = 70969,
        FoundationSnappingRayOrigins = 70970,
        FoundationSnappingProfileRanges = 70971,
        FoundationSnappingCsvScratch = 70972,
        FoundationSnappingDebugRays = 70973,
        FoundationSnappingIndirectArgs = 70974,
        ShinobuHapticSynthesisPulses = 70975,
        ShinobuHapticSynthesisFinalPulse = 70976,
        ShinobuHapticSynthesisMockImpulses = 70977,
        ShinobuHapticSynthesisTelemetryRing = 70978,
        ShinobuHapticSynthesisProfileTable = 70979,
        ShinobuHapticSynthesisTuning = 70980,
        ShinobuHapticSynthesisCsvScratch = 70981,
        BaseModuleCatalogState = 70330,
        BaseModuleCatalogDefinitions = 70331,
        BaseModuleCatalogSockets = 70332,
        BaseModuleCatalogCosts = 70333,
        BaseModuleCatalogHashToIndex = 70334,
        BaseModuleCatalogTelemetryRing = 70335,
        BaseModuleCatalogHydrationBytes = 70336,
        BaseModuleCatalogHydrationStatus = 70337,
        BaseModuleCatalogCsvScratch = 70338,
        BaseModuleCatalogScannerReport = 70339,
        AddressableHeapCacheProfiles = 70323,
        AddressableHeapTelemetry = 70324,
        AddressableHeapTrackers = 70325,
        AddressableHeapTimeToLive = 70326,
        AddressableHeapTrackerFlags = 70327,
        AddressableHeapHandleMap = 70328,
        AddressableHeapCsvScratch = 70329,
        PredatorCognitionAcousticFloat4Bank = 70210,
        PredatorCognitionApexCortexTuning = 70211,
        PredatorCognitionTargetHashBucketHeads = 70212,
        PredatorCognitionTargetHashNext = 70213,
        ShinobuPhysiologyVitals = 70220,
        ShinobuDecompressionStates = 70221,
        ShinobuHaldaneCoefficients = 70222,
        ShinobuEnvironmentVitals = 70223,
        ShinobuPhysiologyScalars = 70224,
        ShinobuVitalsExport = 70225,
        ShinobuPhysiologyTelemetryRing = 70226,
        ShinobuCardiacPulseStates = 70227,
        ShinobuMockToxemiaSignals = 70228,
        ShinobuMockPressureSignals = 70229,
        ShinobuMockCombatDamageSignals = 70230,
        ShinobuMockPredatorAggroSignals = 70231,
        ShinobuMockMedicalItemSignals = 70232,
        ShinobuPhysiologyTuning = 70233,
        ShinobuBiologyCsvOverrides = 70234,
        ShinobuTissueCompartments = 70235,
        ShinobuMockDiveProfile = 70236,
        ShinobuTissueCsvScratch = 70237,
        ShinobuMetabolismStates = 70238,
        ShinobuDroneFleetStates = 70240,
        ShinobuDroneFleetStateBackBuffer = 70241,
        ShinobuDroneFleetRenderMatrices = 70242,
        ShinobuDroneFleetRenderMatrixBackBuffer = 70243,
        ShinobuDroneFleetRenderInstances = 70244,
        ShinobuDroneFleetPositionsSoA = 70245,
        ShinobuDroneFleetStateBytes = 70246,
        ShinobuDroneFleetBlackBox = 70247,
        ShinobuDroneFleetTuningConstants = 70248,
        ShinobuDroneFleetMacroWaypoints = 70249,
        ShinobuDroneFleetMacroWaypointStates = 70250,
        ShinobuDroneFleetAStarOpenHeap = 70251,
        ShinobuDroneFleetAStarGCosts = 70252,
        ShinobuDroneFleetAStarCameFrom = 70253,
        ShinobuDroneFleetAStarNodeStates = 70254,
        ShinobuDroneFleetAStarTelemetry = 70255,
        ShinobuDroneFleetTaskClaimOwners = 70256,
        ShinobuDroneFleetTelemetryAccumulator = 70257,
        ShinobuDroneFleetDockingSurfaceProbeScratchA = 70258,
        ShinobuDroneFleetDockingSurfaceProbeScratchB = 70259,
        ShinobuDroneFleetDockingSurfaceProbeSlots = 70260,
        ShinobuDroneFleetTaskClaimCounts = 70261,
        ShinobuDroneFleetTaskPriorityHeap = 70262,
        ShinobuDroneFleetMacroRouteNodes = 70263,
        ShinobuDroneFleetMacroRouteCounts = 70264,
        ShinobuPhysicsCullingDtos = 70600,
        ShinobuPhysicsCullingFrozenVelocities = 70601,
        ShinobuPhysicsCullingStateAges = 70602,
        ShinobuPhysicsCullingSpatialCandidates = 70603,
        ShinobuPhysicsCullingSpatialCandidateMask = 70604,
        ShinobuPhysicsCullingFrameTelemetry = 70605,
        ShinobuPhysicsCullingTuning = 70606,
        ShinobuPhysicsCullingMockSeismicSignals = 70607,
        ShinobuPhysicsCullingWakeRequestMirror = 70608,
        ShinobuPhysicsCullingSpatialBucketHeads = 70630,
        ShinobuPhysicsCullingSpatialNext = 70631,
        ShinobuPhysicsCullingSpatialCellHashes = 70632,
        ShinobuPhysicsCullingChangedIndices = 70633,
        ShinobuPhysicsCullingChangedCount = 70634,
        ShinobuPhysicsCullingWakeRequestCount = 70635,
        ShinobuPhysicsCullingCsvScratch = 70636,
        ShinobuPhysicsCullingLegacyRadiiScratch = 70637,
        SystemDispatcherMasterJobHandles = 70620,
        SystemDispatcherMasterDependencyScratch = 70621,
        SystemDispatcherMasterJobDependencyTelemetry = 70622,
        SystemDispatcherMasterPipelineTelemetry = 70623,
        SystemDispatcherMasterPipelineCursor = 70624,
        SystemDispatcherMasterMockTimeDilationSignals = 70625,
        SystemDispatcherMasterPresentationSuppression = 70626,
        SystemDispatcherDomainFenceHandles = 70627,
        SystemDispatcherFenceTelemetry = 70628,
        SystemDispatcherFenceTelemetryCursor = 70629,
        SystemDispatcherJobSchedulingProfiles = 70638,
        ShinobuScannerToolBlackBox = 70639,
        ShinobuScannerEntities = 70640,
        ShinobuScannerMetadata = 70641,
        ShinobuScannerOcclusionZones = 70642,
        ShinobuScannerSpatialBucketHeads = 70643,
        ShinobuScannerSpatialNext = 70644,
        ShinobuScannerScanResults = 70645,
        ShinobuScannerResultCount = 70646,
        ShinobuScannerActiveState = 70647,
        ShinobuScannerVfxTarget = 70648,
        ShinobuScannerQueryStats = 70649,
        ShinobuScannerTelemetryRing = 70650,
        ShinobuScannerSettings = 70651,
        ShinobuScannerCsvScratch = 70652,
        ShinobuMigrationGridFront = 70653,
        ShinobuMigrationGridBack = 70654,
        ShinobuMigrationBloodCloudPois = 70655,
        ShinobuMigrationSwarmStates = 70656,
        ShinobuScannerScanProgress = 70657,
        ShinobuScannerLoreIndex = 70658,
        ShinobuScannerEncyclopediaState = 70659,
        ShinobuMaterialStates = 70660,
        ShinobuMaterialPowers = 70661,
        ShinobuMaterialVisibleIndices = 70662,
        ShinobuMaterialConstants = 70663,
        ShinobuMaterialTelemetryRing = 70664,
        ShinobuMaterialTextureMappings = 70665,
        ShinobuMaterialMockBiomassSignals = 70666,
        ShinobuMaterialWearRates = 70667,
        ShinobuMaterialBiomassScalar = 70668,
        ShinobuMaterialCsvScratch = 70670,
        ShinobuMaterialVisiblePayload = 70671,
        AudioLogPlaybackQueue = 70672,
        AudioLogEncryptedFragmentHashes = 70673,
        AudioLogEncryptedFragmentRecoveredBits = 70674,
        AudioLogTelemetryRing = 70675,
        AudioLogTelemetryCursor = 70676,
        ShinobuExosuitState = 70680,
        ShinobuExosuitFrameInput = 70681,
        ShinobuExosuitTuning = 70682,
        ShinobuExosuitMockTerrainSdf = 70683,
        ShinobuExosuitMockFlowField = 70684,
        ShinobuExosuitMockCrushDepth = 70685,
        ShinobuExosuitSolverOutput = 70686,
        ShinobuExosuitHapticSignals = 70687,
        ShinobuExosuitSiltSignals = 70688,
        ShinobuExosuitAcousticTaps = 70689,
        ShinobuExosuitScreenDto = 70690,
        ShinobuExosuitTelemetryRing = 70691,
        ShinobuExosuitTelemetryCursor = 70692,
        ShinobuExosuitFootstepAccumulator = 70693,
        ShinobuExosuitCsvScratch = 70694,
        ShinobuSeedShipAnomalyField = 70700,
        ShinobuSeedShipAnomalyTuning = 70701,
        ShinobuSeedShipAnomalyGlobals = 70702,
        ShinobuSeedShipAnomalyGlitchCommand = 70703,
        ShinobuSeedShipAnomalyMockHudSignals = 70704,
        ShinobuSeedShipAnomalyMockLeviathans = 70705,
        ShinobuSeedShipAnomalyMockAupRebase = 70706,
        ShinobuSeedShipAnomalyThermoSource = 70707,
        ShinobuSeedShipAnomalyTelemetryRing = 70708,
        ShinobuSeedShipAnomalyCsvOverrides = 70709,
        ShinobuSeedShipAnomalyIoScratch = 70710,
        ShinobuSeedShipAnomalyDumpScratch = 70711,
        ShinobuHydroKccStates = 70712,
        ShinobuHydroKccInputs = 70713,
        ShinobuHydroKccProposedVelocities = 70714,
        ShinobuHydroKccCollisionCommands = 70715,
        ShinobuHydroKccCollisionHits = 70716,
        ShinobuHydroKccPreviousAup = 70717,
        ShinobuHydroKccVisualOutputs = 70718,
        ShinobuHydroKccTelemetryRing = 70719,
        ShinobuHydroKccTelemetryCursor = 70743,
        ShinobuHydroKccTuning = 70744,
        ShinobuHydroKccFluidProfiles = 70745,
        ShinobuHydroKccFluidProfileBuckets = 70746,
        ShinobuHydroKccRollbackBytes = 70747,
        ShinobuHydroKccFaultFlags = 70748,
        ShinobuHydroKccWakePackets = 70749,
        ShinobuHydroKccDebugOutputs = 70751,
        ShinobuHydroKccResolvedHits = 70752,
        ZeroGMovementState = 160050,
        ZeroGMovementInput = 160051,
        ZeroGMovementTuning = 160052,
        ZeroGMovementSurfaceHit = 160053,
        ZeroGMovementSolverOutput = 160054,
        ZeroGMovementTelemetryRing = 160055,
        ZeroGMovementTelemetryCursor = 160056,
        ZeroGMovementTestResults = 160057,
        ShinobuOceanWaveParameters = 70760,
        ShinobuOceanAtmosphere = 70761,
        ShinobuOceanWeatherState = 70762,
        ShinobuOceanMockBuoyancyQueries = 70763,
        ShinobuOceanMockBuoyancyResults = 70764,
        ShinobuOceanTelemetryRing = 70765,
        ShinobuOceanCsvScratch = 70766,
        ShinobuOceanDumpScratch = 70767,
        ShinobuOceanLodState = 70768,
        ShinobuOceanWaveReadbackQueries = 70769,
        ShinobuOceanWaveReadbackResults = 70770,
        ShinobuOceanWaveReadbackCompletedQueries = 70771,
        ShinobuOceanWaveReadbackRingQueries = 70772,
        ShinobuOceanBeaufortProfiles = 70773,
        ShinobuOceanSurfaceSwell = 70774,
        ShinobuCausticsParameters = 70775,
        ShinobuCausticsTuning = 70776,
        ShinobuCausticsTelemetryRing = 70777,
        ShinobuCausticsTelemetryCursor = 70778,
        ShinobuCausticsProfiles = 70779,
        ShinobuCausticsCsvScratch = 70799,
        UberNoirReconstructionConstants = 71030,
        UberNoirReconstructionTelemetry = 71031,
        UberNoirReconstructionAestheticProfiles = 71032,
        UberNoirReconstructionCsvScratch = 71033,
        UberNoirReconstructionMockSignal = 71034,
        Shinobu235NoirConstants = 71040,
        Shinobu235NoirInput = 71041,
        Shinobu235NoirTelemetry = 71042,
        Shinobu235NoirTuning = 71043,
        Shinobu235NoirColorProfiles = 71044,
        Shinobu235NoirCsvScratch = 71045,
        Shinobu236BilateralDrsParams = 71050,
        Shinobu236BilateralDrsTuning = 71051,
        Shinobu236BilateralDrsTelemetry = 71052,
        Shinobu236BilateralDrsTelemetryCursor = 71053,
        Shinobu236BilateralDrsProfiles = 71054,
        Shinobu236BilateralDrsCsvScratch = 71055,
        Shinobu236BilateralDrsMockState = 71056,
        BiomeTransitionStates = 71220,
        BiomeTransitionCenters = 71221,
        BiomeTransitionInfluences = 71222,
        BiomeTransitionCurrentAtmosphere = 71223,
        BiomeTransitionBlendMask = 71224,
        BiomeTransitionShaderPayload = 71225,
        BiomeTransitionAcousticStage = 71226,
        BiomeTransitionTelemetryRing = 71227,
        BiomeTransitionCounters = 71228,
        BiomeTransitionTuning = 71229,
        BiomeTransitionCsvScratch = 71230,
        BiomeTransitionMockCameraAup = 71231,
        BiomeBoundaryGlobalBiomeMap = 71232,
        BiomeBoundaryGlobalBiomeHashMap = 71233,
        BiomeBoundarySampleResult = 71234,
        BiomeBoundaryTelemetryRing = 71235,
        VisualPressureAgingParams = 71240,
        VisualPressureAgingRuntime = 71241,
        VisualPressureAgingTelemetryRing = 71242,
        VisualPressureAgingTelemetryCursor = 71243,
        VisualPressureAgingTuning = 71244,
        VisualPressureAgingCsvScratch = 71245,
        VisualPressureAgingMockTemperature = 71246,
        UberNoirInstanceDegradation = 71247,
        UberNoirDegradationTelemetryRing = 71248,
        UberNoirDegradationTelemetryCursor = 71249,
        ShinobuCarrionStates = 71250,
        ShinobuCarrionDeathIngress = 71251,
        ShinobuCarrionRuntimeCounters = 71252,
        ShinobuCarrionTuning = 71253,
        ShinobuCarrionTelemetryRing = 71254,
        ShinobuCarrionAttractionRecords = 71255,
        ShinobuCarrionProfiles = 71256,
        ShinobuCarrionCsvScratch = 71257,
        ShinobuCarrionFaunaStates = 71258,
        ShinobuCarrionFaultFlags = 71259,
        Shinobu319StatusEffectStates = 71260,
        Shinobu319StatusEffectTelemetryRing = 71261,
        Shinobu319StatusEffectTelemetryCursor = 71262,
        Shinobu319StatusEffectTuning = 71263,
        Shinobu319StatusEffectCounters = 71264,
        Shinobu319StatusEffectCsvProfiles = 71265,
        Shinobu319StatusEffectScannerReport = 71266,
        Shinobu319StatusEffectVfxRequests = 71267,
        Shinobu319StatusEffectDamageSignals = 71268,
        Shinobu319StatusEffectRequests = 71269,
        ShinobuFluidCompartmentFront = 70780,
        ShinobuFluidCompartmentBack = 70781,
        ShinobuFluidIntegrityState = 70782,
        ShinobuFluidEdgeOffsets = 70783,
        ShinobuFluidEdgeDestinations = 70784,
        ShinobuFluidEdgeFlags = 70785,
        ShinobuFluidCompartmentCentroids = 70786,
        ShinobuFluidWaterlineShader = 70787,
        ShinobuFluidMassState = 70788,
        ShinobuFluidTuning = 70789,
        ShinobuFluidTelemetryRing = 70790,
        ShinobuFluidTelemetryCursor = 70791,
        ShinobuFluidBfsQueue = 70792,
        ShinobuFluidBfsVisited = 70793,
        ShinobuFluidDeltaVolumes = 70794,
        ShinobuFluidFrameSummary = 70795,
        ShinobuFluidCsvScratch = 70796,
        ShinobuFluidMockBreach = 70797,
        ShinobuFluidCompartmentTelemetry = 70798,
        ShinobuFluidEdgeConductivity = 73330,
        ShinobuFluidTransferRemainders = 73331,
        Shinobu345TideTelemetry = 73350,
        Shinobu345SeismicEvents = 73351,
        Shinobu345ShakeOffsets = 73352,
        Shinobu345TurbiditySpikes = 73353,
        Shinobu345SeismicTelemetryRing = 73354,
        Shinobu345SeismicTuning = 73355,
        Shinobu345MockNarrativeTriggers = 73356,
        Shinobu345MockCameraPositions = 73357,
        Shinobu345MockSiltSignals = 73358,
        Shinobu345MockBaseModules = 73359,
        Shinobu345CelestialStateWrite = 73360,
        Shinobu345CelestialStateRead = 73361,
        Shinobu345CelestialTelemetryRing = 73362,
        Shinobu345CelestialTuning = 73363,
        Shinobu345CelestialCsvScratch = 73364,
        Shinobu345CelestialFlowModifiers = 73365,
        Shinobu345CelestialMockTimeline = 73366,
        Shinobu345CelestialOrbitalParameters = 73367,
        Shinobu345EnvironmentState = 73368,
        Shinobu345SeismicStates = 73369,
        Shinobu345WaterSurfaceAupY = 73370,
        Shinobu345SeismicFaultProfiles = 73371,
        Shinobu345SeismicCsvScratch = 73372,
        Shinobu345CelestialPresentationBlackBox = 73393,
        Shinobu345CelestialGradientDay = 73394,
        Shinobu345CelestialGradientSunset = 73395,
        Shinobu345CelestialGradientNight = 73396,
        Shinobu345CelestialLegacyOrbitOutput = 73397,
        ShinobuNetcodeFuzzerInput = 71880,
        ShinobuNetcodeFuzzerHostAuthoritativeInput = 71881,
        ShinobuNetcodeFuzzerClientAuthoritativeInput = 71882,
        ShinobuNetcodeFuzzerClientAppliedInput = 71883,
        ShinobuNetcodeFuzzerHostKinematics = 71884,
        ShinobuNetcodeFuzzerClientKinematics = 71885,
        ShinobuNetcodeFuzzerHostInventory = 71886,
        ShinobuNetcodeFuzzerClientInventory = 71887,
        ShinobuNetcodeFuzzerHostEcosystem = 71888,
        ShinobuNetcodeFuzzerClientEcosystem = 71889,
        ShinobuNetcodeFuzzerSnapshotRing = 71890,
        ShinobuNetcodeFuzzerTelemetryRing = 71891,
        ShinobuNetcodeFuzzerVisualNoise = 71892,
        ShinobuNetcodeFuzzerResult = 71893,
        ShinobuNetcodeFuzzerDeliveryTicks = 71894,
        ShinobuNetcodeFuzzerHostDispatcherState = 71895,
        ShinobuNetcodeFuzzerClientDispatcherState = 71896,
        ToxicOutgassingDensityFront = 72800,
        ToxicOutgassingDensityBack = 72801,
        ToxicOutgassingFlowField = 72802,
        ToxicOutgassingWorldSampler = 72803,
        ToxicOutgassingSources = 72804,
        ToxicOutgassingSourceIds = 72805,
        ToxicOutgassingEntityAups = 72806,
        ToxicOutgassingEntityIds = 72807,
        ToxicOutgassingEntityCorrosionTimers = 72808,
        ToxicOutgassingEntityExposureAccumulators = 72809,
        ToxicOutgassingExposureSignals = 72810,
        ToxicOutgassingStatusSignals = 72811,
        ToxicOutgassingBiolumSignals = 72812,
        ToxicOutgassingSignalCounters = 72813,
        ToxicOutgassingTelemetryRing = 72814,
        ToxicOutgassingTelemetryScratch = 72815,
        ToxicOutgassingConstants = 72816,
        ToxicOutgassingCsvBytes = 72817,
        ToxicOutgassingBinaryProbeBytes = 72818,
        ToxicOutgassingNanFlags = 72819,
        ToxicOutgassingDensityMirror = 72820,
        ToxicOutgassingGridHeader = 72821,
        ToxicOutgassingCellStatesFront = 72822,
        ToxicOutgassingCellStatesBack = 72823,
        ShinobuActiveEquipmentState = 71300,
        ShinobuActiveEquipmentPublishedState = 71301,
        ShinobuActiveEquipmentAupSamples = 71302,
        ShinobuActiveEquipmentGridLoadRequests = 71303,
        ShinobuActiveEquipmentTelemetryRing = 71304,
        ShinobuActiveEquipmentTelemetryCursor = 71305,
        ShinobuActiveEquipmentIntegrationCounters = 71306,
        ShinobuActiveEquipmentCsvScratch = 71307,
        ShinobuActiveEquipmentTuning = 71308,
        ShinobuActiveEquipmentHardwareSpecs = 71309,
        ShinobuActiveEquipmentDumpScratch = 71310,
        ShinobuActiveEquipmentToolStates = 71311,
        ShinobuActiveEquipmentToolStats = 71312,
        ShinobuActiveEquipmentToolTypes = 71313,
        ShinobuActiveEquipmentStatusMasks = 71314,
        ShinobuActiveEquipmentEnvironmentHeat01 = 71315,
        ShinobuActiveEquipmentWearDrainRates = 71316,
        ShinobuAuxiliaryDeployments = 71480,
        ShinobuAuxiliaryStates = 71481,
        ShinobuAuxiliaryActiveCount = 71482,
        ShinobuAuxiliaryTuning = 71483,
        ShinobuAuxiliaryRouteCounters = 71484,
        ShinobuAuxiliaryVfxMatrices = 71485,
        ShinobuAuxiliaryTelemetryRing = 71486,
        ShinobuAuxiliaryTelemetryCursor = 71487,
        ShinobuAuxiliaryProfiles = 71488,
        ShinobuAuxiliaryCsvScratch = 71489,
        ShinobuAuxiliaryActiveEquipmentState = 71490,
        ShinobuAuxiliaryTetherAnchors = 71491,
        PropwashGpuEventRing = 71492,
        PropwashGpuRingCursor = 71493,
        PropwashGpuTelemetryRing = 71494,
        PropwashGpuTuning = 71495,
        PropwashGpuWakeProfiles = 71496,
        JacobianFoamParams = 71920,
        JacobianFoamTuning = 71921,
        JacobianFoamWakeImpacts = 71922,
        JacobianFoamTelemetryRing = 71923,
        JacobianFoamProfiles = 71924,
        JacobianFoamCsvScratch = 71925,
        JacobianFoamDumpScratch = 71926,
        ShinobuVoxelPathRequests = 73420,
        ShinobuVoxelPathRingState = 73421,
        ShinobuVoxelPathSolverState = 73422,
        ShinobuVoxelPathNodes = 73423,
        ShinobuVoxelPathOpenHeap = 73424,
        ShinobuVoxelPathHeapPositions = 73425,
        ShinobuVoxelPathRawPath = 73426,
        ShinobuVoxelPathWaypoints = 73427,
        ShinobuVoxelPathResults = 73428,
        ShinobuVoxelPathTelemetryRing = 73429,
        ShinobuVoxelPathTuning = 73430,
        ShinobuVoxelPathMockSdf = 73431,
        ShinobuVoxelPathSdfHeader = 73432,
        ShinobuVoxelPathSpeciesProfiles = 73433,
        ShinobuVoxelPathSpeciesProfileCount = 73434,
        ShinobuVoxelPathCsvScratch = 73435,
        ShinobuVoxelPathClosedDebug = 73436,
        ShinobuSpawnSdfValidationRequests = 72600,
        ShinobuSpawnSdfValidationRingState = 72601,
        ShinobuSpawnSdfValidationTelemetryRing = 72602,
        ShinobuSpawnSdfValidationTelemetryCursor = 72603,
        ShinobuSpawnSdfValidationTuning = 72604,
        ShinobuSpawnSdfMockSdf = 72605,
        ShinobuSpawnSdfMockHeader = 72606,
        ShinobuSpawnClearanceProfiles = 72607,
        ShinobuSpawnClearanceCsvScratch = 72608,
        ShinobuParasiteTargets = 71980,
        ShinobuParasiteTargetCandidates = 71981,
        ShinobuParasiteTargetCount = 71982,
        ShinobuParasiteTuning = 71983,
        ShinobuParasiteTelemetryRing = 71984,
        ShinobuParasiteTelemetryCursor = 71985,
        ShinobuParasiteProfiles = 71986,
        ShinobuParasiteCsvScratch = 71987,
        ShinobuParasiteScannerSummary = 71989,
        ShinobuParasiteProfileCount = 71990,
        ShinobuTradeMarauderStates = 70720,
        ShinobuTradeMarauderInventories = 70721,
        ShinobuTradeMarauderEconomyWeights = 70722,
        ShinobuTradeMarauderSectorEconomy = 70723,
        ShinobuTradeMarauderRoutes = 70724,
        ShinobuTradeMarauderRouteCounts = 70725,
        ShinobuTradeMarauderAStarOpenHeap = 70726,
        ShinobuTradeMarauderAStarGCosts = 70727,
        ShinobuTradeMarauderAStarCameFrom = 70728,
        ShinobuTradeMarauderAStarNodeStates = 70729,
        ShinobuTradeMarauderTelemetry = 70730,
        ShinobuTradeMarauderTuning = 70731,
        ShinobuTradeMarauderFactionStanding = 70732,
        ShinobuTradeMarauderMockInventoryHashes = 70733,
        ShinobuTradeMarauderMockInventoryQuantities = 70734,
        ShinobuTradeMarauderSignalScratch = 70735,
        ShinobuTradeMarauderLootNodes = 70736,
        ShinobuTradeMarauderSectorHash = 70737,
        ShinobuTradeMarauderCsvScratch = 70738,
        ShinobuTradeMarauderCounters = 70739,
        ShinobuTradeMarauderRoutePlans = 70740,
        ShinobuTradeMarauderAcousticScratch = 70741,
        ShinobuTradeMarauderVisualProxies = 70742,
        ThermodynamicsHazardConstants = 70016,
        ThermodynamicsTemperatureFrontMirror = 70017,
        ThermodynamicsRadiationFrontMirror = 70018,
        ThermodynamicsTemperatureFront = 70019,
        ThermodynamicsTemperatureBack = 70020,
        ThermodynamicsRadiationFront = 70021,
        ThermodynamicsRadiationBack = 70022,
        ThermodynamicsTemperatureSources = 70023,
        ThermodynamicsRadiationSources = 70024,
        ThermodynamicsSources = 70025,
        ThermodynamicsSourceIds = 70026,
        ThermodynamicsEntityAups = 70027,
        ThermodynamicsEntityIds = 70028,
        ThermodynamicsEntityDamageTimers = 70029,
        ThermodynamicsEntityDamageAccumulators = 70030,
        ThermodynamicsMockDamageSignals = 70031,
        ThermodynamicsCombatDamageSignals = 70032,
        ThermodynamicsUpdraftSignals = 70033,
        ThermodynamicsSignalCounters = 70034,
        ThermodynamicsTelemetryRing = 70035,
        ThermodynamicsTelemetryScratch = 70036,
        ThermodynamicsCsvBytes = 70037,
        ThermodynamicsBinaryConstantBytes = 70038,
        AbyssalThermalCellFront = 70039,
        AbyssalThermalCellBack = 70040,
        AbyssalThermalCellInjection = 70041,
        AbyssalThermalHeatSources = 70042,
        AbyssalThermalSourceCount = 70043,
        AbyssalThermalTuning = 70044,
        AbyssalThermalSampleAups = 70045,
        AbyssalThermalSampleResults = 70046,
        AbyssalThermalTelemetryRing = 70047,
        AbyssalThermalProfileBytes = 70048,
        AbyssalThermalProfiles = 70049,
        AbyssalThermalProfileCount = 70050,
        AbyssalThermalShiftScratch = 70051,
        AbyssalThermalManagerTelemetryRing = 70055,
        ShinobuBuoyancyStates = 71620,
        ShinobuBuoyancyForcePackets = 71621,
        ShinobuBuoyancyFlowSamples = 71622,
        ShinobuBuoyancyTuning = 71623,
        ShinobuBuoyancyTelemetryRing = 71624,
        ShinobuBuoyancyTelemetryCursor = 71625,
        ShinobuBuoyancyMaterialVolumes = 71626,
        ShinobuBuoyancyCsvScratch = 71627,
        ShinobuBuoyancyDebugForces = 71629,
        ShinobuBuoyancyCounters = 71630,
        ShinobuBuoyancyBodyBindings = 71631,
        ShinobuSimdLocalPositions = 71632,
        ShinobuSimdVelocities = 71633,
        ShinobuSimdDragCoefficients = 71634,
        ShinobuSimdOutputForces = 71635,
        ShinobuSimdTelemetryRing = 71636,
        ShinobuSimdTelemetryCursor = 71637,
        ShinobuSimdMathTolerances = 71638,
        ShinobuSimdVisibleIndexMask = 71639,
        ShinobuSimdVisibleIndices = 71640,
        ShinobuSimdVisibleCount = 71641,
        ShinobuSimdHydrodynamicTuning = 71642,
        ShinobuBuoyancySleepSdfDensity = 71643,
        ShinobuBuoyancySleepSdfConfig = 71644,
        ShinobuBuoyancySleepTelemetryRing = 71645,
        ShinobuBuoyancySleepTelemetryCursor = 71646,
        ShinobuBuoyancyMaterialSettlingProfiles = 71647,
        ShinobuSeaglideStates = 71660,
        ShinobuSeaglideRequests = 71661,
        ShinobuSeaglideForcePackets = 71662,
        ShinobuSeaglideFlowSamples = 71663,
        ShinobuSeaglideTuning = 71664,
        ShinobuSeaglideTelemetryRing = 71665,
        ShinobuSeaglideTelemetryCursor = 71666,
        ShinobuSeaglideCounters = 71667,
        ShinobuSeaglideBodyBindings = 71668,
        ShinobuSeaglideVisualStates = 71669,
        ShinobuSeaglideAudioSignals = 71670,
        ShinobuSeaglideCavitationSignals = 71671,
        ShinobuSeaglideCsvScratch = 71672,
        ShinobuStormPropagationState = 71712,
        ShinobuStormPropagationWriteState = 71713,
        ShinobuStormPropagationTuning = 71714,
        ShinobuStormPropagationTelemetryRing = 71715,
        ShinobuStormPropagationTelemetryCursor = 71716,
        ShinobuStormPropagationMockWeather = 71717,
        ShinobuStormPropagationImpactProfiles = 71718,
        ShinobuStormPropagationCsvScratch = 71719,
        ShinobuStormPropagationDumpScratch = 71720,
        ShinobuStormPropagationFlowScalar = 71721,
        ShinobuStormPropagationAudioScalar = 71722,
        ShinobuStormPropagationBiolumScalar = 71723,
        ShinobuStormPropagationFogScalar = 71724,
        Shinobu251AddedMassProfiles = 71730,
        Shinobu251HydrodynamicsTelemetry = 71731,
        Shinobu251HullProfiles = 71732,
        Shinobu251CsvScratch = 71733,
        Shinobu251AddedMassTuning = 71734,
        Shinobu332SubmarineGyros = 71780,
        Shinobu332GyroErrors = 71781,
        Shinobu332GyroForcePackets = 71782,
        Shinobu332GyroTelemetry = 71783,
        Shinobu332GyroVisualStates = 71784,
        Shinobu332GyroProfiles = 71785,
        Shinobu332GyroCounters = 71786,
        Shinobu332GyroCsvScratch = 71787,
        ShinobuKccEnvironmentProfile = 71760,
        ShinobuKccEnvironmentGrid = 71761,
        ShinobuKccEnvironmentFlowField = 71762,
        ShinobuKccEnvironmentSdf = 71763,
        ShinobuKccEnvironmentMockMetabolism = 71764,
        ShinobuKccEnvironmentDebug = 71765,
        ShinobuKccEnvironmentTelemetryRing = 71766,
        ShinobuKccEnvironmentTelemetryCursor = 71767,
        ShinobuKccEnvironmentProfiles = 71768,
        ShinobuKccEnvironmentProfileBuckets = 71769,
        ShinobuKccEnvironmentProfileHashes = 71770,
        Shinobu333BallastTanks = 71771,
        Shinobu333BallastCommands = 71772,
        Shinobu333BallastFluidSamples = 71773,
        Shinobu333BallastForcePackets = 71774,
        Shinobu333BallastTelemetryRing = 71775,
        Shinobu333BallastProfiles = 71776,
        Shinobu333BallastTuning = 71777,
        Shinobu333BallastCsvScratch = 71778,
        Shinobu333VesselTelemetry = 71779,
        Shinobu263WaveSpectrum = 71800,
        Shinobu263WaveTuning = 71801,
        Shinobu263WaveRequests = 71802,
        Shinobu263WaveResults = 71803,
        Shinobu263WaveMacroGrid = 71804,
        Shinobu263WaveTelemetryRing = 71805,
        Shinobu263WaveTelemetryCursor = 71806,
        Shinobu263WaveCsvScratch = 71807,
        Shinobu263WaveProfiles = 71808,
        Shinobu263WaveCounters = 71809,
        Shinobu337ReactorStates = 73620,
        Shinobu337ReactorKinematics = 73621,
        Shinobu337ReactorCount = 73622,
        Shinobu337ReactorTuning = 73623,
        Shinobu337ReactorTelemetryRing = 73624,
        Shinobu337ReactorTelemetryCursor = 73625,
        Shinobu337ReactorProfiles = 73626,
        Shinobu337ReactorProfileCount = 73627,
        Shinobu337ReactorCsvScratch = 73628,
        Shinobu337ReactorScratch = 73629,
        Shinobu337ReactorDumpLatch = 73630,
        VRInteractionHandStates = 73680,
        VRInteractionPreviousHandStates = 73681,
        VRInteractionControllerMatrixInputs = 73682,
        VRInteractionSockets = 73683,
        VRInteractionTuning = 73684,
        VRInteractionTelemetryRing = 73685,
        VRInteractionTelemetryCursor = 73686,
        VRInteractionResolvedHandMatrices = 73687,
        Shinobu274RadiationStates = 72740,
        Shinobu274RadiationSources = 72741,
        Shinobu274RadiationSourceCount = 72742,
        Shinobu274RadiationTelemetryRing = 72743,
        Shinobu274RadiationTelemetryCursor = 72744,
        Shinobu274RadiationProfiles = 72745,
        Shinobu274RadiationCsvScratch = 72746,
        Shinobu274RadiationTuning = 72747,
        Shinobu274RadiationDamageSignal = 72748,
        Shinobu274RadiationGridRead = 72749,
        Shinobu274RadiationGridWrite = 72750,
        Shinobu274RadiationGridSource = 72751,
        WreckGeneratorGrid = 132800,
        WreckGeneratorPropagationQueue = 132801,
        WreckGeneratorAllPlacements = 132802,
        WreckGeneratorFilteredPlacements = 132803,
        WreckGeneratorRuntimeDefinitions = 132804,
        WreckGeneratorLootRecords = 132805,
        WreckGeneratorDebrisRecords = 132806,
        WreckGeneratorDebrisSpatialKeys = 132807,
        WreckGeneratorDebrisClusters = 132808,
        WreckGeneratorArtifactRecords = 132809,
        WreckGeneratorScorchDecalRecords = 132810,
        WreckGeneratorBurialCutRecords = 132811,
        WreckGeneratorTelemetryEntries = 132812,
        WreckGeneratorRenderWorldMatrices = 132813,
        WreckGeneratorRenderModuleIds = 132814,
        WreckGeneratorRenderAges = 132815,
        WreckBrgBatchMetadata = 132816,

        // AUTO-MIGRATED LOCAL CASTS
        ShinobuApexBrainVault_ApexState = 70609,
        ShinobuApexBrainVault_MockPlayerAup = 70610,
        ShinobuApexBrainVault_AcousticEchoTap = 70611,
        ShinobuApexBrainVault_Tuning = 70612,
        ShinobuApexBrainVault_EmergencyStats = 70613,
        ShinobuApexBrainVault_MockWorldSampler = 70614,
        ShinobuApexBrainVault_Output = 70615,
        ShinobuApexBrainVault_ProximitySignal = 70616,
        ShinobuApexBrainVault_CombatDamageSignal = 70617,
        ShinobuApexBrainVault_PanicSignal = 70618,
        ShinobuApexBrainVault_InfluenceNodes = 70619,
        UtilityAICognitionVault_States = 71960,
        UtilityAICognitionVault_Aups = 71961,
        UtilityAICognitionVault_Targets = 71962,
        UtilityAICognitionVault_TargetNext = 71963,
        UtilityAICognitionVault_BucketHeads = 71964,
        UtilityAICognitionVault_Tuning = 71965,
        UtilityAICognitionVault_Outputs = 71966,
        UtilityAICognitionVault_TelemetryRing = 71967,
        UtilityAICognitionVault_TelemetryCursor = 71968,
        UtilityAICognitionVault_Profiles = 71969,
        UtilityAICognitionVault_CsvScratch = 71970,
        UtilityAICognitionVault_AnxietyDecay_Profiles = 71971,
        UtilityAICognitionVault_AnxietyDecay_Tuning = 71972,
        UtilityAICognitionVault_AnxietyDecay_Scratch = 71973,
        UtilityAICognitionVault_AnxietyDecay_TelemetryRing = 71974,
        UtilityAICognitionVault_AnxietyDecay_TelemetryCursor = 71975,
        UtilityAICognitionVault_AnxietyDecay_ShelterSdf = 71976,
        UtilityAICognitionVault_AnxietyDecay_ShelterHeader = 71977,
        UtilityAICognitionVault_AnxietyDecay_CsvScratch = 71978,
        ProceduralBoneBlenderTypes_Rigs = 71680,
        ProceduralBoneBlenderTypes_FrameInputs = 71681,
        ProceduralBoneBlenderTypes_ParentIndices = 71682,
        ProceduralBoneBlenderTypes_BindPoses = 71683,
        ProceduralBoneBlenderTypes_BoneStates = 71684,
        ProceduralBoneBlenderTypes_BoneMatrices = 71685,
        ProceduralBoneBlenderTypes_FrameStats = 71686,
        ProceduralBoneBlenderTypes_TelemetryRing = 71687,
        ProceduralBoneBlenderTypes_TelemetryCursor = 71688,
        ProceduralBoneBlenderTypes_Tuning = 71689,
        ProceduralBoneBlenderTypes_MockAiSignals = 71690,
        KineticCharacterAnimatorTypes_Rigs = 13671360,
        KineticCharacterAnimatorTypes_FrameInputs = 13671361,
        KineticCharacterAnimatorTypes_ParentIndices = 13671362,
        KineticCharacterAnimatorTypes_BindPoses = 13671363,
        KineticCharacterAnimatorTypes_BoneOutputs = 13671364,
        KineticCharacterAnimatorTypes_BoneMatrices = 13671365,
        KineticCharacterAnimatorTypes_IkTargets = 13671366,
        KineticCharacterAnimatorTypes_FrameStats = 13671367,
        KineticCharacterAnimatorTypes_TelemetryRing = 13671368,
        KineticCharacterAnimatorTypes_TelemetryCursor = 13671369,
        KineticCharacterAnimatorTypes_Tuning = 13671370,
        KineticCharacterAnimatorTypes_CsvScratch = 13671371,
        BaseAtmosphereEngine_FrontBufferId = 1111577409,
        BaseAtmosphereEngine_BackBufferId = 1111577410,
        BaseAtmosphereEngine_CarbonDioxideByteLaneBufferId = 1111577411,
        BaseAtmosphereEngine_BlackBoxBufferId = 1111577412,
        GasDynamicsSolver_RoomBaseIndexBufferId = 74436,
        GasDynamicsSolver_BasePlayerInsideBufferId = 74437,
        GasDynamicsSolver_BasePlayerInsideCountBufferId = 74438,
        GasDynamicsSolver_BaseRoomStartBufferId = 74439,
        PlayerCriticalProceduralAudioRenderer_PlayerCriticalSonarEchoTapUploadRingBufferId = 70889,
        PlayerCriticalProceduralAudioRenderer_PlayerCriticalPrologueTransitionRingBufferId = 70890,
        PlayerCriticalProceduralAudioRenderer_PlayerCriticalAudioSynthesisTelemetryRingBufferId = 70891,
        ProceduralAudioEvents_PendingAudioEventsBufferId = 70885,
        ProceduralAudioEvents_NextFrameAudioEventsBufferId = 70886,
        GameBootstrapper_BootstrapShaderWarmupTelemetryRingBufferId = 76000,
        DroneFleetManager_PendingEventBufferId = 72041,
        DroneFleetManager_NextFrameEventBufferId = 72042,
        DroneFleetManager_DroneFleetStateDtoBufferId = 70265,
        DroneFleetManager_DroneFleetTargetDtoBufferId = 70266,
        DroneFleetManager_DroneFleetAssignmentTasksBufferId = 70267,
        DroneFleetManager_DroneFleetProceduralArgsBufferId = 70268,
        DroneFleetManager_DroneFleetServiceCommandsBufferId = 70269,
        DroneFleetManager_DroneFleetChassisSpecsBufferId = 72043,
        DroneFleetManager_DroneFleetAStarPersistentStatesBufferId = 72045,
        DroneFleetManager_Transactions_DroneFleetTransactionTasksBufferId = 72046,
        DroneFleetManager_Transactions_DroneFleetTransactionIntegrityBufferId = 72047,
        DroneFleetManager_Transactions_DroneFleetTransactionResultsBufferId = 72048,
        DroneFleetManager_Transactions_DroneFleetTransactionCountersBufferId = 72049,
        DroneFleetManager_Transactions_DroneFleetTransactionCommandConsumedBufferId = 72050,
        DroneFleetManager_Transactions_DroneFleetTransactionTelemetryBufferId = 72051,
        DroneFleetManager_Transactions_DroneFleetTransactionCommandsBufferId = 72052,
        DroneFleetManager_Transactions_DroneFleetTransactionAupSnapshotsBufferId = 72053,
        FluidPipeGraphRuntime_PipePressureBufferId = 72080,
        FluidPipeGraphRuntime_PipeContentsBufferId = 72081,
        FluidPipeGraphRuntime_PipeFlagsBufferId = 72082,
        FluidPipeGraphRuntime_PipeContentKindsBufferId = 72083,
        FluidPipeGraphRuntime_PipeNetworkIdsBufferId = 72084,
        FluidPipeGraphRuntime_PipeRoomIndicesBufferId = 72085,
        FluidPipeGraphRuntime_PipeCapacitiesBufferId = 72086,
        FluidPipeGraphRuntime_PipeMaxPressureBufferId = 72087,
        FluidPipeGraphRuntime_PipeFlowRatesBufferId = 72088,
        FluidPipeGraphRuntime_PipeSourceRatesBufferId = 72089,
        FluidPipeGraphRuntime_PipeDemandRatesBufferId = 72090,
        FluidPipeGraphRuntime_PipeFlowVectorsBufferId = 72091,
        FluidPipeGraphRuntime_PipeRoomExchangeContentsBufferId = 72092,
        FluidPipeGraphRuntime_PipeLastVisualFlowBufferId = 72093,
        FluidPipeGraphRuntime_PipeAupsBufferId = 72094,
        FluidPipeGraphRuntime_PipeTelemetryRingBufferId = 72095,
        FluidPipeGraphRuntime_PipeRuptureTelemetryRingBufferId = 72096,
        FluidPipeGraphRuntime_PipeRuptureBudgetBufferId = 72097,
        FluidPipeGraphRuntime_PipeConnectionSourcesBufferId = 72098,
        FluidPipeGraphRuntime_PipeConnectionDestinationsBufferId = 72099,
        FluidPipeGraphRuntime_PipeRuptureDispatchBufferId = 72100,
        FluidPipeGraphRuntime_PipeConnectionOffsetsBufferId = 72101,
        FluidPipeGraphRuntime_PipeConnectionCsrDestinationsBufferId = 72102,
        FluidPipeGraphRuntime_PipeConnectionWriteCursorBufferId = 72103,
        HabitatConstructionManager_IntegrityNodeBufferId = 70949,
        HabitatConstructionManager_IntegrityRangeBufferId = 70950,
        HabitatConstructionManager_IntegrityAdjacencyBufferId = 70951,
        HabitatConstructionManager_IntegrityQueueBufferId = 70952,
        HabitatConstructionManager_IntegrityDepthBufferId = 70953,
        HabitatConstructionManager_IntegrityResultBufferId = 70954,
        HabitatConstructionManager_IntegrityDegreeScratchBufferId = 70955,
        HabitatConstructionManager_IntegrityWriteScratchBufferId = 70956,
        HabitatConstructionManager_IntegrityConnectionBufferId = 70957,
        HabitatConstructionManager_IntegritySocketLookupBufferId = 70958,
        HabitatGraphManager_HabitatFloodBlackBoxBufferId = 72120,
        HabitatGraphManager_HabitatFloodPropagationSummaryBufferId = 72121,
        HabitatGraphManager_HabitatSiegeTargetsBufferId = 72122,
        HabitatGraphManager_HabitatModuleStressScalarsBufferId = 72123,
        HabitatGraphManager_HabitatPreviousModuleStressScalarsBufferId = 72124,
        HabitatGraphManager_HabitatModuleImpactStressSpikesBufferId = 72125,
        HabitatGraphManager_HabitatModuleCompromisedFlagsBufferId = 72126,
        HabitatGraphManager_HabitatRoomWaterLevelsBufferId = 72127,
        HabitatGraphManager_HabitatRoomVolumesBufferId = 72128,
        HabitatGraphManager_HabitatRoomFloodDeltaLevelsBufferId = 72129,
        HabitatGraphManager_HabitatRoomFlagsBufferId = 72130,
        HabitatGraphManager_HabitatGraphNodesBufferId = 72131,
        HabitatGraphManager_HabitatGraphEdgeOffsetsBufferId = 72132,
        HabitatGraphManager_HabitatGraphEdgeDestinationsBufferId = 72133,
        HabitatGraphManager_HabitatGraphEdgeResistanceBufferId = 72134,
        HabitatGraphManager_HabitatGraphEdgeWriteCursorBufferId = 72135,
        HabitatGraphManager_HabitatGraphAnchorReachabilityBufferId = 72136,
        HabitatGraphManager_HabitatGraphTraversalVisitedBufferId = 72137,
        HabitatGraphManager_HabitatGraphAnchorTraversalQueueBufferId = 72138,
        HabitatGraphManager_HabitatGraphEdgeFlagsBufferId = 72139,
        LogisticsRouteScratchMemory_EdgeOffsetsBufferId = 72032,
        LogisticsRouteScratchMemory_EdgeDestinationsBufferId = 72033,
        LogisticsRouteScratchMemory_EdgeWriteCursorBufferId = 72034,
        LogisticsRouteScratchMemory_StorageCapacityByNodeBufferId = 72035,
        LogisticsRouteScratchMemory_VisitedBufferId = 72036,
        LogisticsRouteScratchMemory_QueueBufferId = 72037,
        LogisticsRouteScratchMemory_ResultNodeIndexBufferId = 72038,
        RepairDroneTorchAcousticEvents_PendingEventBufferId = 72039,
        RepairDroneTorchAcousticEvents_NextFrameEventBufferId = 72040,
        ShinobuSocketConstructionData_BuilderGhostStateBufferId = 70940,
        ShinobuSocketConstructionData_BuilderGhostVisualBufferId = 70941,
        ShinobuSocketConstructionData_BuilderGhostTelemetryBufferId = 70942,
        ShinobuSocketConstructionData_BuilderGhostSdfSamplesBufferId = 70944,
        ShinobuSocketConstructionData_BuilderGhostIndirectArgsBufferId = 70945,
        SumpPumpPipeGridContracts_PumpNodes = 95820,
        SumpPumpPipeGridContracts_PipeEdges = 95821,
        SumpPumpPipeGridContracts_NodeAup = 95822,
        SumpPumpPipeGridContracts_PumpRoomIndices = 95823,
        SumpPumpPipeGridContracts_CsrOffsets = 95824,
        SumpPumpPipeGridContracts_CsrDestinations = 95825,
        SumpPumpPipeGridContracts_CsrConductance = 95826,
        SumpPumpPipeGridContracts_CsrFlow = 95827,
        SumpPumpPipeGridContracts_CsrFlatEdgeIndex = 95828,
        SumpPumpPipeGridContracts_CsrWriteCursor = 95829,
        SumpPumpPipeGridContracts_PressureFront = 95830,
        SumpPumpPipeGridContracts_PressureBack = 95831,
        SumpPumpPipeGridContracts_PowerPotential = 95832,
        SumpPumpPipeGridContracts_PumpRemainder = 95833,
        SumpPumpPipeGridContracts_Tuning = 95834,
        SumpPumpPipeGridContracts_TelemetryRing = 95835,
        SumpPumpPipeGridContracts_TelemetryCursor = 95836,
        SumpPumpPipeGridContracts_Counters = 95837,
        SumpPumpPipeGridContracts_PipeProfiles = 95838,
        SumpPumpPipeGridContracts_FrameSummary = 95840,
        SumpPumpPipeGridContracts_FlowGpu = 95841,
        SumpPumpPipeGridContracts_PumpMassError = 95842,
        SumpPumpPipeGridContracts_RoomDrainLocks = 95843,
        SumpPumpPipeGridContracts_PumpBaseMaxRate = 95844,
        SumpPumpPipeGridContracts_PumpPowerNodeHashes = 95845,
        VRPipeBlueprintPreview_PipeStateBufferId = 70946,
        VRPipeBlueprintPreview_PipeVisualBufferId = 70947,
        VRPipeBlueprintPreview_PipeIndirectArgsBufferId = 70948,
        ConstructionManager_DeconstructionDfsStackBufferId = 72140,
        ConstructionManager_DeconstructionDfsVisitedBufferId = 72141,
        ConstructionManager_DeconstructionDfsResultBufferId = 72142,
        ConstructionManager_DeconstructionBlackBoxBufferId = 72143,
        ConstructionManager_DeconstructionFallbackCostsBufferId = 72144,
        H8StaticDataContracts_BTreeTelemetryRingBufferId = 72070,
        H8StaticDataContracts_BTreeTelemetryCursorBufferId = 72071,
        H8StaticDataContracts_BTreeTelemetryAccumulatorBufferId = 72072,
        H8StaticDataContracts_BTreeTuningProfilesBufferId = 72073,
        AsynchronousTelemetryExporter_EventRing = 71860,
        AsynchronousTelemetryExporter_Staging = 71861,
        AsynchronousTelemetryExporter_Counters = 71862,
        AsynchronousTelemetryExporter_TelemetryRing = 71863,
        AsynchronousTelemetryExporter_TelemetryCursor = 71864,
        AsynchronousTelemetryExporter_Tuning = 71865,
        AsynchronousTelemetryExporter_CsvScratch = 71866,
        AsynchronousTelemetryExporter_CompressedScratch = 71867,
        AsynchronousTelemetryExporter_HeatmapDebug = 71868,
        AsynchronousTelemetryExporter_HandoffA = 71869,
        AsynchronousTelemetryExporter_HandoffB = 71870,
        AsynchronousTelemetryExporter_WorkerAccum = 71871,
        AsynchronousTelemetryExporter_RawBatchScratch = 71872,
        AsynchronousTelemetryExporter_DumpSnapshot = 71873,
        AsynchronousTelemetryExporter_RoutineIngress = 71874,
        AsynchronousTelemetryExporter_CriticalIngress = 71875,
        AsynchronousTelemetryExporter_IngressCursor = 71876,
        FoveatedSimulationManager_FoveatedScorePositionsBufferId = 73220,
        FoveatedSimulationManager_FoveatedEntityAupsBufferId = 73221,
        FoveatedSimulationManager_FoveatedImportanceScoresBufferId = 73222,
        FoveatedSimulationManager_FoveatedTickRateCodesBufferId = 73223,
        FoveatedSimulationManager_FoveatedInsideFrustumFlagsBufferId = 73224,
        FoveatedSimulationManager_FoveatedEntitySimTiersBufferId = 73225,
        FoveatedSimulationManager_FoveatedDistancesMetersBufferId = 73226,
        FoveatedSimulationManager_FoveatedFromPositionsBufferId = 73227,
        FoveatedSimulationManager_FoveatedToPositionsBufferId = 73228,
        FoveatedSimulationManager_FoveatedAlphasBufferId = 73229,
        FoveatedSimulationManager_FoveatedTelemetryRingBufferId = 73234,
        MathGuard_InvalidNumberCodesBufferId = 70883,
        MathGuard_InvalidNumberCounterBufferId = 70884,
        MemorySentinelRuntime_ValidationStatesBuffer = 70873,
        MemorySentinelRuntime_TargetsBuffer = 70874,
        MemorySentinelRuntime_ResultsBuffer = 70875,
        MemorySentinelRuntime_RollbackBytesBuffer = 70876,
        MemorySentinelRuntime_MockInventoryBuffer = 70877,
        MemorySentinelRuntime_TelemetryBuffer = 70878,
        MemorySentinelRuntime_RuntimeStateBuffer = 70879,
        MemorySentinelRuntime_AupSnapshotBuffer = 70880,
        MemorySentinelRuntime_ModQuarantineBuffer = 70882,
        AupOriginShiftCoordinator_MockStatesBuffer = 73030,
        AupOriginShiftCoordinator_MockVelocitiesBuffer = 73031,
        AupOriginShiftCoordinator_MockHistoricalPointsBuffer = 73032,
        AupOriginShiftCoordinator_TelemetryRingBuffer = 73033,
        AupOriginShiftCoordinator_TelemetryDetailRingBuffer = 73056,
        AupOriginShiftCoordinator_RuntimeStateBuffer = 73034,
        AupOriginShiftCoordinator_MockCameraBuffer = 73035,
        AupOriginShiftCoordinator_CounterBuffer = 73037,
        AupPrecisionJobs_TargetAupsBuffer = 73200,
        AupPrecisionJobs_RuntimeStateBuffer = 73201,
        AupPrecisionJobs_LocalOffsetsBuffer = 73202,
        AupPrecisionJobs_ResultFlagsBuffer = 73203,
        AupPrecisionJobs_TelemetryRingBuffer = 73204,
        AupPrecisionJobs_ToleranceProfilesBuffer = 73205,
        AupPrecisionJobs_CsvScratchBuffer = 73206,
        AupPrecisionJobs_MockExtremeAupsBuffer = 73207,
        AupPrecisionJobs_FaultCounterBuffer = 73208,
        SignalWardenRuntime_ProfileBufferId = 73040,
        SignalWardenRuntime_ProfileCountBufferId = 73041,
        SignalWardenRuntime_SignalTelemetryRingBufferId = 73038,
        SignalWardenRuntime_SignalTelemetryCursorBufferId = 73039,
        SignalWardenRuntime_FrontBytesBufferId = 73043,
        SignalWardenRuntime_BackBytesBufferId = 73044,
        SignalWardenRuntime_FrontHeadersBufferId = 73045,
        SignalWardenRuntime_BackHeadersBufferId = 73046,
        SignalWardenRuntime_CommittedSignalsBufferId = 73047,
        SignalWardenRuntime_CommittedCountBufferId = 73048,
        SignalWardenRuntime_TelemetryRingBufferId = 73049,
        SignalWardenRuntime_TelemetryCursorBufferId = 73050,
        SignalWardenRuntime_TuningBufferId = 73051,
        SignalWardenRuntime_CoalescenceBucketsBufferId = 73052,
        SignalWardenRuntime_OverflowSignalsBufferId = 73053,
        SignalWardenRuntime_OverflowHeaderBufferId = 73054,
        OceanAdapterVaultRoute_RequestBufferID = 72960,
        OceanAdapterVaultRoute_ResultBufferID = 72961,
        OceanAdapterVaultRoute_TelemetryRingBufferID = 72962,
        OceanAdapterVaultRoute_ProfileBufferID = 72963,
        OceanAdapterVaultRoute_GlobalWaterLevelBufferID = 72964,
        OceanAdapterVaultRoute_CsvScratchBufferID = 72965,
        FaunaKinematicsRuntime_TerrainSdfSnapshotBuffer = 71337,
        PredatorCognitionDomain_AcousticSdf_AcousticStimuliBufferId = 72760,
        PredatorCognitionDomain_AcousticSdf_AcousticStimulusCountBufferId = 72761,
        PredatorCognitionDomain_AcousticSdf_AcousticResultsBufferId = 72762,
        PredatorCognitionDomain_AcousticSdf_AcousticTelemetryRingBufferId = 72763,
        PredatorCognitionDomain_AcousticSdf_AcousticTelemetryCursorBufferId = 72764,
        PredatorCognitionDomain_AcousticSdf_AcousticHearingProfilesBufferId = 72765,
        PredatorCognitionDomain_AcousticSdf_AcousticHearingProfileCountBufferId = 72766,
        PredatorCognitionDomain_AcousticSdf_AcousticTuningBufferId = 72767,
        PredatorCognitionDomain_AcousticSdf_AcousticCsvScratchBufferId = 72768,
        AirlockPressurizationContracts_AirlockStates = 73380,
        AirlockPressurizationContracts_Tuning = 73381,
        AirlockPressurizationContracts_DoorPoses = 73382,
        AirlockPressurizationContracts_ExchangeIndices = 73383,
        AirlockPressurizationContracts_EvaluationResults = 73384,
        AirlockPressurizationContracts_BulkheadIntents = 73385,
        AirlockPressurizationContracts_VfxSignals = 73386,
        AirlockPressurizationContracts_AcousticSignals = 73387,
        AirlockPressurizationContracts_TelemetryRing = 73388,
        AirlockPressurizationContracts_TelemetryCursor = 73389,
        AirlockPressurizationContracts_HardwareProfiles = 73390,
        AirlockPressurizationContracts_DebugGizmos = 73391,
        AirlockPressurizationContracts_DumpRequested = 73392,
        BallisticsRuntime_TrajectoriesA = 71270,
        BallisticsRuntime_TrajectoriesB = 71271,
        BallisticsRuntime_AabbPrimitives = 71272,
        BallisticsRuntime_HitResults = 71273,
        BallisticsRuntime_PenetrationLut = 71274,
        BallisticsRuntime_TelemetryRing = 71275,
        BallisticsRuntime_Counters = 71276,
        BallisticsRuntime_Tuning = 71277,
        BallisticsRuntime_ImpactVfx = 71278,
        CombatDamageRuntime_VaultViews_CombatDamageSignalsBufferId = 1417000,
        CombatDamageRuntime_VaultViews_CombatDamageSignalDetailsBufferId = 1417001,
        CombatDamageRuntime_VaultViews_CombatDamageTargetLookupKeysBufferId = 1417002,
        CombatDamageRuntime_VaultViews_CombatDamageTargetLookupSlotsBufferId = 1417003,
        CombatDamageRuntime_VaultViews_CombatDamageInstanceIdsBufferId = 1417004,
        CombatDamageRuntime_VaultViews_CombatDamageHealthBufferId = 1417005,
        CombatDamageRuntime_VaultViews_CombatDamageMaxHealthBufferId = 1417006,
        CombatDamageRuntime_VaultViews_CombatDamageInvMaxHealthBufferId = 1417007,
        CombatDamageRuntime_VaultViews_CombatDamageArmorValuesBufferId = 1417008,
        CombatDamageRuntime_VaultViews_CombatDamageShieldValuesBufferId = 1417009,
        CombatDamageRuntime_VaultViews_CombatDamageMinorAccumulatorsBufferId = 1417010,
        CombatDamageRuntime_VaultViews_CombatDamageTargetForwardBufferId = 1417011,
        CombatDamageRuntime_VaultViews_CombatDamageTargetHeightsBufferId = 1417012,
        CombatDamageRuntime_VaultViews_CombatDamageTargetFlagsBufferId = 1417013,
        CombatDamageRuntime_VaultViews_CombatDamageStatusMasksBufferId = 1417014,
        CombatDamageRuntime_VaultViews_CombatDamageStatusDurations0123BufferId = 1417015,
        CombatDamageRuntime_VaultViews_CombatDamageLegacyStatusDurations4567BufferId = 1417016,
        CombatDamageRuntime_VaultViews_CombatDamageBrittleDurationsBufferId = 1417017,
        CombatDamageRuntime_VaultViews_CombatDamageArmorLutBufferId = 1417018,
        CombatDamageRuntime_VaultViews_CombatDamageResultsBufferId = 1417019,
        CombatDamageRuntime_VaultViews_CombatDamageStatusResultsBufferId = 1417020,
        CombatDamageRuntime_VaultViews_CombatDamageStatusResultActiveBufferId = 1417021,
        CombatDamageRuntime_VaultViews_CombatDamageCountersBufferId = 1417022,
        CombatDamageRuntime_VaultViews_CombatDamageTelemetryRingBufferId = 1417023,
        CombatDamageRuntime_VaultViews_CombatDamageTelemetryStateBufferId = 1417024,
        HectonCombatRuntime_ArmorPenetration_SignalImpactAups = 73580,
        HectonCombatRuntime_ArmorPenetration_TargetRootAups = 73581,
        HectonCombatRuntime_ArmorPenetration_TargetRotations = 73582,
        HectonCombatRuntime_ArmorPenetration_TargetHalfExtents = 73583,
        HectonCombatRuntime_ArmorPenetration_TargetArmorProfiles = 73584,
        HectonCombatRuntime_ArmorPenetration_TelemetryRing = 73585,
        HectonCombatRuntime_ArmorPenetration_DebugHits = 73586,
        HectonCombatRuntime_ArmorPenetration_Tuning = 73587,
        HectonCombatRuntime_ArmorPenetration_MockRequests = 73588,
        HectonCombatRuntime_ArmorPenetration_MockDetails = 73589,
        HectonCombatRuntime_ArmorPenetration_MockAups = 73590,
        HectonCombatRuntime_ArmorPenetration_MockTargetSlots = 73591,
        HectonCombatRuntime_ArmorPenetration_TortureRequests = 73592,
        HectonCombatRuntime_ArmorPenetration_TortureDetails = 73593,
        HectonCombatRuntime_ArmorPenetration_TortureAups = 73594,
        HectonCombatRuntime_ArmorPenetration_TortureTargetSlots = 73595,
        HectonCombatRuntime_ArmorPenetration_TortureResolvedHits = 73596,
        HectonCombatRuntime_ArmorPenetration_CasTortureHealth = 73597,
        HectonCombatRuntime_ArmorPenetration_CasTortureSuccesses = 73598,
        RadiationHazardGrid_RadiationSdfSnapshotBuffer = 72752,
        SuitUpgradeManager_SuitUpgradeTelemetryRingBuffer = 71411,
        AbyssalShadowCullingTypes_Instances = 75940,
        AbyssalShadowCullingTypes_States = 75941,
        AbyssalShadowCullingTypes_IlluminationScalars = 75942,
        AbyssalShadowCullingTypes_FrustumPlanes = 75943,
        AbyssalShadowCullingTypes_Counters = 75944,
        AbyssalShadowCullingTypes_TelemetryRing = 75945,
        AbyssalShadowCullingTypes_RuntimeState = 75946,
        AbyssalShadowCullingTypes_ProfileRules = 71347,
        AbyssalShadowCullingTypes_CsvScratch = 71348,
        AbyssalShadowCullingTypes_HzbDepthTiles = 71349,
        AbyssalShadowCullingTypes_IndirectArgs = 71350,
        TBDRPipelineSurgeonTypes_VertexBudgetCounters = 70820,
        TBDRPipelineSurgeonTypes_TileWarnings = 70821,
        TBDRPipelineSurgeonTypes_TransparentQuadCounters = 70822,
        TBDRPipelineSurgeonTypes_TelemetryRing = 70823,
        TBDRPipelineSurgeonTypes_MockVisibleInstances = 70824,
        TBDRPipelineSurgeonTypes_SortScratch = 70825,
        TBDRPipelineSurgeonTypes_MeshVertexCounts = 70826,
        TBDRPipelineSurgeonTypes_RadixHistogram = 70827,
        TBDRPipelineSurgeonTypes_VisibleCountOut = 70828,
        TBDRPipelineSurgeonTypes_MockQualitySignal = 70829,
        TBDRPipelineSurgeonTypes_MockCamera = 70830,
        TBDRPipelineSurgeonTypes_SourceFrustumPlanes = 70831,
        TBDRPipelineSurgeonTypes_SqueezedFrustumPlanes = 70832,
        TBDRPipelineSurgeonTypes_HzbVisibilityMask = 70833,
        TBDRPipelineSurgeonTypes_IndirectDrawArgs = 70834,
        TBDRPipelineSurgeonTypes_MigratedID_70835 = 70835,
        HullIntegrityRuntime_DeformationStatesBufferId = 70090,
        HullIntegrityRuntime_HullImpactScratchBufferId = 70091,
        HullIntegrityRuntime_DeformationTelemetryBufferId = 70092,
        HullIntegrityRuntime_DeformationTelemetryCursorBufferId = 70093,
        HullIntegrityRuntime_BreachJetsBufferId = 70094,
        HullIntegrityRuntime_BreachJetArgsBufferId = 70095,
        HullIntegrityRuntime_HullMaterialStrengthBufferId = 70096,
        HullIntegrityRuntime_HullMaterialStrengthCsvScratchBufferId = 70097,
        HullIntegrityRuntime_ExternalPressure01BufferId = 70098,
        HullIntegrityRuntime_PendingVisualImpactsBufferId = 70099,
        HectonBoidController_BoidBlackBoxBufferId = 71979,
        HectonDirectorAI_PredatorSpatialAbsolutePositionsBufferId = 73238,
        HectonDirectorAI_PredatorSpatialCellCoordsBufferId = 73239,
        HectonFluidEngine_FluidImpactEventRingBufferId = 70887,
        HectonFluidEngine_FluidPositionsBufferId = 1322000,
        HectonFluidEngine_FluidPreviousPositionsBufferId = 1322001,
        HectonFluidEngine_FluidPreviousPositionValidBufferId = 1322002,
        HectonFluidEngine_FluidVelocitiesBufferId = 1322003,
        HectonFluidEngine_FluidAngularVelocitiesBufferId = 1322004,
        HectonFluidEngine_FluidUpVectorsBufferId = 1322005,
        HectonFluidEngine_FluidSurfaceUpVectorsBufferId = 1322006,
        HectonFluidEngine_FluidBuoyancyParamsBufferId = 1322007,
        HectonFluidEngine_FluidWaveOffsetsBufferId = 1322008,
        HectonFluidEngine_FluidSleepMaskBufferId = 1322009,
        HectonFluidEngine_FluidLocalGerstnerWavesBufferId = 1322010,
        HectonFluidEngine_FluidGpuBuoyancyForcesYBufferId = 1322011,
        HectonFluidEngine_FluidResultForcesBufferId = 1322012,
        HectonFluidEngine_FluidResultTorquesBufferId = 1322013,
        HectonFluidEngine_FluidOceanSurfaceTelemetryBufferId = 1322014,
        HectonFluidEngine_FluidImpactEventScratchBufferId = 1322015,
        HectonFluidEngine_FluidImpactEventFlagsBufferId = 1322016,
        HectonFluidEngine_FluidGpuBuoyancyObjectUploadBufferId = 1322017,
        HectonFluidEngine_FluidGpuBuoyancyReadbackBufferId = 1322018,
        HectonFluidEngine_FluidBrineHeightsBufferId = 1322019,
        HectonFluidEngine_FluidBrineDensityMultipliersBufferId = 1322020,
        HectonFluidEngine_FluidBrineCartographySectorsBufferId = 1322021,
        HectonFluidEngine_FluidBrineFlagsBufferId = 1322022,
        HectonFluidEngine_FluidGpuAbyssalHeatSourceUploadBufferId = 1322023,
        HectonFluidEngine_FluidActiveThrusterFlowsBufferId = 1322024,
        HectonFluidEngine_FluidActiveWhirlpoolsBufferId = 1322025,
        HectonFluidEngine_FluidActiveMaelstromsBufferId = 1322026,
        HectonFluidEngine_FluidMaelstromTelemetryBufferId = 1322027,
        HectonFluidEngine_FluidActiveViscosityRegionsBufferId = 1322028,
        HectonFluidEngine_FluidViscosityGradientLutBufferId = 1322029,
        HectonFluidEngine_FluidPrebakedVectorNoiseFieldBufferId = 1322030,
        HectonFluidEngine_FluidAdvectedSiltUploadBufferId = 1322031,
        HectonFluidEngine_FluidAdvectedBubbleUploadBufferId = 1322032,
        HectonFluidEngine_FluidAdvectedDebrisUploadBufferId = 1322033,
        HectonFluidEngine_FluidEmptyAbyssalFlowUploadBufferId = 1322034,
        HectonFluidEngine_FluidAdvectionTelemetryBufferId = 1322035,
        HectonFluidEngine_FluidSplashdownImpulseUploadBufferId = 1322036,
        HectonFluidEngine_FluidSplashdownImpulseStatsBufferId = 1322037,
        HectonFluidEngine_FluidAbyssalFlowTelemetryBufferId = 1322038,
        HectonFluidEngine_FluidSovereigntyTelemetryRingBufferId = 1322039,
        HectonFluidEngine_FluidSovereigntyTelemetryCursorBufferId = 1322040,
        HectonFluidEngine_FluidAdvectedSiltDirtyPagesBufferId = 1322041,
        HectonFluidEngine_FluidAdvectedBubbleDirtyPagesBufferId = 1322042,
        HectonFluidEngine_FluidAdvectedDebrisDirtyPagesBufferId = 1322043,
        DynamicPointLightCullingContracts_Sources = 71440,
        DynamicPointLightCullingContracts_States = 71441,
        DynamicPointLightCullingContracts_Settings = 71442,
        DynamicPointLightCullingContracts_GpuPayloadFront = 71443,
        DynamicPointLightCullingContracts_GpuPayloadBack = 71444,
        DynamicPointLightCullingContracts_TelemetryRing = 71445,
        DynamicPointLightCullingContracts_TelemetryCursor = 71446,
        DynamicPointLightCullingContracts_ImportanceKeys = 71447,
        DynamicPointLightCullingContracts_ImportanceIndices = 71448,
        DynamicPointLightCullingContracts_SortScratchKeys = 71449,
        DynamicPointLightCullingContracts_SortScratchIndices = 71450,
        DynamicPointLightCullingContracts_CsvScratch = 71451,
        DynamicPointLightCullingContracts_ProfileRules = 71452,
        DynamicPointLightCullingContracts_MockSdfSamples = 71453,
        DynamicPointLightCullingContracts_DynamicProbeLights = 71454,
        DynamicPointLightCullingContracts_RuntimeCounters = 71455,
        DynamicPointLightCullingContracts_FrustumPlanes = 71456,
        DynamicPointLightCullingContracts_SelfAudit = 71457,
        DynamicPointLightCullingContracts_SourceManifest = 71458,
        HectonGIRelaySystem_SHDayBuffer = 6490144,
        HectonGIRelaySystem_SHNightBuffer = 6490145,
        HectonGIRelaySystem_SHDiscreteStatesBuffer = 6490146,
        HectonGIRelaySystem_SHOutputBuffer = 6490147,
        HectonGIRelaySystem_SHLightningScratchBuffer = 6490148,
        HectonGIRelaySystem_SHTelemetryRingBuffer = 6490149,
        HectonLightingRuntime_DayNightRelay_DayNightEnvironmentLightingBuffer = 6490150,
        HectonLightingRuntime_DayNightRelay_DayNightTelemetryRingBuffer = 6490151,
        HectonLightingRuntime_DayNightRelay_DayNightTelemetryCursorBuffer = 6490152,
        HectonLightingRuntime_DayNightRelay_DayNightTuningBuffer = 6490153,
        HectonLightingRuntime_DayNightRelay_DayNightGradientProfilesBuffer = 6490154,
        HectonLightingRuntime_DayNightRelay_DayNightGradientProfileCountBuffer = 6490155,
        HectonLightingRuntime_DayNightRelay_DayNightMockSamplesBuffer = 6490156,
        InteriorGIProbeVolumeRuntime_ProbeFrontBuffer = 6490112,
        InteriorGIProbeVolumeRuntime_ProbeBackBuffer = 6490113,
        InteriorGIProbeVolumeRuntime_ProbeSourcesBuffer = 6490114,
        InteriorGIProbeVolumeRuntime_ProbeOcclusionBuffer = 6490115,
        InteriorGIProbeVolumeRuntime_ProbeTuningBuffer = 6490116,
        InteriorGIProbeVolumeRuntime_ProbeTelemetryRingBuffer = 6490117,
        InteriorGIProbeVolumeRuntime_ProbeTelemetryScratchBuffer = 6490118,
        InteriorGIProbeVolumeRuntime_ProbeMockPowerBuffer = 6490120,
        InteriorGIProbeVolumeRuntime_ProbeFaultBuffer = 6490121,
        InteriorGIProbeVolumeRuntime_ProbeCsvBytesBuffer = 6490122,
        InteriorGIProbeVolumeRuntime_ProbeAmbientProfileBuffer = 6490123,
        InteriorGIProbeVolumeRuntime_ProbeAmbientProfileCountBuffer = 6490124,
        ModularEquipmentEngine_FlashlightTelemetryRingBufferId = 71317,
        ModularEquipmentEngine_FlashlightTelemetryCursorBufferId = 71318,
        RollbackNetcodeContracts_StateRingBuffer = 70750,
        RollbackNetcodeContracts_RemoteInputRing = 70753,
        RollbackNetcodeContracts_TickCommands = 70754,
        RollbackNetcodeContracts_VisualStates = 70755,
        RollbackNetcodeContracts_TelemetryRing = 70756,
        RollbackNetcodeContracts_Tuning = 70757,
        RollbackNetcodeContracts_AudioSuppression = 70758,
        RollbackNetcodeContracts_CsvScratch = 70759,
        VRAMMonitor_VramTelemetryBufferId = 71617,
        CartographyGridJobs_DiscoveryWords = 71420,
        CartographyGridJobs_SectorTable = 71421,
        CartographyGridJobs_UploadPackedR8 = 71422,
        CartographyGridJobs_TelemetryRing = 71423,
        CartographyGridJobs_TelemetryCursor = 71424,
        CartographyGridJobs_Tuning = 71425,
        CartographyGridJobs_ScannerProfiles = 71426,
        CartographyGridJobs_CsvScratch = 71427,
        CartographyGridJobs_MockPings = 71428,
        CartographyGridJobs_Counters = 71429,
        CartographyGridJobs_ActiveSectorHashes = 71430,
        CartographyGridJobs_DebugVoxels = 71431,
        CartographyGridJobs_RleRuns = 71432,
        CartographyGridJobs_SurfaceMaskWords = 71433,
        CartographyGridJobs_RollbackSnapshotWords = 71434,
        CartographyGridJobs_PendingPings = 71435,
        CartographyGridJobs_PendingSignalCounts = 71436,
        CartographyGridJobs_State = 71437,
        CartographyGridJobs_LegacyExplorationWords = 71459,
        CartographyGridJobs_LegacyExploredBitIndices = 71460,
        CartographyGridJobs_LegacyExploredBitIndexCount = 71461,
        AsyncBuoyancyReadbackContracts_Requests = 71820,
        AsyncBuoyancyReadbackContracts_CompletedRequests = 71821,
        AsyncBuoyancyReadbackContracts_ResolvedHeights = 71822,
        AsyncBuoyancyReadbackContracts_ResultStates = 71823,
        AsyncBuoyancyReadbackContracts_Tuning = 71824,
        AsyncBuoyancyReadbackContracts_TelemetryRing = 71825,
        AsyncBuoyancyReadbackContracts_TelemetryCursor = 71826,
        AsyncBuoyancyReadbackContracts_MockRing = 71827,
        AsyncBuoyancyReadbackContracts_FallbackWaves = 71828,
        AsyncBuoyancyReadbackContracts_VehicleSamplingProfiles = 71829,
        AsyncBuoyancyReadbackContracts_Counter = 71831,
        CablePhysicsSolver132_CableNodes = 71320,
        CablePhysicsSolver132_CableConstraints = 71321,
        CablePhysicsSolver132_SplineVertices = 71322,
        CablePhysicsSolver132_SegmentTensions = 71323,
        CablePhysicsSolver132_PhysicsEvents = 71324,
        CablePhysicsSolver132_TelemetryRing = 71325,
        CablePhysicsSolver132_TelemetryHead = 71326,
        CablePhysicsSolver132_PinnedAups = 71327,
        CablePhysicsSolver132_PinnedMask = 71328,
        CablePhysicsSolver132_Tuning = 71329,
        CablePhysicsSolver132_CableMaterials = 71330,
        CablePhysicsSolver132_BootstrapState = 71331,
        CablePhysicsSolver132_Endpoints = 71332,
        AbyssalCavitationContracts_ShockwaveEvents = 71560,
        AbyssalCavitationContracts_ShockwaveCounters = 71561,
        AbyssalCavitationContracts_EntitySnapshots = 71562,
        AbyssalCavitationContracts_ForcePackets = 71563,
        AbyssalCavitationContracts_VisualSpheres = 71564,
        AbyssalCavitationContracts_TelemetryRing = 71565,
        AbyssalCavitationContracts_OrdnanceProfiles = 71566,
        AbyssalCavitationContracts_Tuning = 71568,
        AbyssalCavitationContracts_SdfDescriptor = 71569,
        AbyssalCavitationContracts_SdfVoxels = 71570,
        AbyssalCavitationContracts_ForceTransportPackets = 71571,
        HarpoonTensionSolver328_TetherStates = 72180,
        HarpoonTensionSolver328_TetherNodes = 72181,
        HarpoonTensionSolver328_TetherPreviousNodes = 72182,
        HarpoonTensionSolver328_TetherConstraints = 72183,
        HarpoonTensionSolver328_ForcePackets = 72184,
        HarpoonTensionSolver328_PhysicsEvents = 72185,
        HarpoonTensionSolver328_SplineVertices = 72186,
        HarpoonTensionSolver328_TelemetryRing = 72187,
        HarpoonTensionSolver328_TelemetryHead = 72188,
        HarpoonTensionSolver328_Tuning = 72189,
        HarpoonTensionSolver328_MaterialProfiles = 72190,
        HarpoonTensionSolver328_BootstrapState = 72191,
        HarpoonTensionSolver328_FaultFlags = 72192,
        HarpoonTensionSolver328_StressStates = 72193,
        Shinobu355KccSmokeEditorFacade_SmokeStatesBuffer = 71810,
        Shinobu355KccSmokeEditorFacade_SmokePositionHistoryBuffer = 71811,
        Shinobu355KccSmokeEditorFacade_SmokeRollbackRingBuffer = 71812,
        Shinobu355KccSmokeEditorFacade_SmokeResultBuffer = 71813,
        Shinobu355KccSmokeEditorFacade_SmokeFailureBuffer = 71814,
        Shinobu355KccSmokeEditorFacade_SmokeTelemetryBuffer = 71815,
        Shinobu355KccSmokeEditorFacade_SmokeDriftBuffer = 71816,
        Shinobu355KccSmokeEditorFacade_SmokeDesyncSignalBuffer = 71817,
        Shinobu355KccSmokeEditorFacade_SmokeProfilesBuffer = 71818,
        SubmarineAutopilotSdfNavigator_AutopilotStates = 71592,
        SubmarineAutopilotSdfNavigator_AutopilotAvoidance = 71593,
        SubmarineAutopilotSdfNavigator_AutopilotFeelerResults = 71594,
        SubmarineAutopilotSdfNavigator_AutopilotWaypoints = 71595,
        SubmarineAutopilotSdfNavigator_AutopilotRouteRanges = 71596,
        SubmarineAutopilotSdfNavigator_AutopilotTuning = 71597,
        SubmarineAutopilotSdfNavigator_AutopilotTelemetryRing = 71598,
        SubmarineAutopilotSdfNavigator_AutopilotTelemetryCursor = 71599,
        SubmarineAutopilotSdfNavigator_AutopilotMockSdf = 71600,
        SubmarineAutopilotSdfNavigator_AutopilotFlowSamples = 71601,
        SubmarineAutopilotSdfNavigator_AutopilotHandlingProfiles = 71603,
        VehicleComponentDamageContracts_TelemetryCursorBuffer = 71648,
        ShinobuMetabolismData_MetabolismDetailTelemetryRingBuffer = 73340,
        ShinobuMetabolismData_MetabolismSuitThermalProfilesBuffer = 73341,
        ShinobuMetabolismData_MetabolismSuitProfileIndicesBuffer = 73342,
        ShinobuMetabolismData_ChemicalPublishedGridReadbackBuffer = 71152,
        ShinobuMetabolismData_ChemicalOverlayGridReadbackBuffer = 71153,
        ShinobuMetabolismData_ChemicalTuningReadbackBuffer = 71161,
        ShinobuMetabolismData_ChemicalTelemetryReadbackBuffer = 71162,
        ShinobuMetabolismData_ChemicalTelemetryCursorReadbackBuffer = 71163,
        ShinobuPhysiologyData_BreathingGasFractionsBuffer = 70214,
        ShinobuPhysiologyData_GasPhysiologyTuningBuffer = 70215,
        ShinobuPhysiologyData_StatusEffectStatesBuffer = 70216,
        ShinobuPhysiologyData_GasPhysiologyStatesBuffer = 70239,
        ShinobuPhysiologyData_DecompressionTelemetryRingBuffer = 73343,
        ShinobuRadiationMutationData_MutationStateBuffer = 75320,
        ShinobuRadiationMutationData_MutationTuningBuffer = 75321,
        ShinobuRadiationMutationData_MutationTelemetryBuffer = 75322,
        ShinobuRadiationMutationData_MutationProfileBuffer = 75323,
        ShinobuRadiationMutationData_MutationMockDoseBuffer = 75325,
        ShinobuRespawnData_RespawnStateBuffer = 71604,
        ShinobuRespawnData_MedicalBayRespawnPointsBuffer = 71605,
        ShinobuRespawnData_RespawnFadeBuffer = 71606,
        ShinobuRespawnData_RespawnTelemetryRingBuffer = 71607,
        ShinobuRespawnData_RespawnTelemetryCursorBuffer = 71608,
        ShinobuRespawnData_RespawnTuningBuffer = 71609,
        ShinobuRespawnData_RespawnPenaltyRulesBuffer = 71610,
        ShinobuRespawnData_RespawnPenaltyRuleCountBuffer = 71611,
        ShinobuRespawnData_RespawnRequestBuffer = 71613,
        ShinobuSensoryImpairmentData_SensoryImpairmentBuffer = 75220,
        ShinobuSensoryImpairmentData_SensoryImpairmentTuningBuffer = 75221,
        ShinobuSensoryImpairmentData_SensoryImpairmentTelemetryBuffer = 75222,
        ShinobuSensoryImpairmentData_SensoryImpairmentProfilesBuffer = 75223,
        ShinobuSensoryImpairmentData_SensoryInputDriftDebugBuffer = 75225,
        OceanKinematicsContracts_Requests = 72940,
        OceanKinematicsContracts_Results = 72941,
        OceanKinematicsContracts_GerstnerWaves = 72942,
        OceanKinematicsContracts_Tuning = 72943,
        OceanKinematicsContracts_MacroState = 72944,
        OceanKinematicsContracts_TelemetryRing = 72945,
        OceanKinematicsContracts_TelemetryCursor = 72946,
        OceanKinematicsContracts_GpuCachedResults = 72947,
        OceanKinematicsContracts_CsvScratch = 72948,
        OceanKinematicsContracts_QueueCounters = 72949,
        OceanKinematicsContracts_RollbackFence = 72950,
        BatteryChargerLogisticsContracts_Links = 72300,
        BatteryChargerLogisticsContracts_LinkAup = 72301,
        BatteryChargerLogisticsContracts_ExpectedPowerNodeHashes = 72302,
        BatteryChargerLogisticsContracts_VisualStates = 72303,
        BatteryChargerLogisticsContracts_Tuning = 72304,
        BatteryChargerLogisticsContracts_TelemetryRing = 72305,
        BatteryChargerLogisticsContracts_TelemetryCursor = 72306,
        BatteryChargerLogisticsContracts_AtomicCounters = 72307,
        BatteryChargerLogisticsContracts_Profiles = 72308,
        BatteryChargerLogisticsContracts_CsvScratch = 72309,
        BatteryChargerLogisticsContracts_MockInventorySlots = 72310,
        PowerGridJacobiContracts_Nodes = 70850,
        PowerGridJacobiContracts_Edges = 70851,
        PowerGridJacobiContracts_NodeAup = 70852,
        PowerGridJacobiContracts_CsrOffsets = 70853,
        PowerGridJacobiContracts_CsrDestinations = 70854,
        PowerGridJacobiContracts_CsrConductance = 70855,
        PowerGridJacobiContracts_CsrFlow = 70856,
        PowerGridJacobiContracts_PotentialFront = 70857,
        PowerGridJacobiContracts_PotentialBack = 70858,
        PowerGridJacobiContracts_DemandRate = 70859,
        PowerGridJacobiContracts_BatteryRemainderMilli = 70860,
        PowerGridJacobiContracts_TelemetryRing = 70861,
        PowerGridJacobiContracts_TelemetryCursor = 70862,
        PowerGridJacobiContracts_Profiles = 70863,
        PowerGridJacobiContracts_CsvScratch = 70864,
        PowerGridSolarContracts_PanelStates = 73410,
        PowerGridSolarContracts_PanelOutputs = 73411,
        PowerGridSolarContracts_PanelPowerNodeIndices = 73412,
        PowerGridSolarContracts_NodeSolarInputMilliWatts = 73413,
        PowerGridSolarContracts_Conditions = 73414,
        PowerGridSolarContracts_TelemetryRing = 73415,
        PowerGridSolarContracts_TelemetryCursor = 73416,
        PowerGridSolarContracts_Profiles = 73417,
        PowerGridSolarContracts_CsvScratch = 73418,
        SubmarineOsThermalGridRuntime_NodesAId = 731060,
        SubmarineOsThermalGridRuntime_NodesBId = 731061,
        SubmarineOsThermalGridRuntime_EdgesId = 731062,
        SubmarineOsThermalGridRuntime_InjectionsId = 731063,
        SubmarineOsThermalGridRuntime_ExternalHeatId = 731064,
        SubmarineOsThermalGridRuntime_AnchorsId = 731065,
        SubmarineOsThermalGridRuntime_TuningId = 731066,
        SubmarineOsThermalGridRuntime_TelemetryId = 731067,
        SubmarineOsThermalGridRuntime_CountersId = 731068,
        SubmarineOsThermalGridRuntime_SpecsId = 731069,
        SubmarineOsThermalGridRuntime_CsvBytesId = 731070,
        SubmarineOsThermalGridRuntime_VisualStateId = 731071,
        SubmarineOsThermalGridRuntime_PendingNodesId = 731072,
        SubmarineOsThermalGridRuntime_PendingEdgesId = 731073,
        SubmarineOsThermalGridRuntime_PendingInjectionsId = 731074,
        SubmarineOsThermalGridRuntime_PendingAnchorsId = 731075,
        SubmarineOsThermalGridRuntime_PendingVisualStateId = 731076,
        SubmarineOsThermalGridRuntime_PendingCountersId = 731077,
        SubmarineOsThermalGridRuntime_ConvergenceStateId = 731078,
        SubmarineOsThermalGridRuntime_ResidualSamplesId = 731079,
        WfcOutpostGridRegistry_GridSlotBase = 731620,
        WfcOutpostPowerBootRuntime_MigratedID_731640 = 731640,
        WfcOutpostPowerBootRuntime_MigratedID_731641 = 731641,
        WfcOutpostPowerBootRuntime_MigratedID_731642 = 731642,
        WfcOutpostPowerBootRuntime_MigratedID_731643 = 731643,
        WfcOutpostPowerBootRuntime_MigratedID_731645 = 731645,
        WfcOutpostPowerBootRuntime_MigratedID_731644 = 731644,
        OrbitalRelativityDirector_TelemetryRingBufferId = 1330790977,
        PowerGridJacobiStressFuzzer_Nodes = 35610,
        PowerGridJacobiStressFuzzer_NodeAup = 35611,
        PowerGridJacobiStressFuzzer_CsrOffsets = 35612,
        PowerGridJacobiStressFuzzer_CsrDestinations = 35613,
        PowerGridJacobiStressFuzzer_CsrConductance = 35614,
        PowerGridJacobiStressFuzzer_CsrFlow = 35615,
        PowerGridJacobiStressFuzzer_PotentialFront = 35616,
        PowerGridJacobiStressFuzzer_PotentialBack = 35617,
        PowerGridJacobiStressFuzzer_DemandRate = 35618,
        PowerGridJacobiStressFuzzer_BatteryRemainder = 35619,
        PowerGridJacobiStressFuzzer_Result = 35620,
        PowerGridJacobiStressFuzzer_StressTelemetry = 35621,
        PowerGridJacobiStressFuzzer_GraphCounts = 35622,
        PowerGridJacobiStressFuzzer_CsvScratch = 35623,
        PowerGridJacobiStressFuzzer_VoltageHistory = 35624,
        PowerGridJacobiStressFuzzer_RollbackFront = 35625,
        PowerGridJacobiStressFuzzer_RollbackBack = 35626,
        PowerGridJacobiStressFuzzer_FuzzState = 35627,
        PowerGridJacobiStressFuzzer_FuzzTelemetry = 35628,
        PowerGridJacobiStressFuzzer_TopologyProfile = 35629,
        Shinobu38QaWatchdogRuntime_StateBufferId = 70580,
        Shinobu38QaWatchdogRuntime_SnapshotBufferId = 70581,
        Shinobu38QaWatchdogRuntime_WaypointsBufferId = 70582,
        Shinobu38QaWatchdogRuntime_RebaseSignalsBufferId = 70583,
        Shinobu38QaWatchdogRuntime_TuningBufferId = 70584,
        Shinobu38QaWatchdogRuntime_MockVaultBufferId = 70585,
        Shinobu38QaWatchdogRuntime_TelemetryRingBufferId = 70586,
        Shinobu38QaWatchdogRuntime_CsvScratchBufferId = 70587,
        Shinobu38QaWatchdogRuntime_WaypointScratchBufferId = 70588,
        Shinobu38QaWatchdogRuntime_DumpScratchBufferId = 70589,
        Shinobu38QaWatchdogRuntime_FileWriteCommandsBufferId = 70590,
        Shinobu38QaWatchdogRuntime_FileWritePayloadBufferId = 70591,
        Shinobu38QaWatchdogRuntime_FileWriterStateBufferId = 70592,
        Shinobu38QaWatchdogRuntime_FileWriterCursorBufferId = 70593,
        Shinobu38QaWatchdogRuntime_WaypointIngestStateBufferId = 70594,
        QA_WatchdogBot_MetricsBufferId = 74240,
        QA_WatchdogBot_BlackBoxBufferId = 74241,
        OceanSinglePassContracts_TelemetryRingBuffer = 71897,
        OceanSinglePassContracts_TelemetryCursorBuffer = 71898,
        OceanSinglePassContracts_AestheticProfilesBuffer = 71899,
        OceanSinglePassContracts_CsvScratchBuffer = 71900,
        OceanSinglePassContracts_MockRenderStateBuffer = 71901,
        OceanSinglePassContracts_SelfAuditBuffer = 71902,
        ShorelineFoamGraftContracts_ParamsBuffer = 71940,
        ShorelineFoamGraftContracts_RuntimeStateBuffer = 71941,
        ShorelineFoamGraftContracts_TelemetryRingBuffer = 71942,
        ShorelineFoamGraftContracts_TelemetryCursorBuffer = 71943,
        ShorelineFoamGraftContracts_ProfileBuffer = 71944,
        ShorelineFoamGraftContracts_CsvScratchBuffer = 71945,
        ShorelineFoamGraftContracts_SelfAuditBuffer = 71946,
        WalIntegrityFuzzerCore_SHINOBU357_Shinobu357PayloadBufferId = 73470,
        WalIntegrityFuzzerCore_SHINOBU357_Shinobu357CorruptWalBufferId = 73471,
        WalIntegrityFuzzerCore_SHINOBU357_Shinobu357StateBufferId = 73472,
        WalIntegrityFuzzerCore_SHINOBU357_Shinobu357TelemetryRingBufferId = 73473,
        WalIntegrityFuzzerCore_SHINOBU357_Shinobu357TelemetryCursorBufferId = 73474,
        WalIntegrityFuzzerCore_SHINOBU357_Shinobu357HashScratchBufferId = 73475,
        WalIntegrityFuzzerCore_SHINOBU357_Shinobu357FileHandleStatusBufferId = 73476,
        ScavengingLootOracleRuntime_LootEntriesBufferId = 70930,
        ScavengingLootOracleRuntime_BiomeModifiersBufferId = 70933,
        ScavengingLootOracleRuntime_DistributionAuditBufferId = 70935,
        ScavengingLootOracleRuntime_CsvScratchBufferId = 70936,
        SpatialAudioManager_SpatialAudioAcousticVoxelSdfTexture3DBufferId = 72447,
        SubmarineAtmosphereSystem_HighPressurePendingEvents = 72200,
        SubmarineAtmosphereSystem_HighPressureNextFrameEvents = 72201,
        SubmarineAtmosphereSystem_FatalPressurePendingEvents = 72202,
        SubmarineAtmosphereSystem_FatalPressureNextFrameEvents = 72203,
        SubmarineAtmosphereSystem_RoomVolumes = 72204,
        SubmarineAtmosphereSystem_FloodVolumes = 72205,
        SubmarineAtmosphereSystem_O2Front = 72206,
        SubmarineAtmosphereSystem_O2Back = 72207,
        SubmarineAtmosphereSystem_Co2Front = 72208,
        SubmarineAtmosphereSystem_Co2Back = 72209,
        SubmarineAtmosphereSystem_InertFront = 72210,
        SubmarineAtmosphereSystem_InertBack = 72211,
        SubmarineAtmosphereSystem_PressureFront = 72212,
        SubmarineAtmosphereSystem_PressureBack = 72213,
        SubmarineAtmosphereSystem_O2PartialPressureFront = 72214,
        SubmarineAtmosphereSystem_O2PartialPressureBack = 72215,
        SubmarineAtmosphereSystem_Co2PartialPressureFront = 72216,
        SubmarineAtmosphereSystem_Co2PartialPressureBack = 72217,
        SubmarineAtmosphereSystem_N2PartialPressureFront = 72218,
        SubmarineAtmosphereSystem_N2PartialPressureBack = 72219,
        SubmarineAtmosphereSystem_GasVolumeFront = 72220,
        SubmarineAtmosphereSystem_GasVolumeBack = 72221,
        SubmarineAtmosphereSystem_O2ConsumptionRates = 72222,
        SubmarineAtmosphereSystem_Co2GenerationRates = 72223,
        SubmarineAtmosphereSystem_RoomPlayerCounts = 72224,
        SubmarineAtmosphereSystem_TemperatureFront = 72225,
        SubmarineAtmosphereSystem_TemperatureBack = 72226,
        SubmarineAtmosphereSystem_SteamFront = 72227,
        SubmarineAtmosphereSystem_SteamBack = 72228,
        SubmarineAtmosphereSystem_HydrogenPocketFront = 72229,
        SubmarineAtmosphereSystem_OxygenPocketFront = 72230,
        SubmarineAtmosphereSystem_RoomHeatWatts = 72231,
        SubmarineAtmosphereSystem_RoomStatusMaskFront = 72232,
        SubmarineAtmosphereSystem_RoomStatusMaskBack = 72233,
        SubmarineAtmosphereSystem_DoorPairs = 72234,
        SubmarineAtmosphereSystem_DoorSealed = 72235,
        SubmarineAtmosphereSystem_DoorSealedPrevious = 72236,
        SubmarineAtmosphereSystem_TelemetryRing = 72237,
        SubmarineAtmosphereSystem_TelemetryCursor = 72238,
        SubmarineStructuralGrid_CellIntegrityFront = 1326000,
        SubmarineStructuralGrid_CellIntegrityBack = 1326001,
        SubmarineStructuralGrid_CellFatigue = 1326002,
        SubmarineStructuralGrid_CellCompartmentIndices = 1326003,
        SubmarineStructuralGrid_HullBreachMaskFront = 1326004,
        SubmarineStructuralGrid_HullBreachMaskBack = 1326005,
        SubmarineStructuralGrid_CompartmentBreachAreasFront = 1326006,
        SubmarineStructuralGrid_CompartmentBreachAreasBack = 1326007,
        SubmarineStructuralGrid_QueuedImpacts = 1326008,
        SubmarineStructuralGrid_ScheduledImpacts = 1326009,
        SubmarineStructuralGrid_CompartmentCentroids = 1326010,
        SubmarineStructuralGrid_FatigueCompartmentFlags = 1326011,
        SubmarineStructuralGrid_FatigueIntegrityLossPerCycle = 1326012,
        SubmarineStructuralGrid_FatiguePeakResult = 1326013,
        SubmarineStructuralGrid_BreachSeveritySumResult = 1326014,
        AbyssalThermodynamicsSolver_SolverConvergenceStateId = 70052,
        AbyssalThermodynamicsSolver_SolverResidualSamplesId = 70053,
        AbyssalThermodynamicsSolver_SolverDumpLatchId = 70054,
        ReactorThermalGridContracts_States = 73642,
        ReactorThermalGridContracts_Tuning = 73643,
        ReactorThermalGridContracts_PowerLedger = 73644,
        ReactorThermalGridContracts_TelemetryRing = 73645,
        ReactorThermalGridContracts_TelemetryCursor = 73646,
        ReactorThermalGridContracts_Visuals = 73647,
        ReactorThermalGridContracts_DumpLatch = 73648,
        ReactorThermalGridContracts_Profiles = 73649,
        ReactorThermalGridContracts_ProfileCount = 73650,
        LaserCutterDodContracts_SpecBuffer = 71333,
        LaserCutterDodContracts_CsvScratchBuffer = 71334,
        LaserCutterDodContracts_CountersBuffer = 71335,
        LaserCutterDodContracts_RequestMetaBuffer = 71336,
        UpgradeMatrixCompiler_UpgradeMasksBuffer = 71380,
        UpgradeMatrixCompiler_UpgradeBaseStatsBuffer = 71381,
        UpgradeMatrixCompiler_UpgradeCompiledStatsBuffer = 71382,
        UpgradeMatrixCompiler_UpgradeLutBuffer = 71383,
        UpgradeMatrixCompiler_UpgradeRulesBuffer = 71384,
        UpgradeMatrixCompiler_UpgradeTelemetryRingBuffer = 71385,
        UpgradeMatrixCompiler_UpgradeTelemetryCursorBuffer = 71386,
        UpgradeMatrixCompiler_UpgradeInventorySlotsBuffer = 71387,
        UpgradeMatrixCompiler_UpgradeItemMapBuffer = 71388,
        UpgradeMatrixCompiler_UpgradeVisualStateBuffer = 71389,
        UpgradeMatrixCompiler_UpgradeToolModuleRulesBuffer = 71410,
        UpgradeMatrixCompiler_UpgradeToolProfilesBuffer = 71412,
        BabelSubtitleSyncRuntime_SubtitleCueStateBufferId = 15070550,
        BabelSubtitleSyncRuntime_SubtitleCueTelemetryBufferId = 15070551,
        BabelSubtitleSyncRuntime_UIOptimizationTelemetryBufferId = 15070552,
        DiegeticGlitchSurgeonRuntime_CsvScratchBufferId = 70914,
        DiegeticGlitchSurgeonRuntime_TerminalOsStateBridgeBufferId = 71360,
        PDAEncyclopediaStreamer_UnlockMaskBufferId = 70560,
        PDAEncyclopediaStreamer_RuntimeStateBufferId = 70561,
        PDAEncyclopediaStreamer_MetadataBufferId = 70562,
        PDAEncyclopediaStreamer_TelemetryBufferId = 70563,
        PDAEncyclopediaStreamer_TelemetryCursorBufferId = 70564,
        PDAEncyclopediaStreamer_MockUtf8BufferId = 70565,
        PDAEncyclopediaStreamer_MockIndexBufferId = 70566,
        PDAEncyclopediaStreamer_TypewriterStateBufferId = 70569,
        PDAEncyclopediaStreamer_H8lrMirrorBufferId = 70570,
        TerminalOsRuntime_ScreenCommandsBufferId = 71361,
        TerminalOsRuntime_GlyphUvsBufferId = 71362,
        TerminalOsRuntime_TerminalPositionsBufferId = 71363,
        TerminalOsRuntime_TerminalForwardsBufferId = 71364,
        TerminalOsRuntime_DirtyIndicesBufferId = 71365,
        TerminalOsRuntime_TelemetryRingBufferId = 71366,
        TerminalOsRuntime_MockPowerBufferId = 71367,
        TerminalOsRuntime_MockDamageBufferId = 71368,
        TerminalOsRuntime_MockPowerStatusBufferId = 71369,
        TerminalOsRuntime_ButtonAabbBufferId = 71370,
        TerminalOsRuntime_PanelInstancesBufferId = 71371,
        TerminalOsRuntime_TerminalClickScratchBufferId = 71372,
        TerminalOsRuntime_TerminalPlanesBufferId = 71373,
        TerminalOsRuntime_GazeRayBufferId = 71374,
        TerminalOsRuntime_TerminalInteractionsBufferId = 71375,
        TopographicalSonarSynthesizer_Points = 70840,
        TopographicalSonarSynthesizer_HitMask = 70841,
        TopographicalSonarSynthesizer_Counters = 70842,
        TopographicalSonarSynthesizer_TelemetryRing = 70845,
        TopographicalSonarSynthesizer_TelemetryCursor = 70846,
        TopographicalSonarSynthesizer_MaterialColorLut = 70847,
        TopographicalSonarSynthesizer_IndirectArgs = 70849,
        BiolumPulseSyncRuntime_BiolumPulseStateBufferId = 70311,
        BiolumPulseSyncRuntime_BiolumBlackBoxDumpScratchBufferId = 70312,
        CameraJuiceSystem_CameraJuiceBurst_CameraJuiceStateBufferId = 73373,
        CameraJuiceSystem_CameraJuiceBurst_CameraJuiceImpulseBufferId = 73374,
        CameraJuiceSystem_CameraJuiceBurst_CameraJuiceProjectionBufferId = 73375,
        CameraJuiceSystem_CameraJuiceBurst_CameraJuiceTuningBufferId = 73376,
        CameraJuiceSystem_CameraJuiceBurst_CameraJuiceProfilesBufferId = 73377,
        CameraJuiceSystem_CameraJuiceBurst_CameraJuiceMockSignalsBufferId = 73378,
        DiegeticVisorLensRuntime_StateBufferId = 71020,
        DiegeticVisorLensRuntime_TuningBufferId = 71021,
        DiegeticVisorLensRuntime_PhysiologyBufferId = 71022,
        DiegeticVisorLensRuntime_EnvironmentBufferId = 71023,
        DiegeticVisorLensRuntime_GpuGlobalsBufferId = 71024,
        DiegeticVisorLensRuntime_TelemetryRingBufferId = 71025,
        DiegeticVisorLensRuntime_TelemetryCursorBufferId = 71026,
        DiegeticVisorLensRuntime_CsvByteBufferId = 71027,
        DiegeticVisorLensRuntime_BinaryProbeByteBufferId = 71028,
        DiegeticVisorLensRuntime_NanFlagBufferId = 71029,
        DynamicDecalVaultRuntime_Instances = 73190,
        DynamicDecalVaultRuntime_UploadScratch = 73191,
        DynamicDecalVaultRuntime_RuntimeState = 73192,
        DynamicDecalVaultRuntime_TelemetryRing = 73193,
        DynamicDecalVaultRuntime_Tuning = 73194,
        DynamicDecalVaultRuntime_MaterialProfiles = 73195,
        DynamicDecalVaultRuntime_CsvScratch = 73196,
        DynamicDecalVaultRuntime_RequestRing = 73197,
        DynamicDecalVaultRuntime_RequestState = 73198,
        DynamicDecalVaultRuntime_SignalIngestKeyRing = 73199,
        HectonVisorARStencilRendererFeature_HudParamsBufferId = 73180,
        HectonVisorARStencilRendererFeature_TargetSourceBufferId = 73181,
        HectonVisorARStencilRendererFeature_ProjectedTargetBufferId = 73182,
        HectonVisorARStencilRendererFeature_DigitParamsBufferId = 73183,
        HectonVisorARStencilRendererFeature_TelemetryRingBufferId = 73184,
        HectonVisorARStencilRendererFeature_ProfileBufferId = 73185,
        HectonVisorARStencilRendererFeature_CsvScratchBufferId = 73186,
        AbyssalThermalManager_ThermalMapReadCelsiusBufferId = 70056,
        AbyssalThermalManager_ThermalMapWriteCelsiusBufferId = 70057,
        AbyssalThermalManager_ThermalMapSourceCelsiusBufferId = 70058,
        AbyssalThermalManager_ThermalMapInsulationBufferId = 70059,
        ChemicalInfluenceGrid_GridFrontBufferId = 71150,
        ChemicalInfluenceGrid_GridBackBufferId = 71151,
        ChemicalInfluenceGrid_BreadcrumbBufferId = 71154,
        ChemicalInfluenceGrid_PendingEmitterBufferId = 71155,
        ChemicalInfluenceGrid_PendingEmitterCountBufferId = 71156,
        ChemicalInfluenceGrid_ActiveEmitterBufferId = 71157,
        ChemicalInfluenceGrid_ActiveEmitterCountBufferId = 71158,
        ChemicalInfluenceGrid_MockEmitterBufferId = 71159,
        ChemicalInfluenceGrid_MockEmitterCountBufferId = 71160,
        ChemicalInfluenceGrid_AtomicCounterBufferId = 71164,
        ChemicalInfluenceGrid_DefoliantZoneBufferId = 71165,
        ChemicalInfluenceGrid_CsvScratchBufferId = 71166,
        ChemicalInfluenceGrid_EmitterProfileTableBufferId = 71167,
        ChemicalInfluenceGrid_EmitterProfileCountBufferId = 71168,
        DestructibleOrganicManager_DearLieSurfaceClaimsBufferId = 72980,
        DestructibleOrganicManager_DearLieUnderwaterClaimsBufferId = 72981,
        DestructibleOrganicManager_DearLieDamageEventsBufferId = 72982,
        DestructibleOrganicManager_DearLieResultsBufferId = 72983,
        DestructibleOrganicManager_DearLieCountersBufferId = 72984,
        DestructibleOrganicManager_DearLieRegenRecordsBufferId = 72985,
        DestructibleOrganicManager_DearLieTelemetryRingBufferId = 72986,
        DestructibleOrganicManager_DearLieSurfaceBucketHeadsBufferId = 72987,
        DestructibleOrganicManager_DearLieSurfaceBucketNextBufferId = 72988,
        DestructibleOrganicManager_DearLieUnderwaterBucketHeadsBufferId = 72989,
        DestructibleOrganicManager_DearLieUnderwaterBucketNextBufferId = 72990,
        DestructibleOrganicManager_OrganicSurfaceInstanceUidsBufferId = 72991,
        DestructibleOrganicManager_OrganicUnderwaterInstanceUidsBufferId = 72992,
        DestructibleOrganicManager_OrganicSurfaceMaterialClassesBufferId = 72993,
        DestructibleOrganicManager_OrganicUnderwaterMaterialClassesBufferId = 72994,
        DestructibleOrganicManager_OrganicSurfaceHealthBufferId = 72995,
        DestructibleOrganicManager_OrganicUnderwaterHealthBufferId = 72996,
        DestructibleOrganicManager_OrganicHealthByUidBufferId = 72997,
        DestructibleOrganicManager_OrganicDestroyedByUidBufferId = 72998,
        DestructibleOrganicManager_OrganicPendingWiltEndTimeByUidBufferId = 72999,
        DestructibleOrganicManager_OrganicDamageVisualProgressByUidBufferId = 73000,
        DestructibleOrganicManager_OrganicDecompositionStartTimeByUidBufferId = 73001,
        DestructibleOrganicManager_OrganicRegrowthProgressByUidBufferId = 73002,
        DestructibleOrganicManager_OrganicRegrowthPositionByUidBufferId = 73003,
        DestructibleOrganicManager_OrganicMaturationScaleByUidBufferId = 73004,
        DestructibleOrganicManager_OrganicMaturationYieldByUidBufferId = 73005,
        DestructibleOrganicManager_OrganicNextSporeAcousticTimeByUidBufferId = 73006,
        DestructibleOrganicManager_OrganicBaseScaleByUidBufferId = 73007,
        DestructibleOrganicManager_OrganicRuntimeFlagsByUidBufferId = 73008,
        DestructibleOrganicManager_OrganicLastTouchTimeByUidBufferId = 73009,
        DestructibleOrganicManager_OrganicOvergrownByUidBufferId = 73010,
        DestructibleOrganicManager_OrganicRootMoundAppliedByUidBufferId = 73011,
        DestructibleOrganicManager_OrganicDestroyedFloraScratchBufferId = 73012,
        DestructibleOrganicManager_OrganicFloraStateOverrideScratchBufferId = 73013,
        DestructibleOrganicManager_OrganicPersistedHealth01ByUidBufferId = 73014,
        DestructibleOrganicManager_OrganicPersistedHeightScale01ByUidBufferId = 73015,
        DestructibleOrganicManager_OrganicPendingYieldEventsBufferId = 73016,
        DestructibleOrganicManager_OrganicYieldJobInputBufferId = 73017,
        DestructibleOrganicManager_OrganicTemplateDescriptorsBufferId = 73018,
        DestructibleOrganicManager_OrganicLootEntriesBufferId = 73019,
        DestructibleOrganicManager_OrganicYieldMaterialLutBufferId = 73020,
        DestructibleOrganicManager_OrganicDropDebugScratchBufferId = 73021,
        DestructibleOrganicManager_OrganicDropOutputBufferId = 73022,
        DestructibleOrganicManager_OrganicDropBudgetBufferId = 73023,
        FloraAmbientSwayRuntime_FloraAmbientSwayParamsBufferId = 72900,
        FloraAmbientSwayRuntime_FloraAmbientSwayFlowStateBufferId = 72901,
        FloraAmbientSwayRuntime_FloraAmbientSwayTelemetryRingBufferId = 72902,
        FloraAmbientSwayRuntime_FloraAmbientSwayTelemetryCursorBufferId = 72903,
        FloraAmbientSwayRuntime_FloraAmbientSwayTuningBufferId = 72904,
        FloraAmbientSwayRuntime_FloraAmbientSwayBiomeProfilesBufferId = 72905,
        FloraAmbientSwayRuntime_FloraAmbientSwayCsvScratchBufferId = 72906,
        FloraInteractionManager_FloraSwayDisplacementFieldBufferId = 71650,
        FloraInteractionManager_FloraSwayFieldMetaBufferId = 71651,
        FloraInteractionManager_FloraSwayFieldBlackBoxBufferId = 71652,
        FloraInteractionManager_FloraStiffnessRulesBufferId = 71653,
        FloraInteractionManager_FloraStiffnessCsvScratchBufferId = 71654,
        FloraInteractionManager_FloraOceanFlowSamplePositionsBufferId = 71655,
        FloraInteractionManager_FloraOceanFlowSampleResultsBufferId = 71656,
        FloraInteractionManager_FloraParasiteNodesBufferId = 71657,
        FloraInteractionManager_FloraCascadeReactiveTemplateMaskBufferId = 71658,
        FloraInteractionManager_FloraDefensiveSporeTemplateMaskBufferId = 71659,
        GlobalWorldSampler_ProbeHeightSamplesBuffer = 5440513,
        GlobalWorldSampler_ProbeHeightMaterialsBuffer = 5440514,
        GlobalWorldSampler_ProbeEncodedSdfBuffer = 5440515,
        GlobalWorldSampler_ProbeSdfMaterialsBuffer = 5440516,
        GlobalWorldSampler_ProbeSectorMaskBuffer = 5440517,
        GlobalWorldSampler_ProbeCountersBuffer = 5440518,
        GlobalWorldSampler_ProbeTelemetryBuffer = 5440519,
        GlobalWorldSampler_ProbeBiomeAtlasBuffer = 5440520,
        GlobalWorldSampler_ProbeErosionMaskBuffer = 5440521,
        GlobalWorldSampler_ProbeSdfOverrideBuffer = 5440522,
        GlobalWorldSampler_ProbeActiveSectorsBuffer = 5440523,
        GlobalWorldSampler_ProbeCounterBlocksBuffer = 5440524,
        GlobalWorldSampler_ProbeCsvBuffer = 5440525,
        HectonIndirectVegetationRenderer_FloraAgeBufferId = 74600,
        HectonIndirectVegetationRenderer_CpuCullingMatricesBufferId = 74601,
        HectonIndirectVegetationRenderer_CpuCullingDataBufferId = 74602,
        HectonIndirectVegetationRenderer_NativeUploadMatrixDirtyPagesAId = 74603,
        HectonIndirectVegetationRenderer_NativeUploadMatrixDirtyPagesBId = 74604,
        HectonIndirectVegetationRenderer_NativeUploadDataDirtyPagesAId = 74605,
        HectonIndirectVegetationRenderer_NativeUploadDataDirtyPagesBId = 74606,
        HectonMapMagicVegetationBridge_SurfaceAggregateFrontMatrixDirtyPagesId = 74607,
        HectonMapMagicVegetationBridge_SurfaceAggregateFrontMetadataDirtyPagesId = 74608,
        HectonMapMagicVegetationBridge_SurfaceAggregateBackMatrixDirtyPagesId = 74609,
        HectonMapMagicVegetationBridge_SurfaceAggregateBackMetadataDirtyPagesId = 74610,
        HectonMapMagicVegetationBridge_UnderwaterAggregateFrontMatrixDirtyPagesId = 74611,
        HectonMapMagicVegetationBridge_UnderwaterAggregateFrontMetadataDirtyPagesId = 74612,
        HectonMapMagicVegetationBridge_UnderwaterAggregateBackMatrixDirtyPagesId = 74613,
        HectonMapMagicVegetationBridge_UnderwaterAggregateBackMetadataDirtyPagesId = 74614,
        PersistentWorldRegistry_WorldRegistryResourceTombstoneCountBuffer = 74468,
        PersistentWorldRegistry_WorldRegistryResourceMetamorphosedKeysBuffer = 74469,
        PersistentWorldRegistry_WorldRegistryResourceMetamorphosedValuesBuffer = 74470,
        PersistentWorldRegistry_WorldRegistryResourceMetamorphosedStatesBuffer = 74471,
        PersistentWorldRegistry_WorldRegistryResourceMetamorphosedCountBuffer = 74472,
        PersistentWorldRegistry_WorldRegistryDeltaChunkIndexKeysBuffer = 74473,
        PersistentWorldRegistry_WorldRegistryDeltaChunkIndexValuesBuffer = 74474,
        PersistentWorldRegistry_WorldRegistryDeltaChunkIndexStatesBuffer = 74475,
        PersistentWorldRegistry_WorldRegistryDeltaChunkIndexCountBuffer = 74476,
        PersistentWorldRegistry_WorldRegistryDeltaChunkIdsBuffer = 74477,
        PersistentWorldRegistry_WorldRegistryDeltaChunkIdsCountBuffer = 74478,
        PersistentWorldRegistry_WorldRegistryDeltaItemIndexKeysBuffer = 74479,
        PersistentWorldRegistry_WorldRegistrySpawnVelocityCountBuffer = 74512,
        PersistentWorldRegistry_WorldRegistryDehydrateQueueValuesBuffer = 74513,
        PersistentWorldRegistry_WorldRegistryDehydrateQueueStateBuffer = 74514,
        PersistentWorldRegistry_WorldRegistryPendingHydrationRecordsBuffer = 74515,
        PersistentWorldRegistry_WorldRegistryPendingHydrationRecordsCountBuffer = 74516,
        PersistentWorldRegistry_WorldRegistryTelemetryRingBuffer = 74517,
        PersistentWorldRegistry_WorldRegistryTelemetryCursorBuffer = 74518,
        ProceduralCoralContracts_Rules = 71390,
        ProceduralCoralContracts_InstructionScratchA = 71391,
        ProceduralCoralContracts_InstructionScratchB = 71392,
        ProceduralCoralContracts_Branches = 71393,
        ProceduralCoralContracts_TurtleStack = 71394,
        ProceduralCoralContracts_SpatialCells = 71395,
        ProceduralCoralContracts_RenderMatrices = 71396,
        ProceduralCoralContracts_IndirectArgs = 71397,
        ProceduralCoralContracts_SectorTriggers = 71398,
        ProceduralCoralContracts_CollisionProxies = 71399,
        ProceduralCoralContracts_SyncPulses = 71400,
        ProceduralCoralContracts_TelemetryRing = 71401,
        ProceduralCoralContracts_TelemetryCursor = 71402,
        ProceduralCoralContracts_Tuning = 71403,
        ProceduralCoralContracts_CsvScratch = 71404,
        ProceduralCoralContracts_Counters = 71405,
        ProceduralCoralContracts_DebugSegments = 71406,
        ProceduralCoralContracts_GpuSway = 71407,
        ProceduralCoralContracts_SelfAudit = 71408,
        ProceduralCoralContracts_HzbTiles = 71409,
        ProceduralWreckageContracts_DebrisNodes = 70843,
        ProceduralWreckageContracts_RenderMatrices = 70844,
        ProceduralWreckageContracts_CollisionProxies = 70848,
        ProceduralGeologyContracts_ResourceNodes = 71530,
        ProceduralGeologyContracts_OrePositions = 71531,
        ProceduralGeologyContracts_OreTypes = 71532,
        ProceduralGeologyContracts_DepletionMasks = 71533,
        ProceduralGeologyContracts_ResourceMatrices = 71534,
        ProceduralGeologyContracts_BiomeHeatmap = 71535,
        ProceduralGeologyContracts_SpawnCounts = 71536,
        ProceduralGeologyContracts_TelemetryRing = 71537,
        ProceduralGeologyContracts_MockTerrainSdf = 71538,
        ProceduralGeologyContracts_DistributionRules = 71539,
        ProceduralGeologyContracts_Tuning = 71540,
        ProceduralGeologyContracts_CsvScratch = 71541,
        ProceduralGeologyContracts_SelfAudit = 71542,
        ProceduralGeologyContracts_CandidateSlots = 71543,
        ProceduralGeologyContracts_DepletionCacheKeys = 71544,
        ProceduralGeologyContracts_DepletionCacheMasks = 71545,
        ProceduralGeologyContracts_DepletionCacheCount = 71546,
        ProceduralGeologyContracts_SectorHashGrid = 71547,
        ProceduralGeologyContracts_IndirectArgs = 71548,
        ProceduralGeologyContracts_HzbTiles = 71549,
        ProceduralGeologyContracts_HzbMeta = 71550,
        ProceduralGeologyContracts_PlayerEcosystemTelemetry = 141905,
        TerrainChunkPagerRuntime_MetadataBufferId = 71740,
        TerrainChunkPagerRuntime_SectorCoordsBufferId = 71741,
        TerrainChunkPagerRuntime_StagingBytesBufferId = 71742,
        TerrainChunkPagerRuntime_ActiveBytesBufferId = 71743,
        TerrainChunkPagerRuntime_CompressedScratchBufferId = 71744,
        TerrainChunkPagerRuntime_WorkerRequestBufferId = 71745,
        TerrainChunkPagerRuntime_WorkerResultBufferId = 71746,
        TerrainChunkPagerRuntime_JobLoadRequestsBufferId = 71747,
        TerrainChunkPagerRuntime_JobLoadCountBufferId = 71748,
        TerrainChunkPagerRuntime_JobStaleSlotsBufferId = 71749,
        TerrainChunkPagerRuntime_JobStaleCountBufferId = 71750,
        TerrainChunkPagerRuntime_TelemetryRingBufferId = 71751,
        TerrainChunkPagerRuntime_TuningBufferId = 71752,
        TerrainChunkPagerRuntime_CountersBufferId = 71753,
        TerrainChunkPagerRuntime_FreedSlotsBufferId = 71754,
        TerrainChunkPagerRuntime_FreedCountBufferId = 71755,
        TerrainChunkPagerRuntime_HardwareProfilesBufferId = 71756,
        TerrainChunkPagerRuntime_CsvScratchBufferId = 71757,
        TerrainChunkPagerRuntime_TelemetryDumpSnapshotBufferId = 71758,
        WorldChunkResidencyManager_ChunkCentersVaultBufferId = 70567,
        WorldChunkResidencyManager_ResidencyTelemetryVaultBufferId = 70568,
        WorldChunkResidencyManager_LoadImmediateRadiusFlagsVaultBufferId = 70571,
        WorldChunkResidencyManager_ActiveImpostorsVaultBufferId = 70572,
        WorldChunkResidencyManager_ImpostorTypesVaultBufferId = 70573,
        WorldChunkResidencyManager_ActiveImpostorChunkIdsVaultBufferId = 70574,
        WorldChunkResidencyManager_ActiveImpostorSpawnTimesVaultBufferId = 70575,
        WorldChunkResidencyManager_ActiveImpostorCentersVaultBufferId = 70576,
        WorldChunkResidencyManager_ActiveImpostorSizesVaultBufferId = 70577,
        WorldChunkResidencyManager_ActiveImpostorFlagsVaultBufferId = 70578,
        WorldChunkResidencyManager_ActiveImpostorCartographyVaultBufferId = 70579,
        WorldGenerativeGeologyTerrainSeamApplier_TerrainSeamBlackBoxBufferId = 5440545,
        WorldGenerativeGeologyTerrainSeamApplier_TerrainSeamNativePlansBufferId = 5440546,
        WorldGenerativeGeologyTerrainSeamApplier_TerrainSeamPatchHeightsBufferId = 5440547,
        WorldGenerativeGeologyTerrainSeamApplier_TerrainSeamBlendMaskBufferId = 5440548,
        WorldGenerativeGeologyTerrainSeamApplier_TerrainSeamNormalsBufferId = 5440549,
        WorldGenerativeGeologyVoxelBridgeDirector_VoxelBridgeBlackBoxBufferId = 5440550,
    }

    [Flags]
    public enum H8AllocationFlags : ushort
    {
        None = 0,
        NativeArray = 1 << 0,
        Raw = 1 << 1,
        Vault = 1 << 2,
        Alias = 1 << 3,
        Freed = 1 << 4,
        SubAllocatorRoot = 1 << 6
    }

    public enum H8BlockState : byte
    {
        Free = 0,
        Occupied = 1,
        Reserved = 2
    }

    [Flags]
    internal enum H8MemoryTelemetryFlags : ushort
    {
        None = 0,
        Initialized = 1 << 0,
        Allocated = 1 << 1,
        Released = 1 << 2,
        ForcedRelease = 1 << 3,
        SceneTransition = 1 << 4,
        BaselineMismatch = 1 << 5,
        Shutdown = 1 << 6,
        Fault = 1 << 7,
        Heartbeat = 1 << 8
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct H8RawReallocationGuard
    {
        private const uint GuardSignature = 0x48384752u; // H8GR

        [FieldOffset(0)] public int CompactionFenceHeld;
        [FieldOffset(4)] public uint ActiveLockMask;
        [FieldOffset(8)] public byte HasPinnedExternalViews;
        [FieldOffset(9)] public byte Reserved0;
        [FieldOffset(10)] public ushort Reserved1;
        [FieldOffset(12)] public uint Signature;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static H8RawReallocationGuard Create(
            bool compactionFenceHeld,
            uint activeLockMask,
            bool hasPinnedExternalViews)
        {
            H8RawReallocationGuard guard = default;
            guard.CompactionFenceHeld = compactionFenceHeld ? 1 : 0;
            guard.ActiveLockMask = activeLockMask;
            guard.HasPinnedExternalViews = hasPinnedExternalViews ? (byte)1 : (byte)0;
            guard.Signature = GuardSignature;
            return guard;
        }

        public bool AllowsRelocation =>
            Signature == GuardSignature &&
            CompactionFenceHeld != 0 &&
            ActiveLockMask == 0u &&
            HasPinnedExternalViews == 0;
    }

    /// <summary>
    /// Native memory-map descriptor for occupied/free regions owned by <see cref="H8Memory"/>.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct BlockDescriptor
    {
        [FieldOffset(0)] public IntPtr BasePointer;
        [FieldOffset(8)] public long OffsetBytes;
        [FieldOffset(16)] public long Bytes;
        [FieldOffset(24)] public int OwnerKey;
        [FieldOffset(28)] public int Generation;
        [FieldOffset(32)] public SystemID Owner;
        [FieldOffset(34)] public ushort Flags;
        [FieldOffset(36)] public ushort Reserved2;
        [FieldOffset(38)] public byte State;
        [FieldOffset(39)] public byte Reserved;
    }

    /// <summary>
    /// Blittable record copied to crash dumps and leak-reap passes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    internal struct H8AllocationRecord
    {
        [FieldOffset(0)] internal IntPtr Pointer;
        [FieldOffset(8)] public long Bytes;
        [FieldOffset(16)] public int Length;
        [FieldOffset(20)] public int Stride;
        [FieldOffset(24)] public int Alignment;
        [FieldOffset(28)] public int AllocationIndex;
        [FieldOffset(32)] public int Generation;
        [FieldOffset(36)] public Allocator Allocator;
        [FieldOffset(40)] public SystemID Owner;
        [FieldOffset(42)] public ushort Flags;
        [FieldOffset(44)] public ushort Reserved;
        [FieldOffset(46)] public ushort Reserved2;
    }

    /// <summary>
    /// Fixed-size sentinel heartbeat copied into fatal memory dumps.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct H8MemoryTelemetryEntry
    {
        [FieldOffset(0)] public long TotalBytes;
        [FieldOffset(8)] public long TransitionBaselineBytes;
        [FieldOffset(16)] public long LastTransitionReleasedBytes;
        [FieldOffset(24)] public uint Sequence;
        [FieldOffset(28)] public int ActiveAllocationCount;
        [FieldOffset(32)] public int BlockDescriptorCount;
        [FieldOffset(36)] public int AllocationGeneration;
        [FieldOffset(40)] public int TransitionCutoffGeneration;
        [FieldOffset(44)] public int TransitionSequence;
        [FieldOffset(48)] public int LastTransitionReleasedCount;
        [FieldOffset(52)] public int FatalLeakPreventedCount;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public ushort Owner;
        [FieldOffset(62)] public ushort Flags;
    }

    public sealed class FatalMemoryException : InvalidOperationException
    {
        private FatalMemoryException(string message) : base(message)
        {
        }

        public static void ThrowUnknownFreeOwner()
        {
            throw new FatalMemoryException("H8Memory free owner is unknown.");
        }

        public static void ThrowUnknownAllocationOwner()
        {
            throw new FatalMemoryException("H8Memory allocation owner is unknown.");
        }

        public static void ThrowUnknownAliasReader()
        {
            throw new FatalMemoryException("H8Memory alias reader is unknown.");
        }

        public static void ThrowWrongFreeOwner()
        {
            throw new FatalMemoryException("H8Memory free owner mismatch.");
        }

        public static void ThrowUntrackedPointer()
        {
            throw new FatalMemoryException("H8Memory free pointer is untracked.");
        }

        public static void ThrowStaleVaultHandle()
        {
            throw new FatalMemoryException("GlobalDataVault handle generation mismatch.");
        }

        public static void ThrowVaultTypeMismatch()
        {
            throw new FatalMemoryException("GlobalDataVault buffer type mismatch.");
        }

        public static void ThrowVaultInitializationFailed()
        {
            throw new FatalMemoryException("GlobalDataVault initialization failed.");
        }

        public static void ThrowAllocationSizeMismatch()
        {
            throw new FatalMemoryException("H8Memory reallocation size mismatch.");
        }

        public static void ThrowAllocationTrackingFailed()
        {
            throw new FatalMemoryException("H8Memory allocation tracking failed.");
        }

        public static void ThrowAbiLayoutMismatch()
        {
            throw new FatalMemoryException("H8Memory ABI layout mismatch.");
        }

        public static void ThrowNonBlittableAllocation()
        {
            throw new FatalMemoryException("H8Memory allocation element type is not blittable.");
        }
    }

    /// <summary>
    /// Zero-managed-hot-path memory sentinel for native allocations.
    /// </summary>
    public static unsafe class H8Memory
    {
        private const int DefaultCapacity = 4096;
        private const int MaxTrackingCapacity = 65536;
        private const int OwnerByteSlots = 65536;
        private const int OwnerRegistryCapacity = 256;
        private const int DefaultOwnerPointerCapacity = 16;
        private const int BlackBoxFrameCount = 300;
        private const int MinimumRawAlignment = 16;
        private const int MaximumRawAlignment = 4096;
        private const long LowTierPoolCapBytes = 512L * 1024L * 1024L;
        private const int NoTransitionCutoffGeneration = -1;
        private const int BlockDescriptorSizeBytes = 40;
        private const int H8AllocationRecordSizeBytes = 48;
        private const int H8MemoryTelemetryEntrySizeBytes = 64;
        private const ulong FatalLeakDumpMagic = 0x3130444D454D3848UL; // H8MEMD01
        private const int FatalLeakDumpVersion = 5;
        private const ulong AddressFingerprintSeed = 14695981039346656037UL;
        private const ulong AddressFingerprintPrime = 1099511628211UL;
        private const uint ReplaySnapshotHashSeed = 2166136261U;
        private const uint ReplaySnapshotHashPrime = 16777619U;
        private const byte BlackBoxRingKindHeartbeat = 1;
        private const byte BlackBoxRingKindLifecycleEvent = 2;
        private const string AgentDumpFileName = "Dump_SENTINEL_DISPOSAL_GUARD.bin";
        private const string AgentH8DumpFileName = "Dump_SENTINEL_DISPOSAL_GUARD.h8dump";

        private static NativeParallelHashMap<long, SystemID> _allocationOwners;
        private static NativeParallelHashMap<long, int> _allocationRecordIndices;
        private static NativeParallelHashMap<ushort, NativeList<IntPtr>> _ownerPointers;
        private static NativeParallelHashMap<ushort, JobHandle> _ownerJobHandles;
        private static NativeList<ushort> _ownerPointerKeys;
        private static NativeList<ushort> _ownerJobKeys;
        private static NativeArray<H8AllocationRecord> _records;
        private static NativeArray<long> _ownerBytes;
        private static NativeList<BlockDescriptor> _blockDescriptors;
        private static NativeArray<H8MemoryTelemetryEntry> _blackBox;
        private static NativeArray<H8MemoryTelemetryEntry> _eventBlackBox;
        private static int _recordCount;
        private static long _totalBytes;
        private static long _poolCapBytes = LowTierPoolCapBytes;
        private static int _fatalLeakPreventedCount;
        private static int _blackBoxCursor;
        private static int _eventBlackBoxCursor;
        private static int _blackBoxRecordedCount;
        private static int _eventBlackBoxRecordedCount;
        private static uint _blackBoxSequence;
        private static uint _eventBlackBoxSequence;
        private static uint _telemetryFrameId;
        private static int _blockDescriptorMutationGate;
        private static int _allocationGeneration = 1;
        private static int _transitionCutoffGeneration = NoTransitionCutoffGeneration;
        private static int _transitionSequence;
        private static int _lastTransitionReleasedCount;
        private static long _lastTransitionReleasedBytes;
        private static long _transitionBaselineBytes;
        private static long _transitionExpectedBytes;
        private static Action _beforeShutdownOwnerReleaseHook;
        private static bool _invokingBeforeShutdownOwnerReleaseHook;
        private static bool _lastTransitionBaselineVerified = true;
        private static bool _deferSceneUnloadedVerificationToRuntime;
        private static bool _sceneHooksRegistered;
        private static bool _initialized;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        // One AtomicSafetyHandle per DISTINCT alias region, never one handle for the whole process.
        // A shared handle cannot detect a real race between two views of the SAME region any better than a
        // per-region handle, and it makes Unity report two unrelated regions as "the same
        // UNKNOWN_OBJECT_TYPE", which refuses job SCHEDULING and cannot be cleared by any JobHandle chaining
        // in the calling system. Two views of one region still share a handle - they genuinely alias.
        // Capacity mirrors DefaultCapacity, the initial _records tracking capacity declared above, because an
        // alias region is always either a tracked H8Memory allocation or a sub-offset inside one.
        private const int AliasRegionCapacity = DefaultCapacity;
        private const int AliasRegionCapacityMask = AliasRegionCapacity - 1;
        private const int AliasRegionProbeLimit = 8;

        private static AtomicSafetyHandle _aliasSafetyHandle;
        private static bool _aliasSafetyHandleCreated;
        // COLD ALLOC: long[4096] - alias region pointer keys, 0 marks a free slot - owner: H8Memory
        private static long[] _aliasRegionKeys;
        // COLD ALLOC: AtomicSafetyHandle[4096] - one safety handle per distinct alias region - owner: H8Memory
        private static AtomicSafetyHandle[] _aliasRegionHandles;
        // COLD ALLOC: bool[4096] - per-region safety handle lifetime flags - owner: H8Memory
        private static bool[] _aliasRegionHandleCreated;
        private static int _aliasRegionMutationGate;
        private static int _aliasRegionOwnerThreadId;
        private static int _aliasRegionLiveCount;
        private static bool _aliasRegionExhaustionReported;
#endif

        /// <summary>Tracked allocation count.</summary>
        public static int ActiveAllocationCount => _recordCount;

        /// <summary>True while H8Memory tracking tables are live.</summary>
        public static bool IsInitialized => _initialized;

        /// <summary>
        /// Registers owner teardown that must run before H8Memory force-releases tracked native pointers.
        /// </summary>
        public static void RegisterBeforeShutdownOwnerRelease(Action releaseHook)
        {
            if (releaseHook == null)
                return;

            _beforeShutdownOwnerReleaseHook -= releaseHook;
            _beforeShutdownOwnerReleaseHook += releaseHook;
        }

        /// <summary>
        /// Unregisters owner teardown from the pre-shutdown release lane.
        /// </summary>
        public static void UnregisterBeforeShutdownOwnerRelease(Action releaseHook)
        {
            if (releaseHook == null)
                return;

            _beforeShutdownOwnerReleaseHook -= releaseHook;
        }

        /// <summary>Total tracked bytes.</summary>
        public static long TotalBytes => _totalBytes;

        /// <summary>Total tracked bytes. Scene-transition verification uses this alias.</summary>
        public static long TotalAllocatedBytes => _totalBytes;

        /// <summary>Tracked memory-map descriptor count.</summary>
        public static int BlockDescriptorCount => _blockDescriptors.IsCreated ? _blockDescriptors.Length : 0;

        /// <summary>Configured native pool cap in bytes.</summary>
        public static long PoolCapBytes => _poolCapBytes;

        /// <summary>Number of owner-unregister leaks force-reaped by the sentinel.</summary>
        public static int FatalLeakPreventedCount => _fatalLeakPreventedCount;

        /// <summary>True while a scene transition generation cutoff is awaiting verification.</summary>
        public static bool HasPendingSceneTransition => _transitionCutoffGeneration != NoTransitionCutoffGeneration;

        /// <summary>True when the last scene transition purge removed pre-cutoff scene allocations.</summary>
        public static bool LastTransitionBaselineVerified => _lastTransitionBaselineVerified;

        /// <summary>Bytes released by the last scene transition leak purge.</summary>
        public static long LastTransitionReleasedBytes => _lastTransitionReleasedBytes;

        /// <summary>Allocation records released by the last scene transition leak purge.</summary>
        public static int LastTransitionReleasedCount => _lastTransitionReleasedCount;

        /// <summary>Expected tracked bytes after old-scene purge while post-cutoff scene allocations remain live.</summary>
        public static long LastTransitionExpectedBytes => _transitionExpectedBytes;

        /// <summary>
        /// Records the per-frame memory sentinel heartbeat into the fixed 300-entry blackbox ring.
        /// </summary>
        public static void RecordHeartbeat()
        {
            if (!_initialized)
                return;

            RecordBlackBox(SystemID.H8Memory, H8MemoryTelemetryFlags.Heartbeat);
        }

        public static void SetTelemetryFrameId(uint frameId)
        {
            _telemetryFrameId = frameId;
        }

        internal static uint ResolveTelemetryFrame(uint sequence)
        {
            uint frameId = _telemetryFrameId;
            return frameId != 0u ? frameId : sequence;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorShutdownHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            UnityEditor.EditorApplication.quitting -= Shutdown;
            UnityEditor.EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            UnityEditor.EditorApplication.quitting += Shutdown;
            UnityEditor.EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
        }

        private static void HandleEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode ||
                state == UnityEditor.PlayModeStateChange.EnteredEditMode)
            {
                Shutdown();
            }
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSubsystemRegistration()
        {
            Shutdown();
            ResetStaticValueState();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneHooksAfterSceneLoad()
        {
            RegisterSceneHooks();
        }

        /// <summary>
        /// Initializes native tracking tables. Safe to call more than once.
        /// </summary>
        public static void Initialize(int capacity = DefaultCapacity, long poolCapBytes = LowTierPoolCapBytes)
        {
            if (_initialized)
                return;

            if (!ValidateAbiLayout())
                return;
            int safeCapacity = ResolveTrackingCapacity(capacity);
            try
            {
                // COLD ALLOC: NativeParallelHashMap<long,SystemID>[capacity] - pointer to owner registry - owner: H8Memory
                _allocationOwners = new NativeParallelHashMap<long, SystemID>(safeCapacity, Allocator.Persistent);
                // COLD ALLOC: NativeParallelHashMap<long,int>[capacity] - pointer to allocation record index - owner: H8Memory
                _allocationRecordIndices = new NativeParallelHashMap<long, int>(safeCapacity, Allocator.Persistent);
                // COLD ALLOC: NativeParallelHashMap<ushort,NativeList<IntPtr>>[256] - SystemID value to allocation pointer registry - owner: H8Memory
                _ownerPointers = new NativeParallelHashMap<ushort, NativeList<IntPtr>>(OwnerRegistryCapacity, Allocator.Persistent);
                // COLD ALLOC: NativeParallelHashMap<ushort,JobHandle>[256] - SystemID value teardown job fences - owner: H8Memory
                _ownerJobHandles = new NativeParallelHashMap<ushort, JobHandle>(OwnerRegistryCapacity, Allocator.Persistent);
                // COLD ALLOC: NativeList<ushort>[256] - SystemID value owner pointer registry keys for deterministic disposal - owner: H8Memory
                _ownerPointerKeys = new NativeList<ushort>(OwnerRegistryCapacity, Allocator.Persistent);
                // COLD ALLOC: NativeList<ushort>[256] - SystemID value job fence registry keys for deterministic shutdown - owner: H8Memory
                _ownerJobKeys = new NativeList<ushort>(OwnerRegistryCapacity, Allocator.Persistent);
                // COLD ALLOC: NativeArray<H8AllocationRecord>[capacity] - allocation table for leak reaping - owner: H8Memory
                _records = new NativeArray<H8AllocationRecord>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                // COLD ALLOC: NativeArray<long>[65536] - bytes per SystemID slot - owner: H8Memory
                _ownerBytes = new NativeArray<long>(OwnerByteSlots, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                // COLD ALLOC: NativeList<BlockDescriptor>[capacity] - native memory map descriptors - owner: H8Memory
                _blockDescriptors = new NativeList<BlockDescriptor>(safeCapacity, Allocator.Persistent);
                // COLD ALLOC: NativeArray<H8MemoryTelemetryEntry>[300] - sentinel heartbeat ring - owner: H8Memory
                _blackBox = new NativeArray<H8MemoryTelemetryEntry>(BlackBoxFrameCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                // COLD ALLOC: NativeArray<H8MemoryTelemetryEntry>[300] - lifecycle snapshots for leak dumps - owner: H8Memory
                _eventBlackBox = new NativeArray<H8MemoryTelemetryEntry>(BlackBoxFrameCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                _recordCount = 0;
                _totalBytes = 0L;
                _poolCapBytes = poolCapBytes > 0L ? poolCapBytes : LowTierPoolCapBytes;
                _fatalLeakPreventedCount = 0;
                _blackBoxCursor = 0;
                _eventBlackBoxCursor = 0;
                _blackBoxRecordedCount = 0;
                _eventBlackBoxRecordedCount = 0;
                _blackBoxSequence = 0u;
                _eventBlackBoxSequence = 0u;
                _telemetryFrameId = 0u;
                _blockDescriptorMutationGate = 0;
                _allocationGeneration = 1;
                _transitionCutoffGeneration = NoTransitionCutoffGeneration;
                _transitionSequence = 0;
                _lastTransitionReleasedCount = 0;
                _lastTransitionReleasedBytes = 0L;
                _transitionBaselineBytes = 0L;
                _transitionExpectedBytes = 0L;
                _lastTransitionBaselineVerified = true;
                _deferSceneUnloadedVerificationToRuntime = false;
                _initialized = true;
                RegisterSceneHooks();
                RecordBlackBox(SystemID.H8Memory, H8MemoryTelemetryFlags.Initialized);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                // Idempotent: releases anything a previous partially-initialized pass left live before this
                // pass creates new handles, so no handle can be orphaned by a re-entered Initialize.
                ReleaseAliasSafetyHandleIfCreated();
                _aliasSafetyHandle = AtomicSafetyHandle.Create();
                _aliasSafetyHandleCreated = true;
                if ((AliasRegionCapacity & AliasRegionCapacityMask) == 0)
                {
                    _aliasRegionKeys = new long[AliasRegionCapacity];
                    _aliasRegionHandles = new AtomicSafetyHandle[AliasRegionCapacity];
                    _aliasRegionHandleCreated = new bool[AliasRegionCapacity];
                    _aliasRegionLiveCount = 0;
                    _aliasRegionExhaustionReported = false;
                    _aliasRegionMutationGate = 0;
                    // Captured last: the per-region lane stays closed until the table is fully built, and every
                    // AtomicSafetyHandle.Create/Release for that lane is pinned to this thread.
                    _aliasRegionOwnerThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                }
#endif
            }
            catch
            {
                AbortInitialize();
                throw;
            }
        }

        private static bool ValidateAbiLayout()
        {
            bool valid =
                UnsafeUtility.SizeOf<BlockDescriptor>() == BlockDescriptorSizeBytes &&
                UnsafeUtility.SizeOf<H8AllocationRecord>() == H8AllocationRecordSizeBytes &&
                UnsafeUtility.SizeOf<H8MemoryTelemetryEntry>() == H8MemoryTelemetryEntrySizeBytes &&
                ValidateBlockDescriptorAbiOffsets() &&
                ValidateAllocationRecordAbiOffsets() &&
                ValidateTelemetryEntryAbiOffsets();
            return valid;
        }

        private static bool ValidateBlockDescriptorAbiOffsets()
        {
            BlockDescriptor descriptor = default;
            byte* descriptorBase = (byte*)&descriptor;

            return
                ByteOffset(descriptorBase, &descriptor.BasePointer) == 0 &&
                ByteOffset(descriptorBase, &descriptor.OffsetBytes) == 8 &&
                ByteOffset(descriptorBase, &descriptor.Bytes) == 16 &&
                ByteOffset(descriptorBase, &descriptor.OwnerKey) == 24 &&
                ByteOffset(descriptorBase, &descriptor.Generation) == 28 &&
                ByteOffset(descriptorBase, &descriptor.Owner) == 32 &&
                ByteOffset(descriptorBase, &descriptor.Flags) == 34 &&
                ByteOffset(descriptorBase, &descriptor.Reserved2) == 36 &&
                ByteOffset(descriptorBase, &descriptor.State) == 38 &&
                ByteOffset(descriptorBase, &descriptor.Reserved) == 39;
        }

        private static bool ValidateAllocationRecordAbiOffsets()
        {
            H8AllocationRecord record = default;
            byte* recordBase = (byte*)&record;

            return
                ByteOffset(recordBase, &record.Pointer) == 0 &&
                ByteOffset(recordBase, &record.Bytes) == 8 &&
                ByteOffset(recordBase, &record.Length) == 16 &&
                ByteOffset(recordBase, &record.Stride) == 20 &&
                ByteOffset(recordBase, &record.Alignment) == 24 &&
                ByteOffset(recordBase, &record.AllocationIndex) == 28 &&
                ByteOffset(recordBase, &record.Generation) == 32 &&
                ByteOffset(recordBase, &record.Allocator) == 36 &&
                ByteOffset(recordBase, &record.Owner) == 40 &&
                ByteOffset(recordBase, &record.Flags) == 42 &&
                ByteOffset(recordBase, &record.Reserved) == 44 &&
                ByteOffset(recordBase, &record.Reserved2) == 46;
        }

        private static bool ValidateTelemetryEntryAbiOffsets()
        {
            H8MemoryTelemetryEntry telemetry = default;
            byte* telemetryBase = (byte*)&telemetry;

            return
                ByteOffset(telemetryBase, &telemetry.TotalBytes) == 0 &&
                ByteOffset(telemetryBase, &telemetry.TransitionBaselineBytes) == 8 &&
                ByteOffset(telemetryBase, &telemetry.LastTransitionReleasedBytes) == 16 &&
                ByteOffset(telemetryBase, &telemetry.Sequence) == 24 &&
                ByteOffset(telemetryBase, &telemetry.ActiveAllocationCount) == 28 &&
                ByteOffset(telemetryBase, &telemetry.BlockDescriptorCount) == 32 &&
                ByteOffset(telemetryBase, &telemetry.AllocationGeneration) == 36 &&
                ByteOffset(telemetryBase, &telemetry.TransitionCutoffGeneration) == 40 &&
                ByteOffset(telemetryBase, &telemetry.TransitionSequence) == 44 &&
                ByteOffset(telemetryBase, &telemetry.LastTransitionReleasedCount) == 48 &&
                ByteOffset(telemetryBase, &telemetry.FatalLeakPreventedCount) == 52 &&
                ByteOffset(telemetryBase, &telemetry.Frame) == 56 &&
                ByteOffset(telemetryBase, &telemetry.Owner) == 60 &&
                ByteOffset(telemetryBase, &telemetry.Flags) == 62;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ByteOffset(void* basePtr, void* fieldPtr)
        {
            return (int)((byte*)fieldPtr - (byte*)basePtr);
        }

        /// <summary>
        /// Applies the bootstrap memory ceiling after hardware classification without reallocating tracking tables.
        /// </summary>
        public static void ConfigurePoolCap(long poolCapBytes)
        {
            if (poolCapBytes <= 0L)
                poolCapBytes = LowTierPoolCapBytes;

            if (!_initialized)
            {
                Initialize(DefaultCapacity, poolCapBytes);
                return;
            }

            if (poolCapBytes >= _totalBytes)
                _poolCapBytes = poolCapBytes;
        }

        /// <summary>
        /// Allocates a native array and records its owner before it can be exposed to jobs.
        /// </summary>
        public static NativeArray<T> Allocate<T>(
            int length,
            SystemID owner,
            Allocator allocator,
            NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : struct
        {
            if (!_initialized)
                Initialize();
            if (!_initialized)
                return default;

            if (length <= 0)
                return default;
            if (owner == SystemID.Unknown)
            {
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                return default;
            }
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>() || !UnsafeUtility.IsBlittable<T>())
            {
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                return default;
            }

            int stride = UnsafeUtility.SizeOf<T>();
            long bytes = (long)stride * length;
            if (!TryReserveBytes(owner, bytes) || !EnsureTrackingCapacity())
                return default;

            NativeArray<T> array = new NativeArray<T>(length, allocator, options);
            void* pointer = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            if (!RegisterPointer(pointer, bytes, length, stride, UnsafeUtility.AlignOf<T>(), owner, allocator, H8AllocationFlags.NativeArray))
            {
                array.Dispose();
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                return default;
            }

            return array;
        }

        /// <summary>
        /// Releases a native array only when the caller matches the recorded allocation owner.
        /// </summary>
        public static void Release<T>(ref NativeArray<T> array, SystemID owner) where T : struct
        {
            if (!array.IsCreated)
                return;
            if (owner == SystemID.Unknown)
            {
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                return;
            }
            if (!_initialized)
            {
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                array = default;
                return;
            }

            void* pointer = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            IntPtr pointerValue = (IntPtr)pointer;
            H8AllocationRecord record = default;
            bool canRestoreTracking = TryFindRecordIndex(pointerValue, out int recordIndex);
            if (canRestoreTracking)
                record = _records[recordIndex];
            if (!UnregisterPointer(pointer, owner))
                return;
            try
            {
                array.Dispose();
                array = default;
            }
            catch (Exception disposeException)
            {
                if (canRestoreTracking && array.IsCreated && !TryRestoreUnregisteredRecord(in record))
                    throw new AggregateException("H8Memory NativeArray tracking restore failed after dispose failure.", disposeException);

                throw;
            }
        }

        /// <summary>
        /// Defers native-array disposal behind an active job dependency when the caller matches the recorded owner.
        /// </summary>
        public static JobHandle Release<T>(ref NativeArray<T> array, JobHandle dependency, SystemID owner) where T : struct
        {
            if (!array.IsCreated)
                return dependency;
            if (owner == SystemID.Unknown)
            {
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                return dependency;
            }
            if (!_initialized)
            {
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                array = default;
                return dependency;
            }

            void* pointer = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            IntPtr pointerValue = (IntPtr)pointer;
            H8AllocationRecord record = default;
            bool canRestoreTracking = TryFindRecordIndex(pointerValue, out int recordIndex);
            if (canRestoreTracking)
                record = _records[recordIndex];
            if (!UnregisterPointer(pointer, owner))
                return dependency;
            try
            {
                JobHandle disposeHandle = array.Dispose(dependency);
                if (!RegisterActiveJob(owner, disposeHandle))
                    TryCompleteOwnerJobHandle(ref disposeHandle);
                array = default;
                return disposeHandle;
            }
            catch (Exception disposeException)
            {
                if (canRestoreTracking && array.IsCreated && !TryRestoreUnregisteredRecord(in record))
                    throw new AggregateException("H8Memory NativeArray tracking restore failed after deferred dispose failure.", disposeException);

                throw;
            }
        }

        /// <summary>
        /// Copies H8-owned native allocations into the deterministic replay source list.
        /// </summary>
        public static int CopySnapshotSources(
            NativeArray<Hecton8.Core.NativeAllocationSnapshotSource> destination,
            int startIndex,
            uint excludedOwnerHash = 0u)
        {
            if (!destination.IsCreated)
                return 0;

            int writeIndex = startIndex < 0 ? 0 : startIndex;
            if (writeIndex >= destination.Length)
                return destination.Length;

            if (!_initialized || !_records.IsCreated)
                return writeIndex;

            int count = _recordCount;
            for (int i = 0; i < count && writeIndex < destination.Length; i++)
            {
                H8AllocationRecord record = _records[i];
                if (!CanCopyReplaySnapshotSource(in record, excludedOwnerHash))
                    continue;

                Hecton8.Core.NativeAllocationSnapshotSource snapshot = default;
                snapshot.SourcePointerValue = unchecked((ulong)record.Pointer.ToInt64());
                snapshot.Bytes = record.Bytes;
                snapshot.OwnerHash = ComputeReplayOwnerHash(record.Owner);
                snapshot.LabelHash = ComputeReplayLabelHash(in record);
                snapshot.AllocationFrame = record.Generation;
                snapshot.Lifetime = ResolveReplayLifetime(record.Allocator);
                snapshot.Allocator = (byte)record.Allocator;
                destination[writeIndex++] = snapshot;
            }

            return writeIndex;
        }

        /// <summary>
        /// Records an owner job fence so forced teardown can block only at scene-transition/owner-destruction boundaries.
        /// </summary>
        /// <param name="owner">Native allocation owner.</param>
        /// <param name="handle">Active job handle touching owner memory.</param>
        public static bool RegisterActiveJob(SystemID owner, JobHandle handle)
        {
            if (owner == SystemID.Unknown)
            {
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                TryCompleteOwnerJobHandle(ref handle);
                return false;
            }
            if (!_initialized)
                Initialize();
            if (!_initialized)
            {
                TryCompleteOwnerJobHandle(ref handle);
                return false;
            }

            if (!_ownerJobHandles.IsCreated)
            {
                TryCompleteOwnerJobHandle(ref handle);
                return false;
            }

            ushort ownerKey = GetOwnerKey(owner);
            try
            {
                if (_ownerJobHandles.TryGetValue(ownerKey, out JobHandle existingHandle))
                {
                    JobHandle combinedHandle = JobHandle.CombineDependencies(existingHandle, handle);
                    _ownerJobHandles[ownerKey] = combinedHandle;
                    if (!EnsureOwnerJobKey(ownerKey))
                    {
                        TryCompleteOwnerJobHandle(ref combinedHandle);
                        _ownerJobHandles.Remove(ownerKey);
                        RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                        return false;
                    }

                    return true;
                }

                if (!_ownerJobHandles.TryAdd(ownerKey, handle))
                {
                    TryCompleteOwnerJobHandle(ref handle);
                    RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                    return false;
                }

                if (!EnsureOwnerJobKey(ownerKey))
                {
                    TryCompleteOwnerJobHandle(ref handle);
                    _ownerJobHandles.Remove(ownerKey);
                    RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                    return false;
                }

                return true;
            }
            catch
            {
                TryCompleteOwnerJobHandle(ref handle);
                CompleteOwnerJobs(owner);
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                return false;
            }
        }

        /// <summary>
        /// Defers Unity scene-unload verification so SceneRuntimeService can evict vault buffers first.
        /// </summary>
        public static void SetSceneUnloadedVerificationDeferred(bool deferred)
        {
            _deferSceneUnloadedVerificationToRuntime = deferred;
        }

        /// <summary>
        /// Captures a generation cutoff before scene loading can allocate new Ocean memory.
        /// </summary>
        public static void BeginSceneTransitionPurge()
        {
            if (!_initialized)
                Initialize();

            int cutoffGeneration = _allocationGeneration;
            _allocationGeneration = AdvanceDescriptorGeneration(_allocationGeneration);
            _transitionCutoffGeneration = cutoffGeneration;
            _transitionBaselineBytes = ComputeSceneTransitionBaselineBytes(cutoffGeneration);
            _transitionExpectedBytes = _transitionBaselineBytes;
            _lastTransitionReleasedCount = 0;
            _lastTransitionReleasedBytes = 0L;
            _lastTransitionBaselineVerified = false;
            _transitionSequence = AdvanceDescriptorGeneration(_transitionSequence);
            RecordBlackBox(SystemID.H8Memory, H8MemoryTelemetryFlags.SceneTransition);
        }

        /// <summary>
        /// Completes scene-transition leak purging and validates that no pre-cutoff scene-owned allocations remain.
        /// </summary>
        /// <returns>True when the transition baseline plus post-cutoff allocations matches total tracked bytes.</returns>
        public static bool CompleteSceneTransitionVerification()
        {
            if (!_initialized)
                return true;

            if (_transitionCutoffGeneration == NoTransitionCutoffGeneration)
                return _lastTransitionBaselineVerified;

            int cutoffGeneration = _transitionCutoffGeneration;
            ReleaseSceneTransitionLeaks();
            _transitionExpectedBytes = ComputeSceneTransitionExpectedBytes(cutoffGeneration);
            bool verified = _totalBytes == _transitionExpectedBytes;
            _lastTransitionBaselineVerified = verified;
            if (!verified)
                WriteFatalLeakBlackBox(SystemID.Unknown, 0, _totalBytes - _transitionExpectedBytes, baselineMismatch: true);
            else
            {
                RecordBlackBox(SystemID.H8Memory, H8MemoryTelemetryFlags.SceneTransition);
                _transitionCutoffGeneration = NoTransitionCutoffGeneration;
            }
            return verified;
        }

        /// <summary>
        /// Cancels a captured transition cutoff when a scene load is abandoned before Unity unloads the old scene.
        /// </summary>
        public static void CancelSceneTransitionPurge()
        {
            if (!_initialized)
                return;

            _transitionCutoffGeneration = NoTransitionCutoffGeneration;
            _transitionBaselineBytes = 0L;
            _transitionExpectedBytes = 0L;
            _lastTransitionReleasedCount = 0;
            _lastTransitionReleasedBytes = 0L;
            _lastTransitionBaselineVerified = false;
            _deferSceneUnloadedVerificationToRuntime = false;
            RecordBlackBox(SystemID.H8Memory, H8MemoryTelemetryFlags.SceneTransition);
        }

        /// <summary>
        /// Force-releases every allocation owned by one system after completing its registered job fence.
        /// </summary>
        /// <param name="owner">Owner to purge.</param>
        /// <returns>Number of force-released allocations.</returns>
        public static int ReleaseAll(SystemID owner)
        {
            if (!_initialized || owner == SystemID.Unknown)
                return 0;

            return ReleaseAll(owner, int.MaxValue, writeBlackBox: true);
        }

        /// <summary>
        /// Allocates raw native memory for vault-owned buffers.
        /// </summary>
        public static void* AllocateRaw(
            long bytes,
            int alignment,
            SystemID owner,
            Allocator allocator,
            bool clearMemory,
            H8AllocationFlags extraFlags = H8AllocationFlags.None)
        {
            if (!_initialized)
                Initialize();
            if (!_initialized)
                return null;

            if (bytes <= 0L)
                return null;
            if (owner == SystemID.Unknown)
            {
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                return null;
            }

            int safeAlignment = ResolveSafeAlignment(alignment);
            if (!TryReserveBytes(owner, bytes) || !EnsureTrackingCapacity())
                return null;

            void* pointer = UnsafeUtility.Malloc(bytes, safeAlignment, allocator);
            if (pointer == null)
                return null;

            if (clearMemory)
                UnsafeUtility.MemClear(pointer, bytes);

            if (!RegisterPointer(pointer, bytes, 0, 0, safeAlignment, owner, allocator, H8AllocationFlags.Raw | extraFlags))
            {
                TryFreeUntrackedRawPointer(pointer, allocator, owner);
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                return null;
            }

            return pointer;
        }

        /// <summary>
        /// Reallocates a raw vault buffer with copy/free semantics and refreshed sentinel ownership.
        /// </summary>
        internal static void* ReallocateRaw(
            void* oldPointer,
            long oldBytes,
            long newBytes,
            int alignment,
            SystemID owner,
            Allocator allocator,
            bool clearExtendedBytes,
            in H8RawReallocationGuard relocationGuard,
            H8AllocationFlags extraFlags = H8AllocationFlags.None)
        {
            if (!_initialized)
                Initialize();
            if (!_initialized)
                return null;

            if (newBytes <= 0L)
                return null;
            if (owner == SystemID.Unknown)
            {
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                return null;
            }

            if (!relocationGuard.AllowsRelocation)
            {
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                return null;
            }

            if (oldPointer == null)
                return AllocateRaw(newBytes, alignment, owner, allocator, clearExtendedBytes, extraFlags);

            if (!ValidateTrackedPointerOwner(oldPointer, owner, out long trackedOldBytes))
                return null;
            if (oldBytes > 0L && oldBytes != trackedOldBytes)
            {
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                return null;
            }

            int safeAlignment = ResolveSafeAlignment(alignment);
            if (!TryReserveReplacementBytes(trackedOldBytes, newBytes) || !EnsureTrackingCapacity())
                return null;

            void* newPointer = UnsafeUtility.Malloc(newBytes, safeAlignment, allocator);
            if (newPointer == null)
                return null;

            long copyBytes = trackedOldBytes < newBytes ? trackedOldBytes : newBytes;
            UnsafeUtility.MemMove(newPointer, oldPointer, copyBytes);
            if (clearExtendedBytes && newBytes > copyBytes)
                UnsafeUtility.MemClear((byte*)newPointer + copyBytes, newBytes - copyBytes);

            if (!RegisterPointer(newPointer, newBytes, 0, 0, safeAlignment, owner, allocator, H8AllocationFlags.Raw | extraFlags))
            {
                TryFreeUntrackedRawPointer(newPointer, allocator, owner);
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                return null;
            }

            H8AllocationRecord oldRecord = default;
            bool canRestoreOldTracking = TryFindRecordIndex((IntPtr)oldPointer, out int oldRecordIndex);
            if (canRestoreOldTracking)
                oldRecord = _records[oldRecordIndex];
            if (!UnregisterPointer(oldPointer, owner))
            {
                if (!TryUnregisterFreeAndRestoreOnFailure(newPointer, owner, allocator, requireOwnerMatch: false))
                    RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                return null;
            }
            try
            {
                UnsafeUtility.Free(oldPointer, allocator);
            }
            catch (Exception freeException)
            {
                bool restoredOldTracking = !canRestoreOldTracking || TryRestoreUnregisteredRecord(in oldRecord);
                bool releasedNewPointer = TryUnregisterFreeAndRestoreOnFailure(newPointer, owner, allocator, requireOwnerMatch: false);
                if (!restoredOldTracking || !releasedNewPointer)
                    throw new AggregateException("H8Memory raw reallocation rollback failed after old pointer free failure.", freeException);

                throw;
            }

            return newPointer;
        }

        /// <summary>
        /// Legacy raw free entry point. Tracked memory must use the owner-tagged overload.
        /// </summary>
        [Obsolete("Use FreeRaw(pointer, allocator, SystemID) so tracked memory is freed by its recorded owner.", true)]
        public static void FreeRaw(void* pointer, Allocator allocator)
        {
            FreeRaw(pointer, allocator, SystemID.Unknown);
        }

        /// <summary>
        /// Frees raw native memory only when the caller matches the recorded allocation owner.
        /// </summary>
        public static void FreeRaw(void* pointer, Allocator allocator, SystemID requester)
        {
            if (TryFreeRaw(pointer, allocator, requester))
                return;

            throw new InvalidOperationException(
                $"H8Memory failed to free raw pointer for requester {requester}; pointer ownership remains unchanged.");
        }

        public static bool TryFreeRaw(void* pointer, Allocator allocator, SystemID requester)
        {
            if (pointer == null)
                return true;
            if (requester == SystemID.Unknown)
            {
                RecordBlackBox(requester, H8MemoryTelemetryFlags.Fault);
                return false;
            }
            if (!_initialized)
            {
                RecordBlackBox(requester, H8MemoryTelemetryFlags.Fault);
                return false;
            }

            IntPtr pointerValue = (IntPtr)pointer;
            H8AllocationRecord record = default;
            bool canRestoreTracking = TryFindRecordIndex(pointerValue, out int recordIndex);
            if (canRestoreTracking)
                record = _records[recordIndex];
            if (!UnregisterPointer(pointer, requester))
                return false;
            try
            {
                UnsafeUtility.Free(pointer, allocator);
                return true;
            }
            catch (Exception freeException)
            {
                if (canRestoreTracking && !TryRestoreUnregisteredRecord(in record))
                    throw new AggregateException("H8Memory raw allocation tracking restore failed after free failure.", freeException);

                throw;
            }
        }

        /// <summary>
        /// Releases a raw scene-leak pointer reaped by <c>NativeMemorySentinel</c>, retiring H8 tracking first when present.
        /// </summary>
        /// <returns>True when the pointer was known to H8 tracking; false when the pointer was sentinel-only.</returns>
        public static bool ReleaseSentinelReapedRaw(void* pointer, Allocator fallbackAllocator)
        {
            if (pointer == null)
                return false;

            if (_initialized && TryFindRecordIndex((IntPtr)pointer, out int recordIndex))
            {
                H8AllocationRecord record = _records[recordIndex];
                CompleteOwnerJobs(record.Owner);

                H8AllocationRecord releasedRecord;
                return ForceFreeRecordAt(recordIndex, removeOwnerPointer: true, out releasedRecord);
            }

            TryFreeUntrackedRawPointer(pointer, fallbackAllocator, SystemID.Unknown);
            return false;
        }

        /// <summary>
        /// Creates a read-only alias over an existing buffer without copying.
        /// </summary>
        public static NativeArray<T>.ReadOnly CreateAlias<T>(NativeArray<T> source, SystemID reader) where T : struct
        {
            if (reader == SystemID.Unknown)
            {
                RecordBlackBox(reader, H8MemoryTelemetryFlags.Fault);
                return default;
            }

            if (!source.IsCreated)
                return default;

            return source.AsReadOnly();
        }

        /// <summary>
        /// Creates a read-only alias over raw vault memory without copying.
        /// </summary>
        internal static NativeArray<T>.ReadOnly CreateAlias<T>(void* pointer, int length, SystemID reader) where T : struct
        {
            if (reader == SystemID.Unknown)
            {
                RecordBlackBox(reader, H8MemoryTelemetryFlags.Fault);
                return default;
            }

            return CreateReadOnlyNativeArrayView<T>(pointer, length);
        }

        /// <summary>
        /// Converts owned raw memory into a read-only NativeArray view.
        /// </summary>
        public static NativeArray<T>.ReadOnly CreateReadOnlyNativeArrayView<T>(void* pointer, int length) where T : struct
        {
            NativeArray<T> array = CreateNativeArrayView<T>(pointer, length);
            return array.IsCreated ? array.AsReadOnly() : default;
        }

        /// <summary>
        /// Converts owned raw memory into a NativeArray view.
        /// </summary>
        public static NativeArray<T> CreateNativeArrayView<T>(void* pointer, int length) where T : struct
        {
            if (pointer == null || length <= 0)
                return default;

            NativeArray<T> array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(pointer, length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (TryResolveAliasRegionHandle(pointer, out AtomicSafetyHandle regionHandle))
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, regionHandle);
            else if (_aliasSafetyHandleCreated)
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, _aliasSafetyHandle);
#endif
            return array;
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        /// <summary>
        /// Maps a region pointer onto its table slot. Region pointers are at least
        /// <see cref="MinimumRawAlignment"/> aligned and vault sub-offsets are block aligned, so the low bits
        /// are always zero and have to be mixed before masking or every region collides.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int AliasRegionSlot(long pointerKey)
        {
            ulong mixed = MixAddressFingerprint(AddressFingerprintSeed, unchecked((ulong)pointerKey));
            // The FNV prime is odd, so the low bits of the product carry almost no entropy. Fold the high bits
            // down before masking or every aligned region lands in the same slot.
            mixed ^= mixed >> 29;
            return (int)(mixed & (ulong)AliasRegionCapacityMask);
        }

        /// <summary>
        /// Resolves the safety handle that belongs to one distinct alias region, creating it on first sight of
        /// that region pointer and never again. Returns false - caller falls back to the shared handle, which
        /// is the pre-existing behaviour - when the table is absent, exhausted, contended, or the caller is not
        /// the thread that built the table.
        /// </summary>
        private static bool TryResolveAliasRegionHandle(void* pointer, out AtomicSafetyHandle handle)
        {
            handle = default;
            if (_aliasRegionKeys == null ||
                _aliasRegionHandles == null ||
                _aliasRegionHandleCreated == null ||
                _aliasRegionOwnerThreadId == 0 ||
                System.Threading.Thread.CurrentThread.ManagedThreadId != _aliasRegionOwnerThreadId)
            {
                return false;
            }

            long pointerKey = ((IntPtr)pointer).ToInt64();
            if (pointerKey == 0L)
                return false;

            if (System.Threading.Interlocked.CompareExchange(ref _aliasRegionMutationGate, 1, 0) != 0)
                return false;

            try
            {
                int slot = AliasRegionSlot(pointerKey);
                for (int probe = 0; probe < AliasRegionProbeLimit; probe++)
                {
                    int index = (slot + probe) & AliasRegionCapacityMask;
                    long existingKey = _aliasRegionKeys[index];
                    if (existingKey == pointerKey)
                    {
                        // Same region seen before: hand back the same handle, never create a second one. This
                        // is the per-frame path, so creation here would leak one handle per frame.
                        if (!_aliasRegionHandleCreated[index])
                            return false;

                        handle = _aliasRegionHandles[index];
                        return true;
                    }

                    if (existingKey != 0L)
                        continue;

                    // A key-free slot must not hold a live handle. Enforced rather than assumed so the
                    // exactly-once invariant does not depend on unreachability of a partial write.
                    ReleaseAliasRegionSlot(index);
                    _aliasRegionHandles[index] = AtomicSafetyHandle.Create();
                    _aliasRegionHandleCreated[index] = true;
                    _aliasRegionKeys[index] = pointerKey;
                    _aliasRegionLiveCount++;
                    handle = _aliasRegionHandles[index];
                    return true;
                }

                // Table pressure degrades to the shared handle, which re-admits the old false-positive for the
                // overflowing views only. Reported once so it cannot rot silently.
                if (!_aliasRegionExhaustionReported)
                {
                    _aliasRegionExhaustionReported = true;
                    RecordBlackBox(SystemID.H8Memory, H8MemoryTelemetryFlags.Fault);
                }

                return false;
            }
            finally
            {
                System.Threading.Thread.MemoryBarrier();
                System.Threading.Volatile.Write(ref _aliasRegionMutationGate, 0);
            }
        }

        /// <summary>
        /// Releases the single handle a slot may hold and clears the slot. Caller must be the table owner
        /// thread. The created flag is the exactly-once gate: released handles are unflagged before any other
        /// statement can observe them, so no handle can be released twice.
        /// </summary>
        private static void ReleaseAliasRegionSlot(int index)
        {
            if (_aliasRegionHandleCreated[index])
            {
                AtomicSafetyHandle.Release(_aliasRegionHandles[index]);
                _aliasRegionHandleCreated[index] = false;
            }

            _aliasRegionHandles[index] = default;
            if (_aliasRegionKeys[index] == 0L)
                return;

            _aliasRegionKeys[index] = 0L;
            if (_aliasRegionLiveCount > 0)
                _aliasRegionLiveCount--;
        }

        /// <summary>
        /// Releases every per-region handle whose region lies inside a tracked allocation that is being
        /// retired. This is the region-death path: vault buffers are sub-offsets of the vault arena, so freeing
        /// the arena kills every alias region inside it, and an exact-address region is covered by the same
        /// containment test. Cold path only - it runs on free/ReleaseAll/scene-transition purges, never per
        /// frame - so the full-table scan is affordable and is skipped outright while the table is empty.
        /// </summary>
        private static void ReleaseAliasRegionHandlesInRange(IntPtr basePointer, long bytes)
        {
            if (_aliasRegionKeys == null ||
                _aliasRegionHandles == null ||
                _aliasRegionHandleCreated == null ||
                _aliasRegionLiveCount <= 0 ||
                basePointer == IntPtr.Zero)
            {
                return;
            }

            long rangeStart = basePointer.ToInt64();
            long rangeEnd = bytes > 0L ? rangeStart + bytes : rangeStart + 1L;
            if (rangeEnd <= rangeStart)
                return;

            if (System.Threading.Interlocked.CompareExchange(ref _aliasRegionMutationGate, 1, 0) != 0)
                return;

            try
            {
                for (int i = 0; i < _aliasRegionKeys.Length; i++)
                {
                    long key = _aliasRegionKeys[i];
                    if (key == 0L || key < rangeStart || key >= rangeEnd)
                        continue;

                    ReleaseAliasRegionSlot(i);
                }
            }
            finally
            {
                System.Threading.Thread.MemoryBarrier();
                System.Threading.Volatile.Write(ref _aliasRegionMutationGate, 0);
            }
        }
#endif

        /// <summary>
        /// Force-frees all tracked memory for an unregistered owner.
        /// </summary>
        public static int ReapOwnerLeaks(SystemID owner)
        {
            return ReleaseAll(owner);
        }

        /// <summary>
        /// Dumps the current allocation table to a text file for post-mortem triage.
        /// </summary>
        public static bool DumpAllocationTableText(string path)
        {
            if (!_initialized || string.IsNullOrEmpty(path))
                return false;

            using (StreamWriter writer = new StreamWriter(path, false))
            {
                writer.WriteLine("H8MEMORY_ALLOCATION_TABLE");
                writer.Write("TotalBytes=");
                writer.WriteLine(_totalBytes);
                writer.Write("ActiveAllocationCount=");
                writer.WriteLine(_recordCount);
                for (int i = 0; i < _recordCount; i++)
                {
                    H8AllocationRecord record = _records[i];
                    writer.Write("Index=");
                    writer.Write(record.AllocationIndex);
                    writer.Write(" AddressFingerprint=");
                    writer.Write(ComputeAllocationAddressFingerprint(in record));
                    writer.Write(" Bytes=");
                    writer.Write(record.Bytes);
                    writer.Write(" Owner=");
                    writer.Write((int)record.Owner);
                    writer.Write(" Allocator=");
                    writer.Write((int)record.Allocator);
                    writer.Write(" Flags=");
                    writer.WriteLine(record.Flags);
                }
            }

            return true;
        }

        /// <summary>
        /// Registers or reuses a memory-map descriptor slot. Cold path only.
        /// </summary>
        internal static int RegisterBlockDescriptor(in BlockDescriptor descriptor)
        {
            if (!_initialized)
                Initialize();

            return RegisterBlockDescriptorThreadSafe(in descriptor);
        }

        /// <summary>
        /// Updates a memory-map descriptor in-place.
        /// </summary>
        internal static bool TryUpdateBlockDescriptor(int index, in BlockDescriptor descriptor)
        {
            if (!TryEnterBlockDescriptorMutationGate())
                return false;

            try
            {
                if (!_initialized || !_blockDescriptors.IsCreated || (uint)index >= (uint)_blockDescriptors.Length)
                    return false;

                _blockDescriptors[index] = descriptor;
                return true;
            }
            finally
            {
                ReleaseBlockDescriptorMutationGate();
            }
        }

        /// <summary>
        /// Reads a memory-map descriptor without allocation.
        /// </summary>
        public static bool TryGetBlockDescriptor(int index, out BlockDescriptor descriptor)
        {
            descriptor = default;
            if (!_initialized || !_blockDescriptors.IsCreated || (uint)index >= (uint)_blockDescriptors.Length)
                return false;

            descriptor = _blockDescriptors[index];
            return true;
        }

        internal static bool TryReserveBlockDescriptorSlot(out int index)
        {
            index = -1;
            if (!TryEnterBlockDescriptorMutationGate())
                return false;

            try
            {
                return TryReserveBlockDescriptorSlotNoLock(out index);
            }
            finally
            {
                ReleaseBlockDescriptorMutationGate();
            }
        }

        private static bool TryReserveBlockDescriptorSlotNoLock(out int index)
        {
            index = -1;
            if (!_initialized || !_blockDescriptors.IsCreated)
                return false;

            BlockDescriptor reservation = default;
            reservation.Bytes = -1L;
            reservation.Owner = SystemID.CoreDataVault;
            reservation.Flags = (ushort)H8AllocationFlags.Vault;
            reservation.State = (byte)H8BlockState.Reserved;
            if (TryReserveReusableBlockDescriptorSlot(in reservation, out index))
                return true;
            if (_blockDescriptors.Length < _blockDescriptors.Capacity)
                return TryAppendBlockDescriptorNoLock(in reservation, out index);

            int oldCapacity = _blockDescriptors.Capacity;
            if (oldCapacity >= MaxTrackingCapacity)
                return false;

            int newCapacity = oldCapacity > 0 ? oldCapacity << 1 : DefaultCapacity;
            if (newCapacity < oldCapacity || newCapacity > MaxTrackingCapacity)
                newCapacity = MaxTrackingCapacity;

            if (!TryEnsureBlockDescriptorCapacityNoLock(newCapacity))
                return false;
            if (_blockDescriptors.Length >= _blockDescriptors.Capacity)
                return false;

            return TryAppendBlockDescriptorNoLock(in reservation, out index);
        }

        internal static void ReleaseReservedBlockDescriptor(int index)
        {
            if (!TryEnterBlockDescriptorMutationGate())
                return;

            try
            {
                if (!_initialized || !_blockDescriptors.IsCreated || (uint)index >= (uint)_blockDescriptors.Length)
                    return;

                BlockDescriptor descriptor = _blockDescriptors[index];
                if (descriptor.Bytes >= 0L || descriptor.State != (byte)H8BlockState.Reserved)
                    return;

                int nextGeneration = AdvanceDescriptorGeneration(descriptor.Generation);
                descriptor = default;
                descriptor.Generation = nextGeneration;
                descriptor.State = (byte)H8BlockState.Free;
                _blockDescriptors[index] = descriptor;
            }
            finally
            {
                ReleaseBlockDescriptorMutationGate();
            }
        }

        internal static bool TryCommitReservedBlockDescriptor(int index, in BlockDescriptor descriptor)
        {
            if (!TryEnterBlockDescriptorMutationGate())
                return false;

            try
            {
                if (!_initialized || !_blockDescriptors.IsCreated || (uint)index >= (uint)_blockDescriptors.Length)
                    return false;

                BlockDescriptor current = _blockDescriptors[index];
                if (current.Bytes >= 0L || current.State != (byte)H8BlockState.Reserved)
                    return false;

                BlockDescriptor committed = descriptor;
                int nextGeneration = AdvanceDescriptorGeneration(current.Generation);
                if (committed.Generation < nextGeneration)
                    committed.Generation = nextGeneration;
                _blockDescriptors[index] = committed;
                return true;
            }
            finally
            {
                ReleaseBlockDescriptorMutationGate();
            }
        }

        /// <summary>
        /// Shuts down tracking tables. Only call from service shutdown after users released their buffers.
        /// </summary>
        public static void Shutdown()
        {
            InvokeBeforeShutdownOwnerReleaseHooks();
            UnregisterSceneHooks();
            if (_initialized)
                CompleteAllOwnerJobs();

            GlobalDataVault.DisposeLatestCreatedForNativeMemoryShutdown();
            if (!_initialized)
            {
                DisposeOwnerPointerLists();
                ClearTrackingMemoryBeforeDispose();
                DisposeTrackingContainers();
                ReleaseAliasSafetyHandleIfCreated();
                ResetStaticValueState();
                return;
            }

            for (int i = _recordCount - 1; i >= 0; i--)
            {
                H8AllocationRecord record = _records[i];
                if (!TryFreeRecordPointerForShutdown(in record))
                    RecordBlackBox(record.Owner, H8MemoryTelemetryFlags.Fault);
            }

            _recordCount = 0;
            _totalBytes = 0L;
            RecordBlackBox(SystemID.H8Memory, H8MemoryTelemetryFlags.Shutdown);
            DisposeOwnerPointerLists();
            ClearTrackingMemoryBeforeDispose();
            DisposeTrackingContainers();
            ReleaseAliasSafetyHandleIfCreated();
            ResetStaticValueState();
        }

        private static void AbortInitialize()
        {
            UnregisterSceneHooks();
            DisposeOwnerPointerLists();
            ClearTrackingMemoryBeforeDispose();
            DisposeTrackingContainers();
            ReleaseAliasSafetyHandleIfCreated();
            ResetStaticValueState();
        }

        private static void InvokeBeforeShutdownOwnerReleaseHooks()
        {
            if (_invokingBeforeShutdownOwnerReleaseHook)
                return;

            Action releaseHooks = _beforeShutdownOwnerReleaseHook;
            if (releaseHooks == null)
                return;

            _invokingBeforeShutdownOwnerReleaseHook = true;
            try
            {
                releaseHooks.Invoke();
            }
            finally
            {
                _invokingBeforeShutdownOwnerReleaseHook = false;
            }
        }

        private static void ClearTrackingMemoryBeforeDispose()
        {
            if (_records.IsCreated && _records.Length > 0)
                UnsafeUtility.MemClear(NativeArrayUnsafeUtility.GetUnsafePtr(_records), UnsafeUtility.SizeOf<H8AllocationRecord>() * (long)_records.Length);
            if (_ownerBytes.IsCreated && _ownerBytes.Length > 0)
                UnsafeUtility.MemClear(NativeArrayUnsafeUtility.GetUnsafePtr(_ownerBytes), UnsafeUtility.SizeOf<long>() * (long)_ownerBytes.Length);
            if (_blackBox.IsCreated && _blackBox.Length > 0)
                UnsafeUtility.MemClear(NativeArrayUnsafeUtility.GetUnsafePtr(_blackBox), UnsafeUtility.SizeOf<H8MemoryTelemetryEntry>() * (long)_blackBox.Length);
            if (_eventBlackBox.IsCreated && _eventBlackBox.Length > 0)
                UnsafeUtility.MemClear(NativeArrayUnsafeUtility.GetUnsafePtr(_eventBlackBox), UnsafeUtility.SizeOf<H8MemoryTelemetryEntry>() * (long)_eventBlackBox.Length);
            if (_blockDescriptors.IsCreated)
            {
                for (int i = 0; i < _blockDescriptors.Length; i++)
                    _blockDescriptors[i] = default;
            }
            if (_ownerPointerKeys.IsCreated)
            {
                for (int i = 0; i < _ownerPointerKeys.Length; i++)
                    _ownerPointerKeys[i] = 0;
            }
            if (_ownerJobKeys.IsCreated)
            {
                for (int i = 0; i < _ownerJobKeys.Length; i++)
                    _ownerJobKeys[i] = 0;
            }
        }

        private static void DisposeTrackingContainers()
        {
            if (_allocationOwners.IsCreated)
                _allocationOwners.Dispose();
            if (_allocationRecordIndices.IsCreated)
                _allocationRecordIndices.Dispose();
            if (_ownerPointers.IsCreated)
                _ownerPointers.Dispose();
            if (_ownerJobHandles.IsCreated)
                _ownerJobHandles.Dispose();
            if (_ownerPointerKeys.IsCreated)
                _ownerPointerKeys.Dispose();
            if (_ownerJobKeys.IsCreated)
                _ownerJobKeys.Dispose();
            if (_records.IsCreated)
                _records.Dispose();
            if (_ownerBytes.IsCreated)
                _ownerBytes.Dispose();
            if (_blockDescriptors.IsCreated)
                _blockDescriptors.Dispose();
            if (_blackBox.IsCreated)
                _blackBox.Dispose();
            if (_eventBlackBox.IsCreated)
                _eventBlackBox.Dispose();
        }

        private static void ResetStaticValueState()
        {
            _allocationOwners = default;
            _allocationRecordIndices = default;
            _ownerPointers = default;
            _ownerJobHandles = default;
            _ownerPointerKeys = default;
            _ownerJobKeys = default;
            _records = default;
            _ownerBytes = default;
            _blockDescriptors = default;
            _blackBox = default;
            _eventBlackBox = default;
            _recordCount = 0;
            _totalBytes = 0L;
            _poolCapBytes = LowTierPoolCapBytes;
            _fatalLeakPreventedCount = 0;
            _blackBoxCursor = 0;
            _eventBlackBoxCursor = 0;
            _blackBoxRecordedCount = 0;
            _eventBlackBoxRecordedCount = 0;
            _blackBoxSequence = 0u;
            _eventBlackBoxSequence = 0u;
            _telemetryFrameId = 0u;
            _blockDescriptorMutationGate = 0;
            _allocationGeneration = 1;
            _transitionCutoffGeneration = NoTransitionCutoffGeneration;
            _transitionSequence = 0;
            _lastTransitionReleasedCount = 0;
            _lastTransitionReleasedBytes = 0L;
            _transitionBaselineBytes = 0L;
            _transitionExpectedBytes = 0L;
            _lastTransitionBaselineVerified = true;
            _deferSceneUnloadedVerificationToRuntime = false;
            _sceneHooksRegistered = false;
            _initialized = false;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            _aliasSafetyHandle = default;
            _aliasSafetyHandleCreated = false;
            // Every caller of ResetStaticValueState runs ReleaseAliasSafetyHandleIfCreated first, so dropping
            // the table references here can never orphan a live handle.
            _aliasRegionKeys = null;
            _aliasRegionHandles = null;
            _aliasRegionHandleCreated = null;
            _aliasRegionMutationGate = 0;
            _aliasRegionOwnerThreadId = 0;
            _aliasRegionLiveCount = 0;
            _aliasRegionExhaustionReported = false;
#endif
        }

        private static void ReleaseAliasSafetyHandleIfCreated()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Close the per-region lane first: no other thread can enter TryResolveAliasRegionHandle once the
            // owner-thread id is cleared, so the sweep below cannot race a create.
            _aliasRegionOwnerThreadId = 0;
            if (_aliasRegionKeys != null && _aliasRegionHandles != null && _aliasRegionHandleCreated != null)
            {
                for (int i = 0; i < _aliasRegionHandleCreated.Length; i++)
                    ReleaseAliasRegionSlot(i);
            }

            _aliasRegionLiveCount = 0;
            _aliasRegionExhaustionReported = false;
            _aliasRegionMutationGate = 0;

            if (_aliasSafetyHandleCreated)
            {
                AtomicSafetyHandle.Release(_aliasSafetyHandle);
                _aliasSafetyHandle = default;
                _aliasSafetyHandleCreated = false;
            }
#endif
        }

        private static void RegisterSceneHooks()
        {
            if (_sceneHooksRegistered)
                return;

            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            _sceneHooksRegistered = true;
        }

        private static void UnregisterSceneHooks()
        {
            if (!_sceneHooksRegistered)
                return;

            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            _sceneHooksRegistered = false;
        }

        private static void HandleSceneUnloaded(Scene scene)
        {
            if (_deferSceneUnloadedVerificationToRuntime)
                return;

            CompleteSceneTransitionVerification();
        }

        private static int ReleaseAll(SystemID owner, int generationCutoff, bool writeBlackBox)
        {
            if (!_initialized || owner == SystemID.Unknown)
                return 0;

            CompleteOwnerJobs(owner);
            ushort ownerKey = GetOwnerKey(owner);
            if (!_ownerPointers.IsCreated ||
                !_ownerPointers.TryGetValue(ownerKey, out NativeList<IntPtr> pointers))
            {
                return 0;
            }

            if (!pointers.IsCreated || pointers.Length == 0)
            {
                RemoveOwnerPointerLane(ownerKey, ref pointers);
                return 0;
            }

            int releasedCount = 0;
            long releasedBytes = 0L;
            for (int pointerIndex = pointers.Length - 1; pointerIndex >= 0; pointerIndex--)
            {
                IntPtr pointer = pointers[pointerIndex];
                if (pointer == IntPtr.Zero)
                {
                    pointers.RemoveAtSwapBack(pointerIndex);
                    continue;
                }

                if (!TryFindRecordIndex(pointer, out int recordIndex))
                {
                    pointers.RemoveAtSwapBack(pointerIndex);
                    continue;
                }

                H8AllocationRecord record = _records[recordIndex];
                if (record.Owner != owner)
                {
                    pointers.RemoveAtSwapBack(pointerIndex);
                    continue;
                }

                if (generationCutoff != int.MaxValue && record.Generation > generationCutoff)
                    continue;

                if (ForceFreeRecordAt(recordIndex, removeOwnerPointer: false, out H8AllocationRecord releasedRecord))
                {
                    releasedCount++;
                    releasedBytes += releasedRecord.Bytes;
                }

                pointers.RemoveAtSwapBack(pointerIndex);
            }

            if (pointers.Length == 0)
            {
                RemoveOwnerPointerLane(ownerKey, ref pointers);
            }
            else
            {
                _ownerPointers[ownerKey] = pointers;
            }

            if (releasedCount <= 0)
                return 0;

            _fatalLeakPreventedCount += releasedCount;
            if (writeBlackBox)
                WriteFatalLeakBlackBox(owner, releasedCount, releasedBytes, baselineMismatch: false);

            return releasedCount;
        }

        private static int ReleaseSceneTransitionLeaks()
        {
            int cutoffGeneration = _transitionCutoffGeneration;
            if (cutoffGeneration == NoTransitionCutoffGeneration)
                return 0;

            CompleteSceneTransitionOwnerJobs();
            int releasedCount = 0;
            long releasedBytes = 0L;
            for (int index = _recordCount - 1; index >= 0; index--)
            {
                H8AllocationRecord record = _records[index];
                if (!IsSceneTransitionRecord(in record, cutoffGeneration))
                    continue;

                if (!ForceFreeRecordAt(index, removeOwnerPointer: true, out H8AllocationRecord releasedRecord))
                    continue;

                releasedCount++;
                releasedBytes += releasedRecord.Bytes;
            }

            _lastTransitionReleasedCount = releasedCount;
            _lastTransitionReleasedBytes = releasedBytes;
            if (releasedCount <= 0)
                return 0;

            _fatalLeakPreventedCount += releasedCount;
            WriteFatalLeakBlackBox(SystemID.Unknown, releasedCount, releasedBytes, baselineMismatch: false);
            return releasedCount;
        }

        private static void CompleteOwnerJobs(SystemID owner)
        {
            if (!_ownerJobHandles.IsCreated || owner == SystemID.Unknown)
                return;

            ushort ownerKey = GetOwnerKey(owner);
            if (!_ownerJobHandles.TryGetValue(ownerKey, out JobHandle ownerHandle))
                return;

            // [BLOCKING_SYNC_POINT] Scene transition and owner teardown may wait; gameplay Tick paths may not call this.
            TryCompleteOwnerJobHandle(ref ownerHandle);
            _ownerJobHandles.Remove(ownerKey);
            RemoveOwnerJobKey(ownerKey);
        }

        private static void CompleteAllOwnerJobs()
        {
            if (!_ownerJobHandles.IsCreated || !_ownerJobKeys.IsCreated)
                return;

            for (int i = 0; i < _ownerJobKeys.Length; i++)
            {
                ushort ownerKey = _ownerJobKeys[i];
                if (!_ownerJobHandles.TryGetValue(ownerKey, out JobHandle ownerHandle))
                    continue;

                // [BLOCKING_SYNC_POINT] Shutdown may wait; gameplay Tick paths may not call this.
                TryCompleteOwnerJobHandle(ref ownerHandle);
                _ownerJobHandles.Remove(ownerKey);
            }

            _ownerJobKeys.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TryCompleteOwnerJobHandle(ref JobHandle ownerHandle)
        {
            Hecton8.Core.DispatcherJobFence.TryComplete(ref ownerHandle, forceComplete: true);
        }

        private static void RemoveOwnerJobKey(ushort ownerKey)
        {
            if (!_ownerJobKeys.IsCreated)
                return;

            for (int i = _ownerJobKeys.Length - 1; i >= 0; i--)
            {
                if (_ownerJobKeys[i] != ownerKey)
                    continue;

                _ownerJobKeys.RemoveAtSwapBack(i);
            }
        }

        private static bool EnsureOwnerJobKey(ushort ownerKey)
        {
            if (!_ownerJobKeys.IsCreated)
                return false;

            for (int i = 0; i < _ownerJobKeys.Length; i++)
            {
                if (_ownerJobKeys[i] == ownerKey)
                    return true;
            }

            try
            {
                _ownerJobKeys.Add(ownerKey);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void RemoveOwnerPointerKey(ushort ownerKey)
        {
            if (!_ownerPointerKeys.IsCreated)
                return;

            for (int i = _ownerPointerKeys.Length - 1; i >= 0; i--)
            {
                if (_ownerPointerKeys[i] != ownerKey)
                    continue;

                _ownerPointerKeys.RemoveAtSwapBack(i);
            }
        }

        private static bool AddOwnerPointerKey(ushort ownerKey)
        {
            if (!_ownerPointerKeys.IsCreated)
                return false;

            for (int i = 0; i < _ownerPointerKeys.Length; i++)
            {
                if (_ownerPointerKeys[i] == ownerKey)
                    return false;
            }

            _ownerPointerKeys.Add(ownerKey);
            return true;
        }

        private static void RemoveOwnerPointerLane(ushort ownerKey, ref NativeList<IntPtr> pointers)
        {
            if (pointers.IsCreated)
                pointers.Dispose();

            if (_ownerPointers.IsCreated)
                _ownerPointers.Remove(ownerKey);

            RemoveOwnerPointerKey(ownerKey);
            pointers = default;
        }

        private static void CompleteSceneTransitionOwnerJobs()
        {
            if (_ownerPointerKeys.IsCreated)
            {
                for (int i = 0; i < _ownerPointerKeys.Length; i++)
                {
                    SystemID owner = (SystemID)_ownerPointerKeys[i];
                    if (IsSceneTransitionOwner(owner))
                        CompleteOwnerJobs(owner);
                }
            }

            if (!_ownerJobKeys.IsCreated)
                return;

            for (int i = _ownerJobKeys.Length - 1; i >= 0; i--)
            {
                SystemID owner = (SystemID)_ownerJobKeys[i];
                if (IsSceneTransitionOwner(owner))
                    CompleteOwnerJobs(owner);
            }
        }

        private static long ComputeSceneTransitionBaselineBytes(int cutoffGeneration)
        {
            long releasableBytes = 0L;
            for (int i = 0; i < _recordCount; i++)
            {
                H8AllocationRecord record = _records[i];
                if (IsSceneTransitionRecord(in record, cutoffGeneration))
                    releasableBytes += record.Bytes;
            }

            long baseline = _totalBytes - releasableBytes;
            return baseline > 0L ? baseline : 0L;
        }

        private static long ComputeSceneTransitionExpectedBytes(int cutoffGeneration)
        {
            long postCutoffBytes = 0L;
            for (int i = 0; i < _recordCount; i++)
            {
                H8AllocationRecord record = _records[i];
                if (record.Pointer != IntPtr.Zero && record.Generation > cutoffGeneration)
                    postCutoffBytes += record.Bytes;
            }

            long expectedBytes = _transitionBaselineBytes + postCutoffBytes;
            return expectedBytes > 0L ? expectedBytes : 0L;
        }

        private static bool IsSceneTransitionRecord(in H8AllocationRecord record, int cutoffGeneration)
        {
            return record.Pointer != IntPtr.Zero &&
                   record.Generation <= cutoffGeneration &&
                   IsSceneTransitionOwner(record.Owner);
        }

        private static bool IsSceneTransitionOwner(SystemID owner)
        {
            switch (owner)
            {
                case SystemID.Unknown:
                case SystemID.CoreDataVault:
                case SystemID.H8Memory:
                case SystemID.Bootstrap:
                case SystemID.CoreDeterminism:
                case SystemID.SystemDispatcher:
                case SystemID.HardwareHomeostasis:
                case SystemID.GlobalPhysicsStateManager:
                case SystemID.Physics:
                    return false;
                default:
                    return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort GetOwnerKey(SystemID owner)
        {
            return (ushort)owner;
        }

        private static bool ForceFreeRecordAt(int index, bool removeOwnerPointer, out H8AllocationRecord releasedRecord)
        {
            releasedRecord = default;
            if ((uint)index >= (uint)_recordCount)
                return false;

            H8AllocationRecord record = _records[index];
            if (record.Pointer == IntPtr.Zero)
            {
                RemoveRecordAt(index, removeOwnerPointer, H8MemoryTelemetryFlags.ForcedRelease | H8MemoryTelemetryFlags.Fault);
                return false;
            }

            releasedRecord = record;
            try
            {
                UnsafeUtility.Free(record.Pointer.ToPointer(), record.Allocator);
            }
            catch
            {
                RecordBlackBox(record.Owner, H8MemoryTelemetryFlags.ForcedRelease | H8MemoryTelemetryFlags.Fault);
                return false;
            }

            RemoveRecordAt(index, removeOwnerPointer, H8MemoryTelemetryFlags.ForcedRelease);
            return true;
        }

        private static bool TryFreeRecordPointerForShutdown(in H8AllocationRecord record)
        {
            if (record.Pointer == IntPtr.Zero)
                return true;

            try
            {
                UnsafeUtility.Free(record.Pointer.ToPointer(), record.Allocator);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryFreeUntrackedRawPointer(void* pointer, Allocator allocator, SystemID owner)
        {
            if (pointer == null)
                return true;

            try
            {
                UnsafeUtility.Free(pointer, allocator);
                return true;
            }
            catch
            {
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                return false;
            }
        }

        private static bool RegisterOwnerPointer(SystemID owner, IntPtr pointer)
        {
            if (owner == SystemID.Unknown || pointer == IntPtr.Zero || !_ownerPointers.IsCreated)
                return false;

            ushort ownerKey = GetOwnerKey(owner);
            bool createdLane = false;
            if (!_ownerPointers.TryGetValue(ownerKey, out NativeList<IntPtr> pointers))
            {
                try
                {
                    // COLD ALLOC: NativeList<IntPtr>[16] - owner pointer lane for ReleaseAll(SystemID) - owner: H8Memory
                    pointers = new NativeList<IntPtr>(DefaultOwnerPointerCapacity, Allocator.Persistent);
                }
                catch
                {
                    return false;
                }

                if (!_ownerPointers.TryAdd(ownerKey, pointers))
                {
                    pointers.Dispose();
                    return false;
                }

                createdLane = true;
            }

            bool addedKey = false;
            try
            {
                addedKey = AddOwnerPointerKey(ownerKey);
                pointers.Add(pointer);
                _ownerPointers[ownerKey] = pointers;
                return true;
            }
            catch
            {
                if (createdLane)
                {
                    RemoveOwnerPointerLane(ownerKey, ref pointers);
                }
                else if (addedKey)
                {
                    RemoveOwnerPointerKey(ownerKey);
                }

                return false;
            }
        }

        private static void RemoveOwnerPointer(SystemID owner, IntPtr pointer)
        {
            ushort ownerKey = GetOwnerKey(owner);
            if (owner == SystemID.Unknown ||
                pointer == IntPtr.Zero ||
                !_ownerPointers.IsCreated ||
                !_ownerPointers.TryGetValue(ownerKey, out NativeList<IntPtr> pointers) ||
                !pointers.IsCreated)
            {
                return;
            }

            bool removed = false;
            for (int i = pointers.Length - 1; i >= 0; i--)
            {
                if (pointers[i] != pointer)
                    continue;

                pointers.RemoveAtSwapBack(i);
                removed = true;
            }

            if (!removed)
                return;

            if (pointers.Length == 0)
            {
                RemoveOwnerPointerLane(ownerKey, ref pointers);
            }
            else
            {
                _ownerPointers[ownerKey] = pointers;
            }
        }

        private static void DisposeOwnerPointerLists()
        {
            if (!_ownerPointerKeys.IsCreated || !_ownerPointers.IsCreated)
                return;

            for (int i = 0; i < _ownerPointerKeys.Length; i++)
            {
                ushort ownerKey = _ownerPointerKeys[i];
                if (!_ownerPointers.TryGetValue(ownerKey, out NativeList<IntPtr> pointers) || !pointers.IsCreated)
                    continue;

                pointers.Dispose();
                _ownerPointers.Remove(ownerKey);
            }
        }

        private static bool TryFindRecordIndex(IntPtr pointer, out int index)
        {
            index = -1;
            if (pointer == IntPtr.Zero)
                return false;

            long pointerKey = pointer.ToInt64();
            if (_allocationRecordIndices.IsCreated &&
                _allocationRecordIndices.TryGetValue(pointerKey, out int mappedIndex) &&
                (uint)mappedIndex < (uint)_recordCount &&
                _records[mappedIndex].Pointer.ToInt64() == pointerKey)
            {
                index = mappedIndex;
                return true;
            }

            for (int i = _recordCount - 1; i >= 0; i--)
            {
                if (_records[i].Pointer.ToInt64() != pointerKey)
                    continue;

                index = i;
                if (_allocationRecordIndices.IsCreated)
                    _allocationRecordIndices[pointerKey] = i;
                return true;
            }

            return false;
        }

        private static void WriteFatalLeakBlackBox(SystemID owner, int releaseCount, long releasedBytes, bool baselineMismatch)
        {
            H8MemoryTelemetryFlags flags = H8MemoryTelemetryFlags.Fault;
            if (releaseCount > 0)
                flags |= H8MemoryTelemetryFlags.ForcedRelease;
            if (baselineMismatch)
                flags |= H8MemoryTelemetryFlags.BaselineMismatch;
            RecordBlackBox(owner, flags);

            string path = BuildAgentDumpPath(AgentDumpFileName);
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                WriteFatalLeakBlackBoxFile(path, owner, releaseCount, releasedBytes, baselineMismatch);

                string h8Path = BuildAgentDumpPath(AgentH8DumpFileName);
                if (!string.IsNullOrEmpty(h8Path))
                    WriteFatalLeakBlackBoxFile(h8Path, owner, releaseCount, releasedBytes, baselineMismatch);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void WriteFatalLeakBlackBoxFile(
            string path,
            SystemID owner,
            int releaseCount,
            long releasedBytes,
            bool baselineMismatch)
        {
            int heartbeatCount = ClampBlackBoxRecordedCount(_blackBox, _blackBoxRecordedCount);
            int eventCount = ClampBlackBoxRecordedCount(_eventBlackBox, _eventBlackBoxRecordedCount);
            int dumpCount = _recordCount < 0 ? 0 : _recordCount < 300 ? _recordCount : 300;
            int byteCount =
                23 +
                70 +
                GetBlackBoxRingByteCount(heartbeatCount) +
                GetBlackBoxRingByteCount(eventCount) +
                sizeof(int) +
                sizeof(int) +
                (dumpCount * H8AllocationRecordSizeBytes);
            NativeArray<byte> payload = Hecton8.Core.NativeFaultDumpWriter.CreateTransientPayload(
                byteCount,
                nameof(H8Memory),
                "h8MemoryFatalLeakDumpPayload");
            try
            {
                int cursor = 0;
                WriteFatalLeakMarker(payload, ref cursor);
                WriteUInt64LittleEndian(payload, ref cursor, FatalLeakDumpMagic);
                WriteInt32LittleEndian(payload, ref cursor, FatalLeakDumpVersion);
                WriteInt32LittleEndian(payload, ref cursor, H8MemoryTelemetryEntrySizeBytes);
                WriteInt32LittleEndian(payload, ref cursor, H8AllocationRecordSizeBytes);
                WriteInt32LittleEndian(payload, ref cursor, BlackBoxFrameCount);
                WriteUInt16LittleEndian(payload, ref cursor, (ushort)owner);
                WriteInt32LittleEndian(payload, ref cursor, _transitionSequence);
                WriteInt32LittleEndian(payload, ref cursor, releaseCount);
                WriteInt64LittleEndian(payload, ref cursor, releasedBytes);
                WriteInt64LittleEndian(payload, ref cursor, _totalBytes);
                WriteInt64LittleEndian(payload, ref cursor, _transitionBaselineBytes);
                WriteInt64LittleEndian(payload, ref cursor, _transitionExpectedBytes);
                WriteInt32LittleEndian(payload, ref cursor, baselineMismatch ? 1 : 0);
                WriteBlackBoxEntries(payload, ref cursor, heartbeatCount, eventCount);
                WriteInt32LittleEndian(payload, ref cursor, _recordCount);
                WriteInt32LittleEndian(payload, ref cursor, dumpCount);
                for (int i = 0; i < dumpCount; i++)
                {
                    H8AllocationRecord record = _records[i];
                    WriteUInt64LittleEndian(payload, ref cursor, ComputeAllocationAddressFingerprint(in record));
                    WriteInt64LittleEndian(payload, ref cursor, record.Bytes);
                    WriteInt32LittleEndian(payload, ref cursor, record.Length);
                    WriteInt32LittleEndian(payload, ref cursor, record.Stride);
                    WriteInt32LittleEndian(payload, ref cursor, record.Alignment);
                    WriteInt32LittleEndian(payload, ref cursor, record.AllocationIndex);
                    WriteInt32LittleEndian(payload, ref cursor, record.Generation);
                    WriteInt32LittleEndian(payload, ref cursor, (int)record.Allocator);
                    WriteUInt16LittleEndian(payload, ref cursor, (ushort)record.Owner);
                    WriteUInt16LittleEndian(payload, ref cursor, record.Flags);
                    WriteUInt16LittleEndian(payload, ref cursor, record.Reserved);
                    WriteUInt16LittleEndian(payload, ref cursor, record.Reserved2);
                }

                Hecton8.Core.NativeFaultDumpWriter.TryWriteAll(path, payload, cursor);
            }
            finally
            {
                Hecton8.Core.NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(H8Memory),
                    "h8MemoryFatalLeakDumpPayload");
            }
        }

        private static void WriteFatalLeakMarker(NativeArray<byte> target, ref int cursor)
        {
            target[cursor++] = 22;
            target[cursor++] = (byte)'[';
            target[cursor++] = (byte)'F';
            target[cursor++] = (byte)'A';
            target[cursor++] = (byte)'T';
            target[cursor++] = (byte)'A';
            target[cursor++] = (byte)'L';
            target[cursor++] = (byte)' ';
            target[cursor++] = (byte)'L';
            target[cursor++] = (byte)'E';
            target[cursor++] = (byte)'A';
            target[cursor++] = (byte)'K';
            target[cursor++] = (byte)':';
            target[cursor++] = (byte)' ';
            target[cursor++] = (byte)'S';
            target[cursor++] = (byte)'y';
            target[cursor++] = (byte)'s';
            target[cursor++] = (byte)'t';
            target[cursor++] = (byte)'e';
            target[cursor++] = (byte)'m';
            target[cursor++] = (byte)'I';
            target[cursor++] = (byte)'D';
            target[cursor++] = (byte)']';
        }

        private static void WriteUInt16LittleEndian(NativeArray<byte> target, ref int cursor, ushort value)
        {
            target[cursor++] = (byte)value;
            target[cursor++] = (byte)(value >> 8);
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> target, ref int cursor, int value)
        {
            WriteUInt32LittleEndian(target, ref cursor, unchecked((uint)value));
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> target, ref int cursor, uint value)
        {
            target[cursor++] = (byte)value;
            target[cursor++] = (byte)(value >> 8);
            target[cursor++] = (byte)(value >> 16);
            target[cursor++] = (byte)(value >> 24);
        }

        private static void WriteInt64LittleEndian(NativeArray<byte> target, ref int cursor, long value)
        {
            WriteUInt64LittleEndian(target, ref cursor, unchecked((ulong)value));
        }

        private static void WriteUInt64LittleEndian(NativeArray<byte> target, ref int cursor, ulong value)
        {
            target[cursor++] = (byte)value;
            target[cursor++] = (byte)(value >> 8);
            target[cursor++] = (byte)(value >> 16);
            target[cursor++] = (byte)(value >> 24);
            target[cursor++] = (byte)(value >> 32);
            target[cursor++] = (byte)(value >> 40);
            target[cursor++] = (byte)(value >> 48);
            target[cursor++] = (byte)(value >> 56);
        }

        private static void RecordBlackBox(SystemID owner, H8MemoryTelemetryFlags flags)
        {
            if ((flags & H8MemoryTelemetryFlags.Heartbeat) != 0)
                RecordFrameHeartbeat(owner, flags);
            else
                RecordLifecycleEvent(owner, flags);
        }

        private static void RecordFrameHeartbeat(SystemID owner, H8MemoryTelemetryFlags flags)
        {
            if (!_blackBox.IsCreated || _blackBox.Length == 0)
                return;

            int cursor = _blackBoxCursor;
            if ((uint)cursor >= (uint)_blackBox.Length)
                cursor = 0;

            H8MemoryTelemetryEntry entry = BuildTelemetryEntry(owner, flags, ++_blackBoxSequence);
            _blackBox[cursor] = entry;

            cursor++;
            if (cursor >= _blackBox.Length)
                cursor = 0;
            _blackBoxCursor = cursor;
            if (_blackBoxRecordedCount < _blackBox.Length)
                _blackBoxRecordedCount++;
        }

        private static void RecordLifecycleEvent(SystemID owner, H8MemoryTelemetryFlags flags)
        {
            if (!_eventBlackBox.IsCreated || _eventBlackBox.Length == 0)
                return;

            int cursor = _eventBlackBoxCursor;
            if ((uint)cursor >= (uint)_eventBlackBox.Length)
                cursor = 0;

            H8MemoryTelemetryEntry entry = BuildTelemetryEntry(owner, flags, ++_eventBlackBoxSequence);
            _eventBlackBox[cursor] = entry;

            cursor++;
            if (cursor >= _eventBlackBox.Length)
                cursor = 0;
            _eventBlackBoxCursor = cursor;
            if (_eventBlackBoxRecordedCount < _eventBlackBox.Length)
                _eventBlackBoxRecordedCount++;
        }

        private static H8MemoryTelemetryEntry BuildTelemetryEntry(SystemID owner, H8MemoryTelemetryFlags flags, uint sequence)
        {
            H8MemoryTelemetryEntry entry = default;
            entry.TotalBytes = _totalBytes;
            entry.TransitionBaselineBytes = _transitionBaselineBytes;
            entry.LastTransitionReleasedBytes = _lastTransitionReleasedBytes;
            entry.Sequence = sequence;
            entry.ActiveAllocationCount = _recordCount;
            entry.BlockDescriptorCount = _blockDescriptors.IsCreated ? _blockDescriptors.Length : 0;
            entry.AllocationGeneration = _allocationGeneration;
            entry.TransitionCutoffGeneration = _transitionCutoffGeneration;
            entry.TransitionSequence = _transitionSequence;
            entry.LastTransitionReleasedCount = _lastTransitionReleasedCount;
            entry.FatalLeakPreventedCount = _fatalLeakPreventedCount;
            entry.Frame = ResolveTelemetryFrame(sequence);
            entry.Owner = (ushort)owner;
            entry.Flags = (ushort)flags;
            return entry;
        }

        private static void WriteBlackBoxEntries(NativeArray<byte> target, ref int cursor, int heartbeatCount, int eventCount)
        {
            WriteBlackBoxRing(target, ref cursor, BlackBoxRingKindHeartbeat, _blackBox, heartbeatCount, _blackBoxCursor);
            WriteBlackBoxRing(target, ref cursor, BlackBoxRingKindLifecycleEvent, _eventBlackBox, eventCount, _eventBlackBoxCursor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ComputeAllocationAddressFingerprint(in H8AllocationRecord record)
        {
            ulong hash = AddressFingerprintSeed;
            hash = MixAddressFingerprint(hash, unchecked((ulong)record.Pointer.ToInt64()));
            hash = MixAddressFingerprint(hash, unchecked((ulong)record.Bytes));
            hash = MixAddressFingerprint(hash, (uint)record.AllocationIndex);
            hash = MixAddressFingerprint(hash, (uint)record.Generation);
            hash = MixAddressFingerprint(hash, (ushort)record.Owner);
            hash = MixAddressFingerprint(hash, (ushort)record.Flags);
            return hash != 0UL ? hash : 1UL;
        }

        private static bool CanCopyReplaySnapshotSource(in H8AllocationRecord record, uint excludedOwnerHash)
        {
            if (record.Pointer == IntPtr.Zero || record.Bytes <= 0L)
                return false;

            uint ownerHash = ComputeReplayOwnerHash(record.Owner);
            return excludedOwnerHash == 0u || ownerHash != excludedOwnerHash;
        }

        private static uint ComputeReplayOwnerHash(SystemID owner)
        {
            uint hash = ReplaySnapshotHashSeed;
            hash = MixReplaySnapshotHash(hash, 0x48384F57u); // H8OW
            hash = MixReplaySnapshotHash(hash, (ushort)owner);
            return hash != 0u ? hash : 1u;
        }

        private static uint ComputeReplayLabelHash(in H8AllocationRecord record)
        {
            ulong fingerprint = ComputeAllocationAddressFingerprint(in record);
            uint hash = ReplaySnapshotHashSeed;
            hash = MixReplaySnapshotHash(hash, 0x4838414Cu); // H8AL
            hash = MixReplaySnapshotHash(hash, unchecked((uint)fingerprint));
            hash = MixReplaySnapshotHash(hash, unchecked((uint)(fingerprint >> 32)));
            hash = MixReplaySnapshotHash(hash, unchecked((uint)record.Length));
            hash = MixReplaySnapshotHash(hash, unchecked((uint)record.Stride));
            hash = MixReplaySnapshotHash(hash, record.Flags);
            return hash != 0u ? hash : 1u;
        }

        private static uint MixReplaySnapshotHash(uint hash, uint value)
        {
            return unchecked((hash ^ value) * ReplaySnapshotHashPrime);
        }

        private static byte ResolveReplayLifetime(Allocator allocator)
        {
            switch (allocator)
            {
                case Allocator.Temp:
                    return (byte)Hecton8.Core.NativeAllocationLifetime.Temp;
                case Allocator.TempJob:
                    return (byte)Hecton8.Core.NativeAllocationLifetime.TempJob;
                case Allocator.Persistent:
                    return (byte)Hecton8.Core.NativeAllocationLifetime.Session;
                default:
                    return (byte)Hecton8.Core.NativeAllocationLifetime.Session;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong MixAddressFingerprint(ulong hash, ulong value)
        {
            return unchecked((hash ^ value) * AddressFingerprintPrime);
        }

        private static int ClampBlackBoxRecordedCount(NativeArray<H8MemoryTelemetryEntry> ring, int recordedCount)
        {
            if (!ring.IsCreated || ring.Length == 0)
                return 0;

            if (recordedCount < 0)
                return 0;

            return recordedCount > ring.Length ? ring.Length : recordedCount;
        }

        private static int GetBlackBoxRingByteCount(int recordedCount)
        {
            return 1 + sizeof(int) + sizeof(int) + sizeof(int) + (recordedCount * H8MemoryTelemetryEntrySizeBytes);
        }

        private static void WriteBlackBoxRing(
            NativeArray<byte> target,
            ref int writeCursor,
            byte ringKind,
            NativeArray<H8MemoryTelemetryEntry> ring,
            int recordedCount,
            int ringCursor)
        {
            target[writeCursor++] = ringKind;
            WriteInt32LittleEndian(target, ref writeCursor, ring.IsCreated ? ring.Length : 0);
            WriteInt32LittleEndian(target, ref writeCursor, H8MemoryTelemetryEntrySizeBytes);

            if (!ring.IsCreated || ring.Length == 0)
            {
                WriteInt32LittleEndian(target, ref writeCursor, 0);
                return;
            }

            if (recordedCount < 0)
                recordedCount = 0;
            if (recordedCount > ring.Length)
                recordedCount = ring.Length;
            WriteInt32LittleEndian(target, ref writeCursor, recordedCount);

            int start = recordedCount < ring.Length ? 0 : ringCursor;
            for (int i = 0; i < recordedCount; i++)
            {
                int index = start + i;
                if (index >= ring.Length)
                    index -= ring.Length;

                H8MemoryTelemetryEntry entry = ring[index];
                WriteInt64LittleEndian(target, ref writeCursor, entry.TotalBytes);
                WriteInt64LittleEndian(target, ref writeCursor, entry.TransitionBaselineBytes);
                WriteInt64LittleEndian(target, ref writeCursor, entry.LastTransitionReleasedBytes);
                WriteUInt32LittleEndian(target, ref writeCursor, entry.Sequence);
                WriteInt32LittleEndian(target, ref writeCursor, entry.ActiveAllocationCount);
                WriteInt32LittleEndian(target, ref writeCursor, entry.BlockDescriptorCount);
                WriteInt32LittleEndian(target, ref writeCursor, entry.AllocationGeneration);
                WriteInt32LittleEndian(target, ref writeCursor, entry.TransitionCutoffGeneration);
                WriteInt32LittleEndian(target, ref writeCursor, entry.TransitionSequence);
                WriteInt32LittleEndian(target, ref writeCursor, entry.LastTransitionReleasedCount);
                WriteInt32LittleEndian(target, ref writeCursor, entry.FatalLeakPreventedCount);
                WriteUInt32LittleEndian(target, ref writeCursor, entry.Frame);
                WriteUInt16LittleEndian(target, ref writeCursor, entry.Owner);
                WriteUInt16LittleEndian(target, ref writeCursor, entry.Flags);
            }
        }

        private static string BuildAgentDumpPath(string fileName)
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            if (string.IsNullOrEmpty(currentDirectory))
                return null;

            string projectRoot = Path.GetFileName(currentDirectory) == "Hecton8"
                ? currentDirectory
                : Path.Combine(currentDirectory, "Hecton8");
            return Path.Combine(projectRoot, "Docs", "AgentLogs", fileName);
        }

        private static bool TryReserveBytes(SystemID owner, long bytes)
        {
            if (bytes <= 0L)
                return false;

            if (_poolCapBytes > 0L && bytes > _poolCapBytes - _totalBytes)
                return false;

            return true;
        }

        private static int ResolveTrackingCapacity(int capacity)
        {
            if (capacity <= 0)
                return DefaultCapacity;

            return capacity > MaxTrackingCapacity ? MaxTrackingCapacity : capacity;
        }

        private static bool TryReserveReplacementBytes(long oldBytes, long newBytes)
        {
            if (newBytes <= 0L)
                return false;

            if (_poolCapBytes <= 0L)
                return true;

            long retainedBytes = _totalBytes > oldBytes ? _totalBytes - oldBytes : 0L;
            return newBytes <= _poolCapBytes - retainedBytes;
        }

        private static bool EnsureTrackingCapacity()
        {
            if (_recordCount < _records.Length)
                return true;

            int oldCapacity = _records.Length;
            if (oldCapacity >= MaxTrackingCapacity)
                return false;

            int newCapacity = oldCapacity > 0 ? oldCapacity << 1 : DefaultCapacity;
            if (newCapacity < oldCapacity || newCapacity > MaxTrackingCapacity)
                newCapacity = MaxTrackingCapacity;

            NativeArray<H8AllocationRecord> newRecords = default;
            NativeParallelHashMap<long, SystemID> newOwners = default;
            NativeParallelHashMap<long, int> newIndices = default;

            try
            {
                newRecords = new NativeArray<H8AllocationRecord>(newCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                newOwners = new NativeParallelHashMap<long, SystemID>(newCapacity, Allocator.Persistent);
                newIndices = new NativeParallelHashMap<long, int>(newCapacity, Allocator.Persistent);

                for (int i = 0; i < _recordCount; i++)
                {
                    H8AllocationRecord record = _records[i];
                    newRecords[i] = record;
                    if (record.Pointer == IntPtr.Zero)
                        continue;

                    long pointerKey = record.Pointer.ToInt64();
                    if (newOwners.TryAdd(pointerKey, record.Owner) && newIndices.TryAdd(pointerKey, i))
                        continue;

                    DisposeTrackingCapacityScratch(ref newRecords, ref newOwners, ref newIndices);
                    return false;
                }

                if (!TryEnsureBlockDescriptorCapacity(newCapacity))
                {
                    DisposeTrackingCapacityScratch(ref newRecords, ref newOwners, ref newIndices);
                    return false;
                }
            }
            catch
            {
                DisposeTrackingCapacityScratch(ref newRecords, ref newOwners, ref newIndices);
                return false;
            }

            if (_records.IsCreated)
                _records.Dispose();
            if (_allocationOwners.IsCreated)
                _allocationOwners.Dispose();
            if (_allocationRecordIndices.IsCreated)
                _allocationRecordIndices.Dispose();

            _records = newRecords;
            _allocationOwners = newOwners;
            _allocationRecordIndices = newIndices;
            return true;
        }

        private static void DisposeTrackingCapacityScratch(
            ref NativeArray<H8AllocationRecord> records,
            ref NativeParallelHashMap<long, SystemID> owners,
            ref NativeParallelHashMap<long, int> indices)
        {
            if (records.IsCreated)
                records.Dispose();
            if (owners.IsCreated)
                owners.Dispose();
            if (indices.IsCreated)
                indices.Dispose();

            records = default;
            owners = default;
            indices = default;
        }

        private static bool TryEnsureBlockDescriptorCapacity(int requiredCapacity)
        {
            if (!TryEnterBlockDescriptorMutationGate())
                return false;

            try
            {
                return TryEnsureBlockDescriptorCapacityNoLock(requiredCapacity);
            }
            finally
            {
                ReleaseBlockDescriptorMutationGate();
            }
        }

        private static bool RegisterPointer(
            void* pointer,
            long bytes,
            int length,
            int stride,
            int alignment,
            SystemID owner,
            Allocator allocator,
            H8AllocationFlags flags,
            int generationOverride = 0)
        {
            if (pointer == null || bytes <= 0L || _recordCount >= _records.Length)
                return false;

            IntPtr pointerValue = (IntPtr)pointer;
            long pointerKey = pointerValue.ToInt64();
            int recordIndex = _recordCount;
            try
            {
                if (!_allocationOwners.TryAdd(pointerKey, owner))
                    return false;
            }
            catch
            {
                return false;
            }

            bool addedRecordIndex = false;
            try
            {
                addedRecordIndex = _allocationRecordIndices.TryAdd(pointerKey, recordIndex);
            }
            catch
            {
                addedRecordIndex = false;
            }

            if (!addedRecordIndex)
            {
                _allocationOwners.Remove(pointerKey);
                return false;
            }

            if (!RegisterOwnerPointer(owner, pointerValue))
            {
                _allocationOwners.Remove(pointerKey);
                _allocationRecordIndices.Remove(pointerKey);
                return false;
            }

            H8AllocationRecord record = default;
            record.Pointer = pointerValue;
            record.Bytes = bytes;
            record.Length = length;
            record.Stride = stride;
            record.Alignment = alignment;
            record.AllocationIndex = recordIndex;
            record.Generation = generationOverride > 0 ? generationOverride : _allocationGeneration;
            record.Owner = owner;
            record.Allocator = allocator;
            record.Flags = (ushort)flags;

            _records[_recordCount++] = record;
            _totalBytes += bytes;
            int ownerIndex = (int)owner;
            if ((uint)ownerIndex < (uint)_ownerBytes.Length)
                _ownerBytes[ownerIndex] += bytes;

            if ((flags & H8AllocationFlags.SubAllocatorRoot) != 0)
            {
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Allocated);
                return true;
            }

            BlockDescriptor blockDescriptor = default;
            blockDescriptor.BasePointer = pointerValue;
            blockDescriptor.OffsetBytes = 0L;
            blockDescriptor.Bytes = bytes;
            blockDescriptor.OwnerKey = record.AllocationIndex;
            blockDescriptor.Generation = 1;
            blockDescriptor.Owner = owner;
            blockDescriptor.Flags = (ushort)flags;
            blockDescriptor.State = (byte)H8BlockState.Occupied;

            int descriptorIndex;
            try
            {
                descriptorIndex = RegisterBlockDescriptorThreadSafe(in blockDescriptor);
            }
            catch
            {
                RemoveRecordAt(recordIndex);
                return false;
            }

            if (descriptorIndex >= 0)
            {
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Allocated);
                return true;
            }

            RemoveRecordAt(recordIndex);
            return false;
        }

        private static bool TryRestoreUnregisteredRecord(in H8AllocationRecord record)
        {
            if (record.Pointer == IntPtr.Zero || record.Bytes <= 0L)
                return false;

            return RegisterPointer(
                record.Pointer.ToPointer(),
                record.Bytes,
                record.Length,
                record.Stride,
                record.Alignment,
                record.Owner,
                record.Allocator,
                (H8AllocationFlags)record.Flags,
                record.Generation);
        }

        private static bool TryUnregisterFreeAndRestoreOnFailure(
            void* pointer,
            SystemID owner,
            Allocator allocator,
            bool requireOwnerMatch)
        {
            if (pointer == null)
                return true;

            H8AllocationRecord record = default;
            bool canRestoreTracking = TryFindRecordIndex((IntPtr)pointer, out int recordIndex);
            if (canRestoreTracking)
                record = _records[recordIndex];
            if (!UnregisterPointer(pointer, owner, requireOwnerMatch))
                return false;

            try
            {
                UnsafeUtility.Free(pointer, allocator);
                return true;
            }
            catch
            {
                if (canRestoreTracking)
                    TryRestoreUnregisteredRecord(in record);
                return false;
            }
        }

        private static bool UnregisterPointer(void* pointer, SystemID requester)
        {
            return UnregisterPointer(pointer, requester, requireOwnerMatch: true);
        }

        private static bool UnregisterPointer(void* pointer, SystemID requester, bool requireOwnerMatch)
        {
            if (!_initialized || pointer == null)
                return false;

            if (requireOwnerMatch && requester == SystemID.Unknown)
            {
                RecordBlackBox(requester, H8MemoryTelemetryFlags.Fault);
                return false;
            }

            long pointerKey = ((IntPtr)pointer).ToInt64();
            if (!ValidateOwnerMap(pointerKey, requester, requireOwnerMatch))
                return false;
            for (int i = _recordCount - 1; i >= 0; i--)
            {
                if (_records[i].Pointer.ToInt64() != pointerKey)
                    continue;

                if (requireOwnerMatch && _records[i].Owner != requester)
                {
                    RecordBlackBox(requester, H8MemoryTelemetryFlags.Fault);
                    return false;
                }

                RemoveRecordAt(i);
                return true;
            }

            if (requireOwnerMatch)
                RecordBlackBox(requester, H8MemoryTelemetryFlags.Fault);
            return false;
        }

        private static bool ValidateTrackedPointerOwner(void* pointer, SystemID requester, out long trackedBytes)
        {
            trackedBytes = 0L;
            if (!_initialized || pointer == null)
                return false;
            if (requester == SystemID.Unknown)
            {
                RecordBlackBox(requester, H8MemoryTelemetryFlags.Fault);
                return false;
            }

            long pointerKey = ((IntPtr)pointer).ToInt64();
            if (!ValidateOwnerMap(pointerKey, requester, requireOwnerMatch: true))
                return false;
            for (int i = _recordCount - 1; i >= 0; i--)
            {
                if (_records[i].Pointer.ToInt64() != pointerKey)
                    continue;

                if (_records[i].Owner != requester)
                {
                    RecordBlackBox(requester, H8MemoryTelemetryFlags.Fault);
                    return false;
                }

                trackedBytes = _records[i].Bytes;
                return true;
            }

            RecordBlackBox(requester, H8MemoryTelemetryFlags.Fault);
            return false;
        }

        private static bool ValidateOwnerMap(long pointerKey, SystemID requester, bool requireOwnerMatch)
        {
            if (!_allocationOwners.IsCreated ||
                !_allocationOwners.TryGetValue(pointerKey, out SystemID mappedOwner))
            {
                if (requireOwnerMatch)
                {
                    RecordBlackBox(requester, H8MemoryTelemetryFlags.Fault);
                    return false;
                }

                return true;
            }

            if (requireOwnerMatch && mappedOwner != requester)
            {
                RecordBlackBox(requester, H8MemoryTelemetryFlags.Fault);
                return false;
            }

            return true;
        }

        private static int AdvanceDescriptorGeneration(int generation)
        {
            int nextGeneration = unchecked(generation + 1);
            return nextGeneration <= 0 ? 1 : nextGeneration;
        }

        private static int ResolveSafeAlignment(int alignment)
        {
            if (alignment <= MinimumRawAlignment)
                return MinimumRawAlignment;

            int resolved = MinimumRawAlignment;
            while (resolved < alignment && resolved < MaximumRawAlignment)
                resolved <<= 1;

            return resolved < alignment ? MaximumRawAlignment : resolved;
        }

        private static bool TryEnterBlockDescriptorMutationGate()
        {
            if (System.Threading.Interlocked.CompareExchange(ref _blockDescriptorMutationGate, 1, 0) != 0)
            {
                RecordBlackBox(SystemID.H8Memory, H8MemoryTelemetryFlags.Fault);
                return false;
            }

            System.Threading.Thread.MemoryBarrier();
            return true;
        }

        private static void ReleaseBlockDescriptorMutationGate()
        {
            System.Threading.Thread.MemoryBarrier();
            System.Threading.Volatile.Write(ref _blockDescriptorMutationGate, 0);
        }

        private static void RemoveRecordAt(int index)
        {
            RemoveRecordAt(index, removeOwnerPointer: true, H8MemoryTelemetryFlags.Released);
        }

        private static void RemoveRecordAt(int index, bool removeOwnerPointer)
        {
            RemoveRecordAt(index, removeOwnerPointer, H8MemoryTelemetryFlags.Released);
        }

        private static void RemoveRecordAt(int index, bool removeOwnerPointer, H8MemoryTelemetryFlags telemetryFlags)
        {
            H8AllocationRecord record = _records[index];
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Single funnel for retiring a tracked pointer - UnregisterPointer, ForceFreeRecordAt (ReleaseAll,
            // scene-transition purge, sentinel reap) and the RegisterPointer rollbacks all land here - so the
            // region's alias handles are released exactly once, here, before the record is dropped.
            ReleaseAliasRegionHandlesInRange(record.Pointer, record.Bytes);
#endif
            long pointerKey = record.Pointer.ToInt64();
            _allocationOwners.Remove(pointerKey);
            if (_allocationRecordIndices.IsCreated)
                _allocationRecordIndices.Remove(pointerKey);
            if (removeOwnerPointer)
                RemoveOwnerPointer(record.Owner, record.Pointer);
            MarkBlockDescriptorFree(record.Pointer, 0L);
            _totalBytes -= record.Bytes;
            int ownerIndex = (int)record.Owner;
            if ((uint)ownerIndex < (uint)_ownerBytes.Length)
                _ownerBytes[ownerIndex] -= record.Bytes;

            _recordCount--;
            if (index != _recordCount)
            {
                H8AllocationRecord moved = _records[_recordCount];
                moved.AllocationIndex = index;
                _records[index] = moved;
                if (_allocationRecordIndices.IsCreated && moved.Pointer != IntPtr.Zero)
                    _allocationRecordIndices[moved.Pointer.ToInt64()] = index;
                UpdateBlockDescriptorOwnerKey(moved.Pointer, 0L, index);
            }

            _records[_recordCount] = default;
            RecordBlackBox(record.Owner, telemetryFlags);
        }

        private static int RegisterBlockDescriptorThreadSafe(in BlockDescriptor descriptor)
        {
            if (!TryEnterBlockDescriptorMutationGate())
                return -1;

            try
            {
                return RegisterBlockDescriptorNoInit(in descriptor);
            }
            finally
            {
                ReleaseBlockDescriptorMutationGate();
            }
        }

        private static int RegisterBlockDescriptorNoInit(in BlockDescriptor descriptor)
        {
            if (!_blockDescriptors.IsCreated)
                return -1;

            for (int i = 0; i < _blockDescriptors.Length; i++)
            {
                BlockDescriptor existing = _blockDescriptors[i];
                if (existing.Bytes != 0L)
                    continue;

                BlockDescriptor replacement = descriptor;
                int nextGeneration = AdvanceDescriptorGeneration(existing.Generation);
                if (replacement.Generation < nextGeneration)
                    replacement.Generation = nextGeneration;
                _blockDescriptors[i] = replacement;
                return i;
            }

            if (_blockDescriptors.Length >= _blockDescriptors.Capacity)
            {
                int oldCapacity = _blockDescriptors.Capacity;
                if (oldCapacity >= MaxTrackingCapacity)
                    return -1;

                int newCapacity = oldCapacity > 0 ? oldCapacity << 1 : DefaultCapacity;
                if (newCapacity < oldCapacity || newCapacity > MaxTrackingCapacity)
                    newCapacity = MaxTrackingCapacity;

                if (!TryEnsureBlockDescriptorCapacityNoLock(newCapacity))
                    return -1;
                if (_blockDescriptors.Length >= _blockDescriptors.Capacity)
                    return -1;
            }

            int appendedIndex;
            return TryAppendBlockDescriptorNoLock(in descriptor, out appendedIndex) ? appendedIndex : -1;
        }

        private static bool TryEnsureBlockDescriptorCapacityNoLock(int requiredCapacity)
        {
            try
            {
                EnsureBlockDescriptorCapacity(requiredCapacity);
                return _blockDescriptors.IsCreated && _blockDescriptors.Capacity >= requiredCapacity;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryAppendBlockDescriptorNoLock(in BlockDescriptor descriptor, out int index)
        {
            index = -1;
            if (!_blockDescriptors.IsCreated || _blockDescriptors.Length >= _blockDescriptors.Capacity)
                return false;

            try
            {
                index = _blockDescriptors.Length;
                _blockDescriptors.AddNoResize(descriptor);
                return true;
            }
            catch
            {
                index = -1;
                return false;
            }
        }

        private static bool TryReserveReusableBlockDescriptorSlot(in BlockDescriptor reservation, out int index)
        {
            index = -1;
            if (!_blockDescriptors.IsCreated)
                return false;

            for (int i = 0; i < _blockDescriptors.Length; i++)
            {
                BlockDescriptor existing = _blockDescriptors[i];
                if (existing.Bytes != 0L)
                    continue;

                BlockDescriptor replacement = reservation;
                replacement.Generation = AdvanceDescriptorGeneration(existing.Generation);
                _blockDescriptors[i] = replacement;
                index = i;
                return true;
            }

            return false;
        }

        private static void EnsureBlockDescriptorCapacity(int requiredCapacity)
        {
            if (!_blockDescriptors.IsCreated || requiredCapacity <= _blockDescriptors.Capacity)
                return;

            _blockDescriptors.Capacity = requiredCapacity;
        }

        private static void MarkBlockDescriptorFree(IntPtr basePointer, long offsetBytes)
        {
            if (!TryEnterBlockDescriptorMutationGate())
                return;

            try
            {
                MarkBlockDescriptorFreeNoLock(basePointer, offsetBytes);
            }
            finally
            {
                ReleaseBlockDescriptorMutationGate();
            }
        }

        private static void MarkBlockDescriptorFreeNoLock(IntPtr basePointer, long offsetBytes)
        {
            if (!_blockDescriptors.IsCreated || basePointer == IntPtr.Zero)
                return;

            for (int i = _blockDescriptors.Length - 1; i >= 0; i--)
            {
                BlockDescriptor descriptor = _blockDescriptors[i];
                if (descriptor.BasePointer != basePointer || descriptor.OffsetBytes != offsetBytes)
                    continue;

                descriptor.BasePointer = IntPtr.Zero;
                descriptor.OffsetBytes = 0L;
                descriptor.Bytes = 0L;
                descriptor.OwnerKey = 0;
                descriptor.Owner = SystemID.Unknown;
                descriptor.State = (byte)H8BlockState.Free;
                descriptor.Flags = (ushort)H8AllocationFlags.Freed;
                descriptor.Reserved = 0;
                descriptor.Generation = AdvanceDescriptorGeneration(descriptor.Generation);
                _blockDescriptors[i] = descriptor;
                return;
            }
        }

        private static void UpdateBlockDescriptorOwnerKey(IntPtr basePointer, long offsetBytes, int ownerKey)
        {
            if (!TryEnterBlockDescriptorMutationGate())
                return;

            try
            {
                UpdateBlockDescriptorOwnerKeyNoLock(basePointer, offsetBytes, ownerKey);
            }
            finally
            {
                ReleaseBlockDescriptorMutationGate();
            }
        }

        private static void UpdateBlockDescriptorOwnerKeyNoLock(IntPtr basePointer, long offsetBytes, int ownerKey)
        {
            if (!_blockDescriptors.IsCreated || basePointer == IntPtr.Zero)
                return;

            for (int i = _blockDescriptors.Length - 1; i >= 0; i--)
            {
                BlockDescriptor descriptor = _blockDescriptors[i];
                if (descriptor.BasePointer != basePointer || descriptor.OffsetBytes != offsetBytes)
                    continue;

                if (descriptor.State != (byte)H8BlockState.Occupied)
                    return;

                descriptor.OwnerKey = ownerKey;
                descriptor.Generation = AdvanceDescriptorGeneration(descriptor.Generation);
                _blockDescriptors[i] = descriptor;
                return;
            }
        }
    }
}

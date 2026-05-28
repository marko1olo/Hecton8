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
        HazardExposureJobResult = 503,
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
        ToolKinematicsMockTriggerSignals = 613,
        ToolKinematicsMockCarveRequests = 614,
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
        WreckBrgBatchMetadata = 132816
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
        Occupied = 1
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
        private static int _allocationGeneration = 1;
        private static int _transitionCutoffGeneration = NoTransitionCutoffGeneration;
        private static int _transitionSequence;
        private static int _lastTransitionReleasedCount;
        private static long _lastTransitionReleasedBytes;
        private static long _transitionBaselineBytes;
        private static long _transitionExpectedBytes;
        private static bool _lastTransitionBaselineVerified = true;
        private static bool _deferSceneUnloadedVerificationToRuntime;
        private static bool _sceneHooksRegistered;
        private static bool _initialized;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private static AtomicSafetyHandle _aliasSafetyHandle;
        private static bool _aliasSafetyHandleCreated;
#endif

        /// <summary>Tracked allocation count.</summary>
        public static int ActiveAllocationCount => _recordCount;

        /// <summary>True while H8Memory tracking tables are live.</summary>
        public static bool IsInitialized => _initialized;

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
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                Shutdown();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSubsystemRegistration()
        {
            Shutdown();
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
            _aliasSafetyHandle = AtomicSafetyHandle.Create();
            _aliasSafetyHandleCreated = true;
#endif
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
                return;
            }

            void* pointer = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            if (!UnregisterPointer(pointer, owner))
                return;
            array.Dispose();
            array = default;
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
                return dependency;
            }

            void* pointer = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            if (!UnregisterPointer(pointer, owner))
                return dependency;
            JobHandle disposeHandle = array.Dispose(dependency);
            RegisterActiveJob(owner, disposeHandle);
            array = default;
            return disposeHandle;
        }

        /// <summary>
        /// Records an owner job fence so forced teardown can block only at scene-transition/owner-destruction boundaries.
        /// </summary>
        /// <param name="owner">Native allocation owner.</param>
        /// <param name="handle">Active job handle touching owner memory.</param>
        public static void RegisterActiveJob(SystemID owner, JobHandle handle)
        {
            if (owner == SystemID.Unknown)
            {
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                return;
            }
            if (!_initialized)
                Initialize();
            if (!_initialized)
                return;

            if (!_ownerJobHandles.IsCreated)
                return;

            ushort ownerKey = GetOwnerKey(owner);
            if (_ownerJobHandles.TryGetValue(ownerKey, out JobHandle existingHandle))
            {
                _ownerJobHandles[ownerKey] = JobHandle.CombineDependencies(existingHandle, handle);
                AddOwnerJobKey(ownerKey);
                return;
            }

            if (!_ownerJobKeys.IsCreated || !_ownerJobHandles.TryAdd(ownerKey, handle))
            {
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                return;
            }

            AddOwnerJobKey(ownerKey);
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
                UnsafeUtility.Free(pointer, allocator);
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
                UnsafeUtility.Free(newPointer, allocator);
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Fault);
                return null;
            }

            if (!UnregisterPointer(oldPointer, owner))
            {
                UnregisterPointer(newPointer, owner, requireOwnerMatch: false);
                UnsafeUtility.Free(newPointer, allocator);
                return null;
            }
            UnsafeUtility.Free(oldPointer, allocator);

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
            if (pointer == null)
                return;
            if (requester == SystemID.Unknown)
            {
                RecordBlackBox(requester, H8MemoryTelemetryFlags.Fault);
                return;
            }
            if (!_initialized)
            {
                RecordBlackBox(requester, H8MemoryTelemetryFlags.Fault);
                return;
            }

            if (!UnregisterPointer(pointer, requester))
                return;
            UnsafeUtility.Free(pointer, allocator);
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

            UnsafeUtility.Free(pointer, fallbackAllocator);
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
            if (_aliasSafetyHandleCreated)
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, _aliasSafetyHandle);
#endif
            return array;
        }

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

            return RegisterBlockDescriptorNoInit(in descriptor);
        }

        /// <summary>
        /// Updates a memory-map descriptor in-place.
        /// </summary>
        internal static bool TryUpdateBlockDescriptor(int index, in BlockDescriptor descriptor)
        {
            if (!_initialized || !_blockDescriptors.IsCreated || (uint)index >= (uint)_blockDescriptors.Length)
                return false;

            _blockDescriptors[index] = descriptor;
            return true;
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

        /// <summary>
        /// Shuts down tracking tables. Only call from service shutdown after users released their buffers.
        /// </summary>
        public static void Shutdown()
        {
            UnregisterSceneHooks();
            if (!_initialized)
                return;

            CompleteAllOwnerJobs();

            for (int i = _recordCount - 1; i >= 0; i--)
            {
                H8AllocationRecord record = _records[i];
                if (record.Pointer != IntPtr.Zero)
                    UnsafeUtility.Free(record.Pointer.ToPointer(), record.Allocator);
            }

            _recordCount = 0;
            _totalBytes = 0L;
            RecordBlackBox(SystemID.H8Memory, H8MemoryTelemetryFlags.Shutdown);
            DisposeOwnerPointerLists();
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
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (_aliasSafetyHandleCreated)
            {
                AtomicSafetyHandle.Release(_aliasSafetyHandle);
                _aliasSafetyHandleCreated = false;
            }
#endif
            _allocationGeneration = 1;
            _transitionCutoffGeneration = NoTransitionCutoffGeneration;
            _transitionSequence = 0;
            _lastTransitionReleasedCount = 0;
            _lastTransitionReleasedBytes = 0L;
            _transitionBaselineBytes = 0L;
            _transitionExpectedBytes = 0L;
            _lastTransitionBaselineVerified = true;
            _deferSceneUnloadedVerificationToRuntime = false;
            _blackBoxCursor = 0;
            _eventBlackBoxCursor = 0;
            _blackBoxRecordedCount = 0;
            _eventBlackBoxRecordedCount = 0;
            _blackBoxSequence = 0u;
            _eventBlackBoxSequence = 0u;
            _telemetryFrameId = 0u;
            _initialized = false;
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

        private static void AddOwnerJobKey(ushort ownerKey)
        {
            if (!_ownerJobKeys.IsCreated)
                return;

            for (int i = 0; i < _ownerJobKeys.Length; i++)
            {
                if (_ownerJobKeys[i] == ownerKey)
                    return;
            }

            _ownerJobKeys.Add(ownerKey);
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

        private static void AddOwnerPointerKey(ushort ownerKey)
        {
            if (!_ownerPointerKeys.IsCreated)
                return;

            for (int i = 0; i < _ownerPointerKeys.Length; i++)
            {
                if (_ownerPointerKeys[i] == ownerKey)
                    return;
            }

            _ownerPointerKeys.Add(ownerKey);
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
            UnsafeUtility.Free(record.Pointer.ToPointer(), record.Allocator);
            RemoveRecordAt(index, removeOwnerPointer, H8MemoryTelemetryFlags.ForcedRelease);
            return true;
        }

        private static bool RegisterOwnerPointer(SystemID owner, IntPtr pointer)
        {
            if (owner == SystemID.Unknown || pointer == IntPtr.Zero || !_ownerPointers.IsCreated)
                return false;

            ushort ownerKey = GetOwnerKey(owner);
            if (!_ownerPointers.TryGetValue(ownerKey, out NativeList<IntPtr> pointers))
            {
                // COLD ALLOC: NativeList<IntPtr>[16] - owner pointer lane for ReleaseAll(SystemID) - owner: H8Memory
                pointers = new NativeList<IntPtr>(DefaultOwnerPointerCapacity, Allocator.Persistent);
                if (!_ownerPointers.TryAdd(ownerKey, pointers))
                {
                    pointers.Dispose();
                    return false;
                }
            }

            AddOwnerPointerKey(ownerKey);
            pointers.Add(pointer);
            _ownerPointers[ownerKey] = pointers;
            return true;
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
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

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
            using (FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                WriteFatalLeakMarker(stream);
                WriteUInt64LittleEndian(stream, FatalLeakDumpMagic);
                WriteInt32LittleEndian(stream, FatalLeakDumpVersion);
                WriteInt32LittleEndian(stream, H8MemoryTelemetryEntrySizeBytes);
                WriteInt32LittleEndian(stream, H8AllocationRecordSizeBytes);
                WriteInt32LittleEndian(stream, BlackBoxFrameCount);
                WriteUInt16LittleEndian(stream, (ushort)owner);
                WriteInt32LittleEndian(stream, _transitionSequence);
                WriteInt32LittleEndian(stream, releaseCount);
                WriteInt64LittleEndian(stream, releasedBytes);
                WriteInt64LittleEndian(stream, _totalBytes);
                WriteInt64LittleEndian(stream, _transitionBaselineBytes);
                WriteInt64LittleEndian(stream, _transitionExpectedBytes);
                WriteInt32LittleEndian(stream, baselineMismatch ? 1 : 0);
                WriteBlackBoxEntries(stream);
                WriteInt32LittleEndian(stream, _recordCount);
                int dumpCount = _recordCount < 300 ? _recordCount : 300;
                WriteInt32LittleEndian(stream, dumpCount);
                for (int i = 0; i < dumpCount; i++)
                {
                    H8AllocationRecord record = _records[i];
                    WriteUInt64LittleEndian(stream, ComputeAllocationAddressFingerprint(in record));
                    WriteInt64LittleEndian(stream, record.Bytes);
                    WriteInt32LittleEndian(stream, record.Length);
                    WriteInt32LittleEndian(stream, record.Stride);
                    WriteInt32LittleEndian(stream, record.Alignment);
                    WriteInt32LittleEndian(stream, record.AllocationIndex);
                    WriteInt32LittleEndian(stream, record.Generation);
                    WriteUInt16LittleEndian(stream, (ushort)record.Owner);
                    WriteInt32LittleEndian(stream, (int)record.Allocator);
                    WriteUInt16LittleEndian(stream, record.Flags);
                }
            }
        }

        private static void WriteFatalLeakMarker(FileStream stream)
        {
            stream.WriteByte(22);
            stream.WriteByte((byte)'[');
            stream.WriteByte((byte)'F');
            stream.WriteByte((byte)'A');
            stream.WriteByte((byte)'T');
            stream.WriteByte((byte)'A');
            stream.WriteByte((byte)'L');
            stream.WriteByte((byte)' ');
            stream.WriteByte((byte)'L');
            stream.WriteByte((byte)'E');
            stream.WriteByte((byte)'A');
            stream.WriteByte((byte)'K');
            stream.WriteByte((byte)':');
            stream.WriteByte((byte)' ');
            stream.WriteByte((byte)'S');
            stream.WriteByte((byte)'y');
            stream.WriteByte((byte)'s');
            stream.WriteByte((byte)'t');
            stream.WriteByte((byte)'e');
            stream.WriteByte((byte)'m');
            stream.WriteByte((byte)'I');
            stream.WriteByte((byte)'D');
            stream.WriteByte((byte)']');
        }

        private static void WriteUInt16LittleEndian(FileStream stream, ushort value)
        {
            Span<byte> bytes = stackalloc byte[2];
            bytes[0] = (byte)value;
            bytes[1] = (byte)(value >> 8);
            stream.Write(bytes);
        }

        private static void WriteInt32LittleEndian(FileStream stream, int value)
        {
            WriteUInt32LittleEndian(stream, unchecked((uint)value));
        }

        private static void WriteUInt32LittleEndian(FileStream stream, uint value)
        {
            Span<byte> bytes = stackalloc byte[4];
            bytes[0] = (byte)value;
            bytes[1] = (byte)(value >> 8);
            bytes[2] = (byte)(value >> 16);
            bytes[3] = (byte)(value >> 24);
            stream.Write(bytes);
        }

        private static void WriteInt64LittleEndian(FileStream stream, long value)
        {
            WriteUInt64LittleEndian(stream, unchecked((ulong)value));
        }

        private static void WriteUInt64LittleEndian(FileStream stream, ulong value)
        {
            Span<byte> bytes = stackalloc byte[8];
            bytes[0] = (byte)value;
            bytes[1] = (byte)(value >> 8);
            bytes[2] = (byte)(value >> 16);
            bytes[3] = (byte)(value >> 24);
            bytes[4] = (byte)(value >> 32);
            bytes[5] = (byte)(value >> 40);
            bytes[6] = (byte)(value >> 48);
            bytes[7] = (byte)(value >> 56);
            stream.Write(bytes);
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

        private static void WriteBlackBoxEntries(FileStream stream)
        {
            WriteBlackBoxRing(stream, BlackBoxRingKindHeartbeat, _blackBox, _blackBoxRecordedCount, _blackBoxCursor);
            WriteBlackBoxRing(stream, BlackBoxRingKindLifecycleEvent, _eventBlackBox, _eventBlackBoxRecordedCount, _eventBlackBoxCursor);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong MixAddressFingerprint(ulong hash, ulong value)
        {
            return unchecked((hash ^ value) * AddressFingerprintPrime);
        }

        private static void WriteBlackBoxRing(
            FileStream stream,
            byte ringKind,
            NativeArray<H8MemoryTelemetryEntry> ring,
            int recordedCount,
            int cursor)
        {
            stream.WriteByte(ringKind);
            WriteInt32LittleEndian(stream, ring.IsCreated ? ring.Length : 0);
            WriteInt32LittleEndian(stream, H8MemoryTelemetryEntrySizeBytes);

            if (!ring.IsCreated || ring.Length == 0)
            {
                WriteInt32LittleEndian(stream, 0);
                return;
            }

            if (recordedCount < 0)
                recordedCount = 0;
            if (recordedCount > ring.Length)
                recordedCount = ring.Length;
            WriteInt32LittleEndian(stream, recordedCount);

            int start = recordedCount < ring.Length ? 0 : cursor;
            for (int i = 0; i < recordedCount; i++)
            {
                int index = start + i;
                if (index >= ring.Length)
                    index -= ring.Length;

                H8MemoryTelemetryEntry entry = ring[index];
                WriteInt64LittleEndian(stream, entry.TotalBytes);
                WriteInt64LittleEndian(stream, entry.TransitionBaselineBytes);
                WriteInt64LittleEndian(stream, entry.LastTransitionReleasedBytes);
                WriteUInt32LittleEndian(stream, entry.Sequence);
                WriteInt32LittleEndian(stream, entry.ActiveAllocationCount);
                WriteInt32LittleEndian(stream, entry.BlockDescriptorCount);
                WriteInt32LittleEndian(stream, entry.AllocationGeneration);
                WriteInt32LittleEndian(stream, entry.TransitionCutoffGeneration);
                WriteInt32LittleEndian(stream, entry.TransitionSequence);
                WriteInt32LittleEndian(stream, entry.LastTransitionReleasedCount);
                WriteInt32LittleEndian(stream, entry.FatalLeakPreventedCount);
                WriteUInt32LittleEndian(stream, entry.Frame);
                WriteUInt16LittleEndian(stream, entry.Owner);
                WriteUInt16LittleEndian(stream, entry.Flags);
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

            NativeArray<H8AllocationRecord> newRecords =
                new NativeArray<H8AllocationRecord>(newCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeParallelHashMap<long, SystemID> newOwners =
                new NativeParallelHashMap<long, SystemID>(newCapacity, Allocator.Persistent);
            NativeParallelHashMap<long, int> newIndices =
                new NativeParallelHashMap<long, int>(newCapacity, Allocator.Persistent);

            for (int i = 0; i < _recordCount; i++)
            {
                H8AllocationRecord record = _records[i];
                newRecords[i] = record;
                if (record.Pointer == IntPtr.Zero)
                    continue;

                long pointerKey = record.Pointer.ToInt64();
                if (!newOwners.TryAdd(pointerKey, record.Owner) || !newIndices.TryAdd(pointerKey, i))
                {
                    newRecords.Dispose();
                    newOwners.Dispose();
                    newIndices.Dispose();
                    return false;
                }
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
            EnsureBlockDescriptorCapacity(newCapacity);
            return true;
        }

        private static bool RegisterPointer(
            void* pointer,
            long bytes,
            int length,
            int stride,
            int alignment,
            SystemID owner,
            Allocator allocator,
            H8AllocationFlags flags)
        {
            if (pointer == null || bytes <= 0L || _recordCount >= _records.Length)
                return false;

            IntPtr pointerValue = (IntPtr)pointer;
            long pointerKey = pointerValue.ToInt64();
            int recordIndex = _recordCount;
            if (!_allocationOwners.TryAdd(pointerKey, owner))
                return false;

            if (!_allocationRecordIndices.TryAdd(pointerKey, recordIndex))
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

            H8AllocationRecord record = new H8AllocationRecord
            {
                Pointer = pointerValue,
                Bytes = bytes,
                Length = length,
                Stride = stride,
                Alignment = alignment,
                AllocationIndex = recordIndex,
                Generation = _allocationGeneration,
                Owner = owner,
                Allocator = allocator,
                Flags = (ushort)flags
            };

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

            int descriptorIndex = RegisterBlockDescriptorNoInit(new BlockDescriptor
            {
                BasePointer = pointerValue,
                OffsetBytes = 0L,
                Bytes = bytes,
                OwnerKey = record.AllocationIndex,
                Generation = 1,
                Owner = owner,
                Flags = (ushort)flags,
                State = (byte)H8BlockState.Occupied
            });

            if (descriptorIndex >= 0)
            {
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Allocated);
                return true;
            }

            RemoveRecordAt(recordIndex);
            return false;
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

                EnsureBlockDescriptorCapacity(newCapacity);
                if (_blockDescriptors.Length >= _blockDescriptors.Capacity)
                    return -1;
            }

            int index = _blockDescriptors.Length;
            _blockDescriptors.AddNoResize(descriptor);
            return index;
        }

        private static void EnsureBlockDescriptorCapacity(int requiredCapacity)
        {
            if (!_blockDescriptors.IsCreated || requiredCapacity <= _blockDescriptors.Capacity)
                return;

            _blockDescriptors.Capacity = requiredCapacity;
        }

        private static void MarkBlockDescriptorFree(IntPtr basePointer, long offsetBytes)
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

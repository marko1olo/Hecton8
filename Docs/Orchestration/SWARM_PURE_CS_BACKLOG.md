# SWARM PURE C# BACKLOG (200 TASK MEGA-EDITION)

**CLASSIFICATION:** ISOLATED PURE C# (NO-UNITY) SWARM TASKS
**TARGET ORCHESTRATOR:** JULES / EXTERNAL ROSTER
**AUTHORITY:** Principal Technical Director / Chief Systems Architect

## [OVERVIEW]
This backlog contains exactly 200 isolated mathematical and logical tasks extracted from monolithic engine scripts.
**ALL TASKS MUST BE IMPLEMENTED IN PURE C# (System.Numerics) WITH ZERO DEPENDENCIES ON UNITY ENGINE.**

## [TASKS LIST]

### TASK-01: FaunaHypnosisPullForceCalculator
- **Source Monolith File:** `HectonPlayerMovement.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/FaunaHypnosisPullForceCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/FaunaHypnosisPullForceCalculatorTests.cs`
- **Objective:** Calculate the exact environmental pull force vector exerted on the player during a fauna hypnosis event using inverse-square falloff.
- **Inputs:** `Vector3 playerPos, Vector3 sourcePos, float acceleration, float playerMass, float lockDuration`
- **Outputs:** `Vector3 (Resulting force vector)`
- **Constraints:** Use System.Numerics.Vector3. Do NOT reference UnityEngine or Rigidbody.
- **Test Requirement:** Verify zero acceleration yields zero force. Test distance-based falloff curves.

### TASK-02: ParasiteLatchDragCalculator
- **Source Monolith File:** `HectonPlayerMovement.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/ParasiteLatchDragCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ParasiteLatchDragCalculatorTests.cs`
- **Objective:** Calculate the localized drag force and drift velocity penalty imposed by parasites latched onto the player's body.
- **Inputs:** `int latchedCount, Vector3 currentVelocity, float dragCoefficient, Vector3 harvesterPull`
- **Outputs:** `Vector3 (Drift velocity offset)`
- **Constraints:** Pure math. Vector math only via System.Numerics.
- **Test Requirement:** Verify 0 parasites = 0 drag. Verify exponential scaling with latched count up to a maximum cap.

### TASK-03: ThermalVentUpdraftForce
- **Source Monolith File:** `HectonPlayerMovement.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/ThermalVentUpdraftForce.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ThermalVentUpdraftForceTests.cs`
- **Objective:** Compute the upward buoyancy force applied to the player inside a thermal vent plume, factoring in radial dissipation.
- **Inputs:** `Vector3 playerPos, Vector3 ventCenter, float ventRadius, float coreForce, float decayFactor`
- **Outputs:** `Vector3 (Lift force)`
- **Constraints:** Pure geometry and mathematical curves.
- **Test Requirement:** Validate maximum lift force at core center and smooth falloff to 0 at ventRadius boundary.

### TASK-04: NitrogenNarcosisInputDrifter
- **Source Monolith File:** `HectonPlayerMovement.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/NitrogenNarcosisInputDrifter.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/NitrogenNarcosisInputDrifterTests.cs`
- **Objective:** Generate pseudo-random drift offset applied to player 2D inputs based on current nitrogen narcosis depth.
- **Inputs:** `Vector2 rawInput, float narcosisDepth01, float timeSeconds, int seed`
- **Outputs:** `Vector2 (Drifted input vector)`
- **Constraints:** Implement a pure C# LCG or Simplex algorithm. Do NOT use UnityEngine.Random.
- **Test Requirement:** Ensure narcosisDepth = 0 yields identical input. Validate drift magnitude at depth = 1.0.

### TASK-05: InventoryMassLoadSpeedScalar
- **Source Monolith File:** `HectonPlayerMovement.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/InventoryMassLoadSpeedScalar.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/InventoryMassLoadSpeedScalarTests.cs`
- **Objective:** Compute the logarithmic speed reduction scalar based on total carried inventory mass relative to structural limits.
- **Inputs:** `float carriedMassKg, float carryCapacityKg, float baseSpeed`
- **Outputs:** `float (Final velocity speed scalar)`
- **Constraints:** Pure algebraic curve calculation.
- **Test Requirement:** Validate threshold boundaries: 0 mass = 1.0 scalar, capacity mass = threshold, overcapacity = survival speed floor.

### TASK-06: VehicleEmergencyEjectionVector
- **Source Monolith File:** `HectonPlayerMovement.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/VehicleEmergencyEjectionVector.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/VehicleEmergencyEjectionVectorTests.cs`
- **Objective:** Calculate the trajectory vector for a pilot ejecting from a moving transport, factoring in vehicle velocity and orientation.
- **Inputs:** `Vector3 vehicleVel, Vector3 vehicleForward, Vector3 vehicleUp, float severity`
- **Outputs:** `Vector3 (Ejection impulse vector)`
- **Constraints:** System.Numerics vector math.
- **Test Requirement:** Verify ejection is always biased vertically upward (+Y) and outward from vehicle momentum.

### TASK-07: EquipmentHydrodynamicDragCalculator
- **Source Monolith File:** `HectonPlayerMovement.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/EquipmentHydrodynamicDragCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/EquipmentHydrodynamicDragCalculatorTests.cs`
- **Objective:** Decode active equipment bits to calculate total hydrodynamic drag coefficient.
- **Inputs:** `ulong activeEquipmentMask, float[] baseDragTable`
- **Outputs:** `float (Cumulative drag scalar)`
- **Constraints:** Bitwise logic only.
- **Test Requirement:** Ensure no bits set yields 1.0 drag. Test combinations of heavy tanks and external tools.

### TASK-08: BrineSubmersionToxicityRate
- **Source Monolith File:** `HectonPlayerMovement.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/BrineSubmersionToxicityRate.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/BrineSubmersionToxicityRateTests.cs`
- **Objective:** Calculate toxicity accumulation rate in deep brine pools, factoring in suit acid shielding.
- **Inputs:** `float brineDensity01, float suitShielding01, float elapsedSeconds`
- **Outputs:** `float (Toxic dose delta)`
- **Constraints:** Pure math.
- **Test Requirement:** Validate suitShielding = 1.0 blocks all toxicity. High density and 0 shielding yields max rate.

### TASK-09: SinusoidalHoverBobbingCalculator
- **Source Monolith File:** `HectonPlayerMovement.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/SinusoidalHoverBobbingCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SinusoidalHoverBobbingCalculatorTests.cs`
- **Objective:** Calculate the vertical hovering offset over time to simulate thruster bobbing.
- **Inputs:** `float baseHeight, float timeSeconds, float frequency, float amplitude`
- **Outputs:** `float (Adjusted hover height)`
- **Constraints:** Pure C# math.
- **Test Requirement:** Ensure sine limits are strictly bounded within (baseHeight +/- amplitude).

### TASK-10: WaterSurfaceTransitionDragCalculator
- **Source Monolith File:** `HectonPlayerMovement.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/WaterSurfaceTransitionDragCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/WaterSurfaceTransitionDragCalculatorTests.cs`
- **Objective:** Calculate the dynamic deceleration impulse when breaking the ocean surface at speed.
- **Inputs:** `Vector3 entryVelocity, float surfaceDensity, float bodyCrossSection`
- **Outputs:** `Vector3 (Deceleration impulse)`
- **Constraints:** System.Numerics math.
- **Test Requirement:** High-speed vertical impacts should return massive drag scaling; low-speed transitions yield nominal resistance.

### TASK-11: SubmergedBuoyancyForce
- **Source Monolith File:** `BuoyancyObject.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/SubmergedBuoyancyForce.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SubmergedBuoyancyForceTests.cs`
- **Objective:** Calculate the upward buoyancy force vector based on displaced volume, fluid density, and depth.
- **Inputs:** `float volumeM3, float fluidDensity, float submergedVolume01, Vector3 gravity`
- **Outputs:** `Vector3 (Buoyancy force vector)`
- **Constraints:** System.Numerics math.
- **Test Requirement:** Validate that force vector opposes the gravity direction. 0 submerged volume yields 0 force.

### TASK-12: FluidVelocityFieldDragCalculator
- **Source Monolith File:** `SubmarineFluidDynamics.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/FluidVelocityFieldDragCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/FluidVelocityFieldDragCalculatorTests.cs`
- **Objective:** Compute drag force vector acting on a submarine hull moving through a current field.
- **Inputs:** `Vector3 hullVelocity, Vector3 currentVelocity, float dragCoefficient, float frontalArea`
- **Outputs:** `Vector3 (Resulting drag force vector)`
- **Constraints:** System.Numerics math.
- **Test Requirement:** Hull matching current velocity exactly must experience 0 drag. Opposing currents yield exponential drag.

### TASK-13: ThrusterEfficiencyVsPressureCalculator
- **Source Monolith File:** `PlayerThrusterAudio.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/ThrusterEfficiencyVsPressureCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ThrusterEfficiencyVsPressureCalculatorTests.cs`
- **Objective:** Calculate thruster output efficiency based on ambient hydrostatic pressure at depth.
- **Inputs:** `float baseThrust, float depthPressureBar, float optimalPressureBar, float decayRate`
- **Outputs:** `float (Modified thrust output)`
- **Constraints:** Pure math.
- **Test Requirement:** Maximum efficiency at optimal pressure; degradation at extreme depths.

### TASK-14: KinematicAccelerationLimiter
- **Source Monolith File:** `HectonPlayerMovement.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/KinematicAccelerationLimiter.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/KinematicAccelerationLimiterTests.cs`
- **Objective:** Clamp a raw velocity change delta to respect acceleration limits and engine threshold parameters.
- **Inputs:** `Vector3 currentVelocity, Vector3 targetVelocity, float maxAcceleration, float deltaTime`
- **Outputs:** `Vector3 (Clamped velocity delta)`
- **Constraints:** System.Numerics math.
- **Test Requirement:** Ensure delta magnitude never exceeds (maxAcceleration * deltaTime).

### TASK-15: SargassumKelpDragCalculator
- **Source Monolith File:** `HectonPlayerMovement.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/SargassumKelpDragCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SargassumKelpDragCalculatorTests.cs`
- **Objective:** Calculate drag penalty multiplier when moving through dense Sargassum seaweed patches.
- **Inputs:** `float sargassumDensity01, float currentSpeed, float bodyTangleWeight`
- **Outputs:** `float (Speed multiplier 0.0 to 1.0)`
- **Constraints:** Pure C# math.
- **Test Requirement:** Validate 0 density = 1.0. High speed and high density must drag speed down to a crawl.

### TASK-16: EcosystemSpawnCreditBudgeting
- **Source Monolith File:** `EcosystemDirector.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/EcosystemSpawnCreditBudgeting.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/EcosystemSpawnCreditBudgetingTests.cs`
- **Objective:** Calculate ecosystem credit regeneration factoring in local carrying capacity constraints.
- **Inputs:** `float currentCredits, float maxCredits, float regenRate, float deltaSeconds`
- **Outputs:** `float (New credit budget)`
- **Constraints:** Pure math.
- **Test Requirement:** Verify credit cap is strictly respected. Check linear accumulation over time.

### TASK-17: EcosystemSpeciesSelectionWeightCalculator
- **Source Monolith File:** `EcosystemDirector.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/EcosystemSpeciesSelectionWeightCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/EcosystemSpeciesSelectionWeightCalculatorTests.cs`
- **Objective:** Determine probability weight for selecting a species to spawn based on costs and ecosystem balances.
- **Inputs:** `float baseWeight, float creditCost, float currentAvailableCredits`
- **Outputs:** `float (Adjusted selection weight)`
- **Constraints:** Pure math.
- **Test Requirement:** If cost exceeds available credits, selection weight must be exactly 0.

### TASK-18: BiomassResourceGradientWeightCalculator
- **Source Monolith File:** `EcosystemDirector.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/BiomassResourceGradientWeightCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/BiomassResourceGradientWeightCalculatorTests.cs`
- **Objective:** Modify spawning weight based on spatial proximity to food source heatmap coordinates.
- **Inputs:** `float localFoodHeatValue, float optimalFoodThreshold, float baseWeight`
- **Outputs:** `float (Heat-adjusted weight)`
- **Constraints:** Pure math.
- **Test Requirement:** Higher food heat value yields multiplier > 1.0 for herbivores; no effect on carnivores if modeled.

### TASK-19: BiomeDepthViabilityCurveCalculator
- **Source Monolith File:** `EcosystemDirector.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/BiomeDepthViabilityCurveCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/BiomeDepthViabilityCurveCalculatorTests.cs`
- **Objective:** Calculate depth suitability for creature spawn using a Gaussian distribution.
- **Inputs:** `float currentDepth, float targetOptimalDepth, float depthTolerance`
- **Outputs:** `float (Suitability multiplier 0.0 to 1.0)`
- **Constraints:** Use System.Math.Exp for Gaussian calculation.
- **Test Requirement:** Max suitability at targetOptimalDepth; Suitability drops below 0.1 outside depthTolerance bounds.

### TASK-20: EcosystemLogicalLodTieringCalculator
- **Source Monolith File:** `EcosystemDirector.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/EcosystemLogicalLodTieringCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/EcosystemLogicalLodTieringCalculatorTests.cs`
- **Objective:** Calculate logical update rate tier for fauna entities based on radial distance zones and quality weights.
- **Inputs:** `float distanceSq, float zone1RadiusSq, float zone2RadiusSq, float qualityWeight`
- **Outputs:** `int (Tier Index: 0=Full, 1=Medium, 2=Suspended)`
- **Constraints:** Pure math.
- **Test Requirement:** Ensure high quality weight expands the Zone 1 radius. Verify boundaries match expected indices.

### TASK-21: PreytopredatorSpawnBalancerCalculator
- **Source Monolith File:** `EcosystemDirector.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/PreytopredatorSpawnBalancerCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/PreytopredatorSpawnBalancerCalculatorTests.cs`
- **Objective:** Evaluate predator spawning permission based on current prey-to-predator ratios in the sector.
- **Inputs:** `int preyCount, int predatorCount, float optimalRatio`
- **Outputs:** `bool (Spawn Allowed)`
- **Constraints:** Pure logical math.
- **Test Requirement:** Deny spawn if predator ratio is already oversaturated relative to prey population.

### TASK-22: FlockingBoidCohesionVector
- **Source Monolith File:** `HectonBoidController.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/FlockingBoidCohesionVector.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/FlockingBoidCohesionVectorTests.cs`
- **Objective:** Calculate steer force toward center of mass of neighboring boids.
- **Inputs:** `Vector3 boidPos, Vector3 neighborCenter, float cohesionWeight`
- **Outputs:** `Vector3 (Cohesion steering force)`
- **Constraints:** System.Numerics vector math.
- **Test Requirement:** Verify force vector directs towards the center. 0 weight = zero vector.

### TASK-23: FlockingBoidSeparationVector
- **Source Monolith File:** `HectonBoidController.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/FlockingBoidSeparationVector.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/FlockingBoidSeparationVectorTests.cs`
- **Objective:** Calculate steering vector to maintain minimum distance from nearest neighbor.
- **Inputs:** `Vector3 boidPos, Vector3 obstaclePos, float minDistance`
- **Outputs:** `Vector3 (Separation steering force)`
- **Constraints:** System.Numerics vector math.
- **Test Requirement:** Force magnitude must scale inversely with distance; zero force beyond minDistance.

### TASK-24: FlockingBoidAlignmentVector
- **Source Monolith File:** `HectonBoidController.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/FlockingBoidAlignmentVector.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/FlockingBoidAlignmentVectorTests.cs`
- **Objective:** Calculate steering force to align boid forward vector with average neighbor velocities.
- **Inputs:** `Vector3 boidVelocity, Vector3 averageNeighborVelocity, float maxSteerForce`
- **Outputs:** `Vector3 (Alignment steer vector)`
- **Constraints:** System.Numerics vector math.
- **Test Requirement:** Ensure output vector length is strictly capped by maxSteerForce.

### TASK-25: 2dGridHeatmapDecayCalculator
- **Source Monolith File:** `EcosystemDirector.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/2dGridHeatmapDecayCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/2dGridHeatmapDecayCalculatorTests.cs`
- **Objective:** Apply uniform decay factor to a grid of byte density values over time.
- **Inputs:** `byte[] grid, float decayRate, float deltaSeconds`
- **Outputs:** `byte[] (Decayed grid)`
- **Constraints:** Pure array math.
- **Test Requirement:** Verify values drop over time. Ensure values never wrap below 0.

### TASK-26: FaunaPatrolPathSmootherCalculator
- **Source Monolith File:** `FaunaDirector.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/FaunaPatrolPathSmootherCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/FaunaPatrolPathSmootherCalculatorTests.cs`
- **Objective:** Smooth out raw waypoint coordinates for a patrolling fauna agent using Catmull-Rom interpolation.
- **Inputs:** `Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t`
- **Outputs:** `Vector3 (Interpolated position)`
- **Constraints:** Pure spline math.
- **Test Requirement:** Validate t=0 yields p1, t=1 yields p2. Ensure continuity.

### TASK-27: BiomeDiscoveryBitmaskTracker
- **Source Monolith File:** `BiomeDiscoveryBitMask.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/BiomeDiscoveryBitmaskTracker.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/BiomeDiscoveryBitmaskTrackerTests.cs`
- **Objective:** Check and update bitmask representation of discovered biomes.
- **Inputs:** `uint currentMask, int biomeIndex`
- **Outputs:** `uint (Updated mask)`
- **Constraints:** Bitwise C# operations.
- **Test Requirement:** Verify bit setting. Check if already set.

### TASK-28: AmbientEncounterSpawningWeightCalculator
- **Source Monolith File:** `EncounterDirector.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/AmbientEncounterSpawningWeightCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/AmbientEncounterSpawningWeightCalculatorTests.cs`
- **Objective:** Compute spawning odds for ambient dynamic encounters based on player stress level.
- **Inputs:** `float baseWeight, float playerStress01, float cooldownRemaining`
- **Outputs:** `float (Encounter probability weight)`
- **Constraints:** Pure math.
- **Test Requirement:** If cooldown > 0, weight must be 0. Higher stress increases weight.

### TASK-29: SargassumKelpGrowthCurveCalculator
- **Source Monolith File:** `WorldProceduralScatterDirectorMigratorySargassum.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/SargassumKelpGrowthCurveCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SargassumKelpGrowthCurveCalculatorTests.cs`
- **Objective:** Model Sargassum cluster growth over time using a logistic differential curve.
- **Inputs:** `float currentSize, float maxClusterSize, float growthRate, float deltaHours`
- **Outputs:** `float (New cluster size)`
- **Constraints:** Pure math.
- **Test Requirement:** Ensure cluster size never exceeds maxClusterSize. Sigmoid growth behaviour.

### TASK-30: ThreatCostMultiplier
- **Source Monolith File:** `ThreatCostTable.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/ThreatCostMultiplier.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ThreatCostMultiplierTests.cs`
- **Objective:** Calculate cumulative threat credit cost modifications based on local temperature and depth.
- **Inputs:** `float baseCost, float temperatureCelsius, float depth`
- **Outputs:** `float (Modified threat cost)`
- **Constraints:** Pure math.
- **Test Requirement:** Temperatures below freezing increase threat cost; extreme depths reduce it.

### TASK-31: PressureHullIntegrityStressCalculator
- **Source Monolith File:** `HectonPlayerMovement.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/PressureHullIntegrityStressCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/PressureHullIntegrityStressCalculatorTests.cs`
- **Objective:** Compute integrity damage applied to a submarine pressure hull based on crush depth thresholds and impacts.
- **Inputs:** `float currentDepth, float crushDepth, float impactForce, float structuralIntegrity`
- **Outputs:** `float (Integrity damage delta)`
- **Constraints:** Pure math.
- **Test Requirement:** Depth < crushDepth causes 0 pressure damage. Force causes direct scaling damage.

### TASK-32: ActiveSonarAttenuationCurveCalculator
- **Source Monolith File:** `HectonPlayerMovement.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/ActiveSonarAttenuationCurveCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ActiveSonarAttenuationCurveCalculatorTests.cs`
- **Objective:** Compute active sonar signal return intensity after round-trip attenuation through water.
- **Inputs:** `float pingPower, float distance, float turbidityCoefficient`
- **Outputs:** `float (Return signal strength 0.0 to 1.0)`
- **Constraints:** Pure physical approximation formula.
- **Test Requirement:** Validate exponential decay over distance. Turbid water reduces return strength to 0 quickly.

### TASK-33: SurvivalSuitOxygenBurnRate
- **Source Monolith File:** `HectonPlayerMovement.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/SurvivalSuitOxygenBurnRate.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SurvivalSuitOxygenBurnRateTests.cs`
- **Objective:** Compute oxygen consumption rate based on heart rate, depth, and thruster load.
- **Inputs:** `float baseO2Rate, float movementStaminaBurn, float ambientPressure`
- **Outputs:** `float (Oxygen usage per second)`
- **Constraints:** Pure math.
- **Test Requirement:** Ensure thruster load and high pressure scale consumption exponentially.

### TASK-34: RadiationLeadShieldingCalculator
- **Source Monolith File:** `HectonSurvivalSystem.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/RadiationLeadShieldingCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/RadiationLeadShieldingCalculatorTests.cs`
- **Objective:** Calculate radiation dose reduction after traveling through shielding materials of specific thickness.
- **Inputs:** `float rawRadiationLevel, float leadThicknessCm, float shieldingQuality`
- **Outputs:** `float (Absorbed radiation dose)`
- **Constraints:** Exponential decay formula.
- **Test Requirement:** Validate that thickness of 0 has no effect; high thickness reduces dose to baseline.

### TASK-35: AmbientTemperatureDepthGradientCalculator
- **Source Monolith File:** `HectonAtmosphereManager.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/AmbientTemperatureDepthGradientCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/AmbientTemperatureDepthGradientCalculatorTests.cs`
- **Objective:** Calculate water temperature based on depth gradient and geographic latitude.
- **Inputs:** `float surfaceTemp, float depth, float latitudeDegrees`
- **Outputs:** `float (Temperature Celsius)`
- **Constraints:** Pure thermodynamic formula.
- **Test Requirement:** Verify temperature drops as depth increases, capping at deep-sea floor temperatures.

### TASK-36: HudCrushDepthWarningUrgencyCalculator
- **Source Monolith File:** `HUDNotification.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/HudCrushDepthWarningUrgencyCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/HudCrushDepthWarningUrgencyCalculatorTests.cs`
- **Objective:** Calculate the alarm intensity scalar based on depth velocity and distance to crush limits.
- **Inputs:** `float currentDepth, float crushDepth, float verticalSpeed`
- **Outputs:** `float (Warning level 0.0 to 1.0)`
- **Constraints:** Pure HUD logic math.
- **Test Requirement:** Descent speed towards crush depth must trigger high warning urgency early.

### TASK-37: PowerGridResourceDistributorCalculator
- **Source Monolith File:** `PowerGridManager.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/PowerGridResourceDistributorCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/PowerGridResourceDistributorCalculatorTests.cs`
- **Objective:** Allocate generator output to nodes based on priority lists when in deficit.
- **Inputs:** `float generatedPower, float[] nodeDemands, int[] nodePriorities`
- **Outputs:** `float[] (Allocated power per node)`
- **Constraints:** Pure algorithmic array distributor.
- **Test Requirement:** Verify critical nodes receive full power. Low priority nodes drop first in a deficit.

### TASK-38: BatteryChargeEfficiencyCurveCalculator
- **Source Monolith File:** `PowerNode.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/BatteryChargeEfficiencyCurveCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/BatteryChargeEfficiencyCurveCalculatorTests.cs`
- **Objective:** Calculate battery charge delta factoring in thermal inefficiencies at high capacities.
- **Inputs:** `float currentCharge, float maxCapacity, float chargePower, float deltaTime`
- **Outputs:** `float (New charge value)`
- **Constraints:** Non-linear charging math.
- **Test Requirement:** Verify charging efficiency drops significantly as battery capacity exceeds 90%.

### TASK-39: LaserCutterVoxelDamageCalculator
- **Source Monolith File:** `LaserCutter.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/LaserCutterVoxelDamageCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/LaserCutterVoxelDamageCalculatorTests.cs`
- **Objective:** Calculate damage applied to a terrain voxel based on laser heat, distance, and voxel hardness.
- **Inputs:** `float laserPower, float distance, float materialHardness`
- **Outputs:** `float (Damage applied)`
- **Constraints:** Pure math.
- **Test Requirement:** Distance beyond max range returns 0 damage. Hardness absorbs nominal heat values.

### TASK-40: TerrainSeamDitherAlphaCalculator
- **Source Monolith File:** `SeamGapDitherRenderer.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/TerrainSeamDitherAlphaCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/TerrainSeamDitherAlphaCalculatorTests.cs`
- **Objective:** Calculate pixel dither alpha threshold to smooth mesh seam transitions using a 4x4 matrix.
- **Inputs:** `int x, int y, float blendFactor01`
- **Outputs:** `float (Dither transparency output)`
- **Constraints:** Matrix lookups and math.
- **Test Requirement:** Confirm grid pattern at blend = 0.5. Verify boundaries are locked to 0.0 and 1.0.

### TASK-41: AudioDistanceAttenuationCurveCalculator
- **Source Monolith File:** `SpatialAudioManager.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/AudioDistanceAttenuationCurveCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/AudioDistanceAttenuationCurveCalculatorTests.cs`
- **Objective:** Calculate decibel volume attenuation over distance based on deep-sea acoustic absorption.
- **Inputs:** `float initialDb, float distance, float absorptionRateDbPerMeter`
- **Outputs:** `float (Resulting volume Db)`
- **Constraints:** Logarithmic acoustic formulas.
- **Test Requirement:** Verify correct logarithmic volume drop-off. Zero distance returns initialDb.

### TASK-42: AcousticZoneReverbDecay
- **Source Monolith File:** `AcousticZoneController.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/AcousticZoneReverbDecay.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/AcousticZoneReverbDecayTests.cs`
- **Objective:** Calculate reverberation decay time (RT60) based on cave structural volume and absorption surfaces.
- **Inputs:** `float volumeM3, float surfaceAreaM2, float averageAbsorptionCoefficient`
- **Outputs:** `float (Decay time in seconds)`
- **Constraints:** Sabine reverberation equation.
- **Test Requirement:** Highly reflective small caves yield high reverb times. Absorbent surfaces shorten RT60.

### TASK-43: VoxelSdfTrilinearInterpolationCalculator
- **Source Monolith File:** `HectonVoxelVolume.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/VoxelSdfTrilinearInterpolationCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/VoxelSdfTrilinearInterpolationCalculatorTests.cs`
- **Objective:** Calculate interpolated Signed Distance Field (SDF) value at arbitrary coordinates inside a voxel cube.
- **Inputs:** `float[,,] cornerValues, float localX, float localY, float localZ`
- **Outputs:** `float (Interpolated distance value)`
- **Constraints:** Trilinear mathematical interpolation formulas.
- **Test Requirement:** Coordinates matching corners exactly must return raw corner values. Middle coordinate returns average.

### TASK-44: VoxelExplosionDeformationVolumeCalculator
- **Source Monolith File:** `VoxelDeformationSmokeTester.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/VoxelExplosionDeformationVolumeCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/VoxelExplosionDeformationVolumeCalculatorTests.cs`
- **Objective:** Calculate SDF changes in voxel grids affected by explosive pressure waves.
- **Inputs:** `float currentSdf, float distanceToEpicenter, float explosionRadius, float blastForce`
- **Outputs:** `float (New SDF value)`
- **Constraints:** Mathematical wave propagation and displacement curves.
- **Test Requirement:** Ensure distance > explosionRadius yields no change. Epicenter shifts toward maximum negative SDF (hollowed).

### TASK-45: SaveDataBinaryChecksumCalculator
- **Source Monolith File:** `SaveBinaryPayloadCodec.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/SaveDataBinaryChecksumCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SaveDataBinaryChecksumCalculatorTests.cs`
- **Objective:** Calculate a fast Adler32 or custom checksum hash for a byte array payload to secure saves.
- **Inputs:** `byte[] data`
- **Outputs:** `uint (32-bit checksum hash)`
- **Constraints:** Adler32 pure C# algorithm. Do NOT use external cryptographic libraries.
- **Test Requirement:** Ensure identical byte arrays produce matching hashes. A single byte change alters hash significantly.

### TASK-46: DecompressionNitrogenLoadCalculator
- **Source Monolith File:** `Shinobu namespace / Physiology`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/DecompressionNitrogenLoadCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/DecompressionNitrogenLoadCalculatorTests.cs`
- **Objective:** Calculate tissue nitrogen loading pressure during rapid ascents to predict bends (Arcade iteration).
- **Inputs:** `float currentLoad, float breathingGasPressure, float halflimeMinutes, float deltaMinutes`
- **Outputs:** `float (New nitrogen loading pressure)`
- **Constraints:** Haldanean decompression formula (simplified for arcade gameplay).
- **Test Requirement:** Slow ascent allows safe off-gassing. Extreme ascents push tissue load beyond saturation limits.

### TASK-47: HypoxiaVisorBlurIntensityCalculator
- **Source Monolith File:** `HectonPlayerMovement.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/HypoxiaVisorBlurIntensityCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/HypoxiaVisorBlurIntensityCalculatorTests.cs`
- **Objective:** Compute blur and chromatic aberration intensity scalars for HUD visualization during low-O2 states.
- **Inputs:** `float oxygenLevel01, float elapsedSeconds, float recoveryRate`
- **Outputs:** `float2 (X=Blur magnitude, Y=Chromatic intensity)`
- **Constraints:** Pure math functions.
- **Test Requirement:** O2 levels above 0.8 yield 0.0 blur. O2 levels below 0.2 scale blur exponentially with time.

### TASK-48: StorageAutosorterCalculator
- **Source Monolith File:** `PDAInventoryTab.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/StorageAutosorterCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/StorageAutosorterCalculatorTests.cs`
- **Objective:** Calculate grid placement indices for auto-sorting item lists by category and dimensions.
- **Inputs:** `int[] itemCategories, int[] itemWidths, int[] itemHeights, int gridWidth, int gridHeight`
- **Outputs:** `int[] (1D flattened grid index coordinates for each item, or -1 if no fit)`
- **Constraints:** 2D Bin packing mathematical heuristics.
- **Test Requirement:** Verify items fit inside grid bounds and do not overlap.

### TASK-49: FabricatorBuildProgressCurveCalculator
- **Source Monolith File:** `Fabricator.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/FabricatorBuildProgressCurveCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/FabricatorBuildProgressCurveCalculatorTests.cs`
- **Objective:** Compute fabrication progress increment factoring in tool temperature and power supply level.
- **Inputs:** `float currentProgress, float rawBuildTime, float toolTemp, float powerLevel01, float deltaSeconds`
- **Outputs:** `float (New progress percentage 0.0 to 1.0)`
- **Constraints:** Mathematical progression curve.
- **Test Requirement:** Power level 0 pauses progress. Overheated tools slow down fabrication rate.

### TASK-50: ProceduralFoliageScatterBudgetCalculator
- **Source Monolith File:** `ScatterBudgetController.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/ProceduralFoliageScatterBudgetCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ProceduralFoliageScatterBudgetCalculatorTests.cs`
- **Objective:** Calculate maximum instance budget count for procedural seabed kelp based on target frame rate performance.
- **Inputs:** `float targetFps, float currentFps, int baseBudget, float qualityWeight`
- **Outputs:** `int (Instance budget limit)`
- **Constraints:** Pure mathematical feedback loop calculation.
- **Test Requirement:** Low quality weight heavily limits max budget. Poor FPS throttles budget downwards.

### TASK-51: InventorySalinityCorrosionCalculator
- **Source Monolith File:** `PlayerInventory.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/InventorySalinityCorrosionCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/InventorySalinityCorrosionCalculatorTests.cs`
- **Objective:** Calculate inventory item durability decay over time based on biome salinity factors.
- **Inputs:** `float currentDurability01, float salinityFactor, float baseDegradationRate, float elapsedSeconds`
- **Outputs:** `float (New durability value 0.0 to 1.0)`
- **Constraints:** Pure C# math.
- **Test Requirement:** 0 salinity causes 0 decay. Verify logarithmic acceleration of corrosion in chemosynthetic brine zones.

### TASK-52: InventoryItemDefragmentationConsolidationCalculator
- **Source Monolith File:** `PlayerInventory.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/InventoryItemDefragmentationConsolidationCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/InventoryItemDefragmentationConsolidationCalculatorTests.cs`
- **Objective:** Resolve item stacking and consolidations to clear empty inventory slots.
- **Inputs:** `uint[] itemIds, int[] itemCounts, int[] maxStackSizes`
- **Outputs:** `int[] (Calculated slot-index displacements and consolidated stack size changes)`
- **Constraints:** Pure C# list/array packing algorithms.
- **Test Requirement:** Consolidate multiple small stacks of titanium into maximum capacity stacks; verify slot counts reduce.

### TASK-53: BranchlessReactiveItemChemistryCalculator
- **Source Monolith File:** `PlayerInventory.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/BranchlessReactiveItemChemistryCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/BranchlessReactiveItemChemistryCalculatorTests.cs`
- **Objective:** Compute reactivity flags for adjacent chemical items in inventory slots without branching logic.
- **Inputs:** `uint itemAFlags, uint itemBFlags, uint reactionMatrix`
- **Outputs:** `uint (Reaction code mask, 0 if inert)`
- **Constraints:** Strictly branchless bitwise operations only.
- **Test Requirement:** Validate reactive combinations return expected bitmasks; inert items return 0 without triggering jumps.

### TASK-54: MaelstromSpatialWarpPullCalculator
- **Source Monolith File:** `HectonFluidEngine.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/MaelstromSpatialWarpPullCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/MaelstromSpatialWarpPullCalculatorTests.cs`
- **Objective:** Calculate the gravitational suction warp force pulling entities toward maelstrom cores.
- **Inputs:** `Vector3 objectPos, Vector3 corePos, float coreRadius, float warpStrength`
- **Outputs:** `Vector3 (Suction pull vector)`
- **Constraints:** System.Numerics vector math.
- **Test Requirement:** Suction increases exponentially as distance approaches coreRadius; zero pull beyond twice the coreRadius.

### TASK-55: AbyssalVortexAngularTorqueCalculator
- **Source Monolith File:** `HectonFluidEngine.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/AbyssalVortexAngularTorqueCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/AbyssalVortexAngularTorqueCalculatorTests.cs`
- **Objective:** Calculate the rotational angular torque vector applied to a submarine inside an underwater vortex.
- **Inputs:** `Vector3 hullPos, Vector3 vortexCenter, Vector3 vortexAxis, float angularVelocity, float hullMass`
- **Outputs:** `Vector3 (Angular velocity impulse torque vector)`
- **Constraints:** System.Numerics cross products and math.
- **Test Requirement:** Ensure torque vector aligns with vortex axis; magnitude scales with distance from center core.

### TASK-56: CavitationBurstShockwaveForce
- **Source Monolith File:** `HectonFluidEngine.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/CavitationBurstShockwaveForce.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/CavitationBurstShockwaveForceTests.cs`
- **Objective:** Calculate pressure wave shock force reaching a body at a distance from a cavitation event.
- **Inputs:** `Vector3 bodyPos, Vector3 burstCenter, float burstEnergy, float waterDensity`
- **Outputs:** `Vector3 (Blast acceleration impulse vector)`
- **Constraints:** Spherical pressure wave propagation formulas.
- **Test Requirement:** Validate inverse-cube falloff over distance. Energy scales impulse linearly.

### TASK-57: SurfaceCurrentWindshearVector
- **Source Monolith File:** `HectonFluidEngine.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/SurfaceCurrentWindshearVector.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SurfaceCurrentWindshearVectorTests.cs`
- **Objective:** Calculate ocean surface current vectors generated by surface winds, decaying with depth.
- **Inputs:** `Vector2 windVector, float windStrength, float depth, float decayRate`
- **Outputs:** `Vector3 (Current flow vector)`
- **Constraints:** System.Numerics vector math.
- **Test Requirement:** Surface depth = 0 has maximum wind drag flow. Deep depths drop to zero current flow.

### TASK-58: CeilingConcavityAirPocketVolumeCalculator
- **Source Monolith File:** `HectonVoxelEngine.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/CeilingConcavityAirPocketVolumeCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/CeilingConcavityAirPocketVolumeCalculatorTests.cs`
- **Objective:** Compute if a ceiling shape forms a candidate volume to trap breathable air under water.
- **Inputs:** `Vector3 normal, float ceilingDepth, float waterlineClearance, float boundaryRadius`
- **Outputs:** `float (Trapped air volume cubic meters)`
- **Constraints:** Pure geometric volume math.
- **Test Requirement:** Flat ceilings yield 0 volume. Inverted dome ceiling normals yield maximum pocket volumes.

### TASK-59: VoxelCellDirtystateBitHashingCalculator
- **Source Monolith File:** `HectonVoxelEngine.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/VoxelCellDirtystateBitHashingCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/VoxelCellDirtystateBitHashingCalculatorTests.cs`
- **Objective:** Calculate spatial bit hashing index to track modified/dirty cell boundaries in the voxel grid.
- **Inputs:** `int cellX, int cellY, int cellZ, int gridDimension`
- **Outputs:** `uint (32-bit hash index)`
- **Constraints:** Fast spatial bitwise hash algorithms.
- **Test Requirement:** Ensure adjacent cells produce widely different hash indices. Identical coordinates yield identical index.

### TASK-60: SubseaVehicleDopplerReverbShiftCalculator
- **Source Monolith File:** `SpatialAudioManager.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/SubseaVehicleDopplerReverbShiftCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SubseaVehicleDopplerReverbShiftCalculatorTests.cs`
- **Objective:** Calculate relative pitch frequency shift for moving audio sources in water.
- **Inputs:** `float initialFrequency, Vector3 emitterPos, Vector3 emitterVel, Vector3 listenerPos, Vector3 listenerVel, float speedOfSoundInWater`
- **Outputs:** `float (Shifted frequency)`
- **Constraints:** Acoustic Doppler equation.
- **Test Requirement:** Emitter moving towards listener increases pitch; emitter moving away decreases pitch.

### TASK-61: SoundObstructionLowpassCutoffCalculator
- **Source Monolith File:** `SpatialAudioManager.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/SoundObstructionLowpassCutoffCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SoundObstructionLowpassCutoffCalculatorTests.cs`
- **Objective:** Compute low-pass filter frequency cutoff based on material thickness obstructing the listener.
- **Inputs:** `float baseCutoffHz, float obstructionThicknessCm, float materialDensity`
- **Outputs:** `float (Target lowpass cutoff frequency Hz)`
- **Constraints:** Acoustic attenuation equations.
- **Test Requirement:** Thick, high-density materials drop cutoff frequency to sub-100Hz muffles.

### TASK-62: HuffmanRleSaveDataCompressorCalculator
- **Source Monolith File:** `SaveBinaryPayloadCodec.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/HuffmanRleSaveDataCompressorCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/HuffmanRleSaveDataCompressorCalculatorTests.cs`
- **Objective:** Implement standard Run-Length Encoding (RLE) to pack sequential static block arrays in save files.
- **Inputs:** `byte[] uncompressedData`
- **Outputs:** `byte[] (Compressed output payload)`
- **Constraints:** Pure RLE C# algorithms. No external zip/gzip dependencies.
- **Test Requirement:** Verify massive arrays of identical bytes compress to < 1% size. Lossless recovery.

### TASK-63: SaveDeltaVoxelStatePackingCalculator
- **Source Monolith File:** `SaveBinaryStorage.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/SaveDeltaVoxelStatePackingCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SaveDeltaVoxelStatePackingCalculatorTests.cs`
- **Objective:** Calculate changed-difference payload arrays to store only delta modifications of the voxel terrain.
- **Inputs:** `byte[] originalBlocks, byte[] modifiedBlocks`
- **Outputs:** `byte[] (Delta package containing index offsets and new values)`
- **Constraints:** Pure array serialization math.
- **Test Requirement:** If original equals modified, delta package size must be exactly 0.

### TASK-64: AtmosphericRoomGasDiffusionCalculator
- **Source Monolith File:** `SubmarineAtmosphereSystem.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/AtmosphericRoomGasDiffusionCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/AtmosphericRoomGasDiffusionCalculatorTests.cs`
- **Objective:** Calculate gaseous pressure mix exchange between two rooms connected by an unsealed bulkhead door.
- **Inputs:** `float roomAO2, float roomBO2, float roomACO2, float roomBCO2, float doorAreaM2, float deltaTime`
- **Outputs:** `float2 (X=O2 transfer amount, Y=CO2 transfer amount)`
- **Constraints:** Fick's law of diffusion equations.
- **Test Requirement:** Equal gas levels result in 0 transfer. Larger door area scales diffusion rate linearly.

### TASK-65: ModuleThermalDissipationRate
- **Source Monolith File:** `SubmarineAtmosphereSystem.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/ModuleThermalDissipationRate.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ModuleThermalDissipationRateTests.cs`
- **Objective:** Calculate room temperature changes caused by active power modules, factoring in cooling efficiency.
- **Inputs:** `float currentRoomTemp, float moduleWattage, float coolantFlowRate, float roomVolumeM3, float deltaTime`
- **Outputs:** `float (Temperature delta Celsius)`
- **Constraints:** Thermodynamic heat equations.
- **Test Requirement:** High coolant flow rate must absorb the reactor heat output, stabilizing room temperature.

### TASK-66: FaunaObstacleAvoidanceVector
- **Source Monolith File:** `HectonDirectorAI.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/FaunaObstacleAvoidanceVector.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/FaunaObstacleAvoidanceVectorTests.cs`
- **Objective:** Compute steering offset forces when approaching obstacles, prioritizing normal vector pushes.
- **Inputs:** `Vector3 forwardDirection, Vector3 hitNormal, float distanceToObstacle, float avoidanceRadius`
- **Outputs:** `Vector3 (Avoidance steering vector)`
- **Constraints:** System.Numerics math.
- **Test Requirement:** Verify vector points away from obstacle normal. Force scales inversely with distance.

### TASK-67: FaunaSensoryDetectionRangeCalculator
- **Source Monolith File:** `FaunaDirector.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/FaunaSensoryDetectionRangeCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/FaunaSensoryDetectionRangeCalculatorTests.cs`
- **Objective:** Calculate dynamic visibility and hearing thresholds of a predator looking for prey under water.
- **Inputs:** `Vector3 predatorPos, Vector3 preyPos, float waterTurbidity, float preyMovementSpeed`
- **Outputs:** `bool (Prey Detected)`
- **Constraints:** Pure trigonometry and math.
- **Test Requirement:** Turbid water drastically reduces visual range. Fast moving prey increases hearing detection range.

### TASK-68: FaunaPheromoneTrackingVector
- **Source Monolith File:** `FaunaDirector.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/FaunaPheromoneTrackingVector.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/FaunaPheromoneTrackingVectorTests.cs`
- **Objective:** Calculate steering force vector pointing toward dense trail coordinates to follow migratory paths.
- **Inputs:** `Vector3 faunaPos, Vector3[] trailCoords, float[] trailStrengths`
- **Outputs:** `Vector3 (Attraction vector)`
- **Constraints:** System.Numerics vector path solver.
- **Test Requirement:** Fauna must steer toward coordinate with highest trail strength nearby.

### TASK-69: HypercapniaToxicityDamageCurveCalculator
- **Source Monolith File:** `Shinobu namespace / Physiology`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/HypercapniaToxicityDamageCurveCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/HypercapniaToxicityDamageCurveCalculatorTests.cs`
- **Objective:** Calculate physiological damage to character organs due to high CO2 partial pressure in breathing loop.
- **Inputs:** `float co2PartialPressureKPa, float exposureTimeSeconds, float thresholdKPa`
- **Outputs:** `float (Physiological damage delta)`
- **Constraints:** Pure math.
- **Test Requirement:** Partial pressures below threshold must result in 0 damage. Excessive exposure escalates damage exponentially.

### TASK-70: NitrogenNarcosisCriticalDepthCalculator
- **Source Monolith File:** `HectonPlayerMovement.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/NitrogenNarcosisCriticalDepthCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/NitrogenNarcosisCriticalDepthCalculatorTests.cs`
- **Objective:** Calculate nitrogen partial pressure in blood relative to hydrostatic depth pressure, establishing safety limit thresholds.
- **Inputs:** `float currentDepthMeters, float oxygenFraction, float nitrogenFraction`
- **Outputs:** `float (Narcosis intensity scalar 0.0 to 1.0)`
- **Constraints:** Hydrostatic pressure and Dalton's gas law equations.
- **Test Requirement:** Standard air (79% N2) must trigger narcosis symptoms starting at 30 meters depth.

### TASK-71: RockAlignmentSplineNormalCalculator
- **Source Monolith File:** `HectonRockManager.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/RockAlignmentSplineNormalCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/RockAlignmentSplineNormalCalculatorTests.cs`
- **Objective:** Calculate orientation quaternions to align procedurally generated rock meshes along spline normal vectors.
- **Inputs:** `Vector3 splineTangent, Vector3 terrainNormal`
- **Outputs:** `float[] (4-element float array representing orientation quaternion)`
- **Constraints:** Vector/Quaternion pure math.
- **Test Requirement:** Verify quaternion correctly aligns rock 'up' axis to match terrainNormal, and 'forward' along splineTangent.

### TASK-72: PoissondiscLandmarkSpacingSolver
- **Source Monolith File:** `WorldContentDirector.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/PoissondiscLandmarkSpacingSolver.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/PoissondiscLandmarkSpacingSolverTests.cs`
- **Objective:** Evaluate spatial coordinates to check if a new landmark location respects minimum distance boundaries from existing structures.
- **Inputs:** `Vector3 candidateCoord, Vector3[] existingCoords, float minDistance`
- **Outputs:** `bool (Placement Allowed)`
- **Constraints:** Pure C# distance checks.
- **Test Requirement:** Reject candidate if inside minDistance of any existing coordinate. Allow if clear.

### TASK-73: VoxelMeshHeightSeamBlendCalculator
- **Source Monolith File:** `WorldGenerativeGeologyTerrainSeamApplier.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/VoxelMeshHeightSeamBlendCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/VoxelMeshHeightSeamBlendCalculatorTests.cs`
- **Objective:** Compute blending height weights for voxel vertices intersecting heightmap terrain meshes to eliminate gaps.
- **Inputs:** `float voxelVertexY, float terrainHeightY, float blendWidth`
- **Outputs:** `float (Blend factor 0.0 to 1.0 to weight SDF offset)`
- **Constraints:** Pure math interpolation.
- **Test Requirement:** Distance > blendWidth yields 0.0 weight. Exact match yields 1.0.

### TASK-74: BeaconNetworkSignalAttenuationCalculator
- **Source Monolith File:** `BeaconNetworkSystem.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/BeaconNetworkSignalAttenuationCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/BeaconNetworkSignalAttenuationCalculatorTests.cs`
- **Objective:** Calculate network signal strength between remote transceiver beacons through water of varying salinity.
- **Inputs:** `float transmitPowerDb, float distance, float salinityPpt`
- **Outputs:** `float (Received signal strength Db)`
- **Constraints:** RF subsea propagation formulas.
- **Test Requirement:** High salinity increases absorption rate drastically. Received strength drops logarithmically.

### TASK-75: SuitBatteryThermalEfficiencyCalculator
- **Source Monolith File:** `HectonSurvivalSystem.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/SuitBatteryThermalEfficiencyCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SuitBatteryThermalEfficiencyCalculatorTests.cs`
- **Objective:** Compute battery discharge penalty multiplier under extreme ambient temperatures (caves/abyss).
- **Inputs:** `float ambientTemperatureCelsius, float batteryDrainRate`
- **Outputs:** `float (Temperature-adjusted discharge rate multiplier)`
- **Constraints:** Battery chemistry thermal curve math.
- **Test Requirement:** Temperatures below freezing (0C) increase battery drain rate by 50%. Ideal temp (20C) has 1.0 multiplier.

### TASK-076: WallSlideFrictionCalculator
- **Source Monolith File:** `HectonPlayerMovement.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/WallSlideFrictionCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/WallSlideFrictionCalculatorTests.cs`
- **Objective:** Extract wall-slide friction deceleration math from HectonPlayerMovement.cs. Player slides down vertical surfaces with friction coefficient per material type.
- **Inputs:** `float slideVelocity, float wallFrictionCoeff, float gravityScale, float deltaTime`
- **Outputs:** `float (resulting slide velocity after friction deceleration)`
- **Constraints:** Pure C# math. No Rigidbody, no PhysicsMaterial.
- **Test Requirement:** Zero friction = free fall. Max friction = instant stop. Verify smooth deceleration curve.

### TASK-077: LedgeGrabImpulseCalculator
- **Source Monolith File:** `HectonPlayerMovement.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/LedgeGrabImpulseCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/LedgeGrabImpulseCalculatorTests.cs`
- **Objective:** Extract ledge grab velocity cancellation and pull-up impulse calculation from HectonPlayerMovement.cs.
- **Inputs:** `Vector3 playerVelocity, Vector3 ledgeNormal, float pullUpForce, float cancelFraction`
- **Outputs:** `Vector3 (new velocity after ledge grab impulse applied)`
- **Constraints:** Pure C#, System.Numerics.Vector3.
- **Test Requirement:** Vertical velocity fully cancelled on grab. Lateral preserved. Pull-up adds upward component.

### TASK-078: CrouchCapsuleLerp
- **Source Monolith File:** `HectonPlayerMovement.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/CrouchCapsuleLerp.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/CrouchCapsuleLerpTests.cs`
- **Objective:** Extract capsule height lerp math for crouch/uncrouch transitions from HectonPlayerMovement.cs.
- **Inputs:** `float currentHeight, float targetHeight, float crouchSpeed, float deltaTime`
- **Outputs:** `float (interpolated capsule height)`
- **Constraints:** Pure C# math. No CharacterController, no Collider.
- **Test Requirement:** Reaches target height within expected time. Clamps to min/max bounds.

### TASK-079: SprintStaminaGate
- **Source Monolith File:** `HectonPlayerMovement.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/SprintStaminaGate.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SprintStaminaGateTests.cs`
- **Objective:** Extract sprint eligibility gating logic from HectonPlayerMovement.cs. Sprint blocked below stamina threshold, re-enabled with hysteresis.
- **Inputs:** `float currentStamina, float sprintEnterThreshold, float sprintExitThreshold, bool isCurrentlySprinting`
- **Outputs:** `bool (canSprint)`
- **Constraints:** Hysteresis required. Pure C#.
- **Test Requirement:** Below exit threshold: sprint stops. Above enter threshold: sprint allowed. Hysteresis prevents rapid toggle.

### TASK-080: VariableHeightJumpCalculator
- **Source Monolith File:** `HectonPlayerMovement.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/VariableHeightJumpCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/VariableHeightJumpCalculatorTests.cs`
- **Objective:** Extract variable-height jump impulse from HectonPlayerMovement.cs. Jump height varies with how long button is held, up to maxJumpTime.
- **Inputs:** `float heldTime, float maxJumpTime, float minJumpVelocity, float maxJumpVelocity`
- **Outputs:** `float (vertical jump velocity)`
- **Constraints:** Pure C# math. Smoothstep interpolation preferred.
- **Test Requirement:** Zero hold = minJumpVelocity. Full hold = maxJumpVelocity. Mid hold = proportional.

### TASK-081: SomaticDragCurveCalculator
- **Source Monolith File:** `PlayerKinematicsRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/SomaticDragCurveCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SomaticDragCurveCalculatorTests.cs`
- **Objective:** Extract somatic drag curve from PlayerKinematicsRuntime.cs. Drag is non-linear, depends on depth and suit integrity.
- **Inputs:** `float speed, float depthMeters, float suitIntegrity01, float baseDragCoeff`
- **Outputs:** `float (drag deceleration m/s^2)`
- **Constraints:** Pure C#. Quadratic drag at surface, cubic at depth.
- **Test Requirement:** Zero speed = zero drag. Max depth + broken suit = peak drag. Continuity at depth transition.

### TASK-082: InertiaTransferCalculator
- **Source Monolith File:** `PlayerKinematicsRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/InertiaTransferCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/InertiaTransferCalculatorTests.cs`
- **Objective:** Extract inertia transfer when player mounts/dismounts vehicles from PlayerKinematicsRuntime.cs.
- **Inputs:** `Vector3 vehicleVelocity, Vector3 playerVelocity, float transferFraction, float playerMass, float vehicleMass`
- **Outputs:** `Vector3 (player velocity after momentum transfer)`
- **Constraints:** Pure C#. Conservation of momentum.
- **Test Requirement:** 100% transfer: player inherits vehicle velocity. 0%: no change. Momentum conserved.

### TASK-083: GroundSnapDistanceCalculator
- **Source Monolith File:** `PlayerKinematicsRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/GroundSnapDistanceCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/GroundSnapDistanceCalculatorTests.cs`
- **Objective:** Extract ground snap distance calculation from PlayerKinematicsRuntime.cs. Determines if player should snap based on step height and slope.
- **Inputs:** `float distanceToGround, float maxStepHeight, float slopeAngleDeg, float maxWalkableSlopeDeg`
- **Outputs:** `bool shouldSnap, float snapDistance`
- **Constraints:** Pure C# math. No raycasts.
- **Test Requirement:** Flat ground within step height: snap. Over max slope: no snap. Edge cases at exact threshold.

### TASK-084: StrafeAngleBlendWeightCalculator
- **Source Monolith File:** `PlayerKinematicsRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/StrafeAngleBlendWeightCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/StrafeAngleBlendWeightCalculatorTests.cs`
- **Objective:** Extract strafe animation blend weight from PlayerKinematicsRuntime.cs. Blend based on velocity direction vs facing.
- **Inputs:** `Vector3 velocityDir, Vector3 facingDir, float fullStrafeAngleDeg`
- **Outputs:** `float strafeBlendWeight (-1.0 left, 0 forward, 1.0 right)`
- **Constraints:** Pure C# math. System.Numerics.Vector3.
- **Test Requirement:** Velocity == facing: 0. Velocity 90 deg right: 1.0. Velocity 90 deg left: -1.0.

### TASK-085: BuoyancyDensityRatioMath
- **Source Monolith File:** `HydrodynamicKccRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/BuoyancyDensityRatioMath.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/BuoyancyDensityRatioMathTests.cs`
- **Objective:** Extract buoyancy lift force from fluid density ratio in HydrodynamicKccRuntime.cs. Archimedes principle.
- **Inputs:** `float playerDensity, float fluidDensity, float displacedVolume, float gravity`
- **Outputs:** `float (net buoyancy force in Newtons)`
- **Constraints:** Pure C#. Archimedes principle.
- **Test Requirement:** Equal densities: zero net force. Less dense: positive lift. More dense: negative (sinks).

### TASK-086: OceanCurrentDragCalculator
- **Source Monolith File:** `HydrodynamicKccRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/OceanCurrentDragCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/OceanCurrentDragCalculatorTests.cs`
- **Objective:** Extract ocean current drag vector applied to player from HydrodynamicKccRuntime.cs.
- **Inputs:** `Vector3 oceanCurrentVelocity, Vector3 playerVelocity, float dragCoeff, float crossSectionalArea`
- **Outputs:** `Vector3 (drag force vector)`
- **Constraints:** Pure C# math.
- **Test Requirement:** Player with current: near zero drag. Against current: max drag. Perpendicular: lateral drag.

### TASK-087: PressureCrushDamageModel
- **Source Monolith File:** `HydrodynamicKccRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/PressureCrushDamageModel.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/PressureCrushDamageModelTests.cs`
- **Objective:** Extract pressure-induced suit crush damage rate from HydrodynamicKccRuntime.cs. Exponential below crush depth.
- **Inputs:** `float depthMeters, float crushDepthThreshold, float maxDamageRate, float exponent`
- **Outputs:** `float (damagePerSecond)`
- **Constraints:** Pure C#. No damage application.
- **Test Requirement:** Above crush depth: zero. At threshold: near zero. Double depth: exponential spike.

### TASK-088: ThermoclineResistanceCalculator
- **Source Monolith File:** `HydrodynamicKccRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/ThermoclineResistanceCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ThermoclineResistanceCalculatorTests.cs`
- **Objective:** Extract thermocline layer resistance force from HydrodynamicKccRuntime.cs. Density change creates brief resistance spike.
- **Inputs:** `float currentDepth, float thermoclineDepth, float thermoclineThickness, float playerSpeed, float resistanceForce`
- **Outputs:** `float (resistance multiplier 0.0-1.0)`
- **Constraints:** Pure C#.
- **Test Requirement:** Far above/below thermocline: zero. Inside: peak resistance. Smooth falloff at edges.

### TASK-089: SuitO2ConsumptionModel
- **Source Monolith File:** `ShinobuPhysiologyRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/SuitO2ConsumptionModel.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SuitO2ConsumptionModelTests.cs`
- **Objective:** Extract oxygen consumption rate from ShinobuPhysiologyRuntime.cs. Scales with exertion and suit damage.
- **Inputs:** `float exertionLevel01, float suitSealIntegrity01, float baseO2ConsumptionRate, float depthAtm`
- **Outputs:** `float (O2 consumed per second)`
- **Constraints:** Pure C#.
- **Test Requirement:** Rest + intact suit: base rate. Max exertion + broken seal: 5x base. Linear scaling with exertion.

### TASK-090: Co2ScrubberEfficiencyModel
- **Source Monolith File:** `ShinobuPhysiologyRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/Co2ScrubberEfficiencyModel.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/Co2ScrubberEfficiencyModelTests.cs`
- **Objective:** Extract CO2 scrubber efficiency decay from ShinobuPhysiologyRuntime.cs. Degrades with temperature and usage hours.
- **Inputs:** `float usageHours, float ambientTempCelsius, float maxEfficiency, float degradationRate`
- **Outputs:** `float (current scrubber efficiency 0.0-1.0)`
- **Constraints:** Pure C#.
- **Test Requirement:** Fresh scrubber: max efficiency. After rated hours: below threshold. High temp accelerates decay.

### TASK-091: NitrogenNarcosisModel
- **Source Monolith File:** `ShinobuPhysiologyRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/NitrogenNarcosisModel.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/NitrogenNarcosisModelTests.cs`
- **Objective:** Extract nitrogen narcosis impairment factor from ShinobuPhysiologyRuntime.cs. Logarithmic impairment with depth and time.
- **Inputs:** `float depthMeters, float timeAtDepthSeconds, float narcosisOnsetDepth, float maxImpairment`
- **Outputs:** `float (impairmentFactor 0.0-1.0)`
- **Constraints:** Pure C#. Logarithmic curve.
- **Test Requirement:** Above onset depth: zero. 30m below onset: measurable. Double time same depth: higher impairment.

### TASK-092: CoreTempEquilibriumSolver
- **Source Monolith File:** `ShinobuPhysiologyRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/CoreTempEquilibriumSolver.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/CoreTempEquilibriumSolverTests.cs`
- **Objective:** Extract player core temperature equilibrium solver from ShinobuPhysiologyRuntime.cs.
- **Inputs:** `float coreTempCelsius, float ambientTempCelsius, float suitThermalResistance, float deltaTime`
- **Outputs:** `float (new core temperature after deltaTime)`
- **Constraints:** Pure C# thermal math.
- **Test Requirement:** Core == ambient: no change. Perfect suit: near zero drift. Exposed to 2C water: rapid drop.

### TASK-093: HeartRateExertionModel
- **Source Monolith File:** `ShinobuPhysiologyRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/HeartRateExertionModel.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/HeartRateExertionModelTests.cs`
- **Objective:** Extract heart rate model from ShinobuPhysiologyRuntime.cs. HR rises with exertion and stress.
- **Inputs:** `float exertion01, float stressLevel01, float restingHR, float maxHR, float adaptationSpeed, float deltaTime`
- **Outputs:** `float (current heart rate BPM)`
- **Constraints:** Pure C#.
- **Test Requirement:** Resting: approaches restingHR. Max exertion+stress: approaches maxHR. Recovery proportional to adaptationSpeed.

### TASK-094: ArmorPenetrationCalculator
- **Source Monolith File:** `CombatDamageRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/ArmorPenetrationCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ArmorPenetrationCalculatorTests.cs`
- **Objective:** Extract armor penetration formula from CombatDamageRuntime.cs. Penetration based on projectile velocity, mass, armor hardness.
- **Inputs:** `float projectileMass, float impactVelocity, float armorHardness, float armorThickness`
- **Outputs:** `float (penetrationRatio 0.0=blocked, 1.0=full penetration)`
- **Constraints:** Pure C# ballistics math.
- **Test Requirement:** Slow projectile vs thick armor: zero. Fast heavy round vs thin: full penetration.

### TASK-095: ProjectileDamageFalloffCalculator
- **Source Monolith File:** `CombatDamageRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/ProjectileDamageFalloffCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ProjectileDamageFalloffCalculatorTests.cs`
- **Objective:** Extract projectile damage falloff over range from CombatDamageRuntime.cs.
- **Inputs:** `float distanceMeters, float effectiveRange, float maxDamage, float minDamage, float falloffExponent`
- **Outputs:** `float (damage at given range)`
- **Constraints:** Pure C#.
- **Test Requirement:** Zero range: maxDamage. At effectiveRange: midpoint. Beyond 2x: at or below minDamage.

### TASK-096: ExplosionRadialDamageCalculator
- **Source Monolith File:** `CombatDamageRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/ExplosionRadialDamageCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ExplosionRadialDamageCalculatorTests.cs`
- **Objective:** Extract radial explosion damage from CombatDamageRuntime.cs. Inverse-square inside blast radius.
- **Inputs:** `float distanceFromEpicenter, float blastRadius, float peakDamage, float minDamage`
- **Outputs:** `float (damage received)`
- **Constraints:** Pure C#.
- **Test Requirement:** At epicenter: peakDamage. At blast edge: minDamage. Outside radius: zero.

### TASK-097: BleedStackDecayModel
- **Source Monolith File:** `CombatDamageRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/BleedStackDecayModel.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/BleedStackDecayModelTests.cs`
- **Objective:** Extract bleed stack accumulation and decay model from CombatDamageRuntime.cs.
- **Inputs:** `float currentBleedStacks, float newStacksAdded, float decayRatePerSecond, float maxStacks, float deltaTime`
- **Outputs:** `float (new bleed stack count), float (damage this frame from bleed)`
- **Constraints:** Pure C#.
- **Test Requirement:** Max stacks clamp. Decay to zero after no new stacks. Damage proportional to stacks.

### TASK-098: WaterPressureWeaponMultiplier
- **Source Monolith File:** `CombatDamageRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/WaterPressureWeaponMultiplier.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/WaterPressureWeaponMultiplierTests.cs`
- **Objective:** Extract underwater weapon effectiveness penalty from CombatDamageRuntime.cs.
- **Inputs:** `float baseVelocity, float baseRange, float depthMeters, float waterDensity`
- **Outputs:** `float adjustedVelocity, float adjustedRange`
- **Constraints:** Pure C# fluid drag math.
- **Test Requirement:** Surface: no penalty. At 100m: ~50% velocity reduction. Range scales with velocity.

### TASK-099: PredatorStalkSpeedCalculator
- **Source Monolith File:** `FaunaKinematicsRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/PredatorStalkSpeedCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/PredatorStalkSpeedCalculatorTests.cs`
- **Objective:** Extract predator stalk speed from FaunaKinematicsRuntime.cs. Speed suppressed near prey based on distance and awareness.
- **Inputs:** `float distanceToPrey, float stalkRadius, float maxStalkSpeed, float maxChaseSpeed, float preyAwarenessLevel`
- **Outputs:** `float (current movement speed)`
- **Constraints:** Pure C#.
- **Test Requirement:** Far from prey: maxChaseSpeed. Inside stalkRadius: maxStalkSpeed. Prey aware: switches to chase.

### TASK-100: SchoolingSeparationForceCalculator
- **Source Monolith File:** `FaunaKinematicsRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/SchoolingSeparationForceCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SchoolingSeparationForceCalculatorTests.cs`
- **Objective:** Extract schooling fish separation force from FaunaKinematicsRuntime.cs. Boids-style separation.
- **Inputs:** `Vector3 selfPosition, Vector3[] neighborPositions, float separationRadius, float separationForce`
- **Outputs:** `Vector3 (separation steering force)`
- **Constraints:** Pure C#. System.Numerics.Vector3.
- **Test Requirement:** No neighbors: zero. Single neighbor at boundary: small force. Neighbor at center: max repulsion.

### TASK-101: LeviathanTentacleSpringCalculator
- **Source Monolith File:** `LeviathanTentacleVerletSolver.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/LeviathanTentacleSpringCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/LeviathanTentacleSpringCalculatorTests.cs`
- **Objective:** Extract tentacle spring-damper constraint from LeviathanTentacleVerletSolver.cs. Verlet integration for tentacle segments.
- **Inputs:** `Vector3 currentPos, Vector3 prevPos, Vector3 anchorPos, float springStrength, float damping, float deltaTime`
- **Outputs:** `Vector3 (new segment position)`
- **Constraints:** Pure C#. Verlet integration.
- **Test Requirement:** At rest: stable. Displaced: oscillates and settles. Damping=1: critically damped.

### TASK-102: FaunaFleeVectorCalculator
- **Source Monolith File:** `FaunaKinematicsRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/FaunaFleeVectorCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/FaunaFleeVectorCalculatorTests.cs`
- **Objective:** Extract flee vector computation from FaunaKinematicsRuntime.cs. Fauna flees from nearest threat with obstacle avoidance.
- **Inputs:** `Vector3 selfPos, Vector3 threatPos, Vector3[] obstaclePositions, float obstacleAvoidRadius, float fleeBias`
- **Outputs:** `Vector3 (flee direction, normalized)`
- **Constraints:** Pure C#.
- **Test Requirement:** No obstacles: directly away. Obstacle in path: deflected. Multiple threats: average flee.

### TASK-103: ProjectileDropCalculator
- **Source Monolith File:** `BallisticsRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/ProjectileDropCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ProjectileDropCalculatorTests.cs`
- **Objective:** Extract projectile gravity drop from BallisticsRuntime.cs. Trajectory accounts for gravity, angle, drag.
- **Inputs:** `float muzzleVelocity, float launchAngleDeg, float dragCoeff, float gravityMsq, float timeOfFlight`
- **Outputs:** `float (vertical drop in meters at timeOfFlight)`
- **Constraints:** Pure C#.
- **Test Requirement:** Zero angle: pure drop. 45 deg: max range arc. High drag: faster drop than vacuum.

### TASK-104: HarpoonTensionForceCalculator
- **Source Monolith File:** `HarpoonTensionSolver328.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/HarpoonTensionForceCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/HarpoonTensionForceCalculatorTests.cs`
- **Objective:** Extract harpoon tension cable force from HarpoonTensionSolver328.cs. Elastic cable force based on extension.
- **Inputs:** `float currentLength, float restLength, float stiffness, float dampingCoeff, float extensionVelocity`
- **Outputs:** `float (tension force in Newtons)`
- **Constraints:** Pure C#. Hooke's law with damping.
- **Test Requirement:** At rest length: zero. Extended: proportional to stiffness. Oscillation dampens.

### TASK-105: SplashEntryAngleCalculator
- **Source Monolith File:** `BallisticsRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/SplashEntryAngleCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SplashEntryAngleCalculatorTests.cs`
- **Objective:** Extract water entry angle effect on projectile from BallisticsRuntime.cs. Ricochet probability based on angle.
- **Inputs:** `float entryAngleDeg, float projectileMass, float velocity, float waterSurfaceTension`
- **Outputs:** `float riccochetProbability01, float deflectionAngleDeg`
- **Constraints:** Pure C#.
- **Test Requirement:** 90 deg: zero ricochet. Under 10 deg: high ricochet. Heavier: less deflection.

### TASK-106: IdealGasPressureSolver
- **Source Monolith File:** `GasDynamicsSolver.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/IdealGasPressureSolver.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/IdealGasPressureSolverTests.cs`
- **Objective:** Extract ideal gas law pressure from GasDynamicsSolver.cs. Habitat/submarine atmosphere management.
- **Inputs:** `float moles, float temperatureKelvin, float volumeCubicMeters`
- **Outputs:** `float (pressure in Pascals)`
- **Constraints:** Pure C#. PV=nRT.
- **Test Requirement:** Double volume: half pressure. Double moles: double pressure. Zero volume: guarded.

### TASK-107: GasMixturePartialPressureCalculator
- **Source Monolith File:** `GasDynamicsSolver.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/GasMixturePartialPressureCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/GasMixturePartialPressureCalculatorTests.cs`
- **Objective:** Extract partial pressure for gas mixture from GasDynamicsSolver.cs. Dalton's law.
- **Inputs:** `float totalPressurePa, float[] gasMoleFractions`
- **Outputs:** `float[] (partial pressures for each gas)`
- **Constraints:** Pure C#. Dalton's law.
- **Test Requirement:** Single gas: partial == total. Equal mix of two: each half. Fractions sum to 1.0.

### TASK-108: AtmosphereLeakRateCalculator
- **Source Monolith File:** `GasDynamicsSolver.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/AtmosphereLeakRateCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/AtmosphereLeakRateCalculatorTests.cs`
- **Objective:** Extract atmosphere leak rate through hull breach from GasDynamicsSolver.cs. Bernoulli flow.
- **Inputs:** `float internalPressurePa, float externalPressurePa, float breachAreaM2, float dischargeCoeff`
- **Outputs:** `float (mass flow rate kg/s)`
- **Constraints:** Pure C#. Bernoulli.
- **Test Requirement:** Equal pressure: zero flow. Larger breach: proportionally higher. Vacuum outside: max flow.

### TASK-109: DecompressionStopTimeCalculator
- **Source Monolith File:** `GasDynamicsSolver.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/DecompressionStopTimeCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/DecompressionStopTimeCalculatorTests.cs`
- **Objective:** Extract required decompression stop time from GasDynamicsSolver.cs. Simplified arcade-realistic off-gas model (NOT full Buhlmann 16-tissue).
- **Inputs:** `float maxDepthReached, float timeAtDepthMin, float ascentRate, float stopDepthMeters`
- **Outputs:** `float (requiredStopTimeMinutes)`
- **Constraints:** Pure C#. Arcade approximation, NOT medical Buhlmann.
- **Test Requirement:** Short shallow dive: zero stop. Long deep dive: significant stop. Faster ascent: longer stop.

### TASK-110: PowerLoadBalancer
- **Source Monolith File:** `PowerGrid.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/PowerLoadBalancer.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/PowerLoadBalancerTests.cs`
- **Objective:** Extract power load balancing priority algorithm from PowerGrid.cs.
- **Inputs:** `float totalSupplyWatts, float[] consumerDemands, int[] consumerPriorities`
- **Outputs:** `float[] (allocatedWatts per consumer)`
- **Constraints:** Pure C#.
- **Test Requirement:** Sufficient supply: all satisfied. Insufficient: high priority served, low shed. Total <= supply.

### TASK-111: BatteryChargeCurveCalculator
- **Source Monolith File:** `PowerGrid.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/BatteryChargeCurveCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/BatteryChargeCurveCalculatorTests.cs`
- **Objective:** Extract battery charge curve from PowerGrid.cs. CC/CV model.
- **Inputs:** `float chargeLevel01, float chargerRateWatts, float batteryCapacityWh, float cvTransitionLevel, float deltaTime`
- **Outputs:** `float (new charge level 0.0-1.0), float (actualWattsDrawn)`
- **Constraints:** Pure C#.
- **Test Requirement:** Empty battery: full rate. Above CV transition: rate reduces. Full battery: near zero rate.

### TASK-112: SolarIrradianceDepthCalculator
- **Source Monolith File:** `PowerGrid.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/SolarIrradianceDepthCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SolarIrradianceDepthCalculatorTests.cs`
- **Objective:** Extract solar panel irradiance at depth from PowerGrid.cs. Beer-Lambert light penetration.
- **Inputs:** `float depthMeters, float surfaceIrradianceWm2, float waterAttenuationCoeff, float panelEfficiency`
- **Outputs:** `float (powerOutputWatts per square meter)`
- **Constraints:** Pure C#. Beer-Lambert law.
- **Test Requirement:** Surface: near max. At 20m: ~60%. At 200m: near zero.

### TASK-113: BallastTankController
- **Source Monolith File:** `SubmarineDynamicsRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/BallastTankController.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/BallastTankControllerTests.cs`
- **Objective:** Extract ballast tank fill/vent rate from SubmarineDynamicsRuntime.cs.
- **Inputs:** `float currentFillLevel01, float targetFillLevel, float fillRatePerSec, float ventRatePerSec, float deltaTime`
- **Outputs:** `float (new fill level 0.0-1.0)`
- **Constraints:** Pure C#.
- **Test Requirement:** Empty target: vents at ventRate. Full target: fills at fillRate. At target: stable.

### TASK-114: PitchTrimCorrectionCalculator
- **Source Monolith File:** `SubmarineDynamicsRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/PitchTrimCorrectionCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/PitchTrimCorrectionCalculatorTests.cs`
- **Objective:** Extract pitch trim correction force from SubmarineDynamicsRuntime.cs. PD controller.
- **Inputs:** `float pitchAngleDeg, float trimGain, float maxTrimForceN, float pitchAngularVelocity, float dampingCoeff`
- **Outputs:** `float (pitchCorrectionTorque Nm)`
- **Constraints:** Pure C#. PD controller.
- **Test Requirement:** Zero pitch: zero torque. Pitched 45 deg: proportional correction. High velocity: damping counters overshoot.

### TASK-115: PropellerCavitationLimitCalculator
- **Source Monolith File:** `SubmarineDynamicsRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/PropellerCavitationLimitCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/PropellerCavitationLimitCalculatorTests.cs`
- **Objective:** Extract propeller cavitation speed limit from SubmarineDynamicsRuntime.cs.
- **Inputs:** `float propRPM, float depthMeters, float waterTemperature, float propDiameterM`
- **Outputs:** `float (thrustEfficiency01)`
- **Constraints:** Pure C#.
- **Test Requirement:** Low RPM: full efficiency. Above cavitation RPM shallow: efficiency drops. Deeper raises threshold.

### TASK-116: StructuralDepthRatingCalculator
- **Source Monolith File:** `HullIntegrityRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/StructuralDepthRatingCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/StructuralDepthRatingCalculatorTests.cs`
- **Objective:** Extract hull stress fraction from HullIntegrityRuntime.cs. Stress accumulates past crush depth.
- **Inputs:** `float depthMeters, float crushDepthRating, float hullIntegrity01, float fatigueAccumulated`
- **Outputs:** `float (stressFraction 0.0-1.0), float (damageRatePerSecond)`
- **Constraints:** Pure C#.
- **Test Requirement:** Above crush depth: zero stress. At rating: moderate. Double depth: catastrophic.

### TASK-117: CaloricDeficitPenaltyCalculator
- **Source Monolith File:** `HectonSurvivalSystem.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/CaloricDeficitPenaltyCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/CaloricDeficitPenaltyCalculatorTests.cs`
- **Objective:** Extract caloric deficit stat penalty from HectonSurvivalSystem.cs.
- **Inputs:** `float caloricBalance, float deficitThreshold, float maxPenalty`
- **Outputs:** `float (staminaPenalty01), float (strengthPenalty01)`
- **Constraints:** Pure C#.
- **Test Requirement:** Positive balance: no penalty. At threshold: begins. Max deficit: maxPenalty applied.

### TASK-118: HydrationSweatLossCalculator
- **Source Monolith File:** `HectonSurvivalSystem.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/HydrationSweatLossCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/HydrationSweatLossCalculatorTests.cs`
- **Objective:** Extract hydration loss rate through sweating from HectonSurvivalSystem.cs.
- **Inputs:** `float exertionLevel01, float ambientTempCelsius, float baseSweatRate, float heatThreshold`
- **Outputs:** `float (waterLostPerHour liters)`
- **Constraints:** Pure C#.
- **Test Requirement:** Rest in cold: minimal. Max exertion in heat: 2L+/hr. Below threshold: exertion only.

### TASK-119: RadiationDoseAccumulator
- **Source Monolith File:** `HectonSurvivalSystem.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/RadiationDoseAccumulator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/RadiationDoseAccumulatorTests.cs`
- **Objective:** Extract radiation dose accumulation and recovery from HectonSurvivalSystem.cs.
- **Inputs:** `float currentDoseSv, float exposureRateSvPerHour, float recoveryRateSvPerHour, float deltaTime`
- **Outputs:** `float (newDoseSv)`
- **Constraints:** Pure C#.
- **Test Requirement:** No exposure, recovery: dose decreases. High exposure: increases. Equal rates: stable.

### TASK-120: HypothermiaShiverCurveCalculator
- **Source Monolith File:** `HectonSurvivalSystem.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/HypothermiaShiverCurveCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/HypothermiaShiverCurveCalculatorTests.cs`
- **Objective:** Extract hypothermia severity curve from HectonSurvivalSystem.cs.
- **Inputs:** `float coreTempCelsius, float normalTemp, float shiversOnset, float incapacitationTemp`
- **Outputs:** `float (penaltyFactor 0.0 normal to 1.0 incapacitated)`
- **Constraints:** Pure C#.
- **Test Requirement:** Normal temp: zero penalty. At shiverOnset: mild penalty. At incapacitation: 1.0 penalty.

### TASK-121: ThreatScoreAggregator
- **Source Monolith File:** `EncounterDirector.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/ThreatScoreAggregator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ThreatScoreAggregatorTests.cs`
- **Objective:** Extract aggregate threat score from EncounterDirector.cs.
- **Inputs:** `float[] hazardDistances, float[] hazardWeights, float[] hazardStrengths, float perceptionRadius`
- **Outputs:** `float (totalThreatScore)`
- **Constraints:** Pure C#.
- **Test Requirement:** No hazards in range: zero. Single nearby high-weight hazard: high score. Distance falloff applied.

### TASK-122: SpawnCooldownGate
- **Source Monolith File:** `EncounterDirector.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/SpawnCooldownGate.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SpawnCooldownGateTests.cs`
- **Objective:** Extract spawn cooldown budget gate from EncounterDirector.cs.
- **Inputs:** `float lastSpawnTime, float currentTime, float cooldownBase, float currentPopulationDensity, float densityMultiplier`
- **Outputs:** `bool (canSpawn), float (timeUntilNextSpawn)`
- **Constraints:** Pure C#.
- **Test Requirement:** Cooldown not expired: false. High density: increased cooldown. Expired: canSpawn true.

### TASK-123: StressSpawnEscalationCalculator
- **Source Monolith File:** `StressDrivenSpawnDirector.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/StressSpawnEscalationCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/StressSpawnEscalationCalculatorTests.cs`
- **Objective:** Extract stress-driven spawn rate escalation from StressDrivenSpawnDirector.cs.
- **Inputs:** `float playerStressLevel01, float baseSpawnRate, float stressEscalationMultiplier, float maxSpawnRate`
- **Outputs:** `float (currentSpawnRate)`
- **Constraints:** Pure C#.
- **Test Requirement:** Zero stress: baseSpawnRate. Max stress: capped at maxSpawnRate. Linear interpolation.

### TASK-124: LotkaVolterraPopulationStep
- **Source Monolith File:** `MacroEcosystemMathematicianRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/LotkaVolterraPopulationStep.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/LotkaVolterraPopulationStepTests.cs`
- **Objective:** Extract Lotka-Volterra predator-prey population step from MacroEcosystemMathematicianRuntime.cs.
- **Inputs:** `float preyPop, float predatorPop, float preyGrowthRate, float predationRate, float predatorDeathRate, float conversionEff, float deltaTime`
- **Outputs:** `float (newPreyPop), float (newPredatorPop)`
- **Constraints:** Pure C#. Euler integration. Must not go negative.
- **Test Requirement:** No predators: prey grows. No prey: predators die. Stable equilibrium at known coefficients.

### TASK-125: NutrientCycleSinkCalculator
- **Source Monolith File:** `MacroEcosystemMathematicianRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/NutrientCycleSinkCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/NutrientCycleSinkCalculatorTests.cs`
- **Objective:** Extract nutrient cycling sink from MacroEcosystemMathematicianRuntime.cs. Dead biomass -> nutrient pool.
- **Inputs:** `float deadBiomass, float decompositionRate, float nutrientPool, float deltaTime`
- **Outputs:** `float (newNutrientPool), float (remainingBiomass)`
- **Constraints:** Pure C#.
- **Test Requirement:** Zero biomass: no gain. High decomp rate: rapid conversion. Mass conservation: biomass lost == nutrients gained.

### TASK-126: BloomTriggerThresholdCalculator
- **Source Monolith File:** `MacroEcosystemMathematicianRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/BloomTriggerThresholdCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/BloomTriggerThresholdCalculatorTests.cs`
- **Objective:** Extract algae bloom trigger condition from MacroEcosystemMathematicianRuntime.cs.
- **Inputs:** `float nutrientLevel, float lightLevel01, float temperatureCelsius, float nutrientThreshold, float lightThreshold, float tempMin, float tempMax`
- **Outputs:** `bool (bloomTriggered), float (bloomIntensity01)`
- **Constraints:** Pure C#.
- **Test Requirement:** All conditions met: bloom. One missing: no bloom. Intensity scales with excess nutrients.

### TASK-127: BiomePressureGradientCalculator
- **Source Monolith File:** `ShinobuEcosystemBalancer.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/BiomePressureGradientCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/BiomePressureGradientCalculatorTests.cs`
- **Objective:** Extract biome pressure gradient migration force from ShinobuEcosystemBalancer.cs.
- **Inputs:** `float[] biomePressures, int[] adjacencyMap, float migrationRate`
- **Outputs:** `float[] (migrationFlows between biomes)`
- **Constraints:** Pure C#.
- **Test Requirement:** Equal pressures: zero flow. Higher pressure: outflow. Magnitude proportional to gradient.

### TASK-128: ExtinctionRiskIndexCalculator
- **Source Monolith File:** `ShinobuEcosystemBalancer.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/ExtinctionRiskIndexCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ExtinctionRiskIndexCalculatorTests.cs`
- **Objective:** Extract extinction risk index from ShinobuEcosystemBalancer.cs.
- **Inputs:** `float currentPop, float minViablePop, float habitatQuality01, float predationPressure01`
- **Outputs:** `float (extinctionRiskIndex 0.0 safe to 1.0 critical)`
- **Constraints:** Pure C#.
- **Test Requirement:** Pop above viable, good habitat: low risk. Below viable: high risk. Predation alone can elevate.

### TASK-129: SymbiosisBenefitMatrixCalculator
- **Source Monolith File:** `ShinobuFloraFaunaSymbiosisSolver.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/SymbiosisBenefitMatrixCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SymbiosisBenefitMatrixCalculatorTests.cs`
- **Objective:** Extract symbiosis benefit matrix from ShinobuFloraFaunaSymbiosisSolver.cs. Mutualism, commensalism, parasitism.
- **Inputs:** `float[] speciesPopulations, float[,] interactionMatrix`
- **Outputs:** `float[] (netBenefitPerSpecies)`
- **Constraints:** Pure C#. Matrix multiply.
- **Test Requirement:** Isolated: zero interaction. Mutualist pair: both benefit. Parasite-host: parasite gains, host loses.

### TASK-130: ChemicalDiffusionSolver
- **Source Monolith File:** `ChemicalInfluenceGrid.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/ChemicalDiffusionSolver.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ChemicalDiffusionSolverTests.cs`
- **Objective:** Extract 2D chemical diffusion Laplacian step from ChemicalInfluenceGrid.cs.
- **Inputs:** `float[,] concentrationGrid, float diffusionRate, float deltaTime`
- **Outputs:** `float[,] (updated concentration grid)`
- **Constraints:** Pure C#. No Unity Texture2D or ComputeShader.
- **Test Requirement:** Uniform: no change. Point source: spreads outward. Total mass conserved.

### TASK-131: ToxinBioaccumulationCalculator
- **Source Monolith File:** `ChemicalInfluenceGrid.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/ToxinBioaccumulationCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ToxinBioaccumulationCalculatorTests.cs`
- **Objective:** Extract toxin bioaccumulation from ChemicalInfluenceGrid.cs. Toxin concentrates up food chain.
- **Inputs:** `float waterToxinConcentration, float biomagnificationFactor, int trophicLevel`
- **Outputs:** `float (organicTissueConcentration)`
- **Constraints:** Pure C#.
- **Test Requirement:** Level 1: close to water. Level 4: exponentially higher. Zero water: zero accumulation.

### TASK-132: WeightPenaltyCurveCalculator
- **Source Monolith File:** `PlayerInventory.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/WeightPenaltyCurveCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/WeightPenaltyCurveCalculatorTests.cs`
- **Objective:** Extract carry weight penalty from PlayerInventory.cs. Overweight causes speed/stamina penalties.
- **Inputs:** `float currentWeightKg, float maxCarryKg, float penaltyStartFraction, float maxSpeedPenalty01`
- **Outputs:** `float (speedMultiplier), float (staminaDrainMultiplier)`
- **Constraints:** Pure C#.
- **Test Requirement:** Below penalty start: no penalty. At max carry: maxSpeedPenalty. Above: capped.

### TASK-133: StackMergePriorityCalculator
- **Source Monolith File:** `InventoryRoutingNetwork.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/StackMergePriorityCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/StackMergePriorityCalculatorTests.cs`
- **Objective:** Extract item stack merge priority from InventoryRoutingNetwork.cs. Items merge into most-full stacks first.
- **Inputs:** `int[] stackCounts, int maxStackSize, int quantityToAdd`
- **Outputs:** `int[] (newStackCounts), int (remainder)`
- **Constraints:** Pure C#.
- **Test Requirement:** Single partial stack: fills it. Multiple partial: most-full first. Remainder when no space.

### TASK-134: Co2ScrubberLoadCalculator
- **Source Monolith File:** `SubmarineAtmosphereSystem.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/Co2ScrubberLoadCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/Co2ScrubberLoadCalculatorTests.cs`
- **Objective:** Extract CO2 scrubber load from SubmarineAtmosphereSystem.cs. Capacity shared among crew.
- **Inputs:** `float crewCount, float activityLevel01, float scrubberCapacityKgPerHour, float co2ProductionPerPersonKgHr`
- **Outputs:** `float (co2RemovalRate), float (netCo2Balance)`
- **Constraints:** Pure C#.
- **Test Requirement:** 1 crew resting, full capacity: net negative. 5 active, underpowered: net positive (CO2 rising).

### TASK-135: FireOxygenConsumptionCalculator
- **Source Monolith File:** `SubmarineAtmosphereSystem.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/FireOxygenConsumptionCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/FireOxygenConsumptionCalculatorTests.cs`
- **Objective:** Extract fire oxygen consumption rate from SubmarineAtmosphereSystem.cs.
- **Inputs:** `float fireIntensity01, float compartmentVolumeM3, float o2Fraction, float maxO2ConsumptionRate`
- **Outputs:** `float (o2ConsumptionRateKgPerSec), float (newO2Fraction after deltaTime)`
- **Constraints:** Pure C#.
- **Test Requirement:** No fire: zero. Max intensity: max rate. O2 below minimum: fire dies.

### TASK-136: PressureEqualizationCalculator
- **Source Monolith File:** `SubmarineAtmosphereSystem.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/PressureEqualizationCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/PressureEqualizationCalculatorTests.cs`
- **Objective:** Extract airlock pressure equalization time from SubmarineAtmosphereSystem.cs.
- **Inputs:** `float internalPressurePa, float externalPressurePa, float airlockVolumeM3, float valveFlowRateM3PerSec`
- **Outputs:** `float (equalizationTimeSeconds)`
- **Constraints:** Pure C#.
- **Test Requirement:** Equal pressures: zero time. Large differential, small valve: long time. Large valve: fast.

### TASK-137: LunarPhaseCalculator
- **Source Monolith File:** `HectonCelestialEngine.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/LunarPhaseCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/LunarPhaseCalculatorTests.cs`
- **Objective:** Extract moon phase angle from orbital time from HectonCelestialEngine.cs.
- **Inputs:** `float worldTimeSeconds, float lunarCycleLengthSeconds`
- **Outputs:** `float (phaseAngleDeg 0-360), float (illuminationFraction 0-1)`
- **Constraints:** Pure C#.
- **Test Requirement:** New moon: illumination ~0. Full moon: 1.0. Half cycle: 0.5.

### TASK-138: TidalForceAtPointCalculator
- **Source Monolith File:** `HectonCelestialEngine.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/TidalForceAtPointCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/TidalForceAtPointCalculatorTests.cs`
- **Objective:** Extract tidal force magnitude from HectonCelestialEngine.cs / HectonSeismicTideDirector.cs.
- **Inputs:** `float moonPhaseAngleDeg, float latitude, float tidalAmplitudeBase, float moonGravitationalParam`
- **Outputs:** `float (tidalForceNormalized 0-1)`
- **Constraints:** Pure C#.
- **Test Requirement:** Full moon overhead: max tidal. New moon: second peak. Equatorial: higher than polar.

### TASK-139: SolarHourAngleCalculator
- **Source Monolith File:** `HectonCelestialEngine.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/SolarHourAngleCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SolarHourAngleCalculatorTests.cs`
- **Objective:** Extract solar hour angle and sun elevation from HectonCelestialEngine.cs.
- **Inputs:** `float worldTimeSeconds, float dayLengthSeconds, float latitude, float axialTilt`
- **Outputs:** `float (sunElevationDeg), float (hourAngleDeg)`
- **Constraints:** Pure C#.
- **Test Requirement:** Noon: max elevation. Midnight: negative. Poles at solstice: midnight sun conditions.

### TASK-140: VoronoiBiomeSeedCalculator
- **Source Monolith File:** `HectonWorldGenerator.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/VoronoiBiomeSeedCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/VoronoiBiomeSeedCalculatorTests.cs`
- **Objective:** Extract Voronoi biome seed point assignment from HectonWorldGenerator.cs.
- **Inputs:** `Vector3 worldPos, Vector3[] biomeSeedPoints, string[] biomeTypes, float noiseBlend`
- **Outputs:** `string (dominantBiomeType), float (blendFactor)`
- **Constraints:** Pure C#. System.Numerics.Vector3.
- **Test Requirement:** Exact seed point: 100% that biome. Midpoint: blended. Noise=0: hard edges.

### TASK-141: CaveGraphConnectivityChecker
- **Source Monolith File:** `CaveGraphGenerator.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/CaveGraphConnectivityChecker.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/CaveGraphConnectivityCheckerTests.cs`
- **Objective:** Extract cave graph connectivity check from CaveGraphGenerator.cs. No isolated chambers.
- **Inputs:** `int nodeCount, int[,] adjacencyMatrix`
- **Outputs:** `bool (isFullyConnected), int[] (disconnectedNodeIds)`
- **Constraints:** Pure C#. BFS/DFS flood fill.
- **Test Requirement:** Single node: connected. Linear chain: connected. Isolated node: disconnected, returned.

### TASK-142: SeismicRichterDamageCalculator
- **Source Monolith File:** `HectonSeismicTideDirector.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/SeismicRichterDamageCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SeismicRichterDamageCalculatorTests.cs`
- **Objective:** Extract seismic event damage from HectonSeismicTideDirector.cs. Structure damage scales with Richter and distance.
- **Inputs:** `float richterMagnitude, float distanceKm, float structuralIntegrity01, float dampingFactor`
- **Outputs:** `float (damageDealt01), float (shakeAmplitude)`
- **Constraints:** Pure C#.
- **Test Requirement:** Magnitude 2 at 100km: negligible. Magnitude 7 at 1km: severe. Higher integrity reduces damage.

### TASK-143: SonarPingReturnTimeCalculator
- **Source Monolith File:** `ScannerTool.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/SonarPingReturnTimeCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SonarPingReturnTimeCalculatorTests.cs`
- **Objective:** Extract sonar ping return time from ScannerTool.cs / TopographicalSonarSynthesizer.cs.
- **Inputs:** `float distanceMeters, float soundSpeedMps, float pingFrequencyHz, float waterTemperature`
- **Outputs:** `float (returnTimeSeconds), float (dopplerShiftedFrequencyHz)`
- **Constraints:** Pure C#.
- **Test Requirement:** Standard seawater: ~1500 m/s. Closer target: faster return. Moving target: Doppler detectable.

### TASK-144: ScannerResolutionDepthCalculator
- **Source Monolith File:** `ScannerTool.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/ScannerResolutionDepthCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ScannerResolutionDepthCalculatorTests.cs`
- **Objective:** Extract scanner resolution falloff from ScannerTool.cs.
- **Inputs:** `float targetDistance, float maxScanRange, float ambientNoiseLevel, float scannerPower`
- **Outputs:** `float (resolutionQuality01)`
- **Constraints:** Pure C#.
- **Test Requirement:** Close, low noise: near 1.0. Max range: near 0. High noise degrades proportionally.

### TASK-145: RepairRateMaterialCalculator
- **Source Monolith File:** `RepairTool.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/RepairRateMaterialCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/RepairRateMaterialCalculatorTests.cs`
- **Objective:** Extract repair rate from RepairTool.cs. Rate based on material type and tool charge.
- **Inputs:** `float toolCharge01, float materialHardness, float baseRepairRate, float depthPressureMultiplier`
- **Outputs:** `float (repairRatePerSecond)`
- **Constraints:** Pure C#.
- **Test Requirement:** Full charge, soft: max rate. No charge: zero. Hard material: reduced. Deep: further reduced.

### TASK-146: WeldHeatDissipationCalculator
- **Source Monolith File:** `RepairTool.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/WeldHeatDissipationCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/WeldHeatDissipationCalculatorTests.cs`
- **Objective:** Extract welding heat dissipation into water from RepairTool.cs. Newton's law of cooling.
- **Inputs:** `float weldTemperatureCelsius, float waterTempCelsius, float weldAreaM2, float heatTransferCoeff, float deltaTime`
- **Outputs:** `float (heatDissipatedJoules), float (newWeldTemp)`
- **Constraints:** Pure C#. Newton's law of cooling.
- **Test Requirement:** Weld at water temp: no dissipation. High temp: rapid cooling. Large area: faster.

### TASK-147: LaserBeamIntensityAttenuationCalculator
- **Source Monolith File:** `LaserCutter.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/LaserBeamIntensityAttenuationCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/LaserBeamIntensityAttenuationCalculatorTests.cs`
- **Objective:** Extract laser beam intensity attenuation in water from LaserCutter.cs. Beer-Lambert.
- **Inputs:** `float initialIntensity, float distanceMeters, float waterAttenuationCoeff, float particulateDensity`
- **Outputs:** `float (intensityAtTarget)`
- **Constraints:** Pure C#. Beer-Lambert.
- **Test Requirement:** Zero distance: no attenuation. Dense particulates: heavy loss. Clear water: minimal.

### TASK-148: LaserCutDepthPowerCalculator
- **Source Monolith File:** `LaserCutter.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/LaserCutDepthPowerCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/LaserCutDepthPowerCalculatorTests.cs`
- **Objective:** Extract laser cut depth per pulse from LaserCutter.cs.
- **Inputs:** `float beamPowerWatts, float focusDiameterMm, float materialAbsorptivity, float pulseDurationMs`
- **Outputs:** `float (cutDepthMm)`
- **Constraints:** Pure C#.
- **Test Requirement:** Higher power: deeper. Larger focus: shallower. More absorptive material: deeper.

### TASK-149: FabricationCraftTimeModifier
- **Source Monolith File:** `FabricationAssemblerRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/FabricationCraftTimeModifier.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/FabricationCraftTimeModifierTests.cs`
- **Objective:** Extract crafting time modifier from FabricationAssemblerRuntime.cs.
- **Inputs:** `float baseCraftTimeSeconds, float benchTier01, float playerSkill01, float materialComplexity`
- **Outputs:** `float (actualCraftTimeSeconds)`
- **Constraints:** Pure C#.
- **Test Requirement:** No bonuses: base time. Max tier+skill: min time floored. Higher complexity: scales up.

### TASK-150: FabricationRecipeYieldRoll
- **Source Monolith File:** `FabricationAssemblerRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/FabricationRecipeYieldRoll.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/FabricationRecipeYieldRollTests.cs`
- **Objective:** Extract crafting yield random roll from FabricationAssemblerRuntime.cs. Deterministic seeded.
- **Inputs:** `float playerSkill01, float baseYield, float maxBonusYield, float randomSeed`
- **Outputs:** `float (actualYield)`
- **Constraints:** Pure C#. Deterministic seeded random.
- **Test Requirement:** Skill 0: always baseYield. Skill 1: can reach maxBonusYield. Same seed = same result.

### TASK-151: QuestDagUnlockChecker
- **Source Monolith File:** `QuestDagResolverRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/QuestDagUnlockChecker.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/QuestDagUnlockCheckerTests.cs`
- **Objective:** Extract quest DAG dependency resolution from QuestDagResolverRuntime.cs.
- **Inputs:** `int questId, int[] allCompletedQuestIds, int[,] dependencyGraph, int nodeCount`
- **Outputs:** `bool (isUnlocked)`
- **Constraints:** Pure C#. Graph traversal.
- **Test Requirement:** No dependencies: always unlocked. One incomplete predecessor: locked. All complete: unlocked.

### TASK-152: QuestObjectiveProgressNormalizer
- **Source Monolith File:** `QuestStateManager.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/QuestObjectiveProgressNormalizer.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/QuestObjectiveProgressNormalizerTests.cs`
- **Objective:** Extract objective progress normalization from QuestStateManager.cs.
- **Inputs:** `float currentCount, float requiredCount, bool isOrdered`
- **Outputs:** `float (normalizedProgress 0.0-1.0)`
- **Constraints:** Pure C#.
- **Test Requirement:** Zero count: 0.0. Count == required: 1.0. Over-count: clamped at 1.0.

### TASK-153: AnchorStabilityScoreCalculator
- **Source Monolith File:** `ConstructionManager.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/AnchorStabilityScoreCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/AnchorStabilityScoreCalculatorTests.cs`
- **Objective:** Extract base module anchor stability from ConstructionManager.cs.
- **Inputs:** `float contactAreaM2, float terrainSlopeAngleDeg, float maxStableSlope, float foundationStrength`
- **Outputs:** `float (stabilityScore 0.0-1.0)`
- **Constraints:** Pure C#.
- **Test Requirement:** Flat, large contact: 1.0. Max slope: near 0. Larger contact improves stability.

### TASK-154: FloodFillRoomVolumeCalculator
- **Source Monolith File:** `HabitatGraphManager.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/FloodFillRoomVolumeCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/FloodFillRoomVolumeCalculatorTests.cs`
- **Objective:** Extract connected room volume via flood fill from HabitatGraphManager.cs.
- **Inputs:** `bool[,,] voxelGrid, int startX, int startY, int startZ, float voxelSizeM`
- **Outputs:** `float (connectedVolumeM3), int (voxelCount)`
- **Constraints:** Pure C#. BFS flood fill.
- **Test Requirement:** Single open voxel: correct volume. Blocked path: separate regions. Large connected: correct total.

### TASK-155: ScooterThrustCurveCalculator
- **Source Monolith File:** `MantaScooter.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/ScooterThrustCurveCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ScooterThrustCurveCalculatorTests.cs`
- **Objective:** Extract Manta scooter thrust curve from MantaScooter.cs.
- **Inputs:** `float throttleInput01, float currentSpeed, float maxSpeed, float thrustForce, float dragCoeff`
- **Outputs:** `float (netForceN)`
- **Constraints:** Pure C#.
- **Test Requirement:** Zero throttle: only drag. Full throttle zero speed: maxThrust. At maxSpeed: net force zero.

### TASK-156: ScooterBatteryDrainCalculator
- **Source Monolith File:** `MantaScooter.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/ScooterBatteryDrainCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ScooterBatteryDrainCalculatorTests.cs`
- **Objective:** Extract scooter battery drain from MantaScooter.cs.
- **Inputs:** `float thrustOutput01, float maxDrainRateWatts, float batteryCapacityWh, float currentCharge01, float deltaTime`
- **Outputs:** `float (newCharge01), float (drainThisFrame)`
- **Constraints:** Pure C#.
- **Test Requirement:** Zero thrust: no drain. Full thrust: max drain. Zero charge: zero output.

### TASK-157: ArmReachIkSolver
- **Source Monolith File:** `ExosuitKinematicsRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/ArmReachIkSolver.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ArmReachIkSolverTests.cs`
- **Objective:** Extract 2-bone IK reach solution from ExosuitKinematicsRuntime.cs.
- **Inputs:** `Vector3 shoulderPos, Vector3 targetPos, float upperArmLength, float forearmLength`
- **Outputs:** `Vector3 elbowPos, Vector3 handPos, bool (canReach)`
- **Constraints:** Pure C#. Geometric 2-bone IK. System.Numerics.Vector3.
- **Test Requirement:** Within reach: valid solution. Too far: max extension. Behind: joint limit clamped.

### TASK-158: ServoTorqueLoadCalculator
- **Source Monolith File:** `ExosuitKinematicsRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/ServoTorqueLoadCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ServoTorqueLoadCalculatorTests.cs`
- **Objective:** Extract servo motor torque requirement from ExosuitKinematicsRuntime.cs.
- **Inputs:** `float loadKg, float armAngleDeg, float armLengthM, float gravity, float servoEfficiency`
- **Outputs:** `float (requiredTorqueNm), float (powerConsumptionWatts)`
- **Constraints:** Pure C#.
- **Test Requirement:** No load: minimal torque. Heavy load horizontal: max. Vertical down: gravity-assisted.

### TASK-159: LegGaitPhaseCalculator
- **Source Monolith File:** `ProceduralCrabLegIKRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/LegGaitPhaseCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/LegGaitPhaseCalculatorTests.cs`
- **Objective:** Extract crab leg gait phase offset from ProceduralCrabLegIKRuntime.cs.
- **Inputs:** `int legIndex, int totalLegs, float gaitCycleTime, float currentTime`
- **Outputs:** `float (phaseOffset 0.0-1.0), bool (isInSwingPhase)`
- **Constraints:** Pure C#.
- **Test Requirement:** Opposite legs out of phase by 0.5. 4-leg: 0.25 offset. Phase cycles consistently.

### TASK-160: StepTargetPredictor
- **Source Monolith File:** `ProceduralCrabLegIKRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Kinematics/StepTargetPredictor.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/StepTargetPredictorTests.cs`
- **Objective:** Extract predicted step target position from ProceduralCrabLegIKRuntime.cs.
- **Inputs:** `Vector3 bodyPos, Vector3 bodyVelocity, float stepAheadFraction, float legStepRadius, int legIndex, int totalLegs`
- **Outputs:** `Vector3 (predictedStepTarget)`
- **Constraints:** Pure C#. System.Numerics.Vector3.
- **Test Requirement:** Stationary: step under leg. Moving forward: step ahead. Step within radius.

### TASK-161: TetherSagCatenaryCalculator
- **Source Monolith File:** `TetherInstance.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/TetherSagCatenaryCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/TetherSagCatenaryCalculatorTests.cs`
- **Objective:** Extract tether sag catenary math from TetherInstance.cs.
- **Inputs:** `float anchorASeparationX, float anchorBHeight, float cableLength, float cableWeightPerMeter`
- **Outputs:** `float (maxSagDepthMeters), float (tensionAtAnchors)`
- **Constraints:** Pure C#. Catenary approximation.
- **Test Requirement:** Taut cable: minimal sag. Excess length: deep sag. Heavier cable: more sag.

### TASK-162: TetherSnapLoadCalculator
- **Source Monolith File:** `TetherInstance.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/TetherSnapLoadCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/TetherSnapLoadCalculatorTests.cs`
- **Objective:** Extract tether snap failure load from TetherInstance.cs.
- **Inputs:** `float staticLoadN, float dynamicImpactMultiplier, float tetherBreakingStrengthN`
- **Outputs:** `bool (willSnap), float (snapProbability01)`
- **Constraints:** Pure C#.
- **Test Requirement:** Below breaking: no snap. At 1.5x: definite snap. Dynamic spike can snap at lower static.

### TASK-163: EcholocationRangeCalculator
- **Source Monolith File:** `AcousticEcholocationTranslator.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/EcholocationRangeCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/EcholocationRangeCalculatorTests.cs`
- **Objective:** Extract echolocation effective range from AcousticEcholocationTranslator.cs.
- **Inputs:** `float emittedSoundPressure, float ambientNoiseLevel, float targetReflectivity, float soundAttenuationPerMeter`
- **Outputs:** `float (detectionRangeMeters)`
- **Constraints:** Pure C#.
- **Test Requirement:** High pressure, silent: max range. High ambient noise: reduced. Low reflectivity: shorter.

### TASK-164: SoundShadowOcclusionCalculator
- **Source Monolith File:** `AcousticEcholocationTranslator.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/SoundShadowOcclusionCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SoundShadowOcclusionCalculatorTests.cs`
- **Objective:** Extract sound shadow occlusion factor from AcousticEcholocationTranslator.cs.
- **Inputs:** `float obstacleSize, float distanceToObstacle, float sourceDistance, float soundFrequencyHz`
- **Outputs:** `float (occlusionFactor 0.0-1.0)`
- **Constraints:** Pure C#.
- **Test Requirement:** No obstacle: zero. Large close: near 1.0. High frequency: more occlusion.

### TASK-165: VerletCableSimulator
- **Source Monolith File:** `CablePhysicsSolver132.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/VerletCableSimulator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/VerletCableSimulatorTests.cs`
- **Objective:** Extract Verlet integrated cable segment physics from CablePhysicsSolver132.cs.
- **Inputs:** `Vector3 currentPos, Vector3 prevPos, float segmentRestLength, Vector3 gravity, float dampingFactor, float deltaTime`
- **Outputs:** `Vector3 (newPos)`
- **Constraints:** Pure C#. Verlet integration. System.Numerics.Vector3.
- **Test Requirement:** Falls under gravity. Damped: oscillation reduces. Rest length: segments don't over-stretch.

### TASK-166: CableConstraintSatisfier
- **Source Monolith File:** `CablePhysicsSolver132.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/CableConstraintSatisfier.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/CableConstraintSatisfierTests.cs`
- **Objective:** Extract cable segment constraint satisfaction from CablePhysicsSolver132.cs. Iterative projection.
- **Inputs:** `Vector3 posA, Vector3 posB, float restLength, float stiffness`
- **Outputs:** `Vector3 newPosA, Vector3 newPosB`
- **Constraints:** Pure C#.
- **Test Requirement:** At rest length: no correction. Stretched: pulled together. Compressed: pushed apart.

### TASK-167: BioluminescencePulseFrequencyCalculator
- **Source Monolith File:** `HectonBiolumManager.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/BioluminescencePulseFrequencyCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/BioluminescencePulseFrequencyCalculatorTests.cs`
- **Objective:** Extract bioluminescence pulse frequency from HectonBiolumManager.cs.
- **Inputs:** `float creatureStressLevel01, float depthMeters, float baseFrequencyHz, float stressFrequencyMultiplier, float depthFrequencyMultiplier`
- **Outputs:** `float (pulseFrequencyHz)`
- **Constraints:** Pure C#.
- **Test Requirement:** No stress, surface: base. Max stress: multiplied. Deep adds further boost.

### TASK-168: BioluminescenceIntensityDecayCalculator
- **Source Monolith File:** `HectonBiolumManager.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/BioluminescenceIntensityDecayCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/BioluminescenceIntensityDecayCalculatorTests.cs`
- **Objective:** Extract bioluminescence intensity decay in water column from HectonBiolumManager.cs.
- **Inputs:** `float emittedIntensity, float wavelengthNm, float distanceMeters, float waterClarity`
- **Outputs:** `float (perceivedIntensity)`
- **Constraints:** Pure C#. Wavelength-dependent attenuation.
- **Test Requirement:** Zero distance: emitted == perceived. Blue travels further than red. Murky: rapid falloff.

### TASK-169: ScarcityPriceSpikeCalculator
- **Source Monolith File:** `ResourceScarcityDirector.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/ScarcityPriceSpikeCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ScarcityPriceSpikeCalculatorTests.cs`
- **Objective:** Extract resource price multiplier from scarcity in ResourceScarcityDirector.cs.
- **Inputs:** `float currentSupply, float demandRate, float basePrice, float scarcityElasticity`
- **Outputs:** `float (currentPrice), float (scarcityLevel01)`
- **Constraints:** Pure C#.
- **Test Requirement:** Abundant supply: near base. Half demand: price spikes. Zero supply: max multiplier.

### TASK-170: DepositDepletionCurveCalculator
- **Source Monolith File:** `ProceduralOreSpawner.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/DepositDepletionCurveCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/DepositDepletionCurveCalculatorTests.cs`
- **Objective:** Extract ore deposit depletion curve from ProceduralOreSpawner.cs.
- **Inputs:** `float currentYield, float extractionRate, float depletionExponent, float deltaTime`
- **Outputs:** `float (newYield), float (extractedAmount)`
- **Constraints:** Pure C#.
- **Test Requirement:** Fresh deposit: high rate. Half depleted: reduced. Near zero: minimal yield.

### TASK-171: MarineSnowFluxCalculator
- **Source Monolith File:** `NutrientDriftRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/MarineSnowFluxCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/MarineSnowFluxCalculatorTests.cs`
- **Objective:** Extract marine snow particle flux from NutrientDriftRuntime.cs. Martin curve.
- **Inputs:** `float surfaceProductivity, float depthMeters, float sinkingSpeedMPerDay, float remineralizationRate`
- **Outputs:** `float (fluxMgM2PerDay)`
- **Constraints:** Pure C#. Martin curve.
- **Test Requirement:** Surface: equals productivity. At 100m: ~30%. Deep sea: residual.

### TASK-172: UpwellingNutrientFluxCalculator
- **Source Monolith File:** `NutrientDriftRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Ecosystem/UpwellingNutrientFluxCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/UpwellingNutrientFluxCalculatorTests.cs`
- **Objective:** Extract upwelling nutrient flux from NutrientDriftRuntime.cs.
- **Inputs:** `float upwellingVelocityMPerDay, float deepNutrientConcentration, float shallowNutrientConcentration, float mixingDepthM`
- **Outputs:** `float (nutrientFluxMmolM2PerDay)`
- **Constraints:** Pure C#.
- **Test Requirement:** No upwelling: zero. Strong upwelling + high deep: max flux. Already mixed: reduced.

### TASK-173: StormWaveHeightBeaufortCalculator
- **Source Monolith File:** `HectonSurfaceWeatherDirector.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/StormWaveHeightBeaufortCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/StormWaveHeightBeaufortCalculatorTests.cs`
- **Objective:** Extract significant wave height from Beaufort scale from HectonSurfaceWeatherDirector.cs.
- **Inputs:** `float beaufortNumber, float fetchDistanceKm, float windDurationHours`
- **Outputs:** `float (significantWaveHeightMeters), float (dominantPeriodSeconds)`
- **Constraints:** Pure C#.
- **Test Requirement:** Beaufort 0: calm. Beaufort 7: ~4-5m. Longer fetch/duration: larger waves.

### TASK-174: VisibilityTurbidityCalculator
- **Source Monolith File:** `HectonAtmosphereManager.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/VisibilityTurbidityCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/VisibilityTurbidityCalculatorTests.cs`
- **Objective:** Extract underwater visibility from turbidity and bioluminescence from HectonAtmosphereManager.cs.
- **Inputs:** `float turbidityNTU, float biolumLevelLux, float baseVisibilityMeters`
- **Outputs:** `float (effectiveVisibilityMeters)`
- **Constraints:** Pure C#.
- **Test Requirement:** Clear, no biolum: baseVisibility. High turbidity: reduced. Biolum partially compensates.

### TASK-175: DronePathfindCostCalculator
- **Source Monolith File:** `DroneFleetNavigationKernel.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/DronePathfindCostCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/DronePathfindCostCalculatorTests.cs`
- **Objective:** Extract drone pathfinding traversal cost from DroneFleetNavigationKernel.cs.
- **Inputs:** `Vector3 fromNode, Vector3 toNode, float[] hazardWeightsAtNode, float baseMoveCost`
- **Outputs:** `float (traversalCost)`
- **Constraints:** Pure C#. System.Numerics.Vector3.
- **Test Requirement:** Empty safe space: Euclidean. Hazardous node: elevated. No hazard: base cost.

### TASK-176: DroneTaskPriorityRanker
- **Source Monolith File:** `DroneFleetManager.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/DroneTaskPriorityRanker.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/DroneTaskPriorityRankerTests.cs`
- **Objective:** Extract drone task priority ranking from DroneFleetManager.cs.
- **Inputs:** `float urgency, float proximity01, float resourceAvailability01, float[] weights`
- **Outputs:** `float (priorityScore)`
- **Constraints:** Pure C#. Weighted sum.
- **Test Requirement:** Max urgency, close, resources: max score. Low urgency, far, no resources: min score.

### TASK-177: DroneBatteryReturnThresholdCalculator
- **Source Monolith File:** `DroneFleetManager.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/DroneBatteryReturnThresholdCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/DroneBatteryReturnThresholdCalculatorTests.cs`
- **Objective:** Extract drone return-to-charge decision from DroneFleetManager.cs.
- **Inputs:** `float currentBatteryLevel01, float distanceToBase, float batteryDrainPerMeter, float safetyMargin01`
- **Outputs:** `bool (mustReturnNow), float (remainingOperationalDistance)`
- **Constraints:** Pure C#.
- **Test Requirement:** Full battery: no return. At threshold: mustReturn true. Margin ensures can always return.

### TASK-178: VoxelSdfBooleanSubtraction
- **Source Monolith File:** `HectonVoxelEngine.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/VoxelSdfBooleanSubtraction.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/VoxelSdfBooleanSubtractionTests.cs`
- **Objective:** Extract SDF sphere subtraction from voxel field from HectonVoxelEngine.cs.
- **Inputs:** `float[,,] densityField, Vector3 sphereCenter, float sphereRadius, int gridResolution, float worldScale`
- **Outputs:** `float[,,] (modified density field)`
- **Constraints:** Pure C#. SDF math. System.Numerics.Vector3.
- **Test Requirement:** Outside sphere: unchanged. Inside: density zeroed. Boundary: smooth falloff.

### TASK-179: MarchingCubesLookupTable
- **Source Monolith File:** `HectonVoxelEngine.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/MarchingCubesLookupTable.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/MarchingCubesLookupTableTests.cs`
- **Objective:** Extract marching cubes table lookup for vertex placement from HectonVoxelEngine.cs.
- **Inputs:** `byte caseMask, float[] cornerDensities, float isoLevel`
- **Outputs:** `int (edgeFlags), float[,] (interpolatedVertices)`
- **Constraints:** Pure C#. Standard marching cubes tables.
- **Test Requirement:** All corners above iso: 0 triangles. All below: 0 triangles. Crossing iso: correct edge vertices.

### TASK-180: LodChunkSelector
- **Source Monolith File:** `HectonVoxelEngine.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/LodChunkSelector.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/LodChunkSelectorTests.cs`
- **Objective:** Extract LOD level selector for voxel chunks from HectonVoxelEngine.cs.
- **Inputs:** `float distanceFromCamera, float[] lodDistanceThresholds, int maxLodLevel`
- **Outputs:** `int (selectedLodLevel)`
- **Constraints:** Pure C#.
- **Test Requirement:** Distance 0: LOD 0. Past last threshold: maxLodLevel. Each threshold step: increments.

### TASK-181: FluidAdvectionStepCalculator
- **Source Monolith File:** `HectonFluidEngine.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/FluidAdvectionStepCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/FluidAdvectionStepCalculatorTests.cs`
- **Objective:** Extract semi-Lagrangian fluid advection step from HectonFluidEngine.cs.
- **Inputs:** `float[,,] velocityFieldX, float[,,] velocityFieldY, float[,,] velocityFieldZ, int x, int y, int z, float deltaTime, float gridSpacing`
- **Outputs:** `float (advectedValue at grid position)`
- **Constraints:** Pure C#. Trilinear interpolation.
- **Test Requirement:** Zero velocity: no advection. Uniform flow: shifts by velocity*dt. Stable.

### TASK-182: FluidPressureJacobiSolver
- **Source Monolith File:** `HectonFluidEngine.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/FluidPressureJacobiSolver.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/FluidPressureJacobiSolverTests.cs`
- **Objective:** Extract Jacobi pressure solver iteration from HectonFluidEngine.cs.
- **Inputs:** `float[,,] pressureField, float[,,] divergenceField, float gridSpacing`
- **Outputs:** `float[,,] (updatedPressureField)`
- **Constraints:** Pure C#. Single Jacobi iteration.
- **Test Requirement:** Uniform pressure: no change. Point source divergence: spreads. Converges toward Poisson.

### TASK-183: VorticityConfinementForceCalculator
- **Source Monolith File:** `HectonFluidEngine.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/VorticityConfinementForceCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/VorticityConfinementForceCalculatorTests.cs`
- **Objective:** Extract vorticity confinement force from HectonFluidEngine.cs. Restores small-scale swirling detail.
- **Inputs:** `float[,,] velocityFieldX, float[,,] velocityFieldY, float[,,] velocityFieldZ, float confinementEpsilon, float gridSpacing`
- **Outputs:** `float[,,] vorticityConfinementFX, float[,,] vorticityConfinementFY, float[,,] vorticityConfinementFZ`
- **Constraints:** Pure C#.
- **Test Requirement:** Irrotational: zero. Curl present: force perpendicular to gradient. Larger epsilon: stronger.

### TASK-184: SaveChecksumVerifier
- **Source Monolith File:** `SaveBinaryStorage.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/SaveChecksumVerifier.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SaveChecksumVerifierTests.cs`
- **Objective:** Extract save file checksum verification from SaveBinaryStorage.cs. CRC32 or xxHash.
- **Inputs:** `byte[] payload, uint storedChecksum`
- **Outputs:** `bool (isValid), uint (computedChecksum)`
- **Constraints:** Pure C#. No file IO.
- **Test Requirement:** Correct payload: valid. Flipped bit: invalid. Empty payload: deterministic checksum.

### TASK-185: SaveDeltaCompressDiffCalculator
- **Source Monolith File:** `SaveBinaryPayloadCodec.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/SaveDeltaCompressDiffCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SaveDeltaCompressDiffCalculatorTests.cs`
- **Objective:** Extract delta compression diff generation from SaveBinaryPayloadCodec.cs.
- **Inputs:** `byte[] baseSnapshot, byte[] newSnapshot`
- **Outputs:** `(int offset, int length, byte[] patchData)[] patches`
- **Constraints:** Pure C#. No IO.
- **Test Requirement:** Identical: zero patches. One byte changed: one patch. Large change: efficient patch list.

### TASK-186: SaveMerkleHashNodeCalculator
- **Source Monolith File:** `SaveStateMerkleTree.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/SaveMerkleHashNodeCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SaveMerkleHashNodeCalculatorTests.cs`
- **Objective:** Extract Merkle tree node hash from SaveStateMerkleTree.cs.
- **Inputs:** `byte[] leftChildHash, byte[] rightChildHash`
- **Outputs:** `byte[] (parentHash)`
- **Constraints:** Pure C#. SHA256 or xxHash.
- **Test Requirement:** Same children: deterministic. Different left child: different parent. Null child handled.

### TASK-187: FixedCapacityRingBuffer
- **Source Monolith File:** `SignalBusRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/FixedCapacityRingBuffer.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/FixedCapacityRingBufferTests.cs`
- **Objective:** Extract lock-free ring buffer push/pop from SignalBusRuntime.cs.
- **Inputs:** `int head, int tail, int capacity, bool isPush`
- **Outputs:** `int (newHead or newTail), bool (success)`
- **Constraints:** Pure C#. No threading primitives. Overflow/underflow detection.
- **Test Requirement:** Push to empty: success. Pop from empty: failure. Fill to capacity: next push fails.

### TASK-188: SignalPrioritySortCalculator
- **Source Monolith File:** `SignalBusRuntime.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/SignalPrioritySortCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/SignalPrioritySortCalculatorTests.cs`
- **Objective:** Extract signal priority queue sort key from SignalBusRuntime.cs.
- **Inputs:** `int priorityA, long timestampA, int priorityB, long timestampB`
- **Outputs:** `int (comparison result -1, 0, 1)`
- **Constraints:** Pure C#. Deterministic stable sort.
- **Test Requirement:** Higher priority always wins. Same priority: earlier timestamp wins. Identical: stable (0).

### TASK-189: AnalogStickDeadzoneNormalizer
- **Source Monolith File:** `InputDispatcher.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/AnalogStickDeadzoneNormalizer.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/AnalogStickDeadzoneNormalizerTests.cs`
- **Objective:** Extract analog stick deadzone normalization from InputDispatcher.cs.
- **Inputs:** `float rawValue, float innerDeadzone, float outerDeadzone`
- **Outputs:** `float (normalizedValue 0.0-1.0)`
- **Constraints:** Pure C#.
- **Test Requirement:** Inside inner: 0.0. Outside outer: 1.0. Between: linear interpolation.

### TASK-190: GyroDriftFilterCalculator
- **Source Monolith File:** `InputDispatcher.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/GyroDriftFilterCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/GyroDriftFilterCalculatorTests.cs`
- **Objective:** Extract gyroscope drift low-pass filter from InputDispatcher.cs. RC filter.
- **Inputs:** `float gyroSample, float previousFiltered, float cutoffFrequencyHz, float sampleRateHz`
- **Outputs:** `float (filteredGyroValue)`
- **Constraints:** Pure C#. RC low-pass filter.
- **Test Requirement:** DC offset (drift): filtered to near zero. High frequency rotation: passed through.

### TASK-191: GameStateTensionScorer
- **Source Monolith File:** `HectonMusicDirector.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/GameStateTensionScorer.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/GameStateTensionScorerTests.cs`
- **Objective:** Extract musical tension score from game state from HectonMusicDirector.cs.
- **Inputs:** `float threatLevel01, float depthNormalized01, float oxygenRemaining01, float playerHP01`
- **Outputs:** `float (tensionScore 0.0-1.0)`
- **Constraints:** Pure C#. Weighted aggregate.
- **Test Requirement:** Safe surface, full O2+HP: near 0. Active threat + low O2 + deep: near 1. Factors contribute proportionally.

### TASK-192: MusicStemBlendCrossfader
- **Source Monolith File:** `HectonMusicDirector.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/MusicStemBlendCrossfader.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/MusicStemBlendCrossfaderTests.cs`
- **Objective:** Extract music stem crossfade timing from HectonMusicDirector.cs.
- **Inputs:** `float currentVolume, float targetVolume, float crossfadeDurationSec, float currentTime, float startTime`
- **Outputs:** `float (blendedVolume)`
- **Constraints:** Pure C#.
- **Test Requirement:** At startTime: currentVolume. At startTime+duration: targetVolume. Mid-point: smooth interpolation.

### TASK-193: LufsNormalizationCalculator
- **Source Monolith File:** `AdaptiveStemAudioMixer.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/LufsNormalizationCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/LufsNormalizationCalculatorTests.cs`
- **Objective:** Extract LUFS normalization gain from AdaptiveStemAudioMixer.cs.
- **Inputs:** `float measuredLUFS, float targetLUFS, float maxGainDB, float minGainDB`
- **Outputs:** `float (gainDB)`
- **Constraints:** Pure C#.
- **Test Requirement:** measured == target: 0 dB. Quiet: positive gain up to max. Loud: negative, clamped.

### TASK-194: ReverbPreDelayCalculator
- **Source Monolith File:** `AdaptiveStemAudioMixer.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/ReverbPreDelayCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/ReverbPreDelayCalculatorTests.cs`
- **Objective:** Extract reverb pre-delay time from cave geometry from AdaptiveStemAudioMixer.cs.
- **Inputs:** `float roomVolumeM3, float soundSpeedMps, float listenerDistanceFromWall`
- **Outputs:** `float (preDelayMs)`
- **Constraints:** Pure C#.
- **Test Requirement:** Small room: short delay. Large cave: longer. Distance to wall affects first reflection.

### TASK-195: GrainEnvelopeCalculator
- **Source Monolith File:** `DynamicMusicGranularSynthesizer.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/GrainEnvelopeCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/GrainEnvelopeCalculatorTests.cs`
- **Objective:** Extract granular synthesis grain envelope from DynamicMusicGranularSynthesizer.cs. Hann window.
- **Inputs:** `float grainPositionNormalized, float attackFraction, float decayFraction`
- **Outputs:** `float (envelopeAmplitude 0.0-1.0)`
- **Constraints:** Pure C#.
- **Test Requirement:** Position 0 and 1: amplitude 0. Position 0.5 symmetric: peak. No clicks at boundaries.

### TASK-196: PitchShiftResampleCalculator
- **Source Monolith File:** `DynamicMusicGranularSynthesizer.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/PitchShiftResampleCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/PitchShiftResampleCalculatorTests.cs`
- **Objective:** Extract pitch shift via resampling ratio from DynamicMusicGranularSynthesizer.cs.
- **Inputs:** `float semitones, float originalSampleRate`
- **Outputs:** `float (resampleRatio), float (newSampleRate)`
- **Constraints:** Pure C#.
- **Test Requirement:** Zero semitones: ratio 1.0. 12 up: ratio 2.0. 12 down: ratio 0.5.

### TASK-197: CausticIntensityDepthCalculator
- **Source Monolith File:** `HectonUnderwaterVisuals.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/CausticIntensityDepthCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/CausticIntensityDepthCalculatorTests.cs`
- **Objective:** Extract caustic light intensity at depth from HectonUnderwaterVisuals.cs.
- **Inputs:** `float depthMeters, float surfaceCausticIntensity, float attenuationDepth, float waterClarity`
- **Outputs:** `float (causticIntensity01)`
- **Constraints:** Pure C#.
- **Test Requirement:** Surface: surfaceCausticIntensity. At attenuationDepth: 50%. Murky: faster falloff.

### TASK-198: UnderwaterFogDensityCalculator
- **Source Monolith File:** `HectonUnderwaterVisuals.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/UnderwaterFogDensityCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/UnderwaterFogDensityCalculatorTests.cs`
- **Objective:** Extract per-biome underwater fog density from HectonUnderwaterVisuals.cs.
- **Inputs:** `string biomeType, float depthMeters, float baseFogDensity, float particulateLevel`
- **Outputs:** `float (fogDensity)`
- **Constraints:** Pure C#.
- **Test Requirement:** Open ocean shallow: low. Kelp forest: higher. Brine pool: near opaque.

### TASK-199: O2BarPulseRateCalculator
- **Source Monolith File:** `VisorHUDController.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/O2BarPulseRateCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/O2BarPulseRateCalculatorTests.cs`
- **Objective:** Extract O2 bar warning pulse rate from VisorHUDController.cs.
- **Inputs:** `float o2Level01, float warningThreshold, float criticalThreshold, float baseFrequencyHz, float maxFrequencyHz`
- **Outputs:** `float (pulseFrequencyHz)`
- **Constraints:** Pure C#.
- **Test Requirement:** Above warning: 0 Hz. Below warning: pulse > 0. At critical: maxFrequencyHz.

### TASK-200: DepthGaugeNonlinearCalculator
- **Source Monolith File:** `VisorHUDController.cs`
- **Target File:** `Assets/_Project/Scripts/PureLogic/Systems/DepthGaugeNonlinearCalculator.cs`
- **Test File:** `Assets/_Project/Scripts/PureLogic/Tests/DepthGaugeNonlinearCalculatorTests.cs`
- **Objective:** Extract non-linear depth gauge needle angle from VisorHUDController.cs. Logarithmic scale.
- **Inputs:** `float depthMeters, float maxDisplayDepth, float minAngleDeg, float maxAngleDeg`
- **Outputs:** `float (needleAngleDeg)`
- **Constraints:** Pure C#.
- **Test Requirement:** Depth 0: minAngle. At maxDisplayDepth: maxAngle. Log scale: first 10m uses more arc than 10m at 500m.

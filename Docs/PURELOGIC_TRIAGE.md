# PureLogic Triage — WIRE / KEEP / DELETE

Lane: `purelogic-triage`. Date: 2026-07-29. Deliverable: classification only. **No `.cs` was edited,
deleted, or moved to produce this document.**

---

## 1. Counts

| Bucket | Count |
|---|---|
| **Total models scanned** (`Assets/_Project/Scripts/PureLogic/{Ecosystem,Kinematics,Systems}`) | **199** |
| — already have a real call site outside PureLogic (not triaged here) | 81 |
| — **unreferenced → triaged below** | **118** |
| **WIRE** | **84** |
| **KEEP** | **32** |
| **DELETE** | **2** |

Sums: `84 + 32 + 2 = 118` ✔  `81 + 118 = 199` ✔

Folder split of the 199: Systems 127, Kinematics 37, Ecosystem 35. `PureLogic/Tests` holds a further
207 files and is **not** counted as a model directory.

The brief said "117 of 199 with no runtime call site, 100 reachable only via `_ = typeof(X)`". This scan
reproduces that independently and resolves the off-by-one:

| Reference class | Count |
|---|---|
| `JulesLink_<Name>() { _ = typeof(...); }` keep-alive **only** | 100 |
| referenced **only** from `PureLogic/Tests` | 16 |
| referenced only from another PureLogic model (`WeightPenaltyCurveCalculator`) | 1 |
| **no reference of any kind anywhere** (`2dGridHeatmapDecayCalculator` — no call site *and no test*) | 1 |
| **subtotal unreferenced** | **118** |

117 is the count with no call site *and* no PureLogic-internal reference; 118 is the count with no call
site outside PureLogic. Both figures are correct under their own definition. I triage all 118.

---

## 2. The headline finding — this is an abandoned refactor, not a missing brain

The lane hypothesis was "dead weight, or the world's missing brain". The evidence says: **neither, and
something more actionable than both.**

Every one of the 199 models carries a doc comment of the form `Extracted from <Host>.cs. Fully stateless
and allocation-free.` They are not speculative designs. They are a mechanical extraction wave that lifted
math out of 69 named runtime hosts into hardened, NaN-guarded, allocation-free static classes, gave each
one a unit-test file — and then **never performed the second half of the refactor.** Instead of a call,
each host got a stub:

`Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:2902`
```csharp
private static void JulesLink_SuitO2ConsumptionModel() { _ = typeof(Hecton8.PureLogic.Systems.SuitO2ConsumptionModel); }
```

`Assets/_Project/Scripts/HectonPlayerMovement.cs:15138-15171` — nine of these in a row, each in its own
`#region JulesLink_<Name>`, at the very bottom of a 15,176-line file.

Three facts turn this from cleanup into a roadmap:

1. **The sockets are live.** 104 of the 118 unreferenced models name a host that is demonstrably
   instantiated — on a prefab, `AddComponent`-ed, `GetComponent`-ed, `RequireComponent`-ed, or called
   statically. `HectonPlayerMovement` is on `Assets/_Project/Prefabs/Player.prefab`. `EcosystemDirector`,
   `ConstructionManager`, `PowerGridManager`, `BeaconNetworkSystem` are in `GameBootstrapper`'s
   `AddComponent` list. This is not orphaned code looking for a home; it is code whose home is already
   running and already has a labelled empty slot for it.
2. **In 82 of those 104 cases the host does not implement the behaviour at all.** The model's concept
   appears in its own host *only* inside the keep-alive stub. `LedgeGrab` occurs exactly twice in all
   15,176 lines of `HectonPlayerMovement.cs` — the `#region` line and the stub line. `thermocline`,
   `crushDamage`, `oceanCurrent`, `densityRatio` each occur exactly twice in `HydrodynamicKccRuntime.cs`.
   `detectionRange`, `pheromone`, `patrol` each occur exactly twice in `FaunaDirector.cs`. Wiring these
   adds behaviour that does not exist today anywhere in the project.
3. **In the other 22 cases the host already does the math inline.** `ParasiteLatch` occurs 47 times in
   `HectonPlayerMovement.cs`; `ArmorPenetration` 20 times in `CombatDamageRuntime.cs`; `MarchingCubes` 34
   times in `HectonVoxelEngine.cs`. For these the model is a tested twin of shipping code, and wiring is
   a dedupe, not a feature.

So the correct reading is: **~84 named, tested, host-identified behaviours are one call each away from
existing, and the project already wrote the hard part.** That is the opposite of dead weight, and it is
much cheaper than a "missing brain" would be.

This also gives a code-level confirmation of the standing observation that no creature has a brain:
`FaunaDirector` is alive and contains **zero** sensory-range, pheromone, or patrol logic. The three models
that would give creatures perception and movement intent are sitting next to it with tests, unplugged.

---

## 3. Method, and exactly how far it proves anything

Scan surface: 3,755 `.cs` files under `Assets/_Project`, excluding `Library`, `Temp`, `obj`, `.git`,
`_Archive`, `Crest`, `MapMagic`. Scripts used are outside the repo (`C:\temp\pl_triage\`) so nothing was
written into the tree except this file.

A **real call site** was counted only as: a construction, a method invocation, a field/parameter use, or a
registration — with the enclosing method resolved by brace-depth tracking. Explicitly **not** counted:
`_ = typeof(X)`, any `JulesLink_*` line, any line inside a `//`, `///`, or `/* */` comment, and any
reference living only in `PureLogic/Tests`.

**"Host is alive"** means at least one of: the host's script GUID appears in a `.prefab` (prefabs here are
text YAML, so this search is sound); or `AddComponent<Host>` / `GetComponent<Host>` /
`FindObjectOfType<Host>` / `new Host(` / `Host.StaticCall(` / `RequireComponent(typeof(Host))` appears in a
non-test `.cs`.

**"Host is dark"** means none of the above was found. **This is not proof the host is dead.** Four scenes
in this project — including the world scene — are serialised binary (`m_SerializationMode: 2`), so a
scene-placed MonoBehaviour is invisible to every text search. Every DARK verdict below is therefore
"unproven either way", never "confirmed dead", and I have not sent a single model to DELETE on the strength
of a DARK host alone.

**"Host does / does not implement the concept"** is a keyword-count heuristic: count occurrences of the
model's distinctive concept token in the host file, excluding the `JulesLink` lines. A count of ≤2 means
the only mentions are the stub's own `#region` and body. This is the weakest link in the chain. I
hand-verified it with domain vocabulary for 13 hosts covering ~40 models — `ShinobuPhysiologyRuntime`
(`o2Consum` 2, `scrubber` 2, `exertion` 2), `HectonSurvivalSystem` (`caloric` 2, `calorie` 0, but
`hunger` 28 / `nutrition` 25), `HydrodynamicKccRuntime` (all four tokens 2), `FaunaDirector` (all three
tokens 2), `FaunaKinematicsRuntime` (`school`/`separation`/`stalk` all 2), `NutrientDriftRuntime`
(`marineSnow` 2, `upwelling` 2, but `nutrient` 302), `BallisticsRuntime` (`splash` 2, `entryAngle` 2),
`CombatDamageRuntime` (`explosion` 2, `radial` 2, `falloff` 2, but `armor` 181), `HectonCelestialEngine`
(`tidal` 2, `hourAngle` 2, but `tide` 53), `EcosystemDirector` (everything richly present),
`PlayerKinematicsRuntime`, `WorldContentDirector`, `PDAInventoryTab`. For the remainder the token test
stands unaided and **a host could implement the behaviour under vocabulary I did not guess**. Treat each
individual WIRE row as "high-confidence lead", not as proof, until the host method is opened.

**Not proven at all, by anyone, in this document:** that any of this runs. I cannot launch Unity (the
orchestrator holds the only editor lock) and I executed no Unity, no player, no profiler, no device, and
no `dotnet` build. Nothing here is runtime-verified. The 199 test files were counted, never run — and a
green unit test on a static calculator is not evidence the world uses it.

---

## 4. WIRE — 84 entries, grouped by subsystem, largest first

Each row names the host, the method the call belongs in, and what the world gains. Line numbers for the
stubs are exact; the named insertion methods were read from the host's method table and are the correct
*enclosing* method — the precise line inside them is an implementation decision.

### 4.1 Ecosystem & fauna brain — 21 entries (largest cluster)

| Model | Host (ALIVE) | Method the call belongs in | World gains |
|---|---|---|---|
| `FaunaSensoryDetectionRangeCalculator` | `FaunaDirector.cs` | per-creature sensory refresh in the director tick | creatures can detect the player at all — currently `detectionRange` appears twice in the whole file, both in the stub |
| `FaunaPheromoneTrackingVector` | `FaunaDirector.cs` | same tick, after detection | creatures follow scent trails instead of nothing |
| `FaunaPatrolPathSmootherCalculator` | `FaunaDirector.cs` | patrol-target update | creatures move along smooth routes instead of teleport-straight paths |
| `PredatorStalkSpeedCalculator` | `FaunaKinematicsRuntime.cs` | kinematics step | predators close distance at stalk speed instead of one flat speed |
| `SchoolingSeparationForceCalculator` | `FaunaKinematicsRuntime.cs` | kinematics step | fish stop occupying the same point in space |
| `FaunaObstacleAvoidanceVector` | `HectonDirectorAI.cs` | steering accumulation | creatures stop swimming through terrain |
| `PreytopredatorSpawnBalancerCalculator` | `EcosystemDirector.cs` | spawn-budget pass | predator/prey ratio self-corrects instead of drifting |
| `BiomassResourceGradientWeightCalculator` | `EcosystemDirector.cs` | species-weight selection | spawns follow food density |
| `BiomeDepthViabilityCurveCalculator` | `EcosystemDirector.cs` | species-weight selection | species stop appearing at impossible depths |
| `2dGridHeatmapDecayCalculator` | `EcosystemDirector.cs` | heatmap advance | pressure//activity heatmaps decay instead of saturating (**note: this model has no test file at all**) |
| `BloomTriggerThresholdCalculator` | `EcosystemDirector.cs` (no origin declared; keep-alive sits in `Core/GlobalRegistry.cs`) | nutrient/light/temperature evaluation | algal blooms trigger from nutrient+light+temp; today nothing can start one |
| `MarineSnowFluxCalculator` | `NutrientDriftRuntime.cs` | drift advance | detritus falls through the water column (`marineSnow` count 2) |
| `UpwellingNutrientFluxCalculator` | `NutrientDriftRuntime.cs` | drift advance | deep nutrients reach the photic zone (`upwelling` count 2) |
| `ChemicalDiffusionSolver` | `ChemicalInfluenceGrid.cs` | grid step | chemical plumes spread instead of sitting still |
| `ToxinBioaccumulationCalculator` | `ChemicalInfluenceGrid.cs` | grid step | toxins concentrate up the food chain |
| `ExtinctionRiskIndexCalculator` | `ShinobuEcosystemBalancer.cs` | balance pass | over-harvested species become locally extinct |
| `BiomePressureGradientCalculator` | `ShinobuEcosystemBalancer.cs` | balance pass | population pressure pushes species between biomes |
| `SymbiosisBenefitMatrixCalculator` | `ShinobuFloraFaunaSymbiosisSolver.cs` | solve pass | flora/fauna pairs benefit each other |
| `ThreatScoreAggregator` | `EncounterDirector.cs` | encounter scoring | encounter choice responds to accumulated threat |
| `SpawnCooldownGate` | `EncounterDirector.cs` | pre-spawn gate | stops repeat-spawn spam at one location |
| `BiomeDiscoveryBitmaskTracker` | `BiomeDiscoveryBitMask.cs` | discovery record | biome-first-visit is tracked for progression/PDA |

### 4.2 Player movement & hydrodynamics — 17 entries

| Model | Host (ALIVE) | Method the call belongs in | World gains |
|---|---|---|---|
| `OceanCurrentDragCalculator` | `HydrodynamicKccRuntime.cs` | KCC velocity resolve | current actually pushes the player |
| `ThermoclineResistanceCalculator` | `HydrodynamicKccRuntime.cs` | KCC velocity resolve | crossing a thermocline is felt |
| `PressureCrushDamageModel` | `HydrodynamicKccRuntime.cs` | KCC depth evaluation | depth kills — hull/body damage below rating |
| `BuoyancyDensityRatioMath` | `HydrodynamicKccRuntime.cs` | KCC buoyancy resolve | rise/sink follows density, not a constant |
| `ThermalVentUpdraftForce` | `HectonPlayerMovement.cs` (stub @15158-15159) | `ApplyThermalUpdrafts(float, PlayerTransportPreset)` @11064 | vents lift the player; the method exists and currently computes updraft without this model |
| `EquipmentHydrodynamicDragCalculator` | `HectonPlayerMovement.cs` (stub @15154-15155) | `AdvanceExternalEnvironmentalDrag(float)` @7038 | carried gear slows you down |
| `CrouchCapsuleLerp` | `HectonPlayerMovement.cs` (stub @15138-15139) | `ApplyResolvedCollisionProfile(float,float,float)` @6144 | crouch collider interpolates instead of snapping |
| `LedgeGrabImpulseCalculator` | `HectonPlayerMovement.cs` (stub @15146-15147) | new ledge path off `ProcessJumpInput(...)` @10322 | ledge grab exists — `LedgeGrab` appears **nowhere** in the project outside this model and its test |
| `VehicleEmergencyEjectionVector` | `HectonPlayerMovement.cs` (stub @15166-15167) | `UpdateTransportCriticalBailout()` @11333 | bailing out of a failing vehicle has a direction and impulse |
| `GroundSnapDistanceCalculator` | `PlayerKinematicsRuntime.cs` | ground resolve | player stops floating over/sinking into ground (`groundSnap` count 0) |
| `StrafeAngleBlendWeightCalculator` | `PlayerKinematicsRuntime.cs` | locomotion blend | strafe animation blends (`strafe` count 2) |
| `SubmergedBuoyancyForce` | `BuoyancyObject.cs` | buoyancy tick | dropped objects float/sink correctly |
| `FluidVelocityFieldDragCalculator` | `SubmarineFluidDynamics.cs` | fluid step | submarine feels the flow field |
| `MaelstromSpatialWarpPullCalculator` | `HectonFluidEngine.cs` | maelstrom advance | maelstroms pull |
| `ScooterThrustCurveCalculator` | `MantaScooter.cs` | thrust apply | scooter thrust curves instead of stepping |
| `ArmReachIkSolver` | `ExosuitKinematicsRuntime.cs` | IK pass | exosuit arms reach targets (`Reach` count 2) |
| `ThrusterEfficiencyVsPressureCalculator` | `PlayerThrusterAudio.cs` | thruster state | thrust (and its audio) degrades with depth |

`WaterSurfaceTransitionDragCalculator` (stub @15150-15151, insertion point `UpdateWaterImmersion(float)`
@10069) is the one model my two tests disagree on: the broad token `WaterSurface` scores 134 in the host
while the specific `WaterSurfaceTransition` scores 2. I resolved it to **KEEP** (§6.1) rather than WIRE,
because a 134-hit host is far more likely to already handle immersion transition than not. It is counted
exactly once, in KEEP. If a reading of `UpdateWaterImmersion` shows no transition drag, move it to WIRE and
the counts become WIRE 85 / KEEP 31.

### 4.3 Base, habitat, fabrication & tools — 9 entries

| Model | Host (ALIVE) | Method the call belongs in | World gains |
|---|---|---|---|
| `FloodFillRoomVolumeCalculator` | `HabitatGraphManager.cs` | room rebuild | habitat rooms have real volumes — the input to flooding and atmosphere |
| `AnchorStabilityScoreCalculator` | `ConstructionManager.cs` | placement validation | structures can be refused on unstable ground |
| `PowerLoadBalancer` | `PowerGrid.cs` | grid solve | load spreads across sources instead of draining one |
| `FabricationCraftTimeModifier` | `FabricationAssemblerRuntime.cs` | craft start | craft time responds to skill/power/modifiers |
| `FabricationRecipeYieldRoll` | `FabricationAssemblerRuntime.cs` | craft complete | yields vary instead of being fixed |
| `FabricatorBuildProgressCurveCalculator` | `Fabricator.cs` | progress tick | the fabricator bar has a curve |
| `RepairRateMaterialCalculator` | `RepairTool.cs` | repair tick | repair speed depends on material |
| `WeldHeatDissipationCalculator` | `RepairTool.cs` | repair tick | welding builds and sheds heat |
| `LaserBeamIntensityAttenuationCalculator` | `LaserCutter.cs` | beam evaluate | cutting weakens with distance/water |

### 4.4 Survival & physiology — 8 entries

| Model | Host (ALIVE) | Method the call belongs in | World gains |
|---|---|---|---|
| `SuitO2ConsumptionModel` | `ShinobuPhysiologyRuntime.cs` (stub @2902) | O2 advance | oxygen drains with exertion, seal integrity and depth — `o2Consum` count 2 while `oxygen` count 19, i.e. the field exists and nothing consumes it |
| `Co2ScrubberEfficiencyModel` | `ShinobuPhysiologyRuntime.cs` | atmosphere advance | scrubbers actually scrub (`scrubber` count 2, `co2` count 8) |
| `HeartRateExertionModel` | `ShinobuPhysiologyRuntime.cs` | physiology advance | heart rate tracks exertion (`exertion` count 2) |
| `NitrogenNarcosisModel` | `ShinobuPhysiologyRuntime.cs` | physiology advance | narcosis accumulates from depth-time |
| `DecompressionNitrogenLoadCalculator` | `ShinobuPhysiologyRuntime.cs` (origin recorded loosely as "Shinobu namespace / Physiology") | physiology advance | ascent rate matters; the bends become a threat |
| `NitrogenNarcosisCriticalDepthCalculator` | `HectonPlayerMovement.cs` (stub @15170-15171) | `ApplyRuntimeNarcosisInputNoise(...)` @1797 | narcosis onset depth drives the existing input-noise path |
| `CaloricDeficitPenaltyCalculator` | `HectonSurvivalSystem.cs` | survival tick | starvation has a stat penalty — `caloric` 2 / `calorie` 0 while `hunger` 28 and `nutrition` 25, so hunger is tracked and does nothing |
| `FireOxygenConsumptionCalculator` | `SubmarineAtmosphereSystem.cs` | atmosphere tick | fire eats the room's oxygen |

### 4.5 Combat & ballistics — 6 entries

| Model | Host (ALIVE) | Method the call belongs in | World gains |
|---|---|---|---|
| `ExplosionRadialDamageCalculator` | `CombatDamageRuntime.cs` | damage resolve | explosions fall off with radius (`explosion` 2, `radial` 2) |
| `ProjectileDamageFalloffCalculator` | `CombatDamageRuntime.cs` | damage resolve | range matters (`falloff` 2) |
| `WaterPressureWeaponMultiplier` | `CombatDamageRuntime.cs` | damage resolve | weapons behave differently at depth |
| `BleedStackDecayModel` | `CombatDamageRuntime.cs` | status tick | bleed stacks decay instead of persisting |
| `ProjectileDropCalculator` | `BallisticsRuntime.cs` | trajectory step | projectiles drop (`gravity` count 0 in the ballistics runtime) |
| `SplashEntryAngleCalculator` | `BallisticsRuntime.cs` | water-entry branch | shots entering water deflect/decelerate by angle (`splash` 2, `entryAngle` 2) |

### 4.6 World gen, terrain & voxel — 6 entries

| Model | Host (ALIVE) | Method the call belongs in | World gains |
|---|---|---|---|
| `PoissondiscLandmarkSpacingSolver` | `WorldContentDirector.cs` | landmark placement | landmarks stop clumping (`poisson` 2, `spacing` 2, `landmark` 6) |
| `ProceduralFoliageScatterBudgetCalculator` | `ScatterBudgetController.cs` | budget refresh | foliage density respects a budget |
| `RockAlignmentSplineNormalCalculator` | `HectonRockManager.cs` | placement | rocks align to surface normals instead of floating |
| `VoxelSdfBooleanSubtraction` | `HectonVoxelEngine.cs` | deform apply | voxel subtraction has one hardened implementation |
| `VoxelMeshHeightSeamBlendCalculator` | `WorldGenerativeGeologyTerrainSeamApplier.cs` | seam apply | chunk seams blend in height |
| `TerrainSeamDitherAlphaCalculator` | `SeamGapDitherRenderer.cs` | dither resolve | seam gaps dither instead of showing a hard edge |

### 4.7 Celestial, tides & seismic — 4 entries

| Model | Host (ALIVE) | Method the call belongs in | World gains |
|---|---|---|---|
| `TidalForceAtPointCalculator` | `HectonCelestialEngine.cs` | tide advance | the moons move the water — `tidal` count 2 while `tide` count 53, so tides exist without a force model |
| `LunarPhaseCalculator` | `HectonCelestialEngine.cs` | celestial advance | moon phase is computed once, not duplicated (`lunar` 15, `moonPhase` 20 — likely a dedupe, see risk note) |
| `SolarHourAngleCalculator` | `HectonCelestialEngine.cs` | celestial advance | day/night angle is physically derived (`hourAngle` 2, `solar` 5) |
| `SeismicRichterDamageCalculator` | `HectonSeismicTideDirector.cs` | quake resolve | quakes damage by magnitude and distance |

### 4.8 Inventory & economy — 4 entries

| Model | Host (ALIVE) | Method the call belongs in | World gains |
|---|---|---|---|
| `WeightPenaltyCurveCalculator` | `PlayerInventory.cs` | `ApplyRuntimeInventoryMassLoad(...)` @1847/@1856 in `HectonPlayerMovement.cs` consumes the result | carry weight slows movement on a curve; **only referenced today by another PureLogic model** |
| `StackMergePriorityCalculator` | `InventoryRoutingNetwork.cs` | routing resolve | items merge into the right stack first |
| `StorageAutosorterCalculator` | `PDAInventoryTab.cs` | sort command | lockers auto-sort (`autosort` 2, `consolidat` 0, while generic `sort` 28) |
| `ScarcityPriceSpikeCalculator` | `ResourceScarcityDirector.cs` | scarcity refresh | prices react to depletion |

### 4.9 Audio — 3 entries

| Model | Host (ALIVE) | Method the call belongs in | World gains |
|---|---|---|---|
| `AudioDistanceAttenuationCurveCalculator` | `SpatialAudioManager.cs` | per-source update | one attenuation curve for all sources |
| `ReverbPreDelayCalculator` | `AdaptiveStemAudioMixer.cs` | reverb setup | pre-delay follows room size (`ReverbPre` count 0) |
| `PitchShiftResampleCalculator` | `DynamicMusicGranularSynthesizer.cs` | grain emit | granular pitch shift is correct |

### 4.10 Save & persistence — 3 entries

| Model | Host (ALIVE) | Method the call belongs in | World gains |
|---|---|---|---|
| `SaveDeltaCompressDiffCalculator` | `SaveBinaryPayloadCodec.cs` | payload write | saves store deltas, not full state |
| `HuffmanRleSaveDataCompressorCalculator` | `SaveBinaryPayloadCodec.cs` | payload write | save files shrink (`HuffmanRle` count 0) |
| `SaveDeltaVoxelStatePackingCalculator` | `SaveBinaryStorage.cs` | voxel sector write | voxel edits persist compactly |

### 4.11 HUD & visuals — 2 entries

| Model | Host (ALIVE) | Method the call belongs in | World gains |
|---|---|---|---|
| `CausticIntensityDepthCalculator` | `HectonUnderwaterVisuals.cs` | visual refresh | caustics fade correctly with depth |
| `DepthGaugeNonlinearCalculator` | `VisorHUDController.cs` | HUD refresh | the depth gauge reads non-linearly like a real one |

### 4.12 Quest — 1 entry

| Model | Host (ALIVE) | Method the call belongs in | World gains |
|---|---|---|---|
| `QuestObjectiveProgressNormalizer` | `QuestStateManager.cs` | objective update | objective progress normalises to 0-1 for UI |

### 4.13 Subsystem tally reconciliation

§4.1 ecosystem/fauna 21 · §4.2 movement/hydrodynamics 17 · §4.3 base/fabrication 9 · §4.4
survival/physiology 8 · §4.5 combat 6 · §4.6 worldgen/voxel 6 · §4.7 celestial 4 · §4.8 inventory/economy
4 · §4.9 audio 3 · §4.10 save 3 · §4.11 HUD 2 · §4.12 quest 1

21 + 17 + 9 + 8 + 6 + 6 + 4 + 4 + 3 + 3 + 2 + 1 = **84** ✔

Grouping is by *functional* subsystem, not by source folder, so a few `PureLogic/Systems` models appear
under ecosystem (`ChemicalDiffusionSolver`, `ToxinBioaccumulationCalculator`) and a `PureLogic/Ecosystem`
model appears under economy (`ScarcityPriceSpikeCalculator`). Folder totals for the 84: Systems 46,
Ecosystem 19, Kinematics 17, no-folder-origin 2.

---

## 5. Top 10 WIRE items by player-visible effect per unit of wiring work

Ranked on: one call into an already-running method, behaviour currently absent, effect a player would
notice in the first twenty minutes.

| # | Model(s) | Host | Why it ranks here |
|---|---|---|---|
| 1 | `FaunaSensoryDetectionRangeCalculator` | `FaunaDirector.cs` | Single cheapest step from "props that drift" to "creatures that notice you". Everything else in fauna behaviour is downstream of detection. |
| 2 | `OceanCurrentDragCalculator` + `ThermoclineResistanceCalculator` + `BuoyancyDensityRatioMath` | `HydrodynamicKccRuntime.cs` | Three calls in one live controller turn water from empty space into a medium. Highest ratio of felt change to lines touched in the whole list. |
| 3 | `SuitO2ConsumptionModel` | `ShinobuPhysiologyRuntime.cs` | The oxygen field already exists and nothing drains it. One call creates the core survival clock the entire game loop hangs on. |
| 4 | `PressureCrushDamageModel` | `HydrodynamicKccRuntime.cs` | Makes depth a threat, which is what makes depth interesting. Same host as #2, so it shares the wiring work. |
| 5 | `FaunaPheromoneTrackingVector` + `FaunaPatrolPathSmootherCalculator` | `FaunaDirector.cs` | Shares #1's insertion point. Converts detection into pursuit and routes. |
| 6 | `CaloricDeficitPenaltyCalculator` | `HectonSurvivalSystem.cs` | Hunger and nutrition are already tracked (28/25 mentions) and have no consequence. One call gives them teeth. |
| 7 | `TidalForceAtPointCalculator` | `HectonCelestialEngine.cs` | Tides already exist (53 mentions) with no force model. Wiring it links the sky to the sea — visible, large-scale, continuous. |
| 8 | `SchoolingSeparationForceCalculator` + `PredatorStalkSpeedCalculator` | `FaunaKinematicsRuntime.cs` | Fixes the most obvious visual tell of fake life: fish stacked at one point, predators at constant speed. |
| 9 | `ThermalVentUpdraftForce` | `HectonPlayerMovement.cs` | `ApplyThermalUpdrafts` already exists and runs at line 11064; this replaces its ad-hoc math. Vents become traversal. |
| 10 | `FloodFillRoomVolumeCalculator` | `HabitatGraphManager.cs` | Room volume is the missing input for flooding, atmosphere, and scrubbers. Unlocks several other rows rather than being an end in itself. |

Cheapest ordering by host, not by model: `HydrodynamicKccRuntime` (4 models, one file),
`FaunaDirector` (3, one file), `ShinobuPhysiologyRuntime` (4-5, one file), `EcosystemDirector` (4, one
file, and it is in `GameBootstrapper`'s `AddComponent` list so it is guaranteed to run),
`CombatDamageRuntime` (4, one file), `HectonCelestialEngine` (3, one file). Six files carry 22 of the 84.

---

## 6. KEEP — 32 entries

### 6.1 Tested twin of live inline math — 22 entries

The host **already implements this behaviour inline**, so wiring adds no capability and deleting would
throw away the only NaN-guarded, allocation-free, unit-tested version of shipping math. Neither action is
safe until someone diffs the model against the host's inline implementation. **I did not perform that
diff** — that is the follow-up work, and it is the reason these are KEEP and not DELETE.

| Model | Host | Host's inline concept count (evidence it is already implemented) |
|---|---|---|
| `ParasiteLatchDragCalculator` | `HectonPlayerMovement.cs` | `ParasiteLatch` 47 — full system incl. `ApplyParasiteLatchForces` @7278 |
| `WaterSurfaceTransitionDragCalculator`* | `HectonPlayerMovement.cs` | `WaterSurface` 134 |
| `ActiveSonarAttenuationCurveCalculator` | `HectonPlayerMovement.cs` | `ActiveSonar` 15 |
| `BrineSubmersionToxicityRate` | `HectonPlayerMovement.cs` | `BrineSubmersion` 6 — `UpdateBrineLayerState` @3054, `TryApplyBrineGasToxicity` @3100 |
| `WallSlideFrictionCalculator` | `HectonPlayerMovement.cs` | `WallSlide` 5, plus live contracts in `Core/Contracts/PlayerMovementContracts.cs`, `HectonPlayerMotor.cs`, `PlayerKinematicsRuntime.cs` |
| `ArmorPenetrationCalculator` | `CombatDamageRuntime.cs` | `ArmorPenetration` 20, `armor` 181 |
| `MarchingCubesLookupTable` | `HectonVoxelEngine.cs` | `MarchingCubes` 34 |
| `CeilingConcavityAirPocketVolumeCalculator` | `HectonVoxelEngine.cs` | `CeilingConcavity` 3 |
| `LaserCutDepthPowerCalculator` | `LaserCutter.cs` | `LaserCut` 80 |
| `LaserCutterVoxelDamageCalculator` | `LaserCutter.cs` | `LaserCutter` 68 |
| `AcousticZoneReverbDecay` | `AcousticZoneController.cs` | `AcousticZone` 141 |
| `BeaconNetworkSignalAttenuationCalculator` | `BeaconNetworkSystem.cs` | `BeaconNetwork` 64 — host is in `GameBootstrapper`'s `AddComponent` list |
| `VerletCableSimulator` | `CablePhysicsSolver132.cs` | `VerletCable` 45 |
| `CableConstraintSatisfier` | `CablePhysicsSolver132.cs` | `CableConstraint` 9 |
| `DroneTaskPriorityRanker` | `DroneFleetManager.cs` | `DroneTask` 44 |
| `AbyssalVortexAngularTorqueCalculator` | `HectonFluidEngine.cs` | `AbyssalVortex` 72 |
| `InventoryItemDefragmentationConsolidationCalculator` | `PlayerInventory.cs` | `InventoryItem` 33 |
| `BatteryChargeCurveCalculator` | `PowerGrid.cs` | `BatteryCharge` 12 |
| `PowerGridResourceDistributorCalculator` | `PowerGridManager.cs` | `PowerGrid` 52 — host is in `GameBootstrapper`'s `AddComponent` list |
| `SaveDataBinaryChecksumCalculator` | `SaveBinaryPayloadCodec.cs` | `SaveData` 291 |
| `SaveMerkleHashNodeCalculator` | `SaveStateMerkleTree.cs` | `SaveMerkle` 75 |
| `ThreatCostMultiplier` | `ThreatCostTable.cs` | `ThreatCost` 11 |

\* `WaterSurfaceTransitionDragCalculator` is the single contested row — see the note at the end of §4.2.
Its host stub is at `HectonPlayerMovement.cs:15150-15151` and its insertion point, if it moves to WIRE,
is `UpdateWaterImmersion(float)` @10069. Counted once, here in KEEP.

### 6.2 Host is dark — wiring blocked upstream — 10 entries

The model may be fine; its host shows no code instantiation and is on no prefab, so a call added here
would still never execute. **Do not delete these on this evidence** — scenes are binary and cannot be
searched, so scene placement is unprovable either way. Fix the host first, then re-triage.

| Model | Dark host | Note |
|---|---|---|
| `FlockingBoidCohesionVector` | `HectonBoidController.cs` | MonoBehaviour, no `AddComponent`, no prefab; non-test references are one shader and the two PureLogic models themselves. Note the host also carries a `public static CalculateSteerForce(...)` wrapper documented "Extracts calculation safely for tests" — a test-only bridge, not a runtime path. |
| `FlockingBoidSeparationVector` | `HectonBoidController.cs` | as above |
| `AtmosphereLeakRateCalculator` | `GasDynamicsSolver.cs` | host's non-test references not found; 54 word matches are its own file plus Editor tests |
| `GasMixturePartialPressureCalculator` | `GasDynamicsSolver.cs` | as above |
| `SignalPrioritySortCalculator` | `SignalBusRuntime.cs` | host referenced only from `Assets/_Project/Tests/Editor/*`. Also: per project trap, SignalBus consumers must be found by **signal type**, not by runtime class name — treat this host verdict as low confidence. |
| `NutrientCycleSinkCalculator` | `MacroEcosystemMathematicianRuntime.cs` | the sibling model `LotkaVolterraPopulationStep` **is** genuinely called at `Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs:2058` inside a real substep integration loop, so this host does host real wired math |
| `QuestDagUnlockChecker` | `QuestDagResolverRuntime.cs` | only 2 non-test word references |
| `DepositDepletionCurveCalculator` | `ProceduralOreSpawner.cs` | MonoBehaviour, no instantiation found |
| `SargassumKelpGrowthCurveCalculator` | `WorldProceduralScatterDirectorMigratorySargassum.cs` | 1 non-test reference |
| `DronePathfindCostCalculator` | `DroneFleetNavigationKernel.cs` | 1 non-test reference; sibling `DroneFleetManager` is alive, so re-check via the manager |

---

## 7. DELETE — 2 entries

Only two of 118 survive as defensible deletions. I refused to inflate this list: every other candidate
either has a live socket or an unprovable host, and "an agent could not find it" is not "nothing should
reference it".

| Model | Why | What supersedes it |
|---|---|---|
| `VoxelExplosionDeformationVolumeCalculator` | Its declared origin is `VoxelDeformationSmokeTester.cs` — a **smoke-test harness**, not a system. It was extracted from test scaffolding, so it never had a runtime design role to be unplugged from. Nothing references it and nothing should. | The real voxel deformation path is `HectonVoxelEngine` / `VoxelDeltaProcessor`; if explosion volume is wanted it belongs there, authored against that engine's data, not lifted from a tester. |
| `FixedCapacityRingBuffer` | Misnamed and vestigial: it is not a buffer. Its entire public API is `Calculate(int head, int tail, int capacity, bool isPush) -> int`, i.e. modular index arithmetic wrapped in 47 lines. No consumer, and no caller should take a dependency on a helper for `(i + 1) % capacity`. There is no other `RingBuffer` type in `Assets/_Project/Scripts` for it to be the tested twin of. | Inline modular arithmetic at the two or three call sites a real ring buffer would need. |

---

## 8. What this triage found that was not in the brief

1. **A second class of fake call site.** Beyond `_ = typeof(X)`, this codebase has `public static`
   wrapper methods that call a model and are themselves only invoked from tests — e.g.
   `HectonBoidController.CalculateSteerForce(...)` @2418, whose own doc comment reads *"Pure logic
   redirect for boid alignment force. Extracts calculation safely for tests."* It calls
   `FlockingBoidAlignmentVector.Calculate` and is wired to nothing in the boid update. **This means some
   of the 81 models I counted as "already referenced" are not really wired either.** I did not audit all
   81; that is the highest-value follow-up, because the true unwired count is above 118.
2. **One model has no test at all**: `2dGridHeatmapDecayCalculator` — zero references of any kind,
   including tests. It is the single most orphaned file in the set.
3. **Two models declare no usable origin**: `BloomTriggerThresholdCalculator` (no `Extracted from` line;
   its keep-alive lives in `Core/GlobalRegistry.cs`) and `DecompressionNitrogenLoadCalculator` (origin
   recorded as prose, "Shinobu namespace / Physiology"). Both look like net-new models rather than
   extractions.
4. **Six files carry 22 of the 84 WIRE items.** `HydrodynamicKccRuntime`, `FaunaDirector`,
   `ShinobuPhysiologyRuntime`, `EcosystemDirector`, `CombatDamageRuntime`, `HectonCelestialEngine`. A
   wave scoped one-host-per-agent is the natural next three waves, and those six files are wave one.
5. **The `JulesLink_*` stubs are a mechanical, greppable inventory of intent.** `rg -o 'JulesLink_(\w+)'`
   over `Assets/_Project/Scripts` enumerates the remaining work without needing this document. When a
   model is wired for real, its `#region JulesLink_<Name>` block should be deleted in the same commit —
   otherwise the stub keeps lying about the model being unwired.

---

## 9. Evidence status

| Claim | Status |
|---|---|
| 199 models; 118 with no call site outside PureLogic; 100 keep-alive-only | **Static, reproducible.** Full-tree scan of 3,755 `.cs` files, no truncation, no `head` bound. |
| `JulesLink_*` stub locations and line numbers | **Static, exact.** Read from source. |
| Host aliveness for the 104 ALIVE hosts | **Static.** Prefab GUID hits (prefabs are text YAML here) plus non-test instantiation/call evidence. |
| Host darkness for the 12 DARK hosts | **NOT PROVEN.** Four scenes including the world scene are binary; scene placement is unsearchable. Treat as unknown. |
| "Host does not implement this behaviour" | **Heuristic.** Keyword counts. Hand-verified with domain vocabulary for 13 hosts / ~40 models; unaided for the rest. A host may implement a behaviour under vocabulary I did not guess. |
| Any WIRE row is correct at the exact method named | **Partially proven.** The enclosing method was read from the host's method table; the precise insertion point inside it was not designed. |
| The 199 unit tests pass | **NOT RUN.** Counted only. |
| Anything in this document runs, compiles, or behaves as described | **NOT PROVEN.** No Unity, no player, no profiler, no device, no `dotnet` build was executed by this lane. The orchestrator holds the editor lock. |

Nothing in this document is a verified runtime claim. It is a static classification with named evidence,
intended to be argued with per row rather than trusted wholesale.

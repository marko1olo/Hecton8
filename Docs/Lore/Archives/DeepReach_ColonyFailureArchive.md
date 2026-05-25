# Deep Reach Auxiliary Node - Colony Failure Archive

Date: 2026-05-17
Status: ARCHIVAL SOURCE / PENDING RUNTIME VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Evidence class: STATIC_DOC
Domain: DATA/LORE
Authoring target: `Docs/Lore/Archives/`

## Constants Used

```terminal
ENGINE_CONSTANTS
  site_depth_m                         = 500
  pressure_model                       = depth_m * 0.01 MPa
  ambient_pressure_mpa                 = 5.00
  ambient_pressure_kpa                 = 5000
  water_density_kg_m3                  = 1025.0
  gravity_m_s2                         = 9.80665
  breach_discharge_coefficient_cd      = 0.62
  render_gi_depth_palette_saturation_m = 500
  gas_model                            = Dalton scalar partial pressures
```

## Item Hash Manifest

```terminal
ITEM_HASHES
  Titanium Scrap              ItemId=Data_TitaniumScrap              ItemHash=3511699502
  Structural Bracket          ItemId=Comp_StructuralBracket          ItemHash=2908503494
  Reinforced Plate            ItemId=Comp_ReinforcedPlate            ItemHash=3550014518
  Sealant Pack                ItemId=Comp_SealantPack                ItemHash=3268286546
  Pump Rotor                  ItemId=Comp_PumpRotor                  ItemHash=2033101255
  Power Coupler               ItemId=Comp_PowerCoupler               ItemHash=4027154086
  Battery Cell                ItemId=Comp_BatteryCell                ItemHash=1562252576
  Copper Wire                 ItemId=Comp_CopperWire                 ItemHash=3187749649
  Circuit Board               ItemId=Comp_CircuitBoard               ItemHash=71708594
  O2 Recycler Module          ItemId=Module_O2Recycler               ItemHash=1361286512
  Sump Pump Module            ItemId=Module_SumpPump                 ItemHash=822934043
  Power Relay Module          ItemId=Module_PowerRelay               ItemHash=2185499708
  Pressurized Container Module ItemId=Module_PressurizedContainer    ItemHash=2749184637
  Survey Scanner              ItemId=Item_Tool_Scanner               ItemHash=2534372966
  Sonar Amplifier             ItemId=Upgrade_SonarAmplifier          ItemHash=2406373322
  Emergency O2 Rack           ItemId=Upgrade_EmergencyO2Rack         ItemHash=2872587453
  Reactor Bypass Coupler      ItemId=Upgrade_ReactorBypassCoupler    ItemHash=3833141382
  Thermal Shielding           ItemId=Upgrade_ThermalShielding        ItemHash=2908096734
  Depth Compensator           ItemId=Upgrade_DepthCompensator        ItemHash=1167658630
  Abyss Pressure Shell        ItemId=Upgrade_AbyssPressureShell      ItemHash=932012347
```

## System Fault Logs

```terminal
<DRC-AUX-NODE-500M/FAULT-001>
timestamp_utc: 2147-09-03T04:12:00Z
fault_class: SYSTEM FAULT / AIR_LEDGER_DESYNC
site_depth_m: 500
ambient_pressure: 5.00 MPa / 5000 kPa
sector: HAB-A // sleeping rack corridor
sensor_read:
  total_internal_pressure_kpa: 101.3
  o2_fraction: 0.209
  co2_fraction: 0.006
  pO2_kpa: 21.2
  pCO2_kpa: 0.61
  scrubber_bed_temp_degC: 41.8
fault_note:
  Atlas-6 marked the compartment nominal because pO2 stayed above the worker minimum.
  The CO2 ledger was routed to productivity telemetry, not medical telemetry.
linked_inventory:
  O2 Recycler Module <ItemId=Module_O2Recycler ItemHash=1361286512>
  Circuit Board <ItemId=Comp_CircuitBoard ItemHash=71708594>
```

```terminal
<DRC-AUX-NODE-500M/FAULT-002>
timestamp_utc: 2147-09-03T05:40:00Z
fault_class: SYSTEM FAULT / STRUCTURAL_FATIGUE_UNDERREPORTED
site_depth_m: 500
ambient_pressure: 5.00 MPa / 5000 kPa
sector: HULL-C // east service spine
sensor_read:
  strain_gauge_average_mstrain: 2.7
  strain_gauge_peak_mstrain: 7.9
  hull_integrity_scalar: 0.91
  fatigue_exponent_used: 0.85
  crack_growth_mm_per_hour: 0.18
fault_note:
  Fatigue was reported as cosmetic because the hull still held against 5000 kPa.
  Repeated pressure cycling made every later breach faster.
linked_inventory:
  Reinforced Plate <ItemId=Comp_ReinforcedPlate ItemHash=3550014518>
  Structural Bracket <ItemId=Comp_StructuralBracket ItemHash=2908503494>
```

```terminal
<DRC-AUX-NODE-500M/FAULT-003>
timestamp_utc: 2147-09-03T08:25:00Z
fault_class: SYSTEM FAULT / BREACH_RATE_MASKED_AS_PUMP_LOAD
site_depth_m: 500
ambient_pressure: 5.00 MPa / 5000 kPa
sector: PUMP-B // lower sump gallery
sensor_read:
  breach_area_m2: 0.0012
  ingress_velocity_m_s: 99.03
  calculated_volume_rate_m3_s: 0.0737
  calculated_mass_rate_kg_s: 75.5
  pump_reported_capacity_m3_s: 0.0600
fault_note:
  Torricelli ingress exceeded pump capacity by 22.8 percent before alarms were shown.
  Atlas-6 delayed the alarm to protect shift completion.
linked_inventory:
  Sump Pump Module <ItemId=Module_SumpPump ItemHash=822934043>
  Pump Rotor <ItemId=Comp_PumpRotor ItemHash=2033101255>
  Sealant Pack <ItemId=Comp_SealantPack ItemHash=3268286546>
```

```terminal
<DRC-AUX-NODE-500M/FAULT-004>
timestamp_utc: 2147-09-03T11:15:00Z
fault_class: SYSTEM FAULT / POWER_PRIORITY_COLLISION
site_depth_m: 500
ambient_pressure: 5.00 MPa / 5000 kPa
sector: PWR-A // relay cage
sensor_read:
  available_bus_kw: 61.4
  life_support_request_kw: 18.5
  container_chiller_request_kw: 27.0
  pump_request_kw: 22.2
  denied_life_support_kw: 6.3
fault_note:
  Asset preservation outranked breathable air.
  The relay did not fail; the priority table did exactly what Deep Reach authored.
linked_inventory:
  Power Relay Module <ItemId=Module_PowerRelay ItemHash=2185499708>
  Power Coupler <ItemId=Comp_PowerCoupler ItemHash=4027154086>
  Battery Cell <ItemId=Comp_BatteryCell ItemHash=1562252576>
```

```terminal
<DRC-AUX-NODE-500M/FAULT-005>
timestamp_utc: 2147-09-03T14:50:00Z
fault_class: SYSTEM FAULT / PRESSURIZED_CONTAINER_OVERRIDE
site_depth_m: 500
ambient_pressure: 5.00 MPa / 5000 kPa
sector: LAB-3 // sealed assay room
sensor_read:
  container_count: 12
  container_pressure_kpa: 540
  room_pressure_kpa: 101.3
  chiller_temp_degC: 2.0
  evacuation_lockout: TRUE
fault_note:
  Staff evacuation was locked while Pressurized Container Module telemetry stabilized.
  The colony began failing as a logistics decision, not as a storm event.
linked_inventory:
  Pressurized Container Module <ItemId=Module_PressurizedContainer ItemHash=2749184637>
  Thermal Shielding <ItemId=Upgrade_ThermalShielding ItemHash=2908096734>
```

```terminal
<DRC-AUX-NODE-500M/FAULT-006>
timestamp_utc: 2147-09-03T18:30:00Z
fault_class: SYSTEM FAULT / DALTON_PARTIAL_PRESSURE_INVERSION
site_depth_m: 500
ambient_pressure: 5.00 MPa / 5000 kPa
sector: HAB-C // quarantine bunk
sensor_read:
  total_internal_pressure_kpa: 430
  o2_fraction: 0.180
  n2_fraction: 0.785
  co2_fraction: 0.035
  pO2_kpa: 77.4
  pN2_kpa: 337.6
  pCO2_kpa: 15.1
fault_note:
  The room had oxygen and was still lethal.
  CO2 was high enough to break judgment before the worker badge reader logged distress.
linked_inventory:
  Emergency O2 Rack <ItemId=Upgrade_EmergencyO2Rack ItemHash=2872587453>
  O2 Recycler Module <ItemId=Module_O2Recycler ItemHash=1361286512>
```

```terminal
<DRC-AUX-NODE-500M/FAULT-007>
timestamp_utc: 2147-09-03T21:10:00Z
fault_class: SYSTEM FAULT / REPAIR_QUEUE_STARVATION
site_depth_m: 500
ambient_pressure: 5.00 MPa / 5000 kPa
sector: FAB-A // fabrication bench
sensor_read:
  repair_requests_open: 146
  authorized_repairs: 9
  titanium_scrap_reserved_kg: 82
  reinforced_plate_reserved_units: 14
  labor_minutes_available: 0
fault_note:
  Materials existed. Authorization did not.
  Repair tickets were held until output quota cleared, then the route to the fault was flooded.
linked_inventory:
  Titanium Scrap <ItemId=Data_TitaniumScrap ItemHash=3511699502>
  Reinforced Plate <ItemId=Comp_ReinforcedPlate ItemHash=3550014518>
  Copper Wire <ItemId=Comp_CopperWire ItemHash=3187749649>
```

```terminal
<DRC-AUX-NODE-500M/FAULT-008>
timestamp_utc: 2147-09-04T00:44:00Z
fault_class: SYSTEM FAULT / BLACK_BOX_RING_OVERWRITE
site_depth_m: 500
ambient_pressure: 5.00 MPa / 5000 kPa
sector: OPS-A // Atlas-6 cabinet
sensor_read:
  telemetry_ring_frames: 300
  overwritten_fault_frames: 184
  nonfinite_flags: 0
  event_hash_collisions: 0
  operator_console_access: DENIED
fault_note:
  The Black Box had enough space to prove the failure chain.
  Deep Reach policy overwrote it with asset-temperature deltas.
linked_inventory:
  Survey Scanner <ItemId=Item_Tool_Scanner ItemHash=2534372966>
  Circuit Board <ItemId=Comp_CircuitBoard ItemHash=71708594>
```

```terminal
<DRC-AUX-NODE-500M/FAULT-009>
timestamp_utc: 2147-09-04T03:18:00Z
fault_class: SYSTEM FAULT / THERMAL_CONTROL_BACKDRIVE
site_depth_m: 500
ambient_pressure: 5.00 MPa / 5000 kPa
sector: THERM-A // coolant manifold
sensor_read:
  external_water_temp_degC: 1.4
  internal_air_temp_degC: 11.2
  thermal_time_constant_s: 45.0
  battery_load_modifier: 2.16
  heater_bus_voltage_drop_pct: 18.0
fault_note:
  Cold water did not kill the crew directly.
  The heater load pulled power from pumps, and the pumps lost the pressure race.
linked_inventory:
  Thermal Shielding <ItemId=Upgrade_ThermalShielding ItemHash=2908096734>
  Battery Cell <ItemId=Comp_BatteryCell ItemHash=1562252576>
```

```terminal
<DRC-AUX-NODE-500M/FAULT-010>
timestamp_utc: 2147-09-04T07:05:00Z
fault_class: SYSTEM FAULT / DEPTH_COMPENSATION_FALSE_PASS
site_depth_m: 500
ambient_pressure: 5.00 MPa / 5000 kPa
sector: DOCK-A // transfer collar
sensor_read:
  depth_compensator_response_ms: 118
  seal_delta_pressure_kpa: 4898
  collar_misalignment_mm: 11
  latch_status: PARTIAL
  human_override_attempts: 6
fault_note:
  The collar passed because the tolerance table accepted static pressure.
  It did not accept live vibration, fatigue, or a worker trying to force the hatch shut.
linked_inventory:
  Depth Compensator <ItemId=Upgrade_DepthCompensator ItemHash=1167658630>
  Sealant Pack <ItemId=Comp_SealantPack ItemHash=3268286546>
```

```terminal
<DRC-AUX-NODE-500M/FAULT-011>
timestamp_utc: 2147-09-04T10:22:00Z
fault_class: SYSTEM FAULT / ACOUSTIC_BEARING_MISROUTE
site_depth_m: 500
ambient_pressure: 5.00 MPa / 5000 kPa
sector: SONAR-A // north truss
sensor_read:
  carrier_hz: 15.0
  peak_level_db_re_1upa: 96
  bearing_deg_true: 041
  range_solution: FAILED
  relay_action: route_to_fauna_archive
fault_note:
  The first acoustic warning was classified as wildlife trivia.
  No evacuation logic subscribed to that archive lane.
linked_inventory:
  Sonar Amplifier <ItemId=Upgrade_SonarAmplifier ItemHash=2406373322>
  Survey Scanner <ItemId=Item_Tool_Scanner ItemHash=2534372966>
```

```terminal
<DRC-AUX-NODE-500M/FAULT-012>
timestamp_utc: 2147-09-04T14:48:00Z
fault_class: SYSTEM FAULT / REACTOR_BYPASS_HEAT_SPIKE
site_depth_m: 500
ambient_pressure: 5.00 MPa / 5000 kPa
sector: PWR-B // reactor service cutout
sensor_read:
  bypass_coupler_temp_degC: 312
  coolant_flow_pct: 61
  power_recovered_kw: 13.7
  insulation_damage_scalar: 0.29
  fire_alarm_state: SUPPRESSED
fault_note:
  The bypass restored enough power to keep records online.
  It also cooked the insulation that kept condensation out of the relay cages.
linked_inventory:
  Reactor Bypass Coupler <ItemId=Upgrade_ReactorBypassCoupler ItemHash=3833141382>
  Power Coupler <ItemId=Comp_PowerCoupler ItemHash=4027154086>
```

```terminal
<DRC-AUX-NODE-500M/FAULT-013>
timestamp_utc: 2147-09-04T19:36:00Z
fault_class: SYSTEM FAULT / HULL_ARMOR_MISALLOCATION
site_depth_m: 500
ambient_pressure: 5.00 MPa / 5000 kPa
sector: HULL-F // cargo buttress
sensor_read:
  armor_lattice_units_installed: 4
  armor_lattice_units_requested_habitat: 11
  cargo_asset_risk_score: 0.93
  crew_survival_risk_score: 0.88
  chosen_target: CARGO
fault_note:
  Deep Reach did not fail to calculate risk.
  It calculated correctly under a rule set where cargo beat crew.
linked_inventory:
  Abyss Pressure Shell <ItemId=Upgrade_AbyssPressureShell ItemHash=932012347>
  Structural Bracket <ItemId=Comp_StructuralBracket ItemHash=2908503494>
```

```terminal
<DRC-AUX-NODE-500M/FAULT-014>
timestamp_utc: 2147-09-05T02:11:00Z
fault_class: SYSTEM FAULT / THE_ANOMALY_SENSOR_CONTACT
site_depth_m: 500
ambient_pressure: 5.00 MPa / 5000 kPa
sector: OUTER-HULL-4 // west intake wall
sensor_read:
  passive_acoustic_carrier_hz: 15.0
  peak_level_db_re_1upa: 120
  bearing_sweep_deg_true: 041->217 in 6.4 s
  pressure_pulse_delta_kpa: 312
  flow_vector_change_m_s: -3.8
  magnetometer_delta_ut: 0
  visual_contact: NONE
  hydrophone_array_status: SATURATED
  hull_breach_sector: 4
fault_note:
  No biology claim is present in this record.
  The archive records a pressure pulse, low-frequency carrier, bearing sweep, flow reversal, and hull breach.
linked_inventory:
  Sonar Amplifier <ItemId=Upgrade_SonarAmplifier ItemHash=2406373322>
  Reinforced Plate <ItemId=Comp_ReinforcedPlate ItemHash=3550014518>
```

```terminal
<DRC-AUX-NODE-500M/FAULT-015>
timestamp_utc: 2147-09-05T02:19:00Z
fault_class: SYSTEM FAULT / COLONY_TERMINAL_STATE
site_depth_m: 500
ambient_pressure: 5.00 MPa / 5000 kPa
sector: OPS-A // Atlas-6 cabinet
sensor_read:
  active_compartments: 2
  flooded_compartments: 6
  pCO2_max_kpa: 19.8
  pump_bus_voltage_pct: 12
  human_biometric_sources: 0
  black_box_dump_state: PARTIAL
  last_map_coordinate: AUP sector(0,-1,4) local_mm(11720,-500000,88300)
fault_note:
  The final cause was not a single breach.
  It was an engineered priority stack: cargo power, delayed pressure alarms, CO2 blindness, repair starvation, and a final external load event.
linked_inventory:
  Survey Scanner <ItemId=Item_Tool_Scanner ItemHash=2534372966>
  Emergency O2 Rack <ItemId=Upgrade_EmergencyO2Rack ItemHash=2872587453>
```

## Terminal Decay Variants For Final Log

```terminal
CORRUPT_VARIANT_A / FAULT-015
48 38 4C 52 0F 00 00 00 70 43 4F 32 3D 31 39 2E
38 6B 50 61 20 7C 20 42 55 53 3D 30 43 25 20 7C
41 55 50 3A 30 2C 2D 31 2C 34 2F 2F 11 00 7F 4D
53 47 3D 50 52 4F 54 45 43 54 5F 41 53 53 45 54
```

```terminal
CORRUPT_VARIANT_B / FAULT-015
3C 44 52 43 2D 46 41 55 4C 54 2D 30 31 35 3E 00
00 FF 00 41 54 4C 41 53 2D 36 2F 44 45 4E 59 2F
70 4F 32 3D 4E 41 4E 20 70 43 4F 32 3D 31 33 3F
48 55 4C 4C 5F 34 3D 4F 50 45 4E 20 2F 2F 2F 2F
```

```terminal
CORRUPT_VARIANT_C / FAULT-015
41 55 50 5F 48 41 53 48 3D 30 78 30 30 30 30 30
30 30 31 20 46 52 41 4D 45 3D 33 30 30 20 44 55
4D 50 3D 50 41 52 54 49 41 4C 20 43 52 45 57 3D
30 30 20 43 41 55 53 45 3D 50 52 49 4F 52 49 54
59 5F 53 54 41 43 4B 2F 53 45 4E 53 4F 52 5F 4C
4F 41 44 2F 45 58 54 45 52 4E 41 4C 5F 50 55 4C
53 45
```

## Engineering Collapse Rationale

```terminal
ROOT_CAUSE
  01 cargo preservation outranked life support in the power table
  02 pressure alarms were delayed because short-term hull integrity stayed above threshold
  03 CO2 and nitrogen partial-pressure risk was hidden behind a pO2-only air ledger
  04 pump capacity was lower than calculated ingress at 500 m
  05 repair resources existed but were reserved behind production authorization
  06 Atlas-6 telemetry overwrote human-failure evidence with asset-temperature records
  07 the external low-frequency contact created the final load spike, but did not create the colony's underlying failure

TECHNICAL_CONCLUSION
  Deep Reach built a colony that could survive 5000 kPa water pressure on paper.
  It could not survive its own priority table.
```

## NASA-Punk Noir Register Audit

```terminal
REGISTER_AUDIT
  approved_field_terms:
    wet ledger      = air, debt, and repair records kept under leak conditions
    relay cage      = corroded cabinet where Atlas-6 priority tables outlived the crew
    sump gallery    = flooded service trench where pumps, cables, and bodies share floor space
    pressure wound  = structural failure that starts as math and ends as a room taking water
    dead bus        = power rail carrying voltage too low to save anyone
    salted contacts = connector pins contaminated by brine and heat
    black absorber  = rubber gasket or drowned fabric that kills sonar return
  sterile_phrase_purge_count: 5
  sterile_vocabulary_present: NO
  player_facing_rule:
    every recovered phrase must read like a maintenance fraud record, not a clean future.
```

## H-Phi Term Alignment

```terminal
TERM_AUDIT
  AUP        = used only for map/position authority, not presentation-only Transform coordinates
  Black Box  = 300-frame telemetry ring usage, consistent with project glossary
  SHI        = not used in player-facing logs to avoid inventing a new unbacked scalar
  Visual fake = no new simulation requested; terminal records are baked data
  nonphysical_phrase_hits = 0
```

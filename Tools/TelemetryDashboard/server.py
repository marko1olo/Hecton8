from __future__ import annotations

import csv
import json
import math
import re
import struct
from collections import deque
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from fastapi import FastAPI
from fastapi.responses import FileResponse, JSONResponse


PROJECT_ROOT = Path(__file__).resolve().parents[2]
DASHBOARD_ROOT = Path(__file__).resolve().parent
AGENT_LOGS = PROJECT_ROOT / "Docs" / "AgentLogs"
HPHI_REPORT = PROJECT_ROOT / "Docs" / "Reports" / "HECTON_PHI_REPORT.md"
INDEX_HTML = DASHBOARD_ROOT / "index.html"

MAX_CSV_ROWS = 600
MAX_DUMP_ENTRIES = 600
MAX_DUMP_BYTES = 10 * 1024 * 1024
FRAME_SPIKE_MS = 16.6

NO_STORE_HEADERS = {
    "Cache-Control": "no-store, max-age=0",
    "Pragma": "no-cache",
    "X-Content-Type-Options": "nosniff",
}

HECTON8_MAGIC = 0x00384E4F54434548
BIOMASS_MAGIC = 0x0038424D53434548
MACRO_SWARM_MAGIC = 0x004D57534F434548
FAUNA_MUTATION_MAGIC = 0x004D55474F434548
FAUNA_GENETICS_MAGIC = 0x00474E474F434548
HEADLESS_MAGIC = 0x48385142
LIVE_TELEMETRY_MAGIC = 0x4D4C4554
GLOBAL_TELEMETRY_BUS_DUMP_MAGIC = 0x4838444D

GENERIC_BLACKBOX_HEADER = struct.Struct("<QII")
GENERIC_BLACKBOX_ENTRY = struct.Struct("<IIfffffffIIIIIII")
CRASH_TELEMETRY_HEADER = GENERIC_BLACKBOX_HEADER
CRASH_TELEMETRY_ENTRY = GENERIC_BLACKBOX_ENTRY
JOB_ADMISSION_BLACKBOX_HEADER = struct.Struct("<QIiiiII")
JOB_ADMISSION_BLACKBOX_ENTRY_PREFIX = struct.Struct("<IIffiIBBHI")
SIMULATION_BUCKET_BLACKBOX_HEADER = struct.Struct("<QIiiiiI")
SIMULATION_BUCKET_BLACKBOX_ENTRY = struct.Struct("<iiiiiiIIffffffBBHI")
TERRAIN_STREAMING_HEADER = struct.Struct("<QIIII")
TERRAIN_STREAMING_PAGER_ENTRY = struct.Struct("<dddIIHHHHfIfIII")
WORLD_CHUNK_RESIDENCY_HEADER = struct.Struct("<QIIIIII")
WORLD_CHUNK_RESIDENCY_ENTRY = struct.Struct("<qqqqfffIIIHHHH")
DEFRAG_ENTRY_PACK1 = struct.Struct("<IIiqqqqqfiBBBB")
DEFRAG_ENTRY_ALIGNED = struct.Struct("<IIi4xqqqqqfiBBBB4x")
THERMAL_HEADER = struct.Struct("<II")
THERMAL_ENTRY_MANUAL = struct.Struct("<IIIhBBBBB")
BIOMASS_HEADER = struct.Struct("<Qiiii")
BIOMASS_ENTRY = struct.Struct("<IIiiffff")
MACRO_SWARM_ENTRY = struct.Struct("<IIiifiII")
FAUNA_MUTATION_ENTRY = struct.Struct("<IIiiiIfffII4x")
FAUNA_GENETICS_ENTRY = struct.Struct("<IIiiiifffffIIIII")
HEADLESS_HEADER = struct.Struct("<Iiii")
HEADLESS_ENTRY = struct.Struct("<IiIqqqffffffI")
LIVE_TELEMETRY_ENTRY_V1 = struct.Struct("<IIIIIfff")
LIVE_TELEMETRY_ENTRY_V2 = struct.Struct("<IIIIIIfffffIIIII")
LIVE_TELEMETRY_ENTRY = LIVE_TELEMETRY_ENTRY_V2
DATA_MONOLITH_TELEMETRY_HEADER = struct.Struct("<IIiii")
DATA_MONOLITH_TELEMETRY_ENTRY = struct.Struct("<Qqq" + "I" * 10)
GLOBAL_TELEMETRY_PREFIX = struct.Struct("<QII")
SURVIVAL_BLACKBOX_SOURCE_ENTRY = struct.Struct("<III" + "f" * 11 + "II")
VAULT_SOVEREIGNTY_TELEMETRY_HEADER = struct.Struct("<Qiii")
VAULT_SOVEREIGNTY_TELEMETRY_ENTRY = struct.Struct("<qqiiifIIIIfIQ")
ARM64_ALIGNMENT_TELEMETRY_HEADER = struct.Struct("<Qiii")
ARM64_ALIGNMENT_TELEMETRY_ENTRY = struct.Struct("<QQdddIIIIfI")
HAPTIC_SYNTHESIS_TELEMETRY_ENTRY = struct.Struct("<dddffIIIIIfII")
VOCAL_WARNING_TELEMETRY_HEADER = struct.Struct("<IIIIIIII")
VOCAL_WARNING_TELEMETRY_ENTRY = struct.Struct("<qqqQIIIffIIHBB")
GRANULAR_AUDIO_TELEMETRY_HEADER = struct.Struct("<ii")
GRANULAR_AUDIO_TELEMETRY_ROW = struct.Struct("<IffffffiiiI")
PROLOGUE_AUDIO_TRANSITION_HEADER = struct.Struct("<ii")
PROLOGUE_AUDIO_TRANSITION_ROW = struct.Struct("<IIffffffffiBBBBI")
AUDIO_SYNTHESIS_TELEMETRY_HEADER = struct.Struct("<ii")
AUDIO_SYNTHESIS_TELEMETRY_ROW = struct.Struct("<qIIIIIIiiffii")
VOCAL_BANK_SYNTHESIS_HEADER = struct.Struct("<IIIIIIII")
VOCAL_BANK_SYNTHESIS_ENTRY = struct.Struct("<IIIIfffffiIIIII4x")
ADAPTIVE_STEM_MIXER_ENTRY = struct.Struct("<IIIIffffffffffff")
CAMERA_JUICE_TELEMETRY_HEADER = struct.Struct("<IIiiIiiI")
CAMERA_JUICE_TELEMETRY_ENTRY = struct.Struct("<IIffffffffifffII")
MATERIAL_DECAY_HEADER = struct.Struct("<IBii")
MATERIAL_DECAY_ROW = struct.Struct("<IIfffHBBBI")
INTERACTIVE_WAKE_HEADER = struct.Struct("<Iii")
INTERACTIVE_WAKE_ENTRY = struct.Struct("<IHHffffffffIIIIff")
FLORA_SWAY_FIELD_HEADER = struct.Struct("<Iii")
FLORA_SWAY_FIELD_ENTRY = struct.Struct("<IHHIIffffffffIIII")
FLORA_MEMORY_TELEMETRY_HEADER = struct.Struct("<ii")
FLORA_MEMORY_TELEMETRY_ENTRY = struct.Struct("<IIIIIIIIIIffIIII")
FLORA_AMBIENT_SWAY_HEADER = struct.Struct("<IIIIII")
FLORA_AMBIENT_SWAY_ENTRY = struct.Struct("<IIffffII32x")
VEGETATION_MEMORY_HEADER = struct.Struct("<Qiiii")
VEGETATION_MEMORY_ENTRY = struct.Struct("<QIIIiiiffHHIfffI")
DEAR_LIE_ORGANICS_ENTRY = struct.Struct("<iiiiiiiiiifIIfB7x")
CHEMICAL_INFLUENCE_HEADER = struct.Struct("<Qiii")
CHEMICAL_INFLUENCE_ENTRY = struct.Struct("<dddffIiiiIIfi")
SARGASSUM_FOOD_CHAIN_HEADER = struct.Struct("<IIiiiI")
SARGASSUM_FOOD_CHAIN_ENTRY = struct.Struct("<IIIIiiiiffffffIf")
SARGASSUM_BOID_SENSORY_HEADER = struct.Struct("<IIiiiI")
SARGASSUM_BOID_SENSORY_ENTRY = struct.Struct("<IIIi" + "f" * 12)
MARINE_SNOW_VFX_HEADER = struct.Struct("<IIII")
MARINE_SNOW_VFX_ENTRY = struct.Struct("<iiiiffffffffIIiI")
PROPWASH_GPU_HEADER = struct.Struct("<IIII")
PROPWASH_GPU_ENTRY = struct.Struct("<iiiifffffffIIIII")
CARVE_DEBRIS_HEADER = struct.Struct("<IIIII")
CARVE_DEBRIS_ENTRY = struct.Struct("<IiiiIIfffIIIIIII")
BIOLUM_PULSE_HEADER = struct.Struct("<IBBHii")
BIOLUM_PULSE_ENTRY = struct.Struct("<IIfffffHBB32s")
BIOLUM_DIRECTOR_HEADER = struct.Struct("<IIBi")
BIOLUM_DIRECTOR_ENTRY = struct.Struct("<IffffffHBB")
TOXIC_OUTGASSING_HEADER = struct.Struct("<IIIIIIII")
TOXIC_OUTGASSING_ENTRY = struct.Struct("<dddffffIIHHHBBQ")
GAS_DYNAMICS_HEADER = struct.Struct("<Iiiiii")
GAS_DYNAMICS_ENTRY = struct.Struct("<QIiffffIIIIifIHH")
BASE_ATMOSPHERE_LOGISTICS_HEADER = struct.Struct("<Qii")
BASE_ATMOSPHERE_LOGISTICS_ENTRY = struct.Struct("<QfffffiiiiiiiII")
STORM_PROPAGATION_HEADER = struct.Struct("<IIiiiIII")
STORM_PROPAGATION_ENTRY = struct.Struct("<IIffffffffffffIi")
OCEAN_SURFACE_ATMOSPHERE_HEADER = struct.Struct("<IIIIIIII")
OCEAN_SURFACE_ATMOSPHERE_ENTRY = struct.Struct("<IIffqfifffffIii")
THERMODYNAMICS_HAZARD_HEADER = struct.Struct("<Qiii")
THERMODYNAMICS_HAZARD_ENTRY = struct.Struct("<ffffffIIIIIIIIBBHI")
ABYSSAL_THERMODYNAMICS_ENTRY = struct.Struct("<ffffdddIIIIII")
REACTOR_THERMAL_ENTRY = struct.Struct("<dddfffff" + "I" * 13 + "32x")
NUCLEAR_REACTOR_THERMAL_ENTRY = struct.Struct("<dddffffff" + "I" * 12 + "32x")
FOVEATED_SIMULATION_HEADER = struct.Struct("<Iii")
FOVEATED_SIMULATION_ENTRY = struct.Struct("<iiiiiiffffffII")
INPUT_DETERMINISM_ENTRY = struct.Struct("<dIIIIIIHH")
ORIGIN_SHIFT_HEADER = struct.Struct("<QIIIIIIIIIII")
ORIGIN_SHIFT_BASE_ENTRY = struct.Struct("<dddIIIIiiiiiI")
ORIGIN_SHIFT_DETAIL_ENTRY = struct.Struct("<dddfffffIIi8x")
BINARY_LAYOUT_SENTINEL_HEADER = struct.Struct("<IiIiiiI")
TERMINAL_OS_HEADER = struct.Struct("<IIIiiiiI")
TERMINAL_OS_ENTRY = struct.Struct("<iiiifffIIIffiiff")
TERMINAL_DECRYPTION_HEADER = struct.Struct("<IIIiii")
TERMINAL_DECRYPTION_ENTRY = struct.Struct("<IIffffffIIII16x")
TERMINAL_PROJECTION_HEADER = struct.Struct("<IIIIIIII32x")
TERMINAL_PROJECTION_ENTRY = struct.Struct("<iiiifffIIIIffi8x")
OPENXR_MANUAL_OVERRIDE_HEADER = struct.Struct("<ii")
OPENXR_MANUAL_OVERRIDE_ENTRY = struct.Struct("<fffffffffIB")
VEHICLE_DAMAGE_HOLOGRAPHER_HEADER = struct.Struct("<Iiii")
VEHICLE_DAMAGE_HOLOGRAPHER_ENTRY = struct.Struct("<iiiffI")
PDA_PROJECTION_HEADER = struct.Struct("<IIIIiiiiii24x")
PDA_PROJECTION_ENTRY = struct.Struct("<IIIIfffIIIfffffI")
WRIST_HUD_HEADER = struct.Struct("<IIIIiiii")
WRIST_HUD_ENTRY = struct.Struct("<IIIIIIIIffffffff")
LADDER_CLIMB_IK_HEADER = struct.Struct("<IIIIII")
LADDER_CLIMB_IK_ENTRY_PREFIX = struct.Struct("<" + "f" * 17 + "iiiII")
TOPOGRAPHICAL_SONAR_HEADER = struct.Struct("<IIIIIIII")
TOPOGRAPHICAL_SONAR_ENTRY = struct.Struct("<dddddddIiiiiIffffffffffII")
KINETIC_CHARACTER_HEADER = struct.Struct("<IIIIII")
KINETIC_CHARACTER_ENTRY = struct.Struct("<qqqfffIiffIIf")
PROCEDURAL_BONE_HEADER = struct.Struct("<IIIIII")
PROCEDURAL_BONE_ENTRY = struct.Struct("<IiiifIIfffiifffI")
VR_SOMATIC_HEADER = struct.Struct("<IIii")
VR_SOMATIC_ENTRY = struct.Struct("<iIHH" + "f" * 12 + "I" + "f" * 4 + "IIIIQ24x")
LOCKSTEP_STATE_VALIDATOR_HEADER = struct.Struct("<QIIIIQ")
LOCKSTEP_STATE_VALIDATOR_ENTRY = struct.Struct("<" + "I" * 16)
VOXEL_ASTAR_ENTRY = struct.Struct("<IIIIIIIIIIIIffHHI")
PATH_FUNNEL_ENTRY = struct.Struct("<QQIIIIfIHHHH16x")
PDA_FREQUENCY_TUNING_HEADER = struct.Struct("<ii")
PDA_FREQUENCY_TUNING_ENTRY = struct.Struct("<IIfffffHBB")
COMPASS_GYRO_HEADER = struct.Struct("<Iii")
COMPASS_GYRO_ENTRY = struct.Struct("<IffffffIIi24x")
PDA_ENCYCLOPEDIA_HEADER = struct.Struct("<IIIIII8x")
PDA_ENCYCLOPEDIA_ENTRY = struct.Struct("<IIIIIIIIqqIIII")
HABITAT_FLOOD_HEADER = struct.Struct("<IIIII")
HABITAT_FLOOD_ENTRY = struct.Struct("<IHHHHffffIII")
CONSTRUCTION_VALIDATION_ENTRY = struct.Struct("<dddiiiIIffIII")
CONSTRUCTION_SOCKET_ENTRY = struct.Struct("<dddIIIIffIIfI")
CONSTRUCTION_HOLOGRAPHY_ENTRY = struct.Struct("<dddIIIIffIf8x")
LASER_CUTTER_DOD_HEADER = struct.Struct("<IIIIIIII")
LASER_CUTTER_DOD_ENTRY = struct.Struct("<" + "d" * 6 + "f" * 6 + "I" * 9 + "fQfI")
WFC_LASER_CUT_HEADER = struct.Struct("<IIIIIIII")
WFC_LASER_CUT_ENTRY = struct.Struct("<ddddddQIIfffffIHBxI")
TOOL_KINEMATICS_HEADER = struct.Struct("<IIIIIIII")
TOOL_KINEMATICS_ENTRY = struct.Struct("<IIfffifIffffffII")
AUXILIARY_EQUIPMENT_ENTRY = struct.Struct("<IIIIIfffIIIIIIQ")
UPGRADE_MATRIX_ENTRY = struct.Struct("<IIIIfIIIQQQQ")
METABOLISM_BLACKBOX_HEADER = struct.Struct("<QIIIIII")
METABOLISM_TELEMETRY_ENTRY = struct.Struct("<QIIfffIIIfffIII")
METABOLISM_DETAIL_TELEMETRY_ENTRY = struct.Struct("<dddffffffIIII")
PHYSIOLOGY_AUTOPSY_HEADER = struct.Struct("<QIIIIIIIIII")
PHYSIOLOGY_TELEMETRY_ENTRY = struct.Struct("<QQIIffffffffIf")
DECOMPRESSION_TELEMETRY_ENTRY = struct.Struct("<QIIffffffffIIII")
SENSORY_IMPAIRMENT_HEADER = struct.Struct("<QIIIIII")
SENSORY_IMPAIRMENT_ENTRY = struct.Struct("<QIIfffffffffffI")
SUIT_INTEGRITY_HEADER = struct.Struct("<QIIIIII")
SUIT_INTEGRITY_ENTRY = struct.Struct("<QIIfffffffIIfII")
RADIATION_MUTATION_HEADER = struct.Struct("<QIIIIII")
RADIATION_MUTATION_ENTRY = struct.Struct("<QIIffffffffffII")
RESPAWN_RECONCILIATION_HEADER = struct.Struct("<QIiiI")
RESPAWN_RECONCILIATION_ENTRY = struct.Struct("<ddddddIIfI")

GLOBAL_TELEMETRY_BUS_DUMP_VERSION = 2
GLOBAL_TELEMETRY_BUS_HEADER_BYTES = 1024
GLOBAL_TELEMETRY_BUS_HASH_HISTORY_COUNT = 100
GLOBAL_TELEMETRY_BUS_SOURCE_CAPACITY = 50
GLOBAL_TELEMETRY_BUS_SOURCE_STRIDE_BYTES = 64
GLOBAL_TELEMETRY_BUS_METADATA_OFFSET = GLOBAL_TELEMETRY_PREFIX.size
GLOBAL_TELEMETRY_BUS_SOURCE_DESCRIPTOR_METADATA_INDEX = 32
GLOBAL_TELEMETRY_BUS_SOURCE_DESCRIPTOR_UINT_STRIDE = 4
GLOBAL_TELEMETRY_BUS_MAX_FRAME_STRIDE_BYTES = 64 * 1024
DATA_MONOLITH_TELEMETRY_MAGIC = 0x4858444D
DATA_MONOLITH_TELEMETRY_HEADER_BYTES = 20
DATA_MONOLITH_TELEMETRY_ENTRY_BYTES = 64
DATA_MONOLITH_TELEMETRY_RING_CAPACITY = 300
DATA_MONOLITH_MAX_BLOB_BYTES = 256 * 1024 * 1024
VAULT_SOVEREIGNTY_TELEMETRY_MAGIC = 0x3030315F55424F53
VAULT_SOVEREIGNTY_TELEMETRY_VERSION = 1
VAULT_SOVEREIGNTY_TELEMETRY_HEADER_BYTES = 20
VAULT_SOVEREIGNTY_TELEMETRY_ENTRY_BYTES = 64
VAULT_SOVEREIGNTY_TELEMETRY_CAPACITY = 300
ARM64_ALIGNMENT_TELEMETRY_MAGIC = 0x3430325F55424F53
ARM64_ALIGNMENT_TELEMETRY_VERSION = 1
ARM64_ALIGNMENT_TELEMETRY_HEADER_BYTES = 20
ARM64_ALIGNMENT_TELEMETRY_ENTRY_BYTES = 64
ARM64_ALIGNMENT_TELEMETRY_CAPACITY = 300
HAPTIC_SYNTHESIS_TELEMETRY_ENTRY_BYTES = 64
HAPTIC_SYNTHESIS_TELEMETRY_CAPACITY = 300
HAPTIC_SYNTHESIS_PULSE_CAPACITY = 64
VOCAL_WARNING_TELEMETRY_MAGIC = 0x56333532
VOCAL_WARNING_TELEMETRY_VERSION = 2
VOCAL_WARNING_TELEMETRY_HEADER_BYTES = 32
VOCAL_WARNING_TELEMETRY_ENTRY_BYTES = 64
VOCAL_WARNING_TELEMETRY_CAPACITY = 300
GRANULAR_AUDIO_TELEMETRY_HEADER_BYTES = 8
GRANULAR_AUDIO_TELEMETRY_ROW_BYTES = 44
GRANULAR_AUDIO_TELEMETRY_CAPACITY = 300
GRANULAR_AUDIO_VOICE_CAPACITY = 64
GRANULAR_AUDIO_ECHO_TAP_CAPACITY = 32
GRANULAR_AUDIO_MAX_SAFE_IMPACT_JOULES = 120000.0
PROLOGUE_AUDIO_TRANSITION_HEADER_BYTES = 8
PROLOGUE_AUDIO_TRANSITION_ROW_BYTES = 52
PROLOGUE_AUDIO_TRANSITION_CAPACITY = 300
PROLOGUE_AUDIO_OPEN_LOW_PASS_HZ = 22000.0
AUDIO_SYNTHESIS_TELEMETRY_HEADER_BYTES = 8
AUDIO_SYNTHESIS_TELEMETRY_ROW_BYTES = 56
AUDIO_SYNTHESIS_TELEMETRY_CAPACITY = 300
AUDIO_SYNTHESIS_AUDIO_PLAYER_CRITICAL_SYSTEM_ID = 261
VOCAL_BANK_SYNTHESIS_MAGIC = 0x44563848
VOCAL_BANK_SYNTHESIS_VERSION = 1
VOCAL_BANK_SYNTHESIS_HEADER_BYTES = 32
VOCAL_BANK_SYNTHESIS_ENTRY_BYTES = 64
VOCAL_BANK_SYNTHESIS_TELEMETRY_CAPACITY = 300
VOCAL_BANK_SYNTHESIS_DSP_DUMP_THRESHOLD_US = 1000.0
ADAPTIVE_STEM_MIXER_ENTRY_BYTES = 64
ADAPTIVE_STEM_MIXER_TELEMETRY_CAPACITY = 300
ADAPTIVE_STEM_MIXER_DUMP_THRESHOLD_US = 1000.0
CAMERA_JUICE_TELEMETRY_MAGIC = 0x354A4353
CAMERA_JUICE_TELEMETRY_VERSION = 4
CAMERA_JUICE_TELEMETRY_HEADER_BYTES = 32
CAMERA_JUICE_TELEMETRY_ENTRY_BYTES = 64
CAMERA_JUICE_TELEMETRY_CAPACITY = 300
CAMERA_JUICE_BURST_BUDGET_US = 100.0
MATERIAL_DECAY_MAGIC = 0x4D445350
MATERIAL_DECAY_HEADER_BYTES = 13
MATERIAL_DECAY_ROW_BYTES = 29
MATERIAL_DECAY_TELEMETRY_CAPACITY = 300
INTERACTIVE_WAKE_MAGIC = 0x57414B45
INTERACTIVE_WAKE_HEADER_BYTES = 12
INTERACTIVE_WAKE_ENTRY_BYTES = 64
INTERACTIVE_WAKE_BLACKBOX_CAPACITY = 300
INTERACTIVE_WAKE_MAX_SOURCE_SLOTS = 16
FLORA_SWAY_FIELD_MAGIC = 0x46535759
FLORA_SWAY_FIELD_HEADER_BYTES = 12
FLORA_SWAY_FIELD_ENTRY_BYTES = 64
FLORA_SWAY_FIELD_BLACKBOX_CAPACITY = 300
FLORA_SWAY_FIELD_MIN_RESOLUTION = 16
FLORA_SWAY_FIELD_MAX_RESOLUTION = 64
FLORA_SWAY_FIELD_MAX_NODE_COUNT = FLORA_SWAY_FIELD_MAX_RESOLUTION ** 3
FLORA_SWAY_FIELD_MIN_CELL_SIZE = 2.05
FLORA_SWAY_FIELD_MAX_CELL_SIZE = 4.6
FLORA_SWAY_FIELD_MIN_UPDATE_INTERVAL_SECONDS = 1.0 / 60.0
FLORA_SWAY_FIELD_MAX_UPDATE_INTERVAL_SECONDS = 0.2
FLORA_SWAY_FIELD_MAX_DISPLACEMENT_METERS = 1.35
FLORA_MEMORY_TELEMETRY_HEADER_BYTES = 8
FLORA_MEMORY_TELEMETRY_ENTRY_BYTES = 64
FLORA_MEMORY_TELEMETRY_CAPACITY = 300
FLORA_MEMORY_TELEMETRY_EVENT_RESOLVE = 0x46525652
FLORA_MEMORY_TELEMETRY_EVENT_WRITE_LOCK = 0x4652574C
FLORA_MEMORY_TELEMETRY_EVENT_NAN = 0x46524E41
FLORA_MEMORY_TELEMETRY_BUFFER_ID = 71669
FLORA_MEMORY_TELEMETRY_DUMP_FAILURE_THRESHOLD = 3
FLORA_AMBIENT_SWAY_MAGIC = 0x37363253
FLORA_AMBIENT_SWAY_VERSION = 1
FLORA_AMBIENT_SWAY_SOURCE_HASH = 0x53465759
FLORA_AMBIENT_SWAY_HEADER_BYTES = 24
FLORA_AMBIENT_SWAY_ENTRY_BYTES = 64
FLORA_AMBIENT_SWAY_TELEMETRY_CAPACITY = 300
VEGETATION_MEMORY_MAGIC = 0x313331365F564547
VEGETATION_MEMORY_VERSION = 1
VEGETATION_MEMORY_HEADER_BYTES = 24
VEGETATION_MEMORY_ENTRY_BYTES = 64
VEGETATION_MEMORY_TELEMETRY_CAPACITY = 300
VEGETATION_MEMORY_TELEMETRY_RING_BUFFER_ID = 74398
VEGETATION_MEMORY_TELEMETRY_CURSOR_BUFFER_ID = 74399
DEAR_LIE_ORGANICS_ENTRY_BYTES = 64
DEAR_LIE_ORGANICS_TELEMETRY_CAPACITY = 300
DEAR_LIE_MAX_DAMAGE_SIGNALS_PER_FRAME = 128
DEAR_LIE_MAX_RESULTS_PER_FRAME = DEAR_LIE_MAX_DAMAGE_SIGNALS_PER_FRAME * 2
DEAR_LIE_MAX_REGEN_RECORDS = 2048
CHEMICAL_INFLUENCE_MAGIC = 0x3833315F4D454843
CHEMICAL_INFLUENCE_VERSION = 1
CHEMICAL_INFLUENCE_HEADER_BYTES = 20
CHEMICAL_INFLUENCE_ENTRY_BYTES = 64
CHEMICAL_INFLUENCE_TELEMETRY_CAPACITY = 300
CHEMICAL_INFLUENCE_MAX_ACTIVE_EMITTERS = 160
CHEMICAL_INFLUENCE_MAX_MOCK_EMITTERS = 8
CHEMICAL_INFLUENCE_MAX_JACOBI_ITERATIONS = 6
FAUNA_GENETICS_TELEMETRY_CAPACITY = 300
FAUNA_GENETICS_TELEMETRY_BUDGET_US = 500.0
SARGASSUM_FOOD_CHAIN_MAGIC_LOW = 0x48454354
SARGASSUM_FOOD_CHAIN_MAGIC_HIGH = 0x4643484E
SARGASSUM_FOOD_CHAIN_HEADER_BYTES = 24
SARGASSUM_FOOD_CHAIN_ENTRY_BYTES = 64
SARGASSUM_FOOD_CHAIN_CAPACITY = 300
SARGASSUM_FOOD_CHAIN_MAX_LOD_TIER = 2
SARGASSUM_FOOD_CHAIN_MAX_PENDING_KILL_SIGNALS = 8
SARGASSUM_BOID_SENSORY_MAGIC_LOW = 0x424F4944
SARGASSUM_BOID_SENSORY_MAGIC_HIGH = 0x53454E53
SARGASSUM_BOID_SENSORY_HEADER_BYTES = 24
SARGASSUM_BOID_SENSORY_ENTRY_BYTES = 64
SARGASSUM_BOID_SENSORY_CAPACITY = 300
SARGASSUM_BOID_SENSORY_MAX_THREATS = 16
SARGASSUM_BOID_SENSORY_MIN_RADIUS_METERS = 0.1
SARGASSUM_BOID_SENSORY_MAX_RADIUS_METERS = 256.0
MARINE_SNOW_VFX_CONTEXT_HASH = 0x4D534E57
MARINE_SNOW_VFX_HEADER_BYTES = 16
MARINE_SNOW_VFX_ENTRY_BYTES = 64
MARINE_SNOW_VFX_TELEMETRY_CAPACITY = 300
MARINE_SNOW_VFX_DYNAMIC_WAKE_CAPACITY = 16
MARINE_SNOW_VFX_MIN_PARTICLE_CAPACITY = 64
MARINE_SNOW_VFX_MAX_PARTICLE_CAPACITY = 28672
MARINE_SNOW_VFX_GPU_DUMP_THRESHOLD_US = 1500
PROPWASH_GPU_LAYOUT_HASH = 0x53483237
PROPWASH_GPU_HEADER_BYTES = 16
PROPWASH_GPU_ENTRY_BYTES = 64
PROPWASH_GPU_TELEMETRY_CAPACITY = 300
PROPWASH_GPU_EVENT_RING_CAPACITY = 512
PROPWASH_GPU_MIN_PARTICLE_BUDGET = 64
PROPWASH_GPU_MAX_PARTICLE_BUDGET = 28672
PROPWASH_GPU_ESTIMATED_BUDGET_WARNING_US = 1000.0
CARVE_DEBRIS_MAGIC = 0x44584656
CARVE_DEBRIS_HEADER_BYTES = 20
CARVE_DEBRIS_ENTRY_BYTES = 64
CARVE_DEBRIS_BLACKBOX_CAPACITY = 300
CARVE_DEBRIS_MIN_ACTIVE_CAPACITY = 500
CARVE_DEBRIS_MAX_ACTIVE_CAPACITY = 10000
CARVE_DEBRIS_MAX_CARVE_SIGNALS_PER_FRAME = 32
BIOLUM_PULSE_MAGIC = 0x42505359
BIOLUM_PULSE_HEADER_BYTES = 16
BIOLUM_PULSE_ENTRY_BYTES = 64
BIOLUM_PULSE_BLACKBOX_CAPACITY = 300
BIOLUM_PULSE_MAX_GLOW_INSTANCES = 50000
BIOLUM_PULSE_SYNC_PULSE_CAPACITY = 16
BIOLUM_PULSE_MAX_HDR_INTENSITY = 10.0
BIOLUM_PULSE_OSCILLATOR_WARNING_MS = 0.1
BIOLUM_DIRECTOR_MAGIC = 0x42494F4C
BIOLUM_DIRECTOR_HEADER_BYTES = 13
BIOLUM_DIRECTOR_ENTRY_BYTES = 32
BIOLUM_DIRECTOR_TELEMETRY_CAPACITY = 300
BIOLUM_DIRECTOR_MAX_TOUCH_RIPPLES = 16
BIOLUM_DIRECTOR_MAX_PREDATOR_CONTACTS = 16
SURVIVAL_BLACKBOX_SOURCE_HASH = 0x53555256
SURVIVAL_BLACKBOX_SOURCE_BYTES = 64
SURVIVAL_BLACKBOX_DEATH_CAUSE_SHIFT = 24
SURVIVAL_BLACKBOX_DEATH_CAUSE_MASK = 0xFF000000
TOXIC_OUTGASSING_MAGIC = 0x38584F54
TOXIC_OUTGASSING_VERSION = 1
TOXIC_OUTGASSING_HEADER_BYTES = 32
TOXIC_OUTGASSING_ENTRY_BYTES = 64
TOXIC_OUTGASSING_TELEMETRY_CAPACITY = 300
GAS_DYNAMICS_MAGIC = 0x48384744
GAS_DYNAMICS_VERSION = 2
GAS_DYNAMICS_HEADER_BYTES = 24
GAS_DYNAMICS_ENTRY_BYTES = 64
GAS_DYNAMICS_TELEMETRY_CAPACITY = 300
GAS_DYNAMICS_FAILURE_FLAG = 1 << 15
BASE_ATMOSPHERE_LOGISTICS_MAGIC = 0x4847415332323144
BASE_ATMOSPHERE_LOGISTICS_VERSION = 1
BASE_ATMOSPHERE_LOGISTICS_HEADER_BYTES = 16
BASE_ATMOSPHERE_LOGISTICS_ENTRY_BYTES = 64
BASE_ATMOSPHERE_LOGISTICS_TELEMETRY_CAPACITY = 300
STORM_PROPAGATION_MAGIC = 0x53504450
STORM_PROPAGATION_SOURCE_HASH = 0x53483234
STORM_PROPAGATION_HEADER_BYTES = 32
STORM_PROPAGATION_ENTRY_BYTES = 64
STORM_PROPAGATION_TELEMETRY_CAPACITY = 300
OCEAN_SURFACE_ATMOSPHERE_MAGIC = 0x53555246
OCEAN_SURFACE_ATMOSPHERE_MARKER = 0x36325F57
OCEAN_SURFACE_ATMOSPHERE_HEADER_BYTES = 32
OCEAN_SURFACE_ATMOSPHERE_ENTRY_BYTES = 64
OCEAN_SURFACE_ATMOSPHERE_TELEMETRY_CAPACITY = 300
OCEAN_SURFACE_ATMOSPHERE_DUMP_BUDGET_NS = 500000
THERMODYNAMICS_HAZARD_MAGIC = 0x484543544F4E3800
THERMODYNAMICS_HAZARD_HEADER_BYTES = 20
THERMODYNAMICS_HAZARD_ENTRY_BYTES = 64
THERMODYNAMICS_HAZARD_TELEMETRY_CAPACITY = 300
ABYSSAL_THERMODYNAMICS_ENTRY_BYTES = 64
ABYSSAL_THERMODYNAMICS_TELEMETRY_CAPACITY = 300
REACTOR_THERMAL_ENTRY_BYTES = 128
NUCLEAR_REACTOR_THERMAL_ENTRY_BYTES = 128
REACTOR_THERMAL_TELEMETRY_CAPACITY = 300
FOVEATED_SIMULATION_MAGIC = 0x46384C44

FOVEATED_SIMULATION_FLAG_LABELS = (
    (1 << 0, "forceImmediateImportanceRefresh", "force-refresh"),
)

GLOBAL_TELEMETRY_SOURCE_LABELS = {
    SURVIVAL_BLACKBOX_SOURCE_HASH: "survival",
}

DATA_MONOLITH_LOAD_STATUS_LABELS = {
    0: "none",
    1: "loaded",
    2: "missing",
    3: "file-too-small",
    4: "file-too-large",
    5: "read-failed",
    6: "bad-magic",
    7: "unsupported-version",
    8: "bad-checksum",
    9: "header-mismatch",
    10: "invalid-section-table",
    11: "ready-locked",
}

DATA_MONOLITH_PATH_FLAG_LABELS = (
    (1 << 0, "managedFileFallback", "managed-file-fallback"),
    (1 << 1, "memoryMappedFile", "memory-mapped-file"),
    (1 << 2, "vaultBacked", "vault-backed"),
    (1 << 3, "streamingUriStaged", "streaming-uri-staged"),
    (1 << 4, "nativeFile", "native-file"),
    (1 << 5, "streamingUriRequiresAsync", "streaming-uri-requires-async"),
    (1 << 6, "streamingUriStagingCancelled", "streaming-uri-staging-cancelled"),
    (1 << 7, "androidAssetManager", "android-asset-manager"),
    (1 << 8, "androidJavaAssetManager", "android-java-asset-manager"),
)

DATA_MONOLITH_FAILURE_STAGE_LABELS = {
    0: "none",
    1: "load-status",
    2: "arena-capacity",
    3: "write-lock",
    4: "copy-to-arena",
    6: "write-lock-release",
    7: "telemetry-vault",
    8: "telemetry-bootstrap",
}

VAULT_SOVEREIGNTY_FLAG_LABELS = (
    (1 << 0, "fault", "fault"),
)

ARM64_ALIGNMENT_TELEMETRY_FLAG_LABELS = (
    (1 << 0, "pack1Detected", "pack1-detected"),
    (1 << 1, "misalignedEightByteField", "misaligned-8-byte-field"),
    (1 << 2, "invalidStride", "invalid-stride"),
    (1 << 3, "dynamicCastFault", "dynamic-cast-fault"),
    (1 << 4, "dumpWritten", "dump-written"),
)

HAPTIC_SYNTHESIS_FLAG_LABELS = (
    (1 << 0, "nanSanitized", "nan-sanitized"),
    (1 << 1, "budgetExceeded", "budget-exceeded"),
    (1 << 2, "pulseOverflow", "pulse-overflow"),
    (1 << 3, "missingPlayerAup", "missing-player-aup"),
    (1 << 4, "mockStormActive", "mock-storm-active"),
)

VOCAL_WARNING_ID_LABELS = {
    0: "none",
    1: "crush-depth",
    2: "hull-breach",
    3: "oxygen-low",
    4: "radiation",
    5: "power-low",
}

VOCAL_WARNING_ACTIVE_ALARM_LABELS = (
    (1 << 0, "crushDepth", "crush-depth"),
    (1 << 1, "hullBreach", "hull-breach"),
    (1 << 2, "oxygenLow", "oxygen-low"),
    (1 << 3, "radiation", "radiation"),
    (1 << 4, "powerLow", "power-low"),
)

VOCAL_WARNING_FAULT_LABELS = (
    (1 << 0, "telemetryInvalid", "telemetry-invalid"),
    (1 << 1, "priorityInvalid", "priority-invalid"),
    (1 << 2, "priorityInputInvalid", "priority-input-invalid"),
    (1 << 3, "vocalCueRejected", "vocal-cue-rejected"),
    (1 << 4, "subtitleRejected", "subtitle-rejected"),
    (1 << 5, "alarmMaskOverflow", "alarm-mask-overflow"),
    (1 << 6, "vocalWarningSignalRejected", "vocal-warning-signal-rejected"),
)

GRANULAR_AUDIO_FLAG_LABELS = (
    (1 << 0, "invalid", "invalid"),
    (1 << 1, "voiceLimitReached", "voice-limit-reached"),
    (1 << 2, "impactDriveActive", "impact-drive-active"),
)

PROLOGUE_AUDIO_STAGE_LABELS = {
    1: "space",
    2: "plasma",
    3: "whiteout",
    4: "ocean-handoff",
}

PROLOGUE_AUDIO_STATE_FLAG_LABELS = (
    (1 << 0, "splashdown", "splashdown"),
    (1 << 1, "portalActive", "portal-active"),
    (1 << 2, "granularEnabled", "granular-enabled"),
    (1 << 3, "lowTierProxy", "low-tier-proxy"),
    (1 << 4, "nonFiniteGuard", "nonfinite-guard"),
)

PROLOGUE_AUDIO_DSP_FLAG_LABELS = (
    (1 << 0, "invalid", "invalid"),
    (1 << 2, "portalActive", "portal-active"),
    (1 << 3, "granularEnabled", "granular-enabled"),
    (1 << 4, "splashdown", "splashdown"),
)

AUDIO_SYNTHESIS_FLAG_LABELS = (
    (1 << 0, "lockContention", "lock-contention"),
    (1 << 1, "staleOrMissingHandle", "stale-or-missing-handle"),
    (1 << 2, "nonFiniteSample", "nonfinite-sample"),
    (1 << 3, "outputUnderrun", "output-underrun"),
)

AUDIO_SYNTHESIS_FAILURE_LABELS = {
    0: "none",
    1: "vault-resolution",
    2: "telemetry-lock",
    3: "nonfinite-sample",
    4: "output-ring-full",
}

VOCAL_BANK_SYNTHESIS_FLAG_LABELS = (
    (1 << 0, "playing", "playing"),
    (1 << 1, "vorbisUnsupported", "vorbis-unsupported"),
    (1 << 2, "nonFinite", "nonfinite"),
    (1 << 3, "bankMiss", "bank-miss"),
    (1 << 4, "interrupted", "interrupted"),
)

VOCAL_BANK_SYNTHESIS_CODEC_LABELS = {
    0: "pcm16",
    1: "h8-adpcm",
    2: "vorbis",
}

ADAPTIVE_STEM_MIXER_FLAG_LABELS = (
    (1 << 0, "beatGateOpen", "beat-gate-open"),
    (1 << 1, "narrativeOverride", "narrative-override"),
    (1 << 2, "ioTransitionDelay", "io-transition-delay"),
    (1 << 3, "clipNotStreaming", "clip-not-streaming"),
    (1 << 4, "nonFinite", "nonfinite"),
)

CAMERA_JUICE_FLAG_LABELS = (
    (1 << 0, "xrSuppressed", "xr-suppressed"),
    (1 << 1, "nanSanitized", "nan-sanitized"),
    (1 << 2, "noPlayerAup", "no-player-aup"),
    (1 << 3, "vrSomaticWriteRejected", "vr-somatic-write-rejected"),
    (1 << 4, "vaultUnavailable", "vault-unavailable"),
    (1 << 5, "burstBudgetExceeded", "burst-budget-exceeded"),
)

MATERIAL_DECAY_FLAG_LABELS = (
    (1 << 0, "rustActive", "rust-active"),
    (1 << 1, "wet", "wet"),
    (1 << 2, "blood", "blood"),
)

MATERIAL_DECAY_DUMP_REASON_LABELS = {
    0: "none",
    1: "invalid-delta-time",
    2: "invalid-rust",
}

INTERACTIVE_WAKE_FLAG_LABELS = (
    (1 << 0, "invalidInput", "invalid-input"),
    (1 << 1, "nan", "nan"),
    (1 << 2, "budgetPressure", "budget-pressure"),
    (1 << 3, "thermalPressure", "thermal-pressure"),
)

FLORA_SWAY_FIELD_FLAG_LABELS = (
    (1 << 0, "invalidInput", "invalid-input"),
    (1 << 1, "nan", "nan"),
    (1 << 2, "vaultMissing", "vault-missing"),
    (1 << 3, "emptyWake", "empty-wake"),
    (1 << 4, "uploadStall", "upload-stall"),
    (1 << 5, "wrappedShift", "wrapped-shift"),
    (1 << 6, "fullReset", "full-reset"),
    (1 << 7, "discardedUpload", "discarded-upload"),
)

FLORA_MEMORY_TELEMETRY_FLAG_LABELS = (
    (1 << 0, "missingVault", "missing-vault"),
    (1 << 1, "invalidLength", "invalid-length"),
    (1 << 2, "compactionFence", "compaction-fence"),
    (1 << 3, "handleMismatch", "handle-mismatch"),
    (1 << 4, "resolveFailed", "resolve-failed"),
    (1 << 5, "invalidBuffer", "invalid-buffer"),
    (1 << 6, "writeLockFailed", "write-lock-failed"),
    (1 << 7, "nan", "nan"),
)

FLORA_MEMORY_TELEMETRY_EVENT_LABELS = {
    FLORA_MEMORY_TELEMETRY_EVENT_RESOLVE: "resolve",
    FLORA_MEMORY_TELEMETRY_EVENT_WRITE_LOCK: "write-lock",
    FLORA_MEMORY_TELEMETRY_EVENT_NAN: "nan",
}

FLORA_MEMORY_TELEMETRY_BUFFER_LABELS = {
    71650: "flora-sway-displacement-field",
    71651: "flora-sway-field-meta",
    71652: "flora-sway-field-blackbox",
    71653: "flora-stiffness-rules",
    71654: "flora-stiffness-csv-scratch",
    71655: "flora-ocean-flow-sample-positions",
    71656: "flora-ocean-flow-sample-results",
    71657: "flora-parasite-nodes",
    71658: "flora-cascade-reactive-template-mask",
    71659: "flora-defensive-spore-template-mask",
    71660: "flora-blood-kelp-template-mask",
    71661: "flora-ghost-weed-template-mask",
    71662: "flora-surface-cascade-phase-seeds",
    71663: "flora-underwater-cascade-phase-seeds",
    71664: "flora-surface-cascade-events",
    71665: "flora-underwater-cascade-events",
    71666: "flora-surface-reactive-handles",
    71667: "flora-underwater-reactive-handles",
    71668: "flora-reactive-query-handles",
    FLORA_MEMORY_TELEMETRY_BUFFER_ID: "flora-memory-telemetry",
}

FLORA_AMBIENT_SWAY_FLAG_LABELS = (
    (1 << 0, "vaultMissing", "vault-missing"),
    (1 << 1, "constantBufferUnsupported", "constant-buffer-unsupported"),
    (1 << 2, "invalidNumber", "invalid-number"),
    (1 << 3, "uploadSkipped", "upload-skipped"),
    (1 << 4, "burstKernelUnavailable", "burst-kernel-unavailable"),
)

VEGETATION_MEMORY_FLAG_LABELS = (
    (1 << 0, "coldBoot", "cold-boot"),
    (1 << 1, "defrag", "defrag"),
    (1 << 2, "lockContention", "lock-contention"),
    (1 << 3, "staleHandle", "stale-handle"),
    (1 << 4, "nan", "nan"),
    (1 << 5, "capacity", "capacity"),
    (1 << 6, "compactionFence", "compaction-fence"),
)

VEGETATION_MEMORY_FAILURE_CODE_LABELS = {
    0: "none",
    1: "cold-boot-registered",
    2: "defrag-scheduled",
    3: "defrag-completed",
    4: "vault-resolve-failed",
    5: "write-lock-contention",
    6: "nan-detected",
    7: "shutdown-released",
    8: "staging-capacity-exceeded",
    9: "compaction-fence-active",
}

VEGETATION_MEMORY_PHASE_LABELS = {
    0: "unknown",
    1: "cold-boot",
    2: "slow-tick",
    3: "visual-sync",
    4: "defrag",
    5: "shutdown",
}

VEGETATION_MEMORY_BUFFER_LABELS = {
    VEGETATION_MEMORY_TELEMETRY_RING_BUFFER_ID: "vegetation-memory-telemetry-ring",
    VEGETATION_MEMORY_TELEMETRY_CURSOR_BUFFER_ID: "vegetation-memory-telemetry-cursor",
}

DEAR_LIE_ORGANICS_FLAG_LABELS = (
    (1 << 2, "regenerationRecovered", "regeneration-recovered"),
    (1 << 5, "guardFailed", "guard-failed"),
    (1 << 6, "dropDrainFailed", "drop-drain-failed"),
    (1 << 7, "overflowOrReject", "overflow-or-reject"),
)

CHEMICAL_INFLUENCE_FLAG_LABELS = (
    (1 << 0, "nan", "nan"),
)

FAUNA_GENETICS_FLAG_LABELS = (
    (1 << 0, "invalidMask", "invalid-mask"),
)

SARGASSUM_FOOD_CHAIN_FLAG_LABELS = (
    (1 << 0, "tick", "tick"),
    (1 << 1, "killJobScheduled", "kill-job-scheduled"),
    (1 << 2, "killJobCompleted", "kill-job-completed"),
    (1 << 3, "killDrained", "kill-drained"),
    (1 << 4, "whaleFall", "whale-fall"),
    (1 << 5, "boidsScattered", "boids-scattered"),
    (1 << 31, "nonFinite", "nonfinite"),
)

SARGASSUM_BOID_SENSORY_FLAG_LABELS = (
    (1 << 0, "tick", "tick"),
    (1 << 1, "lightActive", "light-active"),
    (1 << 2, "pingActive", "ping-active"),
    (1 << 3, "capsule", "capsule"),
    (1 << 31, "nonFinite", "nonfinite"),
)

MARINE_SNOW_VFX_FLAG_LABELS = (
    (1 << 0, "nonFinite", "nonfinite"),
    (1 << 1, "gpuBudgetExceeded", "gpu-budget-exceeded"),
)

PROPWASH_GPU_FLAG_LABELS = (
    (1 << 0, "mockSource", "mock-source"),
    (1 << 1, "vehicleWakeSource", "vehicle-wake-source"),
    (1 << 2, "wakeSourceBridge", "wake-source-bridge"),
)

CARVE_DEBRIS_FLAG_LABELS = (
    (1 << 0, "invalidState", "invalid-state"),
    (1 << 2, "sdfActive", "sdf-active"),
    (1 << 3, "flowActive", "flow-active"),
    (1 << 4, "stressRecycle", "stress-recycle"),
    (1 << 5, "wakeActive", "wake-active"),
)

BIOLUM_PULSE_FLAG_LABELS = (
    (1 << 0, "nonFinite", "nonfinite"),
    (1 << 1, "jobOverrun", "job-overrun"),
    (1 << 2, "aupInvalid", "aup-invalid"),
)

BIOLUM_DIRECTOR_FLAG_LABELS = (
    (1 << 0, "daylightMasked", "daylight-masked"),
    (1 << 1, "predatorDim", "predator-dim"),
    (1 << 2, "eclipseMasked", "eclipse-masked"),
    (1 << 3, "cameraNonfinite", "camera-nonfinite"),
    (1 << 4, "zoneRegistryOverflow", "zone-registry-overflow"),
)

BIOLUM_DIRECTOR_REASON_LABELS = (
    (1 << 1, "nonfiniteIntensityPhase", "nonfinite-intensity-phase"),
    (1 << 3, "cameraNonfinite", "camera-nonfinite"),
)

SURVIVAL_BLACKBOX_FLAG_LABELS = (
    (1 << 0, "alive", "alive"),
    (1 << 1, "underwater", "underwater"),
    (1 << 2, "beyondSafeDepth", "beyond-safe-depth"),
    (1 << 3, "oxygenGrace", "oxygen-grace"),
    (1 << 4, "bends", "bends"),
    (1 << 5, "freshPhysiology", "fresh-physiology"),
    (1 << 6, "narcosis", "narcosis"),
    (1 << 7, "toxicity", "toxicity"),
    (1 << 8, "thermalStress", "thermal-stress"),
    (1 << 9, "hasStats", "has-stats"),
)

SURVIVAL_DEATH_CAUSE_LABELS = {
    0: "none",
    1: "oxygen-depletion",
    2: "pressure-collapse",
    3: "thermal-failure",
    4: "radiation-exposure",
    5: "starvation",
    6: "dehydration",
    7: "integrity-failure",
}

TOXIC_OUTGASSING_FLAG_LABELS = (
    (1 << 0, "mockChemistry", "mock-chemistry"),
    (1 << 5, "binaryProbeFailure", "binary-probe-failure"),
    (1 << 6, "dumpFailure", "dump-failure"),
    (1 << 7, "nanDetected", "nan"),
)

GAS_DYNAMICS_FLAG_LABELS = (
    (1 << 0, "nanDetected", "nan"),
    (1 << 1, "breach", "breach"),
    (1 << 2, "hibernating", "hibernating"),
)

GAS_DYNAMICS_FAILURE_LABELS = {
    3: "state-write-lock",
    4: "step-completion-deferred",
}

BASE_ATMOSPHERE_LOGISTICS_FAULT_LABELS = (
    (1 << 0, "layoutFault", "layout-fault"),
    (1 << 1, "emptyGraph", "empty-graph"),
    (1 << 2, "nonFiniteGas", "nonfinite-gas"),
    (1 << 3, "bufferAlias", "buffer-alias"),
    (1 << 4, "csrOverflow", "csr-overflow"),
    (1 << 5, "sourceOverflow", "source-overflow"),
    (1 << 6, "csvMalformed", "csv-malformed"),
    (1 << 7, "nanDetected", "nan"),
)

STORM_PROPAGATION_FLAG_LABELS = (
    (1 << 0, "nonFinite", "nonfinite"),
    (1 << 1, "mockWeather", "mock-weather"),
    (1 << 2, "fogPublished", "fog"),
    (1 << 3, "biolumPublished", "biolum"),
    (1 << 4, "audioPublished", "audio"),
    (1 << 5, "flowPublished", "flow"),
)

OCEAN_SURFACE_ATMOSPHERE_FLAG_LABELS = (
    (1 << 0, "readbackLatencyOrBudget", "latency-or-budget"),
)

THERMODYNAMICS_HAZARD_FLAG_LABELS = (
    (1 << 0, "nanDetected", "nan"),
    (1 << 2, "rebase", "rebase"),
    (1 << 4, "signalDrop", "signal-drop"),
)

ABYSSAL_THERMODYNAMICS_FLAG_LABELS = (
    (1 << 0, "nanDetected", "nan"),
    (1 << 1, "shift", "shift"),
    (1 << 2, "mockSources", "mock-sources"),
    (1 << 3, "energyDrift", "energy-drift"),
    (1 << 4, "divergent", "divergent"),
    (1 << 5, "maxIterations", "max-iterations"),
)

REACTOR_THERMAL_FLAG_LABELS = (
    (1 << 0, "nonFinite", "nonfinite"),
    (1 << 1, "outOfGrid", "out-of-grid"),
    (1 << 2, "meltdown", "meltdown"),
    (1 << 3, "mockLoad", "mock-load"),
    (1 << 4, "costOverBudget", "cost-over-budget"),
    (1 << 5, "signalOverflowRisk", "signal-overflow-risk"),
    (1 << 6, "timingProxy", "timing-proxy"),
    (1 << 7, "noCoolant", "no-coolant"),
    (1 << 8, "atomicAbort", "atomic-abort"),
)

INPUT_DETERMINISM_SCHEME_LABELS = {
    0x4B424D21: "keyboard-mouse",
    0x47504144: "gamepad",
    0x5354444B: "steam-deck",
    0x58525443: "xr-touch",
}

INPUT_DETERMINISM_FLAG_LABELS = (
    (1 << 0, "automationOverride", "automation"),
    (1 << 1, "delayApplied", "delay"),
    (1 << 2, "nonFiniteSanitized", "nonfinite-sanitized"),
)

ORIGIN_SHIFT_MAGIC = 0x504D445055413848
ORIGIN_SHIFT_VERSION = 3
ORIGIN_SHIFT_LITTLE_ENDIAN_TAG = 0x00454C48
ORIGIN_SHIFT_FLAG_BIG_ENDIAN = 1 << 0
ORIGIN_SHIFT_FLAG_HAS_DETAIL_ROWS = 1 << 1
BINARY_LAYOUT_SENTINEL_MAGIC = 0x4838424C
BINARY_LAYOUT_SENTINEL_VERSION = 1
BINARY_LAYOUT_SENTINEL_HEADER_BYTES = 28
BINARY_LAYOUT_SENTINEL_TYPE_NAME_MAX_BYTES = 160
TERMINAL_OS_MAGIC = 0x544F5338
TERMINAL_OS_VERSION = 1
TERMINAL_OS_HEADER_BYTES = 32
TERMINAL_OS_SOURCE_HASH = 0x544F5331
TERMINAL_DECRYPTION_MAGIC = 0x44484348
TERMINAL_DECRYPTION_VERSION = 3
TERMINAL_DECRYPTION_HEADER_BYTES = 24
TERMINAL_BLACKBOX_FRAME_COUNT = 300
TERMINAL_PROJECTION_MAGIC = 0x33334853
TERMINAL_PROJECTION_VERSION = 1
TERMINAL_PROJECTION_HEADER_BYTES = 64
TERMINAL_PROJECTION_ENTRY_BYTES = 64
TERMINAL_PROJECTION_INPUT_STATE_STRIDE_BYTES = 64
TERMINAL_PROJECTION_ROLLBACK_EXCLUDED = 1
OPENXR_MANUAL_OVERRIDE_ENTRY_BYTES = 41
OPENXR_MANUAL_OVERRIDE_FRAME_COUNT = 300
VEHICLE_DAMAGE_HOLOGRAPHER_MAGIC = 0x44484F4C
VEHICLE_DAMAGE_HOLOGRAPHER_ENTRY_BYTES = 24
VEHICLE_COCKPIT_TELEMETRY_CAPACITY = 300
PDA_PROJECTION_MAGIC = 0x50333438
PDA_PROJECTION_VERSION = 2
PDA_PROJECTION_HEADER_BYTES = 64
PDA_PROJECTION_ENTRY_BYTES = 64
PDA_PROJECTION_TELEMETRY_CAPACITY = 300
WRIST_HUD_MAGIC = 0x44554853
WRIST_HUD_VERSION = 1
WRIST_HUD_HEADER_BYTES = 32
WRIST_HUD_ENTRY_BYTES = 64
WRIST_HUD_TELEMETRY_CAPACITY = 300
LADDER_CLIMB_IK_MAGIC = 0x4C43494B
LADDER_CLIMB_IK_VERSION = 1
LADDER_CLIMB_IK_HEADER_BYTES = 24
LADDER_CLIMB_IK_ENTRY_BYTES = 128
LADDER_CLIMB_IK_FRAME_CAPACITY = 300
TOPOGRAPHICAL_SONAR_MAGIC = 0x534F4E52
TOPOGRAPHICAL_SONAR_VERSION = 1
TOPOGRAPHICAL_SONAR_HEADER_BYTES = 32
TOPOGRAPHICAL_SONAR_ENTRY_BYTES = 128
TOPOGRAPHICAL_SONAR_TELEMETRY_FRAMES = 300
KINETIC_CHARACTER_MAGIC = 0x4B424F4E
KINETIC_CHARACTER_VERSION = 1
KINETIC_CHARACTER_HEADER_BYTES = 24
KINETIC_CHARACTER_ENTRY_BYTES = 64
KINETIC_CHARACTER_TELEMETRY_CAPACITY = 300
PROCEDURAL_BONE_MAGIC = 0x50424F4E
PROCEDURAL_BONE_VERSION = 1
PROCEDURAL_BONE_HEADER_BYTES = 24
PROCEDURAL_BONE_ENTRY_BYTES = 64
PROCEDURAL_BONE_TELEMETRY_CAPACITY = 300
VR_SOMATIC_MAGIC = 0x5652534D
VR_SOMATIC_VERSION = 3
VR_SOMATIC_HEADER_BYTES = 16
VR_SOMATIC_ENTRY_BYTES = 128
VR_SOMATIC_FRAME_CAPACITY = 300
LOCKSTEP_STATE_VALIDATOR_MAGIC = 0x504D5544534C3848
LOCKSTEP_STATE_VALIDATOR_VERSION = 1
LOCKSTEP_STATE_VALIDATOR_HEADER_BYTES = 32
LOCKSTEP_STATE_VALIDATOR_ENTRY_BYTES = 64
LOCKSTEP_STATE_VALIDATOR_TELEMETRY_CAPACITY = 300
VOXEL_ASTAR_ENTRY_BYTES = 64
VOXEL_ASTAR_TELEMETRY_CAPACITY = 300
PATH_FUNNEL_ENTRY_BYTES = 64
PATH_FUNNEL_TELEMETRY_CAPACITY = 300
PDA_FREQUENCY_TUNING_HEADER_BYTES = 8
PDA_FREQUENCY_TUNING_ENTRY_BYTES = 32
PDA_FREQUENCY_TUNING_TELEMETRY_CAPACITY = 300
COMPASS_GYRO_MAGIC = 0x4759434F
COMPASS_GYRO_HEADER_BYTES = 12
COMPASS_GYRO_ENTRY_BYTES = 64
COMPASS_GYRO_BLACKBOX_CAPACITY = 300
PDA_ENCYCLOPEDIA_MAGIC = 0x50444145
PDA_ENCYCLOPEDIA_HEADER_BYTES = 32
PDA_ENCYCLOPEDIA_ENTRY_BYTES = 64
PDA_ENCYCLOPEDIA_TELEMETRY_CAPACITY = 300
HABITAT_FLOOD_MAGIC = 0x48464C44
HABITAT_FLOOD_VERSION = 3
HABITAT_FLOOD_HEADER_BYTES = 20
HABITAT_FLOOD_ENTRY_BYTES = 40
HABITAT_FLOOD_BLACKBOX_CAPACITY = 300
CONSTRUCTION_VALIDATION_ENTRY_BYTES = 64
CONSTRUCTION_VALIDATION_TELEMETRY_CAPACITY = 300
CONSTRUCTION_SOCKET_ENTRY_BYTES = 64
CONSTRUCTION_SOCKET_TELEMETRY_CAPACITY = 300
CONSTRUCTION_HOLOGRAPHY_ENTRY_BYTES = 64
CONSTRUCTION_HOLOGRAPHY_TELEMETRY_CAPACITY = 300
LASER_CUTTER_DOD_MAGIC = 0x53483235
LASER_CUTTER_DOD_VERSION = 1
LASER_CUTTER_DOD_HEADER_BYTES = 32
LASER_CUTTER_DOD_ENTRY_BYTES = 128
LASER_CUTTER_DOD_TELEMETRY_CAPACITY = 300
LASER_CUTTER_DOD_LAYOUT_MAGIC = 0x53484C43
WFC_LASER_CUT_MAGIC = 0x5746434C
WFC_LASER_CUT_VERSION = 1
WFC_LASER_CUT_HEADER_BYTES = 32
WFC_LASER_CUT_ENTRY_BYTES = 96
WFC_LASER_CUT_TELEMETRY_CAPACITY = 300
WFC_LASER_CUT_SOURCE_HASH = 0x544C5352
TOOL_KINEMATICS_MAGIC = 0x42424B54
TOOL_KINEMATICS_VERSION = 1
TOOL_KINEMATICS_HEADER_BYTES = 32
TOOL_KINEMATICS_ENTRY_BYTES = 64
TOOL_KINEMATICS_BLACKBOX_CAPACITY = 300
TOOL_KINEMATICS_MAX_TOOL_CAPACITY = 8
TOOL_KINEMATICS_MAX_DUMP_ENTRIES = TOOL_KINEMATICS_BLACKBOX_CAPACITY * TOOL_KINEMATICS_MAX_TOOL_CAPACITY
AUXILIARY_EQUIPMENT_ENTRY_BYTES = 64
AUXILIARY_EQUIPMENT_TELEMETRY_CAPACITY = 300
AUXILIARY_EQUIPMENT_FAULT_DUMP_THRESHOLD_MICROSECONDS = 500.0
UPGRADE_MATRIX_ENTRY_BYTES = 64
UPGRADE_MATRIX_TELEMETRY_CAPACITY = 300
UPGRADE_MATRIX_LAYOUT_MAGIC = 0x55323331
UPGRADE_MATRIX_FAULT_COST_THRESHOLD_MICROSECONDS = 100.0
METABOLISM_BLACKBOX_MAGIC = 0x4D45544153524745
METABOLISM_BLACKBOX_VERSION = 2
METABOLISM_BLACKBOX_HEADER_BYTES = 32
METABOLISM_TELEMETRY_ENTRY_BYTES = 64
METABOLISM_DETAIL_TELEMETRY_ENTRY_BYTES = 64
METABOLISM_TELEMETRY_CAPACITY = 300
METABOLISM_EXECUTION_BUDGET_MICROSECONDS = 200.0
PHYSIOLOGY_AUTOPSY_MAGIC = 0x5348494E4F425532
PHYSIOLOGY_AUTOPSY_VERSION = 3
PHYSIOLOGY_AUTOPSY_HEADER_BYTES = 48
PHYSIOLOGY_TELEMETRY_ENTRY_BYTES = 64
DECOMPRESSION_TELEMETRY_ENTRY_BYTES = 64
PHYSIOLOGY_TELEMETRY_CAPACITY = 300
PHYSIOLOGY_DECOMPRESSION_RING_BUFFER = 73343
PHYSIOLOGY_TELEMETRY_BUDGET_MICROSECONDS = 200.0
SENSORY_IMPAIRMENT_MAGIC = 0x533332324859504F
SENSORY_IMPAIRMENT_VERSION = 1
SENSORY_IMPAIRMENT_HEADER_BYTES = 32
SENSORY_IMPAIRMENT_ENTRY_BYTES = 64
SENSORY_IMPAIRMENT_TELEMETRY_CAPACITY = 300
SENSORY_IMPAIRMENT_SOURCE_HASH = 0x53333232
SUIT_INTEGRITY_MAGIC = 0x5333323350524553
SUIT_INTEGRITY_VERSION = 1
SUIT_INTEGRITY_HEADER_BYTES = 32
SUIT_INTEGRITY_ENTRY_BYTES = 64
SUIT_INTEGRITY_TELEMETRY_CAPACITY = 300
SUIT_INTEGRITY_SOURCE_HASH = 0x53333233
SUIT_INTEGRITY_TICK_BUDGET_MICROSECONDS = 100.0
RADIATION_MUTATION_MAGIC = 0x533332344D555441
RADIATION_MUTATION_VERSION = 1
RADIATION_MUTATION_HEADER_BYTES = 32
RADIATION_MUTATION_ENTRY_BYTES = 64
RADIATION_MUTATION_TELEMETRY_CAPACITY = 300
RADIATION_MUTATION_SOURCE_HASH = 0x53333234
RADIATION_MUTATION_DEFAULT_FATAL_DOSE_RAD = 850.0
RESPAWN_RECONCILIATION_MAGIC = 0x5253504E53524745
RESPAWN_RECONCILIATION_VERSION = 1
RESPAWN_RECONCILIATION_HEADER_BYTES = 24
RESPAWN_RECONCILIATION_ENTRY_BYTES = 64
RESPAWN_RECONCILIATION_TELEMETRY_CAPACITY = 300
RESPAWN_RECONCILIATION_DROPPED_ITEM_MASK = 0x00FF0000
RESPAWN_RECONCILIATION_DROPPED_ITEM_SHIFT = 16

ORIGIN_SHIFT_TELEMETRY_FLAG_LABELS = (
    (1 << 0, "nan", "nan"),
    (1 << 1, "watchdog", "watchdog"),
    (1 << 2, "timeSliced", "time-sliced"),
    (1 << 3, "frameSample", "frame-sample"),
    (1 << 4, "shiftCommit", "shift-commit"),
)

TERMINAL_OS_FAULT_FLAG_LABELS = (
    (1 << 0, "layoutMismatch", "layout-mismatch"),
    (1 << 1, "formatBudget", "format-budget"),
    (1 << 2, "nonFinite", "nonfinite"),
    (1 << 3, "vaultUnavailable", "vault-unavailable"),
    (1 << 4, "decryptionBudget", "decryption-budget"),
    (1 << 5, "decryptionNonFinite", "decryption-nonfinite"),
    (1 << 6, "decryptionDumpBackpressure", "decryption-dump-backpressure"),
)

TERMINAL_DECRYPTION_FLAG_LABELS = (
    (1 << 0, "active", "active"),
    (1 << 1, "solved", "solved"),
    (1 << 2, "initialized", "initialized"),
    (1 << 3, "nonFinite", "nonfinite"),
    (1 << 4, "interactionBlocked", "interaction-blocked"),
)

TERMINAL_DECRYPTION_HOLD_FRAME_MASK = 0xFFFF0000
TERMINAL_DECRYPTION_HOLD_FRAME_SHIFT = 16

TERMINAL_PROJECTION_FAULT_FLAG_LABELS = (
    *TERMINAL_OS_FAULT_FLAG_LABELS,
    (1 << 16, "projectionNonFinite", "projection-nonfinite"),
    (1 << 17, "projectionBudget", "projection-budget"),
    (1 << 18, "projectionLayout", "projection-layout"),
)

OPENXR_MANUAL_OVERRIDE_FLAG_LABELS = (
    (1 << 0, "grabbed", "grabbed"),
    (1 << 1, "latched", "latched"),
    (1 << 2, "ikPressure", "ik-pressure"),
    (1 << 3, "xrActive", "xr-active"),
    (1 << 4, "projectionSingular", "projection-singular"),
    (1 << 5, "blackBoxDumped", "dumped"),
)

VEHICLE_DAMAGE_HOLOGRAPHER_FLAG_LABELS = (
    (1 << 0, "resourcesReady", "resources-ready"),
    (1 << 1, "cheapVisual", "cheap-visual"),
    (1 << 2, "activeDent", "active-dent"),
    (1 << 3, "flicker", "flicker"),
    (1 << 4, "flood", "flood"),
    (1 << 5, "fallbackWarning", "fallback-warning"),
)

PDA_PROJECTION_FLAG_LABELS = (
    (1 << 0, "active", "active"),
    (1 << 1, "mockSource", "mock-source"),
    (1 << 2, "nonFinite", "nonfinite"),
    (1 << 3, "overBudget", "over-budget"),
    (1 << 4, "intrusion", "intrusion"),
    (1 << 5, "qualityOverride", "quality-override"),
    (1 << 6, "gpuUploadFault", "gpu-upload-fault"),
)

WRIST_HUD_FLAG_LABELS = (
    (1 << 0, "culled", "culled"),
    (1 << 1, "pdaOpen", "pda-open"),
    (1 << 3, "jobOverBudget", "job-over-budget"),
    (1 << 4, "nanDetected", "nan-detected"),
    (1 << 5, "csvLoaded", "csv-loaded"),
    (1 << 6, "legacyMissing", "legacy-missing"),
    (1 << 7, "gpuUploadFault", "gpu-upload-fault"),
)

LADDER_CLIMB_IK_FLAG_LABELS = (
    (1 << 0, "active", "active"),
    (1 << 1, "cameraSlideFake", "camera-slide"),
    (1 << 2, "vrGrip", "vr-grip"),
    (1 << 3, "slip", "slip"),
    (1 << 4, "invalidInput", "invalid-input"),
    (1 << 5, "leftLocked", "left-locked"),
    (1 << 6, "rightLocked", "right-locked"),
    (1 << 7, "unreachable", "unreachable"),
)

TOPOGRAPHICAL_SONAR_FLAG_LABELS = (
    (1 << 0, "usedPublishedSdf", "used-published-sdf"),
    (1 << 1, "sdfUnavailable", "sdf-unavailable"),
    (1 << 2, "gpuUpload", "gpu-upload"),
    (1 << 3, "pingEvent", "ping-event"),
    (1 << 4, "csvColor", "csv-color"),
    (1 << 31, "fault", "fault"),
)

KINETIC_CHARACTER_FLAG_LABELS = (
    (1 << 0, "visible", "visible"),
    (1 << 1, "mock", "mock"),
    (1 << 2, "sdfBrace", "sdf-brace"),
    (1 << 3, "playerKinematicsTargets", "player-kinematics-targets"),
    (1 << 4, "toolAligned", "tool-aligned"),
    (1 << 5, "damageFlinch", "damage-flinch"),
    (1 << 6, "qualityCollapsed", "quality-collapsed"),
    (1 << 31, "invalid", "invalid"),
)

PROCEDURAL_BONE_FLAG_LABELS = (
    (1 << 0, "visible", "visible"),
    (1 << 1, "qualityCollapse", "quality-collapse"),
    (1 << 2, "jawSolved", "jaw-solved"),
    (1 << 3, "mockSignal", "mock-signal"),
    (1 << 31, "invalid", "invalid"),
)

VR_SOMATIC_FLAG_LABELS = (
    (1 << 0, "active", "active"),
    (1 << 1, "nonFinite", "nonfinite"),
    (1 << 2, "leftGhost", "left-ghost"),
    (1 << 3, "rightGhost", "right-ghost"),
    (1 << 6, "nearCollision", "near-collision"),
    (1 << 7, "aupShiftSeen", "aup-shift-seen"),
    (1 << 9, "framePressure", "frame-pressure"),
    (1 << 10, "protectiveFallback", "protective-fallback"),
    (1 << 11, "accelerationTunnel", "acceleration-tunnel"),
    (1 << 12, "kccSignal", "kcc-signal"),
    (1 << 13, "kccAccelerationTunnel", "kcc-acceleration-tunnel"),
    (1 << 14, "dynamicHorizonLock", "dynamic-horizon-lock"),
)

LOCKSTEP_STATE_VALIDATOR_FLAG_LABELS = (
    (1 << 0, "hashExecuted", "hash-executed"),
    (1 << 1, "missingData", "missing-data"),
    (1 << 2, "truncated", "truncated"),
    (1 << 3, "nonFinite", "nonfinite"),
    (1 << 4, "replayMode", "replay-mode"),
    (1 << 6, "desync", "desync"),
    (1 << 8, "layoutInvalid", "layout-invalid"),
)

LOCKSTEP_STATE_VALIDATOR_CATEGORY_LABELS = (
    (1 << 0, "rigidbodyAups", "rigidbody-aups"),
    (1 << 1, "playerKinematicState", "player-kinematic-state"),
    (1 << 2, "roomWaterLevels", "room-water-levels"),
    (1 << 3, "entityAups", "entity-aups"),
)

VOXEL_ASTAR_FLAG_LABELS = (
    (1 << 0, "nonFiniteInput", "nonfinite-input"),
    (1 << 1, "startOutOfBounds", "start-out-of-bounds"),
    (1 << 2, "goalOutOfBounds", "goal-out-of-bounds"),
    (1 << 3, "startBlocked", "start-blocked"),
    (1 << 4, "goalBlocked", "goal-blocked"),
    (1 << 5, "openSetExhausted", "open-set-exhausted"),
    (1 << 6, "nodeBudgetYield", "node-budget-yield"),
    (1 << 7, "rawPathOverflow", "raw-path-overflow"),
    (1 << 8, "waypointOverflow", "waypoint-overflow"),
    (1 << 9, "sdfMissing", "sdf-missing"),
    (1 << 10, "nanDetected", "nan-detected"),
    (1 << 11, "timeSliceOverBudget", "time-slice-over-budget"),
    (1 << 12, "usedWeightedHeuristic", "weighted-heuristic"),
    (1 << 13, "partialNearestFallback", "partial-nearest-fallback"),
    (1 << 14, "mockSdfGenerated", "mock-sdf-generated"),
    (1 << 15, "csvProfileOverflow", "csv-profile-overflow"),
)

PATH_FUNNEL_TELEMETRY_FLAG_LABELS = (
    (1 << 0, "blackBoxDumpFailed", "blackbox-dump-failed"),
    (1 << 1, "wfcVaultSignalMismatch", "wfc-vault-signal-mismatch"),
)

PDA_FREQUENCY_TUNING_FLAG_LABELS = (
    (1 << 0, "stage0Locked", "stage-0-locked"),
    (1 << 1, "stage1Locked", "stage-1-locked"),
    (1 << 2, "stage2Locked", "stage-2-locked"),
)

COMPASS_GYRO_FLAG_LABELS = (
    (1 << 0, "initialized", "initialized"),
    (1 << 1, "powered", "powered"),
    (1 << 2, "anomalyUnstable", "anomaly-unstable"),
    (1 << 3, "stressSlowCadence", "stress-slow-cadence"),
    (1 << 4, "calibrationApplied", "calibration-applied"),
    (1 << 5, "nonFiniteFallback", "nonfinite-fallback"),
    (1 << 6, "reducedQualityNoise", "reduced-quality-noise"),
    (1 << 8, "hasPreviousAup", "has-previous-aup"),
    (1 << 9, "calibrationRequested", "calibration-requested"),
)

PDA_ENCYCLOPEDIA_STREAM_STATE_LABELS = {
    0: "idle",
    1: "loading",
    2: "streaming",
    3: "complete",
    4: "locked",
    5: "fault",
}

PDA_ENCYCLOPEDIA_SOURCE_LABELS = {
    0: "none",
    1: "h8lr",
    2: "babel",
    3: "vault-mock",
    4: "data-monolith",
}

PDA_ENCYCLOPEDIA_CANVAS_SPLIT_FLAG = 1 << 16
PDA_ENCYCLOPEDIA_KNOWN_FLAG_MASK = 0xFF | (0x7 << 8) | PDA_ENCYCLOPEDIA_CANVAS_SPLIT_FLAG

HABITAT_FLOOD_FLAG_LABELS = (
    (1 << 0, "nonFinite", "nonfinite"),
    (1 << 1, "overflowClamped", "overflow-clamped"),
    (1 << 2, "traversalOverflow", "traversal-overflow"),
    (1 << 3, "topologyInvalid", "topology-invalid"),
    (1 << 4, "moduleStressInvalid", "module-stress-invalid"),
)

CONSTRUCTION_VALIDATION_FLAG_LABELS = (
    (1 << 0, "occupiedGridCell", "occupied-grid-cell"),
    (1 << 1, "terrainIntersection", "terrain-intersection"),
    (1 << 2, "portMismatch", "port-mismatch"),
    (1 << 3, "structuralWarning", "structural-warning"),
    (1 << 4, "nonFiniteInput", "nonfinite-input"),
    (1 << 5, "outsideBounds", "outside-bounds"),
    (1 << 6, "graphCapacity", "graph-capacity"),
    (1 << 7, "disconnectedWing", "disconnected-wing"),
)

CONSTRUCTION_SOCKET_FLAG_LABELS = (
    (1 << 0, "connected", "connected"),
    (1 << 1, "corridorRoom", "corridor-room"),
    (1 << 2, "hatch", "hatch"),
    (1 << 3, "collisionBlocked", "collision-blocked"),
    (1 << 4, "nonFinite", "nonfinite"),
    (1 << 5, "validSnap", "valid-snap"),
    (1 << 6, "pendingCommit", "pending-commit"),
    (1 << 7, "topologyDirty", "topology-dirty"),
    (1 << 8, "rollbackFence", "rollback-fence"),
    (1 << 9, "dearLieActive", "dear-lie-active"),
    (1 << 10, "capacityExceeded", "capacity-exceeded"),
)

CONSTRUCTION_HOLOGRAPHY_FLAG_LABELS = (
    (1 << 0, "active", "active"),
    (1 << 1, "valid", "valid"),
    (1 << 2, "gridSnapped", "grid-snapped"),
    (1 << 3, "sdfBlocked", "sdf-blocked"),
    (1 << 4, "boundsBlocked", "bounds-blocked"),
    (1 << 5, "nonFinite", "nonfinite"),
    (1 << 6, "socketSnap", "socket-snap"),
    (1 << 7, "presentationOnly", "presentation-only"),
    (1 << 8, "dearLieActive", "dear-lie-active"),
    (1 << 9, "rollbackExcluded", "rollback-excluded"),
)

LASER_CUTTER_DOD_RESULT_FLAG_LABELS = (
    (1 << 0, "hit", "hit"),
    (1 << 1, "nonFinite", "nonfinite"),
    (1 << 2, "shaderDentOnly", "shader-dent-only"),
    (1 << 3, "gpuSparkOnly", "gpu-spark-only"),
    (1 << 4, "batteryDrainQueued", "battery-drain-queued"),
    (1 << 5, "decalQueued", "decal-queued"),
)

WFC_LASER_CUT_FLAG_LABELS = (
    (1 << 0, "completed", "completed"),
    (1 << 1, "alreadyUnlocked", "already-unlocked"),
    (1 << 2, "stressReduced", "stress-reduced"),
)

TOOL_KINEMATICS_FLAG_LABELS = (
    (1 << 0, "idle", "idle"),
    (1 << 1, "active", "active"),
    (1 << 2, "busy", "busy"),
    (1 << 3, "overheated", "overheated"),
    (1 << 4, "lowPower", "low-power"),
    (1 << 5, "targetLock", "target-lock"),
    (1 << 6, "cooling", "cooling"),
    (1 << 7, "fault", "fault"),
    (1 << 8, "rayHit", "ray-hit"),
    (1 << 9, "recoilActive", "recoil-active"),
    (1 << 10, "lowTierSnap", "low-tier-snap"),
    (1 << 11, "sdfPenetrating", "sdf-penetrating"),
    (1 << 12, "beamActive", "beam-active"),
    (1 << 13, "raymarchBudgetExceeded", "raymarch-budget-exceeded"),
    (1 << 14, "csvIoFault", "csv-io-fault"),
    (1 << 15, "lastChargeClutch", "last-charge-clutch"),
    (1 << 16, "powerDepletedSignalQueued", "power-depleted-signal-queued"),
    (1 << 17, "powerDepletedSignalSent", "power-depleted-signal-sent"),
)

TOOL_KINEMATICS_TOOL_HASH_LABELS = {
    0x4C435554: "laser-cutter",
    0x5343414E: "scanner",
    0x57454C44: "welder",
    0x52565654: "rivet-gun",
}

AUXILIARY_EQUIPMENT_FLAG_LABELS = (
    (1 << 0, "active", "active"),
    (1 << 1, "flare", "flare"),
    (1 << 2, "sensorPing", "sensor-ping"),
    (1 << 3, "gravityTether", "gravity-tether"),
    (1 << 4, "mock", "mock"),
    (1 << 5, "routedThisFrame", "routed-this-frame"),
    (1 << 29, "nonFiniteRecovered", "nonfinite-recovered"),
    (1 << 30, "unknownPrefab", "unknown-prefab"),
    (1 << 31, "faulted", "faulted"),
)

UPGRADE_MATRIX_FAULT_LABELS = (
    (1 << 0, "burstOverBudget", "burst-over-budget"),
    (1 << 1, "lutUnavailable", "lut-unavailable"),
    (1 << 2, "thermalGridUnavailable", "thermal-grid-unavailable"),
    (1 << 3, "lutIndexClamped", "lut-index-clamped"),
)

METABOLISM_FLAG_LABELS = (
    (1 << 0, "starving", "starving"),
    (1 << 1, "dehydrated", "dehydrated"),
    (1 << 2, "hypothermia", "hypothermia"),
    (1 << 3, "toxic", "toxic"),
    (1 << 4, "invalidMath", "invalid-math"),
    (1 << 5, "mockEntity", "mock-entity"),
    (1 << 6, "thermalSampled", "thermal-sampled"),
    (1 << 7, "csvProfile", "csv-profile"),
    (1 << 8, "chemicalSampled", "chemical-sampled"),
    (1 << 9, "fatigue", "fatigue"),
    (1 << 10, "hypoxia", "hypoxia"),
    (1 << 30, "executionBudgetExceeded", "execution-budget-exceeded"),
    (1 << 31, "nanDetected", "nan-detected"),
)

PHYSIOLOGY_FLAG_LABELS = (
    (1 << 0, "bends", "bends"),
    (1 << 1, "narcosis", "narcosis"),
    (1 << 2, "hypothermia", "hypothermia"),
    (1 << 3, "oxygenCritical", "oxygen-critical"),
    (1 << 4, "fatalOxygen", "fatal-oxygen"),
    (1 << 5, "invalidMath", "invalid-math"),
    (1 << 6, "emergencyMockCoefficients", "emergency-mock-coefficients"),
    (1 << 7, "csvOverride", "csv-override"),
    (1 << 8, "adrenalineSeen", "adrenaline-seen"),
    (1 << 9, "adrenalineCrash", "adrenaline-crash"),
    (1 << 10, "hyperbaricOverride", "hyperbaric-override"),
    (1 << 11, "fatalBends", "fatal-bends"),
    (1 << 12, "hypoxia", "hypoxia"),
    (1 << 13, "hyperoxia", "hyperoxia"),
    (1 << 14, "carbonDioxideToxicity", "co2-toxicity"),
    (1 << 15, "cnsOxygenToxicity", "cns-o2-toxicity"),
    (1 << 16, "fatalGasToxicity", "fatal-gas-toxicity"),
    (1 << 17, "breathingGasHeliox", "heliox"),
)

PHYSIOLOGY_STATUS_EFFECT_LABELS = (
    (1 << 0, "bends", "bends"),
    (1 << 1, "narcosis", "narcosis"),
    (1 << 2, "hypothermia", "hypothermia"),
    (1 << 3, "oxygenCritical", "oxygen-critical"),
    (1 << 4, "fatalOxygen", "fatal-oxygen"),
    (1 << 5, "invalidMath", "invalid-math"),
    (1 << 6, "hyperbaricOverride", "hyperbaric-override"),
    (1 << 7, "fatalBends", "fatal-bends"),
    (1 << 8, "hypoxia", "hypoxia"),
    (1 << 9, "hyperoxia", "hyperoxia"),
    (1 << 10, "carbonDioxideToxicity", "co2-toxicity"),
    (1 << 11, "cnsOxygenToxicity", "cns-o2-toxicity"),
    (1 << 12, "fatalGasToxicity", "fatal-gas-toxicity"),
    (1 << 16, "bleeding", "bleeding"),
    (1 << 17, "poison", "poison"),
    (1 << 18, "stun", "stun"),
    (1 << 19, "radiation", "radiation"),
)

DECOMPRESSION_BUBBLE_FLAG_LABELS = (
    (1 << 0, "fastTissueOverMValue", "fast-tissue-over-m-value"),
    (1 << 1, "mediumTissueOverMValue", "medium-tissue-over-m-value"),
    (1 << 2, "slowTissueOverMValue", "slow-tissue-over-m-value"),
)

SENSORY_IMPAIRMENT_FLAG_LABELS = (
    (1 << 0, "hypoxiaActive", "hypoxia-active"),
    (1 << 1, "narcosisActive", "narcosis-active"),
    (1 << 2, "latencyActive", "latency-active"),
    (1 << 3, "complexNoiseAdmitted", "complex-noise-admitted"),
    (1 << 4, "mockToxicity", "mock-toxicity"),
    (1 << 5, "nonFiniteSanitized", "nonfinite-sanitized"),
    (1 << 6, "overBudget", "over-budget"),
    (1 << 7, "csvProfile", "csv-profile"),
    (1 << 8, "inputCorrupted", "input-corrupted"),
)

SUIT_INTEGRITY_FLAG_LABELS = (
    (1 << 0, "initialized", "initialized"),
    (1 << 1, "warning", "warning"),
    (1 << 2, "buckling", "buckling"),
    (1 << 3, "imploded", "imploded"),
    (1 << 4, "nonFinitePressure", "nonfinite-pressure"),
    (1 << 5, "overBudget", "over-budget"),
    (1 << 6, "mockProfile", "mock-profile"),
    (1 << 7, "csvProfile", "csv-profile"),
    (1 << 8, "acousticGroan", "acoustic-groan"),
)

RADIATION_MUTATION_FLAG_LABELS = (
    (1 << 0, "active", "active"),
    (1 << 1, "critical", "critical"),
    (1 << 2, "healing", "healing"),
    (1 << 3, "mockDose", "mock-dose"),
    (1 << 4, "toxicBloodVfxRequested", "toxic-blood-vfx"),
    (1 << 5, "complexNoiseAdmitted", "complex-noise-admitted"),
    (1 << 6, "metabolicBridgeApplied", "metabolic-bridge-applied"),
    (1 << 7, "csvProfile", "csv-profile"),
    (1 << 30, "nonFiniteSanitized", "nonfinite-sanitized"),
    (1 << 31, "overBudget", "over-budget"),
)

RESPAWN_RECONCILIATION_FLAG_LABELS = (
    (1 << 0, "respawnActive", "respawn-active"),
    (1 << 1, "pendingRequest", "pending-request"),
    (1 << 2, "penaltyApplied", "penalty-applied"),
    (1 << 3, "mockMedicalBay", "mock-medical-bay"),
    (1 << 4, "fallbackLifepod", "fallback-lifepod"),
    (1 << 5, "invalidTargetAup", "invalid-target-aup"),
    (1 << 6, "committed", "committed"),
    (1 << 7, "manualTuning", "manual-tuning"),
    (1 << 8, "medicalBayActive", "medical-bay-active"),
    (1 << 9, "medicalBayPowered", "medical-bay-powered"),
    (1 << 10, "deathSequenceBlackoutPrimed", "death-sequence-blackout-primed"),
    (1 << 31, "nanDetected", "nan-detected"),
)

SIMULATION_BUCKET_FLAG_LABELS = (
    (1 << 0, "impossible60Fps", "impossible-60fps"),
    (1 << 1, "preSimulationOverBudget", "pre-sim-over-budget"),
    (1 << 2, "nonFiniteCost", "nonfinite-cost"),
    (1 << 3, "rebalancePending", "rebalance-pending"),
    (1 << 4, "survivalStaticDistribution", "survival-static"),
    (1 << 5, "homeostasisKillRequested", "homeostasis-kill"),
    (1 << 6, "visualOverkillBudgetAvailable", "visual-overkill-room"),
)

TERRAIN_STREAMING_DUMP_NAMES = {
    "DUMP1305STREAMINGBIN",
    "DUMP1305STREAMINGH8DUMP",
    "DUMP1305TERRAINCHUNKPAGERBIN",
    "DUMP1305TERRAINCHUNKPAGERH8DUMP",
    "DUMP1305WORLDCHUNKRESIDENCYBIN",
    "DUMP1305WORLDCHUNKRESIDENCYH8DUMP",
    "DUMP1305WORLDCHUNKRESIDENCYBACKPRESSUREBIN",
    "DUMP1305WORLDCHUNKRESIDENCYBACKPRESSUREH8DUMP",
    "DUMP1305WORLDCHUNKRESIDENCYHLODBIN",
    "DUMP1305WORLDCHUNKRESIDENCYHLODH8DUMP",
}

TERRAIN_STREAMING_PAGER_FAULT_LABELS = (
    (1 << 0, "missingFile", "missing-file"),
    (1 << 1, "io", "io"),
    (1 << 2, "queueOverflow", "queue-overflow"),
    (1 << 3, "lz4", "lz4"),
    (1 << 4, "layout", "layout"),
    (1 << 5, "nonFiniteAup", "nonfinite-aup"),
    (1 << 6, "vaultUnavailable", "vault"),
    (1 << 7, "invalidHeader", "invalid-header"),
    (1 << 8, "checksum", "checksum"),
    (1 << 9, "capacityOverflow", "capacity"),
)

WORLD_CHUNK_RESIDENCY_FLAG_LABELS = (
    (1 << 0, "invalidAup", "invalid-aup"),
    (1 << 1, "shift", "shift"),
    (1 << 2, "memoryBreach", "memory-breach"),
    (1 << 3, "teleport", "teleport"),
    (1 << 4, "predictiveSuspended", "predictive-suspended"),
    (1 << 5, "predictivePrewarmFault", "predictive-prewarm-fault"),
    (1 << 6, "activationOverflow", "activation-overflow"),
    (1 << 7, "duplicateChunk", "duplicate-chunk"),
    (1 << 8, "additiveSceneFault", "additive-scene-fault"),
    (1 << 9, "releaseAllReset", "release-all-reset"),
    (1 << 10, "addressablesFault", "addressables-fault"),
    (1 << 11, "activationFault", "activation-fault"),
    (1 << 12, "hydrationCopySpike", "hydration-copy-spike"),
)

WORLD_CHUNK_RESIDENCY_VERSION = 1
WORLD_CHUNK_RESIDENCY_LAYOUT_HASH = 0x44524357


app = FastAPI(title="HECTON-8 Telemetry Dashboard", version="1.0.0")


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds")


def dashboard_json(payload: dict[str, Any]) -> JSONResponse:
    return JSONResponse(payload, headers=NO_STORE_HEADERS)


def file_stamp(path: Path) -> dict[str, Any]:
    try:
        stat = path.stat()
    except FileNotFoundError:
        return {"exists": False, "path": str(path)}
    except OSError as exc:
        return {"exists": False, "path": str(path), "warnings": [f"stat_failed:{exc.__class__.__name__}"]}
    return {
        "exists": True,
        "path": str(path),
        "bytes": stat.st_size,
        "modifiedUtc": datetime.fromtimestamp(stat.st_mtime, timezone.utc).isoformat(timespec="seconds"),
    }


def normalize_name(value: str) -> str:
    return re.sub(r"[^a-z0-9]+", "", value.lower())


def parse_float(value: Any) -> float | None:
    if value is None:
        return None
    text = str(value).strip()
    if not text:
        return None
    try:
        parsed = float(text)
    except ValueError:
        return None
    return parsed if math.isfinite(parsed) else None


def parse_int(value: Any) -> int | None:
    parsed = parse_float(value)
    return None if parsed is None else int(parsed)


def pick_column(row: dict[str, str], aliases: tuple[str, ...]) -> tuple[str | None, str | None]:
    if not row:
        return None, None
    normalized = {normalize_name(key): key for key in row.keys()}
    for alias in aliases:
        key = normalized.get(normalize_name(alias))
        if key is not None:
            return key, row.get(key)
    return None, None


def convert_frame_time_ms(value: float | None, key: str | None) -> float | None:
    if value is None:
        return None
    normalized = normalize_name(key or "")
    if "fps" in normalized:
        return None if value <= 0.0 else 1000.0 / value
    if "delta" in normalized and "ms" not in normalized and value < 10.0:
        return value * 1000.0
    if normalized in {"dt", "deltatime", "frameseconds"} and value < 10.0:
        return value * 1000.0
    return value


def cap_entries(entries: list[dict[str, Any]]) -> list[dict[str, Any]]:
    if len(entries) <= MAX_DUMP_ENTRIES:
        return entries
    return entries[-MAX_DUMP_ENTRIES:]


def parse_csv_file(path: Path, source_label: str) -> dict[str, Any]:
    result: dict[str, Any] = {
        **file_stamp(path),
        "source": source_label,
        "rows": [],
        "frameSeries": [],
        "ecologySeries": [],
        "latestThermal": None,
        "latestHphi": None,
        "warnings": [],
    }
    if not path.exists():
        result["warnings"].append("missing")
        return result

    rows: deque[dict[str, str]] = deque(maxlen=MAX_CSV_ROWS)
    try:
        try:
            with path.open("r", encoding="utf-8-sig", newline="") as handle:
                rows.extend(csv.DictReader(handle))
        except UnicodeDecodeError:
            with path.open("r", encoding="cp1251", newline="") as handle:
                rows.extend(csv.DictReader(handle))
    except OSError as exc:
        result["warnings"].append(f"read_failed:{exc.__class__.__name__}")
        return result

    previous_frame_ms: float | None = None
    latest_thermal: dict[str, Any] | None = None
    latest_hphi: float | None = None
    for ordinal, row in enumerate(rows):
        _, frame_raw = pick_column(row, ("frame", "frameIndex", "FrameIndex", "Day"))
        frame = parse_int(frame_raw)
        if frame is None:
            frame = ordinal

        _, time_raw = pick_column(row, ("time", "timeSeconds", "elapsedSeconds", "distanceMeters", "Day"))
        x_value = parse_float(time_raw)
        if x_value is None:
            x_value = frame

        frame_key, frame_raw = pick_column(
            row,
            ("FrameTimeMs", "frame_time_ms", "FrameMs", "frameTime", "DeltaTimeMs", "deltaTime", "dt", "avgFps", "fps"),
        )
        frame_time_ms = convert_frame_time_ms(parse_float(frame_raw), frame_key)
        _, jitter_raw = pick_column(row, ("JitterMs", "FrameJitterMs", "Jitter", "frame_jitter_ms"))
        jitter_ms = parse_float(jitter_raw)
        if jitter_ms is None and frame_time_ms is not None and previous_frame_ms is not None:
            jitter_ms = abs(frame_time_ms - previous_frame_ms)
        if frame_time_ms is not None:
            previous_frame_ms = frame_time_ms
            result["frameSeries"].append(
                {
                    "x": x_value,
                    "frame": frame,
                    "frameTimeMs": round(frame_time_ms, 4),
                    "jitterMs": round(jitter_ms or 0.0, 4),
                    "spike": frame_time_ms > FRAME_SPIKE_MS,
                    "source": source_label,
                }
            )

        _, prey_raw = pick_column(row, ("PreyBiomass", "PreyBiomassSum", "prey_biomass", "PreyBiomass01"))
        _, predator_raw = pick_column(row, ("PredatorBiomass", "PredatorBiomassSum", "predator_biomass", "PredatorBiomass01"))
        prey = parse_float(prey_raw)
        predator = parse_float(predator_raw)
        if prey is not None or predator is not None:
            result["ecologySeries"].append(
                {"x": x_value, "frame": frame, "prey": round(prey or 0.0, 6), "predator": round(predator or 0.0, 6)}
            )

        _, thermal_raw = pick_column(row, ("HardwareThermalSeverity", "ThermalSeverity", "thermalSeverity", "severity"))
        _, battery_raw = pick_column(row, ("BatteryPercent", "batteryPercent", "Battery", "battery"))
        severity = parse_int(thermal_raw)
        battery = parse_int(battery_raw)
        if severity is not None or battery is not None:
            latest_thermal = {"severity": severity, "batteryPercent": battery, "source": source_label}

        _, hphi_raw = pick_column(row, ("H-Phi", "HPhi", "HectonPhi", "hphi", "staticHPhi"))
        hphi = parse_float(hphi_raw)
        if hphi is not None:
            latest_hphi = hphi

    result["loadedRowCount"] = len(rows)
    result["rows"] = list(rows)[-20:]
    result["latestThermal"] = latest_thermal
    result["latestHphi"] = latest_hphi
    return result


def parse_hphi_report(path: Path = HPHI_REPORT) -> dict[str, Any]:
    result = {"value": None, "status": "missing", "source": str(path), "evidenceClass": "STATIC_DOC"}
    if not path.exists():
        return result
    try:
        text = path.read_text(encoding="utf-8", errors="replace")
    except OSError as exc:
        result["status"] = f"read_failed:{exc.__class__.__name__}"
        return result
    for line in text.splitlines():
        if "h-phi" not in line.lower() and "hphi" not in line.lower():
            continue
        candidate = line.rsplit("=", 1)[-1] if "=" in line else line
        match = re.search(r"([0-9]*\.[0-9]+|[0-9]+)", candidate)
        if match is None:
            continue
        result["value"] = float(match.group(1))
        result["status"] = "static-report"
        break
    else:
        result["status"] = "not_found"
    return result


def parse_generic_blackbox(path: Path, data: bytes) -> dict[str, Any]:
    if is_crash_telemetry_path(path):
        crash_telemetry = try_parse_crash_telemetry(data)
        if crash_telemetry is not None:
            return crash_telemetry
        return {"type": "crash_telemetry_buffer", "entries": [], "latest": None, "warnings": ["invalid_header"]}

    if is_job_admission_blackbox_path(path):
        job_admission = try_parse_job_admission_blackbox(data)
        if job_admission is not None:
            return job_admission
        return {"type": "job_admission_blackbox", "entries": [], "latest": None, "warnings": ["invalid_header"]}

    if is_simulation_bucket_blackbox_path(path):
        simulation_bucket = try_parse_simulation_bucket_blackbox(data)
        if simulation_bucket is not None:
            return simulation_bucket
        return {"type": "simulation_bucket_blackbox", "entries": [], "latest": None, "warnings": ["invalid_header"]}

    if len(data) < GENERIC_BLACKBOX_HEADER.size:
        return {"type": "generic_blackbox", "entries": [], "warnings": ["truncated_header"]}
    magic, entry_count, struct_size = GENERIC_BLACKBOX_HEADER.unpack_from(data, 0)
    if magic != HECTON8_MAGIC or struct_size != GENERIC_BLACKBOX_ENTRY.size:
        return {"type": "generic_blackbox", "entries": [], "warnings": ["invalid_header"]}
    readable = min(entry_count, (len(data) - GENERIC_BLACKBOX_HEADER.size) // struct_size)
    entries = []
    offset = GENERIC_BLACKBOX_HEADER.size
    for _ in range(readable):
        fields = GENERIC_BLACKBOX_ENTRY.unpack_from(data, offset)
        offset += struct_size
        entries.append(
            {
                "frame": fields[0],
                "systemMask": fields[1],
                "deltaTimeMs": round(fields[2] * 1000.0, 4),
                "latencyMs": round(fields[3], 4),
                "gpuFrameTimeMs": round(fields[4], 4),
                "memoryUsedMb": round(fields[5], 4),
                "player": {"x": fields[6], "y": fields[7], "z": fields[8]},
                "activeChunkCount": fields[9],
                "errorFlags": fields[10],
                "exportReason": fields[11],
                "aupShiftSequence": fields[12],
                "payload0": fields[13],
                "payload1": fields[14],
                "lastOriginShiftFrame": fields[15],
            }
        )
    latest = entries[-1] if entries else None
    capped = cap_entries(entries)
    return {
        "type": "generic_blackbox",
        "entrySize": struct_size,
        "declaredEntryCount": entry_count,
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": ["payload_truncated"] if readable < entry_count else [],
    }


def is_crash_telemetry_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPCRASHTELEMETRYBUFFERBIN", "BLACKBOXCRASHBIN", "BLACKBOXCRASHH8DUMP"}


def try_parse_crash_telemetry(data: bytes) -> dict[str, Any] | None:
    if len(data) < CRASH_TELEMETRY_HEADER.size:
        return None

    magic, entry_count, entry_size = CRASH_TELEMETRY_HEADER.unpack_from(data, 0)
    if magic != HECTON8_MAGIC or entry_count < 0 or entry_size != CRASH_TELEMETRY_ENTRY.size:
        return None

    payload_bytes = len(data) - CRASH_TELEMETRY_HEADER.size
    if payload_bytes < 0:
        return None

    readable = min(entry_count, payload_bytes // entry_size)
    entries = []
    offset = CRASH_TELEMETRY_HEADER.size
    for _ in range(readable):
        fields = CRASH_TELEMETRY_ENTRY.unpack_from(data, offset)
        offset += entry_size
        entries.append(
            {
                "frame": fields[0],
                "systemMask": fields[1],
                "deltaTimeMs": round(fields[2] * 1000.0, 4),
                "latencyMs": round(fields[3], 4),
                "gpuFrameTimeMs": round(fields[4], 4),
                "memoryUsedMb": round(fields[5], 4),
                "player": {"x": fields[6], "y": fields[7], "z": fields[8]},
                "activeChunkCount": fields[9],
                "errorFlags": fields[10],
                "exportReason": fields[11],
                "aupShiftSequence": fields[12],
                "payload0": fields[13],
                "payload1": fields[14],
                "lastOriginShiftFrame": fields[15],
                "spike": fields[2] * 1000.0 > FRAME_SPIKE_MS,
            }
        )

    latest = entries[-1] if entries else None
    capped = cap_entries(entries)
    return {
        "type": "crash_telemetry_buffer",
        "entrySize": entry_size,
        "declaredEntryCount": entry_count,
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": ["payload_truncated"] if readable < entry_count else [],
    }


def is_job_admission_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return "JOBADMISSION" in normalized


def is_simulation_bucket_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPSIMULATIONBUCKETDISTRIBUTORBIN", "DUMPSIMULATIONBUCKETDISTRIBUTORH8DUMP"}


def try_parse_job_admission_blackbox(data: bytes) -> dict[str, Any] | None:
    if len(data) < JOB_ADMISSION_BLACKBOX_HEADER.size:
        return None

    magic, version, entry_count, entry_size, cursor, frame_sequence, reserved = JOB_ADMISSION_BLACKBOX_HEADER.unpack_from(data, 0)
    if (
        magic != HECTON8_MAGIC
        or version < 1
        or version > 2
        or entry_count < 0
        or entry_size != 64
        or cursor < 0
        or (entry_count == 0 and cursor != 0)
        or (entry_count > 0 and cursor >= entry_count)
        or reserved != 0
    ):
        return None

    payload_bytes = len(data) - JOB_ADMISSION_BLACKBOX_HEADER.size
    if payload_bytes < 0 or payload_bytes % entry_size != 0:
        return None

    if entry_count > 0 and len(data) < JOB_ADMISSION_BLACKBOX_HEADER.size + entry_size:
        return None

    readable = min(max(entry_count, 0), payload_bytes // entry_size)
    entries = []
    offset = JOB_ADMISSION_BLACKBOX_HEADER.size
    for _ in range(readable):
        fields = JOB_ADMISSION_BLACKBOX_ENTRY_PREFIX.unpack_from(data, offset)
        offset += entry_size
        if not any(fields):
            continue

        flags = fields[7]
        state_hash = fields[9]
        computed_state_hash = compute_job_admission_state_hash(
            fields[0],
            fields[1],
            fields[2],
            fields[3],
            fields[4],
            fields[5],
            flags,
        )
        entry = {
            "frame": fields[0],
            "jobHash": fields[1],
            "estimatedCostMs": round(fields[2], 4),
            "remainingBudgetMs": round(fields[3], 4),
            "criticalDebtFrames": fields[4],
            "killSwitchMask": fields[5],
            "lane": fields[6],
            "flags": flags,
            "stateHash": state_hash,
            "computedStateHash": computed_state_hash,
            "stateHashOk": state_hash == computed_state_hash,
        }
        if version >= 2:
            entry.update(
                {
                    "admitted": bool(flags & 0x01),
                    "denied": bool(flags & 0x02),
                    "aupBarrier": bool(flags & 0x04),
                    "killSwitch": bool(flags & 0x08),
                    "insufficientBudget": bool(flags & 0x10),
                    "nonFinite": bool(flags & 0x20),
                }
            )
        else:
            entry.update(
                {
                    "legacyStarved": bool(flags & 0x01),
                    "legacyNonFinite": bool(flags & 0x02),
                }
            )
        entries.append(entry)

    latest = entries[-1] if entries else None
    capped = cap_entries(entries)
    warnings = []
    if readable < entry_count:
        warnings.append("payload_truncated")
    if any(not entry.get("stateHashOk", True) for entry in entries):
        warnings.append("state_hash_mismatch")
    return {
        "type": "job_admission_blackbox",
        "version": version,
        "entrySize": entry_size,
        "declaredEntryCount": entry_count,
        "cursor": cursor,
        "frameSequence": frame_sequence,
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def float32_bits_or_zero(value: float) -> int:
    if not math.isfinite(value):
        return 0
    return struct.unpack("<I", struct.pack("<f", value))[0]


def fnv1a_mix_u32(hash_value: int, value: int) -> int:
    return ((hash_value ^ (value & 0xFFFFFFFF)) * 16777619) & 0xFFFFFFFF


def fnv1a_mix_bytes(hash_value: int, payload: bytes) -> int:
    for value in payload:
        hash_value = ((hash_value ^ value) * 16777619) & 0xFFFFFFFF
    return hash_value


def compute_job_admission_state_hash(
    frame_sequence: int,
    job_hash: int,
    estimated_cost_ms: float,
    remaining_budget_ms: float,
    critical_debt_frames: int,
    kill_switch_mask: int,
    flags: int,
) -> int:
    hash_value = 2166136261
    hash_value = fnv1a_mix_u32(hash_value, frame_sequence)
    hash_value = fnv1a_mix_u32(hash_value, job_hash)
    hash_value = fnv1a_mix_u32(hash_value, float32_bits_or_zero(estimated_cost_ms))
    hash_value = fnv1a_mix_u32(hash_value, float32_bits_or_zero(remaining_budget_ms))
    hash_value = fnv1a_mix_u32(hash_value, critical_debt_frames)
    hash_value = fnv1a_mix_u32(hash_value, kill_switch_mask)
    hash_value = fnv1a_mix_u32(hash_value, flags)
    return hash_value


def try_parse_simulation_bucket_blackbox(data: bytes) -> dict[str, Any] | None:
    if len(data) < SIMULATION_BUCKET_BLACKBOX_HEADER.size:
        return None

    magic, version, entry_count, entry_size, cursor, frame, rebalance_sequence = (
        SIMULATION_BUCKET_BLACKBOX_HEADER.unpack_from(data, 0)
    )
    if (
        magic != HECTON8_MAGIC
        or version != 1
        or entry_count < 0
        or entry_size != SIMULATION_BUCKET_BLACKBOX_ENTRY.size
        or cursor < 0
        or (entry_count == 0 and cursor != 0)
        or (entry_count > 0 and cursor >= entry_count)
    ):
        return None

    payload_bytes = len(data) - SIMULATION_BUCKET_BLACKBOX_HEADER.size
    if payload_bytes < 0 or payload_bytes % entry_size != 0:
        return None

    readable = min(max(entry_count, 0), payload_bytes // entry_size)
    entries = []
    offset = SIMULATION_BUCKET_BLACKBOX_HEADER.size
    for _ in range(readable):
        fields = SIMULATION_BUCKET_BLACKBOX_ENTRY.unpack_from(data, offset)
        offset += entry_size
        if not any(fields):
            continue

        flags = fields[6]
        labels, unknown_flags = resolve_simulation_bucket_flag_labels(flags)
        entry = {
            "frame": fields[0],
            "activeFastBucket": fields[1],
            "activeSlowBucket": fields[2],
            "activeColdBucket": fields[3],
            "slowBucketCount": fields[4],
            "criticalDebtFrames": fields[5],
            "framePacingFlags": flags,
            "framePacingFlagLabels": labels,
            "unknownFramePacingFlags": unknown_flags,
            "rebalanceSequence": fields[7],
            "activeBucketLoadMs": round(fields[8], 4),
            "jitterVarianceMs": round(fields[9], 4),
            "expectedMaxBucketLoadMs": round(fields[10], 4),
            "expectedMeanBucketLoadMs": round(fields[11], 4),
            "preSimulationCostMs": round(fields[12], 4),
            "interpolationAlpha": round(fields[13], 4),
            "activeSlowBucketCount": fields[14],
            "aupBarrierActive": bool(fields[15]),
            "reservedPadding": fields[16],
            "stateHash": fields[17],
        }
        for bit, key, _ in SIMULATION_BUCKET_FLAG_LABELS:
            entry[key] = bool(flags & bit)
        entries.append(entry)

    latest = entries[-1] if entries else None
    capped = cap_entries(entries)
    warnings = []
    if readable < entry_count:
        warnings.append("payload_truncated")
    if any(entry.get("reservedPadding") for entry in entries):
        warnings.append("reserved_padding_nonzero")
    if any(entry.get("unknownFramePacingFlags") for entry in entries):
        warnings.append("unknown_frame_pacing_flags")
    return {
        "type": "simulation_bucket_blackbox",
        "version": version,
        "entrySize": entry_size,
        "declaredEntryCount": entry_count,
        "cursor": cursor,
        "frame": frame,
        "rebalanceSequence": rebalance_sequence,
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def resolve_simulation_bucket_flag_labels(flags: int) -> tuple[list[str], int]:
    labels = [label for bit, _, label in SIMULATION_BUCKET_FLAG_LABELS if flags & bit]
    known = 0
    for bit, _, _ in SIMULATION_BUCKET_FLAG_LABELS:
        known |= bit
    unknown = flags & ~known
    if unknown:
        labels.append(f"unknown=0x{unknown:08X}")
    return labels, unknown


def is_terrain_streaming_dump_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in TERRAIN_STREAMING_DUMP_NAMES


def parse_terrain_streaming_dump(data: bytes) -> dict[str, Any]:
    if len(data) >= TERRAIN_STREAMING_HEADER.size:
        magic = struct.unpack_from("<Q", data, 0)[0]
        if magic == HECTON8_MAGIC:
            world_chunk_residency = try_parse_world_chunk_residency_header(data)
            if world_chunk_residency is not None:
                return world_chunk_residency
            parsed = try_parse_terrain_streaming_pager(data)
            if parsed is not None:
                return parsed
            return {
                "type": "terrain_streaming_pager",
                "entries": [],
                "latest": None,
                "warnings": ["invalid_header"],
            }

    if len(data) > 0 and len(data) % WORLD_CHUNK_RESIDENCY_ENTRY.size == 0:
        return parse_world_chunk_residency_dump(data)

    return {
        "type": "terrain_streaming",
        "entries": [],
        "latest": None,
        "warnings": ["invalid_header"],
    }


def try_parse_terrain_streaming_pager(data: bytes) -> dict[str, Any] | None:
    if len(data) < TERRAIN_STREAMING_HEADER.size:
        return None

    magic, version, entry_count, entry_size, fault_flags = TERRAIN_STREAMING_HEADER.unpack_from(data, 0)
    if (
        magic != HECTON8_MAGIC
        or version != 1305
        or entry_count == 0
        or entry_count > 100000
        or entry_size != TERRAIN_STREAMING_PAGER_ENTRY.size
        or TERRAIN_STREAMING_HEADER.size + entry_count * entry_size > len(data)
    ):
        return None

    entries = []
    offset = TERRAIN_STREAMING_HEADER.size
    for slot in range(entry_count):
        if is_empty_entry(data, offset, entry_size):
            offset += entry_size
            continue

        fields = TERRAIN_STREAMING_PAGER_ENTRY.unpack_from(data, offset)
        offset += entry_size
        flags = fields[12]
        labels, unknown_flags = resolve_bit_labels(flags, TERRAIN_STREAMING_PAGER_FAULT_LABELS)
        entry = {
            "slot": slot,
            "frame": fields[3],
            "stateHash": fields[4],
            "cameraAup": {"x": round(fields[0], 4), "y": round(fields[1], 4), "z": round(fields[2], 4)},
            "activeChunks": fields[5],
            "loadingChunks": fields[6],
            "staleChunks": fields[7],
            "pendingLoads": fields[8],
            "latencyEwmaMs": round(fields[9], 4),
            "residencyEvalMicros": fields[10],
            "effectiveRingRadius": round(fields[11], 4),
            "faultFlags": flags,
            "faultLabels": labels,
            "unknownFaultFlags": unknown_flags,
            "missingFileCount": fields[13],
            "workerSequence": fields[14],
        }
        for bit, key, _ in TERRAIN_STREAMING_PAGER_FAULT_LABELS:
            entry[key] = bool(flags & bit)
        entries.append(entry)

    latest = entries[-1] if entries else None
    capped = cap_entries(entries)
    warnings = []
    if any(entry.get("unknownFaultFlags") for entry in entries):
        warnings.append("unknown_fault_flags")
    if len(data) > TERRAIN_STREAMING_HEADER.size + entry_count * entry_size:
        warnings.append("trailing_bytes")
    return {
        "type": "terrain_streaming_pager",
        "version": version,
        "entrySize": entry_size,
        "declaredEntryCount": entry_count,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "faultFlags": fault_flags,
        "faultLabels": resolve_bit_labels(fault_flags, TERRAIN_STREAMING_PAGER_FAULT_LABELS)[0],
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def try_parse_world_chunk_residency_header(data: bytes) -> dict[str, Any] | None:
    if len(data) < WORLD_CHUNK_RESIDENCY_HEADER.size:
        return None

    magic, version, entry_count, entry_size, reason_flags, layout_hash, reserved = (
        WORLD_CHUNK_RESIDENCY_HEADER.unpack_from(data, 0)
    )
    if (
        magic != HECTON8_MAGIC
        or version != WORLD_CHUNK_RESIDENCY_VERSION
        or entry_count == 0
        or entry_count > 100000
        or entry_size != WORLD_CHUNK_RESIDENCY_ENTRY.size
        or layout_hash != WORLD_CHUNK_RESIDENCY_LAYOUT_HASH
        or reserved != 0
        or WORLD_CHUNK_RESIDENCY_HEADER.size + entry_count * entry_size > len(data)
    ):
        return None

    parsed = parse_world_chunk_residency_payload(data, WORLD_CHUNK_RESIDENCY_HEADER.size, entry_count)
    if len(data) > WORLD_CHUNK_RESIDENCY_HEADER.size + entry_count * entry_size:
        parsed["warnings"].append("trailing_bytes")
    labels, unknown_flags = resolve_bit_labels(reason_flags, WORLD_CHUNK_RESIDENCY_FLAG_LABELS)
    parsed.update(
        {
            "version": version,
            "reasonFlags": reason_flags,
            "reasonLabels": labels,
            "unknownReasonFlags": unknown_flags,
            "layoutHash": layout_hash,
        }
    )
    if unknown_flags:
        parsed["warnings"].append("unknown_reason_flags")
    return parsed


def parse_world_chunk_residency_dump(data: bytes) -> dict[str, Any]:
    entry_size = WORLD_CHUNK_RESIDENCY_ENTRY.size
    entry_count = len(data) // entry_size
    return parse_world_chunk_residency_payload(data, 0, entry_count)


def parse_world_chunk_residency_payload(data: bytes, payload_offset: int, entry_count: int) -> dict[str, Any]:
    entry_size = WORLD_CHUNK_RESIDENCY_ENTRY.size
    entries = []
    offset = payload_offset
    for slot in range(entry_count):
        if is_empty_entry(data, offset, entry_size):
            offset += entry_size
            continue

        fields = WORLD_CHUNK_RESIDENCY_ENTRY.unpack_from(data, offset)
        offset += entry_size
        packed_flags = fields[8]
        flags = packed_flags & 0x0000FFFF
        active_impostor_count = packed_flags >> 16
        labels, unknown_flags = resolve_bit_labels(flags, WORLD_CHUNK_RESIDENCY_FLAG_LABELS)
        entry = {
            "slot": slot,
            "focusChunkId": fields[0],
            "playerGrid": {"x": fields[1], "y": fields[2], "z": fields[3]},
            "playerLocal": {"x": round(fields[4], 4), "y": round(fields[5], 4), "z": round(fields[6], 4)},
            "frame": fields[7],
            "flags": flags,
            "flagLabels": labels,
            "unknownFlags": unknown_flags,
            "activeImpostorCount": active_impostor_count,
            "stateHash": fields[9],
            "pendingLoads": fields[10],
            "residentCount": fields[11],
            "loadingCount": fields[12],
            "evictingCount": fields[13],
        }
        for bit, key, _ in WORLD_CHUNK_RESIDENCY_FLAG_LABELS:
            entry[key] = bool(flags & bit)
        entries.append(entry)

    latest = entries[-1] if entries else None
    capped = cap_entries(entries)
    warnings = []
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    return {
        "type": "world_chunk_residency_blackbox",
        "entrySize": entry_size,
        "declaredEntryCount": entry_count,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_empty_entry(data: bytes, offset: int, size: int) -> bool:
    return not any(data[offset : offset + size])


def resolve_bit_labels(flags: int, definitions: tuple[tuple[int, str, str], ...]) -> tuple[list[str], int]:
    labels = [label for bit, _, label in definitions if flags & bit]
    known = 0
    for bit, _, _ in definitions:
        known |= bit
    unknown = flags & ~known
    if unknown:
        labels.append(f"unknown=0x{unknown:08X}")
    return labels if labels else ["none"], unknown


def resolve_bit_labels64(flags: int, definitions: tuple[tuple[int, str, str], ...]) -> tuple[list[str], int]:
    labels = [label for bit, _, label in definitions if flags & bit]
    known = 0
    for bit, _, _ in definitions:
        known |= bit
    unknown = flags & ~known
    if unknown:
        labels.append(f"unknown=0x{unknown:016X}")
    return labels if labels else ["none"], unknown


def is_global_telemetry_bus_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPGLOBALTELEMETRYBUSBIN", "DUMPGLOBALTELEMETRYBUSH8DUMP"}


def parse_global_telemetry_bus_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < GLOBAL_TELEMETRY_BUS_HEADER_BYTES:
        return {
            "type": "global_telemetry_bus_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    timestamp, total_frame_writes, fatal_hash = GLOBAL_TELEMETRY_PREFIX.unpack_from(data, 0)
    metadata_base = GLOBAL_TELEMETRY_BUS_METADATA_OFFSET
    metadata_capacity = (GLOBAL_TELEMETRY_BUS_HEADER_BYTES - metadata_base) // 4
    metadata = [
        struct.unpack_from("<I", data, metadata_base + i * 4)[0]
        for i in range(metadata_capacity)
    ]

    magic = metadata[0]
    version = metadata[1]
    header_bytes = metadata[2]
    valid_frames = metadata[3]
    frame_stride = metadata[4]
    payload_bytes = metadata[5]
    if (
        magic != GLOBAL_TELEMETRY_BUS_DUMP_MAGIC
        or version != GLOBAL_TELEMETRY_BUS_DUMP_VERSION
        or header_bytes != GLOBAL_TELEMETRY_BUS_HEADER_BYTES
        or valid_frames == 0
        or frame_stride < GLOBAL_TELEMETRY_PREFIX.size
        or frame_stride > GLOBAL_TELEMETRY_BUS_MAX_FRAME_STRIDE_BYTES
        or payload_bytes < frame_stride
    ):
        return {
            "type": "global_telemetry_bus_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    expected_payload_bytes = valid_frames * frame_stride
    readable_frames = min(valid_frames, max(0, (len(data) - header_bytes) // frame_stride))
    descriptors = parse_global_telemetry_source_descriptors(metadata)
    entries = []
    for index in range(readable_frames):
        frame_offset = header_bytes + index * frame_stride
        entries.append(parse_global_telemetry_frame(data, frame_offset, frame_stride, metadata, descriptors, index))

    latest = entries[-1] if entries else None
    capped = cap_entries(entries)
    warnings = []
    if readable_frames < valid_frames or len(data) < header_bytes + expected_payload_bytes:
        warnings.append("payload_truncated")
    if len(data) > header_bytes + expected_payload_bytes:
        warnings.append("trailing_bytes")
    if any(isinstance(entry.get("survival"), dict) and entry["survival"].get("warnings") for entry in entries):
        warnings.append("survival_source_warnings")
    return {
        "type": "global_telemetry_bus_blackbox",
        "version": version,
        "timestamp": timestamp,
        "totalFrameWrites": total_frame_writes,
        "fatalHash": fatal_hash,
        "headerBytes": header_bytes,
        "entrySize": frame_stride,
        "declaredEntryCount": valid_frames,
        "activeFrameCount": metadata[7],
        "payloadBytes": payload_bytes,
        "appVersionHash": metadata[6],
        "sourceCount": metadata[8],
        "eventWriteCursor": metadata[9],
        "hashHistoryOffsetBytes": metadata[10],
        "sourcePayloadOffsetBytes": metadata[11],
        "mockPhysicsOffsetBytes": metadata[12],
        "mockOriginOffsetBytes": metadata[13],
        "lastDeterminismHash": metadata[14] | (metadata[15] << 32),
        "sourceDescriptors": descriptors,
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def parse_global_telemetry_source_descriptors(metadata: list[int]) -> list[dict[str, Any]]:
    descriptor_index = metadata[19] if len(metadata) > 19 else GLOBAL_TELEMETRY_BUS_SOURCE_DESCRIPTOR_METADATA_INDEX
    descriptor_stride = metadata[20] if len(metadata) > 20 else GLOBAL_TELEMETRY_BUS_SOURCE_DESCRIPTOR_UINT_STRIDE
    source_capacity = min(
        metadata[21] if len(metadata) > 21 else GLOBAL_TELEMETRY_BUS_SOURCE_CAPACITY,
        GLOBAL_TELEMETRY_BUS_SOURCE_CAPACITY,
    )
    source_count = min(max(0, metadata[8] if len(metadata) > 8 else 0), source_capacity)
    descriptors = []
    if descriptor_stride <= 0:
        return descriptors

    for i in range(source_count):
        cursor = descriptor_index + i * descriptor_stride
        if cursor < 0 or cursor + 2 >= len(metadata):
            break

        source_hash = metadata[cursor]
        flags = metadata[cursor + 1]
        payload_bytes = metadata[cursor + 2]
        slot = metadata[cursor + 3] if cursor + 3 < len(metadata) else i
        if source_hash == 0 and flags == 0 and payload_bytes == 0:
            continue

        descriptors.append(
            {
                "sourceHash": source_hash,
                "sourceHashHex": f"0x{source_hash:08X}",
                "sourceName": GLOBAL_TELEMETRY_SOURCE_LABELS.get(source_hash, f"0x{source_hash:08X}"),
                "flags": flags,
                "payloadBytes": payload_bytes,
                "slot": slot,
                "floatScan": bool(flags & 0x01),
            }
        )
    return descriptors


def is_data_monolith_telemetry_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPH8STATICDATAARENATELEMETRYBIN", "DUMPH8STATICDATAARENATELEMETRYH8DUMP"}


def data_monolith_status_label(status: int) -> str:
    return DATA_MONOLITH_LOAD_STATUS_LABELS.get(status, f"unknown={status}")


def parse_data_monolith_telemetry_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < DATA_MONOLITH_TELEMETRY_HEADER_BYTES:
        return {
            "type": "data_monolith_telemetry_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, header_status, cursor, ring_capacity, entry_size = DATA_MONOLITH_TELEMETRY_HEADER.unpack_from(data, 0)
    if (
        magic != DATA_MONOLITH_TELEMETRY_MAGIC
        or ring_capacity <= 0
        or entry_size != DATA_MONOLITH_TELEMETRY_ENTRY_BYTES
    ):
        return {
            "type": "data_monolith_telemetry_blackbox",
            "magic": magic,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = DATA_MONOLITH_TELEMETRY_HEADER_BYTES
    expected_bytes = payload_offset + ring_capacity * entry_size
    readable_entries = min(ring_capacity, max(0, len(data) - payload_offset) // entry_size)
    entries = []
    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = DATA_MONOLITH_TELEMETRY_ENTRY.unpack_from(data, offset)
        load_status = fields[6]
        path_flags = fields[7]
        path_labels, unknown_path_flags = resolve_bit_labels(path_flags, DATA_MONOLITH_PATH_FLAG_LABELS)
        failure_stage = fields[9]
        entries.append(
            {
                "slot": index,
                "checksum64": fields[0],
                "checksum64Hex": f"0x{fields[0]:016X}",
                "loadTicks": fields[1],
                "ioTicks": fields[2],
                "frame": fields[3],
                "blobBytes": fields[4],
                "blobMiB": round(fields[4] / (1024 * 1024), 4),
                "sectionCount": fields[5],
                "loadStatus": load_status,
                "loadStatusLabel": data_monolith_status_label(load_status),
                "pathFlags": path_flags,
                "pathFlagLabels": path_labels,
                "unknownPathFlags": unknown_path_flags,
                "stateHash": fields[8],
                "stateHashHex": f"0x{fields[8]:08X}",
                "failureStage": failure_stage,
                "failureStageLabel": DATA_MONOLITH_FAILURE_STAGE_LABELS.get(
                    failure_stage,
                    f"unknown={failure_stage}",
                ),
                "failureDetail0": fields[10],
                "failureDetail1": fields[11],
                "failureDetail2": fields[12],
                "loaded": load_status == 1,
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if ring_capacity > DATA_MONOLITH_TELEMETRY_RING_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if cursor < 0 or cursor >= ring_capacity:
        warnings.append("telemetry_cursor_out_of_range")
    if header_status != 1:
        warnings.append("header_not_loaded")
    if header_status not in DATA_MONOLITH_LOAD_STATUS_LABELS:
        warnings.append("unknown_header_status")
    if any(entry.get("loadStatus") not in DATA_MONOLITH_LOAD_STATUS_LABELS for entry in entries):
        warnings.append("unknown_load_status")
    if any(entry.get("loadStatus") not in {0, 1} for entry in entries):
        warnings.append("load_failures")
    if any(entry.get("unknownPathFlags") for entry in entries):
        warnings.append("unknown_path_flags")
    if any(entry.get("failureStage") for entry in entries):
        warnings.append("failure_details")
    if any(entry.get("blobBytes", 0) > DATA_MONOLITH_MAX_BLOB_BYTES for entry in entries):
        warnings.append("blob_size_over_cap")
    if any(entry.get("loadTicks", 0) < 0 or entry.get("ioTicks", 0) < 0 for entry in entries):
        warnings.append("negative_ticks")
    return {
        "type": "data_monolith_telemetry_blackbox",
        "magic": magic,
        "headerBytes": DATA_MONOLITH_TELEMETRY_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": ring_capacity,
        "telemetryCursor": cursor,
        "headerStatus": header_status,
        "headerStatusLabel": data_monolith_status_label(header_status),
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_vault_sovereignty_telemetry_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPSHINOBU100BIN", "DUMPSHINOBU100H8DUMP"}


def build_vault_sovereignty_memory_map(latest: dict[str, Any] | None) -> list[dict[str, Any]]:
    if not latest:
        return []

    total = max(0, safe_int(latest.get("totalVaultBytes"), 0))
    arena = max(0, safe_int(latest.get("arenaBytes"), 0))
    if total <= 0 and arena <= 0:
        return []

    blocks = []
    if arena > 0:
        blocks.append({"state": "occupied", "bytes": arena, "label": "vault-arena", "estimated": True})
    remaining = max(0, total - arena)
    if remaining > 0:
        blocks.append({"state": "occupied", "bytes": remaining, "label": "non-arena-vault", "estimated": True})
    return blocks


def parse_vault_sovereignty_telemetry_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < VAULT_SOVEREIGNTY_TELEMETRY_HEADER_BYTES:
        return {
            "type": "vault_sovereignty_telemetry_blackbox",
            "entries": [],
            "latest": None,
            "memoryMap": [],
            "warnings": ["truncated_header"],
        }

    magic, version, entry_count, entry_size = VAULT_SOVEREIGNTY_TELEMETRY_HEADER.unpack_from(data, 0)
    if (
        magic != VAULT_SOVEREIGNTY_TELEMETRY_MAGIC
        or version != VAULT_SOVEREIGNTY_TELEMETRY_VERSION
        or entry_count <= 0
        or entry_size != VAULT_SOVEREIGNTY_TELEMETRY_ENTRY_BYTES
    ):
        return {
            "type": "vault_sovereignty_telemetry_blackbox",
            "magic": magic,
            "version": version,
            "entries": [],
            "latest": None,
            "memoryMap": [],
            "warnings": ["invalid_header"],
        }

    payload_offset = VAULT_SOVEREIGNTY_TELEMETRY_HEADER_BYTES
    expected_bytes = payload_offset + entry_count * entry_size
    readable_entries = min(entry_count, max(0, len(data) - payload_offset) // entry_size)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = VAULT_SOVEREIGNTY_TELEMETRY_ENTRY.unpack_from(data, offset)
        flags = fields[11]
        flag_labels, unknown_flags = resolve_bit_labels(flags, VAULT_SOVEREIGNTY_FLAG_LABELS)
        if not math.isfinite(fields[5]) or not math.isfinite(fields[10]):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "totalVaultBytes": fields[0],
                "arenaBytes": fields[1],
                "activeBufferCount": fields[2],
                "generationMisses": fields[3],
                "strideMultiplier": fields[4],
                "maxMemoryJobUs": finite_round(fields[5]),
                "frame": fields[6],
                "vaultGenerationId": fields[7],
                "bufferId": fields[8],
                "bufferIdHex": f"0x{fields[8]:08X}",
                "stateHash": fields[9],
                "stateHashHex": f"0x{fields[9]:08X}",
                "globalQualityWeight": finite_round(fields[10]),
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "reserved0": fields[12],
                "fault": bool(flags & (1 << 0)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if entry_count > VAULT_SOVEREIGNTY_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if any(entry.get("fault") for entry in entries):
        warnings.append("fault_flag")
    if any(entry.get("stateHash") == 0 for entry in entries):
        warnings.append("state_hash_zero")
    if any(entry.get("totalVaultBytes", 0) < 0 or entry.get("arenaBytes", 0) < 0 for entry in entries):
        warnings.append("negative_bytes")
    if any(entry.get("arenaBytes", 0) > entry.get("totalVaultBytes", 0) >= 0 for entry in entries):
        warnings.append("arena_exceeds_total_vault_bytes")
    if any(entry.get("activeBufferCount", 0) < 0 or entry.get("generationMisses", 0) < 0 for entry in entries):
        warnings.append("negative_counts")
    if any(entry.get("strideMultiplier", 0) < 1 or entry.get("strideMultiplier", 0) > 16 for entry in entries):
        warnings.append("stride_multiplier_out_of_range")
    if any(
        entry.get("maxMemoryJobUs") is None or entry.get("maxMemoryJobUs", 0.0) < 0.0
        for entry in entries
    ):
        warnings.append("memory_job_time_out_of_range")
    if any(
        entry.get("globalQualityWeight") is None
        or entry.get("globalQualityWeight", 0.0) < 0.0
        or entry.get("globalQualityWeight", 0.0) > 1.0
        for entry in entries
    ):
        warnings.append("quality_weight_out_of_range")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("reserved0") for entry in entries):
        warnings.append("reserved_nonzero")
    return {
        "type": "vault_sovereignty_telemetry_blackbox",
        "magic": magic,
        "version": version,
        "headerBytes": VAULT_SOVEREIGNTY_TELEMETRY_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": entry_count,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "memoryMap": build_vault_sovereignty_memory_map(latest),
        "warnings": warnings,
    }


def is_arm64_alignment_telemetry_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPSHINOBU204BIN", "DUMPSHINOBU204H8DUMP"}


def parse_arm64_alignment_telemetry_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < ARM64_ALIGNMENT_TELEMETRY_HEADER_BYTES:
        return {
            "type": "arm64_alignment_telemetry_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, entry_count, entry_size = ARM64_ALIGNMENT_TELEMETRY_HEADER.unpack_from(data, 0)
    if (
        magic != ARM64_ALIGNMENT_TELEMETRY_MAGIC
        or version != ARM64_ALIGNMENT_TELEMETRY_VERSION
        or entry_count <= 0
        or entry_size != ARM64_ALIGNMENT_TELEMETRY_ENTRY_BYTES
    ):
        return {
            "type": "arm64_alignment_telemetry_blackbox",
            "magic": magic,
            "version": version,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = ARM64_ALIGNMENT_TELEMETRY_HEADER_BYTES
    expected_bytes = payload_offset + entry_count * entry_size
    readable_entries = min(entry_count, max(0, len(data) - payload_offset) // entry_size)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = ARM64_ALIGNMENT_TELEMETRY_ENTRY.unpack_from(data, offset)
        flags = fields[8]
        flag_labels, unknown_flags = resolve_bit_labels(flags, ARM64_ALIGNMENT_TELEMETRY_FLAG_LABELS)
        if any(not math.isfinite(value) for value in fields[2:5]):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "structHash": fields[0],
                "structHashHex": f"0x{fields[0]:016X}",
                "offendingAddress": fields[1],
                "offendingAddressHex": f"0x{fields[1]:016X}",
                "aupOrRuntimePosition": {
                    "x": finite_round(fields[2]),
                    "y": finite_round(fields[3]),
                    "z": finite_round(fields[4]),
                },
                "bufferID": fields[5],
                "bufferIDHex": f"0x{fields[5]:08X}",
                "byteOffset": fields[6],
                "frame": fields[7],
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "severity01": finite_round(fields[9]),
                "stateHash": fields[10],
                "stateHashHex": f"0x{fields[10]:08X}",
                "pack1Detected": bool(flags & (1 << 0)),
                "misalignedEightByteField": bool(flags & (1 << 1)),
                "invalidStride": bool(flags & (1 << 2)),
                "dynamicCastFault": bool(flags & (1 << 3)),
                "dumpWritten": bool(flags & (1 << 4)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if entry_count > ARM64_ALIGNMENT_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_position")
    if any(entry.get("pack1Detected") for entry in entries):
        warnings.append("pack1_detected")
    if any(entry.get("misalignedEightByteField") for entry in entries):
        warnings.append("misaligned_8_byte_field")
    if any(entry.get("invalidStride") for entry in entries):
        warnings.append("invalid_stride")
    if any(entry.get("dynamicCastFault") for entry in entries):
        warnings.append("dynamic_cast_fault")
    if any(entry.get("stateHash") == 0 for entry in entries):
        warnings.append("state_hash_zero")
    if any(
        entry.get("severity01") is None or entry.get("severity01", 0.0) < 0.0 or entry.get("severity01", 0.0) > 1.0
        for entry in entries
    ):
        warnings.append("severity_out_of_range")
    return {
        "type": "arm64_alignment_telemetry_blackbox",
        "magic": magic,
        "version": version,
        "headerBytes": ARM64_ALIGNMENT_TELEMETRY_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": entry_count,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_haptic_synthesis_telemetry_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPSHINOBU353BIN", "DUMPSHINOBU353H8DUMP"}


def parse_haptic_synthesis_telemetry_blackbox(data: bytes) -> dict[str, Any]:
    entry_size = HAPTIC_SYNTHESIS_TELEMETRY_ENTRY_BYTES
    expected_bytes = HAPTIC_SYNTHESIS_TELEMETRY_CAPACITY * entry_size
    available_entries = len(data) // entry_size
    readable_entries = min(HAPTIC_SYNTHESIS_TELEMETRY_CAPACITY, available_entries)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = HAPTIC_SYNTHESIS_TELEMETRY_ENTRY.unpack_from(data, offset)
        flags = fields[9]
        flag_labels, unknown_flags = resolve_bit_labels(flags, HAPTIC_SYNTHESIS_FLAG_LABELS)
        if any(not math.isfinite(value) for value in (fields[0], fields[1], fields[2], fields[3], fields[4], fields[10])):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "playerAup": {
                    "x": finite_round(fields[0]),
                    "y": finite_round(fields[1]),
                    "z": finite_round(fields[2]),
                },
                "finalLowFrequency01": finite_round(fields[3]),
                "finalHighFrequency01": finite_round(fields[4]),
                "frame": fields[5],
                "rawSignalCount": fields[6],
                "droppedSignalCount": fields[7],
                "burstExecutionMicroseconds": fields[8],
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "globalQualityWeight": finite_round(fields[10]),
                "stateHash": fields[11],
                "stateHashHex": f"0x{fields[11]:08X}",
                "generatedPulseCount": fields[12],
                "nanSanitized": bool(flags & (1 << 0)),
                "budgetExceeded": bool(flags & (1 << 1)),
                "pulseOverflow": bool(flags & (1 << 2)),
                "missingPlayerAup": bool(flags & (1 << 3)),
                "mockStormActive": bool(flags & (1 << 4)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if available_entries > HAPTIC_SYNTHESIS_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("nanSanitized") for entry in entries):
        warnings.append("nan_sanitized")
    if any(entry.get("budgetExceeded") for entry in entries):
        warnings.append("budget_exceeded")
    if any(entry.get("pulseOverflow") for entry in entries):
        warnings.append("pulse_overflow")
    if any(entry.get("missingPlayerAup") for entry in entries):
        warnings.append("missing_player_aup")
    if any(entry.get("mockStormActive") for entry in entries):
        warnings.append("mock_storm_active")
    if any(entry.get("stateHash") == 0 for entry in entries):
        warnings.append("state_hash_zero")
    if any(entry.get("droppedSignalCount", 0) > 0 for entry in entries):
        warnings.append("dropped_signals")
    if any(entry.get("burstExecutionMicroseconds", 0) > 200 for entry in entries):
        warnings.append("burst_over_200us")
    if any(entry.get("generatedPulseCount", 0) > HAPTIC_SYNTHESIS_PULSE_CAPACITY for entry in entries):
        warnings.append("generated_pulse_over_capacity")
    if any(
        entry.get("finalLowFrequency01") is None
        or entry.get("finalHighFrequency01") is None
        or entry.get("finalLowFrequency01", 0.0) < 0.0
        or entry.get("finalLowFrequency01", 0.0) > 1.0
        or entry.get("finalHighFrequency01", 0.0) < 0.0
        or entry.get("finalHighFrequency01", 0.0) > 1.0
        for entry in entries
    ):
        warnings.append("motor_out_of_range")
    if any(
        entry.get("globalQualityWeight") is None
        or entry.get("globalQualityWeight", 0.0) < 0.0
        or entry.get("globalQualityWeight", 0.0) > 1.0
        for entry in entries
    ):
        warnings.append("quality_weight_out_of_range")

    return {
        "type": "haptic_synthesis_telemetry_blackbox",
        "headerBytes": 0,
        "entrySize": entry_size,
        "capacity": HAPTIC_SYNTHESIS_TELEMETRY_CAPACITY,
        "pulseCapacity": HAPTIC_SYNTHESIS_PULSE_CAPACITY,
        "declaredEntryCount": HAPTIC_SYNTHESIS_TELEMETRY_CAPACITY,
        "availableEntryCount": available_entries,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_vocal_warning_telemetry_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPSHINOBU352VWSBIN", "DUMPSHINOBU352VWSH8DUMP", "DUMPX011BIN", "DUMPX011H8DUMP"}


def vocal_warning_id_label(warning_id: int) -> str:
    return VOCAL_WARNING_ID_LABELS.get(warning_id, f"unknown={warning_id}")


def parse_vocal_warning_telemetry_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < VOCAL_WARNING_TELEMETRY_HEADER_BYTES:
        return {
            "type": "vocal_warning_telemetry_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, entry_stride, capacity, cursor, emitted_count, ring_start, reserved0 = (
        VOCAL_WARNING_TELEMETRY_HEADER.unpack_from(data, 0)
    )
    invalid_header = (
        magic != VOCAL_WARNING_TELEMETRY_MAGIC
        or version != VOCAL_WARNING_TELEMETRY_VERSION
        or entry_stride != VOCAL_WARNING_TELEMETRY_ENTRY_BYTES
        or capacity <= 0
        or capacity > VOCAL_WARNING_TELEMETRY_CAPACITY
        or emitted_count > capacity
        or cursor >= capacity
        or (emitted_count > 0 and ring_start >= capacity)
    )
    if invalid_header:
        return {
            "type": "vocal_warning_telemetry_blackbox",
            "magic": magic,
            "version": version,
            "entrySize": entry_stride,
            "capacity": capacity,
            "telemetryCursor": cursor,
            "emittedCount": emitted_count,
            "ringStartIndex": ring_start,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = VOCAL_WARNING_TELEMETRY_HEADER_BYTES
    expected_bytes = payload_offset + emitted_count * entry_stride
    readable_entries = min(emitted_count, max(0, len(data) - payload_offset) // entry_stride)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = payload_offset + index * entry_stride
        if is_empty_entry(data, offset, entry_stride):
            continue

        fields = VOCAL_WARNING_TELEMETRY_ENTRY.unpack_from(data, offset)
        active_alarm_mask = fields[3]
        fault_flags = fields[9]
        active_alarm_labels, active_alarm_unknown = resolve_bit_labels64(
            active_alarm_mask,
            VOCAL_WARNING_ACTIVE_ALARM_LABELS,
        )
        fault_labels, unknown_fault_flags = resolve_bit_labels(fault_flags, VOCAL_WARNING_FAULT_LABELS)
        current_warning_id = fields[12]
        last_dispatched_warning_id = fields[13]
        if not math.isfinite(fields[7]) or not math.isfinite(fields[8]):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "ringSlot": (ring_start + index) % capacity,
                "sourceAupGrid": {"x": fields[0], "y": fields[1], "z": fields[2]},
                "activeAlarmsMask": active_alarm_mask,
                "activeAlarmsMaskHex": f"0x{active_alarm_mask:016X}",
                "activeAlarmLabels": active_alarm_labels,
                "unknownActiveAlarmFlags": active_alarm_unknown,
                "frame": fields[4],
                "activePriorityCount": fields[5],
                "currentAudioBankHashID": fields[6],
                "currentAudioBankHashHex": f"0x{fields[6]:08X}",
                "currentPriorityScore": finite_round(fields[7]),
                "burstExecutionMicros": finite_round(fields[8]),
                "faultFlags": fault_flags,
                "faultFlagLabels": fault_labels,
                "unknownFaultFlags": unknown_fault_flags,
                "highestPriorityBitIndex": fields[10],
                "directionHash": fields[11],
                "directionHashHex": f"0x{fields[11]:04X}",
                "currentWarningId": current_warning_id,
                "currentWarningLabel": vocal_warning_id_label(current_warning_id),
                "lastDispatchedWarningId": last_dispatched_warning_id,
                "lastDispatchedWarningLabel": vocal_warning_id_label(last_dispatched_warning_id),
                "telemetryInvalid": bool(fault_flags & (1 << 0)),
                "priorityInvalid": bool(fault_flags & (1 << 1)),
                "priorityInputInvalid": bool(fault_flags & (1 << 2)),
                "vocalCueRejected": bool(fault_flags & (1 << 3)),
                "subtitleRejected": bool(fault_flags & (1 << 4)),
                "alarmMaskOverflow": bool(fault_flags & (1 << 5)),
                "vocalWarningSignalRejected": bool(fault_flags & (1 << 6)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_stride != 0:
        warnings.append("trailing_partial_entry")
    if reserved0 != 0:
        warnings.append("reserved_nonzero")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("unknownFaultFlags") for entry in entries):
        warnings.append("unknown_fault_flags")
    if any(entry.get("unknownActiveAlarmFlags") for entry in entries):
        warnings.append("unknown_active_alarm_flags")
    if any(entry.get("faultFlags") for entry in entries):
        warnings.append("fault_flags")
    if any(entry.get("telemetryInvalid") for entry in entries):
        warnings.append("telemetry_invalid")
    if any(entry.get("priorityInvalid") for entry in entries):
        warnings.append("priority_invalid")
    if any(entry.get("priorityInputInvalid") for entry in entries):
        warnings.append("priority_input_invalid")
    if any(entry.get("vocalCueRejected") for entry in entries):
        warnings.append("vocal_cue_rejected")
    if any(entry.get("subtitleRejected") for entry in entries):
        warnings.append("subtitle_rejected")
    if any(entry.get("alarmMaskOverflow") for entry in entries):
        warnings.append("alarm_mask_overflow")
    if any(entry.get("vocalWarningSignalRejected") for entry in entries):
        warnings.append("vocal_warning_signal_rejected")
    if any(entry.get("burstExecutionMicros") is None or entry.get("burstExecutionMicros", 0.0) < 0.0 for entry in entries):
        warnings.append("burst_time_out_of_range")
    if any(entry.get("burstExecutionMicros", 0.0) > 100.0 for entry in entries):
        warnings.append("burst_over_100us")
    if any(entry.get("currentPriorityScore") is None or entry.get("currentPriorityScore", 0.0) < 0.0 for entry in entries):
        warnings.append("priority_score_out_of_range")
    if any(
        entry.get("currentWarningId") not in VOCAL_WARNING_ID_LABELS
        or entry.get("lastDispatchedWarningId") not in VOCAL_WARNING_ID_LABELS
        for entry in entries
    ):
        warnings.append("unknown_warning_id")
    if any(
        entry.get("currentAudioBankHashID", 0) != 0 and entry.get("currentWarningId", 0) == 0
        for entry in entries
    ):
        warnings.append("audio_bank_hash_without_warning_id")
    return {
        "type": "vocal_warning_telemetry_blackbox",
        "magic": magic,
        "version": version,
        "headerBytes": VOCAL_WARNING_TELEMETRY_HEADER_BYTES,
        "entrySize": entry_stride,
        "declaredEntryCount": emitted_count,
        "capacity": capacity,
        "telemetryCursor": cursor,
        "ringStartIndex": ring_start,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_granular_audio_telemetry_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMPPROCEDURALSYNTHBIN",
        "DUMPPROCEDURALSYNTHH8DUMP",
        "DUMPSTRUCTURALACOUSTICSLEADBIN",
        "DUMPSTRUCTURALACOUSTICSLEADH8DUMP",
        "DUMPACOUSTICREFLECTIONMAPPERBIN",
        "DUMPACOUSTICREFLECTIONMAPPERH8DUMP",
        "DUMPKINETICIMPACTACOUSTICSBIN",
        "DUMPKINETICIMPACTACOUSTICSH8DUMP",
        "DUMPSHINOBU351BIN",
        "DUMPSHINOBU351H8DUMP",
    }


def parse_granular_audio_telemetry_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < GRANULAR_AUDIO_TELEMETRY_HEADER_BYTES:
        return {
            "type": "granular_audio_telemetry_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    capacity, cursor = GRANULAR_AUDIO_TELEMETRY_HEADER.unpack_from(data, 0)
    invalid_header = (
        capacity <= 0
        or capacity > GRANULAR_AUDIO_TELEMETRY_CAPACITY
        or cursor < 0
        or cursor >= capacity
        or GRANULAR_AUDIO_TELEMETRY_ROW.size != GRANULAR_AUDIO_TELEMETRY_ROW_BYTES
    )
    if invalid_header:
        return {
            "type": "granular_audio_telemetry_blackbox",
            "headerBytes": GRANULAR_AUDIO_TELEMETRY_HEADER_BYTES,
            "entrySize": GRANULAR_AUDIO_TELEMETRY_ROW_BYTES,
            "capacity": capacity,
            "telemetryCursor": cursor,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = GRANULAR_AUDIO_TELEMETRY_HEADER_BYTES
    entry_size = GRANULAR_AUDIO_TELEMETRY_ROW_BYTES
    expected_bytes = payload_offset + capacity * entry_size
    readable_entries = min(capacity, max(0, len(data) - payload_offset) // entry_size)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = GRANULAR_AUDIO_TELEMETRY_ROW.unpack_from(data, offset)
        flags = fields[10]
        flag_labels, unknown_flags = resolve_bit_labels(flags, GRANULAR_AUDIO_FLAG_LABELS)
        if any(not math.isfinite(value) for value in fields[1:7]):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "sampleIndex": fields[0],
                "stress01": finite_round(fields[1]),
                "stressDerivative01": finite_round(fields[2]),
                "depth01": finite_round(fields[3]),
                "impact01": finite_round(fields[4]),
                "mixedSample": finite_round(fields[5]),
                "peakImpactEnergyJoules": finite_round(fields[6]),
                "activeVoices": fields[7],
                "voiceLimit": fields[8],
                "activeEchoTaps": fields[9],
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "invalid": bool(flags & (1 << 0)),
                "voiceLimitReached": bool(flags & (1 << 1)),
                "impactDriveActive": bool(flags & (1 << 2)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("sampleIndex"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if capacity != GRANULAR_AUDIO_TELEMETRY_CAPACITY:
        warnings.append("capacity_mismatch")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("invalid") for entry in entries):
        warnings.append("invalid")
    if any(entry.get("voiceLimitReached") for entry in entries):
        warnings.append("voice_limit_reached")
    if any(
        entry.get("activeVoices", 0) < 0
        or entry.get("activeVoices", 0) > GRANULAR_AUDIO_VOICE_CAPACITY
        or entry.get("voiceLimit", 0) < 0
        or entry.get("voiceLimit", 0) > GRANULAR_AUDIO_VOICE_CAPACITY
        for entry in entries
    ):
        warnings.append("voice_count_out_of_range")
    if any(entry.get("activeVoices", 0) > entry.get("voiceLimit", GRANULAR_AUDIO_VOICE_CAPACITY) for entry in entries):
        warnings.append("active_voices_over_limit")
    if any(
        entry.get("activeEchoTaps", 0) < 0
        or entry.get("activeEchoTaps", 0) > GRANULAR_AUDIO_ECHO_TAP_CAPACITY
        for entry in entries
    ):
        warnings.append("echo_taps_out_of_range")
    if any(
        entry.get("stress01") is None
        or entry.get("stressDerivative01") is None
        or entry.get("depth01") is None
        or entry.get("impact01") is None
        or entry.get("stress01", 0.0) < 0.0
        or entry.get("stress01", 0.0) > 1.0
        or entry.get("stressDerivative01", 0.0) < 0.0
        or entry.get("stressDerivative01", 0.0) > 1.0
        or entry.get("depth01", 0.0) < 0.0
        or entry.get("depth01", 0.0) > 1.0
        or entry.get("impact01", 0.0) < 0.0
        or entry.get("impact01", 0.0) > 1.0
        for entry in entries
    ):
        warnings.append("drive_out_of_range")
    if any(
        entry.get("peakImpactEnergyJoules") is None
        or entry.get("peakImpactEnergyJoules", 0.0) < 0.0
        or entry.get("peakImpactEnergyJoules", 0.0) > GRANULAR_AUDIO_MAX_SAFE_IMPACT_JOULES
        for entry in entries
    ):
        warnings.append("impact_energy_out_of_range")

    return {
        "type": "granular_audio_telemetry_blackbox",
        "headerBytes": GRANULAR_AUDIO_TELEMETRY_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": capacity,
        "capacity": capacity,
        "telemetryCursor": cursor,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_prologue_audio_transition_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPPROLOGUEACOUSTICORCHESTRATORBIN", "DUMPPROLOGUEACOUSTICORCHESTRATORH8DUMP"}


def prologue_audio_stage_label(stage: int) -> str:
    return PROLOGUE_AUDIO_STAGE_LABELS.get(stage, f"unknown={stage}")


def parse_prologue_audio_transition_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < PROLOGUE_AUDIO_TRANSITION_HEADER_BYTES:
        return {
            "type": "prologue_audio_transition_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    capacity, cursor = PROLOGUE_AUDIO_TRANSITION_HEADER.unpack_from(data, 0)
    invalid_header = (
        capacity <= 0
        or capacity > PROLOGUE_AUDIO_TRANSITION_CAPACITY
        or cursor < 0
        or cursor >= capacity
        or PROLOGUE_AUDIO_TRANSITION_ROW.size != PROLOGUE_AUDIO_TRANSITION_ROW_BYTES
    )
    if invalid_header:
        return {
            "type": "prologue_audio_transition_blackbox",
            "headerBytes": PROLOGUE_AUDIO_TRANSITION_HEADER_BYTES,
            "entrySize": PROLOGUE_AUDIO_TRANSITION_ROW_BYTES,
            "capacity": capacity,
            "telemetryCursor": cursor,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = PROLOGUE_AUDIO_TRANSITION_HEADER_BYTES
    entry_size = PROLOGUE_AUDIO_TRANSITION_ROW_BYTES
    expected_bytes = payload_offset + capacity * entry_size
    readable_entries = min(capacity, max(0, len(data) - payload_offset) // entry_size)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = PROLOGUE_AUDIO_TRANSITION_ROW.unpack_from(data, offset)
        state_flags = fields[12]
        dsp_flags = fields[15]
        state_labels, unknown_state_flags = resolve_bit_labels(state_flags, PROLOGUE_AUDIO_STATE_FLAG_LABELS)
        dsp_labels, unknown_dsp_flags = resolve_bit_labels(dsp_flags, PROLOGUE_AUDIO_DSP_FLAG_LABELS)
        if any(not math.isfinite(value) for value in fields[2:10]):
            nonfinite_seen = True
        stage = fields[11]
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "sequence": fields[1],
                "universeVelocityMetersPerSecond": finite_round(fields[2]),
                "heat01": finite_round(fields[3]),
                "lowPassCutoffHz": finite_round(fields[4], 2),
                "lfeGain01": finite_round(fields[5]),
                "granularStress01": finite_round(fields[6]),
                "splashdownGain01": finite_round(fields[7]),
                "portalBlend01": finite_round(fields[8]),
                "audioLowPassCutoffHz": finite_round(fields[9], 2),
                "splashdownSamplesRemaining": fields[10],
                "stage": stage,
                "stageLabel": prologue_audio_stage_label(stage),
                "flags": state_flags,
                "flagLabels": state_labels,
                "unknownFlags": unknown_state_flags,
                "qualityTier": fields[13],
                "reserved": fields[14],
                "dspFlags": dsp_flags,
                "dspFlagLabels": dsp_labels,
                "unknownDspFlags": unknown_dsp_flags,
                "splashdown": bool(state_flags & (1 << 0)),
                "portalActive": bool(state_flags & (1 << 1)),
                "granularEnabled": bool(state_flags & (1 << 2)),
                "lowTierProxy": bool(state_flags & (1 << 3)),
                "nonFiniteGuard": bool(state_flags & (1 << 4)),
                "dspInvalid": bool(dsp_flags & (1 << 0)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if capacity != PROLOGUE_AUDIO_TRANSITION_CAPACITY:
        warnings.append("capacity_mismatch")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if any(entry.get("unknownDspFlags") for entry in entries):
        warnings.append("unknown_dsp_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("dspInvalid") for entry in entries):
        warnings.append("invalid")
    if any(entry.get("nonFiniteGuard") for entry in entries):
        warnings.append("nonfinite_guard")
    if any(entry.get("stage") not in PROLOGUE_AUDIO_STAGE_LABELS for entry in entries):
        warnings.append("unknown_stage")
    if any(entry.get("reserved", 0) != 0 for entry in entries):
        warnings.append("reserved_nonzero")
    if any(entry.get("splashdownSamplesRemaining", 0) < 0 for entry in entries):
        warnings.append("splashdown_samples_negative")
    if any(
        entry.get("universeVelocityMetersPerSecond") is None
        or entry.get("universeVelocityMetersPerSecond", 0.0) < 0.0
        for entry in entries
    ):
        warnings.append("velocity_out_of_range")
    if any(
        entry.get(name) is None or entry.get(name, 0.0) < 0.0 or entry.get(name, 0.0) > 1.0
        for entry in entries
        for name in ("heat01", "lfeGain01", "granularStress01", "splashdownGain01", "portalBlend01")
    ):
        warnings.append("blend_out_of_range")
    if any(
        entry.get("lowPassCutoffHz") is None
        or entry.get("audioLowPassCutoffHz") is None
        or entry.get("lowPassCutoffHz", 0.0) <= 0.0
        or entry.get("audioLowPassCutoffHz", 0.0) <= 0.0
        or entry.get("lowPassCutoffHz", 0.0) > PROLOGUE_AUDIO_OPEN_LOW_PASS_HZ
        or entry.get("audioLowPassCutoffHz", 0.0) > PROLOGUE_AUDIO_OPEN_LOW_PASS_HZ
        for entry in entries
    ):
        warnings.append("low_pass_out_of_range")

    return {
        "type": "prologue_audio_transition_blackbox",
        "headerBytes": PROLOGUE_AUDIO_TRANSITION_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": capacity,
        "capacity": capacity,
        "telemetryCursor": cursor,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_audio_synthesis_telemetry_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMP1320SYNTHESISBIN", "DUMP1320SYNTHESISH8DUMP"}


def audio_synthesis_failure_label(failure_code: int) -> str:
    return AUDIO_SYNTHESIS_FAILURE_LABELS.get(failure_code, f"unknown={failure_code}")


def parse_audio_synthesis_telemetry_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < AUDIO_SYNTHESIS_TELEMETRY_HEADER_BYTES:
        return {
            "type": "audio_synthesis_telemetry_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    capacity, cursor = AUDIO_SYNTHESIS_TELEMETRY_HEADER.unpack_from(data, 0)
    invalid_header = (
        capacity <= 0
        or capacity > AUDIO_SYNTHESIS_TELEMETRY_CAPACITY
        or cursor < 0
        or cursor >= capacity
        or AUDIO_SYNTHESIS_TELEMETRY_ROW.size != AUDIO_SYNTHESIS_TELEMETRY_ROW_BYTES
    )
    if invalid_header:
        return {
            "type": "audio_synthesis_telemetry_blackbox",
            "headerBytes": AUDIO_SYNTHESIS_TELEMETRY_HEADER_BYTES,
            "entrySize": AUDIO_SYNTHESIS_TELEMETRY_ROW_BYTES,
            "capacity": capacity,
            "telemetryCursor": cursor,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = AUDIO_SYNTHESIS_TELEMETRY_HEADER_BYTES
    entry_size = AUDIO_SYNTHESIS_TELEMETRY_ROW_BYTES
    expected_bytes = payload_offset + capacity * entry_size
    readable_entries = min(capacity, max(0, len(data) - payload_offset) // entry_size)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = AUDIO_SYNTHESIS_TELEMETRY_ROW.unpack_from(data, offset)
        flags = fields[6]
        flag_labels, unknown_flags = resolve_bit_labels(flags, AUDIO_SYNTHESIS_FLAG_LABELS)
        failure_code = fields[11]
        if not math.isfinite(fields[9]) or not math.isfinite(fields[10]):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "stopwatchTicks": fields[0],
                "frame": fields[1],
                "bufferId": fields[2],
                "bufferIdHex": f"0x{fields[2]:08X}",
                "systemId": fields[3],
                "expectedGeneration": fields[4],
                "actualGeneration": fields[5],
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "activePolyphony": fields[7],
                "voiceLimit": fields[8],
                "dspMicroseconds": finite_round(fields[9], 2),
                "globalQualityWeight": finite_round(fields[10]),
                "failureCode": failure_code,
                "failureLabel": audio_synthesis_failure_label(failure_code),
                "underrunCount": fields[12],
                "lockContention": bool(flags & (1 << 0)),
                "staleOrMissingHandle": bool(flags & (1 << 1)),
                "nonFiniteSample": bool(flags & (1 << 2)),
                "outputUnderrun": bool(flags & (1 << 3)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if capacity != AUDIO_SYNTHESIS_TELEMETRY_CAPACITY:
        warnings.append("capacity_mismatch")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("failureCode", 0) != 0 for entry in entries):
        warnings.append("failure_code")
    if any(entry.get("failureCode") not in AUDIO_SYNTHESIS_FAILURE_LABELS for entry in entries):
        warnings.append("unknown_failure_code")
    if any(entry.get("lockContention") for entry in entries):
        warnings.append("lock_contention")
    if any(entry.get("staleOrMissingHandle") for entry in entries):
        warnings.append("stale_or_missing_handle")
    if any(entry.get("nonFiniteSample") for entry in entries):
        warnings.append("nonfinite_sample")
    if any(entry.get("outputUnderrun") for entry in entries):
        warnings.append("output_underrun")
    if any(entry.get("expectedGeneration") != entry.get("actualGeneration") for entry in entries):
        warnings.append("generation_mismatch")
    if any(entry.get("systemId") != AUDIO_SYNTHESIS_AUDIO_PLAYER_CRITICAL_SYSTEM_ID for entry in entries):
        warnings.append("system_id_mismatch")
    if any(entry.get("underrunCount", 0) > 0 for entry in entries):
        warnings.append("underruns")
    if any(
        entry.get("activePolyphony", 0) < 0
        or entry.get("activePolyphony", 0) > GRANULAR_AUDIO_VOICE_CAPACITY
        or entry.get("voiceLimit", 0) < 0
        or entry.get("voiceLimit", 0) > GRANULAR_AUDIO_VOICE_CAPACITY
        for entry in entries
    ):
        warnings.append("voice_count_out_of_range")
    if any(entry.get("activePolyphony", 0) > entry.get("voiceLimit", GRANULAR_AUDIO_VOICE_CAPACITY) for entry in entries):
        warnings.append("active_polyphony_over_limit")
    if any(entry.get("dspMicroseconds") is None or entry.get("dspMicroseconds", 0.0) < 0.0 for entry in entries):
        warnings.append("dsp_time_out_of_range")
    if any(
        entry.get("globalQualityWeight") is None
        or entry.get("globalQualityWeight", 0.0) < 0.0
        or entry.get("globalQualityWeight", 0.0) > 1.0
        for entry in entries
    ):
        warnings.append("quality_weight_out_of_range")

    return {
        "type": "audio_synthesis_telemetry_blackbox",
        "headerBytes": AUDIO_SYNTHESIS_TELEMETRY_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": capacity,
        "capacity": capacity,
        "telemetryCursor": cursor,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_vocal_bank_synthesis_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMP1308SYNTHESISBIN", "DUMP1308SYNTHESISH8DUMP"}


def vocal_bank_synthesis_codec_label(codec: int) -> str:
    return VOCAL_BANK_SYNTHESIS_CODEC_LABELS.get(codec, f"unknown={codec}")


def parse_vocal_bank_synthesis_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < VOCAL_BANK_SYNTHESIS_HEADER_BYTES:
        return {
            "type": "vocal_bank_synthesis_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, capacity, entry_stride, cursor, last_fault_flags, last_phrase_hash, frame_counter = (
        VOCAL_BANK_SYNTHESIS_HEADER.unpack_from(data, 0)
    )
    invalid_header = (
        magic != VOCAL_BANK_SYNTHESIS_MAGIC
        or version != VOCAL_BANK_SYNTHESIS_VERSION
        or capacity <= 0
        or capacity > VOCAL_BANK_SYNTHESIS_TELEMETRY_CAPACITY
        or entry_stride != VOCAL_BANK_SYNTHESIS_ENTRY_BYTES
        or cursor >= capacity
        or VOCAL_BANK_SYNTHESIS_ENTRY.size != VOCAL_BANK_SYNTHESIS_ENTRY_BYTES
    )
    if invalid_header:
        return {
            "type": "vocal_bank_synthesis_blackbox",
            "magic": magic,
            "version": version,
            "headerBytes": VOCAL_BANK_SYNTHESIS_HEADER_BYTES,
            "entrySize": entry_stride,
            "capacity": capacity,
            "telemetryCursor": cursor,
            "lastFaultFlags": last_fault_flags,
            "lastPhraseHashID": last_phrase_hash,
            "lastPhraseHashHex": f"0x{last_phrase_hash:08X}",
            "frameCounter": frame_counter,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = VOCAL_BANK_SYNTHESIS_HEADER_BYTES
    expected_bytes = payload_offset + capacity * entry_stride
    readable_entries = min(capacity, max(0, len(data) - payload_offset) // entry_stride)
    entries = []
    nonfinite_seen = False
    last_fault_labels, unknown_last_fault_flags = resolve_bit_labels(
        last_fault_flags,
        VOCAL_BANK_SYNTHESIS_FLAG_LABELS,
    )

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = payload_offset + index * entry_stride
        if is_empty_entry(data, offset, entry_stride):
            continue

        fields = VOCAL_BANK_SYNTHESIS_ENTRY.unpack_from(data, offset)
        flags = fields[10]
        flag_labels, unknown_flags = resolve_bit_labels(flags, VOCAL_BANK_SYNTHESIS_FLAG_LABELS)
        if any(not math.isfinite(value) for value in fields[4:9]):
            nonfinite_seen = True
        codec = fields[14]
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "phraseHashID": fields[1],
                "phraseHashHex": f"0x{fields[1]:08X}",
                "currentSampleIndex": fields[2],
                "totalSamples": fields[3],
                "dspMicroseconds": finite_round(fields[4], 2),
                "outputPeak": finite_round(fields[5]),
                "outputRms": finite_round(fields[6]),
                "qualityWeight01": finite_round(fields[7]),
                "radioDistortion01": finite_round(fields[8]),
                "priority": fields[9],
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "underrunCount": fields[11],
                "payloadByteLength": fields[12],
                "sampleRate": fields[13],
                "codec": codec,
                "codecLabel": vocal_bank_synthesis_codec_label(codec),
                "playing": bool(flags & (1 << 0)),
                "vorbisUnsupported": bool(flags & (1 << 1)),
                "nonFinite": bool(flags & (1 << 2)),
                "bankMiss": bool(flags & (1 << 3)),
                "interrupted": bool(flags & (1 << 4)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_stride != 0:
        warnings.append("trailing_partial_entry")
    if capacity != VOCAL_BANK_SYNTHESIS_TELEMETRY_CAPACITY:
        warnings.append("capacity_mismatch")
    if unknown_last_fault_flags:
        warnings.append("unknown_last_fault_flags")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if last_fault_flags:
        warnings.append("last_fault_flags")
    if any(entry.get("vorbisUnsupported") for entry in entries) or (last_fault_flags & (1 << 1)):
        warnings.append("vorbis_unsupported")
    if any(entry.get("nonFinite") for entry in entries) or (last_fault_flags & (1 << 2)):
        warnings.append("nonfinite")
    if any(entry.get("bankMiss") for entry in entries) or (last_fault_flags & (1 << 3)):
        warnings.append("bank_miss")
    if any(entry.get("interrupted") for entry in entries):
        warnings.append("interrupted")
    if any(entry.get("underrunCount", 0) > 0 for entry in entries):
        warnings.append("underruns")
    if any(entry.get("dspMicroseconds") is None or entry.get("dspMicroseconds", 0.0) < 0.0 for entry in entries):
        warnings.append("dsp_time_out_of_range")
    if any(entry.get("dspMicroseconds", 0.0) > VOCAL_BANK_SYNTHESIS_DSP_DUMP_THRESHOLD_US for entry in entries):
        warnings.append("dsp_over_1000us")
    if any(
        entry.get("qualityWeight01") is None
        or entry.get("qualityWeight01", 0.0) < 0.0
        or entry.get("qualityWeight01", 0.0) > 1.0
        or entry.get("radioDistortion01") is None
        or entry.get("radioDistortion01", 0.0) < 0.0
        or entry.get("radioDistortion01", 0.0) > 1.0
        for entry in entries
    ):
        warnings.append("quality_or_distortion_out_of_range")
    if any(
        entry.get("outputPeak") is None
        or entry.get("outputPeak", 0.0) < 0.0
        or entry.get("outputRms") is None
        or entry.get("outputRms", 0.0) < 0.0
        for entry in entries
    ):
        warnings.append("output_meter_out_of_range")
    if any(entry.get("totalSamples", 0) > 0 and entry.get("currentSampleIndex", 0) > entry.get("totalSamples", 0) for entry in entries):
        warnings.append("sample_index_out_of_range")
    if any(entry.get("sampleRate", 0) <= 0 for entry in entries):
        warnings.append("sample_rate_out_of_range")
    if any(entry.get("codec") not in VOCAL_BANK_SYNTHESIS_CODEC_LABELS for entry in entries):
        warnings.append("unknown_codec")

    return {
        "type": "vocal_bank_synthesis_blackbox",
        "magic": magic,
        "version": version,
        "headerBytes": VOCAL_BANK_SYNTHESIS_HEADER_BYTES,
        "entrySize": entry_stride,
        "declaredEntryCount": capacity,
        "capacity": capacity,
        "telemetryCursor": cursor,
        "lastFaultFlags": last_fault_flags,
        "lastFaultFlagLabels": last_fault_labels,
        "unknownLastFaultFlags": unknown_last_fault_flags,
        "lastPhraseHashID": last_phrase_hash,
        "lastPhraseHashHex": f"0x{last_phrase_hash:08X}",
        "frameCounter": frame_counter,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_adaptive_stem_mixer_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPSTEMMIXERBIN", "DUMPSTEMMIXERH8DUMP"}


def parse_adaptive_stem_mixer_blackbox(data: bytes) -> dict[str, Any]:
    entry_size = ADAPTIVE_STEM_MIXER_ENTRY_BYTES
    expected_bytes = ADAPTIVE_STEM_MIXER_TELEMETRY_CAPACITY * entry_size
    available_entries = len(data) // entry_size
    readable_entries = min(ADAPTIVE_STEM_MIXER_TELEMETRY_CAPACITY, available_entries)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = ADAPTIVE_STEM_MIXER_ENTRY.unpack_from(data, offset)
        flags = fields[3]
        flag_labels, unknown_flags = resolve_bit_labels(flags, ADAPTIVE_STEM_MIXER_FLAG_LABELS)
        if any(not math.isfinite(value) for value in fields[4:16]):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "activeStemHash": fields[1],
                "activeStemHashHex": f"0x{fields[1]:08X}",
                "biomeHash": fields[2],
                "biomeHashHex": f"0x{fields[2]:08X}",
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "tensionIndex": finite_round(fields[4]),
                "depthFilter": finite_round(fields[5]),
                "cutoffHz": finite_round(fields[6], 2),
                "mixerUpdateMicroseconds": finite_round(fields[7], 2),
                "baseVolume": finite_round(fields[8]),
                "actionVolume": finite_round(fields[9]),
                "depthVolume": finite_round(fields[10]),
                "bossVolume": finite_round(fields[11]),
                "qualityWeight": finite_round(fields[12]),
                "beatPhase01": finite_round(fields[13]),
                "ioPressure01": finite_round(fields[14]),
                "updateCadenceHz": finite_round(fields[15], 3),
                "beatGateOpen": bool(flags & (1 << 0)),
                "narrativeOverride": bool(flags & (1 << 1)),
                "ioTransitionDelay": bool(flags & (1 << 2)),
                "clipNotStreaming": bool(flags & (1 << 3)),
                "nonFinite": bool(flags & (1 << 4)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if available_entries > ADAPTIVE_STEM_MIXER_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("nonFinite") for entry in entries):
        warnings.append("nonfinite")
    if any(entry.get("clipNotStreaming") for entry in entries):
        warnings.append("clip_not_streaming")
    if any(entry.get("ioTransitionDelay") for entry in entries):
        warnings.append("io_transition_delay")
    if any(
        entry.get("mixerUpdateMicroseconds") is None
        or entry.get("mixerUpdateMicroseconds", 0.0) < 0.0
        for entry in entries
    ):
        warnings.append("mixer_time_out_of_range")
    if any(entry.get("mixerUpdateMicroseconds", 0.0) > ADAPTIVE_STEM_MIXER_DUMP_THRESHOLD_US for entry in entries):
        warnings.append("mixer_over_1000us")
    if any(
        entry.get("qualityWeight") is None
        or entry.get("beatPhase01") is None
        or entry.get("ioPressure01") is None
        or entry.get("qualityWeight", 0.0) < 0.0
        or entry.get("qualityWeight", 0.0) > 1.0
        or entry.get("beatPhase01", 0.0) < 0.0
        or entry.get("beatPhase01", 0.0) > 1.0
        or entry.get("ioPressure01", 0.0) < 0.0
        or entry.get("ioPressure01", 0.0) > 1.0
        for entry in entries
    ):
        warnings.append("quality_or_phase_out_of_range")
    if any(
        entry.get("cutoffHz") is None
        or entry.get("cutoffHz", 0.0) <= 0.0
        or entry.get("updateCadenceHz") is None
        or entry.get("updateCadenceHz", 0.0) <= 0.0
        for entry in entries
    ):
        warnings.append("frequency_out_of_range")
    if any(
        entry.get("baseVolume") is None
        or entry.get("actionVolume") is None
        or entry.get("depthVolume") is None
        or entry.get("bossVolume") is None
        or entry.get("baseVolume", 0.0) < 0.0
        or entry.get("actionVolume", 0.0) < 0.0
        or entry.get("depthVolume", 0.0) < 0.0
        or entry.get("bossVolume", 0.0) < 0.0
        for entry in entries
    ):
        warnings.append("volume_out_of_range")

    return {
        "type": "adaptive_stem_mixer_blackbox",
        "headerBytes": 0,
        "entrySize": entry_size,
        "capacity": ADAPTIVE_STEM_MIXER_TELEMETRY_CAPACITY,
        "declaredEntryCount": ADAPTIVE_STEM_MIXER_TELEMETRY_CAPACITY,
        "availableEntryCount": available_entries,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_camera_juice_telemetry_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMPCAMERAJUICESYSTEMBIN",
        "DUMPCAMERAJUICESYSTEMH8DUMP",
    }


def parse_camera_juice_telemetry_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < CAMERA_JUICE_TELEMETRY_HEADER_BYTES:
        return {
            "type": "camera_juice_telemetry_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, entry_size, capacity, cursor, count, start_index, reserved0 = (
        CAMERA_JUICE_TELEMETRY_HEADER.unpack_from(data, 0)
    )
    invalid_header = (
        magic != CAMERA_JUICE_TELEMETRY_MAGIC
        or version != CAMERA_JUICE_TELEMETRY_VERSION
        or entry_size != CAMERA_JUICE_TELEMETRY_ENTRY_BYTES
        or capacity <= 0
        or capacity > CAMERA_JUICE_TELEMETRY_CAPACITY
        or count < 0
        or count > capacity
        or start_index < 0
        or (count > 0 and start_index >= capacity)
        or CAMERA_JUICE_TELEMETRY_ENTRY.size != CAMERA_JUICE_TELEMETRY_ENTRY_BYTES
    )
    if invalid_header:
        return {
            "type": "camera_juice_telemetry_blackbox",
            "magic": magic,
            "version": version,
            "headerBytes": CAMERA_JUICE_TELEMETRY_HEADER_BYTES,
            "entrySize": entry_size,
            "capacity": capacity,
            "telemetryCursor": cursor,
            "declaredEntryCount": count,
            "startIndex": start_index,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = CAMERA_JUICE_TELEMETRY_HEADER_BYTES
    expected_bytes = payload_offset + count * entry_size
    readable_entries = min(count, max(0, len(data) - payload_offset) // entry_size)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = CAMERA_JUICE_TELEMETRY_ENTRY.unpack_from(data, offset)
        flags = fields[1]
        flag_labels, unknown_flags = resolve_bit_labels(flags, CAMERA_JUICE_FLAG_LABELS)
        float_values = fields[2:10] + fields[11:14]
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "ringSlot": (start_index + index) % capacity,
                "frame": fields[0],
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "traumaScalar": finite_round(fields[2]),
                "maxTranslationalOffsetMagnitude": finite_round(fields[3]),
                "offset": {
                    "x": finite_round(fields[4]),
                    "y": finite_round(fields[5]),
                    "z": finite_round(fields[6]),
                },
                "rotationDegrees": {
                    "x": finite_round(fields[7]),
                    "y": finite_round(fields[8]),
                    "z": finite_round(fields[9]),
                },
                "incomingSignalCount": fields[10],
                "burstExecutionMicroseconds": finite_round(fields[11], 2),
                "globalQualityWeight01": finite_round(fields[12]),
                "directionalImpulseMagnitude": finite_round(fields[13]),
                "stateHash": fields[14],
                "stateHashHex": f"0x{fields[14]:08X}",
                "sequence": fields[15],
                "xrSuppressed": bool(flags & (1 << 0)),
                "nanSanitized": bool(flags & (1 << 1)),
                "noPlayerAup": bool(flags & (1 << 2)),
                "vrSomaticWriteRejected": bool(flags & (1 << 3)),
                "vaultUnavailable": bool(flags & (1 << 4)),
                "burstBudgetExceeded": bool(flags & (1 << 5)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if reserved0 != 0:
        warnings.append("reserved_nonzero")
    if capacity != CAMERA_JUICE_TELEMETRY_CAPACITY:
        warnings.append("capacity_mismatch")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("nanSanitized") for entry in entries):
        warnings.append("nan_sanitized")
    if any(entry.get("noPlayerAup") for entry in entries):
        warnings.append("no_player_aup")
    if any(entry.get("vrSomaticWriteRejected") for entry in entries):
        warnings.append("vr_somatic_write_rejected")
    if any(entry.get("vaultUnavailable") for entry in entries):
        warnings.append("vault_unavailable")
    if any(entry.get("burstBudgetExceeded") for entry in entries):
        warnings.append("burst_budget_exceeded")
    if any(
        entry.get("burstExecutionMicroseconds") is None
        or entry.get("burstExecutionMicroseconds", 0.0) < 0.0
        for entry in entries
    ):
        warnings.append("burst_time_out_of_range")
    if any(entry.get("burstExecutionMicroseconds", 0.0) > CAMERA_JUICE_BURST_BUDGET_US for entry in entries):
        warnings.append("burst_over_100us")
    if any(entry.get("incomingSignalCount", 0) < 0 or entry.get("incomingSignalCount", 0) > 32 for entry in entries):
        warnings.append("incoming_signal_count_out_of_range")
    if any(
        entry.get("traumaScalar") is None
        or entry.get("globalQualityWeight01") is None
        or entry.get("traumaScalar", 0.0) < 0.0
        or entry.get("traumaScalar", 0.0) > 1.0
        or entry.get("globalQualityWeight01", 0.0) < 0.0
        or entry.get("globalQualityWeight01", 0.0) > 1.0
        for entry in entries
    ):
        warnings.append("trauma_or_quality_out_of_range")
    if any(entry.get("maxTranslationalOffsetMagnitude") is None or entry.get("maxTranslationalOffsetMagnitude", 0.0) < 0.0 for entry in entries):
        warnings.append("offset_magnitude_out_of_range")
    if any(entry.get("stateHash") == 0 for entry in entries):
        warnings.append("state_hash_zero")
    if any(entry.get("sequence") != entry.get("frame") for entry in entries):
        warnings.append("sequence_frame_mismatch")

    return {
        "type": "camera_juice_telemetry_blackbox",
        "magic": magic,
        "version": version,
        "headerBytes": CAMERA_JUICE_TELEMETRY_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": count,
        "capacity": capacity,
        "telemetryCursor": cursor,
        "startIndex": start_index,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_material_decay_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPMATERIALDECAYARTISTBIN", "DUMPMATERIALDECAYARTISTH8DUMP"}


def material_decay_dump_reason_label(reason: int) -> str:
    return MATERIAL_DECAY_DUMP_REASON_LABELS.get(reason, f"unknown={reason}")


def parse_material_decay_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < MATERIAL_DECAY_HEADER_BYTES:
        return {
            "type": "material_decay_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, dump_reason, cursor, capacity = MATERIAL_DECAY_HEADER.unpack_from(data, 0)
    invalid_header = (
        magic != MATERIAL_DECAY_MAGIC
        or capacity <= 0
        or capacity > MATERIAL_DECAY_TELEMETRY_CAPACITY
        or cursor < 0
        or cursor >= capacity
        or MATERIAL_DECAY_ROW.size != MATERIAL_DECAY_ROW_BYTES
    )
    if invalid_header:
        return {
            "type": "material_decay_blackbox",
            "magic": magic,
            "headerBytes": MATERIAL_DECAY_HEADER_BYTES,
            "entrySize": MATERIAL_DECAY_ROW_BYTES,
            "capacity": capacity,
            "telemetryCursor": cursor,
            "dumpReason": dump_reason,
            "dumpReasonLabel": material_decay_dump_reason_label(dump_reason),
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = MATERIAL_DECAY_HEADER_BYTES
    expected_bytes = payload_offset + capacity * MATERIAL_DECAY_ROW_BYTES
    readable_entries = min(capacity, max(0, len(data) - payload_offset) // MATERIAL_DECAY_ROW_BYTES)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = payload_offset + index * MATERIAL_DECAY_ROW_BYTES
        if is_empty_entry(data, offset, MATERIAL_DECAY_ROW_BYTES):
            continue

        fields = MATERIAL_DECAY_ROW.unpack_from(data, offset)
        flags = fields[8]
        flag_labels, unknown_flags = resolve_bit_labels(flags, MATERIAL_DECAY_FLAG_LABELS)
        if any(not math.isfinite(value) for value in fields[2:5]):
            nonfinite_seen = True
        quality_weight_byte = fields[7]
        entries.append(
            {
                "slot": index,
                "ringSlot": (cursor + index) % capacity,
                "frame": fields[0],
                "itemHash": fields[1],
                "itemHashHex": f"0x{fields[1]:08X}",
                "rust01": finite_round(fields[2]),
                "wetness01": finite_round(fields[3]),
                "blood01": finite_round(fields[4]),
                "slotIndex": fields[5],
                "reason": fields[6],
                "qualityWeightByte": quality_weight_byte,
                "qualityWeight01": round(quality_weight_byte / 255.0, 4),
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "stateHash": fields[9],
                "stateHashHex": f"0x{fields[9]:08X}",
                "rustActive": bool(flags & (1 << 0)),
                "wet": bool(flags & (1 << 1)),
                "blood": bool(flags & (1 << 2)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % MATERIAL_DECAY_ROW_BYTES != 0:
        warnings.append("trailing_partial_entry")
    if capacity != MATERIAL_DECAY_TELEMETRY_CAPACITY:
        warnings.append("capacity_mismatch")
    if dump_reason != 0:
        warnings.append("dump_reason")
    if dump_reason not in MATERIAL_DECAY_DUMP_REASON_LABELS:
        warnings.append("unknown_dump_reason")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("stateHash") == 0 for entry in entries):
        warnings.append("state_hash_zero")
    if any(
        entry.get("rust01") is None
        or entry.get("wetness01") is None
        or entry.get("blood01") is None
        or entry.get("rust01", 0.0) < 0.0
        or entry.get("rust01", 0.0) > 1.0
        or entry.get("wetness01", 0.0) < 0.0
        or entry.get("wetness01", 0.0) > 1.0
        or entry.get("blood01", 0.0) < 0.0
        or entry.get("blood01", 0.0) > 1.0
        for entry in entries
    ):
        warnings.append("decay_value_out_of_range")
    if any(entry.get("rustActive") for entry in entries):
        warnings.append("rust_active")
    if any(entry.get("wet") for entry in entries):
        warnings.append("wet")
    if any(entry.get("blood") for entry in entries):
        warnings.append("blood")

    return {
        "type": "material_decay_blackbox",
        "magic": magic,
        "headerBytes": MATERIAL_DECAY_HEADER_BYTES,
        "entrySize": MATERIAL_DECAY_ROW_BYTES,
        "declaredEntryCount": capacity,
        "capacity": capacity,
        "telemetryCursor": cursor,
        "dumpReason": dump_reason,
        "dumpReasonLabel": material_decay_dump_reason_label(dump_reason),
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_interactive_wake_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPINTERACTIVEWAKEVFXBIN", "DUMPINTERACTIVEWAKEVFXH8DUMP"}


def parse_interactive_wake_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < INTERACTIVE_WAKE_HEADER_BYTES:
        return {
            "type": "interactive_wake_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, capacity, cursor = INTERACTIVE_WAKE_HEADER.unpack_from(data, 0)
    invalid_header = (
        magic != INTERACTIVE_WAKE_MAGIC
        or capacity <= 0
        or capacity > INTERACTIVE_WAKE_BLACKBOX_CAPACITY
        or cursor < 0
        or cursor >= capacity
        or INTERACTIVE_WAKE_ENTRY.size != INTERACTIVE_WAKE_ENTRY_BYTES
    )
    if invalid_header:
        return {
            "type": "interactive_wake_blackbox",
            "magic": magic,
            "magicHex": f"0x{magic:08X}",
            "headerBytes": INTERACTIVE_WAKE_HEADER_BYTES,
            "entrySize": INTERACTIVE_WAKE_ENTRY_BYTES,
            "capacity": capacity,
            "telemetryCursor": cursor,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = INTERACTIVE_WAKE_HEADER_BYTES
    expected_bytes = payload_offset + capacity * INTERACTIVE_WAKE_ENTRY_BYTES
    readable_entries = min(capacity, max(0, len(data) - payload_offset) // INTERACTIVE_WAKE_ENTRY_BYTES)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = payload_offset + index * INTERACTIVE_WAKE_ENTRY_BYTES
        if is_empty_entry(data, offset, INTERACTIVE_WAKE_ENTRY_BYTES):
            continue

        fields = INTERACTIVE_WAKE_ENTRY.unpack_from(data, offset)
        flags = fields[11]
        flag_labels, unknown_flags = resolve_bit_labels(flags, INTERACTIVE_WAKE_FLAG_LABELS)
        float_values = fields[3:11] + fields[15:17]
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "ringSlot": (cursor + index) % capacity,
                "frame": fields[0],
                "activeWakeSourcesCount": fields[1],
                "slotLimit": fields[2],
                "strongestWakePositionWS": {
                    "x": finite_round(fields[3]),
                    "y": finite_round(fields[4]),
                    "z": finite_round(fields[5]),
                },
                "strongestIntensity": finite_round(fields[6]),
                "strongestVelocityWS": {
                    "x": finite_round(fields[7]),
                    "y": finite_round(fields[8]),
                    "z": finite_round(fields[9]),
                },
                "maxRadius": finite_round(fields[10]),
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "stateHash": fields[12],
                "stateHashHex": f"0x{fields[12]:08X}",
                "dataVaultGeneration": fields[13],
                "aupShiftSequence": fields[14],
                "systemStress01": finite_round(fields[15]),
                "budgetPressure01": finite_round(fields[16]),
                "invalidInput": bool(flags & (1 << 0)),
                "nan": bool(flags & (1 << 1)),
                "budgetPressure": bool(flags & (1 << 2)),
                "thermalPressure": bool(flags & (1 << 3)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % INTERACTIVE_WAKE_ENTRY_BYTES != 0:
        warnings.append("trailing_partial_entry")
    if capacity != INTERACTIVE_WAKE_BLACKBOX_CAPACITY:
        warnings.append("capacity_mismatch")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("invalidInput") for entry in entries):
        warnings.append("invalid_input")
    if any(entry.get("nan") for entry in entries):
        warnings.append("nan_flag")
    if any(entry.get("budgetPressure") for entry in entries):
        warnings.append("budget_pressure")
    if any(entry.get("thermalPressure") for entry in entries):
        warnings.append("thermal_pressure")
    if any(entry.get("stateHash") == 0 for entry in entries):
        warnings.append("state_hash_zero")
    if any(
        entry.get("activeWakeSourcesCount", 0) > INTERACTIVE_WAKE_MAX_SOURCE_SLOTS
        or entry.get("slotLimit", 0) > INTERACTIVE_WAKE_MAX_SOURCE_SLOTS
        or entry.get("activeWakeSourcesCount", 0) > entry.get("slotLimit", 0)
        for entry in entries
    ):
        warnings.append("wake_source_count_out_of_range")
    if any(
        entry.get("strongestIntensity") is None
        or entry.get("strongestIntensity", 0.0) < 0.0
        or entry.get("maxRadius") is None
        or entry.get("maxRadius", 0.0) < 0.0
        or entry.get("systemStress01") is None
        or entry.get("systemStress01", 0.0) < 0.0
        or entry.get("systemStress01", 0.0) > 1.0
        or entry.get("budgetPressure01") is None
        or entry.get("budgetPressure01", 0.0) < 0.0
        or entry.get("budgetPressure01", 0.0) > 1.0
        for entry in entries
    ):
        warnings.append("wake_value_out_of_range")

    return {
        "type": "interactive_wake_blackbox",
        "magic": magic,
        "magicHex": f"0x{magic:08X}",
        "headerBytes": INTERACTIVE_WAKE_HEADER_BYTES,
        "entrySize": INTERACTIVE_WAKE_ENTRY_BYTES,
        "declaredEntryCount": capacity,
        "capacity": capacity,
        "telemetryCursor": cursor,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_flora_sway_field_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPFLORASWAYDIRECTORBIN", "DUMPFLORASWAYDIRECTORH8DUMP"}


def parse_flora_sway_field_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < FLORA_SWAY_FIELD_HEADER_BYTES:
        return {
            "type": "flora_sway_field_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, capacity, cursor = FLORA_SWAY_FIELD_HEADER.unpack_from(data, 0)
    invalid_header = (
        magic != FLORA_SWAY_FIELD_MAGIC
        or capacity <= 0
        or capacity > FLORA_SWAY_FIELD_BLACKBOX_CAPACITY
        or cursor < 0
        or cursor >= capacity
        or FLORA_SWAY_FIELD_ENTRY.size != FLORA_SWAY_FIELD_ENTRY_BYTES
    )
    if invalid_header:
        return {
            "type": "flora_sway_field_blackbox",
            "magic": magic,
            "magicHex": f"0x{magic:08X}",
            "headerBytes": FLORA_SWAY_FIELD_HEADER_BYTES,
            "entrySize": FLORA_SWAY_FIELD_ENTRY_BYTES,
            "capacity": capacity,
            "telemetryCursor": cursor,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = FLORA_SWAY_FIELD_HEADER_BYTES
    expected_bytes = payload_offset + capacity * FLORA_SWAY_FIELD_ENTRY_BYTES
    readable_entries = min(capacity, max(0, len(data) - payload_offset) // FLORA_SWAY_FIELD_ENTRY_BYTES)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = payload_offset + index * FLORA_SWAY_FIELD_ENTRY_BYTES
        if is_empty_entry(data, offset, FLORA_SWAY_FIELD_ENTRY_BYTES):
            continue

        fields = FLORA_SWAY_FIELD_ENTRY.unpack_from(data, offset)
        flags = fields[4]
        flag_labels, unknown_flags = resolve_bit_labels(flags, FLORA_SWAY_FIELD_FLAG_LABELS)
        float_values = fields[5:13]
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "ringSlot": (cursor + index) % capacity,
                "frame": fields[0],
                "resolution": fields[1],
                "activeWakeSourcesCount": fields[2],
                "nonZeroCellsCount": fields[3],
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "fieldCenterWS": {
                    "x": finite_round(fields[5]),
                    "y": finite_round(fields[6]),
                    "z": finite_round(fields[7]),
                },
                "cellSize": finite_round(fields[8]),
                "maxMagnitude": finite_round(fields[9]),
                "globalQualityWeight": finite_round(fields[10]),
                "updateIntervalSeconds": finite_round(fields[11], 5),
                "systemStress01": finite_round(fields[12]),
                "stateHash": fields[13],
                "stateHashHex": f"0x{fields[13]:08X}",
                "dataVaultGeneration": fields[14],
                "aupShiftSequence": fields[15],
                "cpuMicroseconds": fields[16],
                "invalidInput": bool(flags & (1 << 0)),
                "nan": bool(flags & (1 << 1)),
                "vaultMissing": bool(flags & (1 << 2)),
                "emptyWake": bool(flags & (1 << 3)),
                "uploadStall": bool(flags & (1 << 4)),
                "wrappedShift": bool(flags & (1 << 5)),
                "fullReset": bool(flags & (1 << 6)),
                "discardedUpload": bool(flags & (1 << 7)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % FLORA_SWAY_FIELD_ENTRY_BYTES != 0:
        warnings.append("trailing_partial_entry")
    if capacity != FLORA_SWAY_FIELD_BLACKBOX_CAPACITY:
        warnings.append("capacity_mismatch")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("invalidInput") for entry in entries):
        warnings.append("invalid_input")
    if any(entry.get("nan") for entry in entries):
        warnings.append("nan_flag")
    if any(entry.get("vaultMissing") for entry in entries):
        warnings.append("vault_missing")
    if any(entry.get("uploadStall") for entry in entries):
        warnings.append("upload_stall")
    if any(entry.get("discardedUpload") for entry in entries):
        warnings.append("discarded_upload")
    if any(entry.get("wrappedShift") for entry in entries):
        warnings.append("wrapped_shift")
    if any(entry.get("fullReset") for entry in entries):
        warnings.append("full_reset")
    if any(entry.get("stateHash") == 0 for entry in entries):
        warnings.append("state_hash_zero")
    if any(
        entry.get("resolution", 0) < FLORA_SWAY_FIELD_MIN_RESOLUTION
        or entry.get("resolution", 0) > FLORA_SWAY_FIELD_MAX_RESOLUTION
        for entry in entries
    ):
        warnings.append("resolution_out_of_range")
    if any(
        entry.get("activeWakeSourcesCount", 0) > INTERACTIVE_WAKE_MAX_SOURCE_SLOTS
        or entry.get("nonZeroCellsCount", 0) > FLORA_SWAY_FIELD_MAX_NODE_COUNT
        for entry in entries
    ):
        warnings.append("cell_or_wake_count_out_of_range")
    if any(
        entry.get("cellSize") is None
        or entry.get("cellSize", 0.0) < FLORA_SWAY_FIELD_MIN_CELL_SIZE
        or entry.get("cellSize", 0.0) > FLORA_SWAY_FIELD_MAX_CELL_SIZE
        or entry.get("maxMagnitude") is None
        or entry.get("maxMagnitude", 0.0) < 0.0
        or entry.get("maxMagnitude", 0.0) > FLORA_SWAY_FIELD_MAX_DISPLACEMENT_METERS
        or entry.get("globalQualityWeight") is None
        or entry.get("globalQualityWeight", 0.0) < 0.0
        or entry.get("globalQualityWeight", 0.0) > 1.0
        or entry.get("updateIntervalSeconds") is None
        or entry.get("updateIntervalSeconds", 0.0) < FLORA_SWAY_FIELD_MIN_UPDATE_INTERVAL_SECONDS
        or entry.get("updateIntervalSeconds", 0.0) > FLORA_SWAY_FIELD_MAX_UPDATE_INTERVAL_SECONDS
        or entry.get("systemStress01") is None
        or entry.get("systemStress01", 0.0) < 0.0
        or entry.get("systemStress01", 0.0) > 1.0
        for entry in entries
    ):
        warnings.append("field_value_out_of_range")

    return {
        "type": "flora_sway_field_blackbox",
        "magic": magic,
        "magicHex": f"0x{magic:08X}",
        "headerBytes": FLORA_SWAY_FIELD_HEADER_BYTES,
        "entrySize": FLORA_SWAY_FIELD_ENTRY_BYTES,
        "declaredEntryCount": capacity,
        "capacity": capacity,
        "telemetryCursor": cursor,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_flora_memory_telemetry_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMP1327FLORAINTERACTIONBIN", "DUMP1327FLORAINTERACTIONH8DUMP"}


def parse_flora_memory_telemetry_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < FLORA_MEMORY_TELEMETRY_HEADER_BYTES:
        return {
            "type": "flora_memory_telemetry_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    capacity, cursor = FLORA_MEMORY_TELEMETRY_HEADER.unpack_from(data, 0)
    invalid_header = (
        capacity <= 0
        or capacity > FLORA_MEMORY_TELEMETRY_CAPACITY
        or cursor < 0
        or cursor >= capacity
        or FLORA_MEMORY_TELEMETRY_ENTRY.size != FLORA_MEMORY_TELEMETRY_ENTRY_BYTES
    )
    if invalid_header:
        return {
            "type": "flora_memory_telemetry_blackbox",
            "headerBytes": FLORA_MEMORY_TELEMETRY_HEADER_BYTES,
            "entrySize": FLORA_MEMORY_TELEMETRY_ENTRY_BYTES,
            "capacity": capacity,
            "telemetryCursor": cursor,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = FLORA_MEMORY_TELEMETRY_HEADER_BYTES
    expected_bytes = payload_offset + capacity * FLORA_MEMORY_TELEMETRY_ENTRY_BYTES
    readable_entries = min(capacity, max(0, len(data) - payload_offset) // FLORA_MEMORY_TELEMETRY_ENTRY_BYTES)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = payload_offset + index * FLORA_MEMORY_TELEMETRY_ENTRY_BYTES
        if is_empty_entry(data, offset, FLORA_MEMORY_TELEMETRY_ENTRY_BYTES):
            continue

        fields = FLORA_MEMORY_TELEMETRY_ENTRY.unpack_from(data, offset)
        flags = fields[7]
        flag_labels, unknown_flags = resolve_bit_labels(flags, FLORA_MEMORY_TELEMETRY_FLAG_LABELS)
        event_hash = fields[1]
        buffer_id = fields[2]
        expected_state_hash = (event_hash ^ buffer_id ^ fields[4] ^ flags) & 0xFFFFFFFF
        if any(not math.isfinite(value) for value in fields[10:12]):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "ringSlot": (cursor + index) % capacity,
                "frame": fields[0],
                "eventHash": event_hash,
                "eventHashHex": f"0x{event_hash:08X}",
                "eventLabel": FLORA_MEMORY_TELEMETRY_EVENT_LABELS.get(event_hash, "unknown"),
                "bufferId": buffer_id,
                "bufferLabel": FLORA_MEMORY_TELEMETRY_BUFFER_LABELS.get(buffer_id, "unknown"),
                "systemId": fields[3],
                "generation": fields[4],
                "requiredLength": fields[5],
                "actualLength": fields[6],
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "consecutiveFailures": fields[8],
                "vaultGeneration": fields[9],
                "globalQualityWeight": finite_round(fields[10]),
                "systemStress01": finite_round(fields[11]),
                "stateHash": fields[12],
                "stateHashHex": f"0x{fields[12]:08X}",
                "expectedStateHash": expected_state_hash,
                "expectedStateHashHex": f"0x{expected_state_hash:08X}",
                "stateHashOk": fields[12] == expected_state_hash,
                "aupShiftSequence": fields[13],
                "cpuMicroseconds": fields[14],
                "reserved0": fields[15],
                "missingVault": bool(flags & (1 << 0)),
                "invalidLength": bool(flags & (1 << 1)),
                "compactionFence": bool(flags & (1 << 2)),
                "handleMismatch": bool(flags & (1 << 3)),
                "resolveFailed": bool(flags & (1 << 4)),
                "invalidBuffer": bool(flags & (1 << 5)),
                "writeLockFailed": bool(flags & (1 << 6)),
                "nan": bool(flags & (1 << 7)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % FLORA_MEMORY_TELEMETRY_ENTRY_BYTES != 0:
        warnings.append("trailing_partial_entry")
    if capacity != FLORA_MEMORY_TELEMETRY_CAPACITY:
        warnings.append("capacity_mismatch")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if any(entry.get("eventLabel") == "unknown" for entry in entries):
        warnings.append("unknown_event_hash")
    if any(entry.get("bufferLabel") == "unknown" for entry in entries):
        warnings.append("unknown_buffer_id")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(not entry.get("stateHashOk", True) for entry in entries):
        warnings.append("state_hash_mismatch")
    if any(entry.get("reserved0") != 0 for entry in entries):
        warnings.append("reserved_nonzero")
    if any(entry.get("missingVault") for entry in entries):
        warnings.append("missing_vault")
    if any(entry.get("invalidLength") for entry in entries):
        warnings.append("invalid_length")
    if any(entry.get("compactionFence") for entry in entries):
        warnings.append("compaction_fence")
    if any(entry.get("handleMismatch") for entry in entries):
        warnings.append("handle_mismatch")
    if any(entry.get("resolveFailed") for entry in entries):
        warnings.append("resolve_failed")
    if any(entry.get("invalidBuffer") for entry in entries):
        warnings.append("invalid_buffer")
    if any(entry.get("writeLockFailed") for entry in entries):
        warnings.append("write_lock_failed")
    if any(entry.get("nan") for entry in entries):
        warnings.append("nan_flag")
    if any(entry.get("consecutiveFailures", 0) >= FLORA_MEMORY_TELEMETRY_DUMP_FAILURE_THRESHOLD for entry in entries):
        warnings.append("consecutive_failure_threshold")
    if any(
        entry.get("actualLength", 0) < entry.get("requiredLength", 0)
        and (
            entry.get("invalidBuffer")
            or entry.get("invalidLength")
            or entry.get("resolveFailed")
            or entry.get("missingVault")
        )
        for entry in entries
    ):
        warnings.append("actual_length_below_required")
    if any(
        entry.get("globalQualityWeight") is None
        or entry.get("globalQualityWeight", 0.0) < 0.0
        or entry.get("globalQualityWeight", 0.0) > 1.0
        or entry.get("systemStress01") is None
        or entry.get("systemStress01", 0.0) < 0.0
        or entry.get("systemStress01", 0.0) > 1.0
        for entry in entries
    ):
        warnings.append("quality_or_stress_out_of_range")

    return {
        "type": "flora_memory_telemetry_blackbox",
        "headerBytes": FLORA_MEMORY_TELEMETRY_HEADER_BYTES,
        "entrySize": FLORA_MEMORY_TELEMETRY_ENTRY_BYTES,
        "declaredEntryCount": capacity,
        "capacity": capacity,
        "telemetryCursor": cursor,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_flora_ambient_sway_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPSHINOBU267BIN", "DUMPSHINOBU267H8DUMP"}


def parse_flora_ambient_sway_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < FLORA_AMBIENT_SWAY_HEADER_BYTES:
        return {
            "type": "flora_ambient_sway_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, source_hash, entry_size, capacity, cursor = FLORA_AMBIENT_SWAY_HEADER.unpack_from(data, 0)
    invalid_header = (
        magic != FLORA_AMBIENT_SWAY_MAGIC
        or version != FLORA_AMBIENT_SWAY_VERSION
        or entry_size != FLORA_AMBIENT_SWAY_ENTRY_BYTES
        or capacity <= 0
        or capacity > FLORA_AMBIENT_SWAY_TELEMETRY_CAPACITY
        or cursor >= max(1, capacity)
        or FLORA_AMBIENT_SWAY_ENTRY.size != FLORA_AMBIENT_SWAY_ENTRY_BYTES
    )
    if invalid_header:
        return {
            "type": "flora_ambient_sway_blackbox",
            "magic": magic,
            "magicHex": f"0x{magic:08X}",
            "version": version,
            "sourceHash": source_hash,
            "sourceHashHex": f"0x{source_hash:08X}",
            "headerBytes": FLORA_AMBIENT_SWAY_HEADER_BYTES,
            "entrySize": entry_size,
            "capacity": capacity,
            "telemetryCursor": cursor,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = FLORA_AMBIENT_SWAY_HEADER_BYTES
    expected_bytes = payload_offset + capacity * entry_size
    readable_entries = min(capacity, max(0, len(data) - payload_offset) // entry_size)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = FLORA_AMBIENT_SWAY_ENTRY.unpack_from(data, offset)
        flags = fields[1]
        flag_labels, unknown_flags = resolve_bit_labels(flags, FLORA_AMBIENT_SWAY_FLAG_LABELS)
        if any(not math.isfinite(value) for value in fields[2:6]):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "ringSlot": (cursor + index) % capacity,
                "frame": fields[0],
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "wrappedTime": finite_round(fields[2], 5),
                "flowMagnitude": finite_round(fields[3]),
                "globalQualityWeight": finite_round(fields[4]),
                "amplitudeMeters": finite_round(fields[5]),
                "stateHash": fields[6],
                "stateHashHex": f"0x{fields[6]:08X}",
                "sourceHash": fields[7],
                "sourceHashHex": f"0x{fields[7]:08X}",
                "sourceHashOk": fields[7] == FLORA_AMBIENT_SWAY_SOURCE_HASH,
                "vaultMissing": bool(flags & (1 << 0)),
                "constantBufferUnsupported": bool(flags & (1 << 1)),
                "invalidNumber": bool(flags & (1 << 2)),
                "uploadSkipped": bool(flags & (1 << 3)),
                "burstKernelUnavailable": bool(flags & (1 << 4)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if capacity != FLORA_AMBIENT_SWAY_TELEMETRY_CAPACITY:
        warnings.append("capacity_mismatch")
    if source_hash != FLORA_AMBIENT_SWAY_SOURCE_HASH:
        warnings.append("source_hash_mismatch")
    if any(entry.get("sourceHashOk") is False for entry in entries):
        warnings.append("entry_source_hash_mismatch")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("vaultMissing") for entry in entries):
        warnings.append("vault_missing")
    if any(entry.get("constantBufferUnsupported") for entry in entries):
        warnings.append("constant_buffer_unsupported")
    if any(entry.get("invalidNumber") for entry in entries):
        warnings.append("invalid_number")
    if any(entry.get("uploadSkipped") for entry in entries):
        warnings.append("upload_skipped")
    if any(entry.get("burstKernelUnavailable") for entry in entries):
        warnings.append("burst_kernel_unavailable")
    if any(entry.get("stateHash") == 0 for entry in entries):
        warnings.append("state_hash_zero")
    if any(
        entry.get("flowMagnitude") is None
        or entry.get("flowMagnitude", 0.0) < 0.0
        or entry.get("globalQualityWeight") is None
        or entry.get("globalQualityWeight", 0.0) < 0.0
        or entry.get("globalQualityWeight", 0.0) > 1.0
        or entry.get("amplitudeMeters") is None
        or entry.get("amplitudeMeters", 0.0) < 0.0
        for entry in entries
    ):
        warnings.append("sway_value_out_of_range")

    return {
        "type": "flora_ambient_sway_blackbox",
        "magic": magic,
        "magicHex": f"0x{magic:08X}",
        "version": version,
        "sourceHash": source_hash,
        "sourceHashHex": f"0x{source_hash:08X}",
        "headerBytes": FLORA_AMBIENT_SWAY_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": capacity,
        "capacity": capacity,
        "telemetryCursor": cursor,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_vegetation_memory_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMP1316VEGETATIONBIN", "DUMP1316VEGETATIONH8DUMP"}


def float32_bits(value: float) -> int:
    return struct.unpack("<I", struct.pack("<f", value))[0]


def compute_vegetation_memory_state_hash(
    buffer_id: int,
    generation: int,
    frame: int,
    expected_length: int,
    actual_length: int,
    culled_instances: int,
    job_microseconds: float,
    quality_weight: float,
    failure_code: int,
    phase: int,
    flags: int,
    position_x: float,
    position_y: float,
    position_z: float,
) -> int:
    hash_value = 1469598103934665603

    def mix(value: int) -> None:
        nonlocal hash_value
        hash_value ^= value & 0xFFFFFFFF
        hash_value = (hash_value * 1099511628211) & 0xFFFFFFFFFFFFFFFF

    mix(buffer_id)
    mix(generation)
    mix(frame)
    mix(expected_length)
    mix(actual_length)
    mix(culled_instances)
    mix(float32_bits(job_microseconds))
    mix(float32_bits(quality_weight))
    mix(failure_code)
    mix(phase)
    mix(flags)
    mix(float32_bits(position_x))
    mix(float32_bits(position_y))
    mix(float32_bits(position_z))
    return hash_value


def parse_vegetation_memory_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < VEGETATION_MEMORY_HEADER_BYTES:
        return {
            "type": "vegetation_memory_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, capacity, entry_size, cursor = VEGETATION_MEMORY_HEADER.unpack_from(data, 0)
    invalid_header = (
        magic != VEGETATION_MEMORY_MAGIC
        or version != VEGETATION_MEMORY_VERSION
        or capacity <= 0
        or capacity > VEGETATION_MEMORY_TELEMETRY_CAPACITY
        or entry_size != VEGETATION_MEMORY_ENTRY_BYTES
        or cursor < 0
        or cursor >= capacity
        or VEGETATION_MEMORY_ENTRY.size != VEGETATION_MEMORY_ENTRY_BYTES
    )
    if invalid_header:
        return {
            "type": "vegetation_memory_blackbox",
            "magic": magic,
            "magicHex": f"0x{magic:016X}",
            "version": version,
            "headerBytes": VEGETATION_MEMORY_HEADER_BYTES,
            "entrySize": entry_size,
            "capacity": capacity,
            "telemetryCursor": cursor,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = VEGETATION_MEMORY_HEADER_BYTES
    expected_bytes = payload_offset + capacity * entry_size
    readable_entries = min(capacity, max(0, len(data) - payload_offset) // entry_size)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = VEGETATION_MEMORY_ENTRY.unpack_from(data, offset)
        flags = fields[11]
        flag_labels, unknown_flags = resolve_bit_labels(flags, VEGETATION_MEMORY_FLAG_LABELS)
        float_values = (fields[7], fields[8], fields[12], fields[13], fields[14])
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        expected_hash = compute_vegetation_memory_state_hash(
            fields[1],
            fields[2],
            fields[3],
            fields[4],
            fields[5],
            fields[6],
            fields[7],
            fields[8],
            fields[9],
            fields[10],
            flags,
            fields[12],
            fields[13],
            fields[14],
        )
        entries.append(
            {
                "slot": index,
                "ringSlot": (cursor + index) % capacity,
                "stateHash": fields[0],
                "stateHashHex": f"0x{fields[0]:016X}",
                "expectedStateHash": expected_hash,
                "expectedStateHashHex": f"0x{expected_hash:016X}",
                "stateHashOk": fields[0] == expected_hash,
                "bufferId": fields[1],
                "bufferLabel": VEGETATION_MEMORY_BUFFER_LABELS.get(fields[1], "unknown"),
                "generation": fields[2],
                "frame": fields[3],
                "expectedLength": fields[4],
                "actualLength": fields[5],
                "culledInstances": fields[6],
                "jobMicroseconds": finite_round(fields[7], 2),
                "qualityWeight": finite_round(fields[8]),
                "failureCode": fields[9],
                "failureCodeLabel": VEGETATION_MEMORY_FAILURE_CODE_LABELS.get(fields[9], "unknown"),
                "phase": fields[10],
                "phaseLabel": VEGETATION_MEMORY_PHASE_LABELS.get(fields[10], "unknown"),
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "position": {
                    "x": finite_round(fields[12]),
                    "y": finite_round(fields[13]),
                    "z": finite_round(fields[14]),
                },
                "reserved0": fields[15],
                "coldBoot": bool(flags & (1 << 0)),
                "defrag": bool(flags & (1 << 1)),
                "lockContention": bool(flags & (1 << 2)),
                "staleHandle": bool(flags & (1 << 3)),
                "nan": bool(flags & (1 << 4)),
                "capacity": bool(flags & (1 << 5)),
                "compactionFence": bool(flags & (1 << 6)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if capacity != VEGETATION_MEMORY_TELEMETRY_CAPACITY:
        warnings.append("capacity_mismatch")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if any(entry.get("failureCodeLabel") == "unknown" for entry in entries):
        warnings.append("unknown_failure_code")
    if any(entry.get("phaseLabel") == "unknown" for entry in entries):
        warnings.append("unknown_phase")
    if any(entry.get("bufferLabel") == "unknown" for entry in entries):
        warnings.append("unknown_buffer_id")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(not entry.get("stateHashOk", True) for entry in entries):
        warnings.append("state_hash_mismatch")
    if any(entry.get("stateHash") == 0 for entry in entries):
        warnings.append("state_hash_zero")
    if any(entry.get("reserved0") != 0 for entry in entries):
        warnings.append("reserved_nonzero")
    if any(entry.get("coldBoot") for entry in entries):
        warnings.append("cold_boot")
    if any(entry.get("defrag") for entry in entries):
        warnings.append("defrag")
    if any(entry.get("lockContention") for entry in entries):
        warnings.append("lock_contention")
    if any(entry.get("staleHandle") for entry in entries):
        warnings.append("stale_handle")
    if any(entry.get("nan") for entry in entries):
        warnings.append("nan_flag")
    if any(entry.get("capacity") for entry in entries):
        warnings.append("capacity_flag")
    if any(entry.get("compactionFence") for entry in entries):
        warnings.append("compaction_fence")
    if any(
        entry.get("expectedLength", 0) < 0
        or entry.get("actualLength", 0) < 0
        or entry.get("culledInstances", 0) < 0
        for entry in entries
    ):
        warnings.append("negative_count")
    if any(
        entry.get("actualLength", 0) < entry.get("expectedLength", 0)
        and (
            entry.get("staleHandle")
            or entry.get("capacity")
            or entry.get("lockContention")
            or entry.get("compactionFence")
        )
        for entry in entries
    ):
        warnings.append("actual_length_below_expected")
    if any(
        entry.get("qualityWeight") is None
        or entry.get("qualityWeight", 0.0) < 0.0
        or entry.get("qualityWeight", 0.0) > 1.0
        or entry.get("jobMicroseconds") is None
        or entry.get("jobMicroseconds", 0.0) < 0.0
        for entry in entries
    ):
        warnings.append("quality_or_job_time_out_of_range")

    return {
        "type": "vegetation_memory_blackbox",
        "magic": magic,
        "magicHex": f"0x{magic:016X}",
        "version": version,
        "headerBytes": VEGETATION_MEMORY_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": capacity,
        "capacity": capacity,
        "telemetryCursor": cursor,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_dear_lie_organics_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMP1318ORGANICSBIN", "DUMP1318ORGANICSH8DUMP"}


def compute_dear_lie_organics_hash(
    frame_index: int,
    damage_signal_count: int,
    destroyed_count: int,
    last_instance_uid: int,
    query_microseconds: float,
) -> int:
    hash_value = 2166136261
    for value in (
        frame_index & 0xFFFFFFFF,
        damage_signal_count & 0xFFFFFFFF,
        destroyed_count & 0xFFFFFFFF,
        last_instance_uid & 0xFFFFFFFF,
        float32_bits(query_microseconds),
    ):
        hash_value ^= value
        hash_value = (hash_value * 16777619) & 0xFFFFFFFF
    return hash_value


def parse_dear_lie_organics_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < DEAR_LIE_ORGANICS_ENTRY_BYTES:
        return {
            "type": "dear_lie_organics_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_entry"],
        }

    raw_entry_count = len(data) // DEAR_LIE_ORGANICS_ENTRY_BYTES
    readable_entries = min(raw_entry_count, DEAR_LIE_ORGANICS_TELEMETRY_CAPACITY)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = index * DEAR_LIE_ORGANICS_ENTRY_BYTES
        if is_empty_entry(data, offset, DEAR_LIE_ORGANICS_ENTRY_BYTES):
            continue

        fields = DEAR_LIE_ORGANICS_ENTRY.unpack_from(data, offset)
        flags = fields[14]
        flag_labels, unknown_flags = resolve_bit_labels(flags, DEAR_LIE_ORGANICS_FLAG_LABELS)
        if any(not math.isfinite(value) for value in (fields[10], fields[13])):
            nonfinite_seen = True
        expected_hash = compute_dear_lie_organics_hash(fields[0], fields[3], fields[4], fields[12], fields[13])
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "surfaceCount": fields[1],
                "underwaterCount": fields[2],
                "damageSignalCount": fields[3],
                "destroyedCount": fields[4],
                "vfxSignalCount": fields[5],
                "regenQueuedCount": fields[6],
                "recoveredCount": fields[7],
                "rejectedSignalCount": fields[8],
                "nanRejectCount": fields[9],
                "globalQualityWeight": finite_round(fields[10]),
                "hash": fields[11],
                "hashHex": f"0x{fields[11]:08X}",
                "expectedHash": expected_hash,
                "expectedHashHex": f"0x{expected_hash:08X}",
                "hashOk": fields[11] == expected_hash,
                "lastInstanceUid": fields[12],
                "lastInstanceUidHex": f"0x{fields[12]:08X}",
                "queryMicroseconds": finite_round(fields[13], 2),
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "regenerationRecovered": bool(flags & (1 << 2)),
                "guardFailed": bool(flags & (1 << 5)),
                "dropDrainFailed": bool(flags & (1 << 6)),
                "overflowOrReject": bool(flags & (1 << 7)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) % DEAR_LIE_ORGANICS_ENTRY_BYTES != 0:
        warnings.append("trailing_partial_entry")
    if raw_entry_count > DEAR_LIE_ORGANICS_TELEMETRY_CAPACITY:
        warnings.append("capacity_exceeded")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(not entry.get("hashOk", True) for entry in entries):
        warnings.append("hash_mismatch")
    if any(entry.get("regenerationRecovered") for entry in entries):
        warnings.append("regeneration_recovered")
    if any(entry.get("guardFailed") for entry in entries):
        warnings.append("guard_failed")
    if any(entry.get("dropDrainFailed") for entry in entries):
        warnings.append("drop_drain_failed")
    if any(entry.get("overflowOrReject") for entry in entries):
        warnings.append("overflow_or_reject")
    if any(entry.get("rejectedSignalCount", 0) > 0 for entry in entries):
        warnings.append("rejected_signals")
    if any(entry.get("nanRejectCount", 0) > 0 for entry in entries):
        warnings.append("nan_rejects")
    if any(
        entry.get("surfaceCount", 0) < 0
        or entry.get("underwaterCount", 0) < 0
        or entry.get("damageSignalCount", 0) < 0
        or entry.get("destroyedCount", 0) < 0
        or entry.get("vfxSignalCount", 0) < 0
        or entry.get("regenQueuedCount", 0) < 0
        or entry.get("recoveredCount", 0) < 0
        or entry.get("rejectedSignalCount", 0) < 0
        or entry.get("nanRejectCount", 0) < 0
        for entry in entries
    ):
        warnings.append("negative_count")
    if any(
        entry.get("damageSignalCount", 0) > DEAR_LIE_MAX_DAMAGE_SIGNALS_PER_FRAME
        or entry.get("destroyedCount", 0) > DEAR_LIE_MAX_RESULTS_PER_FRAME
        or entry.get("vfxSignalCount", 0) > DEAR_LIE_MAX_RESULTS_PER_FRAME
        or entry.get("regenQueuedCount", 0) > DEAR_LIE_MAX_REGEN_RECORDS
        for entry in entries
    ):
        warnings.append("count_out_of_range")
    if any(
        entry.get("globalQualityWeight") is None
        or entry.get("globalQualityWeight", 0.0) < 0.0
        or entry.get("globalQualityWeight", 0.0) > 1.0
        or entry.get("queryMicroseconds") is None
        or entry.get("queryMicroseconds", 0.0) < 0.0
        for entry in entries
    ):
        warnings.append("quality_or_query_out_of_range")

    return {
        "type": "dear_lie_organics_blackbox",
        "entrySize": DEAR_LIE_ORGANICS_ENTRY_BYTES,
        "declaredEntryCount": readable_entries,
        "rawEntryCount": raw_entry_count,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_chemical_influence_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPCHEMISTRYSURGEONBIN", "DUMPCHEMISTRYSURGEONH8DUMP"}


def compute_chemical_influence_state_hash(
    frame: int,
    active_emitters: int,
    mock_emitters: int,
    iterations: int,
    max_blood: float,
    global_quality_weight: float,
    flags: int,
) -> int:
    hash_value = 2166136261
    for value in (
        frame & 0xFFFFFFFF,
        active_emitters & 0xFFFFFFFF,
        mock_emitters & 0xFFFFFFFF,
        iterations & 0xFFFFFFFF,
        float32_bits(max_blood),
        float32_bits(global_quality_weight),
        flags & 0xFFFFFFFF,
    ):
        hash_value ^= value
        hash_value = (hash_value * 16777619) & 0xFFFFFFFF
    return hash_value or 1


def parse_chemical_influence_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < CHEMICAL_INFLUENCE_HEADER_BYTES:
        return {
            "type": "chemical_influence_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, capacity, entry_size = CHEMICAL_INFLUENCE_HEADER.unpack_from(data, 0)
    invalid_header = (
        magic != CHEMICAL_INFLUENCE_MAGIC
        or version != CHEMICAL_INFLUENCE_VERSION
        or capacity <= 0
        or capacity > CHEMICAL_INFLUENCE_TELEMETRY_CAPACITY
        or entry_size != CHEMICAL_INFLUENCE_ENTRY_BYTES
        or CHEMICAL_INFLUENCE_HEADER.size != CHEMICAL_INFLUENCE_HEADER_BYTES
        or CHEMICAL_INFLUENCE_ENTRY.size != CHEMICAL_INFLUENCE_ENTRY_BYTES
    )
    if invalid_header:
        return {
            "type": "chemical_influence_blackbox",
            "magic": magic,
            "magicHex": f"0x{magic:016X}",
            "version": version,
            "headerBytes": CHEMICAL_INFLUENCE_HEADER_BYTES,
            "entrySize": entry_size,
            "capacity": capacity,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = CHEMICAL_INFLUENCE_HEADER_BYTES
    expected_bytes = payload_offset + capacity * entry_size
    readable_entries = min(capacity, max(0, len(data) - payload_offset) // entry_size)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = CHEMICAL_INFLUENCE_ENTRY.unpack_from(data, offset)
        flags = fields[10]
        flag_labels, unknown_flags = resolve_bit_labels(flags, CHEMICAL_INFLUENCE_FLAG_LABELS)
        float_values = fields[0:5] + (fields[11],)
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        expected_hash = compute_chemical_influence_state_hash(
            fields[5],
            fields[6],
            fields[7],
            fields[8],
            fields[3],
            fields[11],
            flags,
        )
        entries.append(
            {
                "slot": index,
                "gridOriginAup": {
                    "x": finite_round(fields[0], 3),
                    "y": finite_round(fields[1], 3),
                    "z": finite_round(fields[2], 3),
                },
                "maxBlood": finite_round(fields[3]),
                "solverMicros": finite_round(fields[4], 2),
                "frame": fields[5],
                "activeEmitters": fields[6],
                "mockEmitters": fields[7],
                "iterations": fields[8],
                "stateHash": fields[9],
                "stateHashHex": f"0x{fields[9]:08X}",
                "expectedStateHash": expected_hash,
                "expectedStateHashHex": f"0x{expected_hash:08X}",
                "stateHashOk": fields[9] == expected_hash,
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "globalQualityWeight": finite_round(fields[11]),
                "gridShiftManhattan": fields[12],
                "nan": bool(flags & (1 << 0)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if capacity != CHEMICAL_INFLUENCE_TELEMETRY_CAPACITY:
        warnings.append("capacity_mismatch")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(not entry.get("stateHashOk", True) for entry in entries):
        warnings.append("state_hash_mismatch")
    if any(entry.get("stateHash") == 0 for entry in entries):
        warnings.append("state_hash_zero")
    if any(entry.get("nan") for entry in entries):
        warnings.append("nan_flag")
    if any(
        entry.get("activeEmitters", 0) < 0
        or entry.get("activeEmitters", 0) > CHEMICAL_INFLUENCE_MAX_ACTIVE_EMITTERS
        or entry.get("mockEmitters", 0) < 0
        or entry.get("mockEmitters", 0) > CHEMICAL_INFLUENCE_MAX_MOCK_EMITTERS
        for entry in entries
    ):
        warnings.append("emitter_count_out_of_range")
    if any(
        entry.get("iterations", 0) < 0
        or entry.get("iterations", 0) > CHEMICAL_INFLUENCE_MAX_JACOBI_ITERATIONS
        for entry in entries
    ):
        warnings.append("iterations_out_of_range")
    if any(entry.get("gridShiftManhattan", 0) < 0 for entry in entries):
        warnings.append("grid_shift_out_of_range")
    if any(
        entry.get("maxBlood") is None
        or entry.get("maxBlood", 0.0) < 0.0
        or entry.get("maxBlood", 0.0) > 1.0
        or entry.get("globalQualityWeight") is None
        or entry.get("globalQualityWeight", 0.0) < 0.0
        or entry.get("globalQualityWeight", 0.0) > 1.0
        or entry.get("solverMicros") is None
        or entry.get("solverMicros", 0.0) < 0.0
        for entry in entries
    ):
        warnings.append("chemical_value_out_of_range")

    return {
        "type": "chemical_influence_blackbox",
        "magic": magic,
        "magicHex": f"0x{magic:016X}",
        "version": version,
        "headerBytes": CHEMICAL_INFLUENCE_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": capacity,
        "capacity": capacity,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_sargassum_food_chain_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPSARGASSUMFOODCHAINBIN", "DUMPSARGASSUMFOODCHAINH8DUMP"}


def has_sargassum_food_chain_signature(data: bytes) -> bool:
    if len(data) < SARGASSUM_FOOD_CHAIN_HEADER_BYTES:
        return False
    magic_low, magic_high, *_ = SARGASSUM_FOOD_CHAIN_HEADER.unpack_from(data, 0)
    return (
        magic_low == SARGASSUM_FOOD_CHAIN_MAGIC_LOW
        and magic_high == SARGASSUM_FOOD_CHAIN_MAGIC_HIGH
    )


def parse_sargassum_food_chain_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < SARGASSUM_FOOD_CHAIN_HEADER_BYTES:
        return {
            "type": "sargassum_food_chain_blackbox",
            "headerBytes": SARGASSUM_FOOD_CHAIN_HEADER_BYTES,
            "entrySize": SARGASSUM_FOOD_CHAIN_ENTRY_BYTES,
            "declaredEntryCount": 0,
            "capacity": 0,
            "telemetryCursor": 0,
            "nonEmptyEntryCount": 0,
            "returnedEntryCount": 0,
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic_low, magic_high, entry_size, capacity, cursor, anomaly_hash = SARGASSUM_FOOD_CHAIN_HEADER.unpack_from(data, 0)
    invalid_header = (
        magic_low != SARGASSUM_FOOD_CHAIN_MAGIC_LOW
        or magic_high != SARGASSUM_FOOD_CHAIN_MAGIC_HIGH
        or entry_size != SARGASSUM_FOOD_CHAIN_ENTRY_BYTES
        or capacity <= 0
        or capacity > SARGASSUM_FOOD_CHAIN_CAPACITY
        or cursor < 0
        or cursor > capacity
        or SARGASSUM_FOOD_CHAIN_HEADER.size != SARGASSUM_FOOD_CHAIN_HEADER_BYTES
        or SARGASSUM_FOOD_CHAIN_ENTRY.size != SARGASSUM_FOOD_CHAIN_ENTRY_BYTES
    )
    if invalid_header:
        return {
            "type": "sargassum_food_chain_blackbox",
            "magicLow": magic_low,
            "magicLowHex": f"0x{magic_low:08X}",
            "magicHigh": magic_high,
            "magicHighHex": f"0x{magic_high:08X}",
            "headerBytes": SARGASSUM_FOOD_CHAIN_HEADER_BYTES,
            "entrySize": entry_size,
            "declaredEntryCount": capacity,
            "capacity": capacity,
            "telemetryCursor": cursor,
            "anomalyHash": anomaly_hash,
            "anomalyHashHex": f"0x{anomaly_hash:08X}",
            "nonEmptyEntryCount": 0,
            "returnedEntryCount": 0,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = SARGASSUM_FOOD_CHAIN_HEADER_BYTES
    expected_bytes = payload_offset + capacity * entry_size
    readable_entries = min(capacity, max(0, len(data) - payload_offset) // entry_size)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    def vector3(values: tuple[float, float, float]) -> dict[str, float | None]:
        return {
            "x": finite_round(values[0], 3),
            "y": finite_round(values[1], 3),
            "z": finite_round(values[2], 3),
        }

    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = SARGASSUM_FOOD_CHAIN_ENTRY.unpack_from(data, offset)
        flags = fields[3]
        flag_labels, unknown_flags = resolve_bit_labels(flags, SARGASSUM_FOOD_CHAIN_FLAG_LABELS)
        float_values = fields[8:14] + (fields[15],)
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "stateHash": fields[1],
                "stateHashHex": f"0x{fields[1]:08X}",
                "sourceHash": fields[2],
                "sourceHashHex": f"0x{fields[2]:08X}",
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "activeBoidCount": fields[4],
                "consumedBoidCount": fields[5],
                "pendingKillJob": fields[6],
                "lodTier": fields[7],
                "fieldCenterWS": vector3(fields[8:11]),
                "eventPositionWS": vector3(fields[11:14]),
                "entryAnomalyHash": fields[14],
                "entryAnomalyHashHex": f"0x{fields[14]:08X}",
                "simulationTime": finite_round(fields[15], 3),
                "tick": bool(flags & (1 << 0)),
                "killJobScheduled": bool(flags & (1 << 1)),
                "killJobCompleted": bool(flags & (1 << 2)),
                "killDrained": bool(flags & (1 << 3)),
                "whaleFall": bool(flags & (1 << 4)),
                "boidsScattered": bool(flags & (1 << 5)),
                "nonFinite": bool(flags & (1 << 31)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if capacity != SARGASSUM_FOOD_CHAIN_CAPACITY:
        warnings.append("capacity_mismatch")
    if cursor == capacity:
        warnings.append("cursor_at_capacity")
    if anomaly_hash != 0:
        warnings.append("anomaly_hash")
    if any(entry.get("entryAnomalyHash") for entry in entries):
        warnings.append("entry_anomaly_hash")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("nonFinite") for entry in entries):
        warnings.append("nonfinite_flag")
    if any(entry.get("stateHash") == 0 for entry in entries):
        warnings.append("state_hash_zero")
    if any(entry.get("activeBoidCount", 0) < 0 for entry in entries):
        warnings.append("active_count_out_of_range")
    if any(
        entry.get("consumedBoidCount", 0) < 0
        or (
            entry.get("activeBoidCount", 0) >= 0
            and entry.get("consumedBoidCount", 0) > entry.get("activeBoidCount", 0)
        )
        for entry in entries
    ):
        warnings.append("consumed_count_out_of_range")
    if any(
        entry.get("pendingKillJob", 0) < 0
        or entry.get("pendingKillJob", 0) > SARGASSUM_FOOD_CHAIN_MAX_PENDING_KILL_SIGNALS
        for entry in entries
    ):
        warnings.append("pending_kill_job_out_of_range")
    if any(
        entry.get("lodTier", 0) < 0
        or entry.get("lodTier", 0) > SARGASSUM_FOOD_CHAIN_MAX_LOD_TIER
        for entry in entries
    ):
        warnings.append("lod_tier_out_of_range")
    if any(entry.get("simulationTime") is None or entry.get("simulationTime", 0.0) < 0.0 for entry in entries):
        warnings.append("simulation_time_out_of_range")
    if any(entry.get("killJobScheduled") for entry in entries):
        warnings.append("kill_job_scheduled")
    if any(entry.get("killJobCompleted") for entry in entries):
        warnings.append("kill_job_completed")
    if any(entry.get("killDrained") for entry in entries):
        warnings.append("kill_drained")
    if any(entry.get("whaleFall") for entry in entries):
        warnings.append("whale_fall")
    if any(entry.get("boidsScattered") for entry in entries):
        warnings.append("boids_scattered")
    if any(entry.get("nonFinite") for entry in entries) and anomaly_hash == 0:
        warnings.append("nonfinite_without_anomaly_hash")

    return {
        "type": "sargassum_food_chain_blackbox",
        "magicLow": magic_low,
        "magicLowHex": f"0x{magic_low:08X}",
        "magicHigh": magic_high,
        "magicHighHex": f"0x{magic_high:08X}",
        "headerBytes": SARGASSUM_FOOD_CHAIN_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": capacity,
        "capacity": capacity,
        "telemetryCursor": cursor,
        "anomalyHash": anomaly_hash,
        "anomalyHashHex": f"0x{anomaly_hash:08X}",
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_sargassum_boid_sensory_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPSARGASSUMBOIDSENSORYBIN", "DUMPSARGASSUMBOIDSENSORYH8DUMP"}


def has_sargassum_boid_sensory_signature(data: bytes) -> bool:
    if len(data) < SARGASSUM_BOID_SENSORY_HEADER_BYTES:
        return False
    magic_low, magic_high, *_ = SARGASSUM_BOID_SENSORY_HEADER.unpack_from(data, 0)
    return (
        magic_low == SARGASSUM_BOID_SENSORY_MAGIC_LOW
        and magic_high == SARGASSUM_BOID_SENSORY_MAGIC_HIGH
    )


def parse_sargassum_boid_sensory_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < SARGASSUM_BOID_SENSORY_HEADER_BYTES:
        return {
            "type": "sargassum_boid_sensory_blackbox",
            "headerBytes": SARGASSUM_BOID_SENSORY_HEADER_BYTES,
            "entrySize": SARGASSUM_BOID_SENSORY_ENTRY_BYTES,
            "declaredEntryCount": 0,
            "capacity": 0,
            "telemetryCursor": 0,
            "nonEmptyEntryCount": 0,
            "returnedEntryCount": 0,
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic_low, magic_high, entry_size, capacity, cursor, anomaly_hash = SARGASSUM_BOID_SENSORY_HEADER.unpack_from(data, 0)
    invalid_header = (
        magic_low != SARGASSUM_BOID_SENSORY_MAGIC_LOW
        or magic_high != SARGASSUM_BOID_SENSORY_MAGIC_HIGH
        or entry_size != SARGASSUM_BOID_SENSORY_ENTRY_BYTES
        or capacity <= 0
        or capacity > SARGASSUM_BOID_SENSORY_CAPACITY
        or cursor < 0
        or cursor > capacity
        or SARGASSUM_BOID_SENSORY_HEADER.size != SARGASSUM_BOID_SENSORY_HEADER_BYTES
        or SARGASSUM_BOID_SENSORY_ENTRY.size != SARGASSUM_BOID_SENSORY_ENTRY_BYTES
    )
    if invalid_header:
        return {
            "type": "sargassum_boid_sensory_blackbox",
            "magicLow": magic_low,
            "magicLowHex": f"0x{magic_low:08X}",
            "magicHigh": magic_high,
            "magicHighHex": f"0x{magic_high:08X}",
            "headerBytes": SARGASSUM_BOID_SENSORY_HEADER_BYTES,
            "entrySize": entry_size,
            "declaredEntryCount": capacity,
            "capacity": capacity,
            "telemetryCursor": cursor,
            "anomalyHash": anomaly_hash,
            "anomalyHashHex": f"0x{anomaly_hash:08X}",
            "nonEmptyEntryCount": 0,
            "returnedEntryCount": 0,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = SARGASSUM_BOID_SENSORY_HEADER_BYTES
    expected_bytes = payload_offset + capacity * entry_size
    readable_entries = min(capacity, max(0, len(data) - payload_offset) // entry_size)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    def threat_vector(values: tuple[float, float, float, float]) -> dict[str, float | None]:
        return {
            "x": finite_round(values[0], 3),
            "y": finite_round(values[1], 3),
            "z": finite_round(values[2], 3),
            "radius": finite_round(values[3], 3),
        }

    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = SARGASSUM_BOID_SENSORY_ENTRY.unpack_from(data, offset)
        flags = fields[2]
        flag_labels, unknown_flags = resolve_bit_labels(flags, SARGASSUM_BOID_SENSORY_FLAG_LABELS)
        float_values = fields[4:16]
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        submarine = fields[4:8]
        flashlight = fields[8:12]
        acoustic = fields[12:16]
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "stateHash": fields[1],
                "stateHashHex": f"0x{fields[1]:08X}",
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "activeThreatCount": fields[3],
                "submarineThreat": threat_vector(submarine),
                "flashlightThreat": threat_vector(flashlight),
                "acousticPingRadii": {
                    "a": finite_round(acoustic[0], 3),
                    "b": finite_round(acoustic[1], 3),
                    "c": finite_round(acoustic[2], 3),
                    "lodTier": finite_round(acoustic[3], 3),
                },
                "tick": bool(flags & (1 << 0)),
                "lightActive": bool(flags & (1 << 1)),
                "pingActive": bool(flags & (1 << 2)),
                "capsule": bool(flags & (1 << 3)),
                "nonFinite": bool(flags & (1 << 31)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if capacity != SARGASSUM_BOID_SENSORY_CAPACITY:
        warnings.append("capacity_mismatch")
    if cursor == capacity:
        warnings.append("cursor_at_capacity")
    if anomaly_hash != 0:
        warnings.append("anomaly_hash")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("nonFinite") for entry in entries):
        warnings.append("nonfinite_flag")
    if any(entry.get("stateHash") == 0 for entry in entries):
        warnings.append("state_hash_zero")
    if any(
        entry.get("activeThreatCount", 0) < 0
        or entry.get("activeThreatCount", 0) > SARGASSUM_BOID_SENSORY_MAX_THREATS
        for entry in entries
    ):
        warnings.append("active_threat_count_out_of_range")

    def radius_value(entry: dict[str, Any], group: str, key: str = "radius") -> float | None:
        value = entry.get(group)
        if not isinstance(value, dict):
            return None
        radius = value.get(key)
        return radius if isinstance(radius, (int, float)) else None

    if any(
        _radius is None
        or _radius < 0.0
        or _radius > SARGASSUM_BOID_SENSORY_MAX_RADIUS_METERS
        for entry in entries
        for _radius in (
            radius_value(entry, "submarineThreat"),
            radius_value(entry, "flashlightThreat"),
            radius_value(entry, "acousticPingRadii", "a"),
            radius_value(entry, "acousticPingRadii", "b"),
            radius_value(entry, "acousticPingRadii", "c"),
        )
    ):
        warnings.append("threat_radius_out_of_range")
    if any(
        bool(entry.get("lightActive"))
        != ((radius_value(entry, "flashlightThreat") or 0.0) >= SARGASSUM_BOID_SENSORY_MIN_RADIUS_METERS)
        for entry in entries
    ):
        warnings.append("light_flag_mismatch")
    if any(
        bool(entry.get("pingActive"))
        != any(
            (value or 0.0) >= SARGASSUM_BOID_SENSORY_MIN_RADIUS_METERS
            for value in (
                radius_value(entry, "acousticPingRadii", "a"),
                radius_value(entry, "acousticPingRadii", "b"),
                radius_value(entry, "acousticPingRadii", "c"),
            )
        )
        for entry in entries
    ):
        warnings.append("ping_flag_mismatch")
    if any(entry.get("nonFinite") for entry in entries) and anomaly_hash == 0:
        warnings.append("nonfinite_without_anomaly_hash")

    return {
        "type": "sargassum_boid_sensory_blackbox",
        "magicLow": magic_low,
        "magicLowHex": f"0x{magic_low:08X}",
        "magicHigh": magic_high,
        "magicHighHex": f"0x{magic_high:08X}",
        "headerBytes": SARGASSUM_BOID_SENSORY_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": capacity,
        "capacity": capacity,
        "telemetryCursor": cursor,
        "anomalyHash": anomaly_hash,
        "anomalyHashHex": f"0x{anomaly_hash:08X}",
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_marine_snow_vfx_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPSILTVFXH8DUMP", "DUMPSILTVFXBIN"}


def has_marine_snow_vfx_blackbox_signature(data: bytes) -> bool:
    if len(data) < MARINE_SNOW_VFX_HEADER_BYTES:
        return False
    context_hash, capacity, entry_size, _written_count = MARINE_SNOW_VFX_HEADER.unpack_from(data, 0)
    return (
        context_hash == MARINE_SNOW_VFX_CONTEXT_HASH
        and capacity == MARINE_SNOW_VFX_TELEMETRY_CAPACITY
        and entry_size == MARINE_SNOW_VFX_ENTRY_BYTES
        and MARINE_SNOW_VFX_ENTRY.size == MARINE_SNOW_VFX_ENTRY_BYTES
    )


def parse_marine_snow_vfx_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < MARINE_SNOW_VFX_HEADER_BYTES:
        return {
            "type": "marine_snow_vfx_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    context_hash, capacity, entry_size, written_count = MARINE_SNOW_VFX_HEADER.unpack_from(data, 0)
    invalid_header = (
        context_hash != MARINE_SNOW_VFX_CONTEXT_HASH
        or capacity != MARINE_SNOW_VFX_TELEMETRY_CAPACITY
        or entry_size != MARINE_SNOW_VFX_ENTRY_BYTES
        or MARINE_SNOW_VFX_ENTRY.size != MARINE_SNOW_VFX_ENTRY_BYTES
    )
    if invalid_header:
        return {
            "type": "marine_snow_vfx_blackbox",
            "contextHash": context_hash,
            "contextHashHex": f"0x{context_hash:08X}",
            "headerBytes": MARINE_SNOW_VFX_HEADER_BYTES,
            "entrySize": entry_size,
            "capacity": capacity,
            "writtenCount": written_count,
            "declaredEntryCount": 0,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = MARINE_SNOW_VFX_HEADER_BYTES
    declared_entries = min(written_count, capacity)
    expected_bytes = payload_offset + declared_entries * entry_size
    readable_entries = min(declared_entries, max(0, len(data) - payload_offset) // entry_size)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = MARINE_SNOW_VFX_ENTRY.unpack_from(data, offset)
        flags = fields[12]
        flag_labels, unknown_flags = resolve_bit_labels(flags, MARINE_SNOW_VFX_FLAG_LABELS)
        float_values = fields[4:12]
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "dispatchedParticleCount": fields[1],
                "capacity": fields[2],
                "dynamicWakeCount": fields[3],
                "throttle": finite_round(fields[4]),
                "systemStress01": finite_round(fields[5]),
                "maxSiltSpeed": finite_round(fields[6]),
                "aupShiftSq": finite_round(fields[7]),
                "cameraPositionWS": {
                    "x": finite_round(fields[8]),
                    "y": finite_round(fields[9]),
                    "z": finite_round(fields[10]),
                },
                "headlightBoost": finite_round(fields[11]),
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "stateHash": fields[13],
                "stateHashHex": f"0x{fields[13]:08X}",
                "mockGpuMicroseconds": fields[14],
                "commandSequence": fields[15],
                "nonFinite": bool(flags & (1 << 0)),
                "gpuBudgetExceeded": bool(flags & (1 << 1)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("nonFinite") for entry in entries):
        warnings.append("nonfinite_flag")
    if any(entry.get("gpuBudgetExceeded") for entry in entries):
        warnings.append("gpu_budget_exceeded")
    if any(entry.get("mockGpuMicroseconds", 0) > MARINE_SNOW_VFX_GPU_DUMP_THRESHOLD_US for entry in entries):
        warnings.append("gpu_over_1500us")
    if any(entry.get("mockGpuMicroseconds", 0) < 0 for entry in entries):
        warnings.append("gpu_time_out_of_range")
    if any(
        entry.get("dispatchedParticleCount", 0) < 0
        or entry.get("capacity", 0) < MARINE_SNOW_VFX_MIN_PARTICLE_CAPACITY
        or entry.get("capacity", 0) > MARINE_SNOW_VFX_MAX_PARTICLE_CAPACITY
        or entry.get("dispatchedParticleCount", 0) > entry.get("capacity", 0)
        for entry in entries
    ):
        warnings.append("particle_count_out_of_range")
    if any(entry.get("dynamicWakeCount", 0) < 0 or entry.get("dynamicWakeCount", 0) > MARINE_SNOW_VFX_DYNAMIC_WAKE_CAPACITY for entry in entries):
        warnings.append("dynamic_wake_count_out_of_range")
    if any(
        entry.get("throttle") is None
        or entry.get("systemStress01") is None
        or entry.get("throttle", 0.0) < 0.0
        or entry.get("throttle", 0.0) > 1.0
        or entry.get("systemStress01", 0.0) < 0.0
        or entry.get("systemStress01", 0.0) > 1.0
        for entry in entries
    ):
        warnings.append("throttle_or_stress_out_of_range")
    if any(entry.get("maxSiltSpeed") is None or entry.get("maxSiltSpeed", 0.0) < 0.0 for entry in entries):
        warnings.append("max_silt_speed_out_of_range")
    if any(entry.get("aupShiftSq") is None or entry.get("aupShiftSq", 0.0) < 0.0 for entry in entries):
        warnings.append("aup_shift_out_of_range")
    if any(entry.get("headlightBoost") is None or entry.get("headlightBoost", 0.0) < 0.0 for entry in entries):
        warnings.append("headlight_boost_out_of_range")
    if any(entry.get("stateHash") == 0 for entry in entries):
        warnings.append("state_hash_zero")

    return {
        "type": "marine_snow_vfx_blackbox",
        "contextHash": context_hash,
        "contextHashHex": f"0x{context_hash:08X}",
        "headerBytes": MARINE_SNOW_VFX_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": declared_entries,
        "writtenCount": written_count,
        "capacity": capacity,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_propwash_gpu_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPPROPWASHGPUH8DUMP", "DUMPPROPWASHGPUBIN"}


def has_propwash_gpu_blackbox_signature(data: bytes) -> bool:
    if len(data) < PROPWASH_GPU_HEADER_BYTES:
        return False
    layout_hash, capacity, entry_size, _written_count = PROPWASH_GPU_HEADER.unpack_from(data, 0)
    return (
        layout_hash == PROPWASH_GPU_LAYOUT_HASH
        and capacity == PROPWASH_GPU_TELEMETRY_CAPACITY
        and entry_size == PROPWASH_GPU_ENTRY_BYTES
        and PROPWASH_GPU_ENTRY.size == PROPWASH_GPU_ENTRY_BYTES
    )


def parse_propwash_gpu_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < PROPWASH_GPU_HEADER_BYTES:
        return {
            "type": "propwash_gpu_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    layout_hash, capacity, entry_size, written_count = PROPWASH_GPU_HEADER.unpack_from(data, 0)
    invalid_header = (
        layout_hash != PROPWASH_GPU_LAYOUT_HASH
        or capacity != PROPWASH_GPU_TELEMETRY_CAPACITY
        or entry_size != PROPWASH_GPU_ENTRY_BYTES
        or PROPWASH_GPU_ENTRY.size != PROPWASH_GPU_ENTRY_BYTES
    )
    if invalid_header:
        return {
            "type": "propwash_gpu_blackbox",
            "layoutHash": layout_hash,
            "layoutHashHex": f"0x{layout_hash:08X}",
            "headerBytes": PROPWASH_GPU_HEADER_BYTES,
            "entrySize": entry_size,
            "capacity": capacity,
            "writtenCount": written_count,
            "declaredEntryCount": 0,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = PROPWASH_GPU_HEADER_BYTES
    declared_entries = min(written_count, capacity)
    expected_bytes = payload_offset + declared_entries * entry_size
    readable_entries = min(declared_entries, max(0, len(data) - payload_offset) // entry_size)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = PROPWASH_GPU_ENTRY.unpack_from(data, offset)
        flags = fields[12]
        flag_labels, unknown_flags = resolve_bit_labels(flags, PROPWASH_GPU_FLAG_LABELS)
        float_values = fields[4:11]
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "eventCount": fields[1],
                "particleBudgetLimit": fields[2],
                "overflowCount": fields[3],
                "globalQualityWeight": finite_round(fields[4]),
                "maxIntensity": finite_round(fields[5]),
                "estimatedGpuMicroseconds": finite_round(fields[6], 2),
                "sdfProximityMeters": finite_round(fields[7], 4),
                "strongestLocalPosition": {
                    "x": finite_round(fields[8], 4),
                    "y": finite_round(fields[9], 4),
                    "z": finite_round(fields[10], 4),
                },
                "stateHash": fields[11],
                "stateHashHex": f"0x{fields[11]:08X}",
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "cursor": fields[13],
                "profileHash": fields[14],
                "profileHashHex": f"0x{fields[14]:08X}",
                "pad0": fields[15],
                "mockSource": bool(flags & (1 << 0)),
                "vehicleWakeSource": bool(flags & (1 << 1)),
                "wakeSourceBridge": bool(flags & (1 << 2)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if written_count > capacity:
        warnings.append("ring_wrapped")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("mockSource") for entry in entries):
        warnings.append("mock_source")
    if any(entry.get("overflowCount", 0) > 0 for entry in entries):
        warnings.append("overflow_count")
    if any(entry.get("eventCount", 0) < 0 or entry.get("eventCount", 0) > PROPWASH_GPU_EVENT_RING_CAPACITY for entry in entries):
        warnings.append("event_count_out_of_range")
    if any(
        entry.get("particleBudgetLimit", 0) < PROPWASH_GPU_MIN_PARTICLE_BUDGET
        or entry.get("particleBudgetLimit", 0) > PROPWASH_GPU_MAX_PARTICLE_BUDGET
        for entry in entries
    ):
        warnings.append("particle_budget_out_of_range")
    if any(
        entry.get("globalQualityWeight") is None
        or entry.get("globalQualityWeight", 0.0) < 0.0
        or entry.get("globalQualityWeight", 0.0) > 1.0
        for entry in entries
    ):
        warnings.append("quality_out_of_range")
    if any(entry.get("maxIntensity") is None or entry.get("maxIntensity", 0.0) < 0.0 for entry in entries):
        warnings.append("max_intensity_out_of_range")
    if any(entry.get("estimatedGpuMicroseconds") is None or entry.get("estimatedGpuMicroseconds", 0.0) < 0.0 for entry in entries):
        warnings.append("gpu_time_out_of_range")
    if any(entry.get("estimatedGpuMicroseconds", 0.0) > PROPWASH_GPU_ESTIMATED_BUDGET_WARNING_US for entry in entries):
        warnings.append("gpu_over_1000us")
    if any(entry.get("sdfProximityMeters") is None or entry.get("sdfProximityMeters", 0.0) < 0.0 for entry in entries):
        warnings.append("sdf_out_of_range")
    if any(entry.get("cursor", 0) > PROPWASH_GPU_EVENT_RING_CAPACITY for entry in entries):
        warnings.append("cursor_out_of_range")
    if any(entry.get("stateHash") == 0 for entry in entries):
        warnings.append("state_hash_zero")
    if any(entry.get("pad0") != 0 for entry in entries):
        warnings.append("pad_nonzero")
    if any(entry.get("eventCount", 0) > 0 and entry.get("flags", 0) == 0 for entry in entries):
        warnings.append("missing_source_flags")

    return {
        "type": "propwash_gpu_blackbox",
        "layoutHash": layout_hash,
        "layoutHashHex": f"0x{layout_hash:08X}",
        "headerBytes": PROPWASH_GPU_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": declared_entries,
        "writtenCount": written_count,
        "capacity": capacity,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_carve_debris_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMPSHINOBU05DEBRISPHYSICSFAKEH8DUMP",
        "DUMPSHINOBU05DEBRISPHYSICSFAKEBIN",
    }


def parse_carve_debris_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < CARVE_DEBRIS_HEADER_BYTES:
        return {
            "type": "carve_debris_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, capacity, entry_size, cursor, reason_flags = CARVE_DEBRIS_HEADER.unpack_from(data, 0)
    invalid_header = (
        magic != CARVE_DEBRIS_MAGIC
        or capacity != CARVE_DEBRIS_BLACKBOX_CAPACITY
        or entry_size != CARVE_DEBRIS_ENTRY_BYTES
        or cursor >= max(1, capacity)
        or CARVE_DEBRIS_ENTRY.size != CARVE_DEBRIS_ENTRY_BYTES
    )
    reason_labels, unknown_reason_flags = resolve_bit_labels(reason_flags, CARVE_DEBRIS_FLAG_LABELS)
    if invalid_header:
        return {
            "type": "carve_debris_blackbox",
            "magic": magic,
            "headerBytes": CARVE_DEBRIS_HEADER_BYTES,
            "entrySize": entry_size,
            "capacity": capacity,
            "telemetryCursor": cursor,
            "reasonFlags": reason_flags,
            "reasonFlagLabels": reason_labels,
            "unknownReasonFlags": unknown_reason_flags,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = CARVE_DEBRIS_HEADER_BYTES
    expected_bytes = payload_offset + capacity * entry_size
    readable_entries = min(capacity, max(0, len(data) - payload_offset) // entry_size)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = CARVE_DEBRIS_ENTRY.unpack_from(data, offset)
        flags = fields[4]
        flag_labels, unknown_flags = resolve_bit_labels(flags, CARVE_DEBRIS_FLAG_LABELS)
        aup_shift = fields[6:9]
        if any(not math.isfinite(value) for value in aup_shift):
            nonfinite_seen = True
        quality_pad = fields[9]
        quality_pressure_q8 = quality_pad & 0xFF
        pad0_high = quality_pad & 0xFFFFFF00
        pad_values = (pad0_high,) + fields[10:16]
        entries.append(
            {
                "slot": index,
                "ringSlot": (cursor + index) % capacity,
                "frame": fields[0],
                "activeCarveDebrisCount": fields[1],
                "queuedCarves": fields[2],
                "injectedParticles": fields[3],
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "stateHash": fields[5],
                "stateHashHex": f"0x{fields[5]:08X}",
                "appliedAupShift": {
                    "x": finite_round(fields[6]),
                    "y": finite_round(fields[7]),
                    "z": finite_round(fields[8]),
                },
                "qualityPressureQ8": quality_pressure_q8,
                "qualityPressure01": round(quality_pressure_q8 / 255.0, 4),
                "pad0High": pad0_high,
                "padValues": list(pad_values),
                "invalidState": bool(flags & (1 << 0)),
                "sdfActive": bool(flags & (1 << 2)),
                "flowActive": bool(flags & (1 << 3)),
                "stressRecycle": bool(flags & (1 << 4)),
                "wakeActive": bool(flags & (1 << 5)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if reason_flags != 0:
        warnings.append("reason_flags")
    if unknown_reason_flags:
        warnings.append("unknown_reason_flags")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("invalidState") for entry in entries) or (reason_flags & (1 << 0)) != 0:
        warnings.append("invalid_state")
    if any(entry.get("stressRecycle") for entry in entries) or (reason_flags & (1 << 4)) != 0:
        warnings.append("stress_recycle")
    if any(entry.get("activeCarveDebrisCount", 0) < 0 or entry.get("activeCarveDebrisCount", 0) > CARVE_DEBRIS_MAX_ACTIVE_CAPACITY for entry in entries):
        warnings.append("active_count_out_of_range")
    if any(entry.get("queuedCarves", 0) < 0 or entry.get("queuedCarves", 0) > CARVE_DEBRIS_MAX_CARVE_SIGNALS_PER_FRAME for entry in entries):
        warnings.append("queued_carves_out_of_range")
    if any(entry.get("injectedParticles", 0) < 0 or entry.get("injectedParticles", 0) > CARVE_DEBRIS_MAX_ACTIVE_CAPACITY for entry in entries):
        warnings.append("injected_particles_out_of_range")
    if any(entry.get("stateHash") == 0 for entry in entries):
        warnings.append("state_hash_zero")
    if any(any(value != 0 for value in entry.get("padValues", [])) for entry in entries):
        warnings.append("pad_nonzero")

    return {
        "type": "carve_debris_blackbox",
        "magic": magic,
        "headerBytes": CARVE_DEBRIS_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": capacity,
        "capacity": capacity,
        "telemetryCursor": cursor,
        "reasonFlags": reason_flags,
        "reasonFlagLabels": reason_labels,
        "unknownReasonFlags": unknown_reason_flags,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_biolum_pulse_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMPSHINOBU238BIN",
        "DUMPSHINOBU238H8DUMP",
    }


def parse_biolum_pulse_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < BIOLUM_PULSE_HEADER_BYTES:
        return {
            "type": "biolum_pulse_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, reason, reserved, entry_size, write_cursor, entry_count = BIOLUM_PULSE_HEADER.unpack_from(data, 0)
    reason_labels, unknown_reason_flags = resolve_bit_labels(reason, BIOLUM_PULSE_FLAG_LABELS)
    invalid_header = (
        magic != BIOLUM_PULSE_MAGIC
        or entry_size != BIOLUM_PULSE_ENTRY_BYTES
        or entry_count <= 0
        or entry_count > BIOLUM_PULSE_BLACKBOX_CAPACITY
        or write_cursor < 0
        or write_cursor >= BIOLUM_PULSE_BLACKBOX_CAPACITY
        or BIOLUM_PULSE_ENTRY.size != BIOLUM_PULSE_ENTRY_BYTES
    )
    if invalid_header:
        return {
            "type": "biolum_pulse_blackbox",
            "magic": magic,
            "headerBytes": BIOLUM_PULSE_HEADER_BYTES,
            "entrySize": entry_size,
            "telemetryCursor": write_cursor,
            "declaredEntryCount": entry_count,
            "reason": reason,
            "reasonFlagLabels": reason_labels,
            "unknownReasonFlags": unknown_reason_flags,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = BIOLUM_PULSE_HEADER_BYTES
    expected_bytes = payload_offset + entry_count * entry_size
    readable_entries = min(entry_count, max(0, len(data) - payload_offset) // entry_size)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = BIOLUM_PULSE_ENTRY.unpack_from(data, offset)
        flags = fields[9]
        flag_labels, unknown_flags = resolve_bit_labels(flags, BIOLUM_PULSE_FLAG_LABELS)
        float_values = fields[2:7]
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        pad_bytes = fields[10]
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "activeGlowingInstances": fields[1],
                "oscillatorComputeTimeMs": finite_round(fields[2], 5),
                "globalDarknessScalar": finite_round(fields[3]),
                "group0Phase": finite_round(fields[4]),
                "frequencyMultiplier": finite_round(fields[5]),
                "primaryAmplitudeHdr": finite_round(fields[6]),
                "wavePulsesActive": fields[7],
                "qualityTier": fields[8],
                "qualityWeight01": round(fields[8] / 255.0, 4),
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "padNonzero": any(value != 0 for value in pad_bytes),
                "nonFinite": bool(flags & (1 << 0)),
                "jobOverrun": bool(flags & (1 << 1)),
                "aupInvalid": bool(flags & (1 << 2)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if reserved != 0:
        warnings.append("reserved_nonzero")
    if reason != 0:
        warnings.append("reason_flags")
    if unknown_reason_flags:
        warnings.append("unknown_reason_flags")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("nonFinite") for entry in entries) or (reason & (1 << 0)) != 0:
        warnings.append("nonfinite_flag")
    if any(entry.get("jobOverrun") for entry in entries) or (reason & (1 << 1)) != 0:
        warnings.append("job_overrun")
    if any(entry.get("aupInvalid") for entry in entries) or (reason & (1 << 2)) != 0:
        warnings.append("aup_invalid")
    if any(entry.get("oscillatorComputeTimeMs", 0.0) > BIOLUM_PULSE_OSCILLATOR_WARNING_MS for entry in entries):
        warnings.append("oscillator_over_0_1ms")
    if any(
        entry.get("activeGlowingInstances", 0) > BIOLUM_PULSE_MAX_GLOW_INSTANCES
        for entry in entries
    ):
        warnings.append("active_instances_out_of_range")
    if any(entry.get("wavePulsesActive", 0) > BIOLUM_PULSE_SYNC_PULSE_CAPACITY for entry in entries):
        warnings.append("wave_pulses_out_of_range")
    if any(
        entry.get("globalDarknessScalar") is None
        or entry.get("frequencyMultiplier") is None
        or entry.get("primaryAmplitudeHdr") is None
        or entry.get("globalDarknessScalar", 0.0) < 0.0
        or entry.get("globalDarknessScalar", 0.0) > 1.0
        or entry.get("frequencyMultiplier", 0.0) < 0.0
        or entry.get("frequencyMultiplier", 0.0) > 8.0
        or entry.get("primaryAmplitudeHdr", 0.0) < 0.0
        or entry.get("primaryAmplitudeHdr", 0.0) > BIOLUM_PULSE_MAX_HDR_INTENSITY
        for entry in entries
    ):
        warnings.append("pulse_value_out_of_range")
    if any(entry.get("padNonzero") for entry in entries):
        warnings.append("pad_nonzero")

    return {
        "type": "biolum_pulse_blackbox",
        "magic": magic,
        "headerBytes": BIOLUM_PULSE_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": entry_count,
        "capacity": BIOLUM_PULSE_BLACKBOX_CAPACITY,
        "telemetryCursor": write_cursor,
        "reason": reason,
        "reasonFlagLabels": reason_labels,
        "unknownReasonFlags": unknown_reason_flags,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def parse_global_telemetry_frame(
    data: bytes,
    frame_offset: int,
    frame_stride: int,
    metadata: list[int],
    descriptors: list[dict[str, Any]],
    index: int,
) -> dict[str, Any]:
    timestamp, frame_number, fatal_hash = GLOBAL_TELEMETRY_PREFIX.unpack_from(data, frame_offset)
    hash_history_offset = metadata[10]
    source_payload_offset = metadata[11]
    mock_physics_offset = metadata[12]
    mock_origin_offset = metadata[13]
    hash_capacity_bytes = max(0, source_payload_offset - hash_history_offset)
    hash_count = min(GLOBAL_TELEMETRY_BUS_HASH_HISTORY_COUNT, hash_capacity_bytes // 4)
    event_hashes = []
    for i in range(hash_count):
        offset = frame_offset + hash_history_offset + i * 4
        if offset + 4 > frame_offset + frame_stride:
            break

        event_hash = struct.unpack_from("<I", data, offset)[0]
        if event_hash != 0:
            event_hashes.append(event_hash)

    source_hashes = []
    decoded_sources = []
    decoded_by_name = {}
    source_non_zero_count = 0
    for descriptor in descriptors:
        slot = safe_int(descriptor.get("slot"), 0)
        payload_bytes = min(
            max(0, safe_int(descriptor.get("payloadBytes"), 0)),
            GLOBAL_TELEMETRY_BUS_SOURCE_STRIDE_BYTES,
        )
        payload_offset = frame_offset + source_payload_offset + slot * GLOBAL_TELEMETRY_BUS_SOURCE_STRIDE_BYTES
        if (
            payload_bytes <= 0
            or slot < 0
            or payload_offset < frame_offset
            or payload_offset + payload_bytes > frame_offset + frame_stride
        ):
            continue

        if not is_empty_entry(data, payload_offset, payload_bytes):
            source_non_zero_count += 1
            source_hashes.append(descriptor.get("sourceHash"))
            decoded = parse_global_telemetry_known_source(data, payload_offset, payload_bytes, descriptor)
            if decoded is not None:
                decoded_sources.append(decoded)
                source_name = decoded.get("sourceName")
                if isinstance(source_name, str) and source_name:
                    decoded_by_name[source_name] = decoded

    mock_physics_active = (
        mock_physics_offset + GLOBAL_TELEMETRY_BUS_SOURCE_STRIDE_BYTES <= frame_stride
        and not is_empty_entry(data, frame_offset + mock_physics_offset, GLOBAL_TELEMETRY_BUS_SOURCE_STRIDE_BYTES)
    )
    mock_origin_active = (
        mock_origin_offset + GLOBAL_TELEMETRY_BUS_SOURCE_STRIDE_BYTES <= frame_stride
        and not is_empty_entry(data, frame_offset + mock_origin_offset, GLOBAL_TELEMETRY_BUS_SOURCE_STRIDE_BYTES)
    )
    return {
        "slot": index,
        "timestamp": timestamp,
        "frame": frame_number,
        "fatalHash": fatal_hash,
        "eventHashCount": len(event_hashes),
        "eventHashesTail": event_hashes[-12:],
        "sourceNonZeroCount": source_non_zero_count,
        "sourceHashes": source_hashes[:16],
        "decodedSourceCount": len(decoded_sources),
        "decodedSources": decoded_sources[:16],
        "survival": decoded_by_name.get("survival"),
        "mockPhysicsActive": mock_physics_active,
        "mockOriginActive": mock_origin_active,
    }


def parse_global_telemetry_known_source(
    data: bytes,
    payload_offset: int,
    payload_bytes: int,
    descriptor: dict[str, Any],
) -> dict[str, Any] | None:
    source_hash = safe_int(descriptor.get("sourceHash"), 0)
    if source_hash == SURVIVAL_BLACKBOX_SOURCE_HASH and payload_bytes >= SURVIVAL_BLACKBOX_SOURCE_BYTES:
        return parse_survival_blackbox_source_payload(data, payload_offset, descriptor)

    return None


def parse_survival_blackbox_source_payload(
    data: bytes,
    payload_offset: int,
    descriptor: dict[str, Any],
) -> dict[str, Any]:
    fields = SURVIVAL_BLACKBOX_SOURCE_ENTRY.unpack_from(data, payload_offset)
    flags = fields[15]
    label_flags = flags & ~SURVIVAL_BLACKBOX_DEATH_CAUSE_MASK
    labels, unknown_flags = resolve_bit_labels(label_flags, SURVIVAL_BLACKBOX_FLAG_LABELS)
    death_cause = (flags & SURVIVAL_BLACKBOX_DEATH_CAUSE_MASK) >> SURVIVAL_BLACKBOX_DEATH_CAUSE_SHIFT
    source_hash = fields[0]
    warnings = []
    if source_hash != SURVIVAL_BLACKBOX_SOURCE_HASH:
        warnings.append("source_hash_mismatch")
    if unknown_flags:
        warnings.append("unknown_flags")
    if any(not math.isfinite(value) for value in fields[3:14]):
        warnings.append("nonfinite_values")
    if bool(flags & (1 << 2)):
        warnings.append("beyond_safe_depth")
    if bool(flags & (1 << 4)):
        warnings.append("bends")
    if bool(flags & (1 << 6)):
        warnings.append("narcosis")
    if bool(flags & (1 << 7)):
        warnings.append("toxicity")
    if bool(flags & (1 << 8)):
        warnings.append("thermal_stress")
    if death_cause:
        warnings.append("death_cause")
    return {
        "sourceName": "survival",
        "sourceHash": source_hash,
        "sourceHashHex": f"0x{source_hash:08X}",
        "descriptorSlot": safe_int(descriptor.get("slot"), 0),
        "frame": fields[1],
        "playerEntityHash": fields[2],
        "playerEntityHashHex": f"0x{fields[2]:08X}",
        "oxygen01": round(fields[3], 4),
        "integrity01": round(fields[4], 4),
        "depthMeters": round(fields[5], 4),
        "pressureAtm": round(fields[6], 4),
        "safeDepthMeters": round(fields[7], 4),
        "overpressureMeters": round(fields[8], 4),
        "pressureExposureSeverity01": round(fields[9], 4),
        "nitrogenLoad01": round(fields[10], 4),
        "nitrogenNarcosis01": round(fields[11], 4),
        "decompressionRisk01": round(fields[12], 4),
        "internalTemperatureCelsius": round(fields[13], 4),
        "statusMask": fields[14],
        "statusMaskHex": f"0x{fields[14]:08X}",
        "flags": flags,
        "flagLabels": labels,
        "unknownFlags": unknown_flags,
        "deathCause": death_cause,
        "deathCauseLabel": SURVIVAL_DEATH_CAUSE_LABELS.get(death_cause, f"unknown-{death_cause}"),
        "alive": bool(flags & (1 << 0)),
        "underwater": bool(flags & (1 << 1)),
        "beyondSafeDepth": bool(flags & (1 << 2)),
        "oxygenGrace": bool(flags & (1 << 3)),
        "bends": bool(flags & (1 << 4)),
        "freshPhysiology": bool(flags & (1 << 5)),
        "narcosis": bool(flags & (1 << 6)),
        "toxicity": bool(flags & (1 << 7)),
        "thermalStress": bool(flags & (1 << 8)),
        "hasStats": bool(flags & (1 << 9)),
        "warnings": warnings,
    }


def is_biolum_director_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPBIOLUMINESCENCEDIRECTORBIN", "DUMPBIOLUMINESCENCEDIRECTORH8DUMP"}


def parse_biolum_director_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < BIOLUM_DIRECTOR_HEADER_BYTES:
        return {
            "type": "biolum_director_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, telemetry_sequence, reason_flags, capacity = BIOLUM_DIRECTOR_HEADER.unpack_from(data, 0)
    reason_labels, unknown_reason_flags = resolve_bit_labels(reason_flags, BIOLUM_DIRECTOR_REASON_LABELS)
    invalid_header = (
        magic != BIOLUM_DIRECTOR_MAGIC
        or capacity <= 0
        or capacity > BIOLUM_DIRECTOR_TELEMETRY_CAPACITY
        or BIOLUM_DIRECTOR_HEADER.size != BIOLUM_DIRECTOR_HEADER_BYTES
        or BIOLUM_DIRECTOR_ENTRY.size != BIOLUM_DIRECTOR_ENTRY_BYTES
    )
    if invalid_header:
        return {
            "type": "biolum_director_blackbox",
            "magic": magic,
            "magicHex": f"0x{magic:08X}",
            "headerBytes": BIOLUM_DIRECTOR_HEADER_BYTES,
            "entrySize": BIOLUM_DIRECTOR_ENTRY_BYTES,
            "telemetrySequence": telemetry_sequence,
            "reasonFlags": reason_flags,
            "reasonFlagLabels": reason_labels,
            "unknownReasonFlags": unknown_reason_flags,
            "capacity": capacity,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = BIOLUM_DIRECTOR_HEADER_BYTES
    expected_bytes = payload_offset + capacity * BIOLUM_DIRECTOR_ENTRY_BYTES
    readable_entries = min(capacity, max(0, len(data) - payload_offset) // BIOLUM_DIRECTOR_ENTRY_BYTES)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    for index in range(readable_entries):
        offset = payload_offset + index * BIOLUM_DIRECTOR_ENTRY_BYTES
        if is_empty_entry(data, offset, BIOLUM_DIRECTOR_ENTRY_BYTES):
            continue

        fields = BIOLUM_DIRECTOR_ENTRY.unpack_from(data, offset)
        flags = fields[9]
        flag_labels, unknown_flags = resolve_bit_labels(flags, BIOLUM_DIRECTOR_FLAG_LABELS)
        float_values = fields[1:7]
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "cameraPosition": {
                    "x": finite_round(fields[1]),
                    "y": finite_round(fields[2]),
                    "z": finite_round(fields[3]),
                },
                "intensity": finite_round(fields[4]),
                "phase": finite_round(fields[5]),
                "predatorDim": finite_round(fields[6]),
                "predatorHits": fields[7],
                "activeRipples": fields[8],
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "daylightMasked": bool(flags & (1 << 0)),
                "predatorDimmed": bool(flags & (1 << 1)),
                "eclipseMasked": bool(flags & (1 << 2)),
                "cameraNonfinite": bool(flags & (1 << 3)),
                "zoneRegistryOverflow": bool(flags & (1 << 4)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % BIOLUM_DIRECTOR_ENTRY_BYTES != 0:
        warnings.append("trailing_partial_entry")
    if capacity != BIOLUM_DIRECTOR_TELEMETRY_CAPACITY:
        warnings.append("capacity_mismatch")
    if reason_flags:
        warnings.append("reason_flags")
    if unknown_reason_flags:
        warnings.append("unknown_reason_flags")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if (reason_flags & (1 << 1)) != 0:
        warnings.append("nonfinite_intensity_phase")
    if any(entry.get("cameraNonfinite") for entry in entries) or (reason_flags & (1 << 3)) != 0:
        warnings.append("camera_nonfinite")
    if any(entry.get("zoneRegistryOverflow") for entry in entries):
        warnings.append("zone_registry_overflow")
    if any(entry.get("activeRipples", 0) > BIOLUM_DIRECTOR_MAX_TOUCH_RIPPLES for entry in entries):
        warnings.append("active_ripples_out_of_range")
    if any(entry.get("predatorHits", 0) > BIOLUM_DIRECTOR_MAX_PREDATOR_CONTACTS for entry in entries):
        warnings.append("predator_hits_out_of_range")
    if any(
        entry.get("intensity") is None
        or entry.get("intensity", 0.0) < 0.0
        or entry.get("phase") is None
        or entry.get("predatorDim") is None
        or entry.get("predatorDim", 0.0) < 0.0
        or entry.get("predatorDim", 0.0) > 1.0
        for entry in entries
    ):
        warnings.append("biolum_value_out_of_range")

    return {
        "type": "biolum_director_blackbox",
        "magic": magic,
        "magicHex": f"0x{magic:08X}",
        "headerBytes": BIOLUM_DIRECTOR_HEADER_BYTES,
        "entrySize": BIOLUM_DIRECTOR_ENTRY_BYTES,
        "declaredEntryCount": capacity,
        "capacity": capacity,
        "telemetrySequence": telemetry_sequence,
        "reasonFlags": reason_flags,
        "reasonFlagLabels": reason_labels,
        "unknownReasonFlags": unknown_reason_flags,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_toxic_outgassing_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPTOXICSURGEONBIN", "DUMPTOXICSURGEONH8DUMP"}


def parse_toxic_outgassing_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < TOXIC_OUTGASSING_HEADER_BYTES:
        return {
            "type": "toxic_outgassing_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, header_bytes, entry_size, capacity, retained_count, telemetry_cursor, payload_bytes = (
        TOXIC_OUTGASSING_HEADER.unpack_from(data, 0)
    )
    if (
        magic != TOXIC_OUTGASSING_MAGIC
        or version != TOXIC_OUTGASSING_VERSION
        or header_bytes != TOXIC_OUTGASSING_HEADER_BYTES
        or entry_size != TOXIC_OUTGASSING_ENTRY_BYTES
        or capacity <= 0
        or retained_count <= 0
        or retained_count > capacity
        or telemetry_cursor >= capacity
        or payload_bytes != retained_count * entry_size
        or len(data) < header_bytes
    ):
        return {
            "type": "toxic_outgassing_blackbox",
            "magic": magic,
            "version": version,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    expected_bytes = header_bytes + payload_bytes
    readable_entries = min(retained_count, max(0, len(data) - header_bytes) // entry_size)
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = header_bytes + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = TOXIC_OUTGASSING_ENTRY.unpack_from(data, offset)
        flags = fields[12]
        flag_labels, unknown_flags = resolve_bit_labels(flags, TOXIC_OUTGASSING_FLAG_LABELS)
        if any(not math.isfinite(value) for value in fields[0:7]):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "gridOriginAup": {
                    "x": round(fields[0], 4),
                    "y": round(fields[1], 4),
                    "z": round(fields[2], 4),
                },
                "maxDensity": round(fields[3], 6),
                "totalPlumeVolume": round(fields[4], 6),
                "globalQualityWeight": round(fields[5], 4),
                "diffusionCompleteMs": round(fields[6], 4),
                "stateHash": fields[7],
                "stateHashHex": f"0x{fields[7]:08X}",
                "frame": fields[8],
                "activeResolution": fields[9],
                "activeSources": fields[10],
                "activeEntities": fields[11],
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "nanDetected": bool(fields[13]),
                "reserved0": fields[14],
                "mockChemistry": bool(flags & (1 << 0)),
                "binaryProbeFailure": bool(flags & (1 << 5)),
                "dumpFailure": bool(flags & (1 << 6)),
                "nanFlag": bool(flags & (1 << 7)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if capacity > TOXIC_OUTGASSING_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("reserved0") for entry in entries):
        warnings.append("reserved_nonzero")
    if any(entry.get("nanDetected") or entry.get("nanFlag") for entry in entries):
        warnings.append("nan_detected")
    if any(entry.get("dumpFailure") for entry in entries):
        warnings.append("dump_failure")
    if any(entry.get("binaryProbeFailure") for entry in entries):
        warnings.append("binary_probe_failure")
    if any(entry.get("mockChemistry") for entry in entries):
        warnings.append("mock_chemistry")
    return {
        "type": "toxic_outgassing_blackbox",
        "magic": magic,
        "version": version,
        "headerBytes": header_bytes,
        "entrySize": entry_size,
        "declaredCapacity": capacity,
        "declaredEntryCount": retained_count,
        "telemetryCursor": telemetry_cursor,
        "payloadBytes": payload_bytes,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_gas_dynamics_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMP1324SUBMARINEATMOSPHEREBIN",
        "DUMP1324SUBMARINEATMOSPHEREH8DUMP",
        "DUMPSUBMARINEATMOSPHEREBIN",
        "DUMPSUBMARINEATMOSPHEREH8DUMP",
    }


def resolve_gas_dynamics_flags(flags: int) -> tuple[list[str], int, int, str | None]:
    if flags & GAS_DYNAMICS_FAILURE_FLAG:
        failure_code = flags & ~GAS_DYNAMICS_FAILURE_FLAG
        failure_label = GAS_DYNAMICS_FAILURE_LABELS.get(failure_code, f"unknown-failure-{failure_code}")
        return ["failure", failure_label], 0, failure_code, failure_label

    labels, unknown_flags = resolve_bit_labels(flags, GAS_DYNAMICS_FLAG_LABELS)
    return labels, unknown_flags, 0, None


def parse_gas_dynamics_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < GAS_DYNAMICS_HEADER_BYTES:
        return {
            "type": "gas_dynamics_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, entry_size, entry_count, telemetry_cursor, tick_count = GAS_DYNAMICS_HEADER.unpack_from(data, 0)
    if (
        magic != GAS_DYNAMICS_MAGIC
        or version != GAS_DYNAMICS_VERSION
        or entry_size != GAS_DYNAMICS_ENTRY_BYTES
        or entry_count <= 0
        or telemetry_cursor < 0
        or telemetry_cursor >= entry_count
    ):
        return {
            "type": "gas_dynamics_blackbox",
            "magic": magic,
            "version": version,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = GAS_DYNAMICS_HEADER_BYTES
    expected_bytes = payload_offset + entry_count * entry_size
    readable_entries = min(entry_count, max(0, len(data) - payload_offset) // entry_size)
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = GAS_DYNAMICS_ENTRY.unpack_from(data, offset)
        flags = fields[14]
        flag_labels, unknown_flags, failure_code, failure_label = resolve_gas_dynamics_flags(flags)
        if any(not math.isfinite(value) for value in fields[3:7]) or not math.isfinite(fields[12]):
            nonfinite_seen = True
        packed_owner = fields[0]
        owner_buffer_id = (packed_owner >> 32) & 0xFFFFFFFF
        owner_system_id = packed_owner & 0xFFFFFFFF
        entries.append(
            {
                "slot": index,
                "packedOwner": packed_owner,
                "packedOwnerHex": f"0x{packed_owner:016X}",
                "ownerBufferId": owner_buffer_id,
                "ownerSystemId": owner_system_id,
                "frame": fields[1],
                "roomCount": fields[2],
                "totalO2KPa": round(fields[3], 4),
                "totalCO2KPa": round(fields[4], 4),
                "totalNitrogenKPa": round(fields[5], 4),
                "maxPressureKPa": round(fields[6], 4),
                "stateHash": fields[7],
                "stateHashHex": f"0x{fields[7]:08X}",
                "bufferId": fields[8],
                "bufferIdHex": f"0x{fields[8]:08X}",
                "systemId": fields[9],
                "generation": fields[10],
                "droppedUpdates": fields[11],
                "cpuMicroseconds": round(fields[12], 4),
                "reservedPad": fields[13],
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "failureCode": failure_code,
                "failureLabel": failure_label,
                "sleepingRoomCount": fields[15],
                "nanDetected": bool(flags & (1 << 0)) and not (flags & GAS_DYNAMICS_FAILURE_FLAG),
                "breach": bool(flags & (1 << 1)) and not (flags & GAS_DYNAMICS_FAILURE_FLAG),
                "hibernating": bool(flags & (1 << 2)) and not (flags & GAS_DYNAMICS_FAILURE_FLAG),
                "failure": bool(flags & GAS_DYNAMICS_FAILURE_FLAG),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if entry_count > GAS_DYNAMICS_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("reservedPad") for entry in entries):
        warnings.append("reserved_pad_nonzero")
    if any(entry.get("nanDetected") for entry in entries):
        warnings.append("nan_detected")
    if any(entry.get("breach") for entry in entries):
        warnings.append("breach")
    if any(entry.get("hibernating") for entry in entries):
        warnings.append("hibernating")
    if any(entry.get("failure") for entry in entries):
        warnings.append("failure")
    if any(entry.get("failureLabel") == "state-write-lock" for entry in entries):
        warnings.append("state_write_lock")
    if any(entry.get("failureLabel") == "step-completion-deferred" for entry in entries):
        warnings.append("step_completion_deferred")
    if any(entry.get("droppedUpdates", 0) > 0 for entry in entries):
        warnings.append("dropped_updates")
    return {
        "type": "gas_dynamics_blackbox",
        "magic": magic,
        "version": version,
        "headerBytes": GAS_DYNAMICS_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": entry_count,
        "telemetryCursor": telemetry_cursor,
        "tickCount": tick_count,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_base_atmosphere_logistics_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMPSHINOBU221BIN",
        "DUMPSHINOBU221H8DUMP",
        "DUMPBASEATMOSPHERELOGISTICSBIN",
        "DUMPBASEATMOSPHERELOGISTICSH8DUMP",
        "DUMPATMOSPHERELOGISTICSBIN",
        "DUMPATMOSPHERELOGISTICSH8DUMP",
    }


def parse_base_atmosphere_logistics_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < BASE_ATMOSPHERE_LOGISTICS_HEADER_BYTES:
        return {
            "type": "base_atmosphere_logistics_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, entry_count = BASE_ATMOSPHERE_LOGISTICS_HEADER.unpack_from(data, 0)
    if (
        magic != BASE_ATMOSPHERE_LOGISTICS_MAGIC
        or version != BASE_ATMOSPHERE_LOGISTICS_VERSION
        or entry_count <= 0
    ):
        return {
            "type": "base_atmosphere_logistics_blackbox",
            "magic": magic,
            "version": version,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = BASE_ATMOSPHERE_LOGISTICS_HEADER_BYTES
    entry_size = BASE_ATMOSPHERE_LOGISTICS_ENTRY_BYTES
    expected_bytes = payload_offset + entry_count * entry_size
    readable_entries = min(entry_count, max(0, len(data) - payload_offset) // entry_size)
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = BASE_ATMOSPHERE_LOGISTICS_ENTRY.unpack_from(data, offset)
        fault_flags = fields[13]
        fault_labels, unknown_fault_flags = resolve_bit_labels(
            fault_flags,
            BASE_ATMOSPHERE_LOGISTICS_FAULT_LABELS,
        )
        if any(not math.isfinite(value) for value in fields[1:6]):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "stateHash": fields[0],
                "stateHashHex": f"0x{fields[0]:016X}",
                "averageOxygen01": round(fields[1], 6),
                "maxCarbonDioxide01": round(fields[2], 6),
                "averageNitrogen01": round(fields[3], 6),
                "maxToxin01": round(fields[4], 6),
                "averageTemperature": round(fields[5], 4),
                "frame": fields[6],
                "nodeCount": fields[7],
                "edgeCount": fields[8],
                "consumerCount": fields[9],
                "sourceCount": fields[10],
                "solverMicros": fields[11],
                "jacobiIterations": fields[12],
                "faultFlags": fault_flags,
                "faultFlagsHex": f"0x{fault_flags:08X}",
                "faultLabels": fault_labels,
                "unknownFaultFlags": unknown_fault_flags,
                "totalGasUnits": fields[14],
                "layoutFault": bool(fault_flags & (1 << 0)),
                "emptyGraph": bool(fault_flags & (1 << 1)),
                "nonFiniteGas": bool(fault_flags & (1 << 2)),
                "bufferAlias": bool(fault_flags & (1 << 3)),
                "csrOverflow": bool(fault_flags & (1 << 4)),
                "sourceOverflow": bool(fault_flags & (1 << 5)),
                "csvMalformed": bool(fault_flags & (1 << 6)),
                "nanDetected": bool(fault_flags & (1 << 7)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if entry_count > BASE_ATMOSPHERE_LOGISTICS_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if any(entry.get("unknownFaultFlags") for entry in entries):
        warnings.append("unknown_fault_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("layoutFault") for entry in entries):
        warnings.append("layout_fault")
    if any(entry.get("emptyGraph") for entry in entries):
        warnings.append("empty_graph")
    if any(entry.get("nonFiniteGas") for entry in entries):
        warnings.append("nonfinite_gas")
    if any(entry.get("bufferAlias") for entry in entries):
        warnings.append("buffer_alias")
    if any(entry.get("csrOverflow") for entry in entries):
        warnings.append("csr_overflow")
    if any(entry.get("sourceOverflow") for entry in entries):
        warnings.append("source_overflow")
    if any(entry.get("csvMalformed") for entry in entries):
        warnings.append("csv_malformed")
    if any(entry.get("nanDetected") for entry in entries):
        warnings.append("nan_detected")
    return {
        "type": "base_atmosphere_logistics_blackbox",
        "magic": magic,
        "version": version,
        "headerBytes": BASE_ATMOSPHERE_LOGISTICS_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": entry_count,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_storm_propagation_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMPSHINOBU234BIN",
        "DUMPSHINOBU234H8DUMP",
        "DUMPSTORMPROPAGATIONBIN",
        "DUMPSTORMPROPAGATIONH8DUMP",
        "DUMPSHINOBUSTORMPROPAGATIONBIN",
        "DUMPSHINOBUSTORMPROPAGATIONH8DUMP",
    }


def parse_storm_propagation_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < STORM_PROPAGATION_HEADER_BYTES:
        return {
            "type": "storm_propagation_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    (
        magic,
        reason_flags,
        write_cursor,
        entry_count,
        entry_stride_bytes,
        source_hash,
        state_hash,
        reserved,
    ) = STORM_PROPAGATION_HEADER.unpack_from(data, 0)
    if (
        magic != STORM_PROPAGATION_MAGIC
        or entry_count <= 0
        or entry_stride_bytes != STORM_PROPAGATION_ENTRY_BYTES
    ):
        return {
            "type": "storm_propagation_blackbox",
            "magic": magic,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    reason_labels, unknown_reason_flags = resolve_bit_labels(reason_flags, STORM_PROPAGATION_FLAG_LABELS)
    payload_offset = STORM_PROPAGATION_HEADER_BYTES
    expected_bytes = payload_offset + entry_count * entry_stride_bytes
    readable_entries = min(entry_count, max(0, len(data) - payload_offset) // entry_stride_bytes)
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = payload_offset + index * entry_stride_bytes
        if is_empty_entry(data, offset, entry_stride_bytes):
            continue

        fields = STORM_PROPAGATION_ENTRY.unpack_from(data, offset)
        flags = fields[1]
        flag_labels, unknown_flags = resolve_bit_labels(flags, STORM_PROPAGATION_FLAG_LABELS)
        if any(not math.isfinite(value) for value in fields[2:14]):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "surfaceIntensity01": round(fields[2], 6),
                "depthMeters": round(fields[3], 4),
                "attenuatedEnergy01": round(fields[4], 6),
                "turbidityScalar": round(fields[5], 6),
                "acousticMuffling01": round(fields[6], 6),
                "biolumStimulus01": round(fields[7], 6),
                "surgeVector": {
                    "x": round(fields[8], 6),
                    "y": round(fields[9], 6),
                    "z": round(fields[10], 6),
                },
                "globalQualityWeight": round(fields[11], 4),
                "scheduleToPublishMicroseconds": round(fields[12], 4),
                "previousSurfaceIntensity01": round(fields[13], 6),
                "stateHash": fields[14],
                "stateHashHex": f"0x{fields[14]:08X}",
                "noiseOctaveCount": fields[15],
                "nonFinite": bool(flags & (1 << 0)),
                "mockWeather": bool(flags & (1 << 1)),
                "fogPublished": bool(flags & (1 << 2)),
                "biolumPublished": bool(flags & (1 << 3)),
                "audioPublished": bool(flags & (1 << 4)),
                "flowPublished": bool(flags & (1 << 5)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_stride_bytes != 0:
        warnings.append("trailing_partial_entry")
    if entry_count > STORM_PROPAGATION_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if write_cursor < 0 or write_cursor >= entry_count:
        warnings.append("write_cursor_out_of_range")
    if source_hash != STORM_PROPAGATION_SOURCE_HASH:
        warnings.append("source_hash_mismatch")
    if reserved != 0:
        warnings.append("reserved_nonzero")
    if unknown_reason_flags:
        warnings.append("unknown_reason_flags")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("nonFinite") for entry in entries) or (reason_flags & (1 << 0)):
        warnings.append("nonfinite")
    if any(entry.get("mockWeather") for entry in entries) or (reason_flags & (1 << 1)):
        warnings.append("mock_weather")
    if any(entry.get("fogPublished") for entry in entries) or (reason_flags & (1 << 2)):
        warnings.append("fog_published")
    if any(entry.get("biolumPublished") for entry in entries) or (reason_flags & (1 << 3)):
        warnings.append("biolum_published")
    if any(entry.get("audioPublished") for entry in entries) or (reason_flags & (1 << 4)):
        warnings.append("audio_published")
    if any(entry.get("flowPublished") for entry in entries) or (reason_flags & (1 << 5)):
        warnings.append("flow_published")
    if any(entry.get("noiseOctaveCount", 0) < 0 for entry in entries):
        warnings.append("negative_noise_octaves")
    return {
        "type": "storm_propagation_blackbox",
        "magic": magic,
        "headerBytes": STORM_PROPAGATION_HEADER_BYTES,
        "entrySize": entry_stride_bytes,
        "declaredEntryCount": entry_count,
        "writeCursor": write_cursor,
        "reasonFlags": reason_flags,
        "reasonFlagLabels": reason_labels,
        "unknownReasonFlags": unknown_reason_flags,
        "sourceHash": source_hash,
        "sourceHashHex": f"0x{source_hash:08X}",
        "stateHash": state_hash,
        "stateHashHex": f"0x{state_hash:08X}",
        "reserved": reserved,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_ocean_surface_atmosphere_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMPSHINOBU147BIN",
        "DUMPSHINOBU147H8DUMP",
        "DUMPOCEANSURFACEATMOSPHEREBIN",
        "DUMPOCEANSURFACEATMOSPHEREH8DUMP",
        "DUMPSHINOBUOCEANSURFACEBIN",
        "DUMPSHINOBUOCEANSURFACEH8DUMP",
    }


def parse_ocean_surface_atmosphere_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < OCEAN_SURFACE_ATMOSPHERE_HEADER_BYTES:
        return {
            "type": "ocean_surface_atmosphere_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    (
        magic,
        marker,
        entry_count,
        entry_size,
        state_hash,
        telemetry_cursor,
        reserved0,
        reserved1,
    ) = OCEAN_SURFACE_ATMOSPHERE_HEADER.unpack_from(data, 0)
    if (
        magic != OCEAN_SURFACE_ATMOSPHERE_MAGIC
        or marker != OCEAN_SURFACE_ATMOSPHERE_MARKER
        or entry_count <= 0
        or entry_size != OCEAN_SURFACE_ATMOSPHERE_ENTRY_BYTES
    ):
        return {
            "type": "ocean_surface_atmosphere_blackbox",
            "magic": magic,
            "marker": marker,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = OCEAN_SURFACE_ATMOSPHERE_HEADER_BYTES
    expected_bytes = payload_offset + entry_count * entry_size
    readable_entries = min(entry_count, max(0, len(data) - payload_offset) // entry_size)
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = OCEAN_SURFACE_ATMOSPHERE_ENTRY.unpack_from(data, offset)
        flags = fields[1]
        flag_labels, unknown_flags = resolve_bit_labels(flags, OCEAN_SURFACE_ATMOSPHERE_FLAG_LABELS)
        if any(not math.isfinite(value) for value in (fields[2], fields[3], fields[5], *fields[7:12])):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "maxWaveHeight": round(fields[2], 6),
                "stormIntensity": round(fields[3], 6),
                "waveComputeTimeNs": fields[4],
                "globalQualityWeight": round(fields[5], 4),
                "activeWaveCount": fields[6],
                "surfaceDisturbance": round(fields[7], 6),
                "foamScalar": round(fields[8], 6),
                "lastNormal": {
                    "x": round(fields[9], 6),
                    "y": round(fields[10], 6),
                    "z": round(fields[11], 6),
                },
                "stateHash": fields[12],
                "stateHashHex": f"0x{fields[12]:08X}",
                "readbackLatencyFrames": fields[13],
                "readbackSampleCount": fields[14],
                "latencyOrBudget": bool(flags & (1 << 0)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if entry_count > OCEAN_SURFACE_ATMOSPHERE_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if telemetry_cursor >= entry_count:
        warnings.append("telemetry_cursor_out_of_range")
    if reserved0 != 0 or reserved1 != 0:
        warnings.append("reserved_nonzero")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("latencyOrBudget") for entry in entries):
        warnings.append("latency_or_budget")
    if any(entry.get("readbackLatencyFrames", 0) > 4 for entry in entries):
        warnings.append("readback_latency")
    if any(entry.get("waveComputeTimeNs", 0) > OCEAN_SURFACE_ATMOSPHERE_DUMP_BUDGET_NS for entry in entries):
        warnings.append("wave_compute_over_budget")
    if any(entry.get("activeWaveCount", 0) < 0 for entry in entries):
        warnings.append("negative_active_wave_count")
    if any(entry.get("readbackSampleCount", 0) < 0 for entry in entries):
        warnings.append("negative_readback_sample_count")
    return {
        "type": "ocean_surface_atmosphere_blackbox",
        "magic": magic,
        "marker": marker,
        "headerBytes": OCEAN_SURFACE_ATMOSPHERE_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": entry_count,
        "telemetryCursor": telemetry_cursor,
        "stateHash": state_hash,
        "stateHashHex": f"0x{state_hash:08X}",
        "reserved0": reserved0,
        "reserved1": reserved1,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_thermodynamics_hazard_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMPTHERMODYNAMICSBIN",
        "DUMPTHERMODYNAMICSH8DUMP",
        "DUMPSHINOBU16BIN",
        "DUMPSHINOBU16H8DUMP",
        "DUMPTHERMODYNAMICSHAZARDBIN",
        "DUMPTHERMODYNAMICSHAZARDH8DUMP",
    }


def parse_thermodynamics_hazard_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < THERMODYNAMICS_HAZARD_HEADER_BYTES:
        return {
            "type": "thermodynamics_hazard_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, entry_count, entry_size, telemetry_cursor = THERMODYNAMICS_HAZARD_HEADER.unpack_from(data, 0)
    if (
        magic != THERMODYNAMICS_HAZARD_MAGIC
        or entry_count <= 0
        or entry_size != THERMODYNAMICS_HAZARD_ENTRY_BYTES
    ):
        return {
            "type": "thermodynamics_hazard_blackbox",
            "magic": magic,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = THERMODYNAMICS_HAZARD_HEADER_BYTES
    expected_bytes = payload_offset + entry_count * entry_size
    readable_entries = min(entry_count, max(0, len(data) - payload_offset) // entry_size)
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = THERMODYNAMICS_HAZARD_ENTRY.unpack_from(data, offset)
        flags = fields[9]
        flag_labels, unknown_flags = resolve_bit_labels(flags, THERMODYNAMICS_HAZARD_FLAG_LABELS)
        if any(not math.isfinite(value) for value in fields[0:6]):
            nonfinite_seen = True
        quality_pressure = fields[14] / 255.0
        health_pressure = fields[15] / 255.0
        entries.append(
            {
                "slot": index,
                "maxGridTemperature": round(fields[0], 4),
                "maxRadiationLevel": round(fields[1], 4),
                "diffusionComputeTimeMs": round(fields[2], 4),
                "gridOrigin": {
                    "x": round(fields[3], 4),
                    "y": round(fields[4], 4),
                    "z": round(fields[5], 4),
                },
                "frame": fields[6],
                "gridVersion": fields[7],
                "sourceCount": fields[8],
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "shiftSequence": fields[10],
                "nanCellIndex": fields[11],
                "activeResolution": fields[12],
                "gridOriginHash": fields[13],
                "gridOriginHashHex": f"0x{fields[13]:08X}",
                "qualityPressureQ8": fields[14],
                "qualityPressure01": round(quality_pressure, 4),
                "healthPressureQ8": fields[15],
                "healthPressure01": round(health_pressure, 4),
                "reservedPad": fields[16],
                "reserved1": fields[17],
                "nanDetected": bool(flags & (1 << 0)),
                "rebase": bool(flags & (1 << 2)),
                "signalDrop": bool(flags & (1 << 4)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if entry_count > THERMODYNAMICS_HAZARD_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if telemetry_cursor < 0 or telemetry_cursor >= entry_count:
        warnings.append("telemetry_cursor_out_of_range")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("reservedPad") or entry.get("reserved1") for entry in entries):
        warnings.append("reserved_nonzero")
    if any(entry.get("nanDetected") for entry in entries):
        warnings.append("nan_detected")
    if any(entry.get("rebase") for entry in entries):
        warnings.append("rebase")
    if any(entry.get("signalDrop") for entry in entries):
        warnings.append("signal_drop")
    if any(entry.get("sourceCount", 0) > 128 for entry in entries):
        warnings.append("source_capacity_exceeded")
    if any(entry.get("activeResolution", 0) not in {16, 32} for entry in entries):
        warnings.append("unexpected_resolution")
    return {
        "type": "thermodynamics_hazard_blackbox",
        "magic": magic,
        "headerBytes": THERMODYNAMICS_HAZARD_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": entry_count,
        "telemetryCursor": telemetry_cursor,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_abyssal_thermodynamics_raw_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMPSHINOBU203BIN",
        "DUMPSHINOBU203H8DUMP",
        "DUMPABYSSALTHERMODYNAMICSBIN",
        "DUMPABYSSALTHERMODYNAMICSH8DUMP",
        "DUMPTHERMOSURGEONBIN",
        "DUMPTHERMOSURGEONH8DUMP",
    }


def parse_abyssal_thermodynamics_raw_blackbox(data: bytes) -> dict[str, Any]:
    entry_size = ABYSSAL_THERMODYNAMICS_ENTRY_BYTES
    if len(data) < entry_size:
        return {
            "type": "abyssal_thermodynamics_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_entry"],
        }

    entry_count = len(data) // entry_size
    readable_entries = min(entry_count, ABYSSAL_THERMODYNAMICS_TELEMETRY_CAPACITY)
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = ABYSSAL_THERMODYNAMICS_ENTRY.unpack_from(data, offset)
        flags = fields[8]
        flag_labels, unknown_flags = resolve_bit_labels(flags, ABYSSAL_THERMODYNAMICS_FLAG_LABELS)
        if any(not math.isfinite(value) for value in fields[0:7]):
            nonfinite_seen = True
        energy_delta = fields[2] - fields[1]
        entries.append(
            {
                "slot": index,
                "maxTemperatureCelsius": round(fields[0], 4),
                "energyBefore": round(fields[1], 4),
                "energyAfter": round(fields[2], 4),
                "energyDelta": round(energy_delta, 4),
                "solverMicroseconds": round(fields[3], 4),
                "gridOriginAup": {
                    "x": round(fields[4], 4),
                    "y": round(fields[5], 4),
                    "z": round(fields[6], 4),
                },
                "frame": fields[7],
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "activeSourceCount": fields[9],
                "jacobiIterations": fields[10],
                "nanCellIndex": fields[11],
                "activeResolution": fields[12],
                "nanDetected": bool(flags & (1 << 0)),
                "shift": bool(flags & (1 << 1)),
                "mockSources": bool(flags & (1 << 2)),
                "energyDrift": bool(flags & (1 << 3)),
                "divergent": bool(flags & (1 << 4)),
                "maxIterations": bool(flags & (1 << 5)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if entry_count > ABYSSAL_THERMODYNAMICS_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("nanDetected") for entry in entries):
        warnings.append("nan_detected")
    if any(entry.get("shift") for entry in entries):
        warnings.append("shift")
    if any(entry.get("mockSources") for entry in entries):
        warnings.append("mock_sources")
    if any(entry.get("energyDrift") for entry in entries):
        warnings.append("energy_drift")
    if any(entry.get("divergent") for entry in entries):
        warnings.append("divergent")
    if any(entry.get("maxIterations") for entry in entries):
        warnings.append("max_iterations")
    if any(entry.get("activeSourceCount", 0) > 128 for entry in entries):
        warnings.append("source_capacity_exceeded")
    if any(entry.get("activeResolution", 0) not in {16, 32} for entry in entries):
        warnings.append("unexpected_resolution")
    return {
        "type": "abyssal_thermodynamics_blackbox",
        "entrySize": entry_size,
        "declaredEntryCount": entry_count,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_reactor_thermal_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPSHINOBU337BIN", "DUMPSHINOBU337H8DUMP"}


def is_nuclear_reactor_thermal_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPSHINOBU342BIN", "DUMPSHINOBU342H8DUMP"}


def reactor_thermal_raw_warnings(
    data_len: int,
    entry_size: int,
    entry_count: int,
    entries: list[dict[str, Any]],
    nonfinite_seen: bool,
) -> list[str]:
    warnings = []
    if data_len % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if entry_count > REACTOR_THERMAL_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("nonFinite") or entry.get("nonFiniteCount", 0) for entry in entries):
        warnings.append("nonfinite")
    if any(entry.get("outOfGrid") for entry in entries):
        warnings.append("out_of_grid")
    if any(entry.get("meltdown") or entry.get("meltdownCount", 0) for entry in entries):
        warnings.append("meltdown")
    if any(entry.get("mockLoad") for entry in entries):
        warnings.append("mock_load")
    if any(entry.get("costOverBudget") for entry in entries):
        warnings.append("cost_over_budget")
    if any(entry.get("signalOverflowRisk") for entry in entries):
        warnings.append("signal_overflow_risk")
    if any(entry.get("noCoolant") for entry in entries):
        warnings.append("no_coolant")
    if any(entry.get("atomicAbort") or entry.get("atomicAbortCount", 0) for entry in entries):
        warnings.append("atomic_abort")
    if any(entry.get("activeReactorCount", 0) > 16 for entry in entries):
        warnings.append("reactor_capacity_exceeded")
    if any(entry.get("ringIndex", 0) >= REACTOR_THERMAL_TELEMETRY_CAPACITY for entry in entries):
        warnings.append("ring_index_out_of_range")
    return warnings


def parse_reactor_thermal_blackbox(data: bytes) -> dict[str, Any]:
    entry_size = REACTOR_THERMAL_ENTRY_BYTES
    if len(data) < entry_size:
        return {
            "type": "reactor_thermal_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_entry"],
        }

    entry_count = len(data) // entry_size
    readable_entries = min(entry_count, REACTOR_THERMAL_TELEMETRY_CAPACITY)
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = REACTOR_THERMAL_ENTRY.unpack_from(data, offset)
        flags = fields[10]
        flag_labels, unknown_flags = resolve_bit_labels(flags, REACTOR_THERMAL_FLAG_LABELS)
        if any(not math.isfinite(value) for value in fields[0:8]):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "hotReactorAup": {
                    "x": round(fields[0], 4),
                    "y": round(fields[1], 4),
                    "z": round(fields[2], 4),
                },
                "totalJoulesInjected": round(fields[3], 4),
                "averageCoreTempCelsius": round(fields[4], 4),
                "maxCoreTempCelsius": round(fields[5], 4),
                "maxSpeedMetersPerSecond": round(fields[6], 4),
                "lastInjectionMicroseconds": round(fields[7], 4),
                "activeReactorCount": fields[8],
                "meltdownCount": fields[9],
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "frame": fields[11],
                "stateHash": fields[12],
                "stateHashHex": f"0x{fields[12]:08X}",
                "hotCellHash": fields[13],
                "hotCellHashHex": f"0x{fields[13]:08X}",
                "injectionCellWrites": fields[14],
                "nonFiniteCount": fields[15],
                "thermalSignalCount": fields[16],
                "damageSignalCount": fields[17],
                "ringIndex": fields[18],
                "hotReactorHashID": fields[19],
                "hotReactorHashHex": f"0x{fields[19]:08X}",
                "hotEntityHashID": fields[20],
                "hotEntityHashHex": f"0x{fields[20]:08X}",
                "nonFinite": bool(flags & (1 << 0)),
                "outOfGrid": bool(flags & (1 << 1)),
                "meltdown": bool(flags & (1 << 2)),
                "mockLoad": bool(flags & (1 << 3)),
                "costOverBudget": bool(flags & (1 << 4)),
                "signalOverflowRisk": bool(flags & (1 << 5)),
                "timingProxy": bool(flags & (1 << 6)),
                "noCoolant": bool(flags & (1 << 7)),
                "atomicAbort": bool(flags & (1 << 8)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    return {
        "type": "reactor_thermal_blackbox",
        "entrySize": entry_size,
        "declaredEntryCount": entry_count,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": reactor_thermal_raw_warnings(len(data), entry_size, entry_count, entries, nonfinite_seen),
    }


def parse_nuclear_reactor_thermal_blackbox(data: bytes) -> dict[str, Any]:
    entry_size = NUCLEAR_REACTOR_THERMAL_ENTRY_BYTES
    if len(data) < entry_size:
        return {
            "type": "nuclear_reactor_thermal_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_entry"],
        }

    entry_count = len(data) // entry_size
    readable_entries = min(entry_count, REACTOR_THERMAL_TELEMETRY_CAPACITY)
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = NUCLEAR_REACTOR_THERMAL_ENTRY.unpack_from(data, offset)
        flags = fields[11]
        flag_labels, unknown_flags = resolve_bit_labels(flags, REACTOR_THERMAL_FLAG_LABELS)
        if any(not math.isfinite(value) for value in fields[0:9]):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "hotReactorAup": {
                    "x": round(fields[0], 4),
                    "y": round(fields[1], 4),
                    "z": round(fields[2], 4),
                },
                "totalGeneratedWatts": round(fields[3], 4),
                "totalBoiledLiters": round(fields[4], 4),
                "averageCoreTempCelsius": round(fields[5], 4),
                "maxCoreTempCelsius": round(fields[6], 4),
                "lastExecutionMicroseconds": round(fields[7], 4),
                "averageCarnotEfficiency01": round(fields[8], 4),
                "activeReactorCount": fields[9],
                "meltdownCount": fields[10],
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "frame": fields[12],
                "stateHash": fields[13],
                "stateHashHex": f"0x{fields[13]:08X}",
                "powerNodeHashID": fields[14],
                "powerNodeHashHex": f"0x{fields[14]:08X}",
                "fluidRoomHashID": fields[15],
                "fluidRoomHashHex": f"0x{fields[15]:08X}",
                "radiationSignalCount": fields[16],
                "baseCompromiseSignalCount": fields[17],
                "ringIndex": fields[18],
                "nonFiniteCount": fields[19],
                "atomicAbortCount": fields[20],
                "nonFinite": bool(flags & (1 << 0)),
                "outOfGrid": bool(flags & (1 << 1)),
                "meltdown": bool(flags & (1 << 2)),
                "mockLoad": bool(flags & (1 << 3)),
                "costOverBudget": bool(flags & (1 << 4)),
                "signalOverflowRisk": bool(flags & (1 << 5)),
                "timingProxy": bool(flags & (1 << 6)),
                "noCoolant": bool(flags & (1 << 7)),
                "atomicAbort": bool(flags & (1 << 8)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    return {
        "type": "nuclear_reactor_thermal_blackbox",
        "entrySize": entry_size,
        "declaredEntryCount": entry_count,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": reactor_thermal_raw_warnings(len(data), entry_size, entry_count, entries, nonfinite_seen),
    }


def is_foveated_simulation_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPFOVEATEDSIMULATIONDIRECTORBIN", "DUMPFOVEATEDSIMULATIONDIRECTORH8DUMP"}


def parse_foveated_simulation_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < FOVEATED_SIMULATION_HEADER.size:
        return {
            "type": "foveated_simulation_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, entry_count, cursor = FOVEATED_SIMULATION_HEADER.unpack_from(data, 0)
    if (
        magic != FOVEATED_SIMULATION_MAGIC
        or entry_count <= 0
        or entry_count > 100000
        or cursor < 0
        or cursor >= entry_count
    ):
        return {
            "type": "foveated_simulation_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_bytes = len(data) - FOVEATED_SIMULATION_HEADER.size
    readable_slots = min(entry_count, max(0, payload_bytes // FOVEATED_SIMULATION_ENTRY.size))
    slot_order = list(range(cursor, readable_slots)) + list(range(0, min(cursor, readable_slots)))
    entries = []
    for slot in slot_order:
        offset = FOVEATED_SIMULATION_HEADER.size + slot * FOVEATED_SIMULATION_ENTRY.size
        if is_empty_entry(data, offset, FOVEATED_SIMULATION_ENTRY.size):
            continue

        fields = FOVEATED_SIMULATION_ENTRY.unpack_from(data, offset)
        flags = fields[12]
        labels, unknown_flags = resolve_bit_labels(flags, FOVEATED_SIMULATION_FLAG_LABELS)
        state_hash = fields[13]
        computed_state_hash = compute_foveated_simulation_state_hash(fields[1], fields[2], fields[3], fields[4], fields[5])
        entries.append(
            {
                "slot": slot,
                "frame": fields[0],
                "targetCount": fields[1],
                "frozenEntityCount": fields[2],
                "tier0Count": fields[3],
                "tier1Count": fields[4],
                "tier2Count": fields[5],
                "cameraPosition": {"x": round(fields[6], 4), "y": round(fields[7], 4), "z": round(fields[8], 4)},
                "cameraForward": {"x": round(fields[9], 4), "y": round(fields[10], 4), "z": round(fields[11], 4)},
                "flags": flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "forceImmediateImportanceRefresh": bool(flags & 0x01),
                "stateHash": state_hash,
                "computedStateHash": computed_state_hash,
                "stateHashOk": state_hash == computed_state_hash,
            }
        )

    latest = entries[-1] if entries else None
    capped = cap_entries(entries)
    warnings = []
    if readable_slots < entry_count:
        warnings.append("payload_truncated")
    if len(data) > FOVEATED_SIMULATION_HEADER.size + entry_count * FOVEATED_SIMULATION_ENTRY.size:
        warnings.append("trailing_bytes")
    if payload_bytes % FOVEATED_SIMULATION_ENTRY.size != 0:
        warnings.append("trailing_partial_entry")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if any(entry.get("stateHashOk") is False for entry in entries):
        warnings.append("state_hash_mismatch")
    return {
        "type": "foveated_simulation_blackbox",
        "entrySize": FOVEATED_SIMULATION_ENTRY.size,
        "declaredEntryCount": entry_count,
        "cursor": cursor,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def compute_foveated_simulation_state_hash(
    target_count: int,
    frozen_entity_count: int,
    tier0_count: int,
    tier1_count: int,
    tier2_count: int,
) -> int:
    hash_value = 2166136261
    hash_value = fnv1a_mix_u32(hash_value, target_count)
    hash_value = fnv1a_mix_u32(hash_value, frozen_entity_count)
    hash_value = fnv1a_mix_u32(hash_value, tier0_count)
    hash_value = fnv1a_mix_u32(hash_value, tier1_count)
    hash_value = fnv1a_mix_u32(hash_value, tier2_count)
    return hash_value if hash_value != 0 else 1


def is_input_determinism_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPINPUTDETERMINISMBIN", "DUMPINPUTDETERMINISMH8DUMP"}


def parse_input_determinism_blackbox(data: bytes) -> dict[str, Any]:
    entry_size = 64
    if len(data) < entry_size:
        return {
            "type": "input_determinism_blackbox",
            "entrySize": entry_size,
            "entries": [],
            "latest": None,
            "warnings": ["truncated_payload"],
        }

    entry_count = len(data) // entry_size
    entries = []
    for slot in range(entry_count):
        offset = slot * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = INPUT_DETERMINISM_ENTRY.unpack_from(data, offset)
        flags = fields[8]
        labels, unknown_flags = resolve_bit_labels(flags, INPUT_DETERMINISM_FLAG_LABELS)
        scheme_hash = fields[4]
        entries.append(
            {
                "slot": slot,
                "inputSystemTimeSeconds": round(fields[0], 6),
                "frame": fields[1],
                "sequence": fields[2],
                "buttonMask": fields[3],
                "currentInputSchemeHash": scheme_hash,
                "currentInputScheme": INPUT_DETERMINISM_SCHEME_LABELS.get(scheme_hash, f"0x{scheme_hash:08X}"),
                "pollingTimeMicroseconds": fields[5],
                "bufferedInputsConsumed": fields[6],
                "hapticCommandsActive": fields[7],
                "flags": flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "automationOverride": bool(flags & 0x01),
                "delayApplied": bool(flags & 0x02),
                "nonFiniteSanitized": bool(flags & 0x04),
            }
        )

    entries.sort(key=lambda entry: (safe_int(entry.get("frame"), 0), safe_int(entry.get("sequence"), 0), safe_int(entry.get("slot"), 0)))
    latest = entries[-1] if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if any(entry.get("pollingTimeMicroseconds", 0) > 500 for entry in entries):
        warnings.append("polling_time_over_500us")
    if any(entry.get("nonFiniteSanitized") for entry in entries):
        warnings.append("nonfinite_sanitized")
    return {
        "type": "input_determinism_blackbox",
        "entrySize": entry_size,
        "declaredEntryCount": entry_count,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_origin_shift_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPORIGINSHIFTBIN", "DUMPORIGINSHIFTH8DUMP"}


def parse_origin_shift_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < ORIGIN_SHIFT_HEADER.size:
        return {
            "type": "origin_shift_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    (
        magic,
        version,
        header_bytes,
        entry_count,
        entry_stride,
        payload_bytes,
        oldest_ring_index,
        latest_frame,
        endian_tag,
        dump_flags,
        detail_stride,
        combined_record_bytes,
    ) = ORIGIN_SHIFT_HEADER.unpack_from(data, 0)

    if dump_flags & ORIGIN_SHIFT_FLAG_BIG_ENDIAN:
        return {
            "type": "origin_shift_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["unsupported_big_endian_dump"],
        }

    expected_combined_record_bytes = entry_stride + detail_stride
    if (
        magic != ORIGIN_SHIFT_MAGIC
        or version != ORIGIN_SHIFT_VERSION
        or header_bytes != ORIGIN_SHIFT_HEADER.size
        or entry_count == 0
        or entry_count > 100000
        or entry_stride != ORIGIN_SHIFT_BASE_ENTRY.size
        or detail_stride not in {0, ORIGIN_SHIFT_DETAIL_ENTRY.size}
        or combined_record_bytes != expected_combined_record_bytes
        or payload_bytes != entry_count * combined_record_bytes
        or endian_tag != ORIGIN_SHIFT_LITTLE_ENDIAN_TAG
        or len(data) < header_bytes + payload_bytes
    ):
        return {
            "type": "origin_shift_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    entries = []
    offset = header_bytes
    for row in range(entry_count):
        base_offset = offset
        detail_offset = offset + entry_stride
        offset += combined_record_bytes
        if is_empty_entry(data, base_offset, entry_stride):
            continue

        base = ORIGIN_SHIFT_BASE_ENTRY.unpack_from(data, base_offset)
        flags = base[12]
        labels, unknown_flags = resolve_bit_labels(flags, ORIGIN_SHIFT_TELEMETRY_FLAG_LABELS)
        entry = {
            "row": row,
            "shiftDelta": {"x": round(base[0], 4), "y": round(base[1], 4), "z": round(base[2], 4)},
            "frame": base[3],
            "rebaseCount": base[4],
            "shiftSequence": base[5],
            "sectorHash": base[6],
            "entitiesShifted": base[7],
            "historicalPointsShifted": base[8],
            "batchStartIndex": base[9],
            "batchCount": base[10],
            "nonFiniteCount": base[11],
            "flags": flags,
            "flagLabels": labels,
            "unknownFlags": unknown_flags,
            "nan": bool(flags & 0x01),
            "watchdog": bool(flags & 0x02),
            "timeSliced": bool(flags & 0x04),
            "frameSample": bool(flags & 0x08),
            "shiftCommit": bool(flags & 0x10),
        }
        if detail_stride == ORIGIN_SHIFT_DETAIL_ENTRY.size and not is_empty_entry(data, detail_offset, detail_stride):
            detail = ORIGIN_SHIFT_DETAIL_ENTRY.unpack_from(data, detail_offset)
            entry.update(
                {
                    "totalUniverseOffset": {"x": round(detail[0], 4), "y": round(detail[1], 4), "z": round(detail[2], 4)},
                    "cameraLocalPosition": {"x": round(detail[3], 4), "y": round(detail[4], 4), "z": round(detail[5], 4)},
                    "rebaseComputeTimeMs": round(detail[6], 4),
                    "systemHealthIndex01": round(detail[7], 4),
                    "cameraSectorHash": detail[8],
                    "positionHash": detail[9],
                    "hotEntitiesShifted": detail[10],
                }
            )
        entries.append(entry)

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) > header_bytes + payload_bytes:
        warnings.append("trailing_bytes")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if any(entry.get("nan") for entry in entries):
        warnings.append("nan_flag")
    if any(entry.get("watchdog") for entry in entries):
        warnings.append("watchdog_flag")
    return {
        "type": "origin_shift_blackbox",
        "version": version,
        "entrySize": entry_stride,
        "detailStrideBytes": detail_stride,
        "combinedRecordBytes": combined_record_bytes,
        "declaredEntryCount": entry_count,
        "oldestRingIndex": oldest_ring_index,
        "latestFrame": latest_frame,
        "dumpFlags": dump_flags,
        "hasDetailRows": bool(dump_flags & ORIGIN_SHIFT_FLAG_HAS_DETAIL_ROWS),
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_binary_layout_sentinel_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPBINARYLAYOUTSENTINELBIN", "DUMPBINARYLAYOUTSENTINELH8DUMP"}


def parse_binary_layout_sentinel(data: bytes) -> dict[str, Any]:
    if len(data) < BINARY_LAYOUT_SENTINEL_HEADER.size:
        return {
            "type": "binary_layout_sentinel",
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, context_hash, expected, observed, name_bytes, type_name_hash = BINARY_LAYOUT_SENTINEL_HEADER.unpack_from(data, 0)
    if (
        magic != BINARY_LAYOUT_SENTINEL_MAGIC
        or version != BINARY_LAYOUT_SENTINEL_VERSION
        or name_bytes < 0
        or name_bytes > BINARY_LAYOUT_SENTINEL_TYPE_NAME_MAX_BYTES
    ):
        return {
            "type": "binary_layout_sentinel",
            "version": version,
            "latest": None,
            "warnings": ["invalid_header"],
        }

    available_name_bytes = max(0, len(data) - BINARY_LAYOUT_SENTINEL_HEADER_BYTES)
    readable_name_bytes = min(name_bytes, available_name_bytes)
    raw_name = data[BINARY_LAYOUT_SENTINEL_HEADER_BYTES : BINARY_LAYOUT_SENTINEL_HEADER_BYTES + readable_name_bytes]
    type_name = raw_name.decode("ascii", errors="replace")
    entry = {
        "contextHash": context_hash,
        "expected": expected,
        "observed": observed,
        "nameBytes": name_bytes,
        "readableNameBytes": readable_name_bytes,
        "typeNameHash": type_name_hash,
        "typeName": type_name,
        "layoutMatches": expected == observed,
    }
    warnings = []
    if readable_name_bytes < name_bytes:
        warnings.append("truncated_type_name")
    if len(data) > BINARY_LAYOUT_SENTINEL_HEADER_BYTES + name_bytes:
        warnings.append("trailing_bytes")
    if expected != observed:
        warnings.append("layout_mismatch")
    return {
        "type": "binary_layout_sentinel",
        "version": version,
        "headerBytes": BINARY_LAYOUT_SENTINEL_HEADER_BYTES,
        "entrySize": BINARY_LAYOUT_SENTINEL_HEADER_BYTES + name_bytes,
        "returnedEntryCount": 1,
        "entries": [entry],
        "latest": entry,
        "warnings": warnings,
    }


def is_terminal_os_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMP1309TERMINALOSBIN",
        "DUMP1309TERMINALOSH8DUMP",
        "DUMP1309TERMINALOSMIRRORBIN",
        "DUMP1309TERMINALOSMIRRORH8DUMP",
    }


def is_terminal_decryption_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMP1309TERMINALDECRYPTIONBIN", "DUMP1309TERMINALDECRYPTIONH8DUMP"}


def parse_terminal_os_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < TERMINAL_OS_HEADER.size:
        return {
            "type": "terminal_os_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, fault_flags, count, ring_length, cursor, entry_size, source_hash = TERMINAL_OS_HEADER.unpack_from(data, 0)
    if (
        magic != TERMINAL_OS_MAGIC
        or version != TERMINAL_OS_VERSION
        or count <= 0
        or count > TERMINAL_BLACKBOX_FRAME_COUNT
        or ring_length <= 0
        or ring_length > 100000
        or count > ring_length
        or cursor < 0
        or cursor >= ring_length
        or entry_size != TERMINAL_OS_ENTRY.size
        or source_hash != TERMINAL_OS_SOURCE_HASH
    ):
        return {
            "type": "terminal_os_blackbox",
            "version": version,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    readable_entries = min(count, max(0, (len(data) - TERMINAL_OS_HEADER_BYTES) // entry_size))
    entries = []
    for index in range(readable_entries):
        offset = TERMINAL_OS_HEADER_BYTES + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = TERMINAL_OS_ENTRY.unpack_from(data, offset)
        entry_fault_flags = fields[7]
        entry_labels, entry_unknown_flags = resolve_bit_labels(entry_fault_flags, TERMINAL_OS_FAULT_FLAG_LABELS)
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "terminalCount": fields[1],
                "dirtyCount": fields[2],
                "dispatchedCount": fields[3],
                "formatMainThreadMilliseconds": round(fields[4], 4),
                "uploadMicroseconds": round(fields[5], 4),
                "dispatchMicroseconds": round(fields[6], 4),
                "faultFlags": entry_fault_flags,
                "faultLabels": entry_labels,
                "unknownFaultFlags": entry_unknown_flags,
                "layoutHash": fields[8],
                "hoveredTerminalHash": fields[9],
                "lastPower01": round(fields[10], 4),
                "lastDamage01": round(fields[11], 4),
                "evaluatedTerminals": fields[12],
                "framesBetweenUpdates": fields[13],
                "intersectionMicroseconds": round(fields[14], 4),
                "globalQualityWeight": round(fields[15], 4),
            }
        )

    dump_labels, dump_unknown_flags = resolve_bit_labels(fault_flags, TERMINAL_OS_FAULT_FLAG_LABELS)
    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if readable_entries < count:
        warnings.append("payload_truncated")
    if len(data) > TERMINAL_OS_HEADER_BYTES + count * entry_size:
        warnings.append("trailing_bytes")
    if len(data) - TERMINAL_OS_HEADER_BYTES > 0 and (len(data) - TERMINAL_OS_HEADER_BYTES) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if dump_unknown_flags or any(entry.get("unknownFaultFlags") for entry in entries):
        warnings.append("unknown_flags")
    if any(entry.get("faultFlags") for entry in entries) or fault_flags:
        warnings.append("fault_flags")
    return {
        "type": "terminal_os_blackbox",
        "version": version,
        "headerBytes": TERMINAL_OS_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": count,
        "ringLength": ring_length,
        "cursor": cursor,
        "sourceHash": source_hash,
        "dumpFaultFlags": fault_flags,
        "dumpFaultLabels": dump_labels,
        "dumpUnknownFaultFlags": dump_unknown_flags,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def resolve_terminal_decryption_flags(flags: int) -> tuple[list[str], int, int]:
    labels, unknown = resolve_bit_labels(flags & ~TERMINAL_DECRYPTION_HOLD_FRAME_MASK, TERMINAL_DECRYPTION_FLAG_LABELS)
    hold_frames = (flags & TERMINAL_DECRYPTION_HOLD_FRAME_MASK) >> TERMINAL_DECRYPTION_HOLD_FRAME_SHIFT
    if hold_frames:
        labels = [label for label in labels if label != "none"]
        labels.append(f"hold={hold_frames}")
    if not labels:
        labels = ["none"]
    unknown |= flags & ~(TERMINAL_DECRYPTION_HOLD_FRAME_MASK | sum(bit for bit, _, _ in TERMINAL_DECRYPTION_FLAG_LABELS))
    return labels, unknown, hold_frames


def parse_terminal_decryption_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < TERMINAL_DECRYPTION_HEADER.size:
        return {
            "type": "terminal_decryption_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, fault_flags, cursor, count, entry_size = TERMINAL_DECRYPTION_HEADER.unpack_from(data, 0)
    if (
        magic != TERMINAL_DECRYPTION_MAGIC
        or version != TERMINAL_DECRYPTION_VERSION
        or count <= 0
        or count > TERMINAL_BLACKBOX_FRAME_COUNT
        or cursor < 0
        or entry_size != TERMINAL_DECRYPTION_ENTRY.size
    ):
        return {
            "type": "terminal_decryption_blackbox",
            "version": version,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    readable_entries = min(count, max(0, (len(data) - TERMINAL_DECRYPTION_HEADER_BYTES) // entry_size))
    entries = []
    for index in range(readable_entries):
        offset = TERMINAL_DECRYPTION_HEADER_BYTES + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = TERMINAL_DECRYPTION_ENTRY.unpack_from(data, offset)
        flags = fields[8]
        flag_labels, unknown_flags, hold_frames = resolve_terminal_decryption_flags(flags)
        entry_fault_flags = fields[11]
        fault_labels, unknown_fault_flags = resolve_bit_labels(entry_fault_flags, TERMINAL_OS_FAULT_FLAG_LABELS)
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "puzzleID": fields[1],
                "playerFrequency": round(fields[2], 4),
                "playerPhase": round(fields[3], 4),
                "targetFrequency": round(fields[4], 4),
                "targetPhase": round(fields[5], 4),
                "alignmentAccuracy01": round(fields[6], 4),
                "burstMicroseconds": round(fields[7], 4),
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "holdFrames": hold_frames,
                "nodeHash": fields[9],
                "terminalHash": fields[10],
                "faultFlags": entry_fault_flags,
                "faultLabels": fault_labels,
                "unknownFaultFlags": unknown_fault_flags,
            }
        )

    dump_labels, dump_unknown_flags = resolve_bit_labels(fault_flags, TERMINAL_OS_FAULT_FLAG_LABELS)
    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if readable_entries < count:
        warnings.append("payload_truncated")
    if len(data) > TERMINAL_DECRYPTION_HEADER_BYTES + count * entry_size:
        warnings.append("trailing_bytes")
    if len(data) - TERMINAL_DECRYPTION_HEADER_BYTES > 0 and (len(data) - TERMINAL_DECRYPTION_HEADER_BYTES) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if dump_unknown_flags or any(entry.get("unknownFaultFlags") or entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if any(entry.get("faultFlags") for entry in entries) or fault_flags:
        warnings.append("fault_flags")
    if any(entry.get("alignmentAccuracy01", 1.0) >= 0.98 for entry in entries):
        warnings.append("solve_threshold_reached")
    return {
        "type": "terminal_decryption_blackbox",
        "version": version,
        "headerBytes": TERMINAL_DECRYPTION_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": count,
        "cursor": cursor,
        "dumpFaultFlags": fault_flags,
        "dumpFaultLabels": dump_labels,
        "dumpUnknownFaultFlags": dump_unknown_flags,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_terminal_projection_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMP1309TERMINALPROJECTIONBIN", "DUMP1309TERMINALPROJECTIONH8DUMP"}


def parse_terminal_projection_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < TERMINAL_PROJECTION_HEADER.size:
        return {
            "type": "terminal_projection_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    (
        magic,
        version,
        fault_flags,
        cursor,
        entry_count,
        entry_stride,
        input_state_stride,
        rollback_excluded,
    ) = TERMINAL_PROJECTION_HEADER.unpack_from(data, 0)
    if (
        magic != TERMINAL_PROJECTION_MAGIC
        or version != TERMINAL_PROJECTION_VERSION
        or entry_count <= 0
        or entry_count > TERMINAL_BLACKBOX_FRAME_COUNT
        or cursor >= entry_count
        or entry_stride != TERMINAL_PROJECTION_ENTRY_BYTES
        or input_state_stride != TERMINAL_PROJECTION_INPUT_STATE_STRIDE_BYTES
        or rollback_excluded != TERMINAL_PROJECTION_ROLLBACK_EXCLUDED
    ):
        return {
            "type": "terminal_projection_blackbox",
            "version": version,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    readable_entries = min(entry_count, max(0, (len(data) - TERMINAL_PROJECTION_HEADER_BYTES) // entry_stride))
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = TERMINAL_PROJECTION_HEADER_BYTES + index * entry_stride
        if is_empty_entry(data, offset, entry_stride):
            continue

        fields = TERMINAL_PROJECTION_ENTRY.unpack_from(data, offset)
        entry_fault_flags = fields[7]
        labels, unknown_flags = resolve_bit_labels(entry_fault_flags, TERMINAL_PROJECTION_FAULT_FLAG_LABELS)
        float_values = (fields[4], fields[5], fields[6], fields[11], fields[12])
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "evaluatedTerminals": fields[1],
                "successfulProjections": fields[2],
                "signalsDispatched": fields[3],
                "burstMicroseconds": round(fields[4], 4),
                "evalRadiusMeters": round(fields[5], 4),
                "globalQualityWeight": round(fields[6], 4),
                "faultFlags": entry_fault_flags,
                "faultLabels": labels,
                "unknownFaultFlags": unknown_flags,
                "hotPathAllocBytes": fields[8],
                "rollbackExcluded": fields[9],
                "lastHoveredTerminalHash": fields[10],
                "cursorSnappingTolerance": round(fields[11], 6),
                "raycastThickness": round(fields[12], 6),
                "nonFiniteCount": fields[13],
            }
        )

    dump_labels, dump_unknown_flags = resolve_bit_labels(fault_flags, TERMINAL_PROJECTION_FAULT_FLAG_LABELS)
    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    expected_bytes = TERMINAL_PROJECTION_HEADER_BYTES + entry_count * entry_stride
    if readable_entries < entry_count:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) - TERMINAL_PROJECTION_HEADER_BYTES > 0 and (len(data) - TERMINAL_PROJECTION_HEADER_BYTES) % entry_stride != 0:
        warnings.append("trailing_partial_entry")
    if dump_unknown_flags or any(entry.get("unknownFaultFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen or fault_flags & (1 << 16) or any(entry.get("faultFlags", 0) & (1 << 16) or entry.get("nonFiniteCount", 0) for entry in entries):
        warnings.append("projection_nonfinite")
    if fault_flags & (1 << 17) or any(entry.get("faultFlags", 0) & (1 << 17) for entry in entries):
        warnings.append("projection_budget")
    if fault_flags & (1 << 18) or any(entry.get("faultFlags", 0) & (1 << 18) for entry in entries):
        warnings.append("projection_layout")
    return {
        "type": "terminal_projection_blackbox",
        "version": version,
        "headerBytes": TERMINAL_PROJECTION_HEADER_BYTES,
        "entrySize": entry_stride,
        "declaredEntryCount": entry_count,
        "cursor": cursor,
        "inputStateStrideBytes": input_state_stride,
        "rollbackExcluded": rollback_excluded,
        "dumpFaultFlags": fault_flags,
        "dumpFaultLabels": dump_labels,
        "dumpUnknownFaultFlags": dump_unknown_flags,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_openxr_manual_override_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMP1335OPENXRMANUALOVERRIDELEVERBIN", "DUMP1335OPENXRMANUALOVERRIDELEVERH8DUMP"}


def parse_openxr_manual_override_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < OPENXR_MANUAL_OVERRIDE_HEADER.size:
        return {
            "type": "openxr_manual_override_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    frame_count, write_index = OPENXR_MANUAL_OVERRIDE_HEADER.unpack_from(data, 0)
    if (
        frame_count <= 0
        or frame_count > OPENXR_MANUAL_OVERRIDE_FRAME_COUNT
        or write_index < 0
        or write_index >= frame_count
    ):
        return {
            "type": "openxr_manual_override_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = OPENXR_MANUAL_OVERRIDE_HEADER.size
    readable_slots = min(frame_count, max(0, (len(data) - payload_offset) // OPENXR_MANUAL_OVERRIDE_ENTRY_BYTES))
    slot_order = list(range(write_index, readable_slots)) + list(range(0, min(write_index, readable_slots)))
    entries = []
    nonfinite_seen = False
    for slot in slot_order:
        offset = payload_offset + slot * OPENXR_MANUAL_OVERRIDE_ENTRY_BYTES
        if is_empty_entry(data, offset, OPENXR_MANUAL_OVERRIDE_ENTRY_BYTES):
            continue

        fields = OPENXR_MANUAL_OVERRIDE_ENTRY.unpack_from(data, offset)
        flags = fields[10]
        labels, unknown_flags = resolve_bit_labels(flags, OPENXR_MANUAL_OVERRIDE_FLAG_LABELS)
        float_values = fields[:9]
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": slot,
                "handLocalPosition": {"x": round(fields[0], 4), "y": round(fields[1], 4), "z": round(fields[2], 4)},
                "pivotLocalPosition": {"x": round(fields[3], 4), "y": round(fields[4], 4), "z": round(fields[5], 4)},
                "angleDegrees": round(fields[6], 4),
                "targetAngleDegrees": round(fields[7], 4),
                "velocityDegreesPerSecond": round(fields[8], 4),
                "frame": fields[9],
                "flags": flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "grabbed": bool(flags & 0x01),
                "latched": bool(flags & 0x02),
                "ikPressure": bool(flags & 0x04),
                "xrActive": bool(flags & 0x08),
                "projectionSingular": bool(flags & 0x10),
                "blackBoxDumped": bool(flags & 0x20),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if readable_slots < frame_count:
        warnings.append("payload_truncated")
    if len(data) > payload_offset + frame_count * OPENXR_MANUAL_OVERRIDE_ENTRY_BYTES:
        warnings.append("trailing_bytes")
    if len(data) - payload_offset > 0 and (len(data) - payload_offset) % OPENXR_MANUAL_OVERRIDE_ENTRY_BYTES != 0:
        warnings.append("trailing_partial_entry")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("projectionSingular") for entry in entries):
        warnings.append("projection_singular")
    return {
        "type": "openxr_manual_override_blackbox",
        "entrySize": OPENXR_MANUAL_OVERRIDE_ENTRY_BYTES,
        "declaredEntryCount": frame_count,
        "writeIndex": write_index,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_vehicle_damage_holographer_dump_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMPVEHICLESUBOSDAMAGEHOLOGRAPHERBIN",
        "DUMPVEHICLESUBOSDAMAGEHOLOGRAPHERH8DUMP",
    }


def parse_vehicle_damage_holographer_dump(data: bytes) -> dict[str, Any]:
    if len(data) < VEHICLE_DAMAGE_HOLOGRAPHER_HEADER.size:
        return {
            "type": "vehicle_damage_holographer_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, count, entry_size, write_index = VEHICLE_DAMAGE_HOLOGRAPHER_HEADER.unpack_from(data, 0)
    if (
        magic != VEHICLE_DAMAGE_HOLOGRAPHER_MAGIC
        or count <= 0
        or count > VEHICLE_COCKPIT_TELEMETRY_CAPACITY
        or entry_size != VEHICLE_DAMAGE_HOLOGRAPHER_ENTRY_BYTES
        or write_index < 0
        or write_index >= count
    ):
        return {
            "type": "vehicle_damage_holographer_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = VEHICLE_DAMAGE_HOLOGRAPHER_HEADER.size
    readable_entries = min(count, max(0, (len(data) - payload_offset) // entry_size))
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = VEHICLE_DAMAGE_HOLOGRAPHER_ENTRY.unpack_from(data, offset)
        flags = fields[5]
        labels, unknown_flags = resolve_bit_labels(flags, VEHICLE_DAMAGE_HOLOGRAPHER_FLAG_LABELS)
        if not math.isfinite(fields[3]) or not math.isfinite(fields[4]):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "holoDamagePoints": fields[1],
                "holoProxyVertices": fields[2],
                "holoFlicker": round(fields[3], 4),
                "holoFlood01": round(fields[4], 4),
                "holoFlags": flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "resourcesReady": bool(flags & 0x01),
                "cheapVisual": bool(flags & 0x02),
                "activeDent": bool(flags & 0x04),
                "flicker": bool(flags & 0x08),
                "flood": bool(flags & 0x10),
                "fallbackWarning": bool(flags & 0x20),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if readable_entries < count:
        warnings.append("payload_truncated")
    if len(data) > payload_offset + count * entry_size:
        warnings.append("trailing_bytes")
    if len(data) - payload_offset > 0 and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("fallbackWarning") for entry in entries):
        warnings.append("fallback_warning")
    if any(entry.get("flood") for entry in entries):
        warnings.append("flood_active")
    return {
        "type": "vehicle_damage_holographer_blackbox",
        "entrySize": entry_size,
        "declaredEntryCount": count,
        "writeIndex": write_index,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_pda_projection_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMP1335UIPRESENTATIONPDAPROJECTIONBIN",
        "DUMP1335UIPRESENTATIONPDAPROJECTIONH8DUMP",
    }


def q16_to_float(value: int) -> float:
    return round(value / 65536.0, 4)


def parse_pda_projection_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < PDA_PROJECTION_HEADER.size:
        return {
            "type": "pda_projection_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    (
        magic,
        version,
        frame_index,
        flags,
        telemetry_capacity,
        telemetry_cursor,
        entry_size,
        payload_bytes,
        valid_count,
        start_index,
    ) = PDA_PROJECTION_HEADER.unpack_from(data, 0)
    if (
        magic != PDA_PROJECTION_MAGIC
        or version != PDA_PROJECTION_VERSION
        or telemetry_capacity <= 0
        or telemetry_capacity > PDA_PROJECTION_TELEMETRY_CAPACITY
        or telemetry_cursor < 0
        or telemetry_cursor > telemetry_capacity
        or entry_size != PDA_PROJECTION_ENTRY_BYTES
        or payload_bytes < 0
        or valid_count < 0
        or valid_count > telemetry_capacity
        or start_index < 0
        or start_index >= telemetry_capacity
        or payload_bytes != valid_count * entry_size
    ):
        return {
            "type": "pda_projection_blackbox",
            "version": version,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    readable_entries = min(valid_count, max(0, (len(data) - PDA_PROJECTION_HEADER_BYTES) // entry_size))
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = PDA_PROJECTION_HEADER_BYTES + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = PDA_PROJECTION_ENTRY.unpack_from(data, offset)
        entry_flags = fields[1]
        pda_flags = fields[15]
        labels, unknown_flags = resolve_bit_labels(entry_flags, PDA_PROJECTION_FLAG_LABELS)
        pda_labels, pda_unknown_flags = resolve_bit_labels(pda_flags, PDA_PROJECTION_FLAG_LABELS)
        float_values = (fields[4], fields[5], fields[6], fields[10], fields[11], fields[12], fields[13], fields[14])
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "flags": entry_flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "activeTabHashID": fields[2],
                "jobMicrosecondsQ16": fields[3],
                "jobMicroseconds": q16_to_float(fields[3]),
                "localizedDistanceMeters": round(fields[4], 4),
                "bootSequenceProgress01": round(fields[5], 4),
                "qualityWeight01": round(fields[6], 4),
                "telemetryCursor": fields[7],
                "matrixHash": fields[8],
                "profileHash": fields[9],
                "screenWidthMeters": round(fields[10], 4),
                "screenHeightMeters": round(fields[11], 4),
                "glassRefractionIndex": round(fields[12], 4),
                "screenCurvatureScalar": round(fields[13], 4),
                "globalQualityWeight01": round(fields[14], 4),
                "pdaFlags": pda_flags,
                "pdaFlagLabels": pda_labels,
                "pdaUnknownFlags": pda_unknown_flags,
            }
        )

    dump_labels, dump_unknown_flags = resolve_bit_labels(flags, PDA_PROJECTION_FLAG_LABELS)
    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if readable_entries < valid_count:
        warnings.append("payload_truncated")
    if len(data) > PDA_PROJECTION_HEADER_BYTES + payload_bytes:
        warnings.append("trailing_bytes")
    if len(data) - PDA_PROJECTION_HEADER_BYTES > 0 and (len(data) - PDA_PROJECTION_HEADER_BYTES) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if dump_unknown_flags or any(entry.get("unknownFlags") or entry.get("pdaUnknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen or flags & (1 << 2) or any(entry.get("flags", 0) & (1 << 2) or entry.get("pdaFlags", 0) & (1 << 2) for entry in entries):
        warnings.append("nonfinite")
    if flags & (1 << 3) or any(entry.get("flags", 0) & (1 << 3) or entry.get("pdaFlags", 0) & (1 << 3) for entry in entries):
        warnings.append("over_budget")
    if flags & (1 << 6) or any(entry.get("flags", 0) & (1 << 6) or entry.get("pdaFlags", 0) & (1 << 6) for entry in entries):
        warnings.append("gpu_upload_fault")
    return {
        "type": "pda_projection_blackbox",
        "version": version,
        "headerBytes": PDA_PROJECTION_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": valid_count,
        "telemetryCapacity": telemetry_capacity,
        "telemetryCursor": telemetry_cursor,
        "telemetryStartIndex": start_index,
        "frameIndex": frame_index,
        "dumpFlags": flags,
        "dumpFlagLabels": dump_labels,
        "dumpUnknownFlags": dump_unknown_flags,
        "payloadBytes": payload_bytes,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_wrist_hud_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMP1335WRISTHOLOGRAMHUDBIN", "DUMP1335WRISTHOLOGRAMHUDH8DUMP"}


def parse_wrist_hud_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < WRIST_HUD_HEADER.size:
        return {
            "type": "wrist_hud_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, frame_index, flags, telemetry_capacity, telemetry_cursor, entry_size, payload_bytes = WRIST_HUD_HEADER.unpack_from(data, 0)
    if (
        magic != WRIST_HUD_MAGIC
        or version != WRIST_HUD_VERSION
        or telemetry_capacity <= 0
        or telemetry_capacity > WRIST_HUD_TELEMETRY_CAPACITY
        or telemetry_cursor < 0
        or telemetry_cursor > telemetry_capacity
        or entry_size != WRIST_HUD_ENTRY_BYTES
        or payload_bytes != telemetry_capacity * entry_size
    ):
        return {
            "type": "wrist_hud_blackbox",
            "version": version,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    readable_slots = min(telemetry_capacity, max(0, (len(data) - WRIST_HUD_HEADER_BYTES) // entry_size))
    normalized_cursor = 0 if telemetry_cursor >= readable_slots and readable_slots > 0 else telemetry_cursor
    slot_order = list(range(normalized_cursor, readable_slots)) + list(range(0, min(normalized_cursor, readable_slots)))
    entries = []
    nonfinite_seen = False
    for slot in slot_order:
        offset = WRIST_HUD_HEADER_BYTES + slot * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = WRIST_HUD_ENTRY.unpack_from(data, offset)
        entry_flags = fields[2]
        labels, unknown_flags = resolve_bit_labels(entry_flags, WRIST_HUD_FLAG_LABELS)
        float_values = fields[8:]
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": slot,
                "frame": fields[0],
                "stateHash": fields[1],
                "flags": entry_flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "activeQuadCount": fields[3],
                "glyphQuadCount": fields[4],
                "radarCount": fields[5],
                "jobMicrosecondsQ16": fields[6],
                "jobMicroseconds": q16_to_float(fields[6]),
                "telemetryCursor": fields[7],
                "oxygen01": round(fields[8], 4),
                "depthMeters": round(fields[9], 4),
                "safeDepthMeters": round(fields[10], 4),
                "radiation01": round(fields[11], 4),
                "toxemia01": round(fields[12], 4),
                "attentionDot": round(fields[13], 4),
                "headingDegrees": round(fields[14], 4),
                "pdaOpen01": round(fields[15], 4),
            }
        )

    dump_labels, dump_unknown_flags = resolve_bit_labels(flags, WRIST_HUD_FLAG_LABELS)
    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if readable_slots < telemetry_capacity:
        warnings.append("payload_truncated")
    if len(data) > WRIST_HUD_HEADER_BYTES + payload_bytes:
        warnings.append("trailing_bytes")
    if len(data) - WRIST_HUD_HEADER_BYTES > 0 and (len(data) - WRIST_HUD_HEADER_BYTES) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if dump_unknown_flags or any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen or flags & (1 << 4) or any(entry.get("flags", 0) & (1 << 4) for entry in entries):
        warnings.append("nan_detected")
    if flags & (1 << 3) or any(entry.get("flags", 0) & (1 << 3) for entry in entries):
        warnings.append("job_over_budget")
    if flags & (1 << 7) or any(entry.get("flags", 0) & (1 << 7) for entry in entries):
        warnings.append("gpu_upload_fault")
    return {
        "type": "wrist_hud_blackbox",
        "version": version,
        "headerBytes": WRIST_HUD_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": telemetry_capacity,
        "telemetryCursor": telemetry_cursor,
        "frameIndex": frame_index,
        "dumpFlags": flags,
        "dumpFlagLabels": dump_labels,
        "dumpUnknownFlags": dump_unknown_flags,
        "payloadBytes": payload_bytes,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_ladder_climb_ik_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPLADDERCLIMBIKBIN", "DUMPLADDERCLIMBIKH8DUMP"}


def parse_ladder_climb_ik_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < LADDER_CLIMB_IK_HEADER.size:
        return {
            "type": "ladder_climb_ik_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, retained_count, entry_size, cursor, start = LADDER_CLIMB_IK_HEADER.unpack_from(data, 0)
    if (
        magic != LADDER_CLIMB_IK_MAGIC
        or version != LADDER_CLIMB_IK_VERSION
        or retained_count <= 0
        or retained_count > LADDER_CLIMB_IK_FRAME_CAPACITY
        or entry_size != LADDER_CLIMB_IK_ENTRY_BYTES
        or cursor >= LADDER_CLIMB_IK_FRAME_CAPACITY
        or start >= LADDER_CLIMB_IK_FRAME_CAPACITY
    ):
        return {
            "type": "ladder_climb_ik_blackbox",
            "version": version,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    readable_entries = min(retained_count, max(0, (len(data) - LADDER_CLIMB_IK_HEADER_BYTES) // entry_size))
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = LADDER_CLIMB_IK_HEADER_BYTES + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = LADDER_CLIMB_IK_ENTRY_PREFIX.unpack_from(data, offset)
        flags = fields[21]
        labels, unknown_flags = resolve_bit_labels(flags, LADDER_CLIMB_IK_FLAG_LABELS)
        float_values = fields[:17]
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "playerRoot": {"x": round(fields[0], 4), "y": round(fields[1], 4), "z": round(fields[2], 4)},
                "leftHandTarget": {"x": round(fields[3], 4), "y": round(fields[4], 4), "z": round(fields[5], 4)},
                "rightHandTarget": {"x": round(fields[6], 4), "y": round(fields[7], 4), "z": round(fields[8], 4)},
                "leftElbowTarget": {"x": round(fields[9], 4), "y": round(fields[10], 4), "z": round(fields[11], 4)},
                "rightElbowTarget": {"x": round(fields[12], 4), "y": round(fields[13], 4), "z": round(fields[14], 4)},
                "progressMeters": round(fields[15], 4),
                "stamina01": round(fields[16], 4),
                "leftRungIndex": fields[17],
                "rightRungIndex": fields[18],
                "frame": fields[19],
                "hash": fields[20],
                "flags": flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "active": bool(flags & 0x01),
                "cameraSlideFake": bool(flags & 0x02),
                "vrGrip": bool(flags & 0x04),
                "slip": bool(flags & 0x08),
                "invalidInput": bool(flags & 0x10),
                "leftLocked": bool(flags & 0x20),
                "rightLocked": bool(flags & 0x40),
                "unreachable": bool(flags & 0x80),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    expected_bytes = LADDER_CLIMB_IK_HEADER_BYTES + retained_count * entry_size
    if readable_entries < retained_count:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) - LADDER_CLIMB_IK_HEADER_BYTES > 0 and (len(data) - LADDER_CLIMB_IK_HEADER_BYTES) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen or any(entry.get("invalidInput") for entry in entries):
        warnings.append("invalid_input")
    if any(entry.get("slip") for entry in entries):
        warnings.append("slip")
    if any(entry.get("unreachable") for entry in entries):
        warnings.append("unreachable")
    return {
        "type": "ladder_climb_ik_blackbox",
        "version": version,
        "headerBytes": LADDER_CLIMB_IK_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": retained_count,
        "cursor": cursor,
        "start": start,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_topographical_sonar_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPSONARSYNTHESIZERBIN", "DUMPSONARSYNTHESIZERH8DUMP"}


def parse_topographical_sonar_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < TOPOGRAPHICAL_SONAR_HEADER.size:
        return {
            "type": "topographical_sonar_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, entry_count, entry_stride, write_index, last_flags, active_point_count, sequence = TOPOGRAPHICAL_SONAR_HEADER.unpack_from(data, 0)
    if (
        magic != TOPOGRAPHICAL_SONAR_MAGIC
        or version != TOPOGRAPHICAL_SONAR_VERSION
        or entry_count <= 0
        or entry_count > TOPOGRAPHICAL_SONAR_TELEMETRY_FRAMES
        or entry_stride != TOPOGRAPHICAL_SONAR_ENTRY_BYTES
        or write_index >= TOPOGRAPHICAL_SONAR_TELEMETRY_FRAMES
    ):
        return {
            "type": "topographical_sonar_blackbox",
            "version": version,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    readable_entries = min(entry_count, max(0, (len(data) - TOPOGRAPHICAL_SONAR_HEADER_BYTES) // entry_stride))
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = TOPOGRAPHICAL_SONAR_HEADER_BYTES + index * entry_stride
        if is_empty_entry(data, offset, entry_stride):
            continue

        fields = TOPOGRAPHICAL_SONAR_ENTRY.unpack_from(data, offset)
        flags = fields[12]
        labels, unknown_flags = resolve_bit_labels(flags, TOPOGRAPHICAL_SONAR_FLAG_LABELS)
        float_values = fields[0:7] + fields[13:23]
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "timeSeconds": round(fields[0], 6),
                "pingAup": {"x": round(fields[1], 4), "y": round(fields[2], 4), "z": round(fields[3], 4)},
                "cameraAup": {"x": round(fields[4], 4), "y": round(fields[5], 4), "z": round(fields[6], 4)},
                "frame": fields[7],
                "sequence": fields[8],
                "requestedRayCount": fields[9],
                "activePointCount": fields[10],
                "hitCount": fields[11],
                "flags": flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "globalQualityWeight": round(fields[13], 4),
                "maxDistanceMeters": round(fields[14], 4),
                "pingOriginCameraLocal": {"x": round(fields[15], 4), "y": round(fields[16], 4), "z": round(fields[17], 4)},
                "sdfOriginRuntime": {"x": round(fields[18], 4), "y": round(fields[19], 4), "z": round(fields[20], 4)},
                "sdfRangeMeters": round(fields[21], 4),
                "stepMeters": round(fields[22], 4),
                "sdfVersion": fields[23],
                "computeTimeMicroseconds": fields[24],
            }
        )

    dump_labels, dump_unknown_flags = resolve_bit_labels(last_flags, TOPOGRAPHICAL_SONAR_FLAG_LABELS)
    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    expected_bytes = TOPOGRAPHICAL_SONAR_HEADER_BYTES + entry_count * entry_stride
    if readable_entries < entry_count:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) - TOPOGRAPHICAL_SONAR_HEADER_BYTES > 0 and (len(data) - TOPOGRAPHICAL_SONAR_HEADER_BYTES) % entry_stride != 0:
        warnings.append("trailing_partial_entry")
    if dump_unknown_flags or any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if last_flags & (1 << 31) or any(entry.get("flags", 0) & (1 << 31) for entry in entries):
        warnings.append("fault")
    if last_flags & (1 << 1) or any(entry.get("flags", 0) & (1 << 1) for entry in entries):
        warnings.append("sdf_unavailable")
    if last_flags & (1 << 2) or any(entry.get("flags", 0) & (1 << 2) for entry in entries):
        warnings.append("gpu_upload")
    return {
        "type": "topographical_sonar_blackbox",
        "version": version,
        "headerBytes": TOPOGRAPHICAL_SONAR_HEADER_BYTES,
        "entrySize": entry_stride,
        "declaredEntryCount": entry_count,
        "writeIndex": write_index,
        "lastFlags": last_flags,
        "lastFlagLabels": dump_labels,
        "lastUnknownFlags": dump_unknown_flags,
        "activePointCount": active_point_count,
        "sequence": sequence,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def parse_animation_ring_telemetry_blackbox(
    data: bytes,
    *,
    type_name: str,
    header_struct: struct.Struct,
    entry_struct: struct.Struct,
    expected_magic: int,
    expected_version: int,
    header_bytes: int,
    entry_bytes: int,
    capacity: int,
    hash_seed,
    decode_entry,
) -> dict[str, Any]:
    if len(data) < header_struct.size:
        return {
            "type": type_name,
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, snapshot_count, snapshot_cursor, entry_size, dump_hash = header_struct.unpack_from(data, 0)
    if (
        magic != expected_magic
        or version != expected_version
        or snapshot_count <= 0
        or snapshot_count > capacity
        or entry_size != entry_bytes
        or entry_struct.size > entry_size
    ):
        return {
            "type": type_name,
            "version": version,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    readable_entries = min(snapshot_count, max(0, (len(data) - header_bytes) // entry_size))
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = header_bytes + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        entry, entry_nonfinite = decode_entry(index, entry_struct.unpack_from(data, offset))
        entries.append(entry)
        nonfinite_seen |= entry_nonfinite

    expected_bytes = header_bytes + snapshot_count * entry_size
    computed_hash = None
    dump_hash_ok = None
    if readable_entries == snapshot_count and len(data) >= expected_bytes:
        computed_hash = fnv1a_mix_bytes(hash_seed(snapshot_count, snapshot_cursor), data[header_bytes:expected_bytes])
        computed_hash = 2166136261 if computed_hash == 0 else computed_hash
        dump_hash_ok = computed_hash == dump_hash

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if readable_entries < snapshot_count:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) - header_bytes > 0 and (len(data) - header_bytes) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if dump_hash_ok is False:
        warnings.append("dump_hash_mismatch")

    return {
        "type": type_name,
        "version": version,
        "headerBytes": header_bytes,
        "entrySize": entry_size,
        "declaredEntryCount": snapshot_count,
        "snapshotCursor": snapshot_cursor,
        "dumpHash": dump_hash,
        "computedDumpHash": computed_hash,
        "dumpHashOk": dump_hash_ok,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_kinetic_character_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMP1403KINETICCHARACTERBIN", "DUMP1403KINETICCHARACTERH8DUMP"}


def decode_kinetic_character_entry(index: int, fields: tuple[Any, ...]) -> tuple[dict[str, Any], bool]:
    flags = fields[11]
    labels, unknown_flags = resolve_bit_labels(flags, KINETIC_CHARACTER_FLAG_LABELS)
    float_values = fields[3:6] + fields[8:10] + (fields[12],)
    return (
        {
            "slot": index,
            "rootSector": {"x": fields[0], "y": fields[1], "z": fields[2]},
            "rootLocal": {"x": round(fields[3], 4), "y": round(fields[4], 4), "z": round(fields[5], 4)},
            "frame": fields[6],
            "bonesEvaluated": fields[7],
            "averageIkIterations": round(fields[8], 4),
            "cpuTimeMicroseconds": round(fields[9], 4),
            "stateHash": fields[10],
            "flags": flags,
            "flagLabels": labels,
            "unknownFlags": unknown_flags,
            "globalQualityWeight": round(fields[12], 4),
            "visible": bool(flags & 0x01),
            "mock": bool(flags & 0x02),
            "sdfBrace": bool(flags & 0x04),
            "playerKinematicsTargets": bool(flags & 0x08),
            "toolAligned": bool(flags & 0x10),
            "damageFlinch": bool(flags & 0x20),
            "qualityCollapsed": bool(flags & 0x40),
            "invalid": bool(flags & 0x80000000),
        },
        any(not math.isfinite(value) for value in float_values),
    )


def parse_kinetic_character_blackbox(data: bytes) -> dict[str, Any]:
    result = parse_animation_ring_telemetry_blackbox(
        data,
        type_name="kinetic_character_blackbox",
        header_struct=KINETIC_CHARACTER_HEADER,
        entry_struct=KINETIC_CHARACTER_ENTRY,
        expected_magic=KINETIC_CHARACTER_MAGIC,
        expected_version=KINETIC_CHARACTER_VERSION,
        header_bytes=KINETIC_CHARACTER_HEADER_BYTES,
        entry_bytes=KINETIC_CHARACTER_ENTRY_BYTES,
        capacity=KINETIC_CHARACTER_TELEMETRY_CAPACITY,
        hash_seed=lambda snapshot_count, _snapshot_cursor: (2166136261 ^ snapshot_count) & 0xFFFFFFFF,
        decode_entry=decode_kinetic_character_entry,
    )
    entries = result.get("entries", [])
    warnings = result.setdefault("warnings", [])
    if any(entry.get("invalid") for entry in entries):
        warnings.append("invalid")
    if any(entry.get("qualityCollapsed") for entry in entries):
        warnings.append("quality_collapsed")
    return result


def is_procedural_bone_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMP1403PROCEDURALBONEBIN", "DUMP1403PROCEDURALBONEH8DUMP"}


def decode_procedural_bone_entry(index: int, fields: tuple[Any, ...]) -> tuple[dict[str, Any], bool]:
    flags = fields[6]
    labels, unknown_flags = resolve_bit_labels(flags, PROCEDURAL_BONE_FLAG_LABELS)
    float_values = fields[4:5] + fields[7:10] + fields[12:15]
    return (
        {
            "slot": index,
            "frame": fields[0],
            "activeSkeletons": fields[1],
            "matricesComputed": fields[2],
            "matrixUploadCount": fields[3],
            "kinematicComputeTimeMs": round(fields[4], 4),
            "stateHash": fields[5],
            "flags": flags,
            "flagLabels": labels,
            "unknownFlags": unknown_flags,
            "globalQualityWeight": round(fields[7], 4),
            "maxWaveSpeed": round(fields[8], 4),
            "averageActiveBones": round(fields[9], 4),
            "invalidMathCount": fields[10],
            "culledSkeletons": fields[11],
            "lastRootLocal": {"x": round(fields[12], 4), "y": round(fields[13], 4), "z": round(fields[14], 4)},
            "visible": bool(flags & 0x01),
            "qualityCollapse": bool(flags & 0x02),
            "jawSolved": bool(flags & 0x04),
            "mockSignal": bool(flags & 0x08),
            "invalid": bool(flags & 0x80000000),
        },
        any(not math.isfinite(value) for value in float_values),
    )


def parse_procedural_bone_blackbox(data: bytes) -> dict[str, Any]:
    result = parse_animation_ring_telemetry_blackbox(
        data,
        type_name="procedural_bone_blackbox",
        header_struct=PROCEDURAL_BONE_HEADER,
        entry_struct=PROCEDURAL_BONE_ENTRY,
        expected_magic=PROCEDURAL_BONE_MAGIC,
        expected_version=PROCEDURAL_BONE_VERSION,
        header_bytes=PROCEDURAL_BONE_HEADER_BYTES,
        entry_bytes=PROCEDURAL_BONE_ENTRY_BYTES,
        capacity=PROCEDURAL_BONE_TELEMETRY_CAPACITY,
        hash_seed=lambda snapshot_count, snapshot_cursor: (
            2166136261 ^ snapshot_count ^ snapshot_cursor ^ 0x414E494D
        )
        & 0xFFFFFFFF,
        decode_entry=decode_procedural_bone_entry,
    )
    entries = result.get("entries", [])
    warnings = result.setdefault("warnings", [])
    if any(entry.get("invalid") for entry in entries):
        warnings.append("invalid")
    if any(entry.get("qualityCollapse") for entry in entries):
        warnings.append("quality_collapse")
    if any(entry.get("invalidMathCount", 0) > 0 for entry in entries):
        warnings.append("invalid_math_count")
    return result


def is_vr_somatic_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMP1335SOMATICCOMFORTBIN", "DUMP1335SOMATICCOMFORTH8DUMP"}


def compute_vr_somatic_state_hash(entry_bytes: bytes) -> int:
    hash_value = 2166136261
    for offset in (12, 16, 20, 24, 28, 32, 36, 48, 52, 64, 68, 72, 76):
        hash_value = fnv1a_mix_u32(hash_value, struct.unpack_from("<I", entry_bytes, offset)[0])
    for offset in (80, 84, 88):
        hash_value = fnv1a_mix_u32(hash_value, struct.unpack_from("<I", entry_bytes, offset)[0])
    hash_value = fnv1a_mix_u32(hash_value, struct.unpack_from("<H", entry_bytes, 8)[0])
    return hash_value


def parse_vr_somatic_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < VR_SOMATIC_HEADER.size:
        return {
            "type": "vr_somatic_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, frame_capacity, entry_count = VR_SOMATIC_HEADER.unpack_from(data, 0)
    if (
        magic != VR_SOMATIC_MAGIC
        or version != VR_SOMATIC_VERSION
        or frame_capacity != VR_SOMATIC_FRAME_CAPACITY
        or entry_count <= 0
        or entry_count > frame_capacity
    ):
        return {
            "type": "vr_somatic_blackbox",
            "version": version,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    readable_entries = min(entry_count, max(0, (len(data) - VR_SOMATIC_HEADER_BYTES) // VR_SOMATIC_ENTRY_BYTES))
    entries = []
    nonfinite_seen = False
    hash_mismatch_seen = False
    for index in range(readable_entries):
        offset = VR_SOMATIC_HEADER_BYTES + index * VR_SOMATIC_ENTRY_BYTES
        if is_empty_entry(data, offset, VR_SOMATIC_ENTRY_BYTES):
            continue

        row = data[offset : offset + VR_SOMATIC_ENTRY_BYTES]
        fields = VR_SOMATIC_ENTRY.unpack_from(row, 0)
        flags = fields[2]
        labels, unknown_flags = resolve_bit_labels(flags, VR_SOMATIC_FLAG_LABELS)
        float_values = fields[4:16] + fields[17:21]
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        computed_hash = compute_vr_somatic_state_hash(row)
        state_hash_ok = computed_hash == fields[1]
        hash_mismatch_seen |= not state_hash_ok
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "stateHash": fields[1],
                "computedStateHash": computed_hash,
                "stateHashOk": state_hash_ok,
                "flags": flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "handGhostMask": fields[3],
                "headPosition": {"x": round(fields[4], 4), "y": round(fields[5], 4), "z": round(fields[6], 4)},
                "headRotation": {
                    "x": round(fields[7], 6),
                    "y": round(fields[8], 6),
                    "z": round(fields[9], 6),
                    "w": round(fields[10], 6),
                },
                "nearCollision01": round(fields[11], 4),
                "comfortVignette01": round(fields[12], 4),
                "leftHandSeparationSq": round(fields[13], 4),
                "rightHandSeparationSq": round(fields[14], 4),
                "headAngularSpeedRadiansPerSecond": round(fields[15], 4),
                "aupShiftSequence": fields[16],
                "kccAngularVelocityRadiansPerSecond": round(fields[17], 4),
                "kccAngularAccelerationRadiansPerSecondSq": round(fields[18], 4),
                "kccComfortVignette01": round(fields[19], 4),
                "kccHorizonLock01": round(fields[20], 4),
                "kccVelocitySequence": fields[21],
                "kccVelocityFrame": fields[22],
                "kccVelocitySourceId": fields[23],
                "reserved0": fields[24],
                "reserved1": fields[25],
                "active": bool(flags & 0x0001),
                "nonFinite": bool(flags & 0x0002),
                "leftGhost": bool(flags & 0x0004),
                "rightGhost": bool(flags & 0x0008),
                "nearCollision": bool(flags & 0x0040),
                "aupShiftSeen": bool(flags & 0x0080),
                "framePressure": bool(flags & 0x0200),
                "protectiveFallback": bool(flags & 0x0400),
                "accelerationTunnel": bool(flags & 0x0800),
                "kccSignal": bool(flags & 0x1000),
                "kccAccelerationTunnel": bool(flags & 0x2000),
                "dynamicHorizonLock": bool(flags & 0x4000),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    expected_bytes = VR_SOMATIC_HEADER_BYTES + entry_count * VR_SOMATIC_ENTRY_BYTES
    warnings = []
    if readable_entries < entry_count:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) - VR_SOMATIC_HEADER_BYTES > 0 and (len(data) - VR_SOMATIC_HEADER_BYTES) % VR_SOMATIC_ENTRY_BYTES != 0:
        warnings.append("trailing_partial_entry")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen or any(entry.get("nonFinite") for entry in entries):
        warnings.append("nonfinite")
    if hash_mismatch_seen:
        warnings.append("state_hash_mismatch")
    if any(entry.get("nearCollision") for entry in entries):
        warnings.append("near_collision")
    if any(entry.get("framePressure") for entry in entries):
        warnings.append("frame_pressure")
    if any(entry.get("protectiveFallback") for entry in entries):
        warnings.append("protective_fallback")
    if any(entry.get("accelerationTunnel") or entry.get("kccAccelerationTunnel") for entry in entries):
        warnings.append("acceleration_tunnel")
    return {
        "type": "vr_somatic_blackbox",
        "version": version,
        "headerBytes": VR_SOMATIC_HEADER_BYTES,
        "entrySize": VR_SOMATIC_ENTRY_BYTES,
        "frameCapacity": frame_capacity,
        "declaredEntryCount": entry_count,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_lockstep_state_validator_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMP1403LOCKSTEPSTATEVALIDATORBIN", "DUMP1403LOCKSTEPSTATEVALIDATORH8DUMP"}


def parse_lockstep_state_validator_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < LOCKSTEP_STATE_VALIDATOR_HEADER.size:
        return {
            "type": "lockstep_state_validator_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, entry_count, entry_size, telemetry_write_index, master_hash = LOCKSTEP_STATE_VALIDATOR_HEADER.unpack_from(data, 0)
    if (
        magic != LOCKSTEP_STATE_VALIDATOR_MAGIC
        or version != LOCKSTEP_STATE_VALIDATOR_VERSION
        or entry_count <= 0
        or entry_count > LOCKSTEP_STATE_VALIDATOR_TELEMETRY_CAPACITY
        or entry_size != LOCKSTEP_STATE_VALIDATOR_ENTRY_BYTES
    ):
        return {
            "type": "lockstep_state_validator_blackbox",
            "version": version,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    readable_entries = min(entry_count, max(0, (len(data) - LOCKSTEP_STATE_VALIDATOR_HEADER_BYTES) // entry_size))
    entries = []
    for index in range(readable_entries):
        offset = LOCKSTEP_STATE_VALIDATOR_HEADER_BYTES + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = LOCKSTEP_STATE_VALIDATOR_ENTRY.unpack_from(data, offset)
        flags = fields[7]
        flag_labels, unknown_flags = resolve_bit_labels(flags, LOCKSTEP_STATE_VALIDATOR_FLAG_LABELS)
        missing_labels, missing_unknown = resolve_bit_labels(fields[12], LOCKSTEP_STATE_VALIDATOR_CATEGORY_LABELS)
        nonfinite_labels, nonfinite_unknown = resolve_bit_labels(fields[13], LOCKSTEP_STATE_VALIDATOR_CATEGORY_LABELS)
        entry_master_hash = ((fields[2] & 0xFFFFFFFF) << 32) | (fields[1] & 0xFFFFFFFF)
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "masterHash": entry_master_hash,
                "masterHashHex": f"0x{entry_master_hash:016X}",
                "hashLo": fields[1],
                "hashHi": fields[2],
                "rigidbodyHash": fields[3],
                "playerHash": fields[4],
                "roomHash": fields[5],
                "entityHash": fields[6],
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "rigidbodyCount": fields[8],
                "playerCount": fields[9],
                "roomCount": fields[10],
                "entityCount": fields[11],
                "missingMask": fields[12],
                "missingLabels": missing_labels,
                "missingUnknownMask": missing_unknown,
                "nonFiniteMask": fields[13],
                "nonFiniteLabels": nonfinite_labels,
                "nonFiniteUnknownMask": nonfinite_unknown,
                "replayBlock": fields[14],
                "reserved0": fields[15],
                "hashExecuted": bool(flags & 0x001),
                "missingData": bool(flags & 0x002),
                "truncated": bool(flags & 0x004),
                "nonFinite": bool(flags & 0x008),
                "replayMode": bool(flags & 0x010),
                "desync": bool(flags & 0x040),
                "layoutInvalid": bool(flags & 0x100),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    expected_bytes = LOCKSTEP_STATE_VALIDATOR_HEADER_BYTES + entry_count * entry_size
    warnings = []
    if readable_entries < entry_count:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) - LOCKSTEP_STATE_VALIDATOR_HEADER_BYTES > 0 and (len(data) - LOCKSTEP_STATE_VALIDATOR_HEADER_BYTES) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if any(
        entry.get("unknownFlags") or entry.get("missingUnknownMask") or entry.get("nonFiniteUnknownMask")
        for entry in entries
    ):
        warnings.append("unknown_flags")
    if any(entry.get("desync") for entry in entries):
        warnings.append("desync")
    if any(entry.get("missingData") or entry.get("missingMask") for entry in entries):
        warnings.append("missing_data")
    if any(entry.get("nonFinite") or entry.get("nonFiniteMask") for entry in entries):
        warnings.append("nonfinite")
    if any(entry.get("truncated") for entry in entries):
        warnings.append("truncated")
    if any(entry.get("layoutInvalid") for entry in entries):
        warnings.append("layout_invalid")
    return {
        "type": "lockstep_state_validator_blackbox",
        "version": version,
        "headerBytes": LOCKSTEP_STATE_VALIDATOR_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": entry_count,
        "telemetryWriteIndex": telemetry_write_index,
        "masterHash": master_hash,
        "masterHashHex": f"0x{master_hash:016X}",
        "latestMasterHashMatchesHeader": None if latest is None else latest.get("masterHash") == master_hash,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_voxel_astar_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMP1403VOXELASTARBIN", "DUMP1403VOXELASTARH8DUMP"}


def parse_voxel_astar_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < VOXEL_ASTAR_ENTRY_BYTES:
        return {
            "type": "voxel_astar_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_payload"],
        }

    readable_entries = min(VOXEL_ASTAR_TELEMETRY_CAPACITY, len(data) // VOXEL_ASTAR_ENTRY_BYTES)
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = index * VOXEL_ASTAR_ENTRY_BYTES
        if is_empty_entry(data, offset, VOXEL_ASTAR_ENTRY_BYTES):
            continue

        fields = VOXEL_ASTAR_ENTRY.unpack_from(data, offset)
        flags = fields[9]
        labels, unknown_flags = resolve_bit_labels(flags, VOXEL_ASTAR_FLAG_LABELS)
        if not math.isfinite(fields[12]) or not math.isfinite(fields[13]):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "pendingRequests": fields[1],
                "acceptedRequests": fields[2],
                "droppedRequests": fields[3],
                "successfulPaths": fields[4],
                "failedPaths": fields[5],
                "nodesExpanded": fields[6],
                "averageNodesExpanded": fields[7],
                "burstMicros": fields[8],
                "flags": flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "searchId": fields[10],
                "requesterEntityHash": fields[11],
                "qualityWeight": round(fields[12], 4),
                "heuristicWeight": round(fields[13], 4),
                "rawPathCount": fields[14],
                "waypointCount": fields[15],
                "reserved0": fields[16],
                "nonFiniteInput": bool(flags & 0x0001),
                "startOutOfBounds": bool(flags & 0x0002),
                "goalOutOfBounds": bool(flags & 0x0004),
                "startBlocked": bool(flags & 0x0008),
                "goalBlocked": bool(flags & 0x0010),
                "openSetExhausted": bool(flags & 0x0020),
                "nodeBudgetYield": bool(flags & 0x0040),
                "rawPathOverflow": bool(flags & 0x0080),
                "waypointOverflow": bool(flags & 0x0100),
                "sdfMissing": bool(flags & 0x0200),
                "nanDetected": bool(flags & 0x0400),
                "timeSliceOverBudget": bool(flags & 0x0800),
                "usedWeightedHeuristic": bool(flags & 0x1000),
                "partialNearestFallback": bool(flags & 0x2000),
                "mockSdfGenerated": bool(flags & 0x4000),
                "csvProfileOverflow": bool(flags & 0x8000),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) % VOXEL_ASTAR_ENTRY_BYTES != 0:
        warnings.append("trailing_partial_entry")
    if len(data) // VOXEL_ASTAR_ENTRY_BYTES > VOXEL_ASTAR_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen or any(entry.get("nonFiniteInput") or entry.get("nanDetected") for entry in entries):
        warnings.append("nonfinite")
    if any(entry.get("timeSliceOverBudget") for entry in entries):
        warnings.append("time_slice_over_budget")
    if any(entry.get("droppedRequests", 0) > 0 for entry in entries):
        warnings.append("dropped_requests")
    if any(entry.get("failedPaths", 0) > 0 for entry in entries):
        warnings.append("failed_paths")
    if any(entry.get("sdfMissing") for entry in entries):
        warnings.append("sdf_missing")
    if any(entry.get("rawPathOverflow") or entry.get("waypointOverflow") or entry.get("csvProfileOverflow") for entry in entries):
        warnings.append("overflow")
    if any(entry.get("startOutOfBounds") or entry.get("goalOutOfBounds") for entry in entries):
        warnings.append("out_of_bounds")
    if any(entry.get("startBlocked") or entry.get("goalBlocked") for entry in entries):
        warnings.append("blocked")
    return {
        "type": "voxel_astar_blackbox",
        "entrySize": VOXEL_ASTAR_ENTRY_BYTES,
        "declaredEntryCount": readable_entries,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_path_funnel_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMP1403PATHFUNNELBIN", "DUMP1403PATHFUNNELH8DUMP"}


def parse_path_funnel_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < PATH_FUNNEL_ENTRY_BYTES:
        return {
            "type": "path_funnel_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_payload"],
        }

    readable_entries = min(PATH_FUNNEL_TELEMETRY_CAPACITY, len(data) // PATH_FUNNEL_ENTRY_BYTES)
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = index * PATH_FUNNEL_ENTRY_BYTES
        if is_empty_entry(data, offset, PATH_FUNNEL_ENTRY_BYTES):
            continue

        fields = PATH_FUNNEL_ENTRY.unpack_from(data, offset)
        flags = fields[11]
        labels, unknown_flags = resolve_bit_labels(flags, PATH_FUNNEL_TELEMETRY_FLAG_LABELS)
        if not math.isfinite(fields[6]):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "lastSectorHash": fields[0],
                "lastSectorHashHex": f"0x{fields[0]:016X}",
                "reserved1": fields[1],
                "frame": fields[2],
                "pathInvalidationCount": fields[3],
                "lastPathId": fields[4],
                "lastCorridorHash": fields[5],
                "lastCorridorHashHex": f"0x{fields[5]:08X}",
                "stress01": round(fields[6], 4),
                "reserved0": fields[7],
                "lastCellIndex": fields[8],
                "activePathCount": fields[9],
                "invalidatedPathCount": fields[10],
                "flags": flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "blackBoxDumpFailed": bool(flags & 0x01),
                "wfcVaultSignalMismatch": bool(flags & 0x02),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) % PATH_FUNNEL_ENTRY_BYTES != 0:
        warnings.append("trailing_partial_entry")
    if len(data) // PATH_FUNNEL_ENTRY_BYTES > PATH_FUNNEL_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("blackBoxDumpFailed") for entry in entries):
        warnings.append("blackbox_dump_failed")
    if any(entry.get("wfcVaultSignalMismatch") for entry in entries):
        warnings.append("wfc_vault_signal_mismatch")
    if any(entry.get("invalidatedPathCount", 0) > 0 for entry in entries):
        warnings.append("path_invalidations")
    return {
        "type": "path_funnel_blackbox",
        "entrySize": PATH_FUNNEL_ENTRY_BYTES,
        "declaredEntryCount": readable_entries,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_laser_cutter_dod_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMPSHINOBU225BIN",
        "DUMPSHINOBU225H8DUMP",
        "DUMPSHINOBU225LASERCUTTERDODBIN",
        "DUMPSHINOBU225LASERCUTTERDODH8DUMP",
        "DUMPSHINOBU225WFCLASERCUTBIN",
        "DUMPSHINOBU225WFCLASERCUTH8DUMP",
        "DUMPLASERCUTTERDODBIN",
        "DUMPLASERCUTTERDODH8DUMP",
        "DUMPWFCLASERCUTBIN",
        "DUMPWFCLASERCUTH8DUMP",
    }


def parse_laser_cut_225_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < 4:
        return {
            "type": "laser_cut_225_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic = struct.unpack_from("<I", data, 0)[0]
    if magic == WFC_LASER_CUT_MAGIC:
        return parse_wfc_laser_cut_blackbox(data)
    return parse_laser_cutter_dod_blackbox(data)


def parse_laser_cutter_dod_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < LASER_CUTTER_DOD_HEADER_BYTES:
        return {
            "type": "laser_cutter_dod_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    (
        magic,
        version,
        frame_index,
        entry_count,
        entry_size,
        cursor,
        request_sequence,
        payload_bytes,
    ) = LASER_CUTTER_DOD_HEADER.unpack_from(data, 0)
    if (
        magic != LASER_CUTTER_DOD_MAGIC
        or version != LASER_CUTTER_DOD_VERSION
        or entry_count <= 0
        or entry_count > LASER_CUTTER_DOD_TELEMETRY_CAPACITY
        or entry_size != LASER_CUTTER_DOD_ENTRY_BYTES
        or cursor < 0
        or cursor > entry_count
        or payload_bytes != entry_count * entry_size
    ):
        return {
            "type": "laser_cutter_dod_blackbox",
            "version": version,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = LASER_CUTTER_DOD_HEADER_BYTES
    readable_entries = min(entry_count, max(0, (len(data) - payload_offset) // entry_size))
    entries = []
    nonfinite_seen = False
    layout_mismatch_seen = False
    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = LASER_CUTTER_DOD_ENTRY.unpack_from(data, offset)
        flags = fields[17]
        labels, unknown_flags = resolve_bit_labels(flags, LASER_CUTTER_DOD_RESULT_FLAG_LABELS)
        layout_magic = fields[20]
        if layout_magic != LASER_CUTTER_DOD_LAYOUT_MAGIC:
            layout_mismatch_seen = True
        float_values = fields[:12] + (fields[21], fields[23])
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "rayOriginAup": {"x": round(fields[0], 4), "y": round(fields[1], 4), "z": round(fields[2], 4)},
                "hitAup": {"x": round(fields[3], 4), "y": round(fields[4], 4), "z": round(fields[5], 4)},
                "rayDirection": {"x": round(fields[6], 4), "y": round(fields[7], 4), "z": round(fields[8], 4)},
                "distanceMeters": round(fields[9], 4),
                "cuttingPower01": round(fields[10], 4),
                "qualityWeight": round(fields[11], 4),
                "frame": fields[12],
                "requestSequence": fields[13],
                "toolHashID": fields[14],
                "toolHashHex": f"0x{fields[14]:08X}",
                "parentEntityID": fields[15],
                "colliderInstanceID": fields[16],
                "flags": flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "sparkCount": fields[18],
                "cooldownUntilFrame": fields[19],
                "layoutMagic": layout_magic,
                "layoutMagicHex": f"0x{layout_magic:08X}",
                "heat01": round(fields[21], 4),
                "stateHash": fields[22],
                "stateHashHex": f"0x{fields[22]:016X}",
                "batteryWatts": round(fields[23], 4),
                "burstWorkEstimateMicros": fields[24],
                "hit": bool(flags & 0x01),
                "nonFinite": bool(flags & 0x02),
                "shaderDentOnly": bool(flags & 0x04),
                "gpuSparkOnly": bool(flags & 0x08),
                "batteryDrainQueued": bool(flags & 0x10),
                "decalQueued": bool(flags & 0x20),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    expected_bytes = LASER_CUTTER_DOD_HEADER_BYTES + payload_bytes
    if readable_entries < entry_count:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) - payload_offset > 0 and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen or any(entry.get("nonFinite") for entry in entries):
        warnings.append("nonfinite")
    if layout_mismatch_seen:
        warnings.append("layout_magic_mismatch")
    return {
        "type": "laser_cutter_dod_blackbox",
        "version": version,
        "headerBytes": LASER_CUTTER_DOD_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": entry_count,
        "cursor": cursor,
        "frameIndex": frame_index,
        "requestSequence": request_sequence,
        "payloadBytes": payload_bytes,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def parse_wfc_laser_cut_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < WFC_LASER_CUT_HEADER_BYTES:
        return {
            "type": "wfc_laser_cut_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, entry_count, entry_size, cursor, source_hash, reserved0, reserved1 = WFC_LASER_CUT_HEADER.unpack_from(data, 0)
    if (
        magic != WFC_LASER_CUT_MAGIC
        or version != WFC_LASER_CUT_VERSION
        or entry_count <= 0
        or entry_count > WFC_LASER_CUT_TELEMETRY_CAPACITY
        or entry_size != WFC_LASER_CUT_ENTRY_BYTES
        or cursor < 0
        or cursor >= entry_count
    ):
        return {
            "type": "wfc_laser_cut_blackbox",
            "version": version,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = WFC_LASER_CUT_HEADER_BYTES
    readable_entries = min(entry_count, max(0, (len(data) - payload_offset) // entry_size))
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = WFC_LASER_CUT_ENTRY.unpack_from(data, offset)
        flags = fields[16]
        labels, unknown_flags = resolve_bit_labels(flags, WFC_LASER_CUT_FLAG_LABELS)
        float_values = fields[:6] + fields[9:14]
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "cutOriginAup": {"x": round(fields[0], 4), "y": round(fields[1], 4), "z": round(fields[2], 4)},
                "hitAup": {"x": round(fields[3], 4), "y": round(fields[4], 4), "z": round(fields[5], 4)},
                "sectorHash": fields[6],
                "sectorHashHex": f"0x{fields[6]:016X}",
                "frame": fields[7],
                "toolHash": fields[8],
                "toolHashHex": f"0x{fields[8]:08X}",
                "progress01": round(fields[9], 4),
                "progressDelta01": round(fields[10], 4),
                "cutterPower01": round(fields[11], 4),
                "heat01": round(fields[12], 4),
                "systemStress01": round(fields[13], 4),
                "doorsCutCount": fields[14],
                "cellIndex": fields[15],
                "flags": flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "reservedPadding": fields[17],
                "completed": bool(flags & 0x01),
                "alreadyUnlocked": bool(flags & 0x02),
                "stressReduced": bool(flags & 0x04),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    expected_bytes = WFC_LASER_CUT_HEADER_BYTES + entry_count * entry_size
    if readable_entries < entry_count:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) - payload_offset > 0 and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if source_hash != WFC_LASER_CUT_SOURCE_HASH:
        warnings.append("source_hash_mismatch")
    if reserved0 or reserved1 or any(entry.get("reservedPadding") for entry in entries):
        warnings.append("reserved_nonzero")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("stressReduced") for entry in entries):
        warnings.append("stress_reduced")
    return {
        "type": "wfc_laser_cut_blackbox",
        "version": version,
        "headerBytes": WFC_LASER_CUT_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": entry_count,
        "cursor": cursor,
        "sourceHash": source_hash,
        "sourceHashHex": f"0x{source_hash:08X}",
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_tool_kinematics_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMPTOOLKINEMATICSBIN",
        "DUMPTOOLKINEMATICSH8DUMP",
        "DUMPTOOLKINEMATICSTELEMETRYBIN",
        "DUMPTOOLKINEMATICSTELEMETRYH8DUMP",
    }


def parse_tool_kinematics_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < TOOL_KINEMATICS_HEADER_BYTES:
        return {
            "type": "tool_kinematics_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, entry_count, entry_size, tool_capacity, telemetry_cursor, frame_index, payload_bytes = TOOL_KINEMATICS_HEADER.unpack_from(data, 0)
    if (
        magic != TOOL_KINEMATICS_MAGIC
        or version != TOOL_KINEMATICS_VERSION
        or entry_count <= 0
        or entry_count > TOOL_KINEMATICS_MAX_DUMP_ENTRIES
        or entry_size != TOOL_KINEMATICS_ENTRY_BYTES
        or tool_capacity <= 0
        or tool_capacity > TOOL_KINEMATICS_MAX_TOOL_CAPACITY
        or telemetry_cursor >= TOOL_KINEMATICS_BLACKBOX_CAPACITY
        or payload_bytes != entry_count * entry_size
        or entry_count > tool_capacity * TOOL_KINEMATICS_BLACKBOX_CAPACITY
    ):
        return {
            "type": "tool_kinematics_blackbox",
            "version": version,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = TOOL_KINEMATICS_HEADER_BYTES
    readable_entries = min(entry_count, max(0, (len(data) - payload_offset) // entry_size))
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = TOOL_KINEMATICS_ENTRY.unpack_from(data, offset)
        flags = fields[7]
        labels, unknown_flags = resolve_bit_labels(flags, TOOL_KINEMATICS_FLAG_LABELS)
        float_values = fields[2:5] + (fields[6],) + fields[8:14]
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        tool_hash = fields[1]
        material_hash = fields[14]
        entries.append(
            {
                "slot": index,
                "toolSlot": index // TOOL_KINEMATICS_BLACKBOX_CAPACITY,
                "ringSlot": index % TOOL_KINEMATICS_BLACKBOX_CAPACITY,
                "frame": fields[0],
                "toolHash": tool_hash,
                "toolHashHex": f"0x{tool_hash:08X}",
                "toolName": TOOL_KINEMATICS_TOOL_HASH_LABELS.get(tool_hash, "unknown"),
                "toolHeatLevel": round(fields[2], 4),
                "energyRemaining": round(fields[3], 4),
                "hitDistance": round(fields[4], 4),
                "raymarchStepCount": fields[5],
                "ikComputeTimeMicroseconds": round(fields[6], 4),
                "flags": flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "toolLocalPosition": {"x": round(fields[8], 4), "y": round(fields[9], 4), "z": round(fields[10], 4)},
                "hitPoint": {"x": round(fields[11], 4), "y": round(fields[12], 4), "z": round(fields[13], 4)},
                "materialHash": material_hash,
                "materialHashHex": f"0x{material_hash:08X}",
                "reservedPadding": fields[15],
                "idle": bool(flags & 0x00001),
                "active": bool(flags & 0x00002),
                "busy": bool(flags & 0x00004),
                "overheated": bool(flags & 0x00008),
                "lowPower": bool(flags & 0x00010),
                "targetLock": bool(flags & 0x00020),
                "cooling": bool(flags & 0x00040),
                "fault": bool(flags & 0x00080),
                "rayHit": bool(flags & 0x00100),
                "recoilActive": bool(flags & 0x00200),
                "lowTierSnap": bool(flags & 0x00400),
                "sdfPenetrating": bool(flags & 0x00800),
                "beamActive": bool(flags & 0x01000),
                "raymarchBudgetExceeded": bool(flags & 0x02000),
                "csvIoFault": bool(flags & 0x04000),
                "lastChargeClutch": bool(flags & 0x08000),
                "powerDepletedSignalQueued": bool(flags & 0x10000),
                "powerDepletedSignalSent": bool(flags & 0x20000),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    expected_bytes = TOOL_KINEMATICS_HEADER_BYTES + payload_bytes
    if readable_entries < entry_count:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) - payload_offset > 0 and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("reservedPadding") for entry in entries):
        warnings.append("reserved_padding_nonzero")
    if any(entry.get("fault") for entry in entries):
        warnings.append("fault_flag")
    if any(entry.get("raymarchBudgetExceeded") for entry in entries):
        warnings.append("raymarch_budget_exceeded")
    if any(entry.get("csvIoFault") for entry in entries):
        warnings.append("csv_io_fault")
    return {
        "type": "tool_kinematics_blackbox",
        "version": version,
        "headerBytes": TOOL_KINEMATICS_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": entry_count,
        "toolCapacity": tool_capacity,
        "telemetryCursor": telemetry_cursor,
        "frameIndex": frame_index,
        "payloadBytes": payload_bytes,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_auxiliary_equipment_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMPSHINOBU229BIN",
        "DUMPSHINOBU229H8DUMP",
        "DUMPAUXILIARYEQUIPMENTBIN",
        "DUMPAUXILIARYEQUIPMENTH8DUMP",
        "DUMPAUXILIARYEQUIPMENTTELEMETRYBIN",
        "DUMPAUXILIARYEQUIPMENTTELEMETRYH8DUMP",
    }


def parse_auxiliary_equipment_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < AUXILIARY_EQUIPMENT_ENTRY_BYTES:
        return {
            "type": "auxiliary_equipment_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_payload"],
        }

    readable_entries = min(AUXILIARY_EQUIPMENT_TELEMETRY_CAPACITY, len(data) // AUXILIARY_EQUIPMENT_ENTRY_BYTES)
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = index * AUXILIARY_EQUIPMENT_ENTRY_BYTES
        if is_empty_entry(data, offset, AUXILIARY_EQUIPMENT_ENTRY_BYTES):
            continue

        fields = AUXILIARY_EQUIPMENT_ENTRY.unpack_from(data, offset)
        fault_flags = fields[8]
        labels, unknown_flags = resolve_bit_labels(fault_flags, AUXILIARY_EQUIPMENT_FLAG_LABELS)
        float_values = fields[5:8]
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "activeCount": fields[1],
                "flareSignals": fields[2],
                "pingSignals": fields[3],
                "tetherSignals": fields[4],
                "effectiveCadenceHz": round(fields[5], 4),
                "cpuMicroseconds": round(fields[6], 4),
                "globalQualityWeight": round(fields[7], 4),
                "faultFlags": fault_flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "snapshotHash": fields[9],
                "snapshotHashHex": f"0x{fields[9]:08X}",
                "droppedSlots": fields[10],
                "droppedSignals": fields[11],
                "corruptedSignals": fields[12],
                "peakQueuedSignals": fields[13],
                "reserved2": fields[14],
                "active": bool(fault_flags & 0x00000001),
                "flare": bool(fault_flags & 0x00000002),
                "sensorPing": bool(fault_flags & 0x00000004),
                "gravityTether": bool(fault_flags & 0x00000008),
                "mock": bool(fault_flags & 0x00000010),
                "routedThisFrame": bool(fault_flags & 0x00000020),
                "nonFiniteRecovered": bool(fault_flags & 0x20000000),
                "unknownPrefab": bool(fault_flags & 0x40000000),
                "faulted": bool(fault_flags & 0x80000000),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) % AUXILIARY_EQUIPMENT_ENTRY_BYTES != 0:
        warnings.append("trailing_partial_entry")
    if len(data) // AUXILIARY_EQUIPMENT_ENTRY_BYTES > AUXILIARY_EQUIPMENT_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("reserved2") for entry in entries):
        warnings.append("reserved_nonzero")
    if any(entry.get("faulted") for entry in entries):
        warnings.append("faulted")
    if any(entry.get("nonFiniteRecovered") for entry in entries):
        warnings.append("nonfinite_recovered")
    if any(entry.get("unknownPrefab") for entry in entries):
        warnings.append("unknown_prefab")
    if any(entry.get("droppedSlots", 0) > 0 for entry in entries):
        warnings.append("dropped_slots")
    if any(entry.get("droppedSignals", 0) > 0 for entry in entries):
        warnings.append("dropped_signals")
    if any(entry.get("corruptedSignals", 0) > 0 for entry in entries):
        warnings.append("corrupted_signals")
    if any(entry.get("cpuMicroseconds", 0.0) > AUXILIARY_EQUIPMENT_FAULT_DUMP_THRESHOLD_MICROSECONDS for entry in entries):
        warnings.append("cpu_over_500us")
    return {
        "type": "auxiliary_equipment_blackbox",
        "entrySize": AUXILIARY_EQUIPMENT_ENTRY_BYTES,
        "declaredEntryCount": readable_entries,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_upgrade_matrix_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMPSHINOBU231BIN",
        "DUMPSHINOBU231H8DUMP",
        "DUMPUPGRADEMATRIXBIN",
        "DUMPUPGRADEMATRIXH8DUMP",
        "DUMPUPGRADEMATRIXTELEMETRYBIN",
        "DUMPUPGRADEMATRIXTELEMETRYH8DUMP",
    }


def parse_upgrade_matrix_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < UPGRADE_MATRIX_ENTRY_BYTES:
        return {
            "type": "upgrade_matrix_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_payload"],
        }

    readable_entries = min(UPGRADE_MATRIX_TELEMETRY_CAPACITY, len(data) // UPGRADE_MATRIX_ENTRY_BYTES)
    entries = []
    nonfinite_seen = False
    layout_mismatch_seen = False
    for index in range(readable_entries):
        offset = index * UPGRADE_MATRIX_ENTRY_BYTES
        if is_empty_entry(data, offset, UPGRADE_MATRIX_ENTRY_BYTES):
            continue

        fields = UPGRADE_MATRIX_ENTRY.unpack_from(data, offset)
        fault_flags = fields[5]
        labels, unknown_flags = resolve_bit_labels(fault_flags, UPGRADE_MATRIX_FAULT_LABELS)
        layout_magic = fields[6]
        if layout_magic != UPGRADE_MATRIX_LAYOUT_MAGIC:
            layout_mismatch_seen = True
        if not math.isfinite(fields[4]):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "evaluatedMaskCount": fields[1],
                "activeBitCount": fields[2],
                "lutLookupCount": fields[3],
                "burstMicroseconds": round(fields[4], 4),
                "faultFlags": fault_flags,
                "faultLabels": labels,
                "unknownFaultFlags": unknown_flags,
                "layoutMagic": layout_magic,
                "layoutMagicHex": f"0x{layout_magic:08X}",
                "lastEntityHashID": fields[7],
                "lastEntityHashHex": f"0x{fields[7]:08X}",
                "lastMask": fields[8],
                "lastMaskHex": f"0x{fields[8]:016X}",
                "stateHash": fields[9],
                "stateHashHex": f"0x{fields[9]:016X}",
                "reserved0": fields[10],
                "reserved1": fields[11],
                "burstOverBudget": bool(fault_flags & 0x01),
                "lutUnavailable": bool(fault_flags & 0x02),
                "thermalGridUnavailable": bool(fault_flags & 0x04),
                "lutIndexClamped": bool(fault_flags & 0x08),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) % UPGRADE_MATRIX_ENTRY_BYTES != 0:
        warnings.append("trailing_partial_entry")
    if len(data) // UPGRADE_MATRIX_ENTRY_BYTES > UPGRADE_MATRIX_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if layout_mismatch_seen:
        warnings.append("layout_magic_mismatch")
    if any(entry.get("unknownFaultFlags") for entry in entries):
        warnings.append("unknown_fault_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("reserved0") or entry.get("reserved1") for entry in entries):
        warnings.append("reserved_nonzero")
    if any(entry.get("faultFlags") for entry in entries):
        warnings.append("fault_flags")
    if any(entry.get("burstMicroseconds", 0.0) > UPGRADE_MATRIX_FAULT_COST_THRESHOLD_MICROSECONDS for entry in entries):
        warnings.append("burst_over_100us")
    return {
        "type": "upgrade_matrix_blackbox",
        "entrySize": UPGRADE_MATRIX_ENTRY_BYTES,
        "declaredEntryCount": readable_entries,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_metabolism_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMPSHINOBU320BIN",
        "DUMPSHINOBU320H8DUMP",
        "DUMPMETASRGEBIN",
        "DUMPMETASRGEH8DUMP",
        "DUMPMETABOLISMBLACKBOXBIN",
        "DUMPMETABOLISMBLACKBOXH8DUMP",
        "DUMPSHINOBUMETABOLISMBIN",
        "DUMPSHINOBUMETABOLISMH8DUMP",
    }


def parse_metabolism_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < METABOLISM_BLACKBOX_HEADER_BYTES:
        return {
            "type": "metabolism_blackbox",
            "entries": [],
            "detailEntries": [],
            "latest": None,
            "latestDetail": None,
            "warnings": ["truncated_header"],
        }

    (
        magic,
        version,
        declared_count,
        entry_size,
        frame_counter,
        pending_telemetry_index,
        detail_stride,
    ) = METABOLISM_BLACKBOX_HEADER.unpack_from(data, 0)
    if (
        magic != METABOLISM_BLACKBOX_MAGIC
        or version != METABOLISM_BLACKBOX_VERSION
        or entry_size != METABOLISM_TELEMETRY_ENTRY_BYTES
        or declared_count <= 0
    ):
        return {
            "type": "metabolism_blackbox",
            "entries": [],
            "detailEntries": [],
            "latest": None,
            "latestDetail": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = METABOLISM_BLACKBOX_HEADER_BYTES
    detail_payload_offset = payload_offset + declared_count * entry_size
    expected_bytes = detail_payload_offset
    detail_stride_valid = detail_stride == METABOLISM_DETAIL_TELEMETRY_ENTRY_BYTES
    if detail_stride_valid:
        expected_bytes += declared_count * detail_stride
    main_available = max(0, min(len(data), detail_payload_offset) - payload_offset)
    detail_available = max(0, len(data) - detail_payload_offset)
    readable_entries = min(
        declared_count,
        METABOLISM_TELEMETRY_CAPACITY,
        main_available // entry_size,
    )
    readable_detail_entries = (
        min(declared_count, METABOLISM_TELEMETRY_CAPACITY, detail_available // detail_stride)
        if detail_stride_valid
        else 0
    )

    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = METABOLISM_TELEMETRY_ENTRY.unpack_from(data, offset)
        flags = fields[12]
        labels, unknown_flags = resolve_bit_labels(flags, METABOLISM_FLAG_LABELS)
        if any(not math.isfinite(value) for value in (fields[3], fields[4], fields[5], fields[9], fields[10], fields[11])):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "frame": fields[1],
                "stateHash": fields[0],
                "stateHashHex": f"0x{fields[0]:016X}",
                "entityCount": fields[2],
                "averageCoreTemperature": round(fields[3], 4),
                "minimumCoreTemperature": round(fields[4], 4),
                "maximumToxicity": round(fields[5], 4),
                "starvationCount": fields[6],
                "dehydrationCount": fields[7],
                "toxicityCount": fields[8],
                "deltaSeconds": round(fields[9], 4),
                "executionMicroseconds": round(fields[10], 4),
                "globalQualityWeight": round(fields[11], 4),
                "flags": flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "firstInvalidIndex": fields[13],
                "signalCount": fields[14],
                "starving": bool(flags & (1 << 0)),
                "dehydrated": bool(flags & (1 << 1)),
                "hypothermia": bool(flags & (1 << 2)),
                "toxic": bool(flags & (1 << 3)),
                "invalidMath": bool(flags & (1 << 4)),
                "mockEntity": bool(flags & (1 << 5)),
                "thermalSampled": bool(flags & (1 << 6)),
                "chemicalSampled": bool(flags & (1 << 8)),
                "fatigue": bool(flags & (1 << 9)),
                "hypoxia": bool(flags & (1 << 10)),
                "executionBudgetExceeded": bool(flags & (1 << 30)),
                "nanDetected": bool(flags & (1 << 31)),
            }
        )

    detail_entries = []
    detail_nonfinite_seen = False
    for index in range(readable_detail_entries):
        offset = detail_payload_offset + index * detail_stride
        if is_empty_entry(data, offset, detail_stride):
            continue

        fields = METABOLISM_DETAIL_TELEMETRY_ENTRY.unpack_from(data, offset)
        flags = fields[11]
        labels, unknown_flags = resolve_bit_labels(flags, METABOLISM_FLAG_LABELS)
        player_aup = fields[0:3]
        if any(not math.isfinite(value) for value in (*player_aup, *fields[3:9])):
            detail_nonfinite_seen = True
        detail_entries.append(
            {
                "slot": index,
                "playerAup": {
                    "x": round(fields[0], 4),
                    "y": round(fields[1], 4),
                    "z": round(fields[2], 4),
                },
                "playerDepthMeters": round(fields[3], 4),
                "activeCalorieBurnPerSecond": round(fields[4], 4),
                "ambientCelsius": round(fields[5], 4),
                "thermalK": round(fields[6], 4),
                "coreAmbientDeltaCelsius": round(fields[7], 4),
                "thermalDeltaCelsiusPerSecond": round(fields[8], 4),
                "frame": fields[9],
                "entityHashID": fields[10],
                "entityHashHex": f"0x{fields[10]:08X}",
                "flags": flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "suitProfileHash": fields[12],
                "suitProfileHashHex": f"0x{fields[12]:08X}",
                "starving": bool(flags & (1 << 0)),
                "dehydrated": bool(flags & (1 << 1)),
                "hypothermia": bool(flags & (1 << 2)),
                "toxic": bool(flags & (1 << 3)),
                "invalidMath": bool(flags & (1 << 4)),
                "fatigue": bool(flags & (1 << 9)),
                "hypoxia": bool(flags & (1 << 10)),
                "nanDetected": bool(flags & (1 << 31)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    latest_detail = max(detail_entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if detail_entries else None
    capped = cap_entries(entries)
    capped_detail = cap_entries(detail_entries)
    warnings = []
    if len(data) < detail_payload_offset:
        warnings.append("payload_truncated")
    elif detail_stride_valid and len(data) < expected_bytes:
        warnings.append("detail_payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if main_available % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if detail_stride and not detail_stride_valid:
        warnings.append("detail_stride_mismatch")
    elif detail_stride_valid and detail_available % detail_stride != 0:
        warnings.append("trailing_partial_detail_entry")
    if declared_count > METABOLISM_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if any(entry.get("unknownFlags") for entry in detail_entries):
        warnings.append("unknown_detail_flags")
    if nonfinite_seen or detail_nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("invalidMath") for entry in entries + detail_entries):
        warnings.append("invalid_math")
    if any(entry.get("nanDetected") for entry in entries + detail_entries):
        warnings.append("nan_detected")
    if any(entry.get("executionBudgetExceeded") for entry in entries):
        warnings.append("execution_budget_exceeded")
    if any(entry.get("executionMicroseconds", 0.0) > METABOLISM_EXECUTION_BUDGET_MICROSECONDS for entry in entries):
        warnings.append("execution_over_200us")
    if any(entry.get("starvationCount", 0) > 0 or entry.get("starving") for entry in entries):
        warnings.append("starvation")
    if any(entry.get("dehydrationCount", 0) > 0 or entry.get("dehydrated") for entry in entries):
        warnings.append("dehydration")
    if any(entry.get("toxicityCount", 0) > 0 or entry.get("toxic") for entry in entries):
        warnings.append("toxicity")
    if any(entry.get("hypothermia") for entry in entries + detail_entries):
        warnings.append("hypothermia")
    if any(entry.get("hypoxia") for entry in entries + detail_entries):
        warnings.append("hypoxia")
    return {
        "type": "metabolism_blackbox",
        "version": version,
        "headerBytes": METABOLISM_BLACKBOX_HEADER_BYTES,
        "entrySize": entry_size,
        "detailEntrySize": detail_stride,
        "declaredEntryCount": declared_count,
        "frameCounter": frame_counter,
        "pendingTelemetryIndex": pending_telemetry_index,
        "nonEmptyEntryCount": len(entries),
        "nonEmptyDetailEntryCount": len(detail_entries),
        "returnedEntryCount": len(capped),
        "returnedDetailEntryCount": len(capped_detail),
        "entries": capped,
        "detailEntries": capped_detail,
        "latest": latest,
        "latestDetail": latest_detail,
        "warnings": warnings,
    }


def is_physiology_autopsy_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMPSHINOBU321BIN",
        "DUMPSHINOBU321H8DUMP",
        "DUMPPHYSIOLOGYAUTOPSYBIN",
        "DUMPPHYSIOLOGYAUTOPSYH8DUMP",
        "DUMPSHINOBUPHYSIOLOGYBIN",
        "DUMPSHINOBUPHYSIOLOGYH8DUMP",
    }


def parse_physiology_autopsy_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < PHYSIOLOGY_AUTOPSY_HEADER_BYTES:
        return {
            "type": "physiology_autopsy_blackbox",
            "entries": [],
            "decompressionEntries": [],
            "latest": None,
            "latestDecompression": None,
            "warnings": ["truncated_header"],
        }

    (
        magic,
        version,
        declared_physiology_count,
        physiology_entry_size,
        physiology_cursor,
        source_hash,
        frame_counter,
        declared_decompression_count,
        decompression_entry_size,
        decompression_cursor,
        decompression_buffer_id,
    ) = PHYSIOLOGY_AUTOPSY_HEADER.unpack_from(data, 0)

    if (
        magic != PHYSIOLOGY_AUTOPSY_MAGIC
        or version != PHYSIOLOGY_AUTOPSY_VERSION
        or physiology_entry_size != PHYSIOLOGY_TELEMETRY_ENTRY_BYTES
        or decompression_entry_size != DECOMPRESSION_TELEMETRY_ENTRY_BYTES
        or declared_physiology_count <= 0
        or declared_decompression_count <= 0
    ):
        return {
            "type": "physiology_autopsy_blackbox",
            "entries": [],
            "decompressionEntries": [],
            "latest": None,
            "latestDecompression": None,
            "warnings": ["invalid_header"],
        }

    physiology_payload_offset = PHYSIOLOGY_AUTOPSY_HEADER_BYTES
    decompression_payload_offset = physiology_payload_offset + declared_physiology_count * physiology_entry_size
    expected_bytes = decompression_payload_offset + declared_decompression_count * decompression_entry_size
    physiology_available = max(0, min(len(data), decompression_payload_offset) - physiology_payload_offset)
    decompression_available = max(0, len(data) - decompression_payload_offset)
    readable_physiology = min(
        declared_physiology_count,
        PHYSIOLOGY_TELEMETRY_CAPACITY,
        physiology_available // physiology_entry_size,
    )
    readable_decompression = min(
        declared_decompression_count,
        PHYSIOLOGY_TELEMETRY_CAPACITY,
        decompression_available // decompression_entry_size,
    )

    physiology_entries = []
    nonfinite_seen = False
    for index in range(readable_physiology):
        offset = physiology_payload_offset + index * physiology_entry_size
        if is_empty_entry(data, offset, physiology_entry_size):
            continue

        fields = PHYSIOLOGY_TELEMETRY_ENTRY.unpack_from(data, offset)
        status_labels, unknown_status = resolve_bit_labels64(fields[1], PHYSIOLOGY_STATUS_EFFECT_LABELS)
        fatal_labels, unknown_fatal = resolve_bit_labels(fields[3], PHYSIOLOGY_FLAG_LABELS)
        if any(not math.isfinite(value) for value in fields[4:12]) or not math.isfinite(fields[13]):
            nonfinite_seen = True
        entry = {
            "slot": index,
            "frame": fields[2],
            "stateHash": fields[0],
            "stateHashHex": f"0x{fields[0]:016X}",
            "statusEffectMask": fields[1],
            "statusEffectMaskHex": f"0x{fields[1]:016X}",
            "statusEffectLabels": status_labels,
            "unknownStatusEffectMask": unknown_status,
            "fatalFlags": fields[3],
            "fatalFlagLabels": fatal_labels,
            "unknownFatalFlags": unknown_fatal,
            "bloodOxygen": round(fields[4], 4),
            "nitrogenLoad": round(fields[5], 4),
            "coreTemperature": round(fields[6], 4),
            "ambientPressureAtm": round(fields[7], 4),
            "narcosisSeverity": round(fields[8], 4),
            "supersaturationScalar": round(fields[9], 4),
            "heartRate": round(fields[10], 4),
            "adrenaline": round(fields[11], 4),
            "tissueOverMValueMask": fields[12],
            "tissueOverMValueMaskHex": f"0x{fields[12]:08X}",
            "executionMicroseconds": round(fields[13], 4),
            "fatal": fields[3] != 0,
            "oxygenCritical": bool(fields[1] & (1 << 3)) or bool(fields[3] & (1 << 3)),
            "fatalOxygen": bool(fields[1] & (1 << 4)) or bool(fields[3] & (1 << 4)),
            "invalidMath": bool(fields[1] & (1 << 5)) or bool(fields[3] & (1 << 5)),
            "fatalBends": bool(fields[1] & (1 << 7)) or bool(fields[3] & (1 << 11)),
            "hypoxia": bool(fields[1] & (1 << 8)) or bool(fields[3] & (1 << 12)),
            "fatalGasToxicity": bool(fields[1] & (1 << 12)) or bool(fields[3] & (1 << 16)),
        }
        physiology_entries.append(entry)

    decompression_entries = []
    decompression_nonfinite_seen = False
    for index in range(readable_decompression):
        offset = decompression_payload_offset + index * decompression_entry_size
        if is_empty_entry(data, offset, decompression_entry_size):
            continue

        fields = DECOMPRESSION_TELEMETRY_ENTRY.unpack_from(data, offset)
        bubble_labels, unknown_bubbles = resolve_bit_labels(fields[2], DECOMPRESSION_BUBBLE_FLAG_LABELS)
        fatal_labels, unknown_fatal = resolve_bit_labels(fields[13], PHYSIOLOGY_FLAG_LABELS)
        if any(not math.isfinite(value) for value in fields[3:11]):
            decompression_nonfinite_seen = True
        entry = {
            "slot": index,
            "frame": fields[1],
            "stateHash": fields[0],
            "stateHashHex": f"0x{fields[0]:016X}",
            "bubbleFlags": fields[2],
            "bubbleFlagLabels": bubble_labels,
            "unknownBubbleFlags": unknown_bubbles,
            "depthMeters": round(fields[3], 4),
            "ambientPressureAtm": round(fields[4], 4),
            "leadingTissueTensionAtm": round(fields[5], 4),
            "allowedAmbientPressureAtm": round(fields[6], 4),
            "mValueGradientAtm": round(fields[7], 4),
            "supersaturationScalar": round(fields[8], 4),
            "executionMicroseconds": round(fields[9], 4),
            "globalQualityWeight": round(fields[10], 4),
            "tissueOverMValueMask": fields[11],
            "tissueOverMValueMaskHex": f"0x{fields[11]:08X}",
            "activeCompartments": fields[12],
            "fatalFlags": fields[13],
            "fatalFlagLabels": fatal_labels,
            "unknownFatalFlags": unknown_fatal,
            "reserved0": fields[14],
            "fatal": fields[13] != 0,
            "bends": bool(fields[2] & (1 << 0)),
            "fatalBends": bool(fields[13] & (1 << 11)),
            "invalidMath": bool(fields[13] & (1 << 5)),
        }
        decompression_entries.append(entry)

    latest = max(physiology_entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if physiology_entries else None
    latest_decompression = (
        max(decompression_entries, key=lambda entry: safe_int(entry.get("frame"), 0))
        if decompression_entries
        else None
    )
    capped = cap_entries(physiology_entries)
    capped_decompression = cap_entries(decompression_entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if physiology_available % physiology_entry_size != 0 or decompression_available % decompression_entry_size != 0:
        warnings.append("trailing_partial_entry")
    if (
        declared_physiology_count > PHYSIOLOGY_TELEMETRY_CAPACITY
        or declared_decompression_count > PHYSIOLOGY_TELEMETRY_CAPACITY
    ):
        warnings.append("entry_capacity_exceeded")
    if decompression_buffer_id != PHYSIOLOGY_DECOMPRESSION_RING_BUFFER:
        warnings.append("decompression_buffer_mismatch")
    if any(entry.get("unknownFatalFlags") for entry in physiology_entries + decompression_entries):
        warnings.append("unknown_fatal_flags")
    if any(entry.get("unknownStatusEffectMask") for entry in physiology_entries):
        warnings.append("unknown_status_effects")
    if any(entry.get("unknownBubbleFlags") for entry in decompression_entries):
        warnings.append("unknown_bubble_flags")
    if nonfinite_seen or decompression_nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("fatal") for entry in physiology_entries + decompression_entries):
        warnings.append("fatal_flags")
    if any(entry.get("oxygenCritical") for entry in physiology_entries):
        warnings.append("oxygen_critical")
    if any(entry.get("fatalOxygen") for entry in physiology_entries):
        warnings.append("fatal_oxygen")
    if any(entry.get("hypoxia") for entry in physiology_entries):
        warnings.append("hypoxia")
    if any(entry.get("fatalBends") for entry in physiology_entries + decompression_entries):
        warnings.append("fatal_bends")
    if any(entry.get("fatalGasToxicity") for entry in physiology_entries):
        warnings.append("fatal_gas_toxicity")
    if any(entry.get("supersaturationScalar", 0.0) >= 0.98 for entry in physiology_entries + decompression_entries):
        warnings.append("supersaturation_fatal_threshold")
    if any(entry.get("executionMicroseconds", 0.0) > PHYSIOLOGY_TELEMETRY_BUDGET_MICROSECONDS for entry in physiology_entries + decompression_entries):
        warnings.append("execution_over_200us")
    if any(entry.get("reserved0") for entry in decompression_entries):
        warnings.append("reserved_nonzero")
    return {
        "type": "physiology_autopsy_blackbox",
        "version": version,
        "headerBytes": PHYSIOLOGY_AUTOPSY_HEADER_BYTES,
        "entrySize": physiology_entry_size,
        "decompressionEntrySize": decompression_entry_size,
        "declaredEntryCount": declared_physiology_count,
        "declaredDecompressionEntryCount": declared_decompression_count,
        "physiologyCursor": physiology_cursor,
        "decompressionCursor": decompression_cursor,
        "sourceHash": source_hash,
        "sourceHashHex": f"0x{source_hash:08X}",
        "frameCounter": frame_counter,
        "decompressionBufferId": decompression_buffer_id,
        "nonEmptyEntryCount": len(physiology_entries),
        "nonEmptyDecompressionEntryCount": len(decompression_entries),
        "returnedEntryCount": len(capped),
        "returnedDecompressionEntryCount": len(capped_decompression),
        "entries": capped,
        "decompressionEntries": capped_decompression,
        "latest": latest,
        "latestDecompression": latest_decompression,
        "warnings": warnings,
    }


def is_sensory_impairment_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMPSHINOBU322BIN",
        "DUMPSHINOBU322H8DUMP",
        "DUMPS322HYPOBIN",
        "DUMPS322HYPOH8DUMP",
        "DUMPSENSORYIMPAIRMENTBIN",
        "DUMPSENSORYIMPAIRMENTH8DUMP",
        "DUMPHYPOXIAIMPAIRMENTBIN",
        "DUMPHYPOXIAIMPAIRMENTH8DUMP",
    }


def parse_sensory_impairment_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < SENSORY_IMPAIRMENT_HEADER_BYTES:
        return {
            "type": "sensory_impairment_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, declared_count, entry_size, telemetry_cursor, source_hash, frame_counter = SENSORY_IMPAIRMENT_HEADER.unpack_from(data, 0)
    if (
        magic != SENSORY_IMPAIRMENT_MAGIC
        or version != SENSORY_IMPAIRMENT_VERSION
        or entry_size != SENSORY_IMPAIRMENT_ENTRY_BYTES
        or declared_count <= 0
    ):
        return {
            "type": "sensory_impairment_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = SENSORY_IMPAIRMENT_HEADER_BYTES
    expected_bytes = payload_offset + declared_count * entry_size
    readable_entries = min(
        declared_count,
        SENSORY_IMPAIRMENT_TELEMETRY_CAPACITY,
        max(0, len(data) - payload_offset) // entry_size,
    )
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = SENSORY_IMPAIRMENT_ENTRY.unpack_from(data, offset)
        flags = fields[2]
        labels, unknown_flags = resolve_bit_labels(flags, SENSORY_IMPAIRMENT_FLAG_LABELS)
        if any(not math.isfinite(value) for value in fields[3:14]):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "frame": fields[1],
                "stateHash": fields[0],
                "stateHashHex": f"0x{fields[0]:016X}",
                "flags": flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "hypoxiaVignette01": round(fields[3], 4),
                "narcosisDrift01": round(fields[4], 4),
                "inputLatencyMilliseconds": round(fields[5], 4),
                "oxygenPartialPressureAtm": round(fields[6], 4),
                "nitrogenPartialPressureAtm": round(fields[7], 4),
                "carbonDioxidePartialPressureAtm": round(fields[8], 4),
                "depthMeters": round(fields[9], 4),
                "moveDriftMagnitude": round(fields[10], 4),
                "lookDriftMagnitude": round(fields[11], 4),
                "globalQualityWeight": round(fields[12], 4),
                "executionMicroseconds": round(fields[13], 4),
                "ringCursor": fields[14],
                "hypoxiaActive": bool(flags & (1 << 0)),
                "narcosisActive": bool(flags & (1 << 1)),
                "latencyActive": bool(flags & (1 << 2)),
                "complexNoiseAdmitted": bool(flags & (1 << 3)),
                "mockToxicity": bool(flags & (1 << 4)),
                "nonFiniteSanitized": bool(flags & (1 << 5)),
                "overBudget": bool(flags & (1 << 6)),
                "csvProfile": bool(flags & (1 << 7)),
                "inputCorrupted": bool(flags & (1 << 8)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if declared_count > SENSORY_IMPAIRMENT_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if source_hash != SENSORY_IMPAIRMENT_SOURCE_HASH:
        warnings.append("source_hash_mismatch")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("nonFiniteSanitized") for entry in entries):
        warnings.append("nonfinite_sanitized")
    if any(entry.get("overBudget") for entry in entries):
        warnings.append("over_budget")
    if any(entry.get("hypoxiaActive") or entry.get("hypoxiaVignette01", 0.0) > 0.0 for entry in entries):
        warnings.append("hypoxia")
    if any(entry.get("narcosisActive") or entry.get("narcosisDrift01", 0.0) > 0.0 for entry in entries):
        warnings.append("narcosis")
    if any(entry.get("latencyActive") or entry.get("inputLatencyMilliseconds", 0.0) > 0.0 for entry in entries):
        warnings.append("input_latency")
    if any(entry.get("inputCorrupted") for entry in entries):
        warnings.append("input_corrupted")
    if any(entry.get("mockToxicity") for entry in entries):
        warnings.append("mock_toxicity")
    return {
        "type": "sensory_impairment_blackbox",
        "version": version,
        "headerBytes": SENSORY_IMPAIRMENT_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": declared_count,
        "telemetryCursor": telemetry_cursor,
        "sourceHash": source_hash,
        "sourceHashHex": f"0x{source_hash:08X}",
        "frameCounter": frame_counter,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_suit_integrity_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMPSHINOBU323BIN",
        "DUMPSHINOBU323H8DUMP",
        "DUMPSUITINTEGRITYBIN",
        "DUMPSUITINTEGRITYH8DUMP",
        "DUMPPRESSURESUITINTEGRITYBIN",
        "DUMPPRESSURESUITINTEGRITYH8DUMP",
    }


def parse_suit_integrity_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < SUIT_INTEGRITY_HEADER_BYTES:
        return {
            "type": "suit_integrity_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, declared_count, entry_size, telemetry_cursor, source_hash, frame_counter = SUIT_INTEGRITY_HEADER.unpack_from(data, 0)
    if (
        magic != SUIT_INTEGRITY_MAGIC
        or version != SUIT_INTEGRITY_VERSION
        or entry_size != SUIT_INTEGRITY_ENTRY_BYTES
        or declared_count <= 0
    ):
        return {
            "type": "suit_integrity_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = SUIT_INTEGRITY_HEADER_BYTES
    expected_bytes = payload_offset + declared_count * entry_size
    readable_entries = min(
        declared_count,
        SUIT_INTEGRITY_TELEMETRY_CAPACITY,
        max(0, len(data) - payload_offset) // entry_size,
    )
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = SUIT_INTEGRITY_ENTRY.unpack_from(data, offset)
        flags = fields[10]
        signal_flags = fields[13]
        flag_labels, unknown_flags = resolve_bit_labels(flags, SUIT_INTEGRITY_FLAG_LABELS)
        signal_labels, unknown_signal_flags = resolve_bit_labels(signal_flags, SUIT_INTEGRITY_FLAG_LABELS)
        if any(not math.isfinite(value) for value in fields[3:10]) or not math.isfinite(fields[12]):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "frame": fields[1],
                "stateHash": fields[0],
                "stateHashHex": f"0x{fields[0]:016X}",
                "entityHash": fields[2],
                "entityHashHex": f"0x{fields[2]:08X}",
                "depthMeters": round(fields[3], 4),
                "appliedPressureAtm": round(fields[4], 4),
                "overpressureScalar": round(fields[5], 4),
                "microFractureAccumulation": round(fields[6], 4),
                "currentIntegrity01": round(fields[7], 4),
                "visualBuckling01": round(fields[8], 4),
                "executionMicroseconds": round(fields[9], 4),
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "equippedSuitHash": fields[11],
                "equippedSuitHashHex": f"0x{fields[11]:08X}",
                "tickIntervalSeconds": round(fields[12], 4),
                "signalFlags": signal_flags,
                "signalFlagLabels": signal_labels,
                "unknownSignalFlags": unknown_signal_flags,
                "reserved0": fields[14],
                "initialized": bool(flags & (1 << 0)),
                "warning": bool(flags & (1 << 1)),
                "buckling": bool(flags & (1 << 2)),
                "imploded": bool(flags & (1 << 3)),
                "nonFinitePressure": bool(flags & (1 << 4)),
                "overBudget": bool(flags & (1 << 5)),
                "mockProfile": bool(flags & (1 << 6)),
                "csvProfile": bool(flags & (1 << 7)),
                "acousticGroan": bool(flags & (1 << 8)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if declared_count > SUIT_INTEGRITY_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if source_hash != SUIT_INTEGRITY_SOURCE_HASH:
        warnings.append("source_hash_mismatch")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if any(entry.get("unknownSignalFlags") for entry in entries):
        warnings.append("unknown_signal_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("reserved0") for entry in entries):
        warnings.append("reserved_nonzero")
    if any(entry.get("imploded") for entry in entries):
        warnings.append("imploded")
    if any(entry.get("warning") for entry in entries):
        warnings.append("pressure_warning")
    if any(entry.get("buckling") for entry in entries):
        warnings.append("buckling")
    if any(entry.get("nonFinitePressure") for entry in entries):
        warnings.append("nonfinite_pressure")
    if any(entry.get("overBudget") for entry in entries):
        warnings.append("over_budget")
    if any(entry.get("executionMicroseconds", 0.0) > SUIT_INTEGRITY_TICK_BUDGET_MICROSECONDS for entry in entries):
        warnings.append("execution_over_100us")
    if any(entry.get("currentIntegrity01", 1.0) <= 0.05 for entry in entries):
        warnings.append("integrity_critical")
    return {
        "type": "suit_integrity_blackbox",
        "version": version,
        "headerBytes": SUIT_INTEGRITY_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": declared_count,
        "telemetryCursor": telemetry_cursor,
        "sourceHash": source_hash,
        "sourceHashHex": f"0x{source_hash:08X}",
        "frameCounter": frame_counter,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_radiation_mutation_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMPSHINOBU324BIN",
        "DUMPSHINOBU324H8DUMP",
        "DUMPRADIATIONMUTATIONBIN",
        "DUMPRADIATIONMUTATIONH8DUMP",
        "DUMPMUTATIONAUTOPSYBIN",
        "DUMPMUTATIONAUTOPSYH8DUMP",
    }


def parse_radiation_mutation_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < RADIATION_MUTATION_HEADER_BYTES:
        return {
            "type": "radiation_mutation_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, declared_count, entry_size, telemetry_cursor, source_hash, frame_counter = RADIATION_MUTATION_HEADER.unpack_from(data, 0)
    if (
        magic != RADIATION_MUTATION_MAGIC
        or version != RADIATION_MUTATION_VERSION
        or entry_size != RADIATION_MUTATION_ENTRY_BYTES
        or declared_count <= 0
    ):
        return {
            "type": "radiation_mutation_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = RADIATION_MUTATION_HEADER_BYTES
    expected_bytes = payload_offset + declared_count * entry_size
    readable_entries = min(
        declared_count,
        RADIATION_MUTATION_TELEMETRY_CAPACITY,
        max(0, len(data) - payload_offset) // entry_size,
    )
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = RADIATION_MUTATION_ENTRY.unpack_from(data, offset)
        flags = fields[2]
        labels, unknown_flags = resolve_bit_labels(flags, RADIATION_MUTATION_FLAG_LABELS)
        if any(not math.isfinite(value) for value in fields[3:13]):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "frame": fields[1],
                "stateHash": fields[0],
                "stateHashHex": f"0x{fields[0]:016X}",
                "flags": flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "cumulativeDoseRad": round(fields[3], 4),
                "currentExposureRate": round(fields[4], 4),
                "attenuatedDoseRad": round(fields[5], 4),
                "mutationSeverity01": round(fields[6], 4),
                "maxStaminaPenalty": round(fields[7], 4),
                "healingSuppression01": round(fields[8], 4),
                "globalQualityWeight": round(fields[9], 4),
                "executionMicroseconds": round(fields[10], 4),
                "metabolicToxicity": round(fields[11], 4),
                "vfxIntensity01": round(fields[12], 4),
                "ringCursor": fields[13],
                "sourceHash": fields[14],
                "sourceHashHex": f"0x{fields[14]:08X}",
                "active": bool(flags & (1 << 0)),
                "critical": bool(flags & (1 << 1)),
                "healing": bool(flags & (1 << 2)),
                "mockDose": bool(flags & (1 << 3)),
                "toxicBloodVfxRequested": bool(flags & (1 << 4)),
                "complexNoiseAdmitted": bool(flags & (1 << 5)),
                "metabolicBridgeApplied": bool(flags & (1 << 6)),
                "csvProfile": bool(flags & (1 << 7)),
                "nonFiniteSanitized": bool(flags & (1 << 30)),
                "overBudget": bool(flags & (1 << 31)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if declared_count > RADIATION_MUTATION_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if source_hash != RADIATION_MUTATION_SOURCE_HASH:
        warnings.append("source_hash_mismatch")
    if any(entry.get("sourceHash") != RADIATION_MUTATION_SOURCE_HASH for entry in entries):
        warnings.append("entry_source_hash_mismatch")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("critical") for entry in entries):
        warnings.append("critical")
    if any(entry.get("nonFiniteSanitized") for entry in entries):
        warnings.append("nonfinite_sanitized")
    if any(entry.get("overBudget") for entry in entries):
        warnings.append("over_budget")
    if any(entry.get("mutationSeverity01", 0.0) >= 1.0 for entry in entries):
        warnings.append("mutation_severity_max")
    if any(entry.get("cumulativeDoseRad", 0.0) >= RADIATION_MUTATION_DEFAULT_FATAL_DOSE_RAD for entry in entries):
        warnings.append("fatal_dose_reached")
    if any(entry.get("metabolicToxicity", 0.0) > 0.0 for entry in entries):
        warnings.append("metabolic_toxicity")
    return {
        "type": "radiation_mutation_blackbox",
        "version": version,
        "headerBytes": RADIATION_MUTATION_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": declared_count,
        "telemetryCursor": telemetry_cursor,
        "sourceHash": source_hash,
        "sourceHashHex": f"0x{source_hash:08X}",
        "frameCounter": frame_counter,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_respawn_reconciliation_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMPSHINOBU329BIN",
        "DUMPSHINOBU329H8DUMP",
        "DUMPRECONCILIATIONSURGEONBIN",
        "DUMPRECONCILIATIONSURGEONH8DUMP",
        "DUMPRESPAWNRECONCILIATIONBIN",
        "DUMPRESPAWNRECONCILIATIONH8DUMP",
    }


def parse_respawn_reconciliation_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < RESPAWN_RECONCILIATION_HEADER_BYTES:
        return {
            "type": "respawn_reconciliation_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, declared_count, telemetry_cursor, reason_flags = RESPAWN_RECONCILIATION_HEADER.unpack_from(data, 0)
    if (
        magic != RESPAWN_RECONCILIATION_MAGIC
        or version != RESPAWN_RECONCILIATION_VERSION
        or declared_count <= 0
    ):
        return {
            "type": "respawn_reconciliation_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = RESPAWN_RECONCILIATION_HEADER_BYTES
    entry_size = RESPAWN_RECONCILIATION_ENTRY_BYTES
    expected_bytes = payload_offset + declared_count * entry_size
    readable_entries = min(
        declared_count,
        RESPAWN_RECONCILIATION_TELEMETRY_CAPACITY,
        max(0, len(data) - payload_offset) // entry_size,
    )
    reason_labels, unknown_reason_flags = resolve_bit_labels(reason_flags, RESPAWN_RECONCILIATION_FLAG_LABELS)
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = payload_offset + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = RESPAWN_RECONCILIATION_ENTRY.unpack_from(data, offset)
        flags = fields[9]
        dropped_item_count = (flags & RESPAWN_RECONCILIATION_DROPPED_ITEM_MASK) >> RESPAWN_RECONCILIATION_DROPPED_ITEM_SHIFT
        label_flags = flags & ~RESPAWN_RECONCILIATION_DROPPED_ITEM_MASK
        labels, unknown_flags = resolve_bit_labels(label_flags, RESPAWN_RECONCILIATION_FLAG_LABELS)
        death_aup = fields[0:3]
        respawn_aup = fields[3:6]
        if any(not math.isfinite(value) for value in (*death_aup, *respawn_aup, fields[8])):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "deathAup": {
                    "x": round(fields[0], 4),
                    "y": round(fields[1], 4),
                    "z": round(fields[2], 4),
                },
                "respawnAup": {
                    "x": round(fields[3], 4),
                    "y": round(fields[4], 4),
                    "z": round(fields[5], 4),
                },
                "causeHash": fields[6],
                "causeHashHex": f"0x{fields[6]:08X}",
                "frame": fields[7],
                "reconcileMicroseconds": round(fields[8], 4),
                "flags": fields[9],
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "droppedItemCount": dropped_item_count,
                "respawnActive": bool(flags & (1 << 0)),
                "pendingRequest": bool(flags & (1 << 1)),
                "penaltyApplied": bool(flags & (1 << 2)),
                "mockMedicalBay": bool(flags & (1 << 3)),
                "fallbackLifepod": bool(flags & (1 << 4)),
                "invalidTargetAup": bool(flags & (1 << 5)),
                "committed": bool(flags & (1 << 6)),
                "manualTuning": bool(flags & (1 << 7)),
                "deathSequenceBlackoutPrimed": bool(flags & (1 << 10)),
                "nanDetected": bool(flags & (1 << 31)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if declared_count > RESPAWN_RECONCILIATION_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if unknown_reason_flags:
        warnings.append("unknown_reason_flags")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if reason_flags:
        warnings.append("reason_flags")
    if any(entry.get("nanDetected") for entry in entries) or (reason_flags & (1 << 31)):
        warnings.append("nan_detected")
    if any(entry.get("invalidTargetAup") for entry in entries) or (reason_flags & (1 << 5)):
        warnings.append("invalid_target_aup")
    if any(entry.get("fallbackLifepod") for entry in entries):
        warnings.append("fallback_lifepod")
    if any(entry.get("penaltyApplied") for entry in entries):
        warnings.append("penalty_applied")
    if any(entry.get("committed") for entry in entries):
        warnings.append("committed")
    if any(entry.get("droppedItemCount", 0) > 0 for entry in entries):
        warnings.append("dropped_items")
    return {
        "type": "respawn_reconciliation_blackbox",
        "version": version,
        "headerBytes": RESPAWN_RECONCILIATION_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": declared_count,
        "telemetryCursor": telemetry_cursor,
        "reasonFlags": reason_flags,
        "reasonFlagLabels": reason_labels,
        "unknownReasonFlags": unknown_reason_flags,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def try_parse_magic_identified_blackbox(data: bytes) -> dict[str, Any] | None:
    if len(data) < 4:
        return None

    magic = struct.unpack_from("<I", data, 0)[0]
    if has_marine_snow_vfx_blackbox_signature(data):
        return parse_marine_snow_vfx_blackbox(data)
    if has_propwash_gpu_blackbox_signature(data):
        return parse_propwash_gpu_blackbox(data)
    if has_sargassum_food_chain_signature(data):
        return parse_sargassum_food_chain_blackbox(data)
    if has_sargassum_boid_sensory_signature(data):
        return parse_sargassum_boid_sensory_blackbox(data)
    if len(data) >= 8:
        magic64 = struct.unpack_from("<Q", data, 0)[0]
        if magic64 == ORIGIN_SHIFT_MAGIC:
            return parse_origin_shift_blackbox(data)
        if magic64 == LOCKSTEP_STATE_VALIDATOR_MAGIC:
            return parse_lockstep_state_validator_blackbox(data)
        if magic64 == METABOLISM_BLACKBOX_MAGIC:
            return parse_metabolism_blackbox(data)
        if magic64 == PHYSIOLOGY_AUTOPSY_MAGIC:
            return parse_physiology_autopsy_blackbox(data)
        if magic64 == SENSORY_IMPAIRMENT_MAGIC:
            return parse_sensory_impairment_blackbox(data)
        if magic64 == SUIT_INTEGRITY_MAGIC:
            return parse_suit_integrity_blackbox(data)
        if magic64 == RADIATION_MUTATION_MAGIC:
            return parse_radiation_mutation_blackbox(data)
        if magic64 == RESPAWN_RECONCILIATION_MAGIC:
            return parse_respawn_reconciliation_blackbox(data)
        if magic64 == BASE_ATMOSPHERE_LOGISTICS_MAGIC:
            return parse_base_atmosphere_logistics_blackbox(data)
        if magic64 == THERMODYNAMICS_HAZARD_MAGIC:
            return parse_thermodynamics_hazard_blackbox(data)
        if magic64 == VEGETATION_MEMORY_MAGIC:
            return parse_vegetation_memory_blackbox(data)
        if magic64 == CHEMICAL_INFLUENCE_MAGIC:
            return parse_chemical_influence_blackbox(data)

    magic_parsers = (
        (DATA_MONOLITH_TELEMETRY_MAGIC, parse_data_monolith_telemetry_blackbox),
        (VOCAL_WARNING_TELEMETRY_MAGIC, parse_vocal_warning_telemetry_blackbox),
        (VOCAL_BANK_SYNTHESIS_MAGIC, parse_vocal_bank_synthesis_blackbox),
        (CAMERA_JUICE_TELEMETRY_MAGIC, parse_camera_juice_telemetry_blackbox),
        (MATERIAL_DECAY_MAGIC, parse_material_decay_blackbox),
        (INTERACTIVE_WAKE_MAGIC, parse_interactive_wake_blackbox),
        (FLORA_SWAY_FIELD_MAGIC, parse_flora_sway_field_blackbox),
        (FLORA_AMBIENT_SWAY_MAGIC, parse_flora_ambient_sway_blackbox),
        (CARVE_DEBRIS_MAGIC, parse_carve_debris_blackbox),
        (BIOLUM_PULSE_MAGIC, parse_biolum_pulse_blackbox),
        (BIOLUM_DIRECTOR_MAGIC, parse_biolum_director_blackbox),
        (TOXIC_OUTGASSING_MAGIC, parse_toxic_outgassing_blackbox),
        (GAS_DYNAMICS_MAGIC, parse_gas_dynamics_blackbox),
        (STORM_PROPAGATION_MAGIC, parse_storm_propagation_blackbox),
        (OCEAN_SURFACE_ATMOSPHERE_MAGIC, parse_ocean_surface_atmosphere_blackbox),
        (FOVEATED_SIMULATION_MAGIC, parse_foveated_simulation_blackbox),
        (BINARY_LAYOUT_SENTINEL_MAGIC, parse_binary_layout_sentinel),
        (TERMINAL_OS_MAGIC, parse_terminal_os_blackbox),
        (TERMINAL_DECRYPTION_MAGIC, parse_terminal_decryption_blackbox),
        (TERMINAL_PROJECTION_MAGIC, parse_terminal_projection_blackbox),
        (VEHICLE_DAMAGE_HOLOGRAPHER_MAGIC, parse_vehicle_damage_holographer_dump),
        (PDA_PROJECTION_MAGIC, parse_pda_projection_blackbox),
        (WRIST_HUD_MAGIC, parse_wrist_hud_blackbox),
        (LADDER_CLIMB_IK_MAGIC, parse_ladder_climb_ik_blackbox),
        (TOPOGRAPHICAL_SONAR_MAGIC, parse_topographical_sonar_blackbox),
        (KINETIC_CHARACTER_MAGIC, parse_kinetic_character_blackbox),
        (PROCEDURAL_BONE_MAGIC, parse_procedural_bone_blackbox),
        (VR_SOMATIC_MAGIC, parse_vr_somatic_blackbox),
        (LASER_CUTTER_DOD_MAGIC, parse_laser_cutter_dod_blackbox),
        (WFC_LASER_CUT_MAGIC, parse_wfc_laser_cut_blackbox),
        (TOOL_KINEMATICS_MAGIC, parse_tool_kinematics_blackbox),
        (COMPASS_GYRO_MAGIC, parse_compass_gyro_blackbox),
        (PDA_ENCYCLOPEDIA_MAGIC, parse_pda_encyclopedia_blackbox),
    )
    for expected_magic, parser in magic_parsers:
        if magic == expected_magic:
            return parser(data)
    return None


def is_pda_frequency_tuning_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPMINIGAMEFREQUENCYTUNINGBIN", "DUMPMINIGAMEFREQUENCYTUNINGH8DUMP"}


def parse_pda_frequency_tuning_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < PDA_FREQUENCY_TUNING_HEADER.size:
        return {
            "type": "pda_frequency_tuning_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    declared_capacity, cursor = PDA_FREQUENCY_TUNING_HEADER.unpack_from(data, 0)
    if (
        declared_capacity != PDA_FREQUENCY_TUNING_TELEMETRY_CAPACITY
        or cursor < 0
        or cursor >= declared_capacity
    ):
        return {
            "type": "pda_frequency_tuning_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    readable_entries = min(declared_capacity, max(0, (len(data) - PDA_FREQUENCY_TUNING_HEADER_BYTES) // PDA_FREQUENCY_TUNING_ENTRY_BYTES))
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = PDA_FREQUENCY_TUNING_HEADER_BYTES + index * PDA_FREQUENCY_TUNING_ENTRY_BYTES
        if is_empty_entry(data, offset, PDA_FREQUENCY_TUNING_ENTRY_BYTES):
            continue

        fields = PDA_FREQUENCY_TUNING_ENTRY.unpack_from(data, offset)
        flags = fields[9]
        labels, unknown_flags = resolve_bit_labels(flags, PDA_FREQUENCY_TUNING_FLAG_LABELS)
        float_values = fields[2:7]
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "artifactHash": fields[1],
                "artifactHashHex": f"0x{fields[1]:08X}",
                "targetFrequency": round(fields[2], 4),
                "targetAmplitude": round(fields[3], 4),
                "playerFrequency": round(fields[4], 4),
                "playerAmplitude": round(fields[5], 4),
                "error01": round(fields[6], 4),
                "holdPermille": fields[7],
                "stage": fields[8],
                "flags": flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "stage0Locked": bool(flags & 0x01),
                "stage1Locked": bool(flags & 0x02),
                "stage2Locked": bool(flags & 0x04),
                "allStagesLocked": (flags & 0x07) == 0x07,
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    expected_bytes = PDA_FREQUENCY_TUNING_HEADER_BYTES + declared_capacity * PDA_FREQUENCY_TUNING_ENTRY_BYTES
    warnings = []
    if readable_entries < declared_capacity:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) - PDA_FREQUENCY_TUNING_HEADER_BYTES > 0 and (len(data) - PDA_FREQUENCY_TUNING_HEADER_BYTES) % PDA_FREQUENCY_TUNING_ENTRY_BYTES != 0:
        warnings.append("trailing_partial_entry")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("stage", 0) >= 3 for entry in entries):
        warnings.append("stage_out_of_range")
    if any(entry.get("holdPermille", 0) > 1000 for entry in entries):
        warnings.append("hold_overflow")
    if any(entry.get("allStagesLocked") for entry in entries):
        warnings.append("all_stages_locked")
    return {
        "type": "pda_frequency_tuning_blackbox",
        "headerBytes": PDA_FREQUENCY_TUNING_HEADER_BYTES,
        "entrySize": PDA_FREQUENCY_TUNING_ENTRY_BYTES,
        "declaredEntryCount": declared_capacity,
        "cursor": cursor,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_compass_gyro_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPCOMPASSGYROSTABILIZERBIN", "DUMPCOMPASSGYROSTABILIZERH8DUMP"}


def parse_compass_gyro_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < COMPASS_GYRO_HEADER.size:
        return {
            "type": "compass_gyro_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, declared_capacity, cursor = COMPASS_GYRO_HEADER.unpack_from(data, 0)
    if (
        magic != COMPASS_GYRO_MAGIC
        or declared_capacity != COMPASS_GYRO_BLACKBOX_CAPACITY
        or cursor < 0
        or cursor >= declared_capacity
    ):
        return {
            "type": "compass_gyro_blackbox",
            "magic": magic,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    readable_entries = min(declared_capacity, max(0, (len(data) - COMPASS_GYRO_HEADER_BYTES) // COMPASS_GYRO_ENTRY_BYTES))
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = COMPASS_GYRO_HEADER_BYTES + index * COMPASS_GYRO_ENTRY_BYTES
        if is_empty_entry(data, offset, COMPASS_GYRO_ENTRY_BYTES):
            continue

        fields = COMPASS_GYRO_ENTRY.unpack_from(data, offset)
        flags = fields[7]
        labels, unknown_flags = resolve_bit_labels(flags, COMPASS_GYRO_FLAG_LABELS)
        float_values = fields[1:7]
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "actualHeadingDegrees": round(fields[1], 4),
                "currentHeadingDegrees": round(fields[2], 4),
                "driftDegrees": round(fields[3], 4),
                "maxGyroDriftDegrees": round(fields[4], 4),
                "anomalyInterference01": round(fields[5], 4),
                "power01": round(fields[6], 4),
                "flags": flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "lastAupShiftFrameId": fields[8],
                "calibrationCount": fields[9],
                "initialized": bool(flags & 0x001),
                "powered": bool(flags & 0x002),
                "anomalyUnstable": bool(flags & 0x004),
                "stressSlowCadence": bool(flags & 0x008),
                "calibrationApplied": bool(flags & 0x010),
                "nonFiniteFallback": bool(flags & 0x020),
                "reducedQualityNoise": bool(flags & 0x040),
                "hasPreviousAup": bool(flags & 0x100),
                "calibrationRequested": bool(flags & 0x200),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    expected_bytes = COMPASS_GYRO_HEADER_BYTES + declared_capacity * COMPASS_GYRO_ENTRY_BYTES
    warnings = []
    if readable_entries < declared_capacity:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) - COMPASS_GYRO_HEADER_BYTES > 0 and (len(data) - COMPASS_GYRO_HEADER_BYTES) % COMPASS_GYRO_ENTRY_BYTES != 0:
        warnings.append("trailing_partial_entry")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen or any(entry.get("nonFiniteFallback") for entry in entries):
        warnings.append("nonfinite")
    if any(entry.get("anomalyUnstable") for entry in entries):
        warnings.append("anomaly_unstable")
    if any(entry.get("stressSlowCadence") for entry in entries):
        warnings.append("stress_slow_cadence")
    if any(entry.get("reducedQualityNoise") for entry in entries):
        warnings.append("reduced_quality_noise")
    if any(entry.get("power01", 0) < 0 or entry.get("power01", 0) > 1 for entry in entries):
        warnings.append("power_out_of_range")
    if any(
        abs(entry.get("driftDegrees", 0)) > entry.get("maxGyroDriftDegrees", 0) + 0.001
        for entry in entries
        if entry.get("maxGyroDriftDegrees", 0) > 0
    ):
        warnings.append("drift_over_max")
    return {
        "type": "compass_gyro_blackbox",
        "magic": magic,
        "headerBytes": COMPASS_GYRO_HEADER_BYTES,
        "entrySize": COMPASS_GYRO_ENTRY_BYTES,
        "declaredEntryCount": declared_capacity,
        "cursor": cursor,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_pda_encyclopedia_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMPPDAENCYCLOPEDIASTREAMERBLACKBOXBIN", "DUMPPDAENCYCLOPEDIASTREAMERBLACKBOXH8DUMP"}


def compute_pda_encyclopedia_state_hash(fields: tuple[Any, ...]) -> int:
    hash_value = 2166136261
    for value in (fields[0], fields[2], fields[5], fields[6], fields[7], fields[10], fields[11]):
        hash_value = fnv1a_mix_u32(hash_value, value)
    return hash_value


def parse_pda_encyclopedia_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < PDA_ENCYCLOPEDIA_HEADER.size:
        return {
            "type": "pda_encyclopedia_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, dump_frame, last_fault_hash, declared_capacity, entry_size, active_entry_hash = PDA_ENCYCLOPEDIA_HEADER.unpack_from(data, 0)
    if (
        magic != PDA_ENCYCLOPEDIA_MAGIC
        or declared_capacity != PDA_ENCYCLOPEDIA_TELEMETRY_CAPACITY
        or entry_size != PDA_ENCYCLOPEDIA_ENTRY_BYTES
    ):
        return {
            "type": "pda_encyclopedia_blackbox",
            "magic": magic,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    readable_entries = min(declared_capacity, max(0, (len(data) - PDA_ENCYCLOPEDIA_HEADER_BYTES) // entry_size))
    entries = []
    hash_mismatch_seen = False
    for index in range(readable_entries):
        offset = PDA_ENCYCLOPEDIA_HEADER_BYTES + index * entry_size
        if is_empty_entry(data, offset, entry_size):
            continue

        fields = PDA_ENCYCLOPEDIA_ENTRY.unpack_from(data, offset)
        flags = fields[10]
        stream_state = flags & 0xFF
        source_id = (flags >> 8) & 0x7
        stream_state_label = PDA_ENCYCLOPEDIA_STREAM_STATE_LABELS.get(stream_state, f"unknown-{stream_state}")
        source_label = PDA_ENCYCLOPEDIA_SOURCE_LABELS.get(source_id, f"unknown-{source_id}")
        flag_labels = [f"stream-{stream_state_label}"]
        if source_id:
            flag_labels.append(f"source-{source_label}")
        if flags & PDA_ENCYCLOPEDIA_CANVAS_SPLIT_FLAG:
            flag_labels.append("canvas-split")
        unknown_flags = flags & ~PDA_ENCYCLOPEDIA_KNOWN_FLAG_MASK
        computed_hash = compute_pda_encyclopedia_state_hash(fields)
        state_hash_ok = computed_hash == fields[1]
        hash_mismatch_seen |= not state_hash_ok
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "stateHash": fields[1],
                "computedStateHash": computed_hash,
                "stateHashOk": state_hash_ok,
                "entryHash": fields[2],
                "entryHashHex": f"0x{fields[2]:08X}",
                "unlockedCount": fields[3],
                "charsRenderedThisFrame": fields[4],
                "visibleChars": fields[5],
                "decodedChars": fields[6],
                "sourceBytes": fields[7],
                "decodeTicks": fields[8],
                "canvasTicks": fields[9],
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "streamState": stream_state,
                "streamStateLabel": stream_state_label,
                "sourceId": source_id,
                "sourceLabel": source_label,
                "canvasSplit": bool(flags & PDA_ENCYCLOPEDIA_CANVAS_SPLIT_FLAG),
                "faultHash": fields[11],
                "faultHashHex": f"0x{fields[11]:08X}",
                "cursorByte": fields[12],
                "capacity": fields[13],
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    expected_bytes = PDA_ENCYCLOPEDIA_HEADER_BYTES + declared_capacity * entry_size
    warnings = []
    if readable_entries < declared_capacity:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) - PDA_ENCYCLOPEDIA_HEADER_BYTES > 0 and (len(data) - PDA_ENCYCLOPEDIA_HEADER_BYTES) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if hash_mismatch_seen:
        warnings.append("state_hash_mismatch")
    if last_fault_hash or any(entry.get("faultHash") for entry in entries):
        warnings.append("fault_hash")
    if any(entry.get("streamStateLabel") == "fault" for entry in entries):
        warnings.append("stream_fault")
    if any(entry.get("decodeTicks", 0) < 0 or entry.get("canvasTicks", 0) < 0 for entry in entries):
        warnings.append("negative_ticks")
    if any(entry.get("capacity", 0) and entry.get("visibleChars", 0) > entry.get("capacity", 0) for entry in entries):
        warnings.append("visible_chars_over_capacity")
    return {
        "type": "pda_encyclopedia_blackbox",
        "magic": magic,
        "headerBytes": PDA_ENCYCLOPEDIA_HEADER_BYTES,
        "entrySize": entry_size,
        "declaredEntryCount": declared_capacity,
        "dumpFrame": dump_frame,
        "lastFaultHash": last_fault_hash,
        "lastFaultHashHex": f"0x{last_fault_hash:08X}",
        "activeEntryHash": active_entry_hash,
        "activeEntryHashHex": f"0x{active_entry_hash:08X}",
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_habitat_flood_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {
        "DUMP1306CONSTRUCTIONHABITATINTEGRITYBIN",
        "DUMP1306CONSTRUCTIONHABITATINTEGRITYH8DUMP",
        "DUMP1306CONSTRUCTIONMODULESTRESSBIN",
        "DUMP1306CONSTRUCTIONMODULESTRESSH8DUMP",
    }


def parse_habitat_flood_blackbox(path: Path, data: bytes) -> dict[str, Any]:
    if len(data) < HABITAT_FLOOD_HEADER.size:
        return {
            "type": "habitat_flood_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_header"],
        }

    magic, version, declared_capacity, cursor, reason_flags = HABITAT_FLOOD_HEADER.unpack_from(data, 0)
    if (
        magic != HABITAT_FLOOD_MAGIC
        or version != HABITAT_FLOOD_VERSION
        or declared_capacity != HABITAT_FLOOD_BLACKBOX_CAPACITY
        or cursor >= declared_capacity
    ):
        return {
            "type": "habitat_flood_blackbox",
            "magic": magic,
            "version": version,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    readable_entries = min(declared_capacity, max(0, (len(data) - HABITAT_FLOOD_HEADER_BYTES) // HABITAT_FLOOD_ENTRY_BYTES))
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = HABITAT_FLOOD_HEADER_BYTES + index * HABITAT_FLOOD_ENTRY_BYTES
        if is_empty_entry(data, offset, HABITAT_FLOOD_ENTRY_BYTES):
            continue

        fields = HABITAT_FLOOD_ENTRY.unpack_from(data, offset)
        flags = fields[9]
        labels, unknown_flags = resolve_bit_labels(flags, HABITAT_FLOOD_FLAG_LABELS)
        float_values = fields[5:9]
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "frame": fields[0],
                "nodeCount": fields[1],
                "edgeCount": fields[2],
                "floodedRoomCount": fields[3],
                "reserved0": fields[4],
                "baseTotalStress": round(fields[5], 4),
                "maxWaterLevel01": round(fields[6], 4),
                "totalWaterVolumeM3": round(fields[7], 4),
                "peakModuleStress": round(fields[8], 4),
                "flags": flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "stateHash": fields[10],
                "stateHashHex": f"0x{fields[10]:08X}",
                "deformationSequence": fields[11],
                "nonFinite": bool(flags & 0x01),
                "overflowClamped": bool(flags & 0x02),
                "traversalOverflow": bool(flags & 0x04),
                "topologyInvalid": bool(flags & 0x08),
                "moduleStressInvalid": bool(flags & 0x10),
            }
        )

    reason_labels, reason_unknown_flags = resolve_bit_labels(reason_flags, HABITAT_FLOOD_FLAG_LABELS)
    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    expected_bytes = HABITAT_FLOOD_HEADER_BYTES + declared_capacity * HABITAT_FLOOD_ENTRY_BYTES
    warnings = []
    if readable_entries < declared_capacity:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) - HABITAT_FLOOD_HEADER_BYTES > 0 and (len(data) - HABITAT_FLOOD_HEADER_BYTES) % HABITAT_FLOOD_ENTRY_BYTES != 0:
        warnings.append("trailing_partial_entry")
    if reason_unknown_flags or any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen or reason_flags & 0x01 or any(entry.get("nonFinite") for entry in entries):
        warnings.append("nonfinite")
    if reason_flags & 0x02 or any(entry.get("overflowClamped") for entry in entries):
        warnings.append("overflow_clamped")
    if reason_flags & 0x04 or any(entry.get("traversalOverflow") for entry in entries):
        warnings.append("traversal_overflow")
    if reason_flags & 0x08 or any(entry.get("topologyInvalid") for entry in entries):
        warnings.append("topology_invalid")
    if reason_flags & 0x10 or any(entry.get("moduleStressInvalid") for entry in entries):
        warnings.append("module_stress_invalid")
    if any(entry.get("maxWaterLevel01", 0) > 1.0 for entry in entries):
        warnings.append("water_level_over_one")
    mode = "module_stress" if "MODULESTRESS" in re.sub(r"[^A-Z0-9]", "", path.name.upper()) else "habitat_integrity"
    return {
        "type": "habitat_flood_blackbox",
        "mode": mode,
        "magic": magic,
        "version": version,
        "headerBytes": HABITAT_FLOOD_HEADER_BYTES,
        "entrySize": HABITAT_FLOOD_ENTRY_BYTES,
        "declaredEntryCount": declared_capacity,
        "cursor": cursor,
        "reasonFlags": reason_flags,
        "reasonFlagLabels": reason_labels,
        "reasonUnknownFlags": reason_unknown_flags,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_construction_validation_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMP1306CONSTRUCTIONVALIDATIONBIN", "DUMP1306CONSTRUCTIONVALIDATIONH8DUMP"}


def parse_construction_validation_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < CONSTRUCTION_VALIDATION_ENTRY_BYTES:
        return {
            "type": "construction_validation_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_payload"],
        }

    readable_entries = min(CONSTRUCTION_VALIDATION_TELEMETRY_CAPACITY, len(data) // CONSTRUCTION_VALIDATION_ENTRY_BYTES)
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = index * CONSTRUCTION_VALIDATION_ENTRY_BYTES
        if is_empty_entry(data, offset, CONSTRUCTION_VALIDATION_ENTRY_BYTES):
            continue

        fields = CONSTRUCTION_VALIDATION_ENTRY.unpack_from(data, offset)
        flags = fields[7]
        labels, unknown_flags = resolve_bit_labels(flags, CONSTRUCTION_VALIDATION_FLAG_LABELS)
        float_values = fields[0:3] + fields[8:10]
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "rootAup": {"x": round(fields[0], 4), "y": round(fields[1], 4), "z": round(fields[2], 4)},
                "gridPos": {"x": fields[3], "y": fields[4], "z": fields[5]},
                "frame": fields[6],
                "failureFlags": flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "minSdfDistance": round(fields[8], 4),
                "validationComputeTimeMs": round(fields[9], 4),
                "buildRequestsValidated": fields[10],
                "graphSplices": fields[11],
                "resultHash": fields[12],
                "resultHashHex": f"0x{fields[12]:08X}",
                "occupiedGridCell": bool(flags & 0x01),
                "terrainIntersection": bool(flags & 0x02),
                "portMismatch": bool(flags & 0x04),
                "structuralWarning": bool(flags & 0x08),
                "nonFiniteInput": bool(flags & 0x10),
                "outsideBounds": bool(flags & 0x20),
                "graphCapacity": bool(flags & 0x40),
                "disconnectedWing": bool(flags & 0x80),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) % CONSTRUCTION_VALIDATION_ENTRY_BYTES != 0:
        warnings.append("trailing_partial_entry")
    if len(data) // CONSTRUCTION_VALIDATION_ENTRY_BYTES > CONSTRUCTION_VALIDATION_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen or any(entry.get("nonFiniteInput") for entry in entries):
        warnings.append("nonfinite")
    if any(entry.get("terrainIntersection") for entry in entries):
        warnings.append("terrain_intersection")
    if any(entry.get("occupiedGridCell") for entry in entries):
        warnings.append("occupied_grid_cell")
    if any(entry.get("portMismatch") for entry in entries):
        warnings.append("port_mismatch")
    if any(entry.get("structuralWarning") for entry in entries):
        warnings.append("structural_warning")
    if any(entry.get("outsideBounds") for entry in entries):
        warnings.append("outside_bounds")
    if any(entry.get("graphCapacity") or entry.get("disconnectedWing") for entry in entries):
        warnings.append("graph_route_fault")
    return {
        "type": "construction_validation_blackbox",
        "entrySize": CONSTRUCTION_VALIDATION_ENTRY_BYTES,
        "declaredEntryCount": readable_entries,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_construction_socket_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMP1306CONSTRUCTIONSOCKETTELEMETRYBIN", "DUMP1306CONSTRUCTIONSOCKETTELEMETRYH8DUMP"}


def parse_construction_socket_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < CONSTRUCTION_SOCKET_ENTRY_BYTES:
        return {
            "type": "construction_socket_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_payload"],
        }

    readable_entries = min(CONSTRUCTION_SOCKET_TELEMETRY_CAPACITY, len(data) // CONSTRUCTION_SOCKET_ENTRY_BYTES)
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = index * CONSTRUCTION_SOCKET_ENTRY_BYTES
        if is_empty_entry(data, offset, CONSTRUCTION_SOCKET_ENTRY_BYTES):
            continue

        fields = CONSTRUCTION_SOCKET_ENTRY.unpack_from(data, offset)
        flags = fields[9]
        labels, unknown_flags = resolve_bit_labels(flags, CONSTRUCTION_SOCKET_FLAG_LABELS)
        float_values = fields[0:3] + fields[7:9] + (fields[11],)
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "previewAup": {"x": round(fields[0], 4), "y": round(fields[1], 4), "z": round(fields[2], 4)},
                "frame": fields[3],
                "activeSocketCount": fields[4],
                "evaluatedCandidateCount": fields[5],
                "acceptedSnapCount": fields[6],
                "solverMicroseconds": round(fields[7], 4),
                "bestDistanceSq": round(fields[8], 4),
                "flags": flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "resultHash": fields[10],
                "resultHashHex": f"0x{fields[10]:08X}",
                "globalQualityWeight": round(fields[11], 4),
                "topologyVersion": fields[12],
                "connected": bool(flags & 0x001),
                "collisionBlocked": bool(flags & 0x008),
                "nonFinite": bool(flags & 0x010),
                "validSnap": bool(flags & 0x020),
                "pendingCommit": bool(flags & 0x040),
                "topologyDirty": bool(flags & 0x080),
                "rollbackFence": bool(flags & 0x100),
                "dearLieActive": bool(flags & 0x200),
                "capacityExceeded": bool(flags & 0x400),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) % CONSTRUCTION_SOCKET_ENTRY_BYTES != 0:
        warnings.append("trailing_partial_entry")
    if len(data) // CONSTRUCTION_SOCKET_ENTRY_BYTES > CONSTRUCTION_SOCKET_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen or any(entry.get("nonFinite") for entry in entries):
        warnings.append("nonfinite")
    if any(entry.get("collisionBlocked") for entry in entries):
        warnings.append("collision_blocked")
    if any(entry.get("capacityExceeded") for entry in entries):
        warnings.append("capacity_exceeded")
    if any(entry.get("rollbackFence") for entry in entries):
        warnings.append("rollback_fence")
    if any(entry.get("topologyDirty") for entry in entries):
        warnings.append("topology_dirty")
    if any(entry.get("solverMicroseconds", 0) > 500 for entry in entries):
        warnings.append("solver_over_500us")
    return {
        "type": "construction_socket_blackbox",
        "entrySize": CONSTRUCTION_SOCKET_ENTRY_BYTES,
        "declaredEntryCount": readable_entries,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def is_construction_holography_blackbox_path(path: Path) -> bool:
    normalized = re.sub(r"[^A-Z0-9]", "", path.name.upper())
    return normalized in {"DUMP1306CONSTRUCTIONHOLOGRAPHYBIN", "DUMP1306CONSTRUCTIONHOLOGRAPHYH8DUMP"}


def parse_construction_holography_blackbox(data: bytes) -> dict[str, Any]:
    if len(data) < CONSTRUCTION_HOLOGRAPHY_ENTRY_BYTES:
        return {
            "type": "construction_holography_blackbox",
            "entries": [],
            "latest": None,
            "warnings": ["truncated_payload"],
        }

    readable_entries = min(CONSTRUCTION_HOLOGRAPHY_TELEMETRY_CAPACITY, len(data) // CONSTRUCTION_HOLOGRAPHY_ENTRY_BYTES)
    entries = []
    nonfinite_seen = False
    for index in range(readable_entries):
        offset = index * CONSTRUCTION_HOLOGRAPHY_ENTRY_BYTES
        if is_empty_entry(data, offset, CONSTRUCTION_HOLOGRAPHY_ENTRY_BYTES):
            continue

        fields = CONSTRUCTION_HOLOGRAPHY_ENTRY.unpack_from(data, offset)
        flags = fields[6]
        labels, unknown_flags = resolve_bit_labels(flags, CONSTRUCTION_HOLOGRAPHY_FLAG_LABELS)
        float_values = fields[0:3] + fields[7:9] + (fields[10],)
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "targetAup": {"x": round(fields[0], 4), "y": round(fields[1], 4), "z": round(fields[2], 4)},
                "frame": fields[3],
                "prefabHashID": fields[4],
                "prefabHashHex": f"0x{fields[4]:08X}",
                "sdfCornerChecks": fields[5],
                "validationFlags": flags,
                "flagLabels": labels,
                "unknownFlags": unknown_flags,
                "solverMicroseconds": round(fields[7], 4),
                "minSdfDistance": round(fields[8], 4),
                "validationStateHash": fields[9],
                "validationStateHashHex": f"0x{fields[9]:08X}",
                "globalQualityWeight": round(fields[10], 4),
                "active": bool(flags & 0x001),
                "valid": bool(flags & 0x002),
                "gridSnapped": bool(flags & 0x004),
                "sdfBlocked": bool(flags & 0x008),
                "boundsBlocked": bool(flags & 0x010),
                "nonFinite": bool(flags & 0x020),
                "socketSnap": bool(flags & 0x040),
                "presentationOnly": bool(flags & 0x080),
                "dearLieActive": bool(flags & 0x100),
                "rollbackExcluded": bool(flags & 0x200),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) % CONSTRUCTION_HOLOGRAPHY_ENTRY_BYTES != 0:
        warnings.append("trailing_partial_entry")
    if len(data) // CONSTRUCTION_HOLOGRAPHY_ENTRY_BYTES > CONSTRUCTION_HOLOGRAPHY_TELEMETRY_CAPACITY:
        warnings.append("entry_capacity_exceeded")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen or any(entry.get("nonFinite") for entry in entries):
        warnings.append("nonfinite")
    if any(entry.get("sdfBlocked") for entry in entries):
        warnings.append("sdf_blocked")
    if any(entry.get("boundsBlocked") for entry in entries):
        warnings.append("bounds_blocked")
    if any(entry.get("rollbackExcluded") for entry in entries):
        warnings.append("rollback_excluded")
    if any(entry.get("solverMicroseconds", 0) > 500 for entry in entries):
        warnings.append("solver_over_500us")
    return {
        "type": "construction_holography_blackbox",
        "entrySize": CONSTRUCTION_HOLOGRAPHY_ENTRY_BYTES,
        "declaredEntryCount": readable_entries,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def parse_defrag_dump(path: Path, data: bytes) -> dict[str, Any]:
    entry_struct = DEFRAG_ENTRY_ALIGNED if len(data) % DEFRAG_ENTRY_ALIGNED.size == 0 else DEFRAG_ENTRY_PACK1
    if len(data) < entry_struct.size:
        return {"type": "memory_defrag", "entries": [], "memoryMap": [], "warnings": ["truncated_payload"]}
    entries = []
    offset = 0
    for _ in range(min(300, len(data) // entry_struct.size)):
        fields = entry_struct.unpack_from(data, offset)
        offset += entry_struct.size
        if not any(fields):
            continue
        entries.append(
            {
                "sequence": fields[0],
                "vaultGenerationId": fields[1],
                "blockCount": fields[2],
                "totalFreeSpaceBytes": fields[3],
                "largestContiguousBlockBytes": fields[4],
                "lastMovedBytes": fields[5],
                "totalMovedBytes": fields[6],
                "pendingMassiveMoveBytes": fields[7],
                "heapFragmentationRatio": round(fields[8], 6),
                "watchdogBreaches": fields[9],
                "flags": fields[10],
                "isFragmented": bool(fields[11]),
                "watchdogExceeded": bool(fields[12]),
            }
        )
    latest = entries[-1] if entries else None
    capped = cap_entries(entries)
    return {
        "type": "memory_defrag",
        "entrySize": entry_struct.size,
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "memoryMap": build_defrag_memory_map(latest),
        "warnings": [],
    }


def build_defrag_memory_map(latest: dict[str, Any] | None) -> list[dict[str, Any]]:
    if not latest:
        return []
    free = max(0, int(latest.get("totalFreeSpaceBytes") or 0))
    largest = max(0, int(latest.get("largestContiguousBlockBytes") or 0))
    moved = max(0, int(latest.get("totalMovedBytes") or 0))
    block_count = max(0, int(latest.get("blockCount") or 0))
    occupied = max(moved, largest, 1 if block_count > 0 else 0)
    blocks = []
    if occupied > 0:
        blocks.append({"state": "occupied", "bytes": occupied, "label": "occupied-estimate", "estimated": True})
    if largest > 0:
        blocks.append({"state": "free", "bytes": largest, "label": "largest-free", "estimated": True})
    remaining = max(0, free - largest)
    if remaining > 0:
        blocks.append({"state": "free-fragmented", "bytes": remaining, "label": "fragmented-free", "estimated": True})
    return blocks


def parse_thermal_dump(path: Path, data: bytes) -> dict[str, Any]:
    if len(data) < THERMAL_HEADER.size:
        return {"type": "thermal", "entries": [], "warnings": ["truncated_header"]}
    sequence, cursor = THERMAL_HEADER.unpack_from(data, 0)
    entries = []
    offset = THERMAL_HEADER.size
    for _ in range((len(data) - THERMAL_HEADER.size) // THERMAL_ENTRY_MANUAL.size):
        fields = THERMAL_ENTRY_MANUAL.unpack_from(data, offset)
        offset += THERMAL_ENTRY_MANUAL.size
        if not any(fields):
            continue
        entries.append(
            {
                "frame": fields[0],
                "sequence": fields[1],
                "actionMask": fields[2],
                "temperatureTenthsCelsius": fields[3],
                "severity": fields[4],
                "batteryPercent": fields[5],
                "batteryStatus": fields[6],
                "thermalStatus": fields[7],
                "flags": fields[8],
            }
        )
    latest = entries[-1] if entries else None
    capped = cap_entries(entries)
    return {
        "type": "thermal",
        "sequence": sequence,
        "cursor": cursor,
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": [],
    }


def parse_headered_entries(
    data: bytes,
    expected_magic: int,
    entry_struct: struct.Struct,
    parser_type: str,
    mapper: Any,
) -> dict[str, Any]:
    if len(data) < BIOMASS_HEADER.size:
        return {"type": parser_type, "entries": [], "warnings": ["truncated_header"]}
    magic, entry_count, entry_size, oldest_index, capacity = BIOMASS_HEADER.unpack_from(data, 0)
    if magic != expected_magic or entry_size != entry_struct.size:
        return {"type": parser_type, "entries": [], "warnings": ["invalid_header"]}
    readable = min(entry_count, (len(data) - BIOMASS_HEADER.size) // entry_size)
    entries = []
    offset = BIOMASS_HEADER.size
    for _ in range(readable):
        fields = entry_struct.unpack_from(data, offset)
        offset += entry_size
        entries.append(mapper(fields))
    latest = entries[-1] if entries else None
    capped = cap_entries(entries)
    return {
        "type": parser_type,
        "entrySize": entry_size,
        "entryCount": entry_count,
        "oldestIndex": oldest_index,
        "capacity": capacity,
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": ["payload_truncated"] if readable < entry_count else [],
    }


def parse_biomass_dump(path: Path, data: bytes) -> dict[str, Any]:
    return parse_headered_entries(
        data,
        BIOMASS_MAGIC,
        BIOMASS_ENTRY,
        "biomass",
        lambda fields: {
            "frame": fields[0],
            "stateHash": fields[1],
            "activeCellCount": fields[2],
            "flags": fields[3],
            "global": round(fields[4], 6),
            "prey": round(fields[5], 6),
            "predator": round(fields[6], 6),
            "floraOvergrowth01": round(fields[7], 6),
        },
    )


def parse_macro_swarm_dump(path: Path, data: bytes) -> dict[str, Any]:
    return parse_headered_entries(
        data,
        MACRO_SWARM_MAGIC,
        MACRO_SWARM_ENTRY,
        "macro_swarm",
        lambda fields: {
            "frame": fields[0],
            "stateHash": fields[1],
            "activeMacroSwarms": fields[2],
            "arrivalCount": fields[3],
            "biomass": round(fields[4], 6),
            "flags": fields[5],
            "reserved0": fields[6],
            "reserved1": fields[7],
        },
    )


def parse_fauna_mutation_dump(path: Path, data: bytes) -> dict[str, Any]:
    return parse_headered_entries(
        data,
        FAUNA_MUTATION_MAGIC,
        FAUNA_MUTATION_ENTRY,
        "fauna_mutation",
        lambda fields: {
            "frame": fields[0],
            "stateHash": fields[1],
            "totalMutatedEntities": fields[2],
            "headlessMutatedCount": fields[3],
            "macroSwarmMutatedCount": fields[4],
            "lastMutationFlags": fields[5],
            "lastRadiationRads": round(fields[6], 6),
            "lastToxicity01": round(fields[7], 6),
            "lastBrineDepth01": round(fields[8], 6),
            "reserved0": fields[9],
            "reserved1": fields[10],
        },
    )


def decode_packed_nibble_histogram(lo: int, hi: int) -> list[int]:
    counts: list[int] = []
    for index in range(16):
        packed = lo if index < 8 else hi
        counts.append((packed >> ((index & 7) * 4)) & 0xF)
    return counts


def parse_fauna_genetics_dump(path: Path, data: bytes) -> dict[str, Any]:
    parser_type = "fauna_genetics"
    if len(data) < BIOMASS_HEADER.size:
        return {"type": parser_type, "entries": [], "latest": None, "warnings": ["truncated_header"]}

    magic, entry_count, entry_size, oldest_index, capacity = BIOMASS_HEADER.unpack_from(data, 0)
    invalid_header = (
        magic != FAUNA_GENETICS_MAGIC
        or entry_count < 0
        or entry_size != FAUNA_GENETICS_ENTRY.size
        or oldest_index < 0
        or capacity <= 0
        or capacity > FAUNA_GENETICS_TELEMETRY_CAPACITY
        or oldest_index >= capacity
    )
    if invalid_header:
        return {
            "type": parser_type,
            "magic": magic,
            "magicHex": f"0x{magic:016X}",
            "entrySize": entry_size,
            "entryCount": entry_count,
            "oldestIndex": oldest_index,
            "capacity": capacity,
            "entries": [],
            "latest": None,
            "warnings": ["invalid_header"],
        }

    payload_offset = BIOMASS_HEADER.size
    expected_bytes = payload_offset + entry_count * entry_size
    readable = min(entry_count, max(0, len(data) - payload_offset) // entry_size)
    entries = []
    nonfinite_seen = False

    def finite_round(value: float, digits: int = 4) -> float | None:
        return round(value, digits) if math.isfinite(value) else None

    offset = payload_offset
    for index in range(readable):
        fields = FAUNA_GENETICS_ENTRY.unpack_from(data, offset)
        offset += entry_size
        flags = fields[14]
        flag_labels, unknown_flags = resolve_bit_labels(flags, FAUNA_GENETICS_FLAG_LABELS)
        float_values = fields[6:11]
        if any(not math.isfinite(value) for value in float_values):
            nonfinite_seen = True
        entries.append(
            {
                "slot": index,
                "ringSlot": (oldest_index + index) % capacity,
                "frame": fields[0],
                "stateHash": fields[1],
                "stateHashHex": f"0x{fields[1]:08X}",
                "compiledGenomeCount": fields[2],
                "activeGenomeCount": fields[3],
                "extractionOperationCount": fields[4],
                "invalidMaskCount": fields[5],
                "averageHueShift01": finite_round(fields[6]),
                "averageSize01": finite_round(fields[7]),
                "averageAggression01": finite_round(fields[8]),
                "averagePattern01": finite_round(fields[9]),
                "burstExecutionMicroseconds": finite_round(fields[10], 2),
                "tuningStateHash": fields[11],
                "tuningStateHashHex": f"0x{fields[11]:08X}",
                "patternHistogramLo": fields[12],
                "patternHistogramLoHex": f"0x{fields[12]:08X}",
                "patternHistogramHi": fields[13],
                "patternHistogramHiHex": f"0x{fields[13]:08X}",
                "flags": flags,
                "flagLabels": flag_labels,
                "unknownFlags": unknown_flags,
                "reserved0": fields[15],
                "patternHistogram": decode_packed_nibble_histogram(fields[12], fields[13]),
                "invalidMask": bool(flags & (1 << 0)),
            }
        )

    latest = max(entries, key=lambda entry: safe_int(entry.get("frame"), 0)) if entries else None
    capped = cap_entries(entries)
    warnings = []
    if len(data) < expected_bytes:
        warnings.append("payload_truncated")
    if len(data) > expected_bytes:
        warnings.append("trailing_bytes")
    if len(data) > payload_offset and (len(data) - payload_offset) % entry_size != 0:
        warnings.append("trailing_partial_entry")
    if entry_count > capacity:
        warnings.append("entry_count_exceeds_capacity")
    if capacity != FAUNA_GENETICS_TELEMETRY_CAPACITY:
        warnings.append("capacity_mismatch")
    if any(entry.get("unknownFlags") for entry in entries):
        warnings.append("unknown_flags")
    if nonfinite_seen:
        warnings.append("nonfinite_values")
    if any(entry.get("stateHash") == 0 for entry in entries):
        warnings.append("state_hash_zero")
    if any(entry.get("reserved0") != 0 for entry in entries):
        warnings.append("reserved_nonzero")
    if any(entry.get("invalidMask") for entry in entries):
        warnings.append("invalid_mask")
    if any(
        (entry.get("invalidMaskCount", 0) > 0) != bool(entry.get("invalidMask"))
        for entry in entries
    ):
        warnings.append("invalid_mask_flag_mismatch")
    if any(
        entry.get("compiledGenomeCount", 0) < 0
        or entry.get("activeGenomeCount", 0) < 0
        or entry.get("extractionOperationCount", 0) < 0
        or entry.get("invalidMaskCount", 0) < 0
        or entry.get("activeGenomeCount", 0) > entry.get("compiledGenomeCount", 0)
        for entry in entries
    ):
        warnings.append("genome_count_out_of_range")
    if any(
        entry.get("extractionOperationCount", 0) != entry.get("activeGenomeCount", 0) * 4
        for entry in entries
    ):
        warnings.append("extraction_count_mismatch")
    if any(
        entry.get("burstExecutionMicroseconds") is None
        or entry.get("burstExecutionMicroseconds", 0.0) < 0.0
        for entry in entries
    ):
        warnings.append("burst_time_out_of_range")
    if any(
        entry.get("burstExecutionMicroseconds", 0.0) > FAUNA_GENETICS_TELEMETRY_BUDGET_US
        for entry in entries
    ):
        warnings.append("burst_over_500us")
    if any(
        entry.get("averageHueShift01") is None
        or entry.get("averageHueShift01", 0.0) < 0.0
        or entry.get("averageHueShift01", 0.0) > 1.0
        or entry.get("averageSize01") is None
        or entry.get("averageSize01", 0.0) < 0.0
        or entry.get("averageSize01", 0.0) > 1.0
        or entry.get("averageAggression01") is None
        or entry.get("averageAggression01", 0.0) < 0.0
        or entry.get("averageAggression01", 0.0) > 1.0
        or entry.get("averagePattern01") is None
        or entry.get("averagePattern01", 0.0) < 0.0
        or entry.get("averagePattern01", 0.0) > 1.0
        for entry in entries
    ):
        warnings.append("average_out_of_range")

    return {
        "type": parser_type,
        "magic": magic,
        "magicHex": f"0x{magic:016X}",
        "entrySize": entry_size,
        "entryCount": entry_count,
        "oldestIndex": oldest_index,
        "capacity": capacity,
        "nonEmptyEntryCount": len(entries),
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": warnings,
    }


def parse_headless_blackbox(path: Path, data: bytes) -> dict[str, Any]:
    if len(data) < HEADLESS_HEADER.size:
        return {"type": "headless_blackbox", "entries": [], "warnings": ["truncated_header"]}
    magic, entry_count, entry_size, cursor = HEADLESS_HEADER.unpack_from(data, 0)
    if magic != HEADLESS_MAGIC or entry_size != HEADLESS_ENTRY.size:
        return {"type": "headless_blackbox", "entries": [], "warnings": ["invalid_header"]}
    readable = min(entry_count, (len(data) - HEADLESS_HEADER.size) // entry_size)
    entries = []
    offset = HEADLESS_HEADER.size
    for _ in range(readable):
        fields = HEADLESS_ENTRY.unpack_from(data, offset)
        offset += entry_size
        entries.append(
            {
                "frame": fields[0],
                "day": fields[1],
                "stateHash": fields[2],
                "grid": {"x": fields[3], "y": fields[4], "z": fields[5]},
                "local": {"x": fields[6], "y": fields[7], "z": fields[8]},
                "prey": round(fields[9], 6),
                "predator": round(fields[10], 6),
                "nativeBytesMb": round(fields[11], 4),
                "flags": fields[12],
            }
        )
    latest = entries[-1] if entries else None
    capped = cap_entries(entries)
    return {
        "type": "headless_blackbox",
        "entrySize": entry_size,
        "entryCount": entry_count,
        "cursor": cursor,
        "returnedEntryCount": len(capped),
        "entries": capped,
        "latest": latest,
        "warnings": ["payload_truncated"] if readable < entry_count else [],
    }


def parse_live_telemetry(path: Path, data: bytes) -> dict[str, Any]:
    if len(data) < LIVE_TELEMETRY_ENTRY_V1.size:
        return {"type": "live_telemetry", "entries": [], "warnings": ["truncated_payload"]}
    magic, version = struct.unpack_from("<II", data, 0)
    if magic != LIVE_TELEMETRY_MAGIC:
        return {"type": "live_telemetry", "entries": [], "warnings": ["invalid_header"]}

    warnings = []
    if version >= 2 and len(data) >= LIVE_TELEMETRY_ENTRY_V2.size:
        fields = LIVE_TELEMETRY_ENTRY_V2.unpack_from(data, 0)
        if fields[2] != LIVE_TELEMETRY_ENTRY_V2.size:
            warnings.append("record_size_mismatch")
        latest = {
            "frame": fields[3],
            "version": fields[1],
            "recordSizeBytes": fields[2],
            "activeChunkCount": fields[4],
            "gcAllocBytes": fields[5],
            "cpuFrameTimeMs": round(fields[6], 4),
            "deltaTimeMs": round(fields[7] * 1000.0, 4),
            "reservedMemoryMb": round(fields[8], 4),
            "latencyMs": round(fields[9], 4),
            "gpuFrameTimeMs": round(fields[10], 4),
            "systemMask": fields[11],
            "errorFlags": fields[12],
            "velocityPacked": fields[13],
            "aupShiftSequence": fields[14],
            "lastOriginShiftFrame": fields[15],
        }
        entry_size = LIVE_TELEMETRY_ENTRY_V2.size
    else:
        fields = LIVE_TELEMETRY_ENTRY_V1.unpack_from(data, 0)
        latest = {
            "frame": fields[2],
            "version": fields[1],
            "activeChunkCount": fields[3],
            "gcAllocBytes": fields[4],
            "cpuFrameTimeMs": round(fields[5], 4),
            "deltaTimeMs": round(fields[6] * 1000.0, 4),
            "reservedMemoryMb": round(fields[7], 4),
        }
        entry_size = LIVE_TELEMETRY_ENTRY_V1.size
        warnings.append("legacy_v1_32_byte_record")

    return {
        "type": "live_telemetry",
        "entrySize": entry_size,
        "returnedEntryCount": 1,
        "entries": [latest],
        "latest": latest,
        "warnings": warnings,
    }


def parse_h8memory_text(path: Path) -> dict[str, Any]:
    result = {"type": "h8memory_text", "records": [], "memoryMap": [], "warnings": []}
    try:
        lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    except OSError as exc:
        return {"type": "h8memory_text", "records": [], "memoryMap": [], "warnings": [f"read_failed:{exc.__class__.__name__}"]}
    total_bytes = 0
    records = []
    for line in lines:
        if line.startswith("TotalBytes="):
            total_bytes = parse_int(line.split("=", 1)[1]) or 0
            continue
        match = re.search(
            r"Index=(?P<index>-?\d+)\s+Ptr=(?P<ptr>-?\d+)\s+Bytes=(?P<bytes>-?\d+)\s+Owner=(?P<owner>-?\d+)\s+Allocator=(?P<allocator>-?\d+)\s+Flags=(?P<flags>-?\d+)",
            line,
        )
        if match:
            records.append({key: int(value) for key, value in match.groupdict().items()})
    used = sum(max(0, record["bytes"]) for record in records)
    blocks = [
        {
            "state": "occupied",
            "bytes": max(0, record["bytes"]),
            "label": f"owner {record['owner']}",
            "owner": record["owner"],
            "index": record["index"],
        }
        for record in records
    ]
    if total_bytes > used:
        blocks.append({"state": "free", "bytes": total_bytes - used, "label": "untracked-free", "estimated": True})
    result["totalBytes"] = total_bytes
    result["records"] = records
    result["memoryMap"] = blocks
    return result


def parse_dump_file(path: Path) -> dict[str, Any]:
    base: dict[str, Any] = {**file_stamp(path), "name": path.name}
    if path.suffix.lower() == ".txt":
        return {**base, **parse_h8memory_text(path)}
    if path.suffix.lower() == ".json":
        try:
            parsed_json = json.loads(path.read_text(encoding="utf-8", errors="replace"))
        except (OSError, json.JSONDecodeError) as exc:
            return {**base, "type": "json_manifest", "warnings": [f"read_failed:{exc.__class__.__name__}"]}
        return {**base, "type": "json_manifest", "manifest": parsed_json, "warnings": []}
    if path.suffix.lower() not in {".bin", ".h8dump"}:
        return {**base, "type": "unsupported", "warnings": ["unsupported_extension"]}
    warnings = list(base.get("warnings", []))
    byte_count = base.get("bytes")
    if byte_count is None:
        warnings.append("missing_or_unreadable")
        return {**base, "type": "unsupported", "warnings": warnings}
    if byte_count > MAX_DUMP_BYTES:
        return {**base, "type": "unsupported", "warnings": ["dump_over_size_cap"]}
    try:
        data = path.read_bytes()
    except OSError as exc:
        warnings.append(f"read_failed:{exc.__class__.__name__}")
        return {**base, "type": "unsupported", "warnings": warnings}

    if is_terrain_streaming_dump_path(path):
        return {**base, **parse_terrain_streaming_dump(data)}
    if is_global_telemetry_bus_blackbox_path(path):
        return {**base, **parse_global_telemetry_bus_blackbox(data)}
    if is_data_monolith_telemetry_blackbox_path(path):
        return {**base, **parse_data_monolith_telemetry_blackbox(data)}
    if is_vault_sovereignty_telemetry_blackbox_path(path):
        return {**base, **parse_vault_sovereignty_telemetry_blackbox(data)}
    if is_arm64_alignment_telemetry_blackbox_path(path):
        return {**base, **parse_arm64_alignment_telemetry_blackbox(data)}
    if is_haptic_synthesis_telemetry_blackbox_path(path):
        return {**base, **parse_haptic_synthesis_telemetry_blackbox(data)}
    if is_vocal_warning_telemetry_blackbox_path(path):
        return {**base, **parse_vocal_warning_telemetry_blackbox(data)}
    if is_granular_audio_telemetry_blackbox_path(path):
        return {**base, **parse_granular_audio_telemetry_blackbox(data)}
    if is_prologue_audio_transition_blackbox_path(path):
        return {**base, **parse_prologue_audio_transition_blackbox(data)}
    if is_audio_synthesis_telemetry_blackbox_path(path):
        return {**base, **parse_audio_synthesis_telemetry_blackbox(data)}
    if is_vocal_bank_synthesis_blackbox_path(path):
        return {**base, **parse_vocal_bank_synthesis_blackbox(data)}
    if is_adaptive_stem_mixer_blackbox_path(path):
        return {**base, **parse_adaptive_stem_mixer_blackbox(data)}
    if is_camera_juice_telemetry_blackbox_path(path):
        return {**base, **parse_camera_juice_telemetry_blackbox(data)}
    if is_material_decay_blackbox_path(path):
        return {**base, **parse_material_decay_blackbox(data)}
    if is_interactive_wake_blackbox_path(path):
        return {**base, **parse_interactive_wake_blackbox(data)}
    if is_flora_sway_field_blackbox_path(path):
        return {**base, **parse_flora_sway_field_blackbox(data)}
    if is_flora_memory_telemetry_blackbox_path(path):
        return {**base, **parse_flora_memory_telemetry_blackbox(data)}
    if is_flora_ambient_sway_blackbox_path(path):
        return {**base, **parse_flora_ambient_sway_blackbox(data)}
    if is_vegetation_memory_blackbox_path(path):
        return {**base, **parse_vegetation_memory_blackbox(data)}
    if is_dear_lie_organics_blackbox_path(path):
        return {**base, **parse_dear_lie_organics_blackbox(data)}
    if is_chemical_influence_blackbox_path(path):
        return {**base, **parse_chemical_influence_blackbox(data)}
    if is_sargassum_food_chain_blackbox_path(path):
        return {**base, **parse_sargassum_food_chain_blackbox(data)}
    if is_sargassum_boid_sensory_blackbox_path(path):
        return {**base, **parse_sargassum_boid_sensory_blackbox(data)}
    if is_marine_snow_vfx_blackbox_path(path):
        return {**base, **parse_marine_snow_vfx_blackbox(data)}
    if is_propwash_gpu_blackbox_path(path):
        return {**base, **parse_propwash_gpu_blackbox(data)}
    if is_carve_debris_blackbox_path(path):
        return {**base, **parse_carve_debris_blackbox(data)}
    if is_biolum_pulse_blackbox_path(path):
        return {**base, **parse_biolum_pulse_blackbox(data)}
    if is_biolum_director_blackbox_path(path):
        return {**base, **parse_biolum_director_blackbox(data)}
    if is_toxic_outgassing_blackbox_path(path):
        return {**base, **parse_toxic_outgassing_blackbox(data)}
    if is_gas_dynamics_blackbox_path(path):
        return {**base, **parse_gas_dynamics_blackbox(data)}
    if is_base_atmosphere_logistics_blackbox_path(path):
        return {**base, **parse_base_atmosphere_logistics_blackbox(data)}
    if is_storm_propagation_blackbox_path(path):
        return {**base, **parse_storm_propagation_blackbox(data)}
    if is_ocean_surface_atmosphere_blackbox_path(path):
        return {**base, **parse_ocean_surface_atmosphere_blackbox(data)}
    if is_thermodynamics_hazard_blackbox_path(path):
        return {**base, **parse_thermodynamics_hazard_blackbox(data)}
    if is_reactor_thermal_blackbox_path(path):
        return {**base, **parse_reactor_thermal_blackbox(data)}
    if is_nuclear_reactor_thermal_blackbox_path(path):
        return {**base, **parse_nuclear_reactor_thermal_blackbox(data)}
    if is_foveated_simulation_blackbox_path(path):
        return {**base, **parse_foveated_simulation_blackbox(data)}
    if is_input_determinism_blackbox_path(path):
        return {**base, **parse_input_determinism_blackbox(data)}
    if is_origin_shift_blackbox_path(path):
        return {**base, **parse_origin_shift_blackbox(data)}
    if is_binary_layout_sentinel_path(path):
        return {**base, **parse_binary_layout_sentinel(data)}
    if is_terminal_os_blackbox_path(path):
        return {**base, **parse_terminal_os_blackbox(data)}
    if is_terminal_decryption_blackbox_path(path):
        return {**base, **parse_terminal_decryption_blackbox(data)}
    if is_terminal_projection_blackbox_path(path):
        return {**base, **parse_terminal_projection_blackbox(data)}
    if is_openxr_manual_override_blackbox_path(path):
        return {**base, **parse_openxr_manual_override_blackbox(data)}
    if is_vehicle_damage_holographer_dump_path(path):
        return {**base, **parse_vehicle_damage_holographer_dump(data)}
    if is_pda_projection_blackbox_path(path):
        return {**base, **parse_pda_projection_blackbox(data)}
    if is_wrist_hud_blackbox_path(path):
        return {**base, **parse_wrist_hud_blackbox(data)}
    if is_ladder_climb_ik_blackbox_path(path):
        return {**base, **parse_ladder_climb_ik_blackbox(data)}
    if is_topographical_sonar_blackbox_path(path):
        return {**base, **parse_topographical_sonar_blackbox(data)}
    if is_kinetic_character_blackbox_path(path):
        return {**base, **parse_kinetic_character_blackbox(data)}
    if is_procedural_bone_blackbox_path(path):
        return {**base, **parse_procedural_bone_blackbox(data)}
    if is_vr_somatic_blackbox_path(path):
        return {**base, **parse_vr_somatic_blackbox(data)}
    if is_lockstep_state_validator_blackbox_path(path):
        return {**base, **parse_lockstep_state_validator_blackbox(data)}
    if is_voxel_astar_blackbox_path(path):
        return {**base, **parse_voxel_astar_blackbox(data)}
    if is_path_funnel_blackbox_path(path):
        return {**base, **parse_path_funnel_blackbox(data)}
    if is_laser_cutter_dod_blackbox_path(path):
        return {**base, **parse_laser_cut_225_blackbox(data)}
    if is_tool_kinematics_blackbox_path(path):
        return {**base, **parse_tool_kinematics_blackbox(data)}
    if is_auxiliary_equipment_blackbox_path(path):
        return {**base, **parse_auxiliary_equipment_blackbox(data)}
    if is_upgrade_matrix_blackbox_path(path):
        return {**base, **parse_upgrade_matrix_blackbox(data)}
    if is_metabolism_blackbox_path(path):
        return {**base, **parse_metabolism_blackbox(data)}
    if is_physiology_autopsy_blackbox_path(path):
        return {**base, **parse_physiology_autopsy_blackbox(data)}
    if is_sensory_impairment_blackbox_path(path):
        return {**base, **parse_sensory_impairment_blackbox(data)}
    if is_suit_integrity_blackbox_path(path):
        return {**base, **parse_suit_integrity_blackbox(data)}
    if is_radiation_mutation_blackbox_path(path):
        return {**base, **parse_radiation_mutation_blackbox(data)}
    if is_respawn_reconciliation_blackbox_path(path):
        return {**base, **parse_respawn_reconciliation_blackbox(data)}
    if is_pda_frequency_tuning_blackbox_path(path):
        return {**base, **parse_pda_frequency_tuning_blackbox(data)}
    if is_compass_gyro_blackbox_path(path):
        return {**base, **parse_compass_gyro_blackbox(data)}
    if is_pda_encyclopedia_blackbox_path(path):
        return {**base, **parse_pda_encyclopedia_blackbox(data)}
    if is_habitat_flood_blackbox_path(path):
        return {**base, **parse_habitat_flood_blackbox(path, data)}
    if is_construction_validation_blackbox_path(path):
        return {**base, **parse_construction_validation_blackbox(data)}
    if is_construction_socket_blackbox_path(path):
        return {**base, **parse_construction_socket_blackbox(data)}
    if is_construction_holography_blackbox_path(path):
        return {**base, **parse_construction_holography_blackbox(data)}

    magic_identified = try_parse_magic_identified_blackbox(data)
    if magic_identified is not None:
        return {**base, **magic_identified}
    if is_abyssal_thermodynamics_raw_blackbox_path(path):
        return {**base, **parse_abyssal_thermodynamics_raw_blackbox(data)}

    name = path.name.upper()
    if len(data) >= GENERIC_BLACKBOX_HEADER.size:
        magic64 = struct.unpack_from("<Q", data, 0)[0]
        if magic64 == VAULT_SOVEREIGNTY_TELEMETRY_MAGIC:
            return {**base, **parse_vault_sovereignty_telemetry_blackbox(data)}
        if magic64 == ARM64_ALIGNMENT_TELEMETRY_MAGIC:
            return {**base, **parse_arm64_alignment_telemetry_blackbox(data)}
        if magic64 == HECTON8_MAGIC:
            return {**base, **parse_generic_blackbox(path, data)}
        if magic64 == BIOMASS_MAGIC:
            return {**base, **parse_biomass_dump(path, data)}
        if magic64 == MACRO_SWARM_MAGIC:
            return {**base, **parse_macro_swarm_dump(path, data)}
        if magic64 == FAUNA_MUTATION_MAGIC:
            return {**base, **parse_fauna_mutation_dump(path, data)}
        if magic64 == FAUNA_GENETICS_MAGIC:
            return {**base, **parse_fauna_genetics_dump(path, data)}
    if len(data) >= HEADLESS_HEADER.size and struct.unpack_from("<I", data, 0)[0] == HEADLESS_MAGIC:
        return {**base, **parse_headless_blackbox(path, data)}
    if len(data) >= LIVE_TELEMETRY_ENTRY_V1.size and struct.unpack_from("<I", data, 0)[0] == LIVE_TELEMETRY_MAGIC:
        return {**base, **parse_live_telemetry(path, data)}
    if "THERMAL" in name:
        return {**base, **parse_thermal_dump(path, data)}
    if "MEMORY_DEFRAGMENTATION" in name or "VAULT_MEMORY" in name or "PHI_VOD" in name:
        return {**base, **parse_defrag_dump(path, data)}
    return {**base, "type": "unknown_binary", "warnings": ["unrecognized_binary_layout"]}


def parse_failed_dump_file(path: Path, exc: Exception) -> dict[str, Any]:
    return {
        **file_stamp(path),
        "name": path.name,
        "type": "parse_failed",
        "entries": [],
        "latest": None,
        "warnings": [f"parse_failed:{exc.__class__.__name__}"],
        "errorType": exc.__class__.__name__,
    }


def empty_job_admission_data() -> dict[str, Any]:
    return {
        "sources": [],
        "latest": None,
        "admittedCount": 0,
        "deniedCount": 0,
        "aupBarrierCount": 0,
        "killSwitchCount": 0,
        "insufficientBudgetCount": 0,
        "nonFiniteCount": 0,
        "stateHashMismatchCount": 0,
        "legacyStarvedCount": 0,
        "legacyNonFiniteCount": 0,
    }


def safe_int(value: Any, fallback: int = -1) -> int:
    try:
        return int(value)
    except (TypeError, ValueError):
        return fallback


def should_replace_job_admission_latest(current: dict[str, Any] | None, candidate: dict[str, Any]) -> bool:
    if not current:
        return True

    current_frame = safe_int(current.get("frame"))
    candidate_frame = safe_int(candidate.get("frame"))
    return candidate_frame >= current_frame


def add_job_admission_dump(summary: dict[str, Any], dump: dict[str, Any]) -> None:
    if dump.get("type") != "job_admission_blackbox":
        return

    summary["sources"].append(
        {
            "name": dump.get("name"),
            "version": dump.get("version"),
            "entrySize": dump.get("entrySize"),
            "declaredEntryCount": dump.get("declaredEntryCount"),
            "returnedEntryCount": dump.get("returnedEntryCount"),
        }
    )
    latest = dump.get("latest")
    if latest and should_replace_job_admission_latest(summary.get("latest"), latest):
        summary["latest"] = {**latest, "source": dump.get("name")}

    for entry in dump.get("entries", []):
        if entry.get("admitted"):
            summary["admittedCount"] += 1
        if entry.get("denied"):
            summary["deniedCount"] += 1
        if entry.get("aupBarrier"):
            summary["aupBarrierCount"] += 1
        if entry.get("killSwitch"):
            summary["killSwitchCount"] += 1
        if entry.get("insufficientBudget"):
            summary["insufficientBudgetCount"] += 1
        if entry.get("nonFinite"):
            summary["nonFiniteCount"] += 1
        if entry.get("stateHashOk") is False:
            summary["stateHashMismatchCount"] += 1
        if entry.get("legacyStarved"):
            summary["legacyStarvedCount"] += 1
        if entry.get("legacyNonFinite"):
            summary["legacyNonFiniteCount"] += 1


def collect_dumps() -> dict[str, Any]:
    candidate_paths = {path for path in AGENT_LOGS.glob("Dump_*")}
    candidate_paths.update(AGENT_LOGS.glob("*.h8dump"))
    for file_name in ("BLACKBOX_CRASH.bin", "BLACKBOX_CRASH.h8dump", "runtime_telemetry.bin"):
        candidate = AGENT_LOGS / file_name
        if candidate.exists():
            candidate_paths.add(candidate)
    dumps = []
    for path in sorted(candidate_paths):
        if not path.is_file():
            continue
        try:
            dumps.append(parse_dump_file(path))
        except Exception as exc:
            dumps.append(parse_failed_dump_file(path, exc))

    memory_maps = []
    thermal_latest = None
    ecology_series = []
    frame_series = []
    job_admission = empty_job_admission_data()
    for dump in dumps:
        if dump.get("memoryMap"):
            blocks = dump["memoryMap"]
            memory_maps.append(
                {
                    "name": dump["name"],
                    "blocks": blocks,
                    "latest": dump.get("latest"),
                    "estimated": all(bool(block.get("estimated")) for block in blocks),
                    "sourceType": dump.get("type"),
                }
            )
        if dump.get("type") == "thermal" and dump.get("latest"):
            thermal_latest = {**dump["latest"], "source": dump["name"]}
        add_job_admission_dump(job_admission, dump)
        if dump.get("type") in {"biomass", "headless_blackbox"}:
            for entry in dump.get("entries", []):
                ecology_series.append(
                    {
                        "x": entry.get("frame") or entry.get("day") or 0,
                        "frame": entry.get("frame"),
                        "prey": entry.get("prey", 0.0),
                        "predator": entry.get("predator", 0.0),
                        "source": dump["name"],
                    }
                )
        if dump.get("type") in {"generic_blackbox", "crash_telemetry_buffer"}:
            previous_frame_ms = None
            for entry in dump.get("entries", []):
                frame_time_ms = entry.get("deltaTimeMs")
                if frame_time_ms is None:
                    continue
                jitter_ms = 0.0 if previous_frame_ms is None else abs(frame_time_ms - previous_frame_ms)
                previous_frame_ms = frame_time_ms
                frame_series.append(
                    {
                        "x": entry.get("frame", 0),
                        "frame": entry.get("frame", 0),
                        "frameTimeMs": round(frame_time_ms, 4),
                        "jitterMs": round(jitter_ms, 4),
                        "spike": frame_time_ms > FRAME_SPIKE_MS,
                        "source": dump["name"],
                    }
                )
        if dump.get("type") == "live_telemetry" and dump.get("latest"):
            entry = dump["latest"]
            frame_time_ms = entry.get("cpuFrameTimeMs") or entry.get("deltaTimeMs")
            if frame_time_ms is not None:
                frame_series.append(
                    {
                        "x": entry.get("frame", 0),
                        "frame": entry.get("frame", 0),
                        "frameTimeMs": round(frame_time_ms, 4),
                        "jitterMs": 0.0,
                        "spike": frame_time_ms > FRAME_SPIKE_MS,
                        "source": dump["name"],
                    }
                )
    memory_maps.sort(key=lambda item: (item["estimated"], item["name"]))
    return {
        "files": dumps,
        "memoryMaps": memory_maps,
        "latestThermal": thermal_latest,
        "jobAdmission": job_admission,
        "ecologySeries": ecology_series[-MAX_CSV_ROWS:],
        "frameSeries": frame_series[-MAX_CSV_ROWS:],
    }


def collect_csv() -> dict[str, Any]:
    sources = [
        (AGENT_LOGS / "QA_Endurance_Log.csv", "QA_Endurance_Log.csv"),
        (AGENT_LOGS / "HeadlessSimulationDaily_HEADLESS_SIMULATION_RUNNER.csv", "HeadlessSimulationDaily_HEADLESS_SIMULATION_RUNNER.csv"),
    ]
    parsed = [parse_csv_file(path, label) for path, label in sources]
    frame_series = []
    ecology_series = []
    latest_thermal = None
    latest_hphi = None
    for source in parsed:
        frame_series.extend(source["frameSeries"])
        ecology_series.extend(source["ecologySeries"])
        if source["latestThermal"] is not None:
            latest_thermal = source["latestThermal"]
        if source["latestHphi"] is not None:
            latest_hphi = source["latestHphi"]
    return {
        "sources": parsed,
        "frameSeries": frame_series[-MAX_CSV_ROWS:],
        "ecologySeries": ecology_series[-MAX_CSV_ROWS:],
        "latestThermal": latest_thermal,
        "latestHphi": latest_hphi,
    }


def build_summary() -> dict[str, Any]:
    csv_data = collect_csv()
    dump_data = collect_dumps()
    hphi = parse_hphi_report()
    if csv_data["latestHphi"] is not None:
        hphi["value"] = csv_data["latestHphi"]
        hphi["status"] = "csv-latest"
        hphi["evidenceClass"] = "FILE_IO"

    latest_thermal = csv_data["latestThermal"] or dump_data["latestThermal"]
    ecology_series = csv_data["ecologySeries"] or dump_data["ecologySeries"]
    frame_series = csv_data["frameSeries"] or dump_data["frameSeries"]

    return {
        "status": "DASHBOARD OPERATIONAL",
        "generatedUtc": utc_now_iso(),
        "projectRoot": str(PROJECT_ROOT),
        "agentLogs": str(AGENT_LOGS),
        "frameSpikeMs": FRAME_SPIKE_MS,
        "csv": csv_data,
        "dumps": dump_data,
        "hphi": hphi,
        "thermal": latest_thermal,
        "jobAdmission": dump_data["jobAdmission"],
        "frameSeries": frame_series[-MAX_CSV_ROWS:],
        "ecologySeries": ecology_series[-MAX_CSV_ROWS:],
        "evidence": {
            "runtimeUnityVerified": False,
            "class": "FILE_IO + STATIC_SOURCE",
            "note": "Dashboard parses files on disk. It does not prove Unity runtime health.",
        },
    }


def empty_csv_data() -> dict[str, Any]:
    return {
        "sources": [],
        "frameSeries": [],
        "ecologySeries": [],
        "latestThermal": None,
        "latestHphi": None,
    }


def empty_dump_data() -> dict[str, Any]:
    return {
        "files": [],
        "memoryMaps": [],
        "latestThermal": None,
        "jobAdmission": empty_job_admission_data(),
        "ecologySeries": [],
        "frameSeries": [],
    }


def build_degraded_summary(exc: Exception) -> dict[str, Any]:
    return {
        "status": "DASHBOARD DEGRADED",
        "generatedUtc": utc_now_iso(),
        "projectRoot": str(PROJECT_ROOT),
        "agentLogs": str(AGENT_LOGS),
        "frameSpikeMs": FRAME_SPIKE_MS,
        "csv": empty_csv_data(),
        "dumps": empty_dump_data(),
        "hphi": {
            "value": None,
            "status": "unavailable",
            "source": "api_exception",
            "evidenceClass": "ERROR",
        },
        "thermal": None,
        "jobAdmission": empty_job_admission_data(),
        "frameSeries": [],
        "ecologySeries": [],
        "errors": [{"type": exc.__class__.__name__, "message": str(exc)[:240]}],
        "evidence": {
            "runtimeUnityVerified": False,
            "class": "ERROR",
            "note": "Dashboard summary generation failed; returned degraded empty telemetry instead of fabricated values.",
        },
    }


@app.get("/")
def index() -> FileResponse:
    return FileResponse(INDEX_HTML, headers=NO_STORE_HEADERS)


@app.get("/api/summary")
def api_summary() -> JSONResponse:
    try:
        payload = build_summary()
    except Exception as exc:
        payload = build_degraded_summary(exc)
    return dashboard_json(payload)


@app.get("/api/health")
def api_health() -> JSONResponse:
    return dashboard_json(
        {
            "status": "ok",
            "generatedUtc": utc_now_iso(),
            "projectRoot": str(PROJECT_ROOT),
            "agentLogsExists": AGENT_LOGS.exists(),
        }
    )

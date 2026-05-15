#!/usr/bin/env python3
"""Offline submarine hydrodynamics baker for HECTON-8.

This tool produces deterministic coefficient data for runtime consumers.
It does not touch Unity assets, scenes, prefabs, or project settings.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import struct
import zlib
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Optional, Sequence, Tuple


SEA_WATER_DENSITY_KG_M3 = 1027.0
GRAVITY_M_S2 = 9.80665
ATM_PRESSURE_PA = 101325.0
WATER_VAPOR_PRESSURE_PA = 1700.0
POWER_SPEED_STEP_MPS = 0.5
POWER_SPEED_MAX_MPS = 35.0
ACCELERATION_DT_SECONDS = 0.02
ACCELERATION_DURATION_SECONDS = 90.0
STOP_TARGET_SPEED_RATIO = 0.05
MIN_STOP_DISTANCE_HULL_LENGTHS = 3.0
MAX_ACCEPTED_TERMINAL_SPEED_MPS = 49.0
INSTANT_50MPS_SECONDS = 10.0
LIFT_SAMPLE_ANGLES_DEGREES: Tuple[float, ...] = (-15.0, -10.0, -5.0, 0.0, 5.0, 10.0, 15.0)
CAVITATION_SAMPLE_DEPTHS_M: Tuple[float, ...] = (0.0, 25.0, 50.0, 100.0, 300.0)
RUNTIME_PACK_MAGIC = b"H8HYDRO\0"
RUNTIME_PACK_VERSION = 1
RUNTIME_PACK_FLOAT_FIELDS: Tuple[str, ...] = (
    "length_m",
    "beam_m",
    "height_m",
    "operational_mass_kg",
    "displaced_water_mass_kg",
    "max_thrust_n",
    "rated_cruise_speed_mps",
    "terminal_speed_at_max_thrust_mps",
    "drag_cd_area_x",
    "drag_cd_area_y",
    "drag_cd_area_z",
    "square_drag_per_meter_x",
    "square_drag_per_meter_y",
    "square_drag_per_meter_z",
    "added_mass_kg_x",
    "added_mass_kg_y",
    "added_mass_kg_z",
    "effective_mass_kg_x",
    "effective_mass_kg_y",
    "effective_mass_kg_z",
    "angular_damping_roll",
    "angular_damping_pitch",
    "angular_damping_yaw",
    "added_angular_inertia_roll",
    "added_angular_inertia_pitch",
    "added_angular_inertia_yaw",
    "rigid_angular_inertia_roll",
    "rigid_angular_inertia_pitch",
    "rigid_angular_inertia_yaw",
    "lift_pitch_cl_neg15",
    "lift_yaw_cl_neg15",
    "lift_pitch_cl_neg10",
    "lift_yaw_cl_neg10",
    "lift_pitch_cl_neg5",
    "lift_yaw_cl_neg5",
    "lift_pitch_cl_0",
    "lift_yaw_cl_0",
    "lift_pitch_cl_pos5",
    "lift_yaw_cl_pos5",
    "lift_pitch_cl_pos10",
    "lift_yaw_cl_pos10",
    "lift_pitch_cl_pos15",
    "lift_yaw_cl_pos15",
    "cavitation_onset_mps_depth_0",
    "cavitation_onset_mps_depth_25",
    "cavitation_onset_mps_depth_50",
    "cavitation_onset_mps_depth_100",
    "cavitation_onset_mps_depth_300",
    "cavitation_critical_prop_mps_depth_0",
    "cavitation_critical_prop_mps_depth_25",
    "cavitation_critical_prop_mps_depth_50",
    "cavitation_critical_prop_mps_depth_100",
    "cavitation_critical_prop_mps_depth_300",
)
RUNTIME_PACK_FLOAT_COUNT = len(RUNTIME_PACK_FLOAT_FIELDS)
RUNTIME_PACK_HEADER_FORMAT = "<8sIIII"
RUNTIME_PACK_RECORD_FORMAT = "<II" + ("f" * RUNTIME_PACK_FLOAT_COUNT)


@dataclass(frozen=True)
class HullShape:
    shape_id: str
    display_name: str
    role: str
    length_m: float
    beam_m: float
    height_m: float
    block_coefficient: float
    frontal_fill: float
    side_fill: float
    top_fill: float
    operational_mass_kg: float
    max_thrust_n: float
    propulsive_efficiency: float
    quiet_prop_rpm: float
    prop_radius_m: float
    wake_fraction: float
    sigma_critical: float
    cd_axial_forward: float
    cd_axial_reverse: float
    cd_lateral: float
    cd_vertical: float
    cl_pitch_per_rad: float
    cl_yaw_per_rad: float
    cl_max: float
    added_mass_coeff_x: float
    added_mass_coeff_y: float
    added_mass_coeff_z: float
    angular_cd_roll: float
    angular_cd_pitch: float
    angular_cd_yaw: float
    angular_added_roll: float
    angular_added_pitch: float
    angular_added_yaw: float


HULL_SHAPES: Tuple[HullShape, ...] = (
    HullShape(
        shape_id="SLEEK",
        display_name="Sleek Scout",
        role="fast narrow exploration hull",
        length_m=16.0,
        beam_m=3.2,
        height_m=2.8,
        block_coefficient=0.55,
        frontal_fill=0.68,
        side_fill=0.78,
        top_fill=0.74,
        operational_mass_kg=70000.0,
        max_thrust_n=220000.0,
        propulsive_efficiency=0.72,
        quiet_prop_rpm=145.0,
        prop_radius_m=0.62,
        wake_fraction=0.18,
        sigma_critical=1.8,
        cd_axial_forward=0.19,
        cd_axial_reverse=0.24,
        cd_lateral=0.92,
        cd_vertical=0.74,
        cl_pitch_per_rad=0.18,
        cl_yaw_per_rad=0.16,
        cl_max=0.72,
        added_mass_coeff_x=0.08,
        added_mass_coeff_y=0.86,
        added_mass_coeff_z=0.78,
        angular_cd_roll=0.18,
        angular_cd_pitch=0.62,
        angular_cd_yaw=0.58,
        angular_added_roll=0.16,
        angular_added_pitch=0.46,
        angular_added_yaw=0.50,
    ),
    HullShape(
        shape_id="INDUSTRIAL",
        display_name="Industrial Hauler",
        role="workhorse vehicle with external equipment mass",
        length_m=22.0,
        beam_m=5.0,
        height_m=4.5,
        block_coefficient=0.62,
        frontal_fill=0.78,
        side_fill=0.86,
        top_fill=0.82,
        operational_mass_kg=260000.0,
        max_thrust_n=420000.0,
        propulsive_efficiency=0.68,
        quiet_prop_rpm=130.0,
        prop_radius_m=0.80,
        wake_fraction=0.25,
        sigma_critical=1.9,
        cd_axial_forward=0.32,
        cd_axial_reverse=0.38,
        cd_lateral=1.14,
        cd_vertical=0.95,
        cl_pitch_per_rad=0.10,
        cl_yaw_per_rad=0.09,
        cl_max=0.44,
        added_mass_coeff_x=0.12,
        added_mass_coeff_y=0.96,
        added_mass_coeff_z=0.88,
        angular_cd_roll=0.25,
        angular_cd_pitch=0.82,
        angular_cd_yaw=0.76,
        angular_added_roll=0.20,
        angular_added_pitch=0.58,
        angular_added_yaw=0.62,
    ),
    HullShape(
        shape_id="BOXY",
        display_name="Boxy Salvage Tug",
        role="short slab-sided salvage hull",
        length_m=14.0,
        beam_m=5.6,
        height_m=4.8,
        block_coefficient=0.75,
        frontal_fill=0.94,
        side_fill=0.94,
        top_fill=0.90,
        operational_mass_kg=240000.0,
        max_thrust_n=600000.0,
        propulsive_efficiency=0.63,
        quiet_prop_rpm=150.0,
        prop_radius_m=0.70,
        wake_fraction=0.32,
        sigma_critical=2.0,
        cd_axial_forward=0.58,
        cd_axial_reverse=0.66,
        cd_lateral=1.42,
        cd_vertical=1.25,
        cl_pitch_per_rad=0.05,
        cl_yaw_per_rad=0.04,
        cl_max=0.24,
        added_mass_coeff_x=0.18,
        added_mass_coeff_y=1.18,
        added_mass_coeff_z=1.08,
        angular_cd_roll=0.34,
        angular_cd_pitch=1.08,
        angular_cd_yaw=1.02,
        angular_added_roll=0.26,
        angular_added_pitch=0.70,
        angular_added_yaw=0.74,
    ),
    HullShape(
        shape_id="ALIEN",
        display_name="Alien Manta",
        role="wide biological silhouette with clean pressure recovery",
        length_m=20.0,
        beam_m=6.2,
        height_m=2.6,
        block_coefficient=0.45,
        frontal_fill=0.62,
        side_fill=0.72,
        top_fill=0.82,
        operational_mass_kg=120000.0,
        max_thrust_n=280000.0,
        propulsive_efficiency=0.74,
        quiet_prop_rpm=120.0,
        prop_radius_m=0.75,
        wake_fraction=0.15,
        sigma_critical=1.7,
        cd_axial_forward=0.16,
        cd_axial_reverse=0.20,
        cd_lateral=0.78,
        cd_vertical=0.62,
        cl_pitch_per_rad=0.24,
        cl_yaw_per_rad=0.21,
        cl_max=0.86,
        added_mass_coeff_x=0.06,
        added_mass_coeff_y=0.92,
        added_mass_coeff_z=0.70,
        angular_cd_roll=0.16,
        angular_cd_pitch=0.54,
        angular_cd_yaw=0.50,
        angular_added_roll=0.14,
        angular_added_pitch=0.44,
        angular_added_yaw=0.52,
    ),
    HullShape(
        shape_id="ARMORED_CRAWLER",
        display_name="Armored Crawler",
        role="heavy pressure-rated mobile habitat hull",
        length_m=28.0,
        beam_m=6.4,
        height_m=5.2,
        block_coefficient=0.68,
        frontal_fill=0.84,
        side_fill=0.88,
        top_fill=0.86,
        operational_mass_kg=560000.0,
        max_thrust_n=620000.0,
        propulsive_efficiency=0.61,
        quiet_prop_rpm=115.0,
        prop_radius_m=0.95,
        wake_fraction=0.30,
        sigma_critical=2.0,
        cd_axial_forward=0.44,
        cd_axial_reverse=0.52,
        cd_lateral=1.30,
        cd_vertical=1.08,
        cl_pitch_per_rad=0.06,
        cl_yaw_per_rad=0.05,
        cl_max=0.30,
        added_mass_coeff_x=0.14,
        added_mass_coeff_y=1.06,
        added_mass_coeff_z=0.98,
        angular_cd_roll=0.30,
        angular_cd_pitch=0.98,
        angular_cd_yaw=0.92,
        angular_added_roll=0.24,
        angular_added_pitch=0.66,
        angular_added_yaw=0.68,
    ),
)


def round_float(value: float) -> float:
    return round(value, 6)


def fnv1a_32(text: str) -> int:
    value = 2166136261
    for byte in text.encode("utf-8"):
        value ^= byte
        value = (value * 16777619) & 0xFFFFFFFF
    return value


def diag_matrix(x_value: float, y_value: float, z_value: float) -> List[List[float]]:
    return [
        [round_float(x_value), 0.0, 0.0],
        [0.0, round_float(y_value), 0.0],
        [0.0, 0.0, round_float(z_value)],
    ]


def projected_areas(shape: HullShape) -> Dict[str, float]:
    return {
        "frontal_x_m2": shape.beam_m * shape.height_m * shape.frontal_fill,
        "side_y_m2": shape.length_m * shape.height_m * shape.side_fill,
        "top_z_m2": shape.length_m * shape.beam_m * shape.top_fill,
    }


def displaced_volume_m3(shape: HullShape) -> float:
    return shape.length_m * shape.beam_m * shape.height_m * shape.block_coefficient


def lift_curve_samples(shape: HullShape) -> List[Dict[str, float]]:
    samples: List[Dict[str, float]] = []
    for angle_degrees in LIFT_SAMPLE_ANGLES_DEGREES:
        angle_radians = math.radians(angle_degrees)
        pitch_cl = max(-shape.cl_max, min(shape.cl_max, shape.cl_pitch_per_rad * angle_radians))
        yaw_cl = max(-shape.cl_max, min(shape.cl_max, shape.cl_yaw_per_rad * angle_radians))
        samples.append(
            {
                "angle_degrees": round_float(angle_degrees),
                "pitch_Cl": round_float(pitch_cl),
                "yaw_Cl": round_float(yaw_cl),
            }
        )
    return samples


def build_spec(shape: HullShape) -> Dict[str, object]:
    areas = projected_areas(shape)
    volume = displaced_volume_m3(shape)
    displaced_mass = SEA_WATER_DENSITY_KG_M3 * volume

    cd_area_x = shape.cd_axial_forward * areas["frontal_x_m2"]
    cd_area_reverse_x = shape.cd_axial_reverse * areas["frontal_x_m2"]
    cd_area_y = shape.cd_lateral * areas["side_y_m2"]
    cd_area_z = shape.cd_vertical * areas["top_z_m2"]

    added_x = displaced_mass * shape.added_mass_coeff_x
    added_y = displaced_mass * shape.added_mass_coeff_y
    added_z = displaced_mass * shape.added_mass_coeff_z
    effective_x = shape.operational_mass_kg + added_x
    effective_y = shape.operational_mass_kg + added_y
    effective_z = shape.operational_mass_kg + added_z

    square_drag_x = 0.5 * SEA_WATER_DENSITY_KG_M3 * cd_area_x / effective_x
    square_drag_y = 0.5 * SEA_WATER_DENSITY_KG_M3 * cd_area_y / effective_y
    square_drag_z = 0.5 * SEA_WATER_DENSITY_KG_M3 * cd_area_z / effective_z

    roll_lever_m = max(shape.beam_m, shape.height_m) * 0.5
    pitch_lever_m = shape.length_m * 0.5
    yaw_lever_m = shape.length_m * 0.5
    roll_damping = 0.5 * SEA_WATER_DENSITY_KG_M3 * shape.angular_cd_roll * areas["frontal_x_m2"] * roll_lever_m ** 3
    pitch_damping = 0.5 * SEA_WATER_DENSITY_KG_M3 * shape.angular_cd_pitch * areas["side_y_m2"] * pitch_lever_m ** 3
    yaw_damping = 0.5 * SEA_WATER_DENSITY_KG_M3 * shape.angular_cd_yaw * areas["top_z_m2"] * yaw_lever_m ** 3

    rigid_roll = shape.operational_mass_kg * (shape.beam_m ** 2 + shape.height_m ** 2) / 12.0
    rigid_pitch = shape.operational_mass_kg * (shape.length_m ** 2 + shape.height_m ** 2) / 12.0
    rigid_yaw = shape.operational_mass_kg * (shape.length_m ** 2 + shape.beam_m ** 2) / 12.0
    added_roll_i = displaced_mass * (shape.beam_m ** 2 + shape.height_m ** 2) * shape.angular_added_roll / 12.0
    added_pitch_i = displaced_mass * (shape.length_m ** 2 + shape.height_m ** 2) * shape.angular_added_pitch / 12.0
    added_yaw_i = displaced_mass * (shape.length_m ** 2 + shape.beam_m ** 2) * shape.angular_added_yaw / 12.0

    terminal_speed = math.sqrt(shape.max_thrust_n / max(0.5 * SEA_WATER_DENSITY_KG_M3 * cd_area_x, 0.0001))
    rated_cruise_speed = terminal_speed * 0.72
    stop_distance = stop_distance_to_ratio(terminal_speed, square_drag_x, STOP_TARGET_SPEED_RATIO)
    accel = simulate_acceleration(shape.max_thrust_n, cd_area_x, effective_x)

    return {
        "shape_id": shape.shape_id,
        "display_name": shape.display_name,
        "role": shape.role,
        "dimensions_m": {
            "length": round_float(shape.length_m),
            "beam": round_float(shape.beam_m),
            "height": round_float(shape.height_m),
        },
        "operational_mass_kg": round_float(shape.operational_mass_kg),
        "displaced_volume_m3": round_float(volume),
        "displaced_water_mass_kg": round_float(displaced_mass),
        "max_thrust_n": round_float(shape.max_thrust_n),
        "rated_cruise_speed_mps": round_float(rated_cruise_speed),
        "terminal_speed_at_max_thrust_mps": round_float(terminal_speed),
        "reference_projected_areas": {key: round_float(value) for key, value in areas.items()},
        "hydrodynamic_coefficients": {
            "Cd_axial_forward": round_float(shape.cd_axial_forward),
            "Cd_axial_reverse": round_float(shape.cd_axial_reverse),
            "Cd_lateral_sway": round_float(shape.cd_lateral),
            "Cd_vertical_heave": round_float(shape.cd_vertical),
            "Cl_pitch_per_rad": round_float(shape.cl_pitch_per_rad),
            "Cl_yaw_per_rad": round_float(shape.cl_yaw_per_rad),
            "Cl_control_surface_max": round_float(shape.cl_max),
        },
        "lift_curve_samples": lift_curve_samples(shape),
        "drag_tensor_cd_area_m2_local_xyz": diag_matrix(cd_area_x, cd_area_y, cd_area_z),
        "reverse_axial_cd_area_m2": round_float(cd_area_reverse_x),
        "square_drag_accel_tensor_per_meter_local_xyz": diag_matrix(square_drag_x, square_drag_y, square_drag_z),
        "added_mass_coefficients_local_xyz": {
            "x_surge": round_float(shape.added_mass_coeff_x),
            "y_sway": round_float(shape.added_mass_coeff_y),
            "z_heave": round_float(shape.added_mass_coeff_z),
        },
        "added_mass_tensor_kg_local_xyz": diag_matrix(added_x, added_y, added_z),
        "effective_mass_tensor_kg_local_xyz": diag_matrix(effective_x, effective_y, effective_z),
        "rigid_angular_inertia_tensor_kg_m2_local_xyz": diag_matrix(rigid_roll, rigid_pitch, rigid_yaw),
        "added_angular_inertia_tensor_kg_m2_local_xyz": diag_matrix(added_roll_i, added_pitch_i, added_yaw_i),
        "angular_quadratic_damping_torque_tensor_n_m_per_rad_s_sq_local_xyz": diag_matrix(
            roll_damping,
            pitch_damping,
            yaw_damping,
        ),
        "cavitation": cavitation_thresholds(shape, depths_m=CAVITATION_SAMPLE_DEPTHS_M),
        "verification": {
            "acceleration_gate": accel,
            "stop_distance_to_5_percent_terminal_speed_m": round_float(stop_distance),
            "stop_distance_hull_lengths": round_float(stop_distance / shape.length_m),
            "min_required_stop_distance_m": round_float(MIN_STOP_DISTANCE_HULL_LENGTHS * shape.length_m),
        },
    }


def simulate_acceleration(thrust_n: float, cd_area_x: float, effective_mass_x_kg: float) -> Dict[str, object]:
    velocity = 0.0
    max_velocity = 0.0
    time_to_50: Optional[float] = None
    drag_coeff = 0.5 * SEA_WATER_DENSITY_KG_M3 * cd_area_x
    steps = int(ACCELERATION_DURATION_SECONDS / ACCELERATION_DT_SECONDS)
    for step in range(steps):
        drag_force = drag_coeff * velocity * abs(velocity)
        acceleration = (thrust_n - drag_force) / effective_mass_x_kg
        velocity = max(0.0, velocity + acceleration * ACCELERATION_DT_SECONDS)
        max_velocity = max(max_velocity, velocity)
        if time_to_50 is None and velocity >= 50.0:
            time_to_50 = (step + 1) * ACCELERATION_DT_SECONDS
            break

    return {
        "duration_seconds": round_float(ACCELERATION_DURATION_SECONDS),
        "dt_seconds": round_float(ACCELERATION_DT_SECONDS),
        "max_speed_mps": round_float(max_velocity),
        "time_to_50_mps_seconds": None if time_to_50 is None else round_float(time_to_50),
    }


def stop_distance_to_ratio(initial_speed_mps: float, square_drag_per_meter: float, ratio: float) -> float:
    safe_ratio = max(min(ratio, 0.99), 0.001)
    if initial_speed_mps <= 0.0 or square_drag_per_meter <= 0.0:
        return 0.0
    return math.log(1.0 / safe_ratio) / square_drag_per_meter


def cavitation_thresholds(shape: HullShape, depths_m: Sequence[float]) -> Dict[str, object]:
    prop_tip_speed = 2.0 * math.pi * (shape.quiet_prop_rpm / 60.0) * shape.prop_radius_m
    inflow_scale = max(0.05, 1.0 - shape.wake_fraction)
    rows: List[Dict[str, float]] = []
    for depth in depths_m:
        ambient_pressure = ATM_PRESSURE_PA + SEA_WATER_DENSITY_KG_M3 * GRAVITY_M_S2 * max(0.0, depth)
        pressure_margin = max(0.0, ambient_pressure - WATER_VAPOR_PRESSURE_PA)
        critical_tip_speed = math.sqrt(max(0.0, 2.0 * pressure_margin / (SEA_WATER_DENSITY_KG_M3 * shape.sigma_critical)))
        hull_speed_onset = math.sqrt(max(0.0, critical_tip_speed ** 2 - prop_tip_speed ** 2)) / inflow_scale
        rows.append(
            {
                "depth_m": round_float(depth),
                "ambient_pressure_kpa": round_float(ambient_pressure / 1000.0),
                "critical_effective_prop_speed_mps": round_float(critical_tip_speed),
                "quiet_prop_tip_speed_mps": round_float(prop_tip_speed),
                "hull_speed_onset_mps": round_float(hull_speed_onset),
            }
        )

    return {
        "model": "sigma=(p_ambient-p_vapor)/(0.5*rho*v_effective_prop^2); cavitates when sigma <= sigma_critical",
        "sigma_critical": round_float(shape.sigma_critical),
        "quiet_prop_rpm": round_float(shape.quiet_prop_rpm),
        "prop_radius_m": round_float(shape.prop_radius_m),
        "wake_fraction": round_float(shape.wake_fraction),
        "thresholds_by_depth": rows,
        "gameplay_output": "Use threshold crossing for acoustic/noise/VFX state. Do not simulate bubble microphysics.",
    }


def steady_power_watts(spec: Dict[str, object], speed_mps: float) -> float:
    cd_area = spec["drag_tensor_cd_area_m2_local_xyz"][0][0]  # type: ignore[index]
    efficiency = next(shape.propulsive_efficiency for shape in HULL_SHAPES if shape.shape_id == spec["shape_id"])
    drag_force = 0.5 * SEA_WATER_DENSITY_KG_M3 * cd_area * speed_mps * speed_mps
    return drag_force * speed_mps / max(efficiency, 0.01)


def generate_power_rows(specs: Sequence[Dict[str, object]]) -> List[Dict[str, object]]:
    rows: List[Dict[str, object]] = []
    count = int(POWER_SPEED_MAX_MPS / POWER_SPEED_STEP_MPS) + 1
    for index in range(count + 1):
        speed = min(POWER_SPEED_MAX_MPS, index * POWER_SPEED_STEP_MPS)
        row: Dict[str, object] = {"speed_mps": round_float(speed)}
        for spec in specs:
            power_mw = steady_power_watts(spec, speed) / 1000000.0
            row[str(spec["shape_id"])] = round_float(power_mw)
        rows.append(row)
    return rows


def write_power_csv(path: Path, rows: Sequence[Dict[str, object]], specs: Sequence[Dict[str, object]]) -> None:
    fieldnames = ["speed_mps"] + [str(spec["shape_id"]) for spec in specs]
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        for row in rows:
            writer.writerow(row)


def write_svg_plot(path: Path, rows: Sequence[Dict[str, object]], specs: Sequence[Dict[str, object]]) -> None:
    path.write_bytes(canonical_svg_bytes())


def write_png_plot(path: Path, rows: Sequence[Dict[str, object]], specs: Sequence[Dict[str, object]]) -> None:
    path.write_bytes(canonical_png_bytes())


def verify_specs(specs: Sequence[Dict[str, object]]) -> Tuple[bool, List[str]]:
    failures: List[str] = []
    for spec in specs:
        shape_id = str(spec["shape_id"])
        terminal = float(spec["terminal_speed_at_max_thrust_mps"])
        if terminal >= MAX_ACCEPTED_TERMINAL_SPEED_MPS:
            failures.append(f"{shape_id}: terminal speed {terminal:.2f} m/s exceeds {MAX_ACCEPTED_TERMINAL_SPEED_MPS:.2f} m/s")
        accel = spec["verification"]["acceleration_gate"]  # type: ignore[index]
        time_to_50 = accel["time_to_50_mps_seconds"]  # type: ignore[index]
        if time_to_50 is not None and float(time_to_50) <= INSTANT_50MPS_SECONDS:
            failures.append(f"{shape_id}: reaches 50 m/s in {float(time_to_50):.2f} seconds")
        stop_lengths = float(spec["verification"]["stop_distance_hull_lengths"])  # type: ignore[index]
        if stop_lengths < MIN_STOP_DISTANCE_HULL_LENGTHS:
            failures.append(f"{shape_id}: stop distance {stop_lengths:.2f} hull lengths below {MIN_STOP_DISTANCE_HULL_LENGTHS:.2f}")
        cd_tensor = spec["drag_tensor_cd_area_m2_local_xyz"]  # type: ignore[index]
        added_tensor = spec["added_mass_tensor_kg_local_xyz"]  # type: ignore[index]
        if cd_tensor[0][0] <= 0.0 or cd_tensor[1][1] <= 0.0 or cd_tensor[2][2] <= 0.0:
            failures.append(f"{shape_id}: drag tensor contains non-positive diagonal")
        if added_tensor[0][0] <= 0.0 or added_tensor[1][1] <= 0.0 or added_tensor[2][2] <= 0.0:
            failures.append(f"{shape_id}: added mass tensor contains non-positive diagonal")
    return len(failures) == 0, failures


def build_export_document(specs: Sequence[Dict[str, object]]) -> Dict[str, object]:
    ok, failures = verify_specs(specs)
    return {
        "schema_id": "hecton8.submarine_hydrodynamics.v1",
        "status": "HYDRODYNAMICS DEFINED" if ok else "PENDING VERIFICATION",
        "expensive_weight_feel": {
            "design_intent": "Submarines are heavy pressure vessels pushing water, not free-flying spacecraft in blue fog.",
            "control_rule": "Forward acceleration is limited by operational mass plus surge added mass; lateral and vertical motion pay larger added-mass penalties.",
            "drag_rule": "Steady speed costs cubic power because drag is quadratic and shaft power is drag force times speed.",
            "rotation_rule": "Pitch, yaw, and roll use separate angular damping tensors so hull shape controls attitude feel.",
            "cavitation_rule": "Speed and depth thresholds drive acoustic/VFX feedback; bubble microphysics remains a presentation fake.",
        },
        "units": {
            "density": "kg/m^3",
            "drag_tensor": "Cd * projected_area in m^2",
            "square_drag_accel_tensor": "1/m; runtime analytical drag v=v0/(1+k*abs(v0)*dt)",
            "added_mass_tensor": "kg",
            "angular_damping_tensor": "N*m per (rad/s)^2",
        },
        "runtime_contract": {
            "force_application": "Route final force packets through PhysicsApplySystem; do not call Rigidbody.AddForce from vehicle gameplay code.",
            "integration_hint": "Use force * rcp(operational_mass + added_mass_axis) and analytical quadratic drag.",
            "hot_path_gc": "Load this data into preallocated structs or NativeArrays. Do not parse JSON in Tick/FixedTick.",
            "black_box": "Runtime vehicle physics owner must write 300 fixed frames of state to Dump_HYDRODYNAMIC_DRAG_MATRIX_BAKER.bin on NaN/crash.",
        },
        "quality_tier_runtime_usage": {
            "Low": "Axial CdA, surge added mass, binary cavitation state, scalar yaw damping. Minimal VFX.",
            "Middle": "Full XYZ drag/effective mass tensors, cavitation intensity ramps, pitch/yaw lift curves.",
            "High": "Angular damping tensors, added angular inertia, wake/acoustic feedback driven by thresholds.",
            "Ultra": "Same physics truth as High plus visual overkill: denser wake sheets, cockpit vibration, sonar bloom, and hull creak audio.",
        },
        "global_constants": {
            "sea_water_density_kg_m3": round_float(SEA_WATER_DENSITY_KG_M3),
            "gravity_m_s2": round_float(GRAVITY_M_S2),
            "atmospheric_pressure_pa": round_float(ATM_PRESSURE_PA),
            "water_vapor_pressure_pa": round_float(WATER_VAPOR_PRESSURE_PA),
            "stop_target_speed_ratio": round_float(STOP_TARGET_SPEED_RATIO),
            "min_stop_distance_hull_lengths": round_float(MIN_STOP_DISTANCE_HULL_LENGTHS),
        },
        "hulls": list(specs),
        "verification": {
            "passed": ok,
            "failures": failures,
            "task_6_self_audit": "No Square_Drag increase required" if ok else "FAILED: increase Square_Drag coefficients and rerun",
        },
    }


def canonical_export_document() -> Dict[str, object]:
    return build_export_document([build_spec(shape) for shape in HULL_SHAPES])


def canonical_specs_and_rows() -> Tuple[List[Dict[str, object]], List[Dict[str, object]]]:
    specs = [build_spec(shape) for shape in HULL_SHAPES]
    return specs, generate_power_rows(specs)


def write_json(path: Path, document: Dict[str, object]) -> None:
    path.write_text(json.dumps(document, indent=2, sort_keys=False, allow_nan=False) + "\n", encoding="utf-8")


def non_finite_number_failures(value: object, path: str) -> List[str]:
    failures: List[str] = []
    if isinstance(value, bool) or value is None or isinstance(value, str):
        return failures
    if isinstance(value, (int, float)):
        if not math.isfinite(float(value)):
            failures.append(f"non-finite number: {path}")
        return failures
    if isinstance(value, list):
        for index, item in enumerate(value):
            failures.extend(non_finite_number_failures(item, f"{path}[{index}]"))
        return failures
    if isinstance(value, dict):
        for key, item in value.items():
            failures.extend(non_finite_number_failures(item, f"{path}.{key}"))
    return failures


def diag_values(matrix: object) -> Tuple[float, float, float]:
    rows = matrix  # type: ignore[assignment]
    return float(rows[0][0]), float(rows[1][1]), float(rows[2][2])  # type: ignore[index]


def runtime_record_values(spec: Dict[str, object]) -> List[float]:
    dimensions = spec["dimensions_m"]  # type: ignore[index]
    drag = diag_values(spec["drag_tensor_cd_area_m2_local_xyz"])
    square_drag = diag_values(spec["square_drag_accel_tensor_per_meter_local_xyz"])
    added_mass = diag_values(spec["added_mass_tensor_kg_local_xyz"])
    effective_mass = diag_values(spec["effective_mass_tensor_kg_local_xyz"])
    angular_damping = diag_values(spec["angular_quadratic_damping_torque_tensor_n_m_per_rad_s_sq_local_xyz"])
    added_angular = diag_values(spec["added_angular_inertia_tensor_kg_m2_local_xyz"])
    rigid_angular = diag_values(spec["rigid_angular_inertia_tensor_kg_m2_local_xyz"])
    lift_samples = spec["lift_curve_samples"]  # type: ignore[index]
    cavitation_rows = spec["cavitation"]["thresholds_by_depth"]  # type: ignore[index]
    values: List[float] = [
        float(dimensions["length"]),  # type: ignore[index]
        float(dimensions["beam"]),  # type: ignore[index]
        float(dimensions["height"]),  # type: ignore[index]
        float(spec["operational_mass_kg"]),
        float(spec["displaced_water_mass_kg"]),
        float(spec["max_thrust_n"]),
        float(spec["rated_cruise_speed_mps"]),
        float(spec["terminal_speed_at_max_thrust_mps"]),
        *drag,
        *square_drag,
        *added_mass,
        *effective_mass,
        *angular_damping,
        *added_angular,
        *rigid_angular,
    ]
    for sample in lift_samples:  # type: ignore[assignment]
        values.append(float(sample["pitch_Cl"]))
        values.append(float(sample["yaw_Cl"]))
    for row in cavitation_rows:  # type: ignore[assignment]
        values.append(float(row["hull_speed_onset_mps"]))
    for row in cavitation_rows:  # type: ignore[assignment]
        values.append(float(row["critical_effective_prop_speed_mps"]))
    if len(values) != RUNTIME_PACK_FLOAT_COUNT:
        raise ValueError(f"runtime pack float count mismatch: {len(values)}")
    return values


def write_runtime_pack(path: Path, specs: Sequence[Dict[str, object]]) -> None:
    header = struct.pack(
        RUNTIME_PACK_HEADER_FORMAT,
        RUNTIME_PACK_MAGIC,
        RUNTIME_PACK_VERSION,
        len(specs),
        RUNTIME_PACK_FLOAT_COUNT,
        struct.calcsize(RUNTIME_PACK_RECORD_FORMAT),
    )
    payload = bytearray(header)
    for index, spec in enumerate(specs):
        shape_hash = fnv1a_32(str(spec["shape_id"]))
        payload.extend(struct.pack(RUNTIME_PACK_RECORD_FORMAT, shape_hash, index, *runtime_record_values(spec)))
    path.write_bytes(payload)


def read_runtime_pack(path: Path) -> List[Dict[str, object]]:
    payload = path.read_bytes()
    header_size = struct.calcsize(RUNTIME_PACK_HEADER_FORMAT)
    record_size = struct.calcsize(RUNTIME_PACK_RECORD_FORMAT)
    if len(payload) < header_size:
        raise ValueError("runtime pack truncated header")
    magic, version, hull_count, float_count, stride = struct.unpack(
        RUNTIME_PACK_HEADER_FORMAT,
        payload[:header_size],
    )
    if magic != RUNTIME_PACK_MAGIC:
        raise ValueError("runtime pack magic mismatch")
    if version != RUNTIME_PACK_VERSION:
        raise ValueError("runtime pack version mismatch")
    if float_count != RUNTIME_PACK_FLOAT_COUNT:
        raise ValueError("runtime pack float count mismatch")
    if stride != record_size:
        raise ValueError("runtime pack stride mismatch")
    expected_size = header_size + (record_size * hull_count)
    if len(payload) != expected_size:
        raise ValueError("runtime pack byte size mismatch")

    records: List[Dict[str, object]] = []
    for record_index in range(hull_count):
        offset = header_size + (record_size * record_index)
        unpacked = struct.unpack(RUNTIME_PACK_RECORD_FORMAT, payload[offset:offset + record_size])
        values: Dict[str, float] = {}
        for field_index, field_name in enumerate(RUNTIME_PACK_FLOAT_FIELDS):
            values[field_name] = float(unpacked[field_index + 2])
        records.append(
            {
                "shape_hash": int(unpacked[0]),
                "shape_index": int(unpacked[1]),
                "values": values,
            }
        )
    return records


def runtime_pack_spec_failures(records: Sequence[Dict[str, object]], specs: Sequence[Dict[str, object]]) -> List[str]:
    failures: List[str] = []
    if len(records) != len(specs):
        failures.append("runtime pack/spec record count mismatch")
        return failures
    for index, spec in enumerate(specs):
        shape_id = str(spec["shape_id"])
        record = records[index]
        if record.get("shape_hash") != fnv1a_32(shape_id):
            failures.append(f"runtime pack shape hash mismatch: {shape_id}")
        if record.get("shape_index") != index:
            failures.append(f"runtime pack index mismatch: {shape_id}")
        values = record.get("values")
        if not isinstance(values, dict):
            failures.append(f"runtime pack values missing: {shape_id}")
            continue
        expected_values = runtime_record_values(spec)
        for field_index, field_name in enumerate(RUNTIME_PACK_FLOAT_FIELDS):
            actual = float(values[field_name])
            expected = expected_values[field_index]
            if not math.isfinite(actual):
                failures.append(f"runtime pack non-finite field: {shape_id} {field_name}")
                continue
            tolerance = max(0.001, abs(expected) * 0.00001)
            if abs(actual - expected) > tolerance:
                failures.append(f"runtime pack field mismatch: {shape_id} {field_name}")
    return failures


def power_csv_spec_failures(rows: Sequence[Dict[str, str]], specs: Sequence[Dict[str, object]]) -> List[str]:
    failures: List[str] = []
    expected_rows = generate_power_rows(specs)
    if len(rows) != len(expected_rows):
        failures.append("power csv/spec row count mismatch")
        return failures
    for row_index, expected_row in enumerate(expected_rows):
        row = rows[row_index]
        speed = float(row["speed_mps"])
        expected_speed = float(expected_row["speed_mps"])
        if not math.isfinite(speed):
            failures.append(f"power csv non-finite speed: row {row_index}")
            continue
        if abs(speed - expected_speed) > 0.000001:
            failures.append(f"power csv speed mismatch: row {row_index}")
        for spec in specs:
            shape_id = str(spec["shape_id"])
            value = float(row[shape_id])
            expected_value = float(expected_row[shape_id])
            if not math.isfinite(value):
                failures.append(f"power csv non-finite value: row {row_index} {shape_id}")
                continue
            if abs(value - expected_value) > 0.00001:
                failures.append(f"power csv value mismatch: row {row_index} {shape_id}")
    return failures


def canonical_svg_bytes() -> bytes:
    specs, rows = canonical_specs_and_rows()
    lines: List[str] = [
        '<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="720" viewBox="0 0 1200 720">',
        '<rect width="1200" height="720" fill="#071019"/>',
        '<text x="48" y="54" fill="#d8e6f3" font-family="monospace" font-size="26">Submarine Speed vs Power Consumption</text>',
        '<text x="48" y="86" fill="#8fa9c2" font-family="monospace" font-size="15">Power rises cubically under quadratic drag; plotted from baked CdA tensors.</text>',
    ]
    max_speed = POWER_SPEED_MAX_MPS
    max_power = max(float(row[str(spec["shape_id"])]) for row in rows for spec in specs)  # type: ignore[index]
    pad_left = 90.0
    pad_right = 70.0
    pad_top = 125.0
    pad_bottom = 86.0
    width = 1200.0
    height = 720.0
    plot_w = width - pad_left - pad_right
    plot_h = height - pad_top - pad_bottom

    def map_x(speed: float) -> float:
        return pad_left + (speed / max_speed) * plot_w

    def map_y(power: float) -> float:
        return pad_top + plot_h - (power / max_power) * plot_h

    for tick in range(0, 8):
        x = map_x(max_speed * tick / 7.0)
        lines.append(f'<line x1="{x:.2f}" y1="{pad_top:.2f}" x2="{x:.2f}" y2="{pad_top + plot_h:.2f}" stroke="#1b2d3a" stroke-width="1"/>')
    for tick in range(0, 7):
        y = map_y(max_power * tick / 6.0)
        lines.append(f'<line x1="{pad_left:.2f}" y1="{y:.2f}" x2="{pad_left + plot_w:.2f}" y2="{y:.2f}" stroke="#1b2d3a" stroke-width="1"/>')
    lines.append(f'<line x1="{pad_left:.2f}" y1="{pad_top + plot_h:.2f}" x2="{pad_left + plot_w:.2f}" y2="{pad_top + plot_h:.2f}" stroke="#6f8799" stroke-width="2"/>')
    lines.append(f'<line x1="{pad_left:.2f}" y1="{pad_top:.2f}" x2="{pad_left:.2f}" y2="{pad_top + plot_h:.2f}" stroke="#6f8799" stroke-width="2"/>')

    colors = ["#56c2ff", "#ffb84d", "#ff6f61", "#8ef0a6", "#c19bff"]
    for index, spec in enumerate(specs):
        shape_id = str(spec["shape_id"])
        points = []
        for row in rows:
            speed = float(row["speed_mps"])
            power = float(row[shape_id])
            points.append(f"{map_x(speed):.2f},{map_y(power):.2f}")
        color = colors[index % len(colors)]
        lines.append(f'<polyline fill="none" stroke="{color}" stroke-width="3" points="{" ".join(points)}"/>')
        legend_y = 135 + index * 26
        lines.append(f'<line x1="865" y1="{legend_y}" x2="914" y2="{legend_y}" stroke="{color}" stroke-width="4"/>')
        lines.append(f'<text x="924" y="{legend_y + 5}" fill="#d8e6f3" font-family="monospace" font-size="14">{shape_id}</text>')

    lines.append('<text x="510" y="676" fill="#8fa9c2" font-family="monospace" font-size="14">Speed (m/s)</text>')
    lines.append('<text x="20" y="368" fill="#8fa9c2" font-family="monospace" font-size="14" transform="rotate(-90 20 368)">Shaft Power (W)</text>')
    lines.append("</svg>")
    return ("\n".join(lines) + "\n").encode("utf-8")


def canonical_png_bytes() -> bytes:
    specs, rows = canonical_specs_and_rows()
    width = 1200
    height = 720
    pad_left = 90
    pad_right = 70
    pad_top = 125
    pad_bottom = 86
    plot_w = width - pad_left - pad_right
    plot_h = height - pad_top - pad_bottom
    max_speed = POWER_SPEED_MAX_MPS
    max_power = max(float(row[str(spec["shape_id"])]) for row in rows for spec in specs)  # type: ignore[index]
    pixels = bytearray([7, 16, 25] * width * height)
    colors: Tuple[Tuple[int, int, int], ...] = (
        (86, 194, 255),
        (255, 184, 77),
        (255, 111, 97),
        (142, 240, 166),
        (193, 155, 255),
    )

    def set_pixel(x_pos: int, y_pos: int, color: Tuple[int, int, int]) -> None:
        if 0 <= x_pos < width and 0 <= y_pos < height:
            index = (y_pos * width + x_pos) * 3
            pixels[index] = color[0]
            pixels[index + 1] = color[1]
            pixels[index + 2] = color[2]

    def draw_line(x0: int, y0: int, x1: int, y1: int, color: Tuple[int, int, int], thickness: int = 1) -> None:
        dx = abs(x1 - x0)
        sx = 1 if x0 < x1 else -1
        dy = -abs(y1 - y0)
        sy = 1 if y0 < y1 else -1
        error = dx + dy
        radius = max(0, thickness // 2)
        while True:
            for y_offset in range(-radius, radius + 1):
                for x_offset in range(-radius, radius + 1):
                    set_pixel(x0 + x_offset, y0 + y_offset, color)
            if x0 == x1 and y0 == y1:
                break
            doubled = 2 * error
            if doubled >= dy:
                error += dy
                x0 += sx
            if doubled <= dx:
                error += dx
                y0 += sy

    def map_x(speed: float) -> int:
        return int(round(pad_left + (speed / max_speed) * plot_w))

    def map_y(power: float) -> int:
        return int(round(pad_top + plot_h - (power / max_power) * plot_h))

    grid = (38, 50, 65)
    axis = (96, 112, 128)
    for tick in range(0, 8):
        x_pos = map_x(max_speed * tick / 7.0)
        draw_line(x_pos, pad_top, x_pos, pad_top + plot_h, grid)
    for tick in range(0, 7):
        y_pos = map_y(max_power * tick / 6.0)
        draw_line(pad_left, y_pos, pad_left + plot_w, y_pos, grid)
    draw_line(pad_left, pad_top + plot_h, pad_left + plot_w, pad_top + plot_h, axis, 2)
    draw_line(pad_left, pad_top, pad_left, pad_top + plot_h, axis, 2)

    for index, spec in enumerate(specs):
        shape_id = str(spec["shape_id"])
        color = colors[index % len(colors)]
        previous_x = map_x(float(rows[0]["speed_mps"]))
        previous_y = map_y(float(rows[0][shape_id]))
        for row in rows[1:]:
            current_x = map_x(float(row["speed_mps"]))
            current_y = map_y(float(row[shape_id]))
            draw_line(previous_x, previous_y, current_x, current_y, color, 3)
            previous_x = current_x
            previous_y = current_y
        legend_y = 70 + index * 24
        draw_line(886, legend_y, 926, legend_y, color, 4)

    def chunk(chunk_type: bytes, payload: bytes) -> bytes:
        return (
            struct.pack(">I", len(payload))
            + chunk_type
            + payload
            + struct.pack(">I", zlib.crc32(chunk_type + payload) & 0xFFFFFFFF)
        )

    raw = bytearray()
    for y_pos in range(height):
        raw.append(0)
        row_start = y_pos * width * 3
        raw.extend(pixels[row_start:row_start + width * 3])
    return (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(bytes(raw), level=9))
        + chunk(b"IEND", b"")
    )


def runtime_pack_layout_document() -> Dict[str, object]:
    header_size = struct.calcsize(RUNTIME_PACK_HEADER_FORMAT)
    record_size = struct.calcsize(RUNTIME_PACK_RECORD_FORMAT)
    fields = []
    for index, field_name in enumerate(RUNTIME_PACK_FLOAT_FIELDS):
        fields.append(
            {
                "index": index,
                "byte_offset_from_record_start": 8 + (index * 4),
                "name": field_name,
                "format": "float32_le",
            }
        )
    return {
        "schema_id": "hecton8.submarine_runtime_pack_layout.v1",
        "byte_order": "little-endian",
        "magic_ascii": "H8HYDRO\\0",
        "version": RUNTIME_PACK_VERSION,
        "header": {
            "format": RUNTIME_PACK_HEADER_FORMAT,
            "bytes": header_size,
            "fields": [
                {"name": "magic", "byte_offset": 0, "bytes": 8, "format": "char[8]"},
                {"name": "version", "byte_offset": 8, "bytes": 4, "format": "uint32_le"},
                {"name": "hull_count", "byte_offset": 12, "bytes": 4, "format": "uint32_le"},
                {"name": "float_count", "byte_offset": 16, "bytes": 4, "format": "uint32_le"},
                {"name": "record_stride_bytes", "byte_offset": 20, "bytes": 4, "format": "uint32_le"},
            ],
        },
        "record": {
            "format": RUNTIME_PACK_RECORD_FORMAT,
            "bytes": record_size,
            "fields": [
                {"index": -2, "byte_offset_from_record_start": 0, "name": "shape_hash_fnv1a32", "format": "uint32_le"},
                {"index": -1, "byte_offset_from_record_start": 4, "name": "shape_index", "format": "uint32_le"},
                *fields,
            ],
        },
        "hull_order": [shape.shape_id for shape in HULL_SHAPES],
        "notes": [
            "Load at initialization only; do not parse JSON in Tick or FixedTick.",
            "Records are append-only for batch compatibility; increase version before changing existing field order.",
        ],
    }


def write_runtime_pack_layout(path: Path) -> None:
    write_json(path, runtime_pack_layout_document())


def file_manifest(path: Path) -> Dict[str, object]:
    payload = path.read_bytes()
    return {
        "file": path.name,
        "bytes": len(payload),
        "sha256": hashlib.sha256(payload).hexdigest().upper(),
    }


def validate_existing_output(output_dir: Path) -> Dict[str, object]:
    failures: List[str] = []
    verification_path = output_dir / "Submarine_Verification.json"
    specs_path = output_dir / "Submarine_Specs.json"
    csv_path = output_dir / "Submarine_SpeedPower.csv"
    svg_path = output_dir / "Submarine_SpeedPower.svg"
    png_path = output_dir / "Submarine_SpeedPower.png"
    runtime_pack_path = output_dir / "Submarine_RuntimePack.bin"
    runtime_layout_path = output_dir / "Submarine_RuntimePackLayout.json"
    required_files = (verification_path, specs_path, csv_path, svg_path, png_path, runtime_pack_path, runtime_layout_path)
    for path in required_files:
        if not path.exists():
            failures.append(f"missing file: {path.name}")

    verification: Dict[str, object] = {}
    specs_document: Dict[str, object] = {}
    validated_hulls: Optional[List[Dict[str, object]]] = None
    if verification_path.exists():
        try:
            verification = json.loads(verification_path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            failures.append(f"verification json decode failed: {exc.msg}")
    if specs_path.exists():
        try:
            specs_document = json.loads(specs_path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            failures.append(f"specs json decode failed: {exc.msg}")

    if verification:
        if verification.get("status") != "HYDRODYNAMICS DEFINED":
            failures.append("verification status is not HYDRODYNAMICS DEFINED")
        if verification.get("verification_passed") is not True:
            failures.append("verification_passed is not true")
        for key, path in (
            ("specs", specs_path),
            ("power_csv", csv_path),
            ("power_svg", svg_path),
            ("power_png", png_path),
            ("runtime_pack", runtime_pack_path),
            ("runtime_pack_layout", runtime_layout_path),
        ):
            if verification.get(key) != path.name:
                failures.append(f"verification summary file mismatch: {key}")
        if verification.get("failures") != []:
            failures.append("verification failures list mismatch")
        artifacts = verification.get("artifacts")
        if not isinstance(artifacts, dict):
            failures.append("verification artifact manifest missing")
        else:
            for key, path in (
                ("specs", specs_path),
                ("power_csv", csv_path),
                ("power_svg", svg_path),
                ("power_png", png_path),
                ("runtime_pack", runtime_pack_path),
                ("runtime_pack_layout", runtime_layout_path),
            ):
                artifact = artifacts.get(key)
                if not isinstance(artifact, dict):
                    failures.append(f"artifact manifest missing: {key}")
                    continue
                if artifact.get("file") != path.name:
                    failures.append(f"artifact file mismatch: {key}")
                if path.exists():
                    manifest = file_manifest(path)
                    if artifact.get("bytes") != manifest["bytes"]:
                        failures.append(f"artifact byte mismatch: {path.name}")
                    if artifact.get("sha256") != manifest["sha256"]:
                        failures.append(f"artifact sha256 mismatch: {path.name}")

    if specs_document:
        failures.extend(non_finite_number_failures(specs_document, "specs"))
        if specs_document != canonical_export_document():
            failures.append("specs document mismatch")
        if specs_document.get("status") != "HYDRODYNAMICS DEFINED":
            failures.append("specs status is not HYDRODYNAMICS DEFINED")
        hulls = specs_document.get("hulls")
        if not isinstance(hulls, list) or len(hulls) != len(HULL_SHAPES):
            failures.append("specs hull count mismatch")
        elif [str(hull.get("shape_id")) for hull in hulls] != [shape.shape_id for shape in HULL_SHAPES]:
            failures.append("specs hull order mismatch")
        else:
            validated_hulls = hulls
            ok, spec_failures = verify_specs(hulls)
            if not ok:
                failures.extend(spec_failures)

    if csv_path.exists():
        try:
            with csv_path.open(encoding="utf-8", newline="") as handle:
                rows = list(csv.DictReader(handle))
            expected_columns = ["speed_mps"] + [shape.shape_id for shape in HULL_SHAPES]
            if rows and list(rows[0].keys()) != expected_columns:
                failures.append("power csv columns mismatch")
            expected_rows = int(POWER_SPEED_MAX_MPS / POWER_SPEED_STEP_MPS) + 2
            if len(rows) != expected_rows:
                failures.append("power csv row count mismatch")
            for shape in HULL_SHAPES:
                previous = -1.0
                for row in rows:
                    value = float(row[shape.shape_id])
                    if value < previous:
                        failures.append(f"power csv non-monotonic: {shape.shape_id}")
                        break
                    previous = value
            if validated_hulls is not None:
                failures.extend(power_csv_spec_failures(rows, validated_hulls))
        except (OSError, ValueError, KeyError) as exc:
            failures.append(f"power csv validation failed: {exc}")

    if svg_path.exists():
        try:
            svg_bytes = svg_path.read_bytes()
            if not svg_bytes.startswith(b'<svg xmlns="http://www.w3.org/2000/svg"'):
                failures.append("svg header mismatch")
            if svg_bytes != canonical_svg_bytes():
                failures.append("svg canonical payload mismatch")
        except OSError as exc:
            failures.append(f"svg validation failed: {exc}")

    if png_path.exists():
        try:
            png_bytes = png_path.read_bytes()
            if png_bytes[:8] != b"\x89PNG\r\n\x1a\n" or png_bytes[12:16] != b"IHDR":
                failures.append("png header mismatch")
            elif struct.unpack(">IIBB", png_bytes[16:26]) != (1200, 720, 8, 2):
                failures.append("png dimensions/format mismatch")
            if png_bytes != canonical_png_bytes():
                failures.append("png canonical payload mismatch")
        except (OSError, struct.error) as exc:
            failures.append(f"png validation failed: {exc}")

    runtime_records: List[Dict[str, object]] = []
    if runtime_pack_path.exists():
        try:
            runtime_records = read_runtime_pack(runtime_pack_path)
            if len(runtime_records) != len(HULL_SHAPES):
                failures.append("runtime pack hull count mismatch")
            else:
                for index, shape in enumerate(HULL_SHAPES):
                    record = runtime_records[index]
                    if record.get("shape_hash") != fnv1a_32(shape.shape_id):
                        failures.append(f"runtime pack shape hash mismatch: {shape.shape_id}")
                    if record.get("shape_index") != index:
                        failures.append(f"runtime pack index mismatch: {shape.shape_id}")
                if validated_hulls is not None:
                    failures.extend(runtime_pack_spec_failures(runtime_records, validated_hulls))
        except (OSError, struct.error, ValueError) as exc:
            failures.append(f"runtime pack validation failed: {exc}")

    if runtime_layout_path.exists():
        try:
            layout = json.loads(runtime_layout_path.read_text(encoding="utf-8"))
            expected_layout = runtime_pack_layout_document()
            if layout.get("schema_id") != "hecton8.submarine_runtime_pack_layout.v1":
                failures.append("runtime layout schema mismatch")
            if layout.get("version") != RUNTIME_PACK_VERSION:
                failures.append("runtime layout version mismatch")
            if layout != expected_layout:
                failures.append("runtime layout document mismatch")
            if layout.get("header") != expected_layout["header"]:
                failures.append("runtime layout header mismatch")
            if layout.get("hull_order") != expected_layout["hull_order"]:
                failures.append("runtime layout hull order mismatch")
            record = layout.get("record")
            if not isinstance(record, dict):
                failures.append("runtime layout record missing")
            else:
                if record.get("bytes") != struct.calcsize(RUNTIME_PACK_RECORD_FORMAT):
                    failures.append("runtime layout stride mismatch")
                fields = record.get("fields")
                if not isinstance(fields, list) or len(fields) != RUNTIME_PACK_FLOAT_COUNT + 2:
                    failures.append("runtime layout field count mismatch")
                elif fields != expected_layout["record"]["fields"]:  # type: ignore[index]
                    failures.append("runtime layout field definition mismatch")
        except (OSError, json.JSONDecodeError) as exc:
            failures.append(f"runtime layout validation failed: {exc}")

    return {
        "status": "HYDRODYNAMICS DEFINED" if not failures else "PENDING VERIFICATION",
        "verification_passed": len(failures) == 0,
        "failures": failures,
        "mode": "verify_only",
    }


def run(output_dir: Path) -> Dict[str, object]:
    output_dir.mkdir(parents=True, exist_ok=True)
    specs = [build_spec(shape) for shape in HULL_SHAPES]
    document = build_export_document(specs)
    json_path = output_dir / "Submarine_Specs.json"
    csv_path = output_dir / "Submarine_SpeedPower.csv"
    svg_path = output_dir / "Submarine_SpeedPower.svg"
    png_path = output_dir / "Submarine_SpeedPower.png"
    runtime_pack_path = output_dir / "Submarine_RuntimePack.bin"
    runtime_layout_path = output_dir / "Submarine_RuntimePackLayout.json"
    verification_path = output_dir / "Submarine_Verification.json"

    rows = generate_power_rows(specs)
    write_json(json_path, document)
    write_power_csv(csv_path, rows, specs)
    write_svg_plot(svg_path, rows, specs)
    write_png_plot(png_path, rows, specs)
    write_runtime_pack(runtime_pack_path, specs)
    write_runtime_pack_layout(runtime_layout_path)
    artifact_manifest = {
        "specs": file_manifest(json_path),
        "power_csv": file_manifest(csv_path),
        "power_svg": file_manifest(svg_path),
        "power_png": file_manifest(png_path),
        "runtime_pack": file_manifest(runtime_pack_path),
        "runtime_pack_layout": file_manifest(runtime_layout_path),
    }

    summary = {
        "status": document["status"],
        "specs": json_path.name,
        "power_csv": csv_path.name,
        "power_svg": svg_path.name,
        "power_png": png_path.name,
        "runtime_pack": runtime_pack_path.name,
        "runtime_pack_layout": runtime_layout_path.name,
        "artifacts": artifact_manifest,
        "verification_passed": document["verification"]["passed"],  # type: ignore[index]
        "failures": document["verification"]["failures"],  # type: ignore[index]
    }
    write_json(verification_path, summary)
    return summary


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Bake HECTON-8 submarine hydrodynamic tensors and plots.")
    parser.add_argument("--out-dir", default="Data/Physics", help="Output directory for JSON/CSV/plot artifacts.")
    parser.add_argument("--verify-only", action="store_true", help="Validate existing hydro artifacts without regenerating files.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    summary = validate_existing_output(Path(args.out_dir)) if args.verify_only else run(Path(args.out_dir))
    print(json.dumps(summary, indent=2))
    return 0 if summary["verification_passed"] else 2


if __name__ == "__main__":
    raise SystemExit(main())

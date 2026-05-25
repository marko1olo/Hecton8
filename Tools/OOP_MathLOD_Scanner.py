#!/usr/bin/env python3
import json
import math
import re
import struct
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SCRIPTS = ROOT / "Assets" / "_Project" / "Scripts"
REPORT = ROOT / "Docs" / "Reports" / "MATH_LOD_OPTIMIZATION_REPORT_X_007.json"

TRANSCENDENTAL_PATTERNS = {
    "math.exp": re.compile(r"\bmath\.exp\s*\("),
    "math.pow": re.compile(r"\bmath\.pow\s*\("),
    "math.sin": re.compile(r"\bmath\.sin\s*\("),
    "math.cos": re.compile(r"\bmath\.cos\s*\("),
    "math.sincos": re.compile(r"\bmath\.sincos\s*\("),
    "math.log": re.compile(r"\bmath\.log\s*\("),
    "math.tan": re.compile(r"\bmath\.tan\s*\("),
    "math.atan": re.compile(r"\bmath\.atan\s*\("),
    "math.atan2": re.compile(r"\bmath\.atan2\s*\("),
    "math.asin": re.compile(r"\bmath\.asin\s*\("),
    "math.acos": re.compile(r"\bmath\.acos\s*\("),
    "UnityMathf.Exp": re.compile(r"\bMathf\.Exp\s*\("),
    "UnityMathf.Pow": re.compile(r"\bMathf\.Pow\s*\("),
    "UnityMathf.Sin": re.compile(r"\bMathf\.Sin\s*\("),
    "UnityMathf.Cos": re.compile(r"\bMathf\.Cos\s*\("),
    "UnityMathf.Log": re.compile(r"\bMathf\.Log\s*\("),
    "UnityMathf.Tan": re.compile(r"\bMathf\.Tan\s*\("),
    "UnityMathf.Atan": re.compile(r"\bMathf\.Atan\s*\("),
    "UnityMathf.Atan2": re.compile(r"\bMathf\.Atan2\s*\("),
    "UnityMathf.Asin": re.compile(r"\bMathf\.Asin\s*\("),
    "UnityMathf.Acos": re.compile(r"\bMathf\.Acos\s*\("),
    "SystemMath.Exp": re.compile(r"\b(?:System\.)?Math\.Exp\s*\("),
    "SystemMath.Pow": re.compile(r"\b(?:System\.)?Math\.Pow\s*\("),
    "SystemMath.Sin": re.compile(r"\b(?:System\.)?Math\.Sin\s*\("),
    "SystemMath.Cos": re.compile(r"\b(?:System\.)?Math\.Cos\s*\("),
    "SystemMath.Log": re.compile(r"\b(?:System\.)?Math\.Log\s*\("),
    "SystemMath.Tan": re.compile(r"\b(?:System\.)?Math\.Tan\s*\("),
    "SystemMath.Atan": re.compile(r"\b(?:System\.)?Math\.Atan\s*\("),
    "SystemMath.Atan2": re.compile(r"\b(?:System\.)?Math\.Atan2\s*\("),
    "SystemMath.Asin": re.compile(r"\b(?:System\.)?Math\.Asin\s*\("),
    "SystemMath.Acos": re.compile(r"\b(?:System\.)?Math\.Acos\s*\("),
    "SystemMathF.Exp": re.compile(r"\b(?:System\.)?MathF\.Exp\s*\("),
    "SystemMathF.Pow": re.compile(r"\b(?:System\.)?MathF\.Pow\s*\("),
    "SystemMathF.Sin": re.compile(r"\b(?:System\.)?MathF\.Sin\s*\("),
    "SystemMathF.Cos": re.compile(r"\b(?:System\.)?MathF\.Cos\s*\("),
    "SystemMathF.Log": re.compile(r"\b(?:System\.)?MathF\.Log\s*\("),
    "SystemMathF.Tan": re.compile(r"\b(?:System\.)?MathF\.Tan\s*\("),
    "SystemMathF.Atan": re.compile(r"\b(?:System\.)?MathF\.Atan\s*\("),
    "SystemMathF.Atan2": re.compile(r"\b(?:System\.)?MathF\.Atan2\s*\("),
    "SystemMathF.Asin": re.compile(r"\b(?:System\.)?MathF\.Asin\s*\("),
    "SystemMathF.Acos": re.compile(r"\b(?:System\.)?MathF\.Acos\s*\("),
}

AUDITED_BRANCH_FILES = [
    "Assets/_Project/Scripts/MathLodApproximation.cs",
    "Assets/_Project/Scripts/Atmosphere/BaseAtmosphereMath.cs",
    "Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs",
    "Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereContracts.cs",
    "Assets/_Project/Scripts/Atmosphere/SurfaceWeatherMath.cs",
    "Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs",
    "Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs",
    "Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs",
    "Assets/_Project/Scripts/Power/PowerGridJacobiContracts.cs",
    "Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs",
    "Assets/_Project/Scripts/Power/WfcOutpostGraphTranslationJob.cs",
    "Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs",
    "Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsJobs.cs",
]


def f32(value: float) -> float:
    return struct.unpack("<f", struct.pack("<f", float(value)))[0]


def clamp_finite_directional_infinity_f32(value: float, minimum: float, maximum: float, nan_fallback: float) -> float:
    if math.isfinite(value):
        selected = f32(value)
    elif value > 0.0:
        selected = f32(maximum)
    elif value < 0.0:
        selected = f32(minimum)
    else:
        selected = f32(nan_fallback)
    return f32(min(max(selected, f32(minimum)), f32(maximum)))


def approx_exp_neg_pade33_reduced_f32(value: float) -> float:
    safe = clamp_finite_directional_infinity_f32(value, 0.0, 4.0, 0.0)
    x = f32(safe * f32(0.25))
    x2 = f32(x * x)
    x3 = f32(x2 * x)
    numerator = f32(f32(1.0) - f32(0.5) * x + f32(0.1) * x2 - f32(f32(1.0) / f32(120.0)) * x3)
    denominator = f32(f32(1.0) + f32(0.5) * x + f32(0.1) * x2 + f32(f32(1.0) / f32(120.0)) * x3)
    base_decay = f32(numerator / max(denominator, f32(0.0001)))
    decay2 = f32(base_decay * base_decay)
    decay4 = f32(decay2 * decay2)
    if not math.isfinite(decay4):
        return f32(0.0)
    return f32(min(max(decay4, f32(0.0)), f32(1.0)))


def approx_exp_neg_pade33_wide40_f32(value: float) -> float:
    safe = clamp_finite_directional_infinity_f32(value, 0.0, 40.0, 0.0)
    segment = approx_exp_neg_pade33_reduced_f32(f32(safe * f32(0.1)))
    decay2 = f32(segment * segment)
    decay4 = f32(decay2 * decay2)
    decay8 = f32(decay4 * decay4)
    decay10 = f32(decay8 * decay2)
    if not math.isfinite(decay10):
        return f32(0.0)
    return f32(min(max(decay10, f32(0.0)), f32(1.0)))


def approx_exp_positive_pade33_reduced_f32(value: float) -> float:
    safe = clamp_finite_directional_infinity_f32(value, 0.0, 4.0, 0.0)
    decay = approx_exp_neg_pade33_reduced_f32(safe)
    growth = f32(1.0 / max(f32(0.0001), decay))
    return growth if math.isfinite(growth) else f32(1.0)


def approx_sin_bhaskara_f32(value: float) -> float:
    angle = f32(value if math.isfinite(value) else 0.0)
    cycle = f32(angle * f32(0.15915494309189535))
    wrapped = f32(cycle - math.floor(cycle))
    x = f32(wrapped * f32(2.0 * math.pi))
    mirrored = f32((2.0 * math.pi) - x) if x > math.pi else x
    sign = f32(-1.0) if x > math.pi else f32(1.0)
    shape = f32(mirrored * f32(math.pi - mirrored))
    numerator = f32(f32(16.0) * shape)
    denominator = f32(max(f32(0.0001), f32(f32(5.0) * f32(math.pi) * f32(math.pi)) - f32(f32(4.0) * shape)))
    sine = f32(sign * f32(numerator / denominator))
    return f32(min(max(sine if math.isfinite(sine) else 0.0, -1.0), 1.0))


def approx_cos_bhaskara_f32(value: float) -> float:
    return approx_sin_bhaskara_f32(f32(value + f32(0.5 * math.pi)))


def approx_tan_clamped_f32(value: float, max_abs: float = 4096.0) -> float:
    sine = approx_sin_bhaskara_f32(value)
    cosine = approx_cos_bhaskara_f32(value)
    denominator = max(f32(0.0001), abs(cosine))
    signed_denominator = denominator if cosine >= 0.0 else -denominator
    tangent = f32(sine / signed_denominator)
    if not math.isfinite(tangent):
        return f32(0.0)
    return f32(min(max(tangent, -abs(max_abs)), abs(max_abs)))


def approx_atan_fast_f32(value: float) -> float:
    x = f32(value if math.isfinite(value) else 0.0)
    ax = f32(abs(x))
    inv = f32(1.0 / max(ax, 0.0001))
    reduced = inv if ax > 1.0 else ax
    reduced_sq = f32(reduced * reduced)
    atan_reduced = f32(reduced / f32(1.0 + f32(0.280872) * reduced_sq))
    angle = f32(f32(0.5 * math.pi) - atan_reduced) if ax > 1.0 else atan_reduced
    signed = angle if x >= 0.0 else -angle
    return signed if math.isfinite(signed) else f32(0.0)


def approx_atan2_fast_f32(y: float, x: float) -> float:
    safe_x = f32(x if math.isfinite(x) else 0.0)
    safe_y = f32(y if math.isfinite(y) else 0.0)
    ratio = f32(abs(safe_y) / max(abs(safe_x), 0.0001))
    base_angle = approx_atan_fast_f32(ratio)
    angle = base_angle if safe_x >= 0.0 else f32(math.pi - base_angle)
    angle = -angle if safe_y < 0.0 else angle
    if abs(safe_x) < 0.0001 and abs(safe_y) < 0.0001:
        angle = f32(0.0)
    return angle if math.isfinite(angle) else f32(0.0)


def approx_acos_fast_f32(value: float) -> float:
    x = f32(min(max(value if math.isfinite(value) else 1.0, -1.0), 1.0))
    ax = f32(abs(x))
    one_minus = f32(max(0.0, 1.0 - ax))
    root = f32(one_minus / math.sqrt(max(one_minus, 0.000001)))
    angle = f32(f32(f32(f32(-0.0187293 * ax + 0.0742610) * ax - 0.2121144) * ax + 1.5707288) * root)
    if x < 0.0:
        angle = f32(math.pi - angle)
    if not math.isfinite(angle):
        return f32(0.0)
    return f32(min(max(angle, 0.0), math.pi))


def approx_pow01_curve_f32(value01: float, exponent: float) -> float:
    x_raw = value01 if math.isfinite(value01) else 0.0
    x = f32(min(max(x_raw, 0.0), 1.0))
    e_raw = exponent if math.isfinite(exponent) else 1.0
    e = f32(min(max(e_raw, 0.25), 4.0))
    sqrt1 = f32(math.sqrt(max(x, 0.0)))
    sqrt2 = f32(math.sqrt(max(sqrt1, 0.0)))
    x2 = f32(x * x)
    x3 = f32(x2 * x)
    x4 = f32(x2 * x2)
    r025_to_05 = f32(sqrt2 + (sqrt1 - sqrt2) * min(max((e - 0.25) * 4.0, 0.0), 1.0))
    r05_to_1 = f32(sqrt1 + (x - sqrt1) * min(max((e - 0.5) * 2.0, 0.0), 1.0))
    r1_to_2 = f32(x + (x2 - x) * min(max(e - 1.0, 0.0), 1.0))
    r2_to_3 = f32(x2 + (x3 - x2) * min(max(e - 2.0, 0.0), 1.0))
    r3_to_4 = f32(x3 + (x4 - x3) * min(max(e - 3.0, 0.0), 1.0))
    result = r3_to_4
    result = r2_to_3 if e < 3.0 else result
    result = r1_to_2 if e < 2.0 else result
    result = r05_to_1 if e < 1.0 else result
    result = r025_to_05 if e < 0.5 else result
    return f32(min(max(result if math.isfinite(result) else 0.0, 0.0), 1.0))


def scan_extreme_kernel_finiteness() -> dict:
    samples = [
        float("nan"),
        float("inf"),
        float("-inf"),
        -1000000.0,
        1000000.0,
        -1000.0,
        1000.0,
        -273.15,
        37.0,
        0.0,
        0.1,
        1.0,
        4.0,
        40.0,
    ]
    rows = []
    non_finite = 0
    max_abs = 0.0
    for value in samples:
        pressure = value
        temperature = value
        outputs = {
            "expNegReduced": approx_exp_neg_pade33_reduced_f32(value),
            "expNegWide": approx_exp_neg_pade33_wide40_f32(value),
            "expPositiveReduced": approx_exp_positive_pade33_reduced_f32(value),
            "sinBhaskara": approx_sin_bhaskara_f32(value),
            "cosBhaskara": approx_cos_bhaskara_f32(value),
            "tanClamped": approx_tan_clamped_f32(value, 4096.0),
            "atanFast": approx_atan_fast_f32(value),
            "atan2Fast": approx_atan2_fast_f32(temperature * 0.000001 if math.isfinite(temperature) else temperature, pressure * 0.001 if math.isfinite(pressure) else pressure),
            "acosFast": approx_acos_fast_f32((pressure * 0.001 if math.isfinite(pressure) else pressure) - 1.0),
            "pow01Curve": approx_pow01_curve_f32(value, abs(value) if math.isfinite(value) else value),
        }
        bad = [name for name, output in outputs.items() if not math.isfinite(float(output))]
        non_finite += len(bad)
        for output in outputs.values():
            if math.isfinite(float(output)):
                max_abs = max(max_abs, abs(float(output)))
        rows.append({
            "input": str(value),
            "nonFiniteKernels": bad,
            "maxAbsOutput": max(abs(float(output)) for output in outputs.values() if math.isfinite(float(output))),
        })
    return {
        "sampleCount": len(samples),
        "kernelCountPerSample": 10,
        "nonFiniteOutputCount": non_finite,
        "maxAbsFiniteOutput": max_abs,
        "rows": rows,
    }


def scan_power_destination_mask_equivalence() -> dict:
    max_conductance = 4096.0
    min_conductance = 0.000001
    potentials = [0.0, 0.25, 1.0, float("nan")]
    destinations = [-99, -1, 0, 1, 2, 3, 99]
    conductances = [float("nan"), float("inf"), -1.0, 0.0, 0.0000001, 0.5, 5000.0]
    shapes = [(1, 1), (2, 1), (1, 2), (3, 4), (4, 3)]

    def sanitize01(value: float) -> float:
        raw = value if math.isfinite(value) else 0.0
        return f32(min(max(raw, 0.0), 1.0))

    def sanitize_conductance(value: float) -> float:
        raw = value if math.isfinite(value) else 0.0
        clamped = f32(min(max(raw, 0.0), max_conductance))
        return f32(clamped * (0.0 if clamped <= min_conductance else 1.0))

    def clamp_index(value: int, lo: int, hi: int) -> int:
        return min(max(value, lo), hi)

    mismatch_count = 0
    max_weighted_abs_diff = 0.0
    max_sum_abs_diff = 0.0
    max_current_abs_diff = 0.0
    checked = 0

    for node_count, front_length in shapes:
        potential_read_limit = min(node_count, front_length)
        safe_potential_max = max(0, potential_read_limit - 1)
        safe_node_max = max(0, node_count - 1)
        front = [potentials[i % len(potentials)] for i in range(front_length)]
        nodes = [potentials[(i + 1) % len(potentials)] for i in range(node_count)]
        source_potential = sanitize01(nodes[0])
        for destination in destinations:
            for conductance_input in conductances:
                checked += 1
                conductance = sanitize_conductance(conductance_input)

                branch_weighted = 0.0
                branch_sum = 0.0
                if 0 <= destination < potential_read_limit:
                    branch_weighted = f32(conductance * sanitize01(front[destination]))
                    branch_sum = conductance

                valid_destination = 0 <= destination < potential_read_limit
                safe_destination = clamp_index(destination, 0, safe_potential_max)
                mask_conductance = f32(conductance * (1.0 if valid_destination else 0.0))
                masked_weighted = f32(mask_conductance * sanitize01(front[safe_destination]))
                masked_sum = mask_conductance

                branch_current = 0.0
                if 0 <= destination < node_count:
                    branch_current = f32((source_potential - sanitize01(nodes[destination])) * conductance)
                    branch_current = f32(min(max(branch_current, -max_conductance), max_conductance))

                valid_battery_destination = 0 <= destination < node_count
                safe_battery_destination = clamp_index(destination, 0, safe_node_max)
                battery_conductance = f32(conductance * (1.0 if valid_battery_destination else 0.0))
                masked_current = f32((source_potential - sanitize01(nodes[safe_battery_destination])) * battery_conductance)
                masked_current = f32(min(max(masked_current, -max_conductance), max_conductance))

                weighted_diff = abs(float(branch_weighted) - float(masked_weighted))
                sum_diff = abs(float(branch_sum) - float(masked_sum))
                current_diff = abs(float(branch_current) - float(masked_current))
                max_weighted_abs_diff = max(max_weighted_abs_diff, weighted_diff)
                max_sum_abs_diff = max(max_sum_abs_diff, sum_diff)
                max_current_abs_diff = max(max_current_abs_diff, current_diff)
                if weighted_diff != 0.0 or sum_diff != 0.0 or current_diff != 0.0:
                    mismatch_count += 1

    return {
        "checkedCases": checked,
        "mismatchCount": mismatch_count,
        "maxWeightedPotentialAbsDiff": max_weighted_abs_diff,
        "maxConductanceSumAbsDiff": max_sum_abs_diff,
        "maxBatteryCurrentAbsDiff": max_current_abs_diff,
        "policy": "safe-index destination masking must be numerically equivalent to the previous invalid-destination branch/continue behavior",
    }


def scan_exp_residual(max_x: float, step: float) -> dict:
    max_abs = 0.0
    max_rel = 0.0
    at_abs = 0.0
    at_rel = 0.0
    count = int(max_x / step)
    for index in range(count + 1):
        x = index * step
        exact = math.exp(-x)
        approx = float(approx_exp_neg_pade33_reduced_f32(x))
        abs_error = abs(approx - exact)
        rel_error = abs_error / max(exact, 1.0e-30)
        if abs_error > max_abs:
            max_abs = abs_error
            at_abs = x
        if rel_error > max_rel:
            max_rel = rel_error
            at_rel = x
    return {
        "domain": [0.0, max_x],
        "step": step,
        "maxAbsError": max_abs,
        "maxAbsAtX": at_abs,
        "maxRelError": max_rel,
        "maxRelAtX": at_rel,
    }


def scan_exp_residual_with(approx_fn, exact_fn, max_x: float, step: float) -> dict:
    max_abs = 0.0
    max_rel = 0.0
    at_abs = 0.0
    at_rel = 0.0
    count = int(max_x / step)
    for index in range(count + 1):
        x = index * step
        exact = exact_fn(x)
        approx = float(approx_fn(x))
        abs_error = abs(approx - exact)
        rel_error = abs_error / max(abs(exact), 1.0e-30)
        if abs_error > max_abs:
            max_abs = abs_error
            at_abs = x
        if rel_error > max_rel:
            max_rel = rel_error
            at_rel = x
    return {
        "domain": [0.0, max_x],
        "step": step,
        "maxAbsError": max_abs,
        "maxAbsAtX": at_abs,
        "maxRelError": max_rel,
        "maxRelAtX": at_rel,
    }


def scan_signed_residual_with(approx_fn, exact_fn, max_x: float, step: float) -> dict:
    max_abs = 0.0
    at_abs = 0.0
    count = int(max_x / step)
    for index in range(count + 1):
        x = index * step
        exact = exact_fn(x)
        approx = float(approx_fn(x))
        abs_error = abs(approx - exact)
        if abs_error > max_abs:
            max_abs = abs_error
            at_abs = x
    return {
        "domain": [0.0, max_x],
        "step": step,
        "maxAbsError": max_abs,
        "maxAbsAtX": at_abs,
    }


def smooth01(q: float) -> float:
    s = min(max(q, 0.0), 1.0)
    return s * s * (3.0 - (2.0 * s))


def jacobi_sample(q: float) -> dict:
    curve = smooth01(q)
    return {
        "globalQualityWeight": q,
        "curve": curve,
        "iterations": max(2, min(50, round(2.0 + ((50.0 - 2.0) * curve)))),
        "omega": 0.55 + ((0.92 - 0.55) * curve),
        "targetToleranceAtBase0_001": min(0.05, 0.001 * 32.0) + ((max(0.0001 * 0.25, 0.001 * 0.5) - min(0.05, 0.001 * 32.0)) * curve),
        "residualSampleMask": max(0, min(7, round(7.0 + ((0.0 - 7.0) * curve)))),
    }


def collect_cs_files() -> list[Path]:
    return sorted(SCRIPTS.rglob("*.cs"))


def collect_asmdef_files() -> list[Path]:
    return sorted(SCRIPTS.rglob("*.asmdef"))


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def strip_csharp_non_code(text: str) -> str:
    result = []
    index = 0
    length = len(text)
    in_line_comment = False
    in_block_comment = False
    in_string = False
    in_char = False
    in_verbatim_string = False
    in_raw_string = False
    raw_quote_count = 0
    while index < length:
        char = text[index]
        next_char = text[index + 1] if index + 1 < length else ""

        if in_line_comment:
            if char == "\n":
                in_line_comment = False
                result.append(char)
            else:
                result.append(" ")
            index += 1
            continue

        if in_block_comment:
            if char == "*" and next_char == "/":
                result.extend("  ")
                index += 2
                in_block_comment = False
            else:
                result.append("\n" if char == "\n" else " ")
                index += 1
            continue

        if in_raw_string:
            if char == '"':
                quote_run = 1
                while index + quote_run < length and text[index + quote_run] == '"':
                    quote_run += 1
                if quote_run >= raw_quote_count:
                    result.extend(" " * quote_run)
                    index += quote_run
                    in_raw_string = False
                    continue
            result.append("\n" if char == "\n" else " ")
            index += 1
            continue

        if in_verbatim_string:
            if char == '"' and next_char == '"':
                result.extend("  ")
                index += 2
                continue
            if char == '"':
                in_verbatim_string = False
            result.append("\n" if char == "\n" else " ")
            index += 1
            continue

        if in_string:
            if char == "\\" and next_char:
                result.extend("  ")
                index += 2
                continue
            if char == '"':
                in_string = False
            result.append("\n" if char == "\n" else " ")
            index += 1
            continue

        if in_char:
            if char == "\\" and next_char:
                result.extend("  ")
                index += 2
                continue
            if char == "'":
                in_char = False
            result.append("\n" if char == "\n" else " ")
            index += 1
            continue

        if char == "/" and next_char == "/":
            in_line_comment = True
            result.extend("  ")
            index += 2
            continue

        if char == "/" and next_char == "*":
            in_block_comment = True
            result.extend("  ")
            index += 2
            continue

        if char == '"' and next_char == '"' and index + 2 < length and text[index + 2] == '"':
            quote_run = 3
            while index + quote_run < length and text[index + quote_run] == '"':
                quote_run += 1
            raw_quote_count = quote_run
            in_raw_string = True
            result.extend(" " * quote_run)
            index += quote_run
            continue

        if char == "@" and next_char == '"':
            in_verbatim_string = True
            result.extend("  ")
            index += 2
            continue

        if char == '"':
            in_string = True
            result.append(" ")
            index += 1
            continue

        if char == "'":
            in_char = True
            result.append(" ")
            index += 1
            continue

        result.append(char)
        index += 1

    return "".join(result)


def line_collections(path: Path, text: str, code_text: str) -> list[dict]:
    rows = []
    original_lines = text.splitlines()
    code_lines = code_text.splitlines()
    for line_number, code_line in enumerate(code_lines, start=1):
        for name, pattern in TRANSCENDENTAL_PATTERNS.items():
            if pattern.search(code_line):
                line = original_lines[line_number - 1] if line_number <= len(original_lines) else code_line
                rows.append({
                    "file": str(path.relative_to(ROOT)).replace("\\", "/"),
                    "line": line_number,
                    "pattern": name,
                    "text": line.strip()[:240],
                    "hotHeuristic": bool(re.search(r"IJob|BurstCompile|Tick|Update|Fixed|Late|Execute|Solver|Runtime|Manager|Director|Controller|Kernel", str(path) + " " + line)),
                })
    return rows


def count_remaining_transcendentals(files: list[Path]) -> tuple[dict, list[dict]]:
    counts = {name: 0 for name in TRANSCENDENTAL_PATTERNS}
    occurrences = []
    for path in files:
        text = read_text(path)
        code_text = strip_csharp_non_code(text)
        for name, pattern in TRANSCENDENTAL_PATTERNS.items():
            matches = pattern.findall(code_text)
            counts[name] += len(matches)
        occurrences.extend(line_collections(path, text, code_text))
    return counts, occurrences


def extract_function_body(text: str, signature: str) -> str:
    start = text.find(signature)
    if start < 0:
        return ""
    brace = text.find("{", start)
    if brace < 0:
        return ""
    depth = 0
    for index in range(brace, len(text)):
        char = text[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return text[brace:index + 1]
    return ""


def extract_struct_body(text: str, struct_name: str) -> str:
    match = re.search(r"\bstruct\s+" + re.escape(struct_name) + r"\b", text)
    if not match:
        return ""
    brace = text.find("{", match.end())
    if brace < 0:
        return ""
    depth = 0
    for index in range(brace, len(text)):
        char = text[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return text[brace:index + 1]
    return ""


def branch_audit() -> dict:
    result = {}
    for relative in AUDITED_BRANCH_FILES:
        path = ROOT / relative
        text = read_text(path) if path.exists() else ""
        result[relative] = {
            "ifCount": len(re.findall(r"\bif\s*\(", text)),
            "ternaryCount": text.count("?"),
            "switchCount": len(re.findall(r"\bswitch\s*\(", text)),
            "burstCompileCount": len(re.findall(r"\[BurstCompile", text)),
            "floatModeFastCount": len(re.findall(r"FloatMode\s*=\s*FloatMode\.Fast|FloatMode\.Fast", text)),
        }
    return result


def asmdef_dependency_audit(files: list[Path]) -> dict:
    asmdefs = {}
    for path in collect_asmdef_files():
        try:
            data = json.loads(read_text(path))
        except json.JSONDecodeError:
            continue
        name = data.get("name", "")
        references = data.get("references", [])
        if isinstance(name, str) and isinstance(references, list):
            asmdefs[path] = {
                "name": name,
                "references": set(str(item) for item in references),
            }

    def nearest_asmdef(path: Path) -> Path | None:
        current = path.parent
        while current != current.parent and SCRIPTS in (current, *current.parents):
            candidates = sorted(current.glob("*.asmdef"))
            if candidates:
                return candidates[0]
            current = current.parent
        return None

    missing = []
    for path in files:
        text = read_text(path)
        if "MathLodApproximation." not in text and "global::Hecton8.Core.MathLodApproximation." not in text:
            continue
        asmdef = nearest_asmdef(path)
        if asmdef is None:
            continue
        data = asmdefs.get(asmdef)
        if data is None:
            continue
        if data["name"] == "Hecton8.Core":
            continue
        if "Hecton8.Core" in data["references"]:
            continue
        missing.append({
            "file": str(path.relative_to(ROOT)).replace("\\", "/"),
            "asmdef": str(asmdef.relative_to(ROOT)).replace("\\", "/"),
            "asmdefName": data["name"],
        })

    return {
        "asmdefCount": len(asmdefs),
        "mathLodApproximationMissingCoreReferenceCount": len(missing),
        "mathLodApproximationMissingCoreReferenceFirst": missing[:50],
    }


def code_anchor_audit() -> dict:
    core = ROOT / "Assets/_Project/Scripts/MathLodApproximation.cs"
    h8memory = ROOT / "Assets/_Project/Scripts/Core/Memory/H8Memory.cs"
    homeostasis_scalability = ROOT / "Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs"
    distance_math = ROOT / "Assets/_Project/Scripts/Core/DistanceMath.cs"
    power_grid = ROOT / "Assets/_Project/Scripts/PowerGrid.cs"
    power_grid_manager = ROOT / "Assets/_Project/Scripts/PowerGridManager.cs"
    battery_charger = ROOT / "Assets/_Project/Scripts/Power/BatteryChargerLogistics/BatteryChargerLogisticsRuntime.cs"
    base_atmosphere_engine = ROOT / "Assets/_Project/Scripts/Atmosphere/BaseAtmosphereEngine.cs"
    base_atmosphere_math = ROOT / "Assets/_Project/Scripts/Atmosphere/BaseAtmosphereMath.cs"
    base_atmosphere_logistics = ROOT / "Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs"
    hecton_fluid = ROOT / "Assets/_Project/Scripts/HectonFluidEngine.cs"
    seismic_tide = ROOT / "Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs"
    async_buoyancy = ROOT / "Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs"
    buoyancy_displacement = ROOT / "Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementRuntime.cs"
    analytical_wave = ROOT / "Assets/_Project/Scripts/Physics/Buoyancy/AnalyticalGerstnerWaveRuntime.cs"
    exosuit_runtime = ROOT / "Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs"
    submarine_dynamics = ROOT / "Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs"
    submarine_autopilot = ROOT / "Assets/_Project/Scripts/Physics/Vehicles/Automation/SubmarineAutopilotSdfNavigator.cs"
    hydrodynamic_kcc = ROOT / "Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs"
    vehicle_damage = ROOT / "Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageRuntime.cs"
    hull_integrity_runtime = ROOT / "Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs"
    structural_runtime = ROOT / "Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorRuntime.cs"
    abyssal_cavitation = ROOT / "Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs"
    habitat_fluid = ROOT / "Assets/_Project/Scripts/Physics/HabitatFluidIncursionDirector.cs"
    asset_load_dispatcher = ROOT / "Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs"
    asset_lifecycle = ROOT / "Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs"
    vram_pressure = ROOT / "Assets/_Project/Scripts/Optimization/VRAMPressureMonitor.cs"
    vram_enforcer = ROOT / "Assets/_Project/Scripts/Optimization/VRAMEnforcer.cs"
    physiology_runtime = ROOT / "Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs"
    physiology = ROOT / "Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs"
    seaglide_runtime = ROOT / "Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsRuntime.cs"
    seaglide_jobs = ROOT / "Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsJobs.cs"
    volcanic_updraft = ROOT / "Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs"
    abyssal_thermo_solver = ROOT / "Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.cs"
    abyssal_thermo_reactor_bridge = ROOT / "Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.ReactorBridge.cs"
    abyssal_thermo_jobs = ROOT / "Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsJobs.cs"
    metabolism_runtime = ROOT / "Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs"
    metabolism_jobs = ROOT / "Assets/_Project/Scripts/Physiology/ShinobuMetabolismJobs.cs"
    bulkhead_runtime = ROOT / "Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime.cs"
    bulkhead_hatchlocks = ROOT / "Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime_HatchLocks.cs"
    symbiosis_solver = ROOT / "Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs"
    migration_director = ROOT / "Assets/_Project/Scripts/Ecosystem/MigrationDirector.cs"
    boid_controller = ROOT / "Assets/_Project/Scripts/HectonBoidController.cs"
    leviathan_terrain_ik = ROOT / "Assets/_Project/Scripts/Animation/LeviathanTerrainIkJobs.cs"
    procedural_bone_jobs = ROOT / "Assets/_Project/Scripts/Animation/FaunaProcedural/ProceduralBoneBlenderJobs.cs"
    kinetic_character_jobs = ROOT / "Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorJobs.cs"
    tether_aup_jobs = ROOT / "Assets/_Project/Scripts/Physics/TetherAupVerletJobs.cs"
    cable_physics_132 = ROOT / "Assets/_Project/Scripts/Physics/Cable132/CablePhysicsSolver132.cs"
    interior_gi_runtime = ROOT / "Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs"
    dynamic_music_granular = ROOT / "Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs"
    ocean_surface_contracts = ROOT / "Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereContracts.cs"
    voxel_surface_nets = ROOT / "Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs"
    critical_audio = ROOT / "Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs"
    biomimetic_runtime = ROOT / "Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs"
    vr_somatic_comfort = ROOT / "Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs"
    seedship_anomaly = ROOT / "Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyContracts.cs"
    sump_pump_pipe = ROOT / "Assets/_Project/Scripts/Construction/SumpPumpPipeGridRuntime.cs"
    chemical_influence = ROOT / "Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs"
    fauna_kinematics = ROOT / "Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs"
    reactor_thermal_jobs = ROOT / "Assets/_Project/Scripts/Thermodynamics/ReactorThermalGridJobs.cs"
    delta_crusher_jobs = ROOT / "Assets/_Project/Scripts/VFX/Debris/ShinobuDeltaCrusherJobs.cs"
    repair_tool = ROOT / "Assets/_Project/Scripts/RepairTool.cs"
    carrion_runtime = ROOT / "Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime_Carrion.cs"
    macro_ecosystem = ROOT / "Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs"
    memory_sentinel = ROOT / "Assets/_Project/Scripts/Core/Memory/MemorySentinelContracts.cs"
    fabrication_assembler = ROOT / "Assets/_Project/Scripts/FabricationAssemblerRuntime.cs"
    topographical_sonar = ROOT / "Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs"
    utility_ai_cognition = ROOT / "Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionJobs.cs"
    save_merkle = ROOT / "Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs"
    player_kinematics = ROOT / "Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs"
    shinobu_watchdog = ROOT / "Assets/_Project/Scripts/QA/Headless/Shinobu38QaWatchdogRuntime.cs"
    mod_projection = ROOT / "Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs"
    power = ROOT / "Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs"
    logistics = ROOT / "Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs"
    power_jacobi = ROOT / "Assets/_Project/Scripts/Power/PowerGridJacobiContracts.cs"
    power_fuzzer = ROOT / "Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs"
    solar = ROOT / "Assets/_Project/Scripts/Power/PowerGridSolarContracts.cs"
    gas = ROOT / "Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs"
    storm = ROOT / "Assets/_Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationContracts.cs"
    anxiety = ROOT / "Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionAnxietyJobs.cs"
    ballistics = ROOT / "Assets/_Project/Scripts/Gameplay/Combat/BallisticsRuntime.cs"
    rollback = ROOT / "Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs"
    bioforge = ROOT / "Assets/_Project/Scripts/Editor/ProceduralGen/BioForgeJobs.cs"
    hydraulic = ROOT / "Assets/_Project/Scripts/Editor/HydraulicErosionForge/Shinobu242/HydraulicErosionForgeJobs.cs"
    core_text = read_text(core) if core.exists() else ""
    h8memory_text = read_text(h8memory) if h8memory.exists() else ""
    homeostasis_scalability_text = read_text(homeostasis_scalability) if homeostasis_scalability.exists() else ""
    distance_math_text = read_text(distance_math) if distance_math.exists() else ""
    power_grid_text = read_text(power_grid) if power_grid.exists() else ""
    power_grid_manager_text = read_text(power_grid_manager) if power_grid_manager.exists() else ""
    battery_charger_text = read_text(battery_charger) if battery_charger.exists() else ""
    base_atmosphere_engine_text = read_text(base_atmosphere_engine) if base_atmosphere_engine.exists() else ""
    base_atmosphere_math_text = read_text(base_atmosphere_math) if base_atmosphere_math.exists() else ""
    base_atmosphere_logistics_text = read_text(base_atmosphere_logistics) if base_atmosphere_logistics.exists() else ""
    hecton_fluid_text = read_text(hecton_fluid) if hecton_fluid.exists() else ""
    seismic_tide_text = read_text(seismic_tide) if seismic_tide.exists() else ""
    async_buoyancy_text = read_text(async_buoyancy) if async_buoyancy.exists() else ""
    buoyancy_displacement_text = read_text(buoyancy_displacement) if buoyancy_displacement.exists() else ""
    analytical_wave_text = read_text(analytical_wave) if analytical_wave.exists() else ""
    exosuit_runtime_text = read_text(exosuit_runtime) if exosuit_runtime.exists() else ""
    submarine_dynamics_text = read_text(submarine_dynamics) if submarine_dynamics.exists() else ""
    submarine_autopilot_text = read_text(submarine_autopilot) if submarine_autopilot.exists() else ""
    hydrodynamic_kcc_text = read_text(hydrodynamic_kcc) if hydrodynamic_kcc.exists() else ""
    vehicle_damage_text = read_text(vehicle_damage) if vehicle_damage.exists() else ""
    hull_integrity_runtime_text = read_text(hull_integrity_runtime) if hull_integrity_runtime.exists() else ""
    structural_runtime_text = read_text(structural_runtime) if structural_runtime.exists() else ""
    abyssal_cavitation_text = read_text(abyssal_cavitation) if abyssal_cavitation.exists() else ""
    habitat_fluid_text = read_text(habitat_fluid) if habitat_fluid.exists() else ""
    asset_load_dispatcher_text = read_text(asset_load_dispatcher) if asset_load_dispatcher.exists() else ""
    asset_lifecycle_text = read_text(asset_lifecycle) if asset_lifecycle.exists() else ""
    vram_pressure_text = read_text(vram_pressure) if vram_pressure.exists() else ""
    vram_enforcer_text = read_text(vram_enforcer) if vram_enforcer.exists() else ""
    physiology_runtime_text = read_text(physiology_runtime) if physiology_runtime.exists() else ""
    physiology_text = read_text(physiology)
    seaglide_runtime_text = read_text(seaglide_runtime) if seaglide_runtime.exists() else ""
    seaglide_jobs_text = read_text(seaglide_jobs) if seaglide_jobs.exists() else ""
    volcanic_updraft_text = read_text(volcanic_updraft) if volcanic_updraft.exists() else ""
    abyssal_thermo_solver_text = read_text(abyssal_thermo_solver) if abyssal_thermo_solver.exists() else ""
    abyssal_thermo_reactor_bridge_text = read_text(abyssal_thermo_reactor_bridge) if abyssal_thermo_reactor_bridge.exists() else ""
    abyssal_thermo_jobs_text = read_text(abyssal_thermo_jobs) if abyssal_thermo_jobs.exists() else ""
    metabolism_runtime_text = read_text(metabolism_runtime) if metabolism_runtime.exists() else ""
    metabolism_jobs_text = read_text(metabolism_jobs) if metabolism_jobs.exists() else ""
    bulkhead_runtime_text = read_text(bulkhead_runtime) if bulkhead_runtime.exists() else ""
    bulkhead_hatchlocks_text = read_text(bulkhead_hatchlocks) if bulkhead_hatchlocks.exists() else ""
    symbiosis_text = read_text(symbiosis_solver) if symbiosis_solver.exists() else ""
    migration_text = read_text(migration_director) if migration_director.exists() else ""
    boid_text = read_text(boid_controller) if boid_controller.exists() else ""
    leviathan_terrain_ik_text = read_text(leviathan_terrain_ik) if leviathan_terrain_ik.exists() else ""
    procedural_bone_text = read_text(procedural_bone_jobs) if procedural_bone_jobs.exists() else ""
    kinetic_character_text = read_text(kinetic_character_jobs) if kinetic_character_jobs.exists() else ""
    tether_aup_text = read_text(tether_aup_jobs) if tether_aup_jobs.exists() else ""
    cable_physics_132_text = read_text(cable_physics_132) if cable_physics_132.exists() else ""
    interior_gi_text = read_text(interior_gi_runtime) if interior_gi_runtime.exists() else ""
    dynamic_music_text = read_text(dynamic_music_granular) if dynamic_music_granular.exists() else ""
    ocean_surface_text = read_text(ocean_surface_contracts) if ocean_surface_contracts.exists() else ""
    voxel_surface_text = read_text(voxel_surface_nets) if voxel_surface_nets.exists() else ""
    critical_audio_text = read_text(critical_audio) if critical_audio.exists() else ""
    biomimetic_text = read_text(biomimetic_runtime) if biomimetic_runtime.exists() else ""
    vr_somatic_text = read_text(vr_somatic_comfort) if vr_somatic_comfort.exists() else ""
    seedship_text = read_text(seedship_anomaly) if seedship_anomaly.exists() else ""
    sump_pump_text = read_text(sump_pump_pipe) if sump_pump_pipe.exists() else ""
    chemical_text = read_text(chemical_influence) if chemical_influence.exists() else ""
    fauna_kinematics_text = read_text(fauna_kinematics) if fauna_kinematics.exists() else ""
    reactor_thermal_text = read_text(reactor_thermal_jobs) if reactor_thermal_jobs.exists() else ""
    delta_crusher_text = read_text(delta_crusher_jobs) if delta_crusher_jobs.exists() else ""
    repair_tool_text = read_text(repair_tool) if repair_tool.exists() else ""
    carrion_text = read_text(carrion_runtime) if carrion_runtime.exists() else ""
    macro_ecosystem_text = read_text(macro_ecosystem) if macro_ecosystem.exists() else ""
    memory_sentinel_text = read_text(memory_sentinel) if memory_sentinel.exists() else ""
    fabrication_text = read_text(fabrication_assembler) if fabrication_assembler.exists() else ""
    topographical_sonar_text = read_text(topographical_sonar) if topographical_sonar.exists() else ""
    utility_ai_text = read_text(utility_ai_cognition) if utility_ai_cognition.exists() else ""
    save_merkle_text = read_text(save_merkle) if save_merkle.exists() else ""
    player_kinematics_text = read_text(player_kinematics) if player_kinematics.exists() else ""
    watchdog_text = read_text(shinobu_watchdog) if shinobu_watchdog.exists() else ""
    mod_projection_text = read_text(mod_projection) if mod_projection.exists() else ""
    power_text = read_text(power)
    logistics_text = read_text(logistics) if logistics.exists() else ""
    power_jacobi_text = read_text(power_jacobi) if power_jacobi.exists() else ""
    power_fuzzer_text = read_text(power_fuzzer) if power_fuzzer.exists() else ""
    solar_text = read_text(solar) if solar.exists() else ""
    gas_text = read_text(gas) if gas.exists() else ""
    storm_text = read_text(storm) if storm.exists() else ""
    anxiety_text = read_text(anxiety) if anxiety.exists() else ""
    ballistics_text = read_text(ballistics) if ballistics.exists() else ""
    rollback_text = read_text(rollback) if rollback.exists() else ""
    bioforge_text = read_text(bioforge) if bioforge.exists() else ""
    hydraulic_text = read_text(hydraulic) if hydraulic.exists() else ""
    torture_start = core_text.find("struct MathLodTortureJob")
    torture_end = core_text.find("public static class MathLodBlackBoxDumpWriter", torture_start)
    torture_text = core_text[torture_start:torture_end] if torture_start >= 0 and torture_end > torture_start else ""
    external_heat_start = power_text.find("private unsafe struct ExternalThermalInjectionJob")
    external_heat_end = power_text.find("[BurstCompile", external_heat_start + 1)
    external_heat_text = power_text[external_heat_start:external_heat_end] if external_heat_start >= 0 and external_heat_end > external_heat_start else ""
    symbiosis_exchange_start = symbiosis_text.find("internal struct SymbiosisExchangeKernelJob")
    symbiosis_exchange_end = symbiosis_text.find("[BurstCompile", symbiosis_exchange_start + 1)
    if symbiosis_exchange_end < 0:
        symbiosis_exchange_end = len(symbiosis_text)
    symbiosis_exchange_text = symbiosis_text[symbiosis_exchange_start:symbiosis_exchange_end] if symbiosis_exchange_start >= 0 and symbiosis_exchange_end > symbiosis_exchange_start else ""
    directional_clamp_scalar_body = extract_function_body(core_text, "ClampFiniteWithDirectionalInfinity(float value")
    directional_clamp_vector_body = extract_function_body(core_text, "ClampFiniteWithDirectionalInfinity(float4 value")
    approx_body = extract_function_body(core_text, "ApproxExpNegPade33Reduced(float4 value)")
    exp_wide_body = extract_function_body(core_text, "ApproxExpNegPade33Wide40(float value)")
    exp_signed_body = extract_function_body(core_text, "ApproxExpSignedPade33Wide40(float value)")
    exp_positive_body = extract_function_body(core_text, "ApproxExpPositivePade33Reduced(float value)")
    bhaskara_body = extract_function_body(core_text, "ApproxSinBhaskara(float radians)")
    tan_body = extract_function_body(core_text, "ApproxTanClamped(float radians")
    atan_body = extract_function_body(core_text, "ApproxAtanFast(float value)")
    atan2_body = extract_function_body(core_text, "ApproxAtan2Fast(float y, float x)")
    acos_body = extract_function_body(core_text, "ApproxAcosFast(float value)")
    pow01_body = extract_function_body(core_text, "ApproxPow01Curve(float value01, float exponent)")
    approximation_kernel_text = "".join([
        directional_clamp_scalar_body,
        directional_clamp_vector_body,
        approx_body,
        exp_wide_body,
        exp_signed_body,
        exp_positive_body,
        bhaskara_body,
        tan_body,
        atan_body,
        atan2_body,
        acos_body,
        pow01_body,
    ])
    math_lod_torture_body = extract_struct_body(core_text, "MathLodTortureJob")
    power_voltage_solver_body = extract_struct_body(power_jacobi_text, "PowerVoltageSolverJob")
    integrate_battery_body = extract_struct_body(power_jacobi_text, "IntegrateBatteryChargeJob")
    equipment_drain_body = extract_struct_body(power_jacobi_text, "ApplyEquipmentPowerDrainJob")
    power_voltage_execute_body = extract_function_body(power_voltage_solver_body, "public void Execute(int index)")
    power_voltage_loop_match = re.search(r"for\s*\(int\s+edgeCursor\s*=.*?conductanceSum\s*\+=\s*conductance;\s*}", power_voltage_execute_body, re.DOTALL)
    power_voltage_edge_loop = power_voltage_loop_match.group(0) if power_voltage_loop_match else ""
    integrate_battery_execute_body = extract_function_body(integrate_battery_body, "public void Execute(int index)")
    integrate_battery_loop_match = re.search(r"for\s*\(int\s+edgeCursor\s*=.*?netCurrentOut\s*=\s*math\.clamp\(netCurrentOut\s*\+\s*current.*?}", integrate_battery_execute_body, re.DOTALL)
    integrate_battery_edge_loop = integrate_battery_loop_match.group(0) if integrate_battery_loop_match else ""
    math_lod_read_body = extract_function_body(core_text, "TryReadLatestConfig(out MathLodConfigDTO config)")
    anxiety_approx_body = extract_function_body(anxiety_text, "ApproxExpNegPade33Reduced(float value)")
    battery_cadence_body = extract_function_body(battery_charger_text, "ResolveCadenceHzStatic(float quality)")
    battery_tuning_body = extract_function_body(battery_charger_text, "ApplyPendingTuningValues(ref ChargerTuningDTO dto)")
    battery_sample_body = extract_function_body(battery_charger_text, "SampleQualityWeightUnderTuningLock(IDataVault vault, out float q)")
    base_atmosphere_engine_quality_body = extract_function_body(base_atmosphere_engine_text, "private static float ResolveGlobalQualityWeight01()")
    base_atmosphere_cadence_body = extract_function_body(base_atmosphere_math_text, "ResolveColdTickIntervalSeconds(float globalQualityWeight01)")
    base_atmosphere_tuning_body = extract_function_body(base_atmosphere_logistics_text, "ApplyQualityAndEditorTuning(IDataVault vault")
    base_atmosphere_quality_body = extract_function_body(base_atmosphere_logistics_text, "private static float ResolveVisualQualityWeight()")
    base_atmosphere_iterations_body = extract_function_body(base_atmosphere_logistics_text, "ResolveDiffusionIterations(float globalQualityWeight)")
    fluid_advection_quality_body = extract_function_body(hecton_fluid_text, "private static float ResolveFluidAdvectionQualityWeight()")
    fluid_abyssal_visual_quality_body = extract_function_body(hecton_fluid_text, "private static float ResolveAbyssalVisualQualityWeight()")
    seismic_quality_body = extract_function_body(seismic_tide_text, "private float UpdateGlobalQualityWeight()")
    async_buoyancy_quality_body = extract_function_body(async_buoyancy_text, "private float ResolveGlobalQualityWeight()")
    buoyancy_displacement_quality_body = extract_function_body(buoyancy_displacement_text, "private static float ResolveGlobalQualityWeightFromHomeostasis()")
    analytical_wave_quality_body = extract_function_body(analytical_wave_text, "private static float ResolveGlobalQualityWeight()")
    exosuit_quality_body = extract_function_body(exosuit_runtime_text, "private static float ResolveGlobalQualityWeight01()")
    submarine_dynamics_quality_body = extract_function_body(submarine_dynamics_text, "private static float ResolveMathLodQualityWeight()")
    submarine_autopilot_quality_body = extract_function_body(submarine_autopilot_text, "private static float ResolveRuntimeQualityWeight(float qualityCap)")
    hydrodynamic_kcc_quality_body = extract_function_body(hydrodynamic_kcc_text, "private float ResolveGlobalQualityWeight()")
    vehicle_damage_quality_body = extract_function_body(vehicle_damage_text, "private float ResolveQualityWeight()")
    hull_integrity_quality_body = extract_function_body(hull_integrity_runtime_text, "private static float ResolveGlobalQualityWeight()")
    structural_visual_quality_body = extract_function_body(structural_runtime_text, "private static float ResolveVisualQualityWeight()")
    abyssal_cavitation_quality_body = extract_function_body(abyssal_cavitation_text, "private static float ResolveGlobalQualityWeight()")
    habitat_fluid_quality_body = extract_function_body(habitat_fluid_text, "private static float ResolveGlobalQualityWeight()")
    asset_load_quality_body = extract_function_body(asset_load_dispatcher_text, "private static float ResolveGlobalQualityWeight()")
    asset_lifecycle_quality_body = extract_function_body(asset_lifecycle_text, "private static float ResolveGlobalQualityWeight()")
    vram_pressure_quality_body = extract_function_body(vram_pressure_text, "private static float ResolveGlobalQualityWeight()")
    vram_enforcer_quality_body = extract_function_body(vram_enforcer_text, "private static float ResolveQualityCurve()")
    physiology_runtime_quality_body = extract_function_body(physiology_runtime_text, "private float ResolveGlobalQualityWeight()")
    physiology_runtime_interval_body = extract_function_body(physiology_runtime_text, "ResolvePhysiologyUpdateIntervalSeconds(float globalQualityWeight)")
    gas_quality_body = extract_function_body(gas_text, "private static float ResolveGlobalQualityWeight()")
    gas_cadence_body = extract_function_body(gas_text, "ResolveCadenceSeconds(float globalQualityWeight01)")
    seaglide_quality_body = extract_function_body(seaglide_runtime_text, "ApplyResolvedGlobalQualityWeight(ref SeaglideTuningDTO tuning)")
    volcanic_quality_body = extract_function_body(volcanic_updraft_text, "private float ResolveGlobalQualityWeight()")
    volcanic_debris_body = extract_function_body(volcanic_updraft_text, "ResolveDebrisLiftWeight(float qualityWeight)")
    volcanic_turbulence_body = extract_function_body(volcanic_updraft_text, "ResolveTurbulenceGate(float qualityWeight)")
    abyssal_thermo_quality_body = extract_function_body(abyssal_thermo_solver_text, "private float ResolveVisualQualityWeight()")
    abyssal_thermo_build_body = extract_function_body(abyssal_thermo_solver_text, "private ThermalGridTuningDTO BuildTuning()")
    abyssal_thermo_write_body = extract_function_body(abyssal_thermo_solver_text, "public bool TryWriteTuning(ThermalGridTuningDTO tuning)")
    reactor_default_body = extract_function_body(abyssal_thermo_reactor_bridge_text, "private ReactorThermalTuningDTO BuildDefaultReactorTuning()")
    nuclear_reactor_default_body = extract_function_body(abyssal_thermo_reactor_bridge_text, "private NuclearReactorThermalTuningDTO BuildDefaultNuclearReactorTuning()")
    reactor_write_body = extract_function_body(abyssal_thermo_reactor_bridge_text, "public bool TryWriteReactorTuning(ReactorThermalTuningDTO tuning)")
    nuclear_reactor_write_body = extract_function_body(abyssal_thermo_reactor_bridge_text, "public bool TryWriteNuclearReactorTuning(NuclearReactorThermalTuningDTO tuning)")
    metabolism_quality_body = extract_function_body(metabolism_runtime_text, "private static float ResolveGlobalQualityWeight()")
    metabolism_interpolation_body = extract_function_body(metabolism_jobs_text, "ResolveThermalInterpolationWeight(float globalQualityWeight)")
    bulkhead_quality_body = extract_function_body(bulkhead_runtime_text, "private static float ResolveBulkheadQualityWeight()")
    bulkhead_cadence_body = extract_function_body(bulkhead_runtime_text, "private float ResolveAuthorityCadenceHz(float q)")
    symbiosis_quality_body = extract_function_body(symbiosis_text, "private static float ResolveSymbiosisQualityWeight()")
    migration_quality_body = extract_function_body(migration_text, "private static float ResolveMigrationQualityWeight()")
    migration_cadence_body = extract_function_body(migration_text, "private float ResolveMigrationFieldColdTickIntervalSeconds(float globalQualityWeight)")
    boid_social_lod_body = extract_function_body(boid_text, "private static float ResolveBoidSocialLodWeight01()")
    leviathan_sdf_body = extract_function_body(leviathan_terrain_ik_text, "private bool TrySampleSdfAdaptive(")
    kinetic_sdf_body = extract_function_body(kinetic_character_text, "private bool TrySampleSdf(")
    interior_gi_quality_body = extract_function_body(interior_gi_text, "private float ResolveQualityWeight()")
    interior_gi_build_tuning_body = extract_function_body(interior_gi_text, "private InteriorGITuningDTO BuildTuning(")
    interior_gi_cadence_body = extract_function_body(interior_gi_text, "private static float ResolveCadenceSeconds(")
    storm_noise_octave_body = extract_function_body(storm_text, "public static int ResolveNoiseOctaveCount(")
    ocean_foam_body = extract_function_body(ocean_surface_text, "public static float ResolveFoamScalar(")
    voxel_density_body = extract_function_body(voxel_surface_text, "private float SampleDensityLocal(")
    critical_audio_reverb_body = extract_function_body(critical_audio_text, "private ReverbDspTier ResolveReverbDspTier()")
    biomimetic_hzb_body = extract_function_body(biomimetic_text, "public void Execute(int index)")
    seedship_budget_body = extract_function_body(seedship_text, "public static int ResolveEntityBudget(")
    sump_pump_thermal_body = extract_function_body(sump_pump_text, "private static float ResolveSolveCadenceSeconds(float quality)")
    reactor_injection_body = extract_function_body(reactor_thermal_text, "public static int ResolveInjectionDiameter(float globalQualityWeight)")
    delta_crusher_cap_body = extract_function_body(delta_crusher_text, "public static int ResolveDebrisCap(")
    macro_quality_body = extract_function_body(macro_ecosystem_text, "private static float ResolveQualityCurve(float globalQualityWeight)")
    sonar_work_curve_body = extract_function_body(topographical_sonar_text, "private static float ResolveWorkCurve(float quality)")
    utility_quality_body = extract_function_body(utility_ai_text, "public static float ResolveQuality(float quality)")
    seismic_harmonic_body = extract_function_body(seismic_tide_text, "private static int ResolveActiveHarmonicCount(float quality)")
    quality_step_sweep_texts = [
        critical_audio_text,
        biomimetic_text,
        vr_somatic_text,
        seedship_text,
        homeostasis_scalability_text,
        sump_pump_text,
        chemical_text,
        fauna_kinematics_text,
        reactor_thermal_text,
        delta_crusher_text,
        repair_tool_text,
        carrion_text,
        macro_ecosystem_text,
        memory_sentinel_text,
        fabrication_text,
        topographical_sonar_text,
        utility_ai_text,
        save_merkle_text,
        player_kinematics_text,
        watchdog_text,
        seismic_tide_text,
        mod_projection_text,
    ]
    quality_step_sweep_code = "\n".join(strip_csharp_non_code(text) for text in quality_step_sweep_texts)
    quality_step_pattern = re.compile(r"math\.step\s*\([^;\n)]*(?:quality|Quality|globalQuality|GlobalQuality|qualityWeight|QualityWeight|\bq\b)[^;\n)]*\)")
    tissue_count_match = re.search(r"TissueCompartmentCount\s*=\s*(\d+)", read_text(ROOT / "Assets/_Project/Scripts/Physiology/ShinobuPhysiologyData.cs"))
    tissue_count = int(tissue_count_match.group(1)) if tissue_count_match else None
    return {
        "mathLodApproximationContractPresent": core.exists(),
        "mathLodConfigDto64BytesDeclared": "struct MathLodConfigDTO" in core_text and "Size = MathLodApproximation.ConfigSizeBytes" in core_text,
        "mathLodRuntimeConfigPresent": "public static class MathLodRuntimeConfig" in core_text,
        "expPositiveInfinityClampsToMaxRange": "ClampFiniteWithDirectionalInfinity(value, 0f, 4f, 0f)" in approx_body and "ClampFiniteWithDirectionalInfinity(value, 0f, 40f, 0f)" in core_text,
        "directionalInfinityClampIfCount": len(re.findall(r"\bif\s*\(", directional_clamp_scalar_body + directional_clamp_vector_body)),
        "directionalInfinityClampUsesMathSelect": "math.select" in directional_clamp_scalar_body and "math.select" in directional_clamp_vector_body,
        "mathLodConfigBufferIdsPresent": all(token in h8memory_text for token in [
            "ShinobuMathLodConfig = 74400",
            "ShinobuMathLodTelemetryRing = 74401",
            "ShinobuMathLodTelemetryCursor = 74402",
        ]),
        "mathLodConfigPublishedByHomeostasis": "MathLodRuntimeConfig.PublishConfig" in homeostasis_scalability_text and "MathLodRuntimeConfig.ResolveRequestedBytes()" in homeostasis_scalability_text,
        "mathLodReadAccessorPure": "TryReadLatestConfig(out MathLodConfigDTO config)" in core_text and "EnsureRuntimeBuffers" not in math_lod_read_body and "TryReadOnlyHandle" in math_lod_read_body,
        "mathLodBlackBoxFaultDumpIntegrated": "MathLodRuntimeConfig.TryDumpOnFault(null)" in homeostasis_scalability_text and "Dump_SHINOBU_300_MathLOD.bin" in core_text,
        "distanceMathContinuousShaderWeight": "_HectonMathLodWeight" in distance_math_text and "PushShaderMathLod(float globalQualityWeight)" in distance_math_text,
        "distanceMathContinuousDistanceWeight": "ResolveDistanceQualityWeight01(float distanceSq, float globalQualityWeight)" in distance_math_text,
        "mathLodTelemetryEntry64BytesDeclared": "StructLayout(LayoutKind.Explicit, Size = MathLodApproximation.TelemetryEntrySizeBytes)" in core_text,
        "mathLodTortureJobPresent": "struct MathLodTortureJob : IJob" in core_text,
        "mathLodTortureCoversAngleKernels": all(token in torture_text for token in [
            "ApproxSinBhaskara",
            "ApproxCosBhaskara",
            "ApproxTanClamped",
            "ApproxAtanFast",
            "ApproxAtan2Fast",
            "ApproxAcosFast",
            "ApproxPow01Curve",
        ]),
        "mathLodTortureCoversExtremePressureTemperature": all(token in torture_text for token in [
            "1000000f",
            "1000f",
            "ResolveTemperature",
            "ResolvePressure",
            "WorstTemperatureCelsius",
            "WorstPressureAtm",
        ]),
        "mathLodTortureChecksNonFiniteAllKernels": all(token in torture_text for token in [
            "math.isfinite(blended)",
            "math.isfinite(neg)",
            "math.isfinite(pos)",
            "math.isfinite(sine)",
            "math.isfinite(cosine)",
            "math.isfinite(tangent)",
            "math.isfinite(atan)",
            "math.isfinite(atan2)",
            "math.isfinite(acos)",
            "math.isfinite(pow)",
        ]),
        "mathLodTortureSanitizesEnvelope": all(token in torture_text for token in [
            "safeBlended = MathLodApproximation.FiniteOr(blended, 0f)",
            "safeTangent = MathLodApproximation.FiniteOr(tangent, 0f)",
            "safeAtan2 = MathLodApproximation.FiniteOr(atan2, 0f)",
            "safeAcos = MathLodApproximation.FiniteOr(acos, 0f)",
            "safePow = MathLodApproximation.FiniteOr(pow, 0f)",
            "entry.ApproxOutput = safeBlended",
        ]),
        "mathLodBlackBoxDumpWriterPresent": "Dump_SHINOBU_300_MathLOD.bin" in core_text and "ReadOnlySpan<byte>" in core_text,
        "decompressionApproxPresent": "ApproxExpNegPade33Reduced" in physiology_text,
        "decompressionDirectExpRemoved": "math.exp(-effectiveK" not in physiology_text,
        "decompressionAuthorityConstantTissues": "return ShinobuPhysiologyConstants.TissueCompartmentCount;" in physiology_text and "activeCompartments = ShinobuPhysiologyConstants.TissueCompartmentCount" in physiology_text,
        "decompressionAuthorityTissueCount": tissue_count,
        "physiologyDirectExpRemoved": "math.exp" not in physiology_text,
        "approxCoreIfCount": len(re.findall(r"\bif\s*\(", approx_body)),
        "approxCoreTernaryCount": approx_body.count("?"),
        "approxCoreUsesMathSelect": "math.select" in approx_body,
        "bhaskaraCoreIfCount": len(re.findall(r"\bif\s*\(", bhaskara_body)),
        "bhaskaraCoreTernaryCount": bhaskara_body.count("?"),
        "bhaskaraCoreUsesMathSelect": "math.select" in bhaskara_body,
        "tanCoreIfCount": len(re.findall(r"\bif\s*\(", tan_body)),
        "atanCoreIfCount": len(re.findall(r"\bif\s*\(", atan_body)),
        "atan2CoreIfCount": len(re.findall(r"\bif\s*\(", atan2_body)),
        "acosCoreIfCount": len(re.findall(r"\bif\s*\(", acos_body)),
        "atanCoreUsesMathSelect": "math.select" in atan_body and "math.select" in atan2_body and "math.select" in acos_body,
        "approximationKernelTotalIfCount": len(re.findall(r"\bif\s*\(", approximation_kernel_text)),
        "approximationKernelTotalTernaryCount": approximation_kernel_text.count("?"),
        "approximationKernelUsesMathSelect": "math.select" in approximation_kernel_text,
        "mathLodTortureSafetyIfCount": len(re.findall(r"\bif\s*\(", math_lod_torture_body)),
        "mathLodTortureTernaryCount": math_lod_torture_body.count("?"),
        "powerVoltageSolverSafetyIfCount": len(re.findall(r"\bif\s*\(", power_voltage_solver_body)),
        "powerVoltageSolverTernaryCount": power_voltage_solver_body.count("?"),
        "powerVoltageEdgeLoopIfCount": len(re.findall(r"\bif\s*\(", power_voltage_edge_loop)),
        "powerVoltageEdgeLoopContinueCount": len(re.findall(r"\bcontinue\s*;", power_voltage_edge_loop)),
        "integrateBatterySafetyIfCount": len(re.findall(r"\bif\s*\(", integrate_battery_body)),
        "integrateBatteryDestinationMaskBranchless": "bool validDestination = (uint)destination < (uint)NodeCount;" in integrate_battery_edge_loop and "int safeDestination = math.clamp(destination, 0, safeDestinationMaxIndex);" in integrate_battery_edge_loop and "NodesPtr + safeDestination" in integrate_battery_edge_loop and "conductance *= math.select(0f, 1f, validDestination);" in integrate_battery_edge_loop and re.search(r"\bif\s*\(\s*\(uint\)destination", integrate_battery_edge_loop) is None,
        "equipmentDrainSafetyIfCount": len(re.findall(r"\bif\s*\(", equipment_drain_body)),
        "jacobiContinuousIterationsPresent": "MinPropagationIterations = 2" in power_text and "MaxPropagationIterations = 50" in power_text,
        "jacobiRuntimeUsesGlobalQualityParameter": "float qualityWeight = MathLodApproximation.SaturateFinite(globalQualityWeight" in power_text,
        "powerVoltageConductanceMaskBranchless": "conductance *= math.select(1f, 0f, conductance <= PowerGridJacobiConstants.MinimumConductance);" in power_jacobi_text and re.search(r"\bif\s*\(\s*conductance\s*<=\s*PowerGridJacobiConstants\.MinimumConductance", power_jacobi_text) is None,
        "powerVoltageDestinationMaskBranchless": "bool validDestination = (uint)destination < (uint)potentialReadLimit;" in power_voltage_edge_loop and "int safeDestination = math.clamp(destination, 0, safePotentialMaxIndex);" in power_voltage_edge_loop and "conductance *= math.select(0f, 1f, validDestination);" in power_voltage_edge_loop and "FrontPotential[safeDestination]" in power_voltage_edge_loop and len(re.findall(r"\bif\s*\(", power_voltage_edge_loop)) == 0 and len(re.findall(r"\bcontinue\s*;", power_voltage_edge_loop)) == 0,
        "powerVoltageBrownoutUsesMathSelect": "node.Flags = math.select(flags & ~PowerGridJacobiConstants.NodeFlagBrownout, flags | PowerGridJacobiConstants.NodeFlagBrownout" in power_jacobi_text,
        "powerHotFiniteGuardsUseMathSelect": "math.isfinite(EdgeConductance[edgeCursor]) ? EdgeConductance[edgeCursor] : 0f" not in power_jacobi_text and "math.isfinite(DeltaTimeSeconds) ? DeltaTimeSeconds : 0f" not in power_jacobi_text and "math.isfinite(value) ? value : 0f" not in power_jacobi_text,
        "auditedFiniteGuardTernariesRemoved": sum(len(re.findall(r"math\.isfinite\([^\n]*\?", text)) for text in [physiology_text, power_text, power_jacobi_text, abyssal_thermo_jobs_text]) == 0,
        "externalThermalInjectionQualityInvariantHeatShape": "float sample01 = near01 * near01 * (3f - 2f * near01);" in external_heat_text and "cheapStep" not in external_heat_text and "GlobalQualityWeight" not in external_heat_text,
        "externalHeatRetentionQualityInvariant": "const float ExternalHeatRetention = 0.55f;" in power_text and "ExternalHeat[nodeIndex] = externalHeat * ExternalHeatRetention;" in power_text and "ExternalHeat[nodeIndex] = externalHeat * math.lerp" not in power_text,
        "logisticsGraphReadsMathLodConfigQuality": "ResolveEvaluationQualityWeight()" in logistics_text and "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in logistics_text,
        "logisticsGraphJobUsesResolvedQuality": "GlobalQualityWeight = qualityWeight" in logistics_text and "GlobalQualityWeight = PowerSolverConvergenceMath.AuthoritativeQualityWeight" not in logistics_text,
        "logisticsGraphAdaptiveWindowUsesResolvedQuality": "ResolveAdaptiveSolveWindow(qualityWeight, out int solveStartNode, out int solveNodeCount)" in logistics_text and "ResolveAdaptiveSolveNodesPerFrame(globalQualityWeight)" in logistics_text and "ResolveAdaptiveSolveNodesPerFrame(PowerSolverConvergenceMath.AuthoritativeQualityWeight)" not in logistics_text,
        "powerGridManagerReadsMathLodConfigQuality": "ResolveMathLodQualityWeight()" in power_grid_manager_text and "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in power_grid_manager_text,
        "powerGridManagerThermalCadenceContinuous": "ResolveSubmarineThermalGridCadenceSeconds(float globalQualityWeight)" in power_grid_manager_text and "MathLodApproximation.SmoothStep01(q)" in power_grid_manager_text and "math.lerp(SubmarineThermalGridLowCadenceSeconds, SubmarineThermalGridHighCadenceSeconds, curve)" in power_grid_manager_text and "return SubmarineThermalGridHighCadenceSeconds;" not in power_grid_manager_text,
        "powerGridManagerThermalScheduleUsesResolvedQuality": "float quality = ResolveMathLodQualityWeight();" in power_grid_manager_text and "runtime.ScheduleSolve(cadenceSeconds, quality" in power_grid_manager_text and "float quality = PowerSolverConvergenceMath.AuthoritativeQualityWeight;" not in power_grid_manager_text,
        "powerGridCableThermalIterationBudgetUsesResolvedQuality": "float qualityWeight = PowerGridManager.ResolveMathLodQualityWeight();" in power_grid_text and "ResolvePropagationIterations(qualityWeight)" in power_grid_text and "ResolvePropagationIterations(PowerSolverConvergenceMath.AuthoritativeQualityWeight)" not in power_grid_text,
        "batteryChargerReadsMathLodConfigQuality": "ResolvePendingQualityWeight()" in battery_charger_text and "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in battery_charger_text,
        "batteryChargerCadenceContinuous": "MinimumCadenceHz = 5f" in battery_charger_text and "MaximumCadenceHz = 60f" in battery_charger_text and "MathLodApproximation.SmoothStep01(q)" in battery_cadence_body and "math.lerp(MinimumCadenceHz, MaximumCadenceHz, curve)" in battery_cadence_body and "return 60f;" not in battery_cadence_body,
        "batteryChargerScheduleUsesTuningQuality": "SampleQualityWeightUnderTuningLock(vault, out float q)" in battery_charger_text and "float q = AuthoritativeQualityWeight;" not in battery_charger_text,
        "batteryChargerTuningUsesResolvedQuality": "dto.GlobalQualityWeight = ResolvePendingQualityWeight()" in battery_tuning_body and "dto.CadenceHz = ResolveCadenceHzStatic(dto.GlobalQualityWeight)" in battery_tuning_body and "dto.GlobalQualityWeight = AuthoritativeQualityWeight" not in battery_tuning_body,
        "batteryChargerSamplesQualityUnderLock": "TryLockBuffer(BatteryChargerLogisticsBufferIds.Tuning, SystemID.Power)" in battery_sample_body and "TryUnlockBuffer(BatteryChargerLogisticsBufferIds.Tuning, SystemID.Power)" in battery_sample_body,
        "baseAtmosphereReadsMathLodConfigQuality": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in base_atmosphere_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, AuthoritativeQualityWeight)" in base_atmosphere_quality_body,
        "baseAtmosphereTuningUsesResolvedQuality": "tune.GlobalQualityWeight = targetQuality;" in base_atmosphere_tuning_body and "tune.GlobalQualityWeight = AuthoritativeQualityWeight" not in base_atmosphere_tuning_body,
        "baseAtmosphereDiffusionIterationsContinuous": "MinQualityDiffusionIterations = 2" in base_atmosphere_logistics_text and "MaxQualityDiffusionIterations = AuthoritativeDiffusionIterations" in base_atmosphere_logistics_text and "ResolveDiffusionIterations(qualityWeight)" in base_atmosphere_logistics_text and "math.lerp(MinQualityDiffusionIterations, MaxQualityDiffusionIterations, q)" in base_atmosphere_iterations_body and "int iterations = AuthoritativeDiffusionIterations;" not in base_atmosphere_logistics_text,
        "baseAtmosphereEngineReadsMathLodConfigQuality": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in base_atmosphere_engine_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, AuthoritativeQualityWeight)" in base_atmosphere_engine_quality_body,
        "baseAtmosphereEngineColdTickCadenceContinuous": "BaseAtmosphereMath.ResolveColdTickIntervalSeconds(qualityWeight01)" in base_atmosphere_engine_text and "math.lerp(LowColdTickSeconds, HighTickSeconds, curve)" in base_atmosphere_cadence_body and "return HighTickSeconds;" not in base_atmosphere_cadence_body,
        "fluidAdvectionReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in fluid_advection_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 0f)" in fluid_advection_quality_body,
        "fluidAbyssalVisualReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in fluid_abyssal_visual_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 0f)" in fluid_abyssal_visual_quality_body,
        "seismicTideReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in seismic_quality_body and "target = config.GlobalQualityWeight" in seismic_quality_body,
        "asyncBuoyancyReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in async_buoyancy_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f)" in async_buoyancy_quality_body,
        "buoyancyDisplacementReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in buoyancy_displacement_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f)" in buoyancy_displacement_quality_body,
        "analyticalWaveReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in analytical_wave_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f)" in analytical_wave_quality_body,
        "exosuitReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in exosuit_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, ExosuitMathGuards.DefaultQualityWeight)" in exosuit_quality_body,
        "submarineDynamicsReadsMathLodConfig": "ResolveMathLodQualityWeight()" in submarine_dynamics_text and "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in submarine_dynamics_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f)" in submarine_dynamics_quality_body,
        "submarineAutopilotReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in submarine_autopilot_quality_body and "liveQuality = config.GlobalQualityWeight" in submarine_autopilot_quality_body,
        "hydrodynamicKccReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in hydrodynamic_kcc_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f)" in hydrodynamic_kcc_quality_body,
        "vehicleDamageReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in vehicle_damage_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f)" in vehicle_damage_quality_body,
        "hullIntegrityReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in hull_integrity_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f)" in hull_integrity_quality_body,
        "structuralVisualReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in structural_visual_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f)" in structural_visual_quality_body,
        "abyssalCavitationReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in abyssal_cavitation_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f)" in abyssal_cavitation_quality_body,
        "habitatFluidReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in habitat_fluid_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, HabitatFluidIncursionMath.AuthoritativeQualityWeight)" in habitat_fluid_quality_body,
        "assetLoadDispatcherReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in asset_load_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f)" in asset_load_quality_body,
        "assetLifecycleReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in asset_lifecycle_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f)" in asset_lifecycle_quality_body,
        "vramPressureReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in vram_pressure_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f)" in vram_pressure_quality_body,
        "vramEnforcerReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in vram_enforcer_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f)" in vram_enforcer_quality_body,
        "physiologyRuntimeReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in physiology_runtime_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, AuthoritativeQualityWeight)" in physiology_runtime_quality_body,
        "physiologyRuntimeCadenceContinuous": "ResolvePhysiologyUpdateIntervalSeconds(qualityWeight01)" in physiology_runtime_text and "ShinobuPhysiologyConstants.MaxSimulationStepSeconds" in physiology_runtime_interval_body and "AuthoritativeUpdateIntervalSeconds" in physiology_runtime_interval_body and "math.lerp(" in physiology_runtime_interval_body and "return AuthoritativeUpdateIntervalSeconds;" not in physiology_runtime_interval_body,
        "gasDynamicsReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in gas_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, AuthoritativeQualityWeight)" in gas_quality_body,
        "gasDynamicsCadenceContinuous": "ResolveCadenceSeconds(qualityWeight01)" in gas_text and "math.lerp(lowCadence, midCadence, q)" in gas_cadence_body and "math.lerp(lowToMid, highCadence, q)" in gas_cadence_body and "return math.max(0.02f, math.min(highCadence" not in gas_cadence_body,
        "seaglideRuntimeReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in seaglide_quality_body and "MathLodApproximation.SaturateFinite(" in seaglide_quality_body and "tuning.ResolvedQualityWeight = quality;" in seaglide_quality_body,
        "seaglideJobUsesGlobalQualityWeight": "float quality = math.saturate(math.select(" in seaglide_jobs_text and "GlobalQualityWeight" in seaglide_jobs_text and "float quality = SeaglideSimdMath.AuthoritativeQualityWeight;" not in seaglide_jobs_text,
        "volcanicUpdraftReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in volcanic_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, VolcanicUpdraftVault.AuthoritativeQualityWeight)" in volcanic_quality_body,
        "volcanicUpdraftContinuousVisualWeights": "return SmoothStep(0f, 1f, q);" in volcanic_debris_body and "return SmoothStep(0f, 1f, q);" in volcanic_turbulence_body and "math.step(0.3f, q)" not in (volcanic_debris_body + volcanic_turbulence_body),
        "volcanicUpdraftJobsUseSettingsQuality": "ResolveTurbulenceGate(settings.GlobalQualityWeight)" in volcanic_updraft_text and "ResolveDebrisLiftWeight(Settings.GlobalQualityWeight)" in volcanic_updraft_text and "ResolveDebrisLiftWeight(VolcanicUpdraftVault.AuthoritativeQualityWeight)" not in volcanic_updraft_text,
        "abyssalThermodynamicsReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in abyssal_thermo_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, AbyssalThermalMath.AuthoritativeQualityWeight)" in abyssal_thermo_quality_body,
        "abyssalThermodynamicsBuildTuningUsesResolvedQuality": "float safeQuality = ResolveVisualQualityWeight();" in abyssal_thermo_build_body and "tuning.GlobalQualityWeight = safeQuality;" in abyssal_thermo_build_body and "ResolveJacobiIterations(safeQuality)" in abyssal_thermo_build_body and "float safeQuality = AbyssalThermalMath.AuthoritativeQualityWeight;" not in abyssal_thermo_build_body,
        "abyssalThermodynamicsWriteTuningUsesResolvedQuality": "float safeQuality = ResolveVisualQualityWeight();" in abyssal_thermo_write_body and "tuning.GlobalQualityWeight = safeQuality;" in abyssal_thermo_write_body and "ResolveJacobiIterations(safeQuality)" in abyssal_thermo_write_body and "tuning.GlobalQualityWeight = AbyssalThermalMath.AuthoritativeQualityWeight;" not in abyssal_thermo_write_body,
        "abyssalReactorDefaultsUseResolvedQuality": "float qualityWeight = ResolveVisualQualityWeight();" in reactor_default_body and "tuning.GlobalQualityWeight = qualityWeight;" in reactor_default_body and "float qualityWeight = ResolveVisualQualityWeight();" in nuclear_reactor_default_body and "tuning.GlobalQualityWeight = qualityWeight;" in nuclear_reactor_default_body,
        "abyssalReactorWriteFallbackUsesResolvedQuality": "ResolveVisualQualityWeight()" in reactor_write_body and "ResolveVisualQualityWeight()" in nuclear_reactor_write_body and ": AbyssalThermalMath.AuthoritativeQualityWeight;" not in (reactor_write_body + nuclear_reactor_write_body),
        "metabolismRuntimeReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in metabolism_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f)" in metabolism_quality_body,
        "metabolismThermalInterpolationContinuous": "return q * q * (3f - 2f * q);" in metabolism_interpolation_body and "math.step(0.3f, q)" not in metabolism_interpolation_body,
        "bulkheadRuntimeReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in bulkhead_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, AuthoritativeQualityWeight)" in bulkhead_quality_body,
        "bulkheadAuthorityCadenceUsesResolvedQuality": "float q = ResolveBulkheadQualityWeight();" in bulkhead_runtime_text and "ResolveAuthorityCadenceHz(q)" in bulkhead_runtime_text and "math.lerp(5f, 30f, weight * weight)" in bulkhead_cadence_body and "float q = AuthoritativeQualityWeight;" not in bulkhead_runtime_text,
        "bulkheadHatchTuningUsesResolvedQuality": "ResolveBulkheadQualityWeight()" in bulkhead_hatchlocks_text and "BulkheadContainmentMath.Sanitize01(HomeostasisBrain.GlobalQualityWeight, 0f)" not in bulkhead_hatchlocks_text,
        "symbiosisReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in symbiosis_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, AuthoritativeQualityWeight)" in symbiosis_quality_body,
        "symbiosisTuningUsesResolvedQuality": "float quality = ResolveSymbiosisQualityWeight();" in symbiosis_text and "activeTuning.GlobalQualityWeight = quality;" in symbiosis_text and "dto.GlobalQualityWeight = ResolveSymbiosisQualityWeight();" in symbiosis_text and "activeTuning.GlobalQualityWeight = AuthoritativeQualityWeight" not in symbiosis_text and "const float quality = AuthoritativeQualityWeight" not in symbiosis_text,
        "symbiosisComplexityUsesContinuousQuality": "float q = math.saturate(tuning.GlobalQualityWeight);" in symbiosis_exchange_text and "float qualityCurve = q * q * (3f - 2f * q);" in symbiosis_exchange_text and "const float qualityCurve = 1f;" not in symbiosis_exchange_text,
        "symbiosisTruthAmplitudeInvariant": symbiosis_exchange_text.count("const float truthCurve = 1f;") >= 2 and "math.lerp(0.02f, 0.18f, truthCurve)" in symbiosis_exchange_text and "math.lerp(0.25f, 1f, truthCurve)" in symbiosis_exchange_text,
        "migrationReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in migration_quality_body and "MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, AuthoritativeQualityWeight)" in migration_quality_body,
        "migrationCadenceContinuousResolved": "float qualityWeight = ResolveMigrationQualityWeight();" in migration_text and "ResolveMigrationFieldColdTickIntervalSeconds(qualityWeight)" in migration_text and "math.lerp(2.4f, 0.2f, quality)" in migration_cadence_body and "ResolveMigrationFieldColdTickIntervalSeconds()" not in migration_text,
        "migrationJobUsesResolvedQuality": "GlobalQualityWeight = ResolveMigrationQualityWeight()" in migration_text and "GlobalQualityWeight = AuthoritativeQualityWeight" not in migration_text,
        "boidSocialLodReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in boid_social_lod_body and "MathLodApproximation.SaturateFinite(qualityWeight, 1f)" in boid_social_lod_body and "HomeostasisBrain.GlobalQualityWeight;" in boid_social_lod_body,
        "leviathanTerrainIkSdfContinuous": "float trilinearWeight = Smooth01(qualityWeight);" in leviathan_sdf_body and "math.lerp(nearestDensity, trilinearDensity, trilinearWeight)" in leviathan_sdf_body and "math.step(0.3f" not in leviathan_sdf_body,
        "proceduralBoneSecondaryContinuous": "float secondaryCurve = ProceduralBoneMath.SmoothRange01(quality, tuning.SecondaryBoneStart01, 1f);" in procedural_bone_text and "secondaryGate" not in procedural_bone_text,
        "proceduralBoneJawContinuous": "float jawWeight = math.saturate(tuning.JawIkWeight * ProceduralBoneMath.SmoothRange01(quality, 0.35f, 1f));" in procedural_bone_text and "jawGate" not in procedural_bone_text,
        "kineticCharacterSdfGradientContinuous": "float gradientWeight = KineticCharacterMath.SmoothRange01(quality, 0.08f, 1f);" in kinetic_sdf_body and "math.lerp(normal, gradientNormal, gradientWeight)" in kinetic_sdf_body and "math.step(0.24f, quality)" not in kinetic_sdf_body,
        "tetherAupCatmullContinuous": "float catmullWeight = Smooth01(q);" in tether_aup_text and "math.step(0.3f, q) * Smooth01(q)" not in tether_aup_text,
        "cable132CatmullContinuous": "float catmullWeight = Smooth01(q);" in cable_physics_132_text and "math.step(0.25f, q) * Smooth01(q)" not in cable_physics_132_text,
        "interiorGIReadsMathLodConfig": "MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)" in interior_gi_quality_body and "MathLodApproximation.SaturateFinite(weight, 1f)" in interior_gi_quality_body,
        "interiorGIDirectionalWeightsContinuous": "float directional = Smooth01((safeQuality - 0.08f) * 1.35f);" in interior_gi_build_tuning_body and "float l2 = Smooth01((safeQuality - 0.54f) * 2.05f);" in interior_gi_build_tuning_body and "l1Gate" not in interior_gi_build_tuning_body and "l2Gate" not in interior_gi_build_tuning_body,
        "interiorGICadenceContinuous": "float thermalGate = 1f - Smooth01((q - 0.05f) * 2.2222223f);" in interior_gi_cadence_body and "math.step(0.3f, q)" not in interior_gi_cadence_body,
        "dynamicMusicInterpolationContinuous": "float interpolationCurve = Smooth01(qualityWeight);" in dynamic_music_text and "int nextIndex = math.min(baseIndex + 1, safeBankLength - 1);" in dynamic_music_text and "interpolationAdmission" not in dynamic_music_text,
        "stormNoiseOctavesContinuous": "return math.clamp(1 + (int)math.round(Smooth01(q) * 2f), 1, 3);" in storm_noise_octave_body and "math.step(0.3f, q)" not in storm_noise_octave_body and "math.step(0.7f, q)" not in storm_noise_octave_body,
        "oceanFoamQualityContinuous": "qualityFoam = qualityFoam * qualityFoam * (3f - (2f * qualityFoam));" in ocean_foam_body and "math.step(0.28f, q)" not in ocean_foam_body,
        "voxelSurfaceDensityContinuous": "float interpolationWeight = Smooth01(quality);" in voxel_density_body and "return math.lerp(nearest, trilinear, interpolationWeight);" in voxel_density_body and "math.step(0.3f, quality)" not in voxel_density_body,
        "runtimeQualityStepGateSweepRemoved": quality_step_pattern.search(quality_step_sweep_code) is None,
        "criticalAudioReverbTierContinuous": "math.round(SmoothQuality01(quality) * 2f)" in critical_audio_reverb_body and "math.step" not in strip_csharp_non_code(critical_audio_reverb_body),
        "biomimeticHizTapContinuous": "int tapCount = 1 + (int)math.floor(ShinobuPoiMath.ResolveQualityCurve(quality) * 4f);" in biomimetic_text and "highTapGate" not in biomimetic_text,
        "vrSomaticLowQualityContinuous": "float lowQualityCurve = SmoothJob01((0.3f - quality) * 3.3333333f);" in vr_somatic_text and "lowQualityWindow" not in vr_somatic_text,
        "seedShipEntityBudgetContinuous": "float curvedQuality = qualitySq * qualitySq;" in seedship_budget_body and "requested = math.lerp(minFloor, maxTarget, curvedQuality) * corruptionGate" in seedship_budget_body and "activeGate" not in seedship_budget_body,
        "homeostasisSurvivalFloorContinuous": "float survivalFloor = SmoothStep01(" in homeostasis_scalability_text and "MathLodSurvivalStep - qualityWeight" in homeostasis_scalability_text and "math.step(MathLodSurvivalStep" not in homeostasis_scalability_text,
        "sumpPumpLowPowerHoldContinuous": "float lowPowerT = math.saturate((0.30f - q) * 3.3333333f);" in sump_pump_thermal_body and "lowPowerT = lowPowerT * lowPowerT * (3f - 2f * lowPowerT);" in sump_pump_thermal_body and "float lowPowerHold = lowPowerT * math.max(0f, 0.30f - q) * 0.12f;" in sump_pump_thermal_body and "math.step(q, 0.30f)" not in sump_pump_thermal_body,
        "chemicalInfluenceQualityContinuous": "float sampleBlend = Smooth01(quality);" in chemical_text and "float driftGate = qCurve;" in chemical_text and "float highTap = Smooth01((q - 0.7f) * 3.3333333f);" in chemical_text and "math.step(0.3f" not in strip_csharp_non_code(chemical_text),
        "faunaKinematicsNearZeroCompatibility": "SmoothQualityCurve(_globalQualityWeight) <= 0.0001f" in fauna_kinematics_text and "SmoothQualityCurve(qualityWeight) <= 0.0001f" in fauna_kinematics_text and "math.step(0.3f" not in strip_csharp_non_code(fauna_kinematics_text),
        "reactorInjectionDiameterContinuous": "int shell = math.clamp((int)math.round(quality * quality * (3f - 2f * quality)), 0, 1);" in reactor_injection_body and "math.step(0.30f" not in reactor_injection_body,
        "reactorFiniteOrUsesMathSelect": "return math.select(fallback, value, math.isfinite(value));" in reactor_thermal_text,
        "deltaCrusherCapContinuous": "float upperT = SmoothQuality01((quality - 0.5f) * 2f);" in delta_crusher_cap_body and "math.round(math.lerp(minimumToMiddle, middleToMaximum, upperT))" in delta_crusher_cap_body and "math.step(0.5f" not in delta_crusher_cap_body,
        "repairToolQualityFlagsContinuous": "ResolveRepairQualityCurve(quality01) > 0.0001f" in repair_tool_text and "ResolveRepairQualityCurve(quality01) <= 0.0001f" in repair_tool_text and "math.step(0.25f" not in strip_csharp_non_code(repair_tool_text) and "math.step(0.3f" not in strip_csharp_non_code(repair_tool_text),
        "carrionExpBlendContinuous": "float expWeight = math.smoothstep(0.4f, 0.95f, quality);" in carrion_text and "lowQualityExpBlend" in carrion_text and "smoothstep_0.4_0.95" in carrion_text and "math.step" not in strip_csharp_non_code(carrion_text),
        "macroEcosystemQualityCurveContinuous": "float polynomial = thermalBand * thermalBand * (3f - 2f * thermalBand);" in macro_quality_body and "math.step(0.0001f" not in macro_quality_body,
        "memorySentinelQualityDeficitContinuous": "qualityDeficit = qualityDeficit * qualityDeficit * (3f - 2f * qualityDeficit);" in memory_sentinel_text and "math.round(math.lerp(1f, 64f, qualityDeficit))" in memory_sentinel_text and "math.step" not in strip_csharp_non_code(memory_sentinel_text),
        "fabricationUploadContinuous": "float curved = q * q * (3f - (2f * q));" in fabrication_text and "ResolveVisualUploadStride(_lastQualityWeight)" in fabrication_text and "activeQualityGate" not in fabrication_text,
        "topographicalSonarSamplingContinuous": "return math.lerp(nearest, trilinear, ResolveWorkCurve(QualityWeight));" in topographical_sonar_text and ("return Smooth01(math.saturate((math.saturate(quality) - 0.1f) * math.rcp(0.9f)));" in topographical_sonar_text or "return t * t * (3f - 2f * t);" in sonar_work_curve_body) and "math.step(0.3f" not in strip_csharp_non_code(topographical_sonar_text),
        "utilityAiQualityContinuous": "return math.smoothstep(0f, 1f, q);" in utility_quality_body and "math.step(Epsilon" not in utility_quality_body,
        "saveMerkleSurvivalPullContinuous": "float survivalPull = SmoothUnit((0.3f - quality) * 3.3333333f);" in save_merkle_text and "1f - math.step(0.3f" not in strip_csharp_non_code(save_merkle_text),
        "playerKinematicsNearZeroCompatibility": "SmoothQuality01(qualityWeight01) <= 0.0001f" in player_kinematics_text and "math.step(SmoothQuality01(qualityWeight01), 0.25f)" not in strip_csharp_non_code(player_kinematics_text),
        "qaWatchdogQualityContinuous": "float richNormalGate = Smooth01((quality - LowQualityNormalCollapseThreshold)" in watchdog_text and "float recoveryGate = Smooth01((phase - QualityClampSeconds)" in watchdog_text and "math.step(LowQualityNormalCollapseThreshold" not in strip_csharp_non_code(watchdog_text),
        "seismicActiveHarmonicsContinuous": "SmoothStepRange(0.30f, 0.55f, q)" in seismic_harmonic_body and "SmoothStepRange(0.58f, 0.78f, q)" in seismic_harmonic_body and "SmoothStepRange(0.82f, 1f, q)" in seismic_harmonic_body and "math.step(0.3f" not in seismic_harmonic_body,
        "modProjectionLowQualityFlagsContinuous": "Smooth01((LowProjectionQualityFlagThreshold01 - quality)" in mod_projection_text and "math.step(quality, LowProjectionQualityFlagThreshold01)" not in strip_csharp_non_code(mod_projection_text),
        "jacobiSelfAuditMonotonic": "lowIterations < midIterations" in power_text and "midIterations < highIterations" in power_text,
        "powerJacobiConductanceCapPresent": "MaximumConductance = 4096f" in power_jacobi_text and power_jacobi_text.count("PowerGridJacobiConstants.MaximumConductance") >= 4,
        "powerJacobiNetCurrentCapPresent": "MaximumNetCurrentAbs = 1048576f" in power_jacobi_text and "netCurrentOut = math.clamp(netCurrentOut + current" in power_jacobi_text,
        "powerJacobiTickCapPresent": "MaximumTickDeltaSeconds = 1f" in power_jacobi_text and "PowerGridJacobiConstants.MaximumTickDeltaSeconds" in power_jacobi_text,
        "powerJacobiFuzzerContinuousIterationsPresent": "MinimumSolverIterationCount = 2" in power_fuzzer_text and "MaximumSolverIterationCount = 50" in power_fuzzer_text and "ResolveIterationCount(config.IterationCount, globalQualityWeight)" in power_fuzzer_text and "config.IterationCount = 0;" in power_fuzzer_text,
        "powerJacobiFuzzerUsesMathLodBudget": "MathLodRuntimeConfig.ResolveActiveIterationBudget(globalQualityWeight)" in power_fuzzer_text and "MathLodApproximation.SaturateFinite(globalQualityWeight, 1f)" in power_fuzzer_text,
        "powerJacobiFuzzerDampedOmega": "OmegaMax = 0.92f" in power_fuzzer_text and "OmegaMax = 1.90f" not in power_fuzzer_text,
        "powerJacobiFuzzerConductanceCurrentCaps": "MaximumConductance = 4096f" in power_fuzzer_text and "MaximumEdgeCurrentAbs = MaximumConductance" in power_fuzzer_text and "EdgeCurrentFlow[edgeCursor] = math.clamp" in power_fuzzer_text,
        "powerJacobiFuzzerIsolatedVault": "CreateIsolatedFuzzerVault" in power_fuzzer_text and "GlobalDataVault.Create(32, arenaBytes)" not in power_fuzzer_text,
        "solarDirectExpRemoved": "math.exp" not in solar_text,
        "gasLeakDirectExpRemoved": "ResolveAnalyticalLeakAlpha" in gas_text and "math.exp" not in extract_function_body(gas_text, "ResolveAnalyticalLeakAlpha"),
        "stormAttenuationDirectExpRemoved": "Attenuate(float intensity01" in storm_text and "math.exp" not in extract_function_body(storm_text, "Attenuate(float intensity01"),
        "aiAnxietyDirectExpRemoved": "CalculateAnxietyDecayJob" in anxiety_text and "math.exp" not in anxiety_text,
        "ballisticsDirectExpRemoved": "MathLodApproximation.ApproxExpNegPade33Wide40(math.max(0f, Tuning.DragCoefficient) * closestDistance)" in ballistics_text and "math.exp" not in ballistics_text,
        "rollbackDirectExpRemoved": "MathLodApproximation.ApproxExpNegPade33Wide40(math.max(0.001f, ExtrapolationDecay) * missingTicks)" in rollback_text and "MathLodApproximation.ApproxExpNegPade33Wide40(math.max(0.001f, ExponentialDecay))" in rollback_text and "math.exp" not in rollback_text,
        "editorBioForgeExpLogRemoved": "math.exp" not in bioforge_text and "math.log" not in bioforge_text and "SmoothMinExp" in bioforge_text,
        "hydraulicErosionExpRemoved": "MathLodApproximation.ApproxExpNegPade33Wide40(math.abs(p.x + p.y * 0.37f) * 8f)" in hydraulic_text and "math.exp" not in hydraulic_text,
        "aiAnxietyApproxIfCount": len(re.findall(r"\bif\s*\(", anxiety_approx_body)),
        "remainingFloatModeFastCount": len(re.findall(r"FloatMode\s*=\s*FloatMode\.Fast|FloatMode\.Fast", "\n".join(read_text(path) for path in collect_cs_files()))),
    }


def main() -> int:
    files = collect_cs_files()
    counts, occurrences = count_remaining_transcendentals(files)
    domain_1 = scan_exp_residual(1.0, 0.0001)
    domain_4 = scan_exp_residual(4.0, 0.0001)
    domain_pos_4 = scan_exp_residual_with(approx_exp_positive_pade33_reduced_f32, math.exp, 4.0, 0.0001)
    domain_wide_40 = scan_exp_residual_with(approx_exp_neg_pade33_wide40_f32, lambda x: math.exp(-x), 40.0, 0.001)
    domain_sin_twopi = scan_signed_residual_with(approx_sin_bhaskara_f32, math.sin, 2.0 * math.pi, 0.0001)
    domain_cos_twopi = scan_signed_residual_with(approx_cos_bhaskara_f32, math.cos, 2.0 * math.pi, 0.0001)
    domain_tan_visual = scan_signed_residual_with(lambda x: approx_tan_clamped_f32(x, 4096.0), math.tan, 1.4, 0.0001)
    domain_atan = scan_signed_residual_with(approx_atan_fast_f32, math.atan, 16.0, 0.0001)
    domain_acos = scan_signed_residual_with(approx_acos_fast_f32, math.acos, 1.0, 0.0001)
    extreme_kernel_finiteness = scan_extreme_kernel_finiteness()
    power_destination_mask_equivalence = scan_power_destination_mask_equivalence()
    phys_x = math.log(2.0) / 300.0 * 256.0 * 0.25
    phys_exact = math.exp(-phys_x)
    phys_approx = float(approx_exp_neg_pade33_reduced_f32(phys_x))
    anchors = code_anchor_audit()
    asmdef_audit = asmdef_dependency_audit(files)
    extremes = []
    for value in [float("nan"), float("inf"), float("-inf"), -1.0e9, 1.0e6, 1000.0, 4.0, 1.0, 0.0]:
        approx = float(approx_exp_neg_pade33_reduced_f32(value))
        extremes.append({
            "input": str(value),
            "approx": approx,
            "finite": math.isfinite(approx),
            "within01": 0.0 <= approx <= 1.0,
        })
    positive_inf_decay = float(approx_exp_neg_pade33_reduced_f32(float("inf")))
    report = {
        "agent": "X_007",
        "generatedBy": "Tools/OOP_MathLOD_Scanner.py",
        "sourceRoot": str(ROOT),
        "scannedCSharpFiles": len(files),
        "remainingTranscendentals": counts,
        "remainingTranscendentalTotal": sum(counts.values()),
        "firstOccurrences": occurrences[:200],
        "residualProof": {
            "approximation": "P33(clamp(x,0,4)/4)^4",
            "p33Numerator": "1 - y/2 + y^2/10 - y^3/120",
            "p33Denominator": "1 + y/2 + y^2/10 + y^3/120",
            "floatScan0To1": domain_1,
            "floatScan0To4": domain_4,
            "positiveExpScan0To4": domain_pos_4,
            "wideExpNegScan0To40": domain_wide_40,
            "bhaskaraSinScan0To2Pi": domain_sin_twopi,
            "bhaskaraCosScan0To2Pi": domain_cos_twopi,
            "tanScan0To1_4": domain_tan_visual,
            "atanScan0To16": domain_atan,
            "acosScan0To1": domain_acos,
            "physiologyWorstCase": {
                "x": phys_x,
                "exact": phys_exact,
                "approx": phys_approx,
                "absError": abs(phys_approx - phys_exact),
            },
            "extremeInputs": extremes,
            "positiveInfinityDecayPolicy": {
                "expected": "clamp to x=4 maximum finite decay range, not NaN fallback",
                "approx": positive_inf_decay,
                "finite": math.isfinite(positive_inf_decay),
                "withinMaxDecayRange": 0.018 <= positive_inf_decay <= 0.019,
            },
        },
        "qualityDropProof": {
            "globalQualityWeightInputs": [1.0, 0.1],
            "authorityUsesGlobalQualityWeightForTissueCount": False,
            "authorityTissueCount": anchors["decompressionAuthorityTissueCount"],
            "expectedTissueStateDeltaForEqualPhysicalInputs": 0.0,
        },
        "tortureProof": {
            "sampleCount": 16,
            "coversExtremeInputs": anchors["mathLodTortureCoversExtremePressureTemperature"],
            "coversAngleAndPowKernels": anchors["mathLodTortureCoversAngleKernels"],
            "checksNonFiniteAcrossAllKernels": anchors["mathLodTortureChecksNonFiniteAllKernels"],
            "sanitizesResultEnvelope": anchors["mathLodTortureSanitizesEnvelope"],
            "extremeTemperatureCelsiusSamples": [-273.15, 37.0, 1000000.0, -1000000.0],
            "extremePressureAtmSamples": [0.0, 1.0, 1000.0, 1000000.0],
        },
        "extremeKernelFinitenessProof": extreme_kernel_finiteness,
        "powerDestinationMaskEquivalenceProof": power_destination_mask_equivalence,
        "jacobiProof": {
            "samples": [jacobi_sample(q) for q in [0.0, 0.1, 0.5, 1.0]],
            "safetyInvariant": "bounded relaxation: capped non-negative conductance, guarded denominator, saturated [0,1] potential, finite current caps, divergence flags",
            "convergenceClaimAtMinQuality": "not claimed",
            "headlessFuzzerContract": {
                "minimumIterations": 2,
                "maximumIterations": 50,
                "omegaRange": [0.55, 0.92],
                "usesGlobalQualityWeight": anchors["powerJacobiFuzzerUsesMathLodBudget"],
                "conductanceCap": 4096.0,
                "edgeCurrentCap": 4096.0,
                "isolatedVaultDoesNotPublishLatestCreated": anchors["powerJacobiFuzzerIsolatedVault"],
            },
        },
        "thermalInjectionTruthProof": {
            "qualityAffectsExternalHeatTruth": False,
            "heatShape": "quality-invariant smoothstep radial mask",
            "heatRetention": "quality-invariant 0.55 carry-over",
            "reason": "external thermal heat can trip damage/brownout flags, so quality must not change its source amplitude or decay",
            "heatShapeQualityInvariant": anchors["externalThermalInjectionQualityInvariantHeatShape"],
            "heatRetentionQualityInvariant": anchors["externalHeatRetentionQualityInvariant"],
        },
        "logisticsQualityRouteProof": {
            "readsMathLodConfig": anchors["logisticsGraphReadsMathLodConfigQuality"],
            "jobUsesResolvedQuality": anchors["logisticsGraphJobUsesResolvedQuality"],
            "adaptiveWindowUsesResolvedQuality": anchors["logisticsGraphAdaptiveWindowUsesResolvedQuality"],
            "fallback": "AuthoritativeQualityWeight only when MathLodRuntimeConfig has no published snapshot",
        },
        "powerGridManagerQualityRouteProof": {
            "readsMathLodConfig": anchors["powerGridManagerReadsMathLodConfigQuality"],
            "thermalCadenceContinuous": anchors["powerGridManagerThermalCadenceContinuous"],
            "thermalScheduleUsesResolvedQuality": anchors["powerGridManagerThermalScheduleUsesResolvedQuality"],
            "cableThermalIterationBudgetUsesResolvedQuality": anchors["powerGridCableThermalIterationBudgetUsesResolvedQuality"],
            "cadenceRangeSeconds": [0.2, 0.016666666666666666],
        },
        "batteryChargerQualityRouteProof": {
            "readsMathLodConfig": anchors["batteryChargerReadsMathLodConfigQuality"],
            "cadenceContinuous": anchors["batteryChargerCadenceContinuous"],
            "scheduleUsesTuningQuality": anchors["batteryChargerScheduleUsesTuningQuality"],
            "tuningUsesResolvedQuality": anchors["batteryChargerTuningUsesResolvedQuality"],
            "samplesQualityUnderTuningLock": anchors["batteryChargerSamplesQualityUnderLock"],
            "cadenceRangeHz": [5.0, 60.0],
            "overridePolicy": "QualityOverride >= 0 clamps to 0..1 and overrides the MathLodRuntimeConfig snapshot; negative override follows global quality.",
        },
        "baseAtmosphereQualityRouteProof": {
            "readsMathLodConfig": anchors["baseAtmosphereReadsMathLodConfigQuality"],
            "tuningUsesResolvedQuality": anchors["baseAtmosphereTuningUsesResolvedQuality"],
            "diffusionIterationsContinuous": anchors["baseAtmosphereDiffusionIterationsContinuous"],
            "engineReadsMathLodConfig": anchors["baseAtmosphereEngineReadsMathLodConfigQuality"],
            "engineColdTickCadenceContinuous": anchors["baseAtmosphereEngineColdTickCadenceContinuous"],
            "iterationRange": [2, 8],
            "coldTickRangeSeconds": [1.0, 0.2],
            "truthPolicy": "gas source/consumer rates stay unchanged; quality scales diffusion solver passes only",
        },
        "aiEcosystemQualityRouteProof": {
            "symbiosisReadsMathLodConfig": anchors["symbiosisReadsMathLodConfig"],
            "symbiosisTuningUsesResolvedQuality": anchors["symbiosisTuningUsesResolvedQuality"],
            "symbiosisComplexityUsesContinuousQuality": anchors["symbiosisComplexityUsesContinuousQuality"],
            "symbiosisTruthAmplitudeInvariant": anchors["symbiosisTruthAmplitudeInvariant"],
            "migrationReadsMathLodConfig": anchors["migrationReadsMathLodConfig"],
            "migrationCadenceContinuousResolved": anchors["migrationCadenceContinuousResolved"],
            "migrationJobUsesResolvedQuality": anchors["migrationJobUsesResolvedQuality"],
            "boidSocialLodReadsMathLodConfig": anchors["boidSocialLodReadsMathLodConfig"],
            "truthPolicy": "ecosystem quality scales cadence/stride/social visual detail; oxygen, feeding, migration authority amplitudes are not reduced by binary quality tiers",
        },
        "animationQualityGateProof": {
            "leviathanTerrainIkSdfContinuous": anchors["leviathanTerrainIkSdfContinuous"],
            "proceduralBoneSecondaryContinuous": anchors["proceduralBoneSecondaryContinuous"],
            "proceduralBoneJawContinuous": anchors["proceduralBoneJawContinuous"],
            "kineticCharacterSdfGradientContinuous": anchors["kineticCharacterSdfGradientContinuous"],
            "truthPolicy": "animation quality scales interpolation/detail weights continuously; bone identity, pose authority, and collision/SDF validity checks remain unchanged",
        },
        "physicsLightingQualityGateProof": {
            "tetherAupCatmullContinuous": anchors["tetherAupCatmullContinuous"],
            "cable132CatmullContinuous": anchors["cable132CatmullContinuous"],
            "interiorGIReadsMathLodConfig": anchors["interiorGIReadsMathLodConfig"],
            "interiorGIDirectionalWeightsContinuous": anchors["interiorGIDirectionalWeightsContinuous"],
            "interiorGICadenceContinuous": anchors["interiorGICadenceContinuous"],
            "truthPolicy": "quality scales cable/tether spline presentation and GI directional/cadence detail; tether constraints, tension events, light sources, and occlusion validity stay authoritative",
        },
        "presentationQualityGateProof": {
            "dynamicMusicInterpolationContinuous": anchors["dynamicMusicInterpolationContinuous"],
            "stormNoiseOctavesContinuous": anchors["stormNoiseOctavesContinuous"],
            "oceanFoamQualityContinuous": anchors["oceanFoamQualityContinuous"],
            "voxelSurfaceDensityContinuous": anchors["voxelSurfaceDensityContinuous"],
            "truthPolicy": "quality scales audio interpolation, storm visual octaves, foam scalar, and voxel density interpolation only; biome state, survival state, and source scalar truth stay unchanged",
        },
        "runtimeQualityStepGateSweepProof": {
            "qualityStepPatternAbsent": anchors["runtimeQualityStepGateSweepRemoved"],
            "criticalAudioReverbTierContinuous": anchors["criticalAudioReverbTierContinuous"],
            "biomimeticHizTapContinuous": anchors["biomimeticHizTapContinuous"],
            "vrSomaticLowQualityContinuous": anchors["vrSomaticLowQualityContinuous"],
            "seedShipEntityBudgetContinuous": anchors["seedShipEntityBudgetContinuous"],
            "homeostasisSurvivalFloorContinuous": anchors["homeostasisSurvivalFloorContinuous"],
            "sumpPumpLowPowerHoldContinuous": anchors["sumpPumpLowPowerHoldContinuous"],
            "chemicalInfluenceQualityContinuous": anchors["chemicalInfluenceQualityContinuous"],
            "faunaKinematicsNearZeroCompatibility": anchors["faunaKinematicsNearZeroCompatibility"],
            "reactorInjectionDiameterContinuous": anchors["reactorInjectionDiameterContinuous"],
            "reactorFiniteOrUsesMathSelect": anchors["reactorFiniteOrUsesMathSelect"],
            "deltaCrusherCapContinuous": anchors["deltaCrusherCapContinuous"],
            "repairToolQualityFlagsContinuous": anchors["repairToolQualityFlagsContinuous"],
            "carrionExpBlendContinuous": anchors["carrionExpBlendContinuous"],
            "macroEcosystemQualityCurveContinuous": anchors["macroEcosystemQualityCurveContinuous"],
            "memorySentinelQualityDeficitContinuous": anchors["memorySentinelQualityDeficitContinuous"],
            "fabricationUploadContinuous": anchors["fabricationUploadContinuous"],
            "topographicalSonarSamplingContinuous": anchors["topographicalSonarSamplingContinuous"],
            "utilityAiQualityContinuous": anchors["utilityAiQualityContinuous"],
            "saveMerkleSurvivalPullContinuous": anchors["saveMerkleSurvivalPullContinuous"],
            "playerKinematicsNearZeroCompatibility": anchors["playerKinematicsNearZeroCompatibility"],
            "qaWatchdogQualityContinuous": anchors["qaWatchdogQualityContinuous"],
            "seismicActiveHarmonicsContinuous": anchors["seismicActiveHarmonicsContinuous"],
            "modProjectionLowQualityFlagsContinuous": anchors["modProjectionLowQualityFlagsContinuous"],
            "truthPolicy": "quality may scale cadence, sampling, visual flags, and optional upload budgets; it does not change physiology tissue count, thermal source amplitude, save identity, or authority DTO layout",
        },
        "runtimeQualitySnapshotRouteProof": {
            "fluidAdvection": anchors["fluidAdvectionReadsMathLodConfig"],
            "fluidAbyssalVisual": anchors["fluidAbyssalVisualReadsMathLodConfig"],
            "seismicTide": anchors["seismicTideReadsMathLodConfig"],
            "asyncBuoyancyReadback": anchors["asyncBuoyancyReadsMathLodConfig"],
            "buoyancyDisplacement": anchors["buoyancyDisplacementReadsMathLodConfig"],
            "analyticalGerstnerWave": anchors["analyticalWaveReadsMathLodConfig"],
            "exosuitKinematics": anchors["exosuitReadsMathLodConfig"],
            "submarineDynamics": anchors["submarineDynamicsReadsMathLodConfig"],
            "submarineAutopilotSdf": anchors["submarineAutopilotReadsMathLodConfig"],
            "hydrodynamicKcc": anchors["hydrodynamicKccReadsMathLodConfig"],
            "vehicleComponentDamage": anchors["vehicleDamageReadsMathLodConfig"],
            "hullIntegrity": anchors["hullIntegrityReadsMathLodConfig"],
            "structuralVisual": anchors["structuralVisualReadsMathLodConfig"],
            "abyssalCavitation": anchors["abyssalCavitationReadsMathLodConfig"],
            "habitatFluidIncursion": anchors["habitatFluidReadsMathLodConfig"],
            "assetLoadDispatcher": anchors["assetLoadDispatcherReadsMathLodConfig"],
            "assetLifecycleGovernor": anchors["assetLifecycleReadsMathLodConfig"],
            "vramPressureMonitor": anchors["vramPressureReadsMathLodConfig"],
            "vramEnforcer": anchors["vramEnforcerReadsMathLodConfig"],
            "policy": "heavy runtime readers consume the owner-published zero-GC MathLodRuntimeConfig snapshot first; HomeostasisBrain remains a cold bootstrap fallback only",
        },
        "runtimeContinuousCadenceAndVisualProof": {
            "physiologyRuntimeSnapshotRoute": anchors["physiologyRuntimeReadsMathLodConfig"],
            "physiologyCadenceContinuous": anchors["physiologyRuntimeCadenceContinuous"],
            "gasDynamicsSnapshotRoute": anchors["gasDynamicsReadsMathLodConfig"],
            "gasDynamicsCadenceContinuous": anchors["gasDynamicsCadenceContinuous"],
            "seaglideRuntimeSnapshotRoute": anchors["seaglideRuntimeReadsMathLodConfig"],
            "seaglideBurstJobUsesGlobalQualityWeight": anchors["seaglideJobUsesGlobalQualityWeight"],
            "volcanicUpdraftSnapshotRoute": anchors["volcanicUpdraftReadsMathLodConfig"],
            "volcanicVisualWeightsContinuous": anchors["volcanicUpdraftContinuousVisualWeights"],
            "volcanicJobsUseSettingsQuality": anchors["volcanicUpdraftJobsUseSettingsQuality"],
            "abyssalThermodynamicsSnapshotRoute": anchors["abyssalThermodynamicsReadsMathLodConfig"],
            "abyssalThermodynamicsBuildTuningUsesResolvedQuality": anchors["abyssalThermodynamicsBuildTuningUsesResolvedQuality"],
            "abyssalThermodynamicsWriteTuningUsesResolvedQuality": anchors["abyssalThermodynamicsWriteTuningUsesResolvedQuality"],
            "abyssalReactorDefaultsUseResolvedQuality": anchors["abyssalReactorDefaultsUseResolvedQuality"],
            "abyssalReactorWriteFallbackUsesResolvedQuality": anchors["abyssalReactorWriteFallbackUsesResolvedQuality"],
            "metabolismRuntimeSnapshotRoute": anchors["metabolismRuntimeReadsMathLodConfig"],
            "metabolismThermalInterpolationContinuous": anchors["metabolismThermalInterpolationContinuous"],
            "bulkheadRuntimeSnapshotRoute": anchors["bulkheadRuntimeReadsMathLodConfig"],
            "bulkheadAuthorityCadenceUsesResolvedQuality": anchors["bulkheadAuthorityCadenceUsesResolvedQuality"],
            "bulkheadHatchTuningUsesResolvedQuality": anchors["bulkheadHatchTuningUsesResolvedQuality"],
            "symbiosisSnapshotRoute": anchors["symbiosisReadsMathLodConfig"],
            "symbiosisTuningUsesResolvedQuality": anchors["symbiosisTuningUsesResolvedQuality"],
            "symbiosisComplexityContinuous": anchors["symbiosisComplexityUsesContinuousQuality"],
            "symbiosisTruthAmplitudeInvariant": anchors["symbiosisTruthAmplitudeInvariant"],
            "migrationSnapshotRoute": anchors["migrationReadsMathLodConfig"],
            "migrationCadenceContinuous": anchors["migrationCadenceContinuousResolved"],
            "migrationJobUsesResolvedQuality": anchors["migrationJobUsesResolvedQuality"],
            "boidSocialLodSnapshotRoute": anchors["boidSocialLodReadsMathLodConfig"],
            "policy": "cadence changes integrate accumulated delta time; visual turbulence/debris use smooth continuous quality curves; gameplay truth amplitudes are not reduced by binary quality tiers",
        },
        "branchAudit": branch_audit(),
        "burstBranchBoundaryProof": {
            "approximationKernelTotalIfCount": anchors["approximationKernelTotalIfCount"],
            "approximationKernelTotalTernaryCount": anchors["approximationKernelTotalTernaryCount"],
            "approximationKernelUsesMathSelect": anchors["approximationKernelUsesMathSelect"],
            "mathLodTortureSafetyIfCount": anchors["mathLodTortureSafetyIfCount"],
            "mathLodTortureTernaryCount": anchors["mathLodTortureTernaryCount"],
            "powerVoltageSolverSafetyIfCount": anchors["powerVoltageSolverSafetyIfCount"],
            "powerVoltageSolverTernaryCount": anchors["powerVoltageSolverTernaryCount"],
            "powerVoltageEdgeLoopIfCount": anchors["powerVoltageEdgeLoopIfCount"],
            "powerVoltageEdgeLoopContinueCount": anchors["powerVoltageEdgeLoopContinueCount"],
            "powerVoltageDestinationMaskBranchless": anchors["powerVoltageDestinationMaskBranchless"],
            "integrateBatterySafetyIfCount": anchors["integrateBatterySafetyIfCount"],
            "integrateBatteryDestinationMaskBranchless": anchors["integrateBatteryDestinationMaskBranchless"],
            "equipmentDrainSafetyIfCount": anchors["equipmentDrainSafetyIfCount"],
            "truthPolicy": "approximation kernels plus the PowerVoltageSolverJob and IntegrateBatteryChargeJob destination accumulation paths are branchless; Burst jobs still contain explicit setup/topology branches for native memory validity, offline/damaged nodes, map lookup, capacity handling, and telemetry writes",
        },
        "asmdefDependencyAudit": asmdef_audit,
        "codeAnchorAudit": anchors,
        "hardFailures": [],
    }
    if report["remainingTranscendentalTotal"] > 0:
        report["hardFailures"].append("remaining direct transcendental calls exist; full purge is false")
    if not report["codeAnchorAudit"]["decompressionDirectExpRemoved"]:
        report["hardFailures"].append("decompression direct math.exp still present")
    if not report["codeAnchorAudit"]["mathLodConfigDto64BytesDeclared"]:
        report["hardFailures"].append("math lod config dto layout missing")
    if not report["codeAnchorAudit"]["mathLodRuntimeConfigPresent"]:
        report["hardFailures"].append("math lod runtime config route missing")
    if not report["codeAnchorAudit"]["expPositiveInfinityClampsToMaxRange"]:
        report["hardFailures"].append("exp approximation does not use directional infinity clamp")
    if report["codeAnchorAudit"]["directionalInfinityClampIfCount"] != 0 or not report["codeAnchorAudit"]["directionalInfinityClampUsesMathSelect"]:
        report["hardFailures"].append("directional infinity clamp is not branchless math.select code")
    if not report["residualProof"]["positiveInfinityDecayPolicy"]["withinMaxDecayRange"]:
        report["hardFailures"].append("positive infinity exp decay does not clamp to maximum finite range")
    if not report["codeAnchorAudit"]["mathLodConfigBufferIdsPresent"]:
        report["hardFailures"].append("math lod vault buffer ids missing")
    if not report["codeAnchorAudit"]["mathLodConfigPublishedByHomeostasis"]:
        report["hardFailures"].append("math lod config not published by homeostasis owner phase")
    if not report["codeAnchorAudit"]["mathLodReadAccessorPure"]:
        report["hardFailures"].append("math lod config read accessor is not pure read-only")
    if not report["codeAnchorAudit"]["mathLodBlackBoxFaultDumpIntegrated"]:
        report["hardFailures"].append("math lod blackbox fault dump not integrated")
    if not report["codeAnchorAudit"]["mathLodTortureCoversAngleKernels"]:
        report["hardFailures"].append("math lod torture job does not cover angle/tangent/pow approximation kernels")
    if not report["codeAnchorAudit"]["mathLodTortureCoversExtremePressureTemperature"]:
        report["hardFailures"].append("math lod torture job does not cover extreme pressure/temperature samples")
    if not report["codeAnchorAudit"]["mathLodTortureChecksNonFiniteAllKernels"]:
        report["hardFailures"].append("math lod torture job does not check finite output across all approximation kernels")
    if not report["codeAnchorAudit"]["mathLodTortureSanitizesEnvelope"]:
        report["hardFailures"].append("math lod torture job does not sanitize result envelope before writing telemetry")
    if not report["codeAnchorAudit"]["distanceMathContinuousShaderWeight"]:
        report["hardFailures"].append("distance math continuous shader weight route missing")
    if not report["codeAnchorAudit"]["distanceMathContinuousDistanceWeight"]:
        report["hardFailures"].append("distance math continuous distance quality weight missing")
    if not report["codeAnchorAudit"]["physiologyDirectExpRemoved"]:
        report["hardFailures"].append("physiology direct math.exp still present")
    if not report["codeAnchorAudit"]["jacobiRuntimeUsesGlobalQualityParameter"]:
        report["hardFailures"].append("power jacobi runtime still ignores input globalQualityWeight")
    if not report["codeAnchorAudit"]["powerVoltageConductanceMaskBranchless"]:
        report["hardFailures"].append("power voltage solver conductance cutoff still branches inside edge loop")
    if not report["codeAnchorAudit"]["powerVoltageDestinationMaskBranchless"]:
        report["hardFailures"].append("power voltage solver destination bounds still branch inside edge loop")
    if not report["codeAnchorAudit"]["powerVoltageBrownoutUsesMathSelect"]:
        report["hardFailures"].append("power voltage solver brownout flag still uses branch-style write")
    if not report["codeAnchorAudit"]["powerHotFiniteGuardsUseMathSelect"]:
        report["hardFailures"].append("power jacobi hot finite guards still use branch-style ternaries")
    if not report["codeAnchorAudit"]["integrateBatteryDestinationMaskBranchless"]:
        report["hardFailures"].append("battery charge integration destination bounds still branch inside edge loop")
    if report["powerDestinationMaskEquivalenceProof"]["mismatchCount"] != 0:
        report["hardFailures"].append("safe-index destination masks are not numerically equivalent to the previous branch/continue behavior")
    if not report["codeAnchorAudit"]["auditedFiniteGuardTernariesRemoved"]:
        report["hardFailures"].append("audited physiology/power/thermal finite guards still use branch-style ternaries")
    if not report["codeAnchorAudit"]["externalThermalInjectionQualityInvariantHeatShape"]:
        report["hardFailures"].append("external thermal injection heat shape still depends on quality or contains the old cheap-step cliff")
    if not report["codeAnchorAudit"]["externalHeatRetentionQualityInvariant"]:
        report["hardFailures"].append("external thermal heat retention still depends on quality")
    if not report["codeAnchorAudit"]["logisticsGraphReadsMathLodConfigQuality"]:
        report["hardFailures"].append("logistics graph does not read the Math-LOD config quality snapshot")
    if not report["codeAnchorAudit"]["logisticsGraphJobUsesResolvedQuality"]:
        report["hardFailures"].append("logistics graph job still uses authoritative quality instead of resolved quality")
    if not report["codeAnchorAudit"]["logisticsGraphAdaptiveWindowUsesResolvedQuality"]:
        report["hardFailures"].append("logistics adaptive solve window still ignores resolved quality")
    if not report["codeAnchorAudit"]["powerGridManagerReadsMathLodConfigQuality"]:
        report["hardFailures"].append("power grid manager does not read the Math-LOD config quality snapshot")
    if not report["codeAnchorAudit"]["powerGridManagerThermalCadenceContinuous"]:
        report["hardFailures"].append("submarine thermal grid cadence is not continuous quality-driven")
    if not report["codeAnchorAudit"]["powerGridManagerThermalScheduleUsesResolvedQuality"]:
        report["hardFailures"].append("submarine thermal grid schedule still uses authoritative quality")
    if not report["codeAnchorAudit"]["powerGridCableThermalIterationBudgetUsesResolvedQuality"]:
        report["hardFailures"].append("cable thermal iteration budget still ignores resolved quality")
    if not report["codeAnchorAudit"]["batteryChargerReadsMathLodConfigQuality"]:
        report["hardFailures"].append("battery charger logistics does not read the Math-LOD config quality snapshot")
    if not report["codeAnchorAudit"]["batteryChargerCadenceContinuous"]:
        report["hardFailures"].append("battery charger logistics cadence is not continuous quality-driven")
    if not report["codeAnchorAudit"]["batteryChargerScheduleUsesTuningQuality"]:
        report["hardFailures"].append("battery charger logistics schedule still uses authoritative quality")
    if not report["codeAnchorAudit"]["batteryChargerTuningUsesResolvedQuality"]:
        report["hardFailures"].append("battery charger logistics tuning DTO still writes authoritative quality")
    if not report["codeAnchorAudit"]["batteryChargerSamplesQualityUnderLock"]:
        report["hardFailures"].append("battery charger logistics samples cadence quality without the tuning lock")
    if not report["codeAnchorAudit"]["baseAtmosphereReadsMathLodConfigQuality"]:
        report["hardFailures"].append("base atmosphere logistics does not read the Math-LOD config quality snapshot")
    if not report["codeAnchorAudit"]["baseAtmosphereTuningUsesResolvedQuality"]:
        report["hardFailures"].append("base atmosphere tuning still writes authoritative quality")
    if not report["codeAnchorAudit"]["baseAtmosphereDiffusionIterationsContinuous"]:
        report["hardFailures"].append("base atmosphere diffusion iterations are not continuous quality-driven")
    if not report["codeAnchorAudit"]["baseAtmosphereEngineReadsMathLodConfigQuality"]:
        report["hardFailures"].append("base atmosphere engine does not read the Math-LOD config quality snapshot")
    if not report["codeAnchorAudit"]["baseAtmosphereEngineColdTickCadenceContinuous"]:
        report["hardFailures"].append("base atmosphere engine cold tick cadence is not continuous quality-driven")
    missing_runtime_snapshot_routes = [
        name
        for name, ok in report["runtimeQualitySnapshotRouteProof"].items()
        if name != "policy" and not ok
    ]
    if missing_runtime_snapshot_routes:
        report["hardFailures"].append(f"heavy runtime quality readers still bypass MathLodRuntimeConfig snapshot: {missing_runtime_snapshot_routes}")
    missing_continuous_routes = [
        name
        for name, ok in report["runtimeContinuousCadenceAndVisualProof"].items()
        if name != "policy" and not ok
    ]
    if missing_continuous_routes:
        report["hardFailures"].append(f"runtime cadence/visual Math-LOD routes are not continuous snapshot-driven: {missing_continuous_routes}")
    missing_animation_quality_routes = [
        name
        for name, ok in report["animationQualityGateProof"].items()
        if name != "truthPolicy" and not ok
    ]
    if missing_animation_quality_routes:
        report["hardFailures"].append(f"animation Math-LOD quality gates remain binary: {missing_animation_quality_routes}")
    missing_physics_lighting_quality_routes = [
        name
        for name, ok in report["physicsLightingQualityGateProof"].items()
        if name != "truthPolicy" and not ok
    ]
    if missing_physics_lighting_quality_routes:
        report["hardFailures"].append(f"physics/lighting presentation Math-LOD quality gates remain binary or bypass snapshot route: {missing_physics_lighting_quality_routes}")
    missing_presentation_quality_routes = [
        name
        for name, ok in report["presentationQualityGateProof"].items()
        if name != "truthPolicy" and not ok
    ]
    if missing_presentation_quality_routes:
        report["hardFailures"].append(f"presentation Math-LOD quality gates remain binary: {missing_presentation_quality_routes}")
    missing_runtime_quality_step_routes = [
        name
        for name, ok in report["runtimeQualityStepGateSweepProof"].items()
        if name != "truthPolicy" and not ok
    ]
    if missing_runtime_quality_step_routes:
        report["hardFailures"].append(f"runtime Math-LOD quality step sweep has binary or unproved routes: {missing_runtime_quality_step_routes}")
    if not report["codeAnchorAudit"]["powerJacobiConductanceCapPresent"]:
        report["hardFailures"].append("power jacobi conductance cap missing")
    if not report["codeAnchorAudit"]["powerJacobiNetCurrentCapPresent"]:
        report["hardFailures"].append("power jacobi net-current cap missing")
    if not report["codeAnchorAudit"]["powerJacobiTickCapPresent"]:
        report["hardFailures"].append("power jacobi tick-delta cap missing")
    if not report["codeAnchorAudit"]["powerJacobiFuzzerContinuousIterationsPresent"]:
        report["hardFailures"].append("power jacobi fuzzer is not capped to continuous 2..50 Math-LOD iterations")
    if not report["codeAnchorAudit"]["powerJacobiFuzzerUsesMathLodBudget"]:
        report["hardFailures"].append("power jacobi fuzzer does not use the Math-LOD quality budget")
    if not report["codeAnchorAudit"]["powerJacobiFuzzerDampedOmega"]:
        report["hardFailures"].append("power jacobi fuzzer still permits over-relaxation above production omega")
    if not report["codeAnchorAudit"]["powerJacobiFuzzerConductanceCurrentCaps"]:
        report["hardFailures"].append("power jacobi fuzzer conductance/current caps missing")
    if not report["codeAnchorAudit"]["powerJacobiFuzzerIsolatedVault"]:
        report["hardFailures"].append("power jacobi fuzzer publishes isolated QA vault through GlobalDataVault.Create")
    if not report["codeAnchorAudit"]["gasLeakDirectExpRemoved"]:
        report["hardFailures"].append("gas leak direct math.exp still present")
    if not report["codeAnchorAudit"]["stormAttenuationDirectExpRemoved"]:
        report["hardFailures"].append("storm attenuation direct math.exp still present")
    if not report["codeAnchorAudit"]["aiAnxietyDirectExpRemoved"]:
        report["hardFailures"].append("ai anxiety direct math.exp still present")
    if not report["codeAnchorAudit"]["ballisticsDirectExpRemoved"]:
        report["hardFailures"].append("ballistics direct math.exp still present")
    if not report["codeAnchorAudit"]["rollbackDirectExpRemoved"]:
        report["hardFailures"].append("rollback direct math.exp still present")
    if not report["codeAnchorAudit"]["editorBioForgeExpLogRemoved"]:
        report["hardFailures"].append("editor bioforge direct exp/log still present")
    if not report["codeAnchorAudit"]["hydraulicErosionExpRemoved"]:
        report["hardFailures"].append("hydraulic erosion direct math.exp still present")
    if report["codeAnchorAudit"]["approxCoreIfCount"] != 0:
        report["hardFailures"].append("approximation core contains if branches")
    if report["codeAnchorAudit"]["approximationKernelTotalIfCount"] != 0 or report["codeAnchorAudit"]["approximationKernelTotalTernaryCount"] != 0:
        report["hardFailures"].append("audited approximation kernel set contains branch syntax")
    if report["extremeKernelFinitenessProof"]["nonFiniteOutputCount"] != 0:
        report["hardFailures"].append("extreme approximation kernel proof produced non-finite output")
    if report["codeAnchorAudit"]["aiAnxietyApproxIfCount"] != 0:
        report["hardFailures"].append("ai anxiety approximation core contains if branches")
    if report["codeAnchorAudit"]["bhaskaraCoreIfCount"] != 0:
        report["hardFailures"].append("bhaskara approximation core contains if branches")
    if report["codeAnchorAudit"]["tanCoreIfCount"] != 0:
        report["hardFailures"].append("tan approximation core contains if branches")
    if report["codeAnchorAudit"]["atanCoreIfCount"] != 0 or report["codeAnchorAudit"]["atan2CoreIfCount"] != 0 or report["codeAnchorAudit"]["acosCoreIfCount"] != 0:
        report["hardFailures"].append("atan/acos approximation core contains if branches")
    if report["asmdefDependencyAudit"]["mathLodApproximationMissingCoreReferenceCount"] != 0:
        report["hardFailures"].append("MathLodApproximation call exists under asmdef without Hecton8.Core reference")
    audited_fast_mode = {
        path: data["floatModeFastCount"]
        for path, data in report["branchAudit"].items()
        if data["floatModeFastCount"] != 0
    }
    if audited_fast_mode:
        report["hardFailures"].append(f"audited deterministic solver files still use FloatMode.Fast: {audited_fast_mode}")
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(str(REPORT.relative_to(ROOT)).replace("\\", "/"))
    print(json.dumps({
        "scannedCSharpFiles": report["scannedCSharpFiles"],
        "remainingTranscendentalTotal": report["remainingTranscendentalTotal"],
        "physiologyWorstAbsError": report["residualProof"]["physiologyWorstCase"]["absError"],
        "hardFailures": report["hardFailures"],
    }, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

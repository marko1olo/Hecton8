from __future__ import annotations

import datetime as dt
import json
import math
import pathlib
import statistics
import sys
from collections import defaultdict

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt


PROJECT = pathlib.Path(r"C:\hades\Hecton8")
TOOLS = PROJECT / "Tools"
sys.path.insert(0, str(TOOLS))

import ProjectMetricsDashboard_20260528 as base


SAMARA = dt.timezone(dt.timedelta(hours=4))
REPORT_DATE = dt.datetime.now(SAMARA).date().isoformat()
TOKEN_REPORT_DIR = PROJECT / "Docs" / "DEPRECATED" / "Root_Docs_Noise_2026-05-26"
TOKEN_JSON = TOKEN_REPORT_DIR / f"TOKEN_USAGE_AUDIT_{REPORT_DATE}.json"
REPORT_DIR = PROJECT / "Docs" / "Reports"
CHART_DIR = REPORT_DIR / "MetricChartsDeep" / REPORT_DATE
DEEP_JSON = REPORT_DIR / f"TOKEN_USAGE_DEEP_ANALYTICS_{REPORT_DATE}.json"
DEEP_MD = REPORT_DIR / f"TOKEN_USAGE_DEEP_ANALYTICS_{REPORT_DATE}.md"

USAGE_KEYS = ("input_tokens", "cached_input_tokens", "output_tokens", "reasoning_output_tokens", "total_tokens")


def read_json(path: pathlib.Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def fmt_int(value: float | int | None) -> str:
    return f"{int(value or 0):,}"


def fmt_float(value: float | int | None, digits: int = 2) -> str:
    return f"{float(value or 0):,.{digits}f}"


def fmt_money(value: float | int | None) -> str:
    return f"${float(value or 0):,.2f}"


def safe_div(a: float | int | None, b: float | int | None, default: float = 0.0) -> float:
    b = float(b or 0)
    if b == 0:
        return default
    return float(a or 0) / b


def parse_hour(period: str) -> dt.datetime:
    return dt.datetime.strptime(period, "%Y-%m-%d %H:00").replace(tzinfo=SAMARA)


def parse_day(period: str) -> dt.datetime:
    return dt.datetime.strptime(period, "%Y-%m-%d").replace(tzinfo=SAMARA)


def hour_key(value: dt.datetime) -> str:
    return value.strftime("%Y-%m-%d %H:00")


def day_key(value: dt.datetime) -> str:
    return value.date().isoformat()


def enrich(row: dict, cost_usd: float | None = None) -> dict:
    base.enrich_usage_row(row, cost_usd)
    output = int(row.get("output_tokens", 0) or 0)
    total = int(row.get("total_tokens", 0) or 0)
    cost = float(row.get("cost_usd", 0) or 0)
    uncached = int(row.get("uncached_input_tokens", 0) or 0)
    cached = int(row.get("cached_input_tokens", 0) or 0)
    input_tokens = int(row.get("input_tokens", 0) or 0)
    reasoning = int(row.get("reasoning_output_tokens", 0) or 0)
    row["cost_per_1m_output_tokens"] = safe_div(cost * 1_000_000, output)
    row["tokens_per_usd"] = safe_div(total, cost)
    row["output_per_1m_input_tokens"] = safe_div(output * 1_000_000, input_tokens)
    row["output_per_1m_uncached_tokens"] = safe_div(output * 1_000_000, uncached)
    row["reasoning_per_1m_total_tokens"] = safe_div(reasoning * 1_000_000, total)
    row["cached_per_uncached_input"] = safe_div(cached, uncached)
    row["waste_pressure"] = safe_div(input_tokens, max(output, 1)) * (1.0 - min(1.0, row.get("cache_ratio", 0) or 0))
    row["long_context_upper_cost_usd"] = (uncached * 10.0 + cached * 1.0 + output * 45.0) / 1_000_000
    row["long_context_regional_upper_cost_usd"] = row["long_context_upper_cost_usd"] * 1.10
    row["regional_10pct_cost_usd"] = cost * 1.10
    return row


def fill_hourly(rows: list[dict]) -> list[dict]:
    if not rows:
        return []
    by_period = {row["period"]: row for row in rows}
    start = parse_hour(rows[0]["period"])
    end = parse_hour(rows[-1]["period"])
    filled = []
    cursor = start
    while cursor <= end:
        key = hour_key(cursor)
        if key in by_period:
            filled.append(by_period[key])
        else:
            missing = {"period": key, **{usage_key: 0 for usage_key in USAGE_KEYS}}
            filled.append(enrich(missing, 0.0))
        cursor += dt.timedelta(hours=1)
    return filled


def aggregate_hours(rows: list[dict], bucket_hours: int, label_suffix: str) -> list[dict]:
    buckets: dict[dt.datetime, dict] = {}
    costs: defaultdict[dt.datetime, float] = defaultdict(float)
    for row in rows:
        stamp = parse_hour(row["period"])
        start = stamp.replace(hour=(stamp.hour // bucket_hours) * bucket_hours)
        if start not in buckets:
            buckets[start] = {"period": f"{hour_key(start)} {label_suffix}", **{usage_key: 0 for usage_key in USAGE_KEYS}}
        for usage_key in USAGE_KEYS:
            buckets[start][usage_key] += int(row.get(usage_key, 0) or 0)
        costs[start] += float(row.get("cost_usd", 0) or 0)
    return [enrich(buckets[key], costs[key]) for key in sorted(buckets)]


def ensure_daily(rows: list[dict]) -> list[dict]:
    if not rows:
        return []
    by_period = {row["period"]: row for row in rows}
    start = parse_day(rows[0]["period"])
    end = parse_day(rows[-1]["period"])
    filled = []
    cursor = start
    while cursor <= end:
        key = day_key(cursor)
        if key in by_period:
            filled.append(by_period[key])
        else:
            missing = {"period": key, **{usage_key: 0 for usage_key in USAGE_KEYS}}
            filled.append(enrich(missing, 0.0))
        cursor += dt.timedelta(days=1)
    return filled


def window_rows(rows: list[dict], count: int | None) -> list[dict]:
    if count is None or count >= len(rows):
        return list(rows)
    return list(rows[-count:])


def ema(values: list[float], alpha: float) -> list[float]:
    out = []
    current = None
    for value in values:
        if current is None:
            current = value
        else:
            current = alpha * value + (1.0 - alpha) * current
        out.append(current)
    return out


def rolling_median(values: list[float], width: int) -> list[float]:
    if width <= 1:
        return list(values)
    half = width // 2
    out = []
    for index in range(len(values)):
        lo = max(0, index - half)
        hi = min(len(values), index + half + 1)
        out.append(statistics.median(values[lo:hi]))
    return out


def rolling_mean(values: list[float], width: int) -> list[float]:
    if width <= 1:
        return list(values)
    out = []
    for index in range(len(values)):
        lo = max(0, index - width + 1)
        subset = values[lo:index + 1]
        out.append(sum(subset) / len(subset))
    return out


def sample_ticks(count: int, max_ticks: int = 12) -> list[int]:
    if count <= 0:
        return []
    step = max(1, math.ceil(count / max_ticks))
    ticks = list(range(0, count, step))
    if ticks[-1] != count - 1:
        ticks.append(count - 1)
    return ticks


def save_chart(charts: list[dict], name: str, title: str, description: str, group: str) -> pathlib.Path:
    CHART_DIR.mkdir(parents=True, exist_ok=True)
    path = CHART_DIR / f"{name}.png"
    plt.tight_layout()
    plt.savefig(path, dpi=115)
    plt.close()
    charts.append({
        "name": name,
        "title": title,
        "group": group,
        "description": description,
        "path": str(path.relative_to(REPORT_DIR)).replace("\\", "/"),
    })
    return path


def configure_axis(ax, rows: list[dict], title: str, ylabel: str, max_ticks: int = 12) -> None:
    ax.set_title(title, fontsize=12, pad=10)
    ax.set_ylabel(ylabel)
    ax.grid(True, alpha=0.22)
    ticks = sample_ticks(len(rows), max_ticks)
    ax.set_xticks(ticks)
    ax.set_xticklabels([str(rows[index].get("period", "")) for index in ticks], rotation=45, ha="right", fontsize=7)


METRICS = [
    ("total_tokens", "total tokens", 1_000_000, "million tokens", "#2563eb"),
    ("input_tokens", "input tokens", 1_000_000, "million tokens", "#0f766e"),
    ("cached_input_tokens", "cached input tokens", 1_000_000, "million tokens", "#16a34a"),
    ("uncached_input_tokens", "uncached input tokens", 1_000_000, "million tokens", "#f97316"),
    ("output_tokens", "output tokens", 1_000, "thousand tokens", "#1d4ed8"),
    ("reasoning_output_tokens", "reasoning output tokens", 1_000, "thousand tokens", "#9333ea"),
    ("cost_usd", "GPT-5.5 standard cost", 1, "USD", "#16a34a"),
    ("cost_no_cache_usd", "no-cache cost", 1, "USD", "#dc2626"),
    ("cache_savings_usd", "cache savings", 1, "USD saved", "#059669"),
    ("long_context_upper_cost_usd", "long-context upper cost", 1, "USD", "#be123c"),
    ("effective_usd_per_1m_total_tokens", "effective cost per total token", 1, "USD / 1M total tokens", "#0f766e"),
    ("cost_per_1m_output_tokens", "cost per output token", 1, "USD / 1M output tokens", "#7c2d12"),
    ("tokens_per_usd", "tokens per dollar", 1_000_000, "million tokens / USD", "#0891b2"),
    ("cache_ratio", "cache ratio", 0.01, "percent", "#16a34a"),
    ("output_ratio", "output ratio", 0.01, "percent", "#2563eb"),
    ("reasoning_ratio", "reasoning/output ratio", 0.01, "percent", "#9333ea"),
    ("output_cost_share", "output cost share", 0.01, "percent", "#be123c"),
    ("output_per_1m_input_tokens", "output per input", 1, "output tokens / 1M input tokens", "#1d4ed8"),
    ("reasoning_per_1m_total_tokens", "reasoning per total", 1, "reasoning tokens / 1M total tokens", "#9333ea"),
    ("printed_pages_500w", "human-scale pages", 1_000, "thousand 500-word pages", "#7c2d12"),
]


def line_smooth_chart(charts: list[dict], rows: list[dict], period_id: str, window_id: str, metric: tuple, median_width: int, alpha: float) -> None:
    key, label, scale, ylabel, color = metric
    if not rows:
        return
    values = [float(row.get(key, 0) or 0) / scale for row in rows]
    xs = list(range(len(values)))
    fig, ax = plt.subplots(figsize=(13.5, 6))
    ax.plot(xs, values, color=color, linewidth=1.0, alpha=0.35, label="raw")
    ax.plot(xs, rolling_median(values, median_width), color="#111827", linewidth=1.7, alpha=0.85, label=f"rolling median {median_width}")
    ax.plot(xs, ema(values, alpha), color=color, linewidth=2.4, alpha=0.95, label=f"EMA {alpha:.2f}")
    if values:
        peak = max(range(len(values)), key=lambda index: values[index])
        ax.scatter([peak], [values[peak]], color="#dc2626", s=36, zorder=5)
        ax.annotate(
            f"peak {values[peak]:,.2f}",
            xy=(peak, values[peak]),
            xytext=(0, 12),
            textcoords="offset points",
            ha="center",
            fontsize=8,
            bbox={"boxstyle": "round,pad=0.25", "facecolor": "white", "edgecolor": "#94a3b8", "alpha": 0.88},
        )
    title = f"{period_id} {window_id} {label}: raw vs smoothed"
    configure_axis(ax, rows, title, ylabel)
    ax.legend(loc="upper left", fontsize=8)
    name = f"{period_id}_{window_id}_{key}_raw_median_ema"
    save_chart(charts, name, title, "Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.", "time_series")


def stack_chart(charts: list[dict], rows: list[dict], period_id: str, window_id: str) -> None:
    if not rows:
        return
    xs = list(range(len(rows)))
    series = [
        ("uncached_input_tokens", "uncached input", "#f97316"),
        ("cached_input_tokens", "cached input", "#22c55e"),
        ("output_tokens", "output", "#3b82f6"),
    ]
    values = [[float(row.get(key, 0) or 0) / 1_000_000 for row in rows] for key, _label, _color in series]
    fig, ax = plt.subplots(figsize=(13.5, 6))
    ax.stackplot(xs, values, labels=[label for _key, label, _color in series], colors=[color for _key, _label, color in series], alpha=0.88)
    title = f"{period_id} {window_id} input/output composition"
    configure_axis(ax, rows, title, "million tokens")
    ax.legend(loc="upper left", fontsize=8)
    save_chart(charts, f"{period_id}_{window_id}_io_composition_stack", title, "Composition stack separates cached, uncached, and output token load.", "composition")


def ratio_chart(charts: list[dict], rows: list[dict], period_id: str, window_id: str) -> None:
    if not rows:
        return
    xs = list(range(len(rows)))
    series = [
        ("cache_ratio", "cache ratio", "#16a34a"),
        ("output_ratio", "output / total", "#2563eb"),
        ("reasoning_ratio", "reasoning / output", "#9333ea"),
        ("output_cost_share", "output cost share", "#be123c"),
    ]
    fig, ax = plt.subplots(figsize=(13.5, 6))
    for key, label, color in series:
        values = [float(row.get(key, 0) or 0) / 0.01 for row in rows]
        ax.plot(xs, ema(values, 0.32), color=color, linewidth=2.0, label=label)
    title = f"{period_id} {window_id} quality ratios"
    configure_axis(ax, rows, title, "percent")
    ax.legend(loc="upper left", fontsize=8)
    save_chart(charts, f"{period_id}_{window_id}_ratio_pack", title, "Smoothed ratios show cache health, output yield, reasoning pressure, and output cost share.", "ratio_pack")


def cost_band_chart(charts: list[dict], rows: list[dict], period_id: str, window_id: str) -> None:
    if not rows:
        return
    xs = list(range(len(rows)))
    series = [
        ("cost_usd", "base cached", "#16a34a"),
        ("cost_no_cache_usd", "no-cache", "#dc2626"),
        ("long_context_upper_cost_usd", "long-context upper", "#be123c"),
        ("long_context_regional_upper_cost_usd", "long-context + regional upper", "#7f1d1d"),
    ]
    fig, ax = plt.subplots(figsize=(13.5, 6))
    for key, label, color in series:
        values = [float(row.get(key, 0) or 0) for row in rows]
        ax.plot(xs, ema(values, 0.32), color=color, linewidth=2.0, label=label)
    title = f"{period_id} {window_id} cost sensitivity bands"
    configure_axis(ax, rows, title, "USD")
    ax.legend(loc="upper left", fontsize=8)
    save_chart(charts, f"{period_id}_{window_id}_cost_sensitivity_bands", title, "Sensitivity bands are API-equivalent approximations, not invoice proof.", "cost_bands")


def efficiency_chart(charts: list[dict], rows: list[dict], period_id: str, window_id: str) -> None:
    if not rows:
        return
    xs = list(range(len(rows)))
    series = [
        ("cost_per_1m_output_tokens", "USD / 1M output", "#7c2d12", 1),
        ("output_per_1m_input_tokens", "output / 1M input", "#2563eb", 1),
        ("reasoning_per_1m_total_tokens", "reasoning / 1M total", "#9333ea", 1),
    ]
    fig, ax = plt.subplots(figsize=(13.5, 6))
    for key, label, color, scale in series:
        values = [float(row.get(key, 0) or 0) / scale for row in rows]
        ax.plot(xs, ema(values, 0.32), color=color, linewidth=2.0, label=label)
    title = f"{period_id} {window_id} efficiency pack"
    configure_axis(ax, rows, title, "raw metric units")
    ax.legend(loc="upper left", fontsize=8)
    save_chart(charts, f"{period_id}_{window_id}_efficiency_pack", title, "Efficiency pack compares output yield, reasoning load, and cost per output token.", "efficiency")


def outlier_chart(charts: list[dict], rows: list[dict], period_id: str, metric: tuple, top_n: int = 16) -> None:
    key, label, scale, ylabel, color = metric
    ranked = sorted(rows, key=lambda row: float(row.get(key, 0) or 0), reverse=True)[:top_n]
    if not ranked:
        return
    values = [float(row.get(key, 0) or 0) / scale for row in ranked]
    labels = [str(row.get("period", "")) for row in ranked]
    fig, ax = plt.subplots(figsize=(12, 6))
    ax.bar(range(len(ranked)), values, color=color)
    ax.set_xticks(range(len(ranked)))
    ax.set_xticklabels(labels, rotation=55, ha="right", fontsize=8)
    ax.set_ylabel(ylabel)
    ax.set_title(f"{period_id} top {top_n} outliers by {label}", fontsize=12, pad=10)
    ax.grid(True, axis="y", alpha=0.22)
    save_chart(charts, f"{period_id}_outliers_{key}_top{top_n}", f"{period_id} outliers by {label}", "Ranks highest buckets by raw metric value; no smoothing is applied.", "outliers")


def histogram_chart(charts: list[dict], rows: list[dict], period_id: str, metric: tuple) -> None:
    key, label, scale, ylabel, color = metric
    values = [float(row.get(key, 0) or 0) / scale for row in rows if float(row.get(key, 0) or 0) > 0]
    if len(values) < 2:
        return
    bins = min(24, max(8, int(math.sqrt(len(values)) * 2)))
    fig, ax = plt.subplots(figsize=(11, 5.5))
    ax.hist(values, bins=bins, color=color, alpha=0.82, edgecolor="#0f172a", linewidth=0.4)
    ax.set_title(f"{period_id} distribution: {label}", fontsize=12, pad=10)
    ax.set_xlabel(ylabel)
    ax.set_ylabel("bucket count")
    ax.grid(True, axis="y", alpha=0.22)
    save_chart(charts, f"{period_id}_distribution_{key}", f"{period_id} distribution of {label}", "Distribution uses non-zero period buckets only.", "distributions")


def log_histogram_chart(charts: list[dict], rows: list[dict], period_id: str, metric: tuple) -> None:
    key, label, scale, ylabel, color = metric
    values = [float(row.get(key, 0) or 0) / scale for row in rows if float(row.get(key, 0) or 0) > 0]
    if len(values) < 2:
        return
    fig, ax = plt.subplots(figsize=(11, 5.5))
    ax.hist(values, bins=min(24, max(8, int(math.sqrt(len(values)) * 2))), color=color, alpha=0.82, edgecolor="#0f172a", linewidth=0.4)
    ax.set_xscale("log")
    ax.set_title(f"{period_id} log distribution: {label}", fontsize=12, pad=10)
    ax.set_xlabel(f"{ylabel} log scale")
    ax.set_ylabel("bucket count")
    ax.grid(True, axis="y", alpha=0.22)
    save_chart(charts, f"{period_id}_log_distribution_{key}", f"{period_id} log distribution of {label}", "Log-scale distribution preserves outlier visibility without clipping peaks.", "distributions")


def heatmap(charts: list[dict], rows: list[dict], period_id: str, metric: tuple, parser, row_label_func, col_func, col_labels: list[str]) -> None:
    key, label, scale, _ylabel, _color = metric
    labels = []
    label_index = {}
    matrix = []
    for row in rows:
        try:
            stamp = parser(row["period"].split(" +")[0])
        except Exception:
            continue
        row_label = row_label_func(stamp)
        if row_label not in label_index:
            label_index[row_label] = len(labels)
            labels.append(row_label)
            matrix.append([0.0 for _ in col_labels])
        col = col_func(stamp)
        if 0 <= col < len(col_labels):
            matrix[label_index[row_label]][col] += float(row.get(key, 0) or 0) / scale
    if not matrix:
        return
    fig, ax = plt.subplots(figsize=(13, 6))
    im = ax.imshow(matrix, aspect="auto", cmap="magma")
    ax.set_title(f"{period_id} heatmap: {label}", fontsize=12, pad=10)
    ax.set_xticks(range(len(col_labels)))
    ax.set_xticklabels(col_labels, fontsize=8)
    ax.set_yticks(range(len(labels)))
    ax.set_yticklabels(labels, fontsize=8)
    fig.colorbar(im, ax=ax)
    save_chart(charts, f"{period_id}_heatmap_{key}", f"{period_id} heatmap of {label}", "Heatmap aggregates local Samara time buckets.", "heatmaps")


def pareto_chart(charts: list[dict], rows: list[dict], label_key: str, value_key: str, title: str, name: str, scale: float, ylabel: str) -> None:
    ranked = sorted(rows, key=lambda row: float(row.get(value_key, 0) or 0), reverse=True)
    if not ranked:
        return
    values = [float(row.get(value_key, 0) or 0) / scale for row in ranked]
    total = sum(values)
    if total <= 0:
        return
    cumulative = []
    acc = 0.0
    for value in values:
        acc += value
        cumulative.append(acc / total * 100.0)
    labels = [str(row.get(label_key, ""))[:14] for row in ranked]
    fig, ax1 = plt.subplots(figsize=(13, 6))
    ax1.bar(range(len(ranked)), values, color="#2563eb", alpha=0.82)
    ax1.set_ylabel(ylabel)
    ax1.set_xticks(range(len(ranked)))
    ax1.set_xticklabels(labels, rotation=55, ha="right", fontsize=8)
    ax2 = ax1.twinx()
    ax2.plot(range(len(ranked)), cumulative, color="#dc2626", linewidth=2.2, marker="o", markersize=3)
    ax2.set_ylabel("cumulative percent")
    ax2.set_ylim(0, 105)
    ax1.set_title(title, fontsize=12, pad=10)
    ax1.grid(True, axis="y", alpha=0.22)
    save_chart(charts, name, title, "Pareto chart is limited to rows present in the source report.", "pareto")


def forecast_charts(charts: list[dict], report: dict, daily_rows: list[dict], hourly_rows: list[dict]) -> dict:
    velocity = ((report.get("previous_snapshot_delta") or {}).get("velocity") or {})
    current_per_day = float(velocity.get("total_tokens_per_day", 0) or 0)
    current_cost_per_day = float(velocity.get("gpt_5_5_standard_usd_per_day", 0) or 0)
    daily_token_avg_7 = sum(row.get("total_tokens", 0) for row in daily_rows[-7:]) / max(1, len(daily_rows[-7:]))
    daily_token_avg_30 = sum(row.get("total_tokens", 0) for row in daily_rows[-30:]) / max(1, len(daily_rows[-30:]))
    daily_cost_avg_7 = sum(row.get("cost_usd", 0) for row in daily_rows[-7:]) / max(1, len(daily_rows[-7:]))
    daily_cost_avg_30 = sum(row.get("cost_usd", 0) for row in daily_rows[-30:]) / max(1, len(daily_rows[-30:]))
    projections = [
        ("24h", 1),
        ("7d", 7),
        ("30d", 30),
    ]
    rows = []
    for label, days in projections:
        rows.append({
            "label": label,
            "current_tokens": current_per_day * days,
            "avg_7d_tokens": daily_token_avg_7 * days,
            "avg_30d_tokens": daily_token_avg_30 * days,
            "current_cost": current_cost_per_day * days,
            "avg_7d_cost": daily_cost_avg_7 * days,
            "avg_30d_cost": daily_cost_avg_30 * days,
        })
    for value_kind, ylabel, scale in (("tokens", "billion tokens", 1_000_000_000), ("cost", "USD", 1)):
        fig, ax = plt.subplots(figsize=(10.5, 5.5))
        x = list(range(len(rows)))
        width = 0.24
        keys = [f"current_{value_kind}", f"avg_7d_{value_kind}", f"avg_30d_{value_kind}"]
        labels = ["current velocity", "7d average", "30d average"]
        colors = ["#dc2626", "#2563eb", "#16a34a"]
        for offset, key in enumerate(keys):
            ax.bar([i + (offset - 1) * width for i in x], [row[key] / scale for row in rows], width=width, label=labels[offset], color=colors[offset])
        ax.set_xticks(x)
        ax.set_xticklabels([row["label"] for row in rows])
        ax.set_ylabel(ylabel)
        ax.set_title(f"Forecast fan: {value_kind}", fontsize=12, pad=10)
        ax.grid(True, axis="y", alpha=0.22)
        ax.legend(fontsize=8)
        save_chart(charts, f"forecast_fan_{value_kind}", f"Forecast fan: {value_kind}", "Forecast compares current snapshot velocity with 7-day and 30-day averages.", "forecast")
    return {
        "current_tokens_per_day": current_per_day,
        "current_cost_per_day": current_cost_per_day,
        "daily_token_avg_7": daily_token_avg_7,
        "daily_token_avg_30": daily_token_avg_30,
        "daily_cost_avg_7": daily_cost_avg_7,
        "daily_cost_avg_30": daily_cost_avg_30,
        "projections": rows,
    }


def build_rows(report: dict) -> dict[str, list[dict]]:
    hourly = fill_hourly(base.period_rows(report, "hourly", "hourly_gpt_5_5_standard_costs_usd"))
    four_hour = aggregate_hours(hourly, 4, "+4h")
    twelve_hour = aggregate_hours(hourly, 12, "+12h")
    daily = ensure_daily(base.period_rows(report, "daily", "daily_gpt_5_5_standard_costs_usd"))
    return {"1h": hourly, "4h": four_hour, "12h": twelve_hour, "1d": daily}


def generate_charts(report: dict, rows_by_period: dict[str, list[dict]]) -> tuple[list[dict], dict]:
    charts: list[dict] = []
    period_windows = {
        "1h": [("all", None), ("last24h", 24), ("last48h", 48), ("last72h", 72), ("last96h", 96)],
        "4h": [("all", None), ("last24h", 6), ("last48h", 12), ("last72h", 18), ("last120h", 30)],
        "12h": [("all", None), ("last48h", 4), ("last72h", 6), ("last120h", 10)],
        "1d": [("all", None), ("last7d", 7), ("last14d", 14), ("last30d", 30), ("last60d", 60)],
    }
    smoothing = {
        "1h": (5, 0.28),
        "4h": (3, 0.32),
        "12h": (3, 0.36),
        "1d": (3, 0.40),
    }
    for period_id, windows in period_windows.items():
        rows = rows_by_period[period_id]
        median_width, alpha = smoothing[period_id]
        for window_id, count in windows:
            scoped = window_rows(rows, count)
            for metric in METRICS:
                line_smooth_chart(charts, scoped, period_id, window_id, metric, median_width, alpha)
            stack_chart(charts, scoped, period_id, window_id)
            ratio_chart(charts, scoped, period_id, window_id)
            cost_band_chart(charts, scoped, period_id, window_id)
            efficiency_chart(charts, scoped, period_id, window_id)

    outlier_metrics = [metric for metric in METRICS if metric[0] in {
        "total_tokens",
        "input_tokens",
        "uncached_input_tokens",
        "output_tokens",
        "reasoning_output_tokens",
        "cost_usd",
        "cost_no_cache_usd",
        "cache_savings_usd",
        "effective_usd_per_1m_total_tokens",
        "cost_per_1m_output_tokens",
    }]
    distribution_metrics = [metric for metric in METRICS if metric[0] in {
        "total_tokens",
        "output_tokens",
        "reasoning_output_tokens",
        "cost_usd",
        "cache_savings_usd",
        "effective_usd_per_1m_total_tokens",
        "cache_ratio",
        "output_ratio",
        "reasoning_ratio",
        "cost_per_1m_output_tokens",
    }]
    for period_id, rows in rows_by_period.items():
        for metric in outlier_metrics:
            outlier_chart(charts, rows, period_id, metric)
        for metric in distribution_metrics:
            histogram_chart(charts, rows, period_id, metric)
            log_histogram_chart(charts, rows, period_id, metric)

    heat_metrics = [metric for metric in METRICS if metric[0] in {
        "total_tokens",
        "output_tokens",
        "reasoning_output_tokens",
        "cost_usd",
        "cache_ratio",
        "cost_per_1m_output_tokens",
    }]
    for metric in heat_metrics:
        heatmap(
            charts,
            rows_by_period["1h"],
            "1h_day_hour",
            metric,
            parse_hour,
            lambda stamp: stamp.strftime("%m-%d"),
            lambda stamp: stamp.hour,
            [str(hour) for hour in range(24)],
        )
        heatmap(
            charts,
            rows_by_period["1h"],
            "1h_weekday_hour",
            metric,
            parse_hour,
            lambda stamp: ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"][stamp.weekday()],
            lambda stamp: stamp.hour,
            [str(hour) for hour in range(24)],
        )
        heatmap(
            charts,
            rows_by_period["4h"],
            "4h_day_slot",
            metric,
            parse_hour,
            lambda stamp: stamp.strftime("%m-%d"),
            lambda stamp: stamp.hour // 4,
            ["00", "04", "08", "12", "16", "20"],
        )
        heatmap(
            charts,
            rows_by_period["12h"],
            "12h_day_slot",
            metric,
            parse_hour,
            lambda stamp: stamp.strftime("%m-%d"),
            lambda stamp: stamp.hour // 12,
            ["00", "12"],
        )

    top_session_sets = [
        ("top_sessions", "total_tokens", "Top sessions by total tokens", "top_sessions_total_tokens_pareto", 1_000_000, "million tokens"),
        ("top_sessions", "gpt_5_5_standard_cost_usd", "Top sessions by GPT-5.5 standard cost", "top_sessions_cost_pareto", 1, "USD"),
        ("top_output_sessions", "output_tokens", "Top sessions by output tokens", "top_sessions_output_pareto", 1_000, "thousand output tokens"),
        ("top_reasoning_sessions", "reasoning_output_tokens", "Top sessions by reasoning output", "top_sessions_reasoning_pareto", 1_000, "thousand reasoning tokens"),
    ]
    for report_key, value_key, title, name, scale, ylabel in top_session_sets:
        rows = report.get(report_key) or []
        compact = [{"label": str(row.get("session_id", ""))[:8], **row} for row in rows]
        pareto_chart(charts, compact, "label", value_key, title, name, scale, ylabel)

    for report_key, title, name in [
        ("model_effort_delta_standard_cost_rows", "Model+effort Pareto by cost", "model_effort_delta_cost_pareto"),
        ("model_effort_final_standard_cost_rows", "Final model+effort Pareto by cost", "model_effort_final_cost_pareto"),
    ]:
        rows = report.get(report_key) or []
        compact = [{"label": row.get("key") or row.get("model_effort") or row.get("model") or "unknown", **row} for row in rows]
        value_key = "gpt_5_5_standard_cost_usd" if compact and "gpt_5_5_standard_cost_usd" in compact[0] else "cost_usd"
        if compact and value_key in compact[0]:
            pareto_chart(charts, compact, "label", value_key, title, name, 1, "USD")

    forecast = forecast_charts(charts, report, rows_by_period["1d"], rows_by_period["1h"])
    return charts, forecast


def markdown(report: dict, charts: list[dict], rows_by_period: dict[str, list[dict]], forecast: dict) -> str:
    totals = report["totals"]
    primary = report["pricing"][report["primary_price_key"]]
    groups = defaultdict(list)
    for chart in charts:
        groups[chart["group"]].append(chart)
    lines = [
        f"# Token Usage Deep Analytics {REPORT_DATE}",
        "",
        f"Generated Samara: `{dt.datetime.now(SAMARA).isoformat()}`",
        "Evidence class: STATIC_LOCAL_JSONL_REPORT_DERIVED. This is not provider invoice proof.",
        "",
        "## Headline",
        "",
        "| Metric | Value |",
        "|---|---:|",
        f"| Total tokens | {fmt_int(totals['total_tokens'])} |",
        f"| Input tokens | {fmt_int(totals['input_tokens'])} |",
        f"| Cached input tokens | {fmt_int(totals['cached_input_tokens'])} |",
        f"| Output tokens | {fmt_int(totals['output_tokens'])} |",
        f"| Reasoning output tokens | {fmt_int(totals['reasoning_output_tokens'])} |",
        f"| GPT-5.5 standard API-equivalent | {fmt_money(primary['total_cost_usd'])} |",
        f"| 1h buckets | {len(rows_by_period['1h'])} |",
        f"| 4h buckets | {len(rows_by_period['4h'])} |",
        f"| 12h buckets | {len(rows_by_period['12h'])} |",
        f"| 1d buckets | {len(rows_by_period['1d'])} |",
        f"| Deep chart count | {len(charts)} |",
        f"| Current tokens/day forecast lane | {fmt_int(forecast.get('current_tokens_per_day'))} |",
        f"| 7d average tokens/day lane | {fmt_int(forecast.get('daily_token_avg_7'))} |",
        f"| 30d average tokens/day lane | {fmt_int(forecast.get('daily_token_avg_30'))} |",
        "",
        "## Smoothing Contract",
        "",
        "- Raw data is plotted on each time-series chart as a thin line.",
        "- Rolling median and EMA overlays are trend aids; peaks and totals are not replaced by smoothing.",
        "- 1h, 4h, and 12h charts derive from the current high-resolution hourly report window.",
        "- 1d charts use the all-time daily report rows.",
        "",
        "## Groups",
        "",
    ]
    for group, items in sorted(groups.items()):
        lines.append(f"- `{group}`: {len(items)} charts")
    lines += ["", "## Chart Index", ""]
    for group, items in sorted(groups.items()):
        lines += [f"### {group}", ""]
        for chart in items:
            lines.append(f"- [{chart['title']}](#{chart['name']})")
        lines.append("")
    lines += ["## Charts", ""]
    for group, items in sorted(groups.items()):
        lines += [f"### {group}", ""]
        for chart in items:
            lines += [
                f"#### {chart['name']}",
                "",
                f"![{chart['title']}]({chart['path']})",
                "",
            ]
            if chart.get("description"):
                lines += [f"Evidence note: {chart['description']}", ""]
    lines += [
        "## Residual Risk",
        "",
        "- Local Codex JSONL/report data is not billing-provider invoice proof.",
        "- Recent high-resolution buckets depend on the current token report's retained hourly window.",
        "- Long-context and regional cost bands are sensitivity approximations.",
        "- Smoothing overlays are visualization aids and are not used to replace raw totals.",
    ]
    return "\n".join(lines) + "\n"


def main() -> None:
    if not TOKEN_JSON.exists():
        raise FileNotFoundError(f"missing token report: {TOKEN_JSON}")
    report = read_json(TOKEN_JSON)
    rows_by_period = build_rows(report)
    charts, forecast = generate_charts(report, rows_by_period)
    payload = {
        "schema": "hecton8.token_usage_deep_analytics.v1",
        "generated_at_samara": dt.datetime.now(SAMARA).isoformat(),
        "evidence_class": "STATIC_LOCAL_JSONL_REPORT_DERIVED_NOT_PROVIDER_INVOICE_PROOF",
        "token_report": str(TOKEN_JSON.relative_to(PROJECT)).replace("\\", "/"),
        "chart_dir": str(CHART_DIR.relative_to(PROJECT)).replace("\\", "/"),
        "chart_count": len(charts),
        "bucket_counts": {key: len(value) for key, value in rows_by_period.items()},
        "smoothing": {
            "1h": {"rolling_median_buckets": 5, "ema_alpha": 0.28},
            "4h": {"rolling_median_buckets": 3, "ema_alpha": 0.32},
            "12h": {"rolling_median_buckets": 3, "ema_alpha": 0.36},
            "1d": {"rolling_median_buckets": 3, "ema_alpha": 0.40},
        },
        "forecast": forecast,
        "charts": charts,
        "totals": report.get("totals"),
        "primary_price_key": report.get("primary_price_key"),
        "primary_price": (report.get("pricing") or {}).get(report.get("primary_price_key")),
        "residual_risk": [
            "Local JSONL/report data is not provider invoice proof.",
            "1h/4h/12h charts are bounded by retained hourly report coverage.",
            "Cost sensitivity bands are approximations.",
            "Smoothing overlays preserve raw line visibility and do not replace raw totals.",
        ],
    }
    REPORT_DIR.mkdir(parents=True, exist_ok=True)
    DEEP_JSON.write_text(json.dumps(payload, indent=2, ensure_ascii=False), encoding="utf-8")
    DEEP_MD.write_text(markdown(report, charts, rows_by_period, forecast), encoding="utf-8-sig")
    print(json.dumps({
        "deep_json": str(DEEP_JSON),
        "deep_md": str(DEEP_MD),
        "chart_dir": str(CHART_DIR),
        "chart_count": len(charts),
        "bucket_counts": payload["bucket_counts"],
    }, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()

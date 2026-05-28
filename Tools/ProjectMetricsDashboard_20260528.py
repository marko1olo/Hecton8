import datetime
import json
import math
import os
import pathlib
import subprocess
from collections import Counter, defaultdict

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt


PROJECT = pathlib.Path(r"C:\hades\Hecton8")
SAMARA = datetime.timezone(datetime.timedelta(hours=4))
REPORT_DATE = datetime.datetime.now(SAMARA).date().isoformat()
TOKEN_REPORT_DIR = PROJECT / "Docs" / "DEPRECATED" / "Root_Docs_Noise_2026-05-26"
TOKEN_JSON = TOKEN_REPORT_DIR / f"TOKEN_USAGE_AUDIT_{REPORT_DATE}.json"
REPORT_DIR = PROJECT / "Docs" / "Reports"
CHART_DIR = REPORT_DIR / "MetricCharts" / REPORT_DATE
DASHBOARD_JSON = REPORT_DIR / f"PROJECT_METRICS_DASHBOARD_{REPORT_DATE}.json"
DASHBOARD_MD = REPORT_DIR / f"PROJECT_METRICS_DASHBOARD_{REPORT_DATE}.md"

USAGE_KEYS = ("input_tokens", "cached_input_tokens", "output_tokens", "reasoning_output_tokens", "total_tokens")
PRIMARY_RATE = {"input": 5.0, "cached_input": 0.5, "output": 30.0}
EXCLUDE_DIRS = {
    ".git",
    ".vs",
    ".idea",
    "Library",
    "Temp",
    "Obj",
    "obj",
    "bin",
    "Logs",
    "UserSettings",
    "node_modules",
    ".gradle",
    ".cache",
    "Archive",
    "_Archive",
    "DEPRECATED",
}
TEXT_EXTS = {
    ".asmdef",
    ".asset",
    ".bat",
    ".cginc",
    ".compute",
    ".cs",
    ".css",
    ".hlsl",
    ".html",
    ".json",
    ".md",
    ".meta",
    ".prefab",
    ".ps1",
    ".py",
    ".shader",
    ".txt",
    ".uxml",
    ".xml",
    ".yaml",
    ".yml",
}


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_bom(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8-sig")


def fmt_int(value):
    return f"{int(value):,}"


def fmt_money(value):
    return f"${float(value):,.2f}"


def usage_cost(usage, rate=PRIMARY_RATE):
    cached = int(usage.get("cached_input_tokens", 0) or 0)
    input_tokens = int(usage.get("input_tokens", 0) or 0)
    output = int(usage.get("output_tokens", 0) or 0)
    uncached = max(0, input_tokens - cached)
    return (uncached * rate["input"] + cached * rate["cached_input"] + output * rate["output"]) / 1_000_000


def period_rows(report, usage_key, cost_key=None):
    usage_map = report.get(usage_key) or {}
    costs = report.get(cost_key) or {}
    rows = []
    for period, usage in sorted(usage_map.items()):
        row = {"period": period}
        row.update({key: int(usage.get(key, 0) or 0) for key in USAGE_KEYS})
        row["uncached_input_tokens"] = max(0, row["input_tokens"] - row["cached_input_tokens"])
        row["cost_usd"] = float(costs.get(period, usage_cost(row)) or 0)
        row["cache_ratio"] = row["cached_input_tokens"] / max(1, row["input_tokens"])
        row["output_ratio"] = row["output_tokens"] / max(1, row["total_tokens"])
        row["reasoning_ratio"] = row["reasoning_output_tokens"] / max(1, row["output_tokens"])
        rows.append(row)
    return rows


def parse_hour(period):
    return datetime.datetime.strptime(period, "%Y-%m-%d %H:00").replace(tzinfo=SAMARA)


def parse_day(period):
    return datetime.datetime.strptime(period, "%Y-%m-%d").date()


def fill_recent_hours(rows, count=96):
    by_period = {row["period"]: row for row in rows}
    if rows:
        end = parse_hour(rows[-1]["period"])
    else:
        end = datetime.datetime.now(SAMARA).replace(minute=0, second=0, microsecond=0)
    start = end - datetime.timedelta(hours=count - 1)
    filled = []
    for index in range(count):
        dt = start + datetime.timedelta(hours=index)
        key = dt.strftime("%Y-%m-%d %H:00")
        row = by_period.get(key)
        if row is None:
            row = {"period": key, **{usage_key: 0 for usage_key in USAGE_KEYS}, "uncached_input_tokens": 0, "cost_usd": 0.0, "cache_ratio": 0.0, "output_ratio": 0.0, "reasoning_ratio": 0.0}
        filled.append(row)
    return filled


def fill_recent_days(rows, count):
    by_period = {row["period"]: row for row in rows}
    if rows:
        end = parse_day(rows[-1]["period"])
    else:
        end = datetime.datetime.now(SAMARA).date()
    start = end - datetime.timedelta(days=count - 1)
    filled = []
    for index in range(count):
        dt = start + datetime.timedelta(days=index)
        key = dt.isoformat()
        row = by_period.get(key)
        if row is None:
            row = {"period": key, **{usage_key: 0 for usage_key in USAGE_KEYS}, "uncached_input_tokens": 0, "cost_usd": 0.0, "cache_ratio": 0.0, "output_ratio": 0.0, "reasoning_ratio": 0.0}
        filled.append(row)
    return filled


def configure_axes(ax, title, ylabel=None):
    ax.set_title(title, fontsize=13, pad=12)
    if ylabel:
        ax.set_ylabel(ylabel)
    ax.grid(True, alpha=0.25)


def label_period_axis(ax, rows, max_labels=12):
    if not rows:
        return
    step = max(1, math.ceil(len(rows) / max_labels))
    ticks = list(range(0, len(rows), step))
    ax.set_xticks(ticks)
    ax.set_xticklabels([rows[index]["period"] for index in ticks], rotation=45, ha="right", fontsize=8)


def save_current_figure(name):
    CHART_DIR.mkdir(parents=True, exist_ok=True)
    path = CHART_DIR / f"{name}.png"
    plt.tight_layout()
    plt.savefig(path, dpi=170)
    plt.close()
    return path


def annotate_extremes(ax, rows, y_key, scale=1.0, label="value"):
    if not rows:
        return
    values = [row[y_key] / scale for row in rows]
    if not values:
        return
    candidates = {0, len(values) - 1, max(range(len(values)), key=lambda index: values[index])}
    low = min(range(len(values)), key=lambda index: values[index])
    if values[low] > 0:
        candidates.add(low)
    for index in sorted(candidates):
        value = values[index]
        if not math.isfinite(value):
            continue
        ax.annotate(
            f"{label}: {value:,.2f}\n{rows[index]['period']}",
            xy=(index, value),
            xytext=(0, 14),
            textcoords="offset points",
            ha="center",
            fontsize=8,
            bbox={"boxstyle": "round,pad=0.28", "facecolor": "white", "edgecolor": "#94a3b8", "alpha": 0.88},
            arrowprops={"arrowstyle": "-", "color": "#64748b", "lw": 0.8},
        )


def add_line_chart(charts, rows, name, title, y_key, scale=1.0, ylabel=None, color="#3b82f6", description=""):
    fig, ax = plt.subplots(figsize=(14, 6))
    xs = list(range(len(rows)))
    ys = [row[y_key] / scale for row in rows]
    ax.plot(xs, ys, color=color, linewidth=2)
    configure_axes(ax, title, ylabel)
    label_period_axis(ax, rows)
    path = save_current_figure(name)
    charts.append({"name": name, "title": title, "path": str(path.relative_to(REPORT_DIR)).replace("\\", "/"), "description": description})


def add_annotated_line_chart(charts, rows, name, title, y_key, scale=1.0, ylabel=None, color="#3b82f6", value_label="value", description=""):
    fig, ax = plt.subplots(figsize=(15, 7))
    xs = list(range(len(rows)))
    ys = [row[y_key] / scale for row in rows]
    ax.plot(xs, ys, color=color, linewidth=2.4)
    ax.fill_between(xs, ys, color=color, alpha=0.12)
    configure_axes(ax, title, ylabel)
    label_period_axis(ax, rows, max_labels=14)
    annotate_extremes(ax, rows, y_key, scale, value_label)
    path = save_current_figure(name)
    charts.append({"name": name, "title": title, "path": str(path.relative_to(REPORT_DIR)).replace("\\", "/"), "description": description})


def add_multi_line_chart(charts, rows, name, title, series, scale=1.0, ylabel=None, description=""):
    fig, ax = plt.subplots(figsize=(14, 6))
    xs = list(range(len(rows)))
    for key, label, color in series:
        ax.plot(xs, [row[key] / scale for row in rows], label=label, linewidth=2, color=color)
    configure_axes(ax, title, ylabel)
    label_period_axis(ax, rows)
    ax.legend(loc="upper left", fontsize=9)
    path = save_current_figure(name)
    charts.append({"name": name, "title": title, "path": str(path.relative_to(REPORT_DIR)).replace("\\", "/"), "description": description})


def add_stack_chart(charts, rows, name, title, series, scale=1.0, ylabel=None, description=""):
    fig, ax = plt.subplots(figsize=(14, 6))
    xs = list(range(len(rows)))
    values = [[row[key] / scale for row in rows] for key, _label, _color in series]
    labels = [label for _key, label, _color in series]
    colors = [color for _key, _label, color in series]
    ax.stackplot(xs, values, labels=labels, colors=colors, alpha=0.88)
    configure_axes(ax, title, ylabel)
    label_period_axis(ax, rows)
    ax.legend(loc="upper left", fontsize=9)
    path = save_current_figure(name)
    charts.append({"name": name, "title": title, "path": str(path.relative_to(REPORT_DIR)).replace("\\", "/"), "description": description})


def add_bar_chart(charts, items, name, title, value_key, label_key="label", scale=1.0, ylabel=None, top_n=25, color="#14b8a6", description=""):
    rows = list(items)[:top_n]
    fig, ax = plt.subplots(figsize=(14, 7))
    labels = [str(row[label_key]) for row in rows]
    values = [float(row[value_key]) / scale for row in rows]
    ax.bar(range(len(rows)), values, color=color)
    configure_axes(ax, title, ylabel)
    ax.set_xticks(range(len(rows)))
    ax.set_xticklabels(labels, rotation=65, ha="right", fontsize=8)
    path = save_current_figure(name)
    charts.append({"name": name, "title": title, "path": str(path.relative_to(REPORT_DIR)).replace("\\", "/"), "description": description})


def add_heatmap(charts, matrix, name, title, x_labels, y_labels, description=""):
    fig, ax = plt.subplots(figsize=(14, 6))
    im = ax.imshow(matrix, aspect="auto", cmap="magma")
    ax.set_title(title, fontsize=13, pad=12)
    ax.set_xticks(range(len(x_labels)))
    ax.set_xticklabels(x_labels, fontsize=8)
    ax.set_yticks(range(len(y_labels)))
    ax.set_yticklabels(y_labels, fontsize=9)
    fig.colorbar(im, ax=ax, label="commits")
    path = save_current_figure(name)
    charts.append({"name": name, "title": title, "path": str(path.relative_to(REPORT_DIR)).replace("\\", "/"), "description": description})


def iter_project_files():
    for root, dirs, files in os.walk(PROJECT):
        dirs[:] = [name for name in dirs if name not in EXCLUDE_DIRS]
        root_path = pathlib.Path(root)
        for name in files:
            yield root_path / name


def text_line_count(path):
    try:
        text = path.read_text(encoding="utf-8-sig", errors="replace")
    except Exception:
        return 0
    return text.count("\n") + (1 if text and not text.endswith("\n") else 0)


def collect_project_metrics():
    ext_rows = defaultdict(lambda: {"extension": "", "files": 0, "bytes": 0, "lines": 0})
    root_counts = Counter()
    largest_files = []
    for path in iter_project_files():
        try:
            stat = path.stat()
        except OSError:
            continue
        relative = path.relative_to(PROJECT)
        first = relative.parts[0] if relative.parts else "."
        root_counts[first] += 1
        ext = path.suffix.lower() or "[no_ext]"
        row = ext_rows[ext]
        row["extension"] = ext
        row["files"] += 1
        row["bytes"] += stat.st_size
        if ext in TEXT_EXTS:
            row["lines"] += text_line_count(path)
        largest_files.append({"path": str(relative).replace("\\", "/"), "bytes": stat.st_size, "extension": ext})
    ext_list = sorted(ext_rows.values(), key=lambda row: row["files"], reverse=True)
    byte_list = sorted(ext_rows.values(), key=lambda row: row["bytes"], reverse=True)
    largest_files = sorted(largest_files, key=lambda row: row["bytes"], reverse=True)[:40]
    docs = PROJECT / "Docs"
    reports = docs / "Reports"
    agent_logs = docs / "AgentLogs"
    tasks = docs / "Tasks"
    doc_artifacts = {
        "Docs reports json": len(list(reports.glob("*.json"))) if reports.exists() else 0,
        "Docs reports md": len(list(reports.glob("*.md"))) if reports.exists() else 0,
        "Agent logs md": len(list(agent_logs.glob("*.md"))) if agent_logs.exists() else 0,
        "Agent logs json": len(list(agent_logs.glob("*.json"))) if agent_logs.exists() else 0,
        "Task status md": len(list(tasks.glob("Status_*.md"))) if tasks.exists() else 0,
        "Architecture md": len(list((docs / "ARCHITECTURE").glob("*.md"))) if (docs / "ARCHITECTURE").exists() else 0,
        "Modding md": len(list((docs / "Modding").glob("*.md"))) if (docs / "Modding").exists() else 0,
        "Editor tests cs": len(list((PROJECT / "Assets" / "_Project" / "Tests" / "Editor").glob("*.cs"))) if (PROJECT / "Assets" / "_Project" / "Tests" / "Editor").exists() else 0,
    }
    return {
        "extension_by_files": ext_list,
        "extension_by_bytes": byte_list,
        "root_counts": [{"label": key, "files": value} for key, value in root_counts.most_common()],
        "largest_files": largest_files,
        "doc_artifacts": [{"label": key, "count": value} for key, value in doc_artifacts.items()],
    }


def run_git_log():
    cmd = [
        "git",
        "log",
        "--since=2026-04-01",
        "--date=iso-strict",
        "--numstat",
        "--pretty=format:--COMMIT--%H%x09%ad",
    ]
    try:
        return subprocess.run(cmd, cwd=PROJECT, check=True, capture_output=True, text=True, encoding="utf-8", errors="replace").stdout
    except Exception:
        return ""


def collect_git_metrics():
    daily = defaultdict(lambda: {"period": "", "commits": 0, "insertions": 0, "deletions": 0, "files_changed": 0})
    weekly = defaultdict(lambda: {"period": "", "commits": 0, "insertions": 0, "deletions": 0, "files_changed": 0})
    heatmap = [[0 for _hour in range(24)] for _day in range(7)]
    current_day = None
    current_week = None
    for line in run_git_log().splitlines():
        if line.startswith("--COMMIT--"):
            parts = line.split("\t")
            if len(parts) < 2:
                current_day = None
                current_week = None
                continue
            dt = datetime.datetime.fromisoformat(parts[1]).astimezone(SAMARA)
            current_day = dt.date().isoformat()
            iso = dt.isocalendar()
            current_week = f"{iso.year}-W{iso.week:02d}"
            daily[current_day]["period"] = current_day
            weekly[current_week]["period"] = current_week
            daily[current_day]["commits"] += 1
            weekly[current_week]["commits"] += 1
            heatmap[dt.weekday()][dt.hour] += 1
            continue
        if not line.strip() or current_day is None:
            continue
        parts = line.split("\t")
        if len(parts) < 3:
            continue
        daily[current_day]["files_changed"] += 1
        weekly[current_week]["files_changed"] += 1
        if parts[0].isdigit():
            insertions = int(parts[0])
            deletions = int(parts[1]) if parts[1].isdigit() else 0
            daily[current_day]["insertions"] += insertions
            daily[current_day]["deletions"] += deletions
            weekly[current_week]["insertions"] += insertions
            weekly[current_week]["deletions"] += deletions
    daily_rows = [daily[key] for key in sorted(daily)]
    weekly_rows = [weekly[key] for key in sorted(weekly)]
    for row in daily_rows + weekly_rows:
        row["churn"] = row["insertions"] + row["deletions"]
    return {"daily": daily_rows, "weekly": weekly_rows, "weekday_hour_commit_heatmap": heatmap}


def generate_charts(report, project_metrics, git_metrics):
    plt.style.use("seaborn-v0_8-darkgrid")
    charts = []
    hourly = fill_recent_hours(period_rows(report, "hourly", "hourly_gpt_5_5_standard_costs_usd"), 96)
    daily = period_rows(report, "daily", "daily_gpt_5_5_standard_costs_usd")
    weekly = period_rows(report, "weekly", "weekly_gpt_5_5_standard_costs_usd")
    long_windows = {
        7: fill_recent_days(daily, 7),
        30: fill_recent_days(daily, 30),
        60: fill_recent_days(daily, 60),
    }

    add_line_chart(charts, hourly, "hourly_total_tokens_last_96h", "Hourly total tokens - last 96h", "total_tokens", 1_000_000, "million tokens", "#2563eb")
    add_line_chart(charts, hourly, "hourly_cost_last_96h", "Hourly GPT-5.5 standard cost - last 96h", "cost_usd", 1, "USD", "#16a34a")
    add_stack_chart(charts, hourly, "hourly_io_stack_last_96h", "Hourly input/output stack - last 96h", (("uncached_input_tokens", "uncached input", "#f97316"), ("cached_input_tokens", "cached input", "#22c55e"), ("output_tokens", "output", "#3b82f6")), 1_000_000, "million tokens")
    add_multi_line_chart(charts, hourly, "hourly_output_reasoning_last_96h", "Hourly output and reasoning output - last 96h", (("output_tokens", "output", "#2563eb"), ("reasoning_output_tokens", "reasoning output", "#9333ea")), 1_000, "thousand tokens")
    add_multi_line_chart(charts, hourly, "hourly_ratios_last_96h", "Hourly cache/output/reasoning ratios - last 96h", (("cache_ratio", "cache ratio", "#16a34a"), ("output_ratio", "output/total", "#2563eb"), ("reasoning_ratio", "reasoning/output", "#9333ea")), 0.01, "percent")

    for days, rows in long_windows.items():
        suffix = f"last_{days}d"
        add_annotated_line_chart(
            charts,
            rows,
            f"daily_total_tokens_{suffix}",
            f"Daily total tokens - last {days} days",
            "total_tokens",
            1_000_000_000,
            "billion tokens",
            "#1d4ed8",
            "B tokens",
            f"Long-range token consumption window covering the last {days} calendar days with start/end/peak labels.",
        )
        add_annotated_line_chart(
            charts,
            rows,
            f"daily_cost_{suffix}",
            f"Daily GPT-5.5 standard cost - last {days} days",
            "cost_usd",
            1,
            "USD",
            "#15803d",
            "USD",
            f"Long-range GPT-5.5 API-equivalent cost window covering the last {days} calendar days with start/end/peak labels.",
        )
        add_stack_chart(
            charts,
            rows,
            f"daily_io_stack_{suffix}",
            f"Daily input/output stack - last {days} days",
            (("uncached_input_tokens", "uncached input", "#f97316"), ("cached_input_tokens", "cached input", "#22c55e"), ("output_tokens", "output", "#3b82f6")),
            1_000_000_000,
            "billion tokens",
            f"Long-range daily token composition window covering the last {days} calendar days.",
        )
        add_multi_line_chart(
            charts,
            rows,
            f"daily_ratios_{suffix}",
            f"Daily cache/output/reasoning ratios - last {days} days",
            (("cache_ratio", "cache ratio", "#16a34a"), ("output_ratio", "output/total", "#2563eb"), ("reasoning_ratio", "reasoning/output", "#9333ea")),
            0.01,
            "percent",
            f"Long-range daily quality-of-usage ratios covering the last {days} calendar days.",
        )

    add_line_chart(charts, daily, "daily_total_tokens", "Daily total tokens", "total_tokens", 1_000_000_000, "billion tokens", "#2563eb")
    add_line_chart(charts, daily, "daily_cost", "Daily GPT-5.5 standard cost", "cost_usd", 1, "USD", "#16a34a")
    add_stack_chart(charts, daily, "daily_io_stack", "Daily input/output stack", (("uncached_input_tokens", "uncached input", "#f97316"), ("cached_input_tokens", "cached input", "#22c55e"), ("output_tokens", "output", "#3b82f6")), 1_000_000_000, "billion tokens")
    add_multi_line_chart(charts, daily, "daily_output_reasoning", "Daily output and reasoning output", (("output_tokens", "output", "#2563eb"), ("reasoning_output_tokens", "reasoning output", "#9333ea")), 1_000_000, "million tokens")
    add_multi_line_chart(charts, daily, "daily_ratios", "Daily cache/output/reasoning ratios", (("cache_ratio", "cache ratio", "#16a34a"), ("output_ratio", "output/total", "#2563eb"), ("reasoning_ratio", "reasoning/output", "#9333ea")), 0.01, "percent")

    add_line_chart(charts, weekly, "weekly_total_tokens", "Weekly total tokens", "total_tokens", 1_000_000_000, "billion tokens", "#2563eb")
    add_line_chart(charts, weekly, "weekly_cost", "Weekly GPT-5.5 standard cost", "cost_usd", 1, "USD", "#16a34a")
    add_stack_chart(charts, weekly, "weekly_io_stack", "Weekly input/output stack", (("uncached_input_tokens", "uncached input", "#f97316"), ("cached_input_tokens", "cached input", "#22c55e"), ("output_tokens", "output", "#3b82f6")), 1_000_000_000, "billion tokens")

    model_effort = report.get("model_effort_final_standard_cost_rows") or []
    add_bar_chart(charts, model_effort, "model_effort_tokens_top20", "Top model+effort buckets by tokens", "total_tokens", "key", 1_000_000_000, "billion tokens", 20, "#7c3aed")
    cost_rows = [row for row in model_effort if row.get("model_standard_cost_usd") is not None]
    add_bar_chart(charts, cost_rows, "model_effort_cost_top20", "Top model+effort buckets by model-standard cost", "model_standard_cost_usd", "key", 1, "USD", 20, "#0f766e")
    top_sessions = [{"label": row.get("session_id", "")[:8], "total_tokens": row.get("total_tokens", 0)} for row in report.get("top_sessions", [])]
    add_bar_chart(charts, top_sessions, "top_sessions_total_tokens", "Top sessions by total tokens", "total_tokens", "label", 1_000_000, "million tokens", 25, "#dc2626")
    top_cwd = [{"label": str(row.get("key", ""))[-42:], "total_tokens": row.get("total_tokens", 0)} for row in report.get("top_cwd_usage", [])]
    add_bar_chart(charts, top_cwd, "top_cwd_total_tokens", "Top CWD buckets by total tokens", "total_tokens", "label", 1_000_000_000, "billion tokens", 15, "#0891b2")

    scopes = [{"label": key, **value} for key, value in (report.get("scope_economics") or {}).items()]
    add_bar_chart(charts, sorted(scopes, key=lambda row: row["lines"], reverse=True), "project_lines_by_scope", "Current project lines by scope", "lines", "label", 1_000, "thousand lines", 20, "#475569")
    add_bar_chart(charts, sorted(scopes, key=lambda row: row["tokens_per_1k_characters"], reverse=True), "token_density_by_scope", "Token density by current source scope", "tokens_per_1k_characters", "label", 1, "tokens / 1k chars", 20, "#ea580c")
    add_bar_chart(charts, project_metrics["extension_by_files"], "file_counts_by_extension", "Project file counts by extension", "files", "extension", 1, "files", 25, "#0d9488")
    add_bar_chart(charts, project_metrics["extension_by_bytes"], "bytes_by_extension", "Project bytes by extension", "bytes", "extension", 1_000_000, "MB", 25, "#1d4ed8")
    add_bar_chart(charts, project_metrics["root_counts"], "file_counts_by_root", "Project file counts by root folder", "files", "label", 1, "files", 25, "#64748b")
    add_bar_chart(charts, project_metrics["doc_artifacts"], "docs_artifact_counts", "Documentation and audit artifact counts", "count", "label", 1, "count", 25, "#9333ea")

    git_daily = git_metrics.get("daily") or []
    git_weekly = git_metrics.get("weekly") or []
    add_line_chart(charts, git_daily, "git_commits_by_day", "Git commits by day since 2026-04-01", "commits", 1, "commits", "#0f766e")
    add_line_chart(charts, git_daily, "git_churn_by_day", "Git churn by day since 2026-04-01", "churn", 1_000, "thousand changed lines", "#be123c")
    add_multi_line_chart(charts, git_daily, "git_insertions_deletions_by_day", "Git insertions/deletions by day", (("insertions", "insertions", "#16a34a"), ("deletions", "deletions", "#dc2626")), 1_000, "thousand lines")
    add_line_chart(charts, git_weekly, "git_commits_by_week", "Git commits by ISO week", "commits", 1, "commits", "#0f766e")
    add_line_chart(charts, git_weekly, "git_churn_by_week", "Git churn by ISO week", "churn", 1_000, "thousand changed lines", "#be123c")
    add_heatmap(charts, git_metrics.get("weekday_hour_commit_heatmap") or [[0] * 24 for _ in range(7)], "git_commit_weekday_hour_heatmap", "Git commit heatmap by Samara weekday/hour", [str(hour) for hour in range(24)], ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"])
    return charts


def markdown_report(report, project_metrics, git_metrics, charts):
    total = report["totals"]
    primary = report["pricing"][report["primary_price_key"]]
    velocity = ((report.get("previous_snapshot_delta") or {}).get("velocity") or {})
    pricing_context = report.get("pricing_context_rules") or {}
    generated = datetime.datetime.now(SAMARA).isoformat()
    lines = [
        f"# Project Metrics Dashboard {REPORT_DATE}",
        "",
        f"Generated Samara: `{generated}`",
        "Evidence class: static local Codex JSONL, git history, and filesystem scan. Token cost is API-equivalent, not invoice proof.",
        "",
        "## Headline",
        "",
        "| Metric | Value |",
        "|---|---:|",
        f"| Total tokens | {fmt_int(total['total_tokens'])} |",
        f"| Input tokens | {fmt_int(total['input_tokens'])} |",
        f"| Cached input tokens | {fmt_int(total['cached_input_tokens'])} |",
        f"| Output tokens | {fmt_int(total['output_tokens'])} |",
        f"| Reasoning output tokens | {fmt_int(total['reasoning_output_tokens'])} |",
        f"| Sessions with usage | {fmt_int(report['sessions_with_usage'])} |",
        f"| GPT-5.5 standard API-equivalent total | {fmt_money(primary['total_cost_usd'])} |",
        f"| GPT-5.5 long-context sensitivity upper bound | {fmt_money(pricing_context.get('gpt_5_5_long_context_upper_bound_usd', primary['total_cost_usd']))} |",
        f"| GPT-5.5 long-context + regional upper bound | {fmt_money(pricing_context.get('gpt_5_5_long_context_regional_10pct_upper_bound_usd', primary['total_cost_usd']))} |",
        f"| GPT-5.5 regional +10% sensitivity | {fmt_money(pricing_context.get('gpt_5_5_regional_10pct_usd', primary['total_cost_usd']))} |",
        f"| Post-cutoff detected long-context delta events (lower-bound) | {fmt_int(pricing_context.get('post_cutoff_long_context_event_count', 0))} |",
        f"| Post-cutoff detected long-context surcharge delta (lower-bound) | {fmt_money(pricing_context.get('post_cutoff_long_context_event_surcharge_delta_usd', 0))} |",
        f"| Post-cutoff long-context evidence class | `{pricing_context.get('post_cutoff_long_context_event_evidence_class', 'LOCAL_JSONL_DELTA_LOWER_BOUND_NOT_PROVIDER_INVOICE_CLASSIFICATION')}` |",
        f"| Tokens/hour since previous snapshot | {fmt_int(velocity.get('total_tokens_per_hour', 0))} |",
        f"| GPT-5.5 standard USD/hour since previous snapshot | {fmt_money(velocity.get('gpt_5_5_standard_usd_per_hour', 0))} |",
        f"| Primary C# LOC/hour since previous snapshot | {velocity.get('primary_code_lines_per_hour', 0):,.2f} |",
        "| Long-range chart windows | 7d, 30d, 60d |",
        f"| Chart count | {len(charts)} |",
        "",
        "## Chart Index",
        "",
    ]
    for chart in charts:
        lines.append(f"- [{chart['title']}](#{chart['name']})")
    lines += ["", "## Charts", ""]
    for chart in charts:
        lines += [
            f"### {chart['name']}",
            "",
            f"![{chart['title']}]({chart['path']})",
            "",
        ]
        if chart.get("description"):
            lines += [f"Evidence note: {chart['description']}", ""]
    lines += [
        "## Supporting Data",
        "",
        f"- Machine-readable dashboard: `{DASHBOARD_JSON.relative_to(PROJECT).as_posix()}`",
        f"- Token report JSON: `{TOKEN_JSON.relative_to(PROJECT).as_posix()}`",
        "- OpenAI pricing source: https://developers.openai.com/api/docs/pricing",
        "- GPT-5.5 model pricing source: https://developers.openai.com/api/docs/models/gpt-5.5",
        "- Prompt caching source: https://developers.openai.com/api/docs/guides/prompt-caching",
        "- Reasoning source: https://developers.openai.com/api/docs/guides/reasoning",
        "",
        "## Residual Risk",
        "",
        "- Local Codex JSONL is not billing-provider proof.",
        "- Long-context post-cutoff detection is a lower-bound delta-event heuristic; exact provider-side surcharge classification is absent.",
        "- Git churn charts use committed history; uncommitted live-agent work is visible only after commit.",
        "- Filesystem metrics exclude configured build/cache/archive directories.",
    ]
    return "\n".join(lines) + "\n"


def main():
    if not TOKEN_JSON.exists():
        raise FileNotFoundError(f"missing token report: {TOKEN_JSON}")
    report = read_json(TOKEN_JSON)
    project_metrics = collect_project_metrics()
    git_metrics = collect_git_metrics()
    charts = generate_charts(report, project_metrics, git_metrics)
    payload = {
        "schema": "hecton8.project_metrics_dashboard.v1",
        "generated_at_samara": datetime.datetime.now(SAMARA).isoformat(),
        "token_report": str(TOKEN_JSON.relative_to(PROJECT)).replace("\\", "/"),
        "dashboard_markdown": str(DASHBOARD_MD.relative_to(PROJECT)).replace("\\", "/"),
        "chart_count": len(charts),
        "long_range_windows_days": [7, 30, 60],
        "charts": charts,
        "token_headline": {
            "total_tokens": report["totals"]["total_tokens"],
            "sessions_with_usage": report["sessions_with_usage"],
            "gpt_5_5_standard_cost_usd": report["pricing"][report["primary_price_key"]]["total_cost_usd"],
            "pricing_context_rules": report.get("pricing_context_rules") or {},
            "velocity": (report.get("previous_snapshot_delta") or {}).get("velocity") or {},
        },
        "project_metrics": project_metrics,
        "git_metrics": git_metrics,
    }
    DASHBOARD_JSON.parent.mkdir(parents=True, exist_ok=True)
    DASHBOARD_JSON.write_text(json.dumps(payload, indent=2, ensure_ascii=False), encoding="utf-8")
    write_bom(DASHBOARD_MD, markdown_report(report, project_metrics, git_metrics, charts))
    print(json.dumps({
        "dashboard_json": str(DASHBOARD_JSON),
        "dashboard_md": str(DASHBOARD_MD),
        "chart_dir": str(CHART_DIR),
        "chart_count": len(charts),
        "token_total": report["totals"]["total_tokens"],
        "tokens_per_hour": ((report.get("previous_snapshot_delta") or {}).get("velocity") or {}).get("total_tokens_per_hour"),
    }, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()

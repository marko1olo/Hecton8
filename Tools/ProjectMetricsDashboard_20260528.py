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
WORDS_PER_TOKEN_HEURISTIC = 0.75
PRINTED_PAGE_WORDS = 500
BOOK_WORDS = 80_000
LONG_RANGE_WINDOWS_DAYS = (7, 14, 30, 60, 90)
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


def fmt_float(value, digits=2):
    return f"{float(value):,.{digits}f}"


def tail_label(value, width=54):
    text = str(value)
    if len(text) <= width:
        return text
    return "..." + text[-(width - 3):]


def safe_div(numerator, denominator, default=0.0):
    denominator = float(denominator or 0)
    if denominator == 0:
        return default
    return float(numerator or 0) / denominator


def usage_cost(usage, rate=PRIMARY_RATE):
    cached = int(usage.get("cached_input_tokens", 0) or 0)
    input_tokens = int(usage.get("input_tokens", 0) or 0)
    output = int(usage.get("output_tokens", 0) or 0)
    uncached = max(0, input_tokens - cached)
    return (uncached * rate["input"] + cached * rate["cached_input"] + output * rate["output"]) / 1_000_000


def enrich_usage_row(row, cost_usd=None):
    cached = int(row.get("cached_input_tokens", 0) or 0)
    input_tokens = int(row.get("input_tokens", 0) or 0)
    output_tokens = int(row.get("output_tokens", 0) or 0)
    total_tokens = int(row.get("total_tokens", 0) or 0)
    reasoning_tokens = int(row.get("reasoning_output_tokens", 0) or 0)
    row["uncached_input_tokens"] = max(0, input_tokens - cached)
    row["cost_usd"] = float(usage_cost(row) if cost_usd is None else (cost_usd or 0))
    row["cost_no_cache_usd"] = (input_tokens * PRIMARY_RATE["input"] + output_tokens * PRIMARY_RATE["output"]) / 1_000_000
    row["cache_savings_usd"] = max(0.0, row["cost_no_cache_usd"] - row["cost_usd"])
    row["input_side_cost_usd"] = (row["uncached_input_tokens"] * PRIMARY_RATE["input"] + cached * PRIMARY_RATE["cached_input"]) / 1_000_000
    row["output_side_cost_usd"] = output_tokens * PRIMARY_RATE["output"] / 1_000_000
    row["effective_usd_per_1m_total_tokens"] = safe_div(row["cost_usd"] * 1_000_000, total_tokens)
    row["cache_ratio"] = safe_div(cached, input_tokens)
    row["output_ratio"] = safe_div(output_tokens, total_tokens)
    row["reasoning_ratio"] = safe_div(reasoning_tokens, output_tokens)
    row["output_cost_share"] = safe_div(row["output_side_cost_usd"], row["cost_usd"])
    row["cached_to_uncached_ratio"] = safe_div(cached, row["uncached_input_tokens"])
    row["printed_pages_500w"] = total_tokens * WORDS_PER_TOKEN_HEURISTIC / PRINTED_PAGE_WORDS
    row["books_80k_words"] = total_tokens * WORDS_PER_TOKEN_HEURISTIC / BOOK_WORDS
    return row


def empty_period_row(period):
    return enrich_usage_row({"period": period, **{usage_key: 0 for usage_key in USAGE_KEYS}}, 0.0)


def period_rows(report, usage_key, cost_key=None):
    usage_map = report.get(usage_key) or {}
    costs = report.get(cost_key) or {}
    rows = []
    for period, usage in sorted(usage_map.items()):
        row = {"period": period}
        row.update({key: int(usage.get(key, 0) or 0) for key in USAGE_KEYS})
        rows.append(enrich_usage_row(row, costs.get(period, usage_cost(row))))
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
            row = empty_period_row(key)
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
            row = empty_period_row(key)
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


def add_heatmap(charts, matrix, name, title, x_labels, y_labels, description="", colorbar_label="value"):
    fig, ax = plt.subplots(figsize=(14, 6))
    im = ax.imshow(matrix, aspect="auto", cmap="magma")
    ax.set_title(title, fontsize=13, pad=12)
    ax.set_xticks(range(len(x_labels)))
    ax.set_xticklabels(x_labels, fontsize=8)
    ax.set_yticks(range(len(y_labels)))
    ax.set_yticklabels(y_labels, fontsize=9)
    fig.colorbar(im, ax=ax, label=colorbar_label)
    path = save_current_figure(name)
    charts.append({"name": name, "title": title, "path": str(path.relative_to(REPORT_DIR)).replace("\\", "/"), "description": description})


def add_scatter_chart(charts, rows, name, title, x_key, y_key, x_scale=1.0, y_scale=1.0, xlabel=None, ylabel=None, color_key=None, description=""):
    fig, ax = plt.subplots(figsize=(13, 7))
    xs = [float(row.get(x_key, 0) or 0) / x_scale for row in rows]
    ys = [float(row.get(y_key, 0) or 0) / y_scale for row in rows]
    if color_key:
        colors = [float(row.get(color_key, 0) or 0) for row in rows]
        scatter = ax.scatter(xs, ys, c=colors, cmap="viridis", s=58, alpha=0.82, edgecolors="#0f172a", linewidths=0.4)
        fig.colorbar(scatter, ax=ax, label=color_key)
    else:
        ax.scatter(xs, ys, s=58, alpha=0.82, color="#2563eb", edgecolors="#0f172a", linewidths=0.4)
    configure_axes(ax, title, ylabel)
    if xlabel:
        ax.set_xlabel(xlabel)
    for row, x, y in zip(rows, xs, ys):
        period = str(row.get("period", ""))
        if period:
            ax.annotate(period[-5:], (x, y), textcoords="offset points", xytext=(4, 4), fontsize=7, alpha=0.72)
    path = save_current_figure(name)
    charts.append({"name": name, "title": title, "path": str(path.relative_to(REPORT_DIR)).replace("\\", "/"), "description": description})


def period_value_heatmap(rows, value_key, parser, row_label_func, column_func, column_count, scale=1.0):
    labels = []
    label_index = {}
    matrix = []
    for row in rows:
        try:
            dt = parser(row["period"])
        except Exception:
            continue
        label = row_label_func(dt)
        if label not in label_index:
            label_index[label] = len(labels)
            labels.append(label)
            matrix.append([0.0 for _ in range(column_count)])
        column = column_func(dt)
        if 0 <= column < column_count:
            matrix[label_index[label]][column] += float(row.get(value_key, 0) or 0) / scale
    return matrix, labels


def join_token_git_daily(daily_rows, git_daily_rows):
    git_by_day = {row["period"]: row for row in git_daily_rows}
    joined = []
    for row in daily_rows:
        git_row = git_by_day.get(row["period"], {})
        churn = int(git_row.get("churn", 0) or 0)
        commits = int(git_row.get("commits", 0) or 0)
        joined.append({
            "period": row["period"],
            "total_tokens": row.get("total_tokens", 0),
            "cost_usd": row.get("cost_usd", 0),
            "output_tokens": row.get("output_tokens", 0),
            "reasoning_output_tokens": row.get("reasoning_output_tokens", 0),
            "churn": churn,
            "commits": commits,
            "files_changed": int(git_row.get("files_changed", 0) or 0),
            "tokens_per_changed_line": safe_div(row.get("total_tokens", 0), churn),
            "cost_per_changed_line": safe_div(row.get("cost_usd", 0), churn),
            "tokens_per_commit": safe_div(row.get("total_tokens", 0), commits),
        })
    return joined


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
    largest_text_files = []
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
            lines = text_line_count(path)
            row["lines"] += lines
            largest_text_files.append({"path": str(relative).replace("\\", "/"), "lines": lines, "extension": ext})
        largest_files.append({"path": str(relative).replace("\\", "/"), "bytes": stat.st_size, "extension": ext})
    for row in ext_rows.values():
        row["avg_bytes_per_file"] = safe_div(row["bytes"], row["files"])
        row["avg_lines_per_file"] = safe_div(row["lines"], row["files"])
    ext_list = sorted(ext_rows.values(), key=lambda row: row["files"], reverse=True)
    byte_list = sorted(ext_rows.values(), key=lambda row: row["bytes"], reverse=True)
    line_list = sorted([row for row in ext_rows.values() if row["lines"] > 0], key=lambda row: row["lines"], reverse=True)
    avg_byte_list = sorted(ext_rows.values(), key=lambda row: row["avg_bytes_per_file"], reverse=True)
    avg_line_list = sorted([row for row in ext_rows.values() if row["lines"] > 0], key=lambda row: row["avg_lines_per_file"], reverse=True)
    largest_files = sorted(largest_files, key=lambda row: row["bytes"], reverse=True)[:40]
    largest_text_files = sorted(largest_text_files, key=lambda row: row["lines"], reverse=True)[:40]
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
        "extension_by_lines": line_list,
        "extension_by_avg_bytes_per_file": avg_byte_list,
        "extension_by_avg_lines_per_file": avg_line_list,
        "root_counts": [{"label": key, "files": value} for key, value in root_counts.most_common()],
        "largest_files": largest_files,
        "largest_text_files": largest_text_files,
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
    churn_heatmap = [[0 for _hour in range(24)] for _day in range(7)]
    current_day = None
    current_week = None
    current_weekday = None
    current_hour = None
    for line in run_git_log().splitlines():
        if line.startswith("--COMMIT--"):
            parts = line.split("\t")
            if len(parts) < 2:
                current_day = None
                current_week = None
                current_weekday = None
                current_hour = None
                continue
            dt = datetime.datetime.fromisoformat(parts[1]).astimezone(SAMARA)
            current_day = dt.date().isoformat()
            iso = dt.isocalendar()
            current_week = f"{iso.year}-W{iso.week:02d}"
            current_weekday = dt.weekday()
            current_hour = dt.hour
            daily[current_day]["period"] = current_day
            weekly[current_week]["period"] = current_week
            daily[current_day]["commits"] += 1
            weekly[current_week]["commits"] += 1
            heatmap[current_weekday][current_hour] += 1
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
            churn = insertions + deletions
            daily[current_day]["insertions"] += insertions
            daily[current_day]["deletions"] += deletions
            weekly[current_week]["insertions"] += insertions
            weekly[current_week]["deletions"] += deletions
            if current_weekday is not None and current_hour is not None:
                churn_heatmap[current_weekday][current_hour] += churn
    daily_rows = [daily[key] for key in sorted(daily)]
    weekly_rows = [weekly[key] for key in sorted(weekly)]
    for row in daily_rows + weekly_rows:
        row["churn"] = row["insertions"] + row["deletions"]
        row["net_lines"] = row["insertions"] - row["deletions"]
        row["churn_per_commit"] = safe_div(row["churn"], row["commits"])
        row["files_changed_per_commit"] = safe_div(row["files_changed"], row["commits"])
    return {"daily": daily_rows, "weekly": weekly_rows, "weekday_hour_commit_heatmap": heatmap, "weekday_hour_churn_heatmap": churn_heatmap}


def generate_charts(report, project_metrics, git_metrics):
    plt.style.use("seaborn-v0_8-darkgrid")
    charts = []
    hourly_all = period_rows(report, "hourly", "hourly_gpt_5_5_standard_costs_usd")
    hourly = fill_recent_hours(hourly_all, 96)
    daily = period_rows(report, "daily", "daily_gpt_5_5_standard_costs_usd")
    weekly = period_rows(report, "weekly", "weekly_gpt_5_5_standard_costs_usd")
    long_windows = {days: fill_recent_days(daily, days) for days in LONG_RANGE_WINDOWS_DAYS}

    add_line_chart(charts, hourly, "hourly_total_tokens_last_96h", "Hourly total tokens - last 96h", "total_tokens", 1_000_000, "million tokens", "#2563eb")
    add_line_chart(charts, hourly, "hourly_cost_last_96h", "Hourly GPT-5.5 standard cost - last 96h", "cost_usd", 1, "USD", "#16a34a")
    add_stack_chart(charts, hourly, "hourly_io_stack_last_96h", "Hourly input/output stack - last 96h", (("uncached_input_tokens", "uncached input", "#f97316"), ("cached_input_tokens", "cached input", "#22c55e"), ("output_tokens", "output", "#3b82f6")), 1_000_000, "million tokens")
    add_multi_line_chart(charts, hourly, "hourly_output_reasoning_last_96h", "Hourly output and reasoning output - last 96h", (("output_tokens", "output", "#2563eb"), ("reasoning_output_tokens", "reasoning output", "#9333ea")), 1_000, "thousand tokens")
    add_multi_line_chart(charts, hourly, "hourly_ratios_last_96h", "Hourly cache/output/reasoning ratios - last 96h", (("cache_ratio", "cache ratio", "#16a34a"), ("output_ratio", "output/total", "#2563eb"), ("reasoning_ratio", "reasoning/output", "#9333ea")), 0.01, "percent")
    add_line_chart(charts, hourly, "hourly_cache_savings_last_96h", "Hourly cache discount saved - last 96h", "cache_savings_usd", 1, "USD saved", "#059669")
    add_multi_line_chart(charts, hourly, "hourly_actual_vs_no_cache_cost_last_96h", "Hourly actual vs no-cache GPT-5.5 cost - last 96h", (("cost_usd", "actual cached cost", "#16a34a"), ("cost_no_cache_usd", "no-cache theoretical cost", "#dc2626")), 1, "USD")
    add_line_chart(charts, hourly, "hourly_effective_cost_per_1m_last_96h", "Hourly effective USD per 1M total tokens - last 96h", "effective_usd_per_1m_total_tokens", 1, "USD / 1M tokens", "#0f766e")
    add_line_chart(charts, hourly, "hourly_output_cost_share_last_96h", "Hourly output cost share - last 96h", "output_cost_share", 0.01, "percent", "#9333ea")
    add_line_chart(charts, hourly, "hourly_printed_pages_last_96h", "Hourly human-scale burn - last 96h", "printed_pages_500w", 1_000, "thousand 500-word pages", "#7c2d12")
    add_line_chart(charts, hourly, "hourly_cached_to_uncached_ratio_last_96h", "Hourly cached-to-uncached input ratio - last 96h", "cached_to_uncached_ratio", 1, "cached / uncached", "#0d9488")

    heatmap_96, heatmap_96_labels = period_value_heatmap(hourly, "total_tokens", parse_hour, lambda dt: dt.strftime("%m-%d"), lambda dt: dt.hour, 24, 1_000_000)
    add_heatmap(charts, heatmap_96, "hourly_token_day_hour_heatmap_last_96h", "Token heatmap by day/hour - last 96h", [str(hour) for hour in range(24)], heatmap_96_labels, "Last-96-hour total token pressure by local Samara day and hour.", "million tokens")
    weekday_token_heatmap = [[0.0 for _hour in range(24)] for _day in range(7)]
    weekday_cost_heatmap = [[0.0 for _hour in range(24)] for _day in range(7)]
    for row in hourly_all:
        dt = parse_hour(row["period"])
        weekday_token_heatmap[dt.weekday()][dt.hour] += row["total_tokens"] / 1_000_000
        weekday_cost_heatmap[dt.weekday()][dt.hour] += row["cost_usd"]
    weekday_labels = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"]
    hour_labels = [str(hour) for hour in range(24)]
    add_heatmap(charts, weekday_token_heatmap, "token_weekday_hour_heatmap_all", "Token heatmap by weekday/hour - all available hours", hour_labels, weekday_labels, "All available hourly token pressure aggregated by Samara weekday and hour.", "million tokens")
    add_heatmap(charts, weekday_cost_heatmap, "cost_weekday_hour_heatmap_all", "Cost heatmap by weekday/hour - all available hours", hour_labels, weekday_labels, "All available hourly GPT-5.5 API-equivalent cost aggregated by Samara weekday and hour.", "USD")

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
        add_line_chart(
            charts,
            rows,
            f"daily_cache_savings_{suffix}",
            f"Daily cache discount saved - last {days} days",
            "cache_savings_usd",
            1,
            "USD saved",
            "#059669",
            f"Long-range cache-discount value window covering the last {days} calendar days.",
        )
        add_line_chart(
            charts,
            rows,
            f"daily_effective_cost_per_1m_{suffix}",
            f"Daily effective USD per 1M total tokens - last {days} days",
            "effective_usd_per_1m_total_tokens",
            1,
            "USD / 1M tokens",
            "#0f766e",
            f"Long-range effective blended token price window covering the last {days} calendar days.",
        )

    add_line_chart(charts, daily, "daily_total_tokens", "Daily total tokens", "total_tokens", 1_000_000_000, "billion tokens", "#2563eb")
    add_line_chart(charts, daily, "daily_cost", "Daily GPT-5.5 standard cost", "cost_usd", 1, "USD", "#16a34a")
    add_stack_chart(charts, daily, "daily_io_stack", "Daily input/output stack", (("uncached_input_tokens", "uncached input", "#f97316"), ("cached_input_tokens", "cached input", "#22c55e"), ("output_tokens", "output", "#3b82f6")), 1_000_000_000, "billion tokens")
    add_multi_line_chart(charts, daily, "daily_output_reasoning", "Daily output and reasoning output", (("output_tokens", "output", "#2563eb"), ("reasoning_output_tokens", "reasoning output", "#9333ea")), 1_000_000, "million tokens")
    add_multi_line_chart(charts, daily, "daily_ratios", "Daily cache/output/reasoning ratios", (("cache_ratio", "cache ratio", "#16a34a"), ("output_ratio", "output/total", "#2563eb"), ("reasoning_ratio", "reasoning/output", "#9333ea")), 0.01, "percent")
    add_line_chart(charts, daily, "daily_cache_savings", "Daily cache discount saved", "cache_savings_usd", 1, "USD saved", "#059669")
    add_multi_line_chart(charts, daily, "daily_actual_vs_no_cache_cost", "Daily actual vs no-cache GPT-5.5 cost", (("cost_usd", "actual cached cost", "#16a34a"), ("cost_no_cache_usd", "no-cache theoretical cost", "#dc2626")), 1, "USD")
    add_line_chart(charts, daily, "daily_effective_cost_per_1m", "Daily effective USD per 1M total tokens", "effective_usd_per_1m_total_tokens", 1, "USD / 1M tokens", "#0f766e")
    add_line_chart(charts, daily, "daily_output_cost_share", "Daily output cost share", "output_cost_share", 0.01, "percent", "#9333ea")
    add_line_chart(charts, daily, "daily_printed_pages", "Daily human-scale burn", "printed_pages_500w", 1_000_000, "million 500-word pages", "#7c2d12")

    add_line_chart(charts, weekly, "weekly_total_tokens", "Weekly total tokens", "total_tokens", 1_000_000_000, "billion tokens", "#2563eb")
    add_line_chart(charts, weekly, "weekly_cost", "Weekly GPT-5.5 standard cost", "cost_usd", 1, "USD", "#16a34a")
    add_stack_chart(charts, weekly, "weekly_io_stack", "Weekly input/output stack", (("uncached_input_tokens", "uncached input", "#f97316"), ("cached_input_tokens", "cached input", "#22c55e"), ("output_tokens", "output", "#3b82f6")), 1_000_000_000, "billion tokens")
    add_multi_line_chart(charts, weekly, "weekly_output_reasoning", "Weekly output and reasoning output", (("output_tokens", "output", "#2563eb"), ("reasoning_output_tokens", "reasoning output", "#9333ea")), 1_000_000, "million tokens")
    add_multi_line_chart(charts, weekly, "weekly_ratios", "Weekly cache/output/reasoning ratios", (("cache_ratio", "cache ratio", "#16a34a"), ("output_ratio", "output/total", "#2563eb"), ("reasoning_ratio", "reasoning/output", "#9333ea")), 0.01, "percent")
    add_line_chart(charts, weekly, "weekly_cache_savings", "Weekly cache discount saved", "cache_savings_usd", 1, "USD saved", "#059669")
    add_line_chart(charts, weekly, "weekly_effective_cost_per_1m", "Weekly effective USD per 1M total tokens", "effective_usd_per_1m_total_tokens", 1, "USD / 1M tokens", "#0f766e")
    add_line_chart(charts, weekly, "weekly_output_cost_share", "Weekly output cost share", "output_cost_share", 0.01, "percent", "#9333ea")

    model_effort = []
    for row in report.get("model_effort_final_standard_cost_rows") or []:
        clone = dict(row)
        clone["effective_usd_per_1m_total_tokens"] = safe_div((clone.get("model_standard_cost_usd") or 0) * 1_000_000, clone.get("total_tokens", 0))
        model_effort.append(clone)
    add_bar_chart(charts, model_effort, "model_effort_tokens_top20", "Top model+effort buckets by tokens", "total_tokens", "key", 1_000_000_000, "billion tokens", 20, "#7c3aed")
    cost_rows = [row for row in model_effort if row.get("model_standard_cost_usd") is not None]
    add_bar_chart(charts, cost_rows, "model_effort_cost_top20", "Top model+effort buckets by model-standard cost", "model_standard_cost_usd", "key", 1, "USD", 20, "#0f766e")
    add_bar_chart(charts, sorted(model_effort, key=lambda row: row.get("output_tokens", 0), reverse=True), "model_effort_output_top20", "Top model+effort buckets by output tokens", "output_tokens", "key", 1_000_000, "million output tokens", 20, "#2563eb")
    add_bar_chart(charts, sorted(model_effort, key=lambda row: row.get("reasoning_output_tokens", 0), reverse=True), "model_effort_reasoning_top20", "Top model+effort buckets by reasoning output", "reasoning_output_tokens", "key", 1_000_000, "million reasoning tokens", 20, "#9333ea")
    add_bar_chart(charts, sorted(cost_rows, key=lambda row: row.get("effective_usd_per_1m_total_tokens", 0), reverse=True), "model_effort_effective_cost_per_1m_top20", "Top priced model+effort buckets by effective USD per 1M tokens", "effective_usd_per_1m_total_tokens", "key", 1, "USD / 1M tokens", 20, "#be123c")

    top_sessions = [{"label": row.get("session_id", "")[:8], "total_tokens": row.get("total_tokens", 0)} for row in report.get("top_sessions", [])]
    add_bar_chart(charts, top_sessions, "top_sessions_total_tokens", "Top sessions by total tokens", "total_tokens", "label", 1_000_000, "million tokens", 25, "#dc2626")
    top_output_sessions = [{"label": row.get("session_id", "")[:8], "output_tokens": row.get("output_tokens", 0)} for row in report.get("top_output_sessions", [])]
    add_bar_chart(charts, top_output_sessions, "top_sessions_output_tokens", "Top sessions by output tokens", "output_tokens", "label", 1_000, "thousand output tokens", 25, "#2563eb")
    top_reasoning_sessions = [{"label": row.get("session_id", "")[:8], "reasoning_output_tokens": row.get("reasoning_output_tokens", 0)} for row in report.get("top_reasoning_sessions", [])]
    add_bar_chart(charts, top_reasoning_sessions, "top_sessions_reasoning_tokens", "Top sessions by reasoning output tokens", "reasoning_output_tokens", "label", 1_000, "thousand reasoning tokens", 25, "#9333ea")
    top_session_cost = [{"label": row.get("session_id", "")[:8], "gpt_5_5_standard_cost_usd": row.get("gpt_5_5_standard_cost_usd", 0)} for row in report.get("top_sessions", [])]
    add_bar_chart(charts, sorted(top_session_cost, key=lambda row: row["gpt_5_5_standard_cost_usd"], reverse=True), "top_sessions_cost", "Top sessions by GPT-5.5 standard cost", "gpt_5_5_standard_cost_usd", "label", 1, "USD", 25, "#0f766e")

    top_cwd = [{"label": str(row.get("key", ""))[-42:], "total_tokens": row.get("total_tokens", 0)} for row in report.get("top_cwd_usage", [])]
    add_bar_chart(charts, top_cwd, "top_cwd_total_tokens", "Top CWD buckets by total tokens", "total_tokens", "label", 1_000_000_000, "billion tokens", 15, "#0891b2")
    for report_key, chart_name, title, color in (
        ("top_source_usage", "top_source_total_tokens", "Top telemetry sources by total tokens", "#475569"),
        ("top_originator_usage", "top_originator_total_tokens", "Top originators by total tokens", "#7c2d12"),
        ("top_plan_usage", "top_plan_total_tokens", "Top plan buckets by total tokens", "#a16207"),
        ("top_cli_usage", "top_cli_total_tokens", "Top CLI versions by total tokens", "#64748b"),
    ):
        rows = [{"label": tail_label(row.get("key", ""), 34), "total_tokens": row.get("total_tokens", 0)} for row in report.get(report_key, [])]
        add_bar_chart(charts, rows, chart_name, title, "total_tokens", "label", 1_000_000_000, "billion tokens", 20, color)

    scopes = [{"label": key, **value} for key, value in (report.get("scope_economics") or {}).items()]
    add_bar_chart(charts, sorted(scopes, key=lambda row: row["lines"], reverse=True), "project_lines_by_scope", "Current project lines by scope", "lines", "label", 1_000, "thousand lines", 20, "#475569")
    add_bar_chart(charts, sorted(scopes, key=lambda row: row["tokens_per_1k_characters"], reverse=True), "token_density_by_scope", "Token density by current source scope", "tokens_per_1k_characters", "label", 1, "tokens / 1k chars", 20, "#ea580c")
    add_bar_chart(charts, sorted(scopes, key=lambda row: row["gpt_5_5_standard_usd_per_1k_lines"], reverse=True), "cost_per_1k_lines_by_scope", "GPT-5.5 standard cost per 1k current lines by scope", "gpt_5_5_standard_usd_per_1k_lines", "label", 1, "USD / 1k lines", 20, "#16a34a")
    add_bar_chart(charts, sorted(scopes, key=lambda row: row["gpt_5_5_standard_usd_per_1k_characters"], reverse=True), "cost_per_1k_chars_by_scope", "GPT-5.5 standard cost per 1k current characters by scope", "gpt_5_5_standard_usd_per_1k_characters", "label", 1, "USD / 1k chars", 20, "#0f766e")
    add_bar_chart(charts, sorted(scopes, key=lambda row: row["output_tokens_per_1k_characters"], reverse=True), "output_tokens_per_1k_chars_by_scope", "Output tokens per 1k current characters by scope", "output_tokens_per_1k_characters", "label", 1, "output tokens / 1k chars", 20, "#2563eb")

    add_bar_chart(charts, project_metrics["extension_by_files"], "file_counts_by_extension", "Project file counts by extension", "files", "extension", 1, "files", 25, "#0d9488")
    add_bar_chart(charts, project_metrics["extension_by_bytes"], "bytes_by_extension", "Project bytes by extension", "bytes", "extension", 1_000_000, "MB", 25, "#1d4ed8")
    add_bar_chart(charts, project_metrics["extension_by_lines"], "lines_by_extension", "Project text lines by extension", "lines", "extension", 1_000, "thousand lines", 25, "#475569")
    add_bar_chart(charts, project_metrics["extension_by_avg_bytes_per_file"], "avg_bytes_per_file_by_extension", "Average bytes per file by extension", "avg_bytes_per_file", "extension", 1_000, "KB / file", 25, "#0891b2")
    add_bar_chart(charts, project_metrics["extension_by_avg_lines_per_file"], "avg_lines_per_file_by_extension", "Average text lines per file by extension", "avg_lines_per_file", "extension", 1, "lines / file", 25, "#64748b")
    add_bar_chart(charts, project_metrics["root_counts"], "file_counts_by_root", "Project file counts by root folder", "files", "label", 1, "files", 25, "#64748b")
    add_bar_chart(charts, project_metrics["doc_artifacts"], "docs_artifact_counts", "Documentation and audit artifact counts", "count", "label", 1, "count", 25, "#9333ea")
    largest_files = [{"label": tail_label(row["path"], 58), "bytes": row["bytes"]} for row in project_metrics["largest_files"]]
    add_bar_chart(charts, largest_files, "largest_files_by_bytes", "Largest project files by bytes", "bytes", "label", 1_000_000, "MB", 25, "#be123c")
    largest_text_files = [{"label": tail_label(row["path"], 58), "lines": row["lines"]} for row in project_metrics["largest_text_files"]]
    add_bar_chart(charts, largest_text_files, "largest_text_files_by_lines", "Largest text files by lines", "lines", "label", 1_000, "thousand lines", 25, "#7c2d12")

    git_daily = git_metrics.get("daily") or []
    git_weekly = git_metrics.get("weekly") or []
    add_line_chart(charts, git_daily, "git_commits_by_day", "Git commits by day since 2026-04-01", "commits", 1, "commits", "#0f766e")
    add_line_chart(charts, git_daily, "git_churn_by_day", "Git churn by day since 2026-04-01", "churn", 1_000, "thousand changed lines", "#be123c")
    add_multi_line_chart(charts, git_daily, "git_insertions_deletions_by_day", "Git insertions/deletions by day", (("insertions", "insertions", "#16a34a"), ("deletions", "deletions", "#dc2626")), 1_000, "thousand lines")
    add_line_chart(charts, git_daily, "git_files_changed_by_day", "Git files changed by day since 2026-04-01", "files_changed", 1, "files", "#0891b2")
    add_line_chart(charts, git_daily, "git_net_lines_by_day", "Git net lines by day since 2026-04-01", "net_lines", 1_000, "thousand net lines", "#7c3aed")
    add_line_chart(charts, git_daily, "git_churn_per_commit_by_day", "Git churn per commit by day", "churn_per_commit", 1_000, "thousand changed lines / commit", "#ea580c")
    add_line_chart(charts, git_weekly, "git_commits_by_week", "Git commits by ISO week", "commits", 1, "commits", "#0f766e")
    add_line_chart(charts, git_weekly, "git_churn_by_week", "Git churn by ISO week", "churn", 1_000, "thousand changed lines", "#be123c")
    add_line_chart(charts, git_weekly, "git_files_changed_by_week", "Git files changed by ISO week", "files_changed", 1, "files", "#0891b2")
    add_line_chart(charts, git_weekly, "git_net_lines_by_week", "Git net lines by ISO week", "net_lines", 1_000, "thousand net lines", "#7c3aed")
    add_line_chart(charts, git_weekly, "git_churn_per_commit_by_week", "Git churn per commit by ISO week", "churn_per_commit", 1_000, "thousand changed lines / commit", "#ea580c")
    add_heatmap(charts, git_metrics.get("weekday_hour_commit_heatmap") or [[0] * 24 for _ in range(7)], "git_commit_weekday_hour_heatmap", "Git commit heatmap by Samara weekday/hour", hour_labels, weekday_labels, colorbar_label="commits")
    add_heatmap(charts, git_metrics.get("weekday_hour_churn_heatmap") or [[0] * 24 for _ in range(7)], "git_churn_weekday_hour_heatmap", "Git churn heatmap by Samara weekday/hour", hour_labels, weekday_labels, "Committed changed-line pressure by Samara weekday and hour.", "changed lines")

    token_git_daily = join_token_git_daily(daily, git_daily)
    add_line_chart(charts, token_git_daily, "daily_tokens_per_git_changed_line", "Daily tokens per committed changed line", "tokens_per_changed_line", 1_000, "thousand tokens / changed line", "#dc2626", "Correlation-only: token usage and git churn are grouped by calendar day, not causally matched per task.")
    add_line_chart(charts, token_git_daily, "daily_cost_per_git_changed_line", "Daily GPT-5.5 cost per committed changed line", "cost_per_changed_line", 1, "USD / changed line", "#16a34a", "Correlation-only: token usage and git churn are grouped by calendar day, not causally matched per task.")
    add_scatter_chart(charts, [row for row in token_git_daily if row["churn"] > 0], "daily_tokens_vs_git_churn", "Daily tokens vs git churn", "churn", "total_tokens", 1_000, 1_000_000_000, "thousand changed lines", "billion tokens", "commits", "Correlation-only scatter: shows whether high-token days also had high committed churn.")
    add_scatter_chart(charts, [row for row in token_git_daily if row["churn"] > 0], "daily_cost_vs_git_churn", "Daily GPT-5.5 cost vs git churn", "churn", "cost_usd", 1_000, 1, "thousand changed lines", "USD", "commits", "Correlation-only scatter: shows cost pressure against committed churn.")

    velocity = ((report.get("previous_snapshot_delta") or {}).get("velocity") or {})
    token_velocity_rows = [
        {"label": "total", "value": velocity.get("total_tokens_per_hour", 0)},
        {"label": "input", "value": velocity.get("input_tokens_per_hour", 0)},
        {"label": "cached input", "value": velocity.get("cached_input_tokens_per_hour", 0)},
        {"label": "uncached input", "value": velocity.get("uncached_input_tokens_per_hour", 0)},
        {"label": "output", "value": velocity.get("output_tokens_per_hour", 0)},
        {"label": "reasoning", "value": velocity.get("reasoning_output_tokens_per_hour", 0)},
    ]
    add_bar_chart(charts, token_velocity_rows, "current_snapshot_token_velocity", "Current snapshot token velocity by class", "value", "label", 1_000_000, "million tokens/hour", 12, "#2563eb")
    money_velocity_rows = [
        {"label": "GPT-5.5 standard / hour", "value": velocity.get("gpt_5_5_standard_usd_per_hour", 0)},
        {"label": "GPT-5.5 priority / hour", "value": velocity.get("gpt_5_5_priority_usd_per_hour", 0)},
        {"label": "GPT-5.3-codex / hour", "value": velocity.get("gpt_5_3_codex_standard_usd_per_hour", 0)},
        {"label": "GPT-5.5 standard / day", "value": velocity.get("gpt_5_5_standard_usd_per_day", 0)},
    ]
    add_bar_chart(charts, money_velocity_rows, "current_snapshot_money_velocity", "Current snapshot API-equivalent money velocity", "value", "label", 1, "USD", 12, "#16a34a")
    code_velocity_rows = [
        {"label": "primary C# lines/hour", "value": velocity.get("primary_code_lines_per_hour", 0)},
        {"label": "primary C# lines/day", "value": velocity.get("primary_code_lines_per_day", 0)},
        {"label": "tokens/net line", "value": velocity.get("tokens_per_net_primary_code_line", 0)},
        {"label": "USD/net line", "value": velocity.get("gpt_5_5_standard_usd_per_net_primary_code_line", 0)},
    ]
    add_bar_chart(charts, code_velocity_rows, "current_snapshot_code_velocity", "Current snapshot code and density velocity", "value", "label", 1, "raw value", 12, "#7c3aed")

    scale = report.get("layperson_scale") or {}
    all_time = scale.get("all_time") or {}
    current = scale.get("since_previous_snapshot") or {}
    scale_rows = [
        {"label": "500-word pages (M)", "value": safe_div(all_time.get("approx_printed_pages_500_words", 0), 1_000_000)},
        {"label": "80k-word books (K)", "value": safe_div(all_time.get("approx_80k_word_books", 0), 1_000)},
        {"label": "reading years", "value": all_time.get("continuous_reading_years_at_250_wpm", 0)},
        {"label": "$60 games", "value": all_time.get("equivalent_60_usd_games", 0)},
        {"label": "$2k workstations", "value": all_time.get("equivalent_2000_usd_workstations", 0)},
    ]
    add_bar_chart(charts, scale_rows, "layperson_all_time_scale", "All-time token scale for non-specialists", "value", "label", 1, "unit noted in label", 12, "#7c2d12")
    burn_rows = [
        {"label": "tokens/sec", "value": current.get("tokens_per_second", 0)},
        {"label": "words/sec equiv", "value": current.get("approx_words_per_second", 0)},
        {"label": "pages/hour equiv", "value": current.get("approx_pages_per_hour", 0)},
        {"label": "USD/hour", "value": current.get("gpt_5_5_standard_usd_per_hour", 0)},
        {"label": "USD/day", "value": current.get("gpt_5_5_standard_usd_per_day_at_current_velocity", 0)},
    ]
    add_bar_chart(charts, burn_rows, "layperson_current_burn_rate", "Current burn-rate scale for non-specialists", "value", "label", 1, "raw value", 12, "#be123c")

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
        f"| Long-range chart windows | {', '.join(str(days) + 'd' for days in LONG_RANGE_WINDOWS_DAYS)} |",
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
        "- Git churn and token-vs-git charts use committed history; uncommitted live-agent work is visible only after commit.",
        "- Token-vs-git scatter plots are same-day correlations, not per-task causal attribution.",
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
        "schema": "hecton8.project_metrics_dashboard.v2",
        "generated_at_samara": datetime.datetime.now(SAMARA).isoformat(),
        "token_report": str(TOKEN_JSON.relative_to(PROJECT)).replace("\\", "/"),
        "dashboard_markdown": str(DASHBOARD_MD.relative_to(PROJECT)).replace("\\", "/"),
        "chart_count": len(charts),
        "long_range_windows_days": list(LONG_RANGE_WINDOWS_DAYS),
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

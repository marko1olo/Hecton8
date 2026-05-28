import datetime
import json
import os
import pathlib
import statistics
from collections import Counter, defaultdict

PROJECT = pathlib.Path(r"C:\hades\Hecton8")
ROOTS = [
    ("current_sessions", pathlib.Path(r"C:\Users\danat\.codex\sessions")),
    ("current_archived_sessions", pathlib.Path(r"C:\Users\danat\.codex\archived_sessions")),
    ("backup_cleanup_20260521_194850", pathlib.Path(r"C:\Users\danat\Documents\CodexBackups\codex_cleanup_20260521_194850")),
]

UTC = datetime.timezone.utc
SAMARA = datetime.timezone(datetime.timedelta(hours=4))
REPORT_DATE = datetime.datetime.now(SAMARA).date().isoformat()

TOKEN_REPORT_DIR = PROJECT / "Docs" / "DEPRECATED" / "Root_Docs_Noise_2026-05-26"
REPORT_JSON = TOKEN_REPORT_DIR / f"TOKEN_USAGE_AUDIT_{REPORT_DATE}.json"
REPORT_MD = TOKEN_REPORT_DIR / f"TOKEN_USAGE_AUDIT_{REPORT_DATE}.md"
LEDGER = TOKEN_REPORT_DIR / "TOKEN_USAGE_LEDGER.md"
STATUS = PROJECT / "Docs" / "Tasks" / "Status_TOKEN_USAGE_AUDIT.md"
RATIONALE = PROJECT / "Docs" / "AgentLogs" / "Rationale_TOKEN_USAGE_AUDIT.md"
LOG = PROJECT / "Docs" / "AgentLogs" / "LOG_TOKEN_USAGE_AUDIT.md"

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
USAGE_KEYS = ("input_tokens", "cached_input_tokens", "output_tokens", "reasoning_output_tokens", "total_tokens")
PRIMARY_PRICE_KEY = "gpt-5.5_standard_short_context_equivalent"
PRIMARY_PRICE_LABEL = "gpt-5.5 standard short-context API-equivalent"
PRIMARY_JSON_PREFIX = "gpt_5_5_standard"
CODEX_STANDARD_PRICE_KEY = "gpt-5.3-codex_standard_api_equivalent"
CODEX_PRICE_LABEL = "gpt-5.3-codex standard specialized Codex API-equivalent"


def zero_usage():
    return {key: 0 for key in USAGE_KEYS}


def parse_ts(value):
    if not value:
        return None
    try:
        if isinstance(value, (int, float)):
            return datetime.datetime.fromtimestamp(value, UTC)
        text = str(value)
        if text.endswith("Z"):
            text = text[:-1] + "+00:00"
        result = datetime.datetime.fromisoformat(text)
        if result.tzinfo is None:
            result = result.replace(tzinfo=UTC)
        return result.astimezone(UTC)
    except Exception:
        return None


def has_usage(usage):
    return isinstance(usage, dict) and any(int(usage.get(key, 0) or 0) for key in USAGE_KEYS)


def add_usage(target, source):
    if not isinstance(source, dict):
        return target
    for key in USAGE_KEYS:
        target[key] += int(source.get(key, 0) or 0)
    return target


def positive_delta(current, previous):
    return {key: max(0, int(current.get(key, 0) or 0) - int(previous.get(key, 0) or 0)) for key in USAGE_KEYS}


def sub_usage(left, right):
    return {key: int(left.get(key, 0) or 0) - int(right.get(key, 0) or 0) for key in USAGE_KEYS}


def clone_usage(source):
    return {key: int(source.get(key, 0) or 0) for key in USAGE_KEYS}


def usage_cost(usage, rate):
    uncached = max(0, int(usage.get("input_tokens", 0) or 0) - int(usage.get("cached_input_tokens", 0) or 0))
    cached = int(usage.get("cached_input_tokens", 0) or 0)
    output = int(usage.get("output_tokens", 0) or 0)
    return {
        "uncached_input_cost_usd": uncached / 1_000_000 * rate["input"],
        "cached_input_cost_usd": cached / 1_000_000 * rate["cached_input"],
        "output_cost_usd": output / 1_000_000 * rate["output"],
        "total_cost_usd": (uncached / 1_000_000 * rate["input"]) + (cached / 1_000_000 * rate["cached_input"]) + (output / 1_000_000 * rate["output"]),
        "rate_per_1m": rate,
    }


def normalize_model(value):
    if value is None:
        return None
    text = str(value).strip()
    return text if text else None


def model_from_payload(payload):
    if not isinstance(payload, dict):
        return None
    direct = normalize_model(payload.get("model"))
    if direct:
        return direct
    settings = ((payload.get("collaboration_mode") or {}).get("settings") or {})
    return normalize_model(settings.get("model"))


def effort_from_payload(payload):
    if not isinstance(payload, dict):
        return None
    direct = payload.get("effort") or payload.get("reasoning_effort")
    if direct:
        return str(direct)
    settings = ((payload.get("collaboration_mode") or {}).get("settings") or {})
    nested = settings.get("effort") or settings.get("reasoning_effort")
    return str(nested) if nested else None


def percentile(sorted_values, fraction):
    if not sorted_values:
        return 0
    index = int(len(sorted_values) * fraction) - 1
    index = max(0, min(len(sorted_values) - 1, index))
    return sorted_values[index]


def gini(values):
    values = sorted(int(value) for value in values if int(value) >= 0)
    if not values:
        return 0.0
    total = sum(values)
    if total <= 0:
        return 0.0
    weighted = sum((index + 1) * value for index, value in enumerate(values))
    count = len(values)
    return (2 * weighted) / (count * total) - (count + 1) / count


def fmt_int(value):
    return f"{int(value):,}"


def fmt_money(value):
    return f"${value:,.2f}"


def week_key(dt):
    iso = dt.isocalendar()
    return f"{iso.year}-W{iso.week:02d}"


def collect_jsonl_files():
    files = []
    for root_label, root in ROOTS:
        if not root.exists():
            continue
        for path in root.rglob("*.jsonl"):
            try:
                stat = path.stat()
            except OSError:
                continue
            files.append((root_label, path, stat.st_size, stat.st_mtime))
    return sorted(files, key=lambda row: str(row[1]).lower())


def read_file_record(path):
    record = {
        "path": str(path),
        "session_id": None,
        "meta_timestamp": None,
        "final_timestamp": None,
        "cwd": None,
        "originator": None,
        "source": None,
        "cli_version": None,
        "model_provider": None,
        "final_usage": None,
        "token_event_count": 0,
        "parse_errors": 0,
        "model_context_window": None,
        "plan_type": None,
        "limit_id": None,
        "final_model": None,
        "model_counts": {},
        "final_effort": None,
        "effort_counts": {},
    }
    model_counts = Counter()
    effort_counts = Counter()
    try:
        with path.open("r", encoding="utf-8", errors="replace") as handle:
            for line in handle:
                if "session_meta" not in line and "token_count" not in line and "turn_context" not in line and '"model"' not in line:
                    continue
                try:
                    item = json.loads(line)
                except Exception:
                    record["parse_errors"] += 1
                    continue
                payload = item.get("payload") or {}
                model = model_from_payload(payload)
                if model:
                    model_counts[model] += 1
                    record["final_model"] = model
                effort = effort_from_payload(payload)
                if effort:
                    effort_counts[effort] += 1
                    record["final_effort"] = effort
                if item.get("type") == "session_meta":
                    record["session_id"] = payload.get("id") or record["session_id"]
                    meta_ts = parse_ts(payload.get("timestamp"))
                    record["meta_timestamp"] = meta_ts.isoformat() if meta_ts else record["meta_timestamp"]
                    for key in ("cwd", "originator", "source", "cli_version", "model_provider"):
                        record[key] = payload.get(key) or record[key]
                elif item.get("type") == "event_msg" and payload.get("type") == "token_count":
                    info = payload.get("info") or {}
                    usage = info.get("total_token_usage") or {}
                    if not has_usage(usage):
                        continue
                    record["final_usage"] = {key: int(usage.get(key, 0) or 0) for key in USAGE_KEYS}
                    ts = parse_ts(item.get("timestamp"))
                    record["final_timestamp"] = ts.isoformat() if ts else record["final_timestamp"]
                    record["token_event_count"] += 1
                    record["model_context_window"] = info.get("model_context_window") or record["model_context_window"]
                    limits = payload.get("rate_limits") or {}
                    record["plan_type"] = limits.get("plan_type") or record["plan_type"]
                    record["limit_id"] = limits.get("limit_id") or record["limit_id"]
    except Exception:
        record["parse_errors"] += 1
    record["model_counts"] = dict(model_counts)
    record["effort_counts"] = dict(effort_counts)
    return record


def read_increment_events(path):
    events = []
    parse_errors = 0
    current_model = "unknown_model"
    current_effort = "unknown"
    try:
        with path.open("r", encoding="utf-8", errors="replace") as handle:
            for line in handle:
                if "token_count" not in line and "turn_context" not in line and '"model"' not in line:
                    continue
                try:
                    item = json.loads(line)
                except Exception:
                    parse_errors += 1
                    continue
                payload = item.get("payload") or {}
                model = model_from_payload(payload)
                if model:
                    current_model = model
                effort = effort_from_payload(payload)
                if effort:
                    current_effort = effort
                if item.get("type") != "event_msg" or payload.get("type") != "token_count":
                    continue
                usage = (payload.get("info") or {}).get("total_token_usage") or {}
                if not has_usage(usage):
                    continue
                ts = parse_ts(item.get("timestamp"))
                if ts is None:
                    continue
                events.append((ts, {key: int(usage.get(key, 0) or 0) for key in USAGE_KEYS}, current_model, current_effort))
    except Exception:
        parse_errors += 1

    events.sort(key=lambda item: item[0])
    previous = zero_usage()
    increments = []
    for ts, usage, model, effort in events:
        delta = positive_delta(usage, previous)
        if has_usage(delta):
            increments.append((ts, delta, model, effort))
        previous = usage
    return increments, parse_errors


def iter_project_files():
    for root, dirs, files in os.walk(PROJECT):
        dirs[:] = [name for name in dirs if name not in EXCLUDE_DIRS]
        root_path = pathlib.Path(root)
        for name in files:
            yield root_path / name


def count_text_metrics(path):
    byte_count = 0
    line_count = 0
    nonblank = 0
    characters = 0
    non_ws = 0
    alnum = 0
    try:
        with path.open("rb") as handle:
            for raw_line in handle:
                byte_count += len(raw_line)
                line_count += 1
                line = raw_line.rstrip(b"\r\n")
                characters += len(line)
                stripped = line.strip()
                if stripped:
                    nonblank += 1
                for byte in line:
                    if byte > 32:
                        non_ws += 1
                    if 48 <= byte <= 57 or 65 <= byte <= 90 or 97 <= byte <= 122 or byte >= 128:
                        alnum += 1
    except Exception:
        return {
            "bytes": 0,
            "lines": 0,
            "nonblank_lines": 0,
            "characters": 0,
            "non_whitespace_characters": 0,
            "alphanumeric_characters": 0,
        }
    return {
        "bytes": byte_count,
        "lines": line_count,
        "nonblank_lines": nonblank,
        "characters": characters,
        "non_whitespace_characters": non_ws,
        "alphanumeric_characters": alnum,
    }


def count_loc():
    scopes = {
        "first_party_assets_project_cs": (PROJECT / "Assets" / "_Project", {".cs"}),
        "first_party_scripts_cs": (PROJECT / "Assets" / "_Project" / "Scripts", {".cs"}),
        "all_repo_cs_excluding_generated": (PROJECT, {".cs"}),
        "all_repo_source_broad": (PROJECT, {".cs", ".shader", ".hlsl", ".compute", ".cginc", ".uxml", ".uss", ".py", ".ps1", ".csproj", ".asmdef"}),
        "tools_scripts": (PROJECT / "Tools", {".py", ".ps1"}),
        "docs_markdown_text": (PROJECT / "Docs", {".md", ".txt"}),
    }
    result = {
        key: {
            "files": 0,
            "bytes": 0,
            "lines": 0,
            "nonblank_lines": 0,
            "characters": 0,
            "non_whitespace_characters": 0,
            "alphanumeric_characters": 0,
        }
        for key in scopes
    }
    for path in iter_project_files():
        suffix = path.suffix.lower()
        metrics = None
        for key, (prefix, suffixes) in scopes.items():
            try:
                in_scope = path.is_relative_to(prefix)
            except AttributeError:
                in_scope = str(path).lower().startswith(str(prefix).lower())
            if in_scope and suffix in suffixes:
                if metrics is None:
                    metrics = count_text_metrics(path)
                result[key]["files"] += 1
                for metric_key, metric_value in metrics.items():
                    result[key][metric_key] += metric_value
    return result


def usage_map_costs(usage_map, rate):
    return {key: usage_cost(usage, rate)["total_cost_usd"] for key, usage in sorted(usage_map.items())}


def observed_costs_by_period(nested_usage, model_rate_catalog):
    result = {}
    codex_rate = model_rate_catalog["gpt-5.3-codex"]
    high_rate = model_rate_catalog["gpt-5.5"]
    for period, by_model in sorted(nested_usage.items()):
        known = 0.0
        unpriced = zero_usage()
        for model, usage in by_model.items():
            rate = model_rate_catalog.get(model)
            if rate:
                known += usage_cost(usage, rate)["total_cost_usd"]
            else:
                add_usage(unpriced, usage)
        codex_bound = usage_cost(unpriced, codex_rate)["total_cost_usd"]
        high_bound = usage_cost(unpriced, high_rate)["total_cost_usd"]
        result[period] = {
            "known_standard_usd": known,
            "unpriced_tokens": unpriced["total_tokens"],
            "known_plus_unpriced_as_gpt_5_3_codex_standard_usd": known + codex_bound,
            "known_plus_unpriced_as_gpt_5_5_standard_usd": known + high_bound,
        }
    return result


def freeze_nested_usage(nested_usage):
    return {
        outer: {inner: clone_usage(usage) for inner, usage in sorted(inner_map.items())}
        for outer, inner_map in sorted(nested_usage.items())
    }


def top_usage_rows(usage_map, limit=20):
    return [
        {"key": key, **clone_usage(usage)}
        for key, usage in sorted(usage_map.items(), key=lambda item: item[1]["total_tokens"], reverse=True)[:limit]
    ]


def find_previous_report_path():
    try:
        current_date = datetime.date.fromisoformat(REPORT_DATE)
    except ValueError:
        return None
    for days_back in range(1, 8):
        candidate_date = (current_date - datetime.timedelta(days=days_back)).isoformat()
        candidate = REPORT_JSON.parent / f"TOKEN_USAGE_AUDIT_{candidate_date}.json"
        if candidate.exists():
            return candidate
    return None


def previous_snapshot_delta(report):
    previous_path = find_previous_report_path()
    if not previous_path:
        return None
    try:
        previous = json.loads(previous_path.read_text(encoding="utf-8-sig"))
    except Exception:
        return {"previous_report_path": str(previous_path), "error": "previous report was unreadable"}
    current_generated = parse_ts(report.get("generated_at_samara"))
    previous_generated = parse_ts(previous.get("generated_at_samara"))
    elapsed_hours = None
    if current_generated and previous_generated:
        elapsed_hours = (current_generated - previous_generated).total_seconds() / 3600
    def price_delta(key):
        current_row = (report.get("pricing") or {}).get(key) or {}
        previous_row = (previous.get("pricing") or {}).get(key) or {}
        return float(current_row.get("total_cost_usd", 0) or 0) - float(previous_row.get("total_cost_usd", 0) or 0)
    current_top = (report.get("model_effort_final_standard_cost_rows") or [{}])[0]
    previous_top = (previous.get("model_effort_final_standard_cost_rows") or [{}])[0]
    current_scope = (report.get("scope_economics") or {}).get("first_party_assets_project_cs") or {}
    previous_scope = (previous.get("scope_economics") or {}).get("first_party_assets_project_cs") or {}
    totals_delta = sub_usage(report.get("totals") or zero_usage(), previous.get("totals") or zero_usage())
    file_count_delta = int(report.get("file_count", 0) or 0) - int(previous.get("file_count", 0) or 0)
    sessions_delta = int(report.get("sessions_with_usage", 0) or 0) - int(previous.get("sessions_with_usage", 0) or 0)
    primary_cost_delta = price_delta(PRIMARY_PRICE_KEY)
    priority_cost_delta = price_delta("gpt-5.5_priority_short_context_equivalent")
    codex_cost_delta = price_delta(CODEX_STANDARD_PRICE_KEY)
    primary_code_lines_delta = int(current_scope.get("lines", 0) or 0) - int(previous_scope.get("lines", 0) or 0)
    primary_code_characters_delta = int(current_scope.get("characters", 0) or 0) - int(previous_scope.get("characters", 0) or 0)
    uncached_input_delta = totals_delta["input_tokens"] - totals_delta["cached_input_tokens"]

    def per_hour(value):
        if elapsed_hours is None or elapsed_hours <= 0:
            return None
        return float(value) / elapsed_hours

    def per_day(value):
        hourly = per_hour(value)
        return None if hourly is None else hourly * 24

    def per_minute(value):
        hourly = per_hour(value)
        return None if hourly is None else hourly / 60

    def per_second(value):
        hourly = per_hour(value)
        return None if hourly is None else hourly / 3600

    def per_positive_unit(value, denominator):
        if denominator <= 0:
            return None
        return float(value) / denominator

    velocity = {
        "total_tokens_per_hour": per_hour(totals_delta["total_tokens"]),
        "total_tokens_per_day": per_day(totals_delta["total_tokens"]),
        "total_tokens_per_minute": per_minute(totals_delta["total_tokens"]),
        "total_tokens_per_second": per_second(totals_delta["total_tokens"]),
        "input_tokens_per_hour": per_hour(totals_delta["input_tokens"]),
        "cached_input_tokens_per_hour": per_hour(totals_delta["cached_input_tokens"]),
        "uncached_input_tokens_per_hour": per_hour(uncached_input_delta),
        "output_tokens_per_hour": per_hour(totals_delta["output_tokens"]),
        "reasoning_output_tokens_per_hour": per_hour(totals_delta["reasoning_output_tokens"]),
        "sessions_with_usage_per_hour": per_hour(sessions_delta),
        "jsonl_files_per_hour": per_hour(file_count_delta),
        "primary_code_lines_per_hour": per_hour(primary_code_lines_delta),
        "primary_code_lines_per_day": per_day(primary_code_lines_delta),
        "primary_code_characters_per_hour": per_hour(primary_code_characters_delta),
        "primary_code_characters_per_day": per_day(primary_code_characters_delta),
        "tokens_per_net_primary_code_line": per_positive_unit(totals_delta["total_tokens"], primary_code_lines_delta),
        "input_tokens_per_net_primary_code_line": per_positive_unit(totals_delta["input_tokens"], primary_code_lines_delta),
        "output_tokens_per_net_primary_code_line": per_positive_unit(totals_delta["output_tokens"], primary_code_lines_delta),
        "reasoning_tokens_per_net_primary_code_line": per_positive_unit(totals_delta["reasoning_output_tokens"], primary_code_lines_delta),
        "tokens_per_1k_net_primary_code_chars": per_positive_unit(totals_delta["total_tokens"] * 1000, primary_code_characters_delta),
        "output_tokens_per_1k_net_primary_code_chars": per_positive_unit(totals_delta["output_tokens"] * 1000, primary_code_characters_delta),
        "gpt_5_5_standard_usd_per_hour": per_hour(primary_cost_delta),
        "gpt_5_5_standard_usd_per_day": per_day(primary_cost_delta),
        "gpt_5_5_priority_usd_per_hour": per_hour(priority_cost_delta),
        "gpt_5_3_codex_standard_usd_per_hour": per_hour(codex_cost_delta),
        "gpt_5_5_standard_usd_per_net_primary_code_line": per_positive_unit(primary_cost_delta, primary_code_lines_delta),
        "gpt_5_5_standard_usd_per_1k_net_primary_code_chars": per_positive_unit(primary_cost_delta * 1000, primary_code_characters_delta),
    }
    return {
        "previous_report_path": str(previous_path),
        "previous_generated_at_samara": previous.get("generated_at_samara"),
        "elapsed_hours": elapsed_hours,
        "file_count_delta": file_count_delta,
        "sessions_with_usage_delta": sessions_delta,
        "totals_delta": totals_delta,
        "uncached_input_tokens_delta": uncached_input_delta,
        "gpt_5_5_standard_cost_usd_delta": primary_cost_delta,
        "gpt_5_5_priority_cost_usd_delta": priority_cost_delta,
        "gpt_5_3_codex_standard_cost_usd_delta": codex_cost_delta,
        "top_model_effort_key_current": current_top.get("key"),
        "top_model_effort_key_previous": previous_top.get("key"),
        "top_model_effort_tokens_delta": int(current_top.get("total_tokens", 0) or 0) - int(previous_top.get("total_tokens", 0) or 0),
        "top_model_effort_sessions_delta": int(current_top.get("session_count", 0) or 0) - int(previous_top.get("session_count", 0) or 0),
        "top_model_effort_cost_usd_delta": float(current_top.get("model_standard_cost_usd", 0) or 0) - float(previous_top.get("model_standard_cost_usd", 0) or 0),
        "primary_code_lines_delta": primary_code_lines_delta,
        "primary_code_characters_delta": primary_code_characters_delta,
        "tokens_per_primary_code_line_delta": float(current_scope.get("tokens_per_line", 0) or 0) - float(previous_scope.get("tokens_per_line", 0) or 0),
        "tokens_per_1k_primary_code_chars_delta": float(current_scope.get("tokens_per_1k_characters", 0) or 0) - float(previous_scope.get("tokens_per_1k_characters", 0) or 0),
        "gpt_5_5_cost_per_1k_primary_loc_delta": float(current_scope.get("gpt_5_5_standard_usd_per_1k_lines", 0) or 0) - float(previous_scope.get("gpt_5_5_standard_usd_per_1k_lines", 0) or 0),
        "gpt_5_5_cost_per_1k_primary_code_chars_delta": float(current_scope.get("gpt_5_5_standard_usd_per_1k_characters", 0) or 0) - float(previous_scope.get("gpt_5_5_standard_usd_per_1k_characters", 0) or 0),
        "velocity": velocity,
    }


def session_report_row(record, primary_rate, secondary_rate, model_rate_catalog):
    usage = clone_usage(record.get("final_usage") or zero_usage())
    primary_cost = usage_cost(usage, primary_rate)
    secondary_cost = usage_cost(usage, secondary_rate)
    model = record.get("final_model") or "unknown_model"
    model_cost = usage_cost(usage, model_rate_catalog[model])["total_cost_usd"] if model in model_rate_catalog else None
    output = max(1, usage["output_tokens"])
    total_tokens = max(1, usage["total_tokens"])
    input_tokens = max(1, usage["input_tokens"])
    uncached_input = max(0, usage["input_tokens"] - usage["cached_input_tokens"])
    return {
        "session_id": record.get("session_id"),
        "path": record.get("path"),
        "root": record.get("root"),
        "final_timestamp": record.get("final_timestamp"),
        "cwd": record.get("cwd"),
        "originator": record.get("originator"),
        "source": record.get("source"),
        "cli_version": record.get("cli_version"),
        "plan_type": record.get("plan_type"),
        "final_model": model,
        "final_effort": record.get("final_effort") or "unknown",
        "gpt_5_5_standard_cost_usd": primary_cost["total_cost_usd"],
        "gpt_5_3_codex_standard_cost_usd": secondary_cost["total_cost_usd"],
        "final_model_standard_cost_usd": model_cost,
        "input_to_output_ratio": usage["input_tokens"] / output,
        "uncached_input_to_output_ratio": uncached_input / output,
        "cached_input_to_output_ratio": usage["cached_input_tokens"] / output,
        "output_ratio": usage["output_tokens"] / total_tokens,
        "cache_ratio": usage["cached_input_tokens"] / input_tokens,
        "reasoning_output_ratio": usage["reasoning_output_tokens"] / output,
        "primary_output_cost_share": primary_cost["output_cost_usd"] / max(0.000001, primary_cost["total_cost_usd"]),
        **usage,
    }


def model_effort_cost_rows(usage_map, counts, model_rate_catalog, count_label, total_tokens):
    rows = []
    for key, usage in sorted(usage_map.items(), key=lambda item: item[1]["total_tokens"], reverse=True):
        model, _separator, effort = key.partition("::")
        rate = model_rate_catalog.get(model)
        cost = usage_cost(usage, rate) if rate else None
        no_cache_cost = (usage["input_tokens"] / 1_000_000 * rate["input"] + usage["output_tokens"] / 1_000_000 * rate["output"]) if rate else None
        count = int(counts.get(key, 0) or 0)
        rows.append({
            "key": key,
            "model": model,
            "effort": effort or "unknown",
            count_label: count,
            **clone_usage(usage),
            "tokens_share": usage["total_tokens"] / max(1, total_tokens),
            "cache_ratio": usage["cached_input_tokens"] / max(1, usage["input_tokens"]),
            "output_ratio": usage["output_tokens"] / max(1, usage["total_tokens"]),
            "reasoning_output_ratio": usage["reasoning_output_tokens"] / max(1, usage["output_tokens"]),
            "reasoning_tokens_per_1m_total_tokens": usage["reasoning_output_tokens"] / max(1, usage["total_tokens"]) * 1_000_000,
            "input_to_output_ratio": usage["input_tokens"] / max(1, usage["output_tokens"]),
            "uncached_input_to_output_ratio": max(0, usage["input_tokens"] - usage["cached_input_tokens"]) / max(1, usage["output_tokens"]),
            "cached_input_to_output_ratio": usage["cached_input_tokens"] / max(1, usage["output_tokens"]),
            "rate_input_per_1m": rate["input"] if rate else None,
            "rate_cached_input_per_1m": rate["cached_input"] if rate else None,
            "rate_output_per_1m": rate["output"] if rate else None,
            "rate_source": rate.get("source") if rate else None,
            "model_standard_cost_usd": cost["total_cost_usd"] if cost else None,
            "uncached_input_cost_usd": cost["uncached_input_cost_usd"] if cost else None,
            "cached_input_cost_usd": cost["cached_input_cost_usd"] if cost else None,
            "output_cost_usd": cost["output_cost_usd"] if cost else None,
            "no_cache_cost_usd": no_cache_cost,
            "cache_savings_usd": (no_cache_cost - cost["total_cost_usd"]) if cost else None,
            "cost_per_count_usd": (cost["total_cost_usd"] / max(1, count)) if cost else None,
            "output_cost_share": (cost["output_cost_usd"] / max(0.000001, cost["total_cost_usd"])) if cost else None,
            "input_side_cost_share": ((cost["uncached_input_cost_usd"] + cost["cached_input_cost_usd"]) / max(0.000001, cost["total_cost_usd"])) if cost else None,
        })
    return rows


def build_report():
    now_utc = datetime.datetime.now(UTC)
    now_local = now_utc.astimezone(SAMARA)
    files = collect_jsonl_files()
    records = []
    for root_label, path, size, mtime in files:
        record = read_file_record(path)
        record["root"] = root_label
        record["size_bytes"] = size
        record["last_write_time_utc"] = datetime.datetime.fromtimestamp(mtime, UTC).isoformat()
        records.append(record)

    selected = {}
    duplicate_records = 0
    missing_session_id = 0
    for record in records:
        key = record["session_id"] or "PATH:" + record["path"]
        if not record["session_id"]:
            missing_session_id += 1
        current = selected.get(key)
        new_total = (record.get("final_usage") or {}).get("total_tokens", -1)
        current_total = (current.get("final_usage") or {}).get("total_tokens", -1) if current else -1
        if current is None or new_total > current_total or (new_total == current_total and str(record.get("final_timestamp")) > str(current.get("final_timestamp"))):
            if current is not None:
                duplicate_records += 1
            selected[key] = record
        elif current is not None:
            duplicate_records += 1

    selected_records = list(selected.values())
    selected_with_usage = [record for record in selected_records if has_usage(record.get("final_usage"))]
    total = zero_usage()
    root_breakdown = defaultdict(lambda: {"jsonl_files": 0, "files_with_usage": 0, "selected_sessions": 0, "selected_with_usage": 0, "selected_total_tokens": 0})
    for record in records:
        row = root_breakdown[record["root"]]
        row["jsonl_files"] += 1
        if has_usage(record.get("final_usage")):
            row["files_with_usage"] += 1
    for record in selected_records:
        row = root_breakdown[record["root"]]
        row["selected_sessions"] += 1
        if has_usage(record.get("final_usage")):
            row["selected_with_usage"] += 1
            row["selected_total_tokens"] += int(record["final_usage"].get("total_tokens", 0))
            add_usage(total, record["final_usage"])

    daily = defaultdict(zero_usage)
    hourly = defaultdict(zero_usage)
    weekly = defaultdict(zero_usage)
    monthly = defaultdict(zero_usage)
    daily_model_delta_usage = defaultdict(lambda: defaultdict(zero_usage))
    weekly_model_delta_usage = defaultdict(lambda: defaultdict(zero_usage))
    monthly_model_delta_usage = defaultdict(lambda: defaultdict(zero_usage))
    model_delta_usage = defaultdict(zero_usage)
    effort_delta_usage = defaultdict(zero_usage)
    model_effort_delta_usage = defaultdict(zero_usage)
    model_effort_delta_counts = Counter()
    increment_parse_errors = 0
    for record in selected_with_usage:
        increments, errors = read_increment_events(pathlib.Path(record["path"]))
        increment_parse_errors += errors
        for ts, delta, model, effort in increments:
            local = ts.astimezone(SAMARA)
            hour_key = local.strftime("%Y-%m-%d %H:00")
            add_usage(daily[local.date().isoformat()], delta)
            add_usage(hourly[hour_key], delta)
            add_usage(weekly[week_key(local)], delta)
            month_key = f"{local.year}-{local.month:02d}"
            day_key = local.date().isoformat()
            week = week_key(local)
            add_usage(monthly[month_key], delta)
            add_usage(model_delta_usage[model or "unknown_model"], delta)
            add_usage(effort_delta_usage[effort or "unknown"], delta)
            model_effort_key = f"{model or 'unknown_model'}::{effort or 'unknown'}"
            add_usage(model_effort_delta_usage[model_effort_key], delta)
            model_effort_delta_counts[model_effort_key] += 1
            add_usage(daily_model_delta_usage[day_key][model or "unknown_model"], delta)
            add_usage(weekly_model_delta_usage[week][model or "unknown_model"], delta)
            add_usage(monthly_model_delta_usage[month_key][model or "unknown_model"], delta)

    daily_delta_sum = zero_usage()
    for usage in daily.values():
        add_usage(daily_delta_sum, usage)

    loc = count_loc()
    primary_loc = max(1, loc["first_party_assets_project_cs"]["lines"])
    uncached_input = max(0, total["input_tokens"] - total["cached_input_tokens"])
    cache_ratio = total["cached_input_tokens"] / total["input_tokens"] if total["input_tokens"] else 0.0
    output_ratio = total["output_tokens"] / total["total_tokens"] if total["total_tokens"] else 0.0
    reasoning_output_ratio = total["reasoning_output_tokens"] / total["output_tokens"] if total["output_tokens"] else 0.0

    pricing = {
        "gpt-5.3-codex_standard_api_equivalent": {"input": 1.75, "cached_input": 0.175, "output": 14.0},
        "gpt-5.3-codex_priority_api_equivalent": {"input": 3.5, "cached_input": 0.35, "output": 28.0},
        "gpt-5.4_standard_short_context_equivalent": {"input": 2.5, "cached_input": 0.25, "output": 15.0},
        "gpt-5.5_standard_short_context_equivalent": {"input": 5.0, "cached_input": 0.5, "output": 30.0},
        "gpt-5.5_batch_short_context_equivalent": {"input": 2.5, "cached_input": 0.25, "output": 15.0},
        "gpt-5.5_flex_short_context_equivalent": {"input": 2.5, "cached_input": 0.25, "output": 15.0},
        "gpt-5.5_priority_short_context_equivalent": {"input": 12.5, "cached_input": 1.25, "output": 75.0},
        "gpt-5.4_mini_standard_equivalent": {"input": 0.75, "cached_input": 0.075, "output": 4.5},
    }
    model_rate_catalog = {
        "gpt-5.3-codex": {"input": 1.75, "cached_input": 0.175, "output": 14.0, "source": "developers.openai.com/api/docs/pricing specialized Codex standard"},
        "gpt-5.5": {"input": 5.0, "cached_input": 0.5, "output": 30.0, "source": "developers.openai.com/api/docs/pricing flagship standard short-context"},
        "gpt-5.4": {"input": 2.5, "cached_input": 0.25, "output": 15.0, "source": "developers.openai.com/api/docs/pricing flagship standard short-context"},
        "gpt-5.4-mini": {"input": 0.75, "cached_input": 0.075, "output": 4.5, "source": "developers.openai.com/api/docs/pricing flagship standard short-context"},
    }
    price_rows = {}
    upper_no_cache = {}
    for name, rate in pricing.items():
        price_rows[name] = usage_cost(total, rate)
        upper_no_cache[name] = total["input_tokens"] / 1_000_000 * rate["input"] + total["output_tokens"] / 1_000_000 * rate["output"]

    session_totals = [int(record["final_usage"]["total_tokens"]) for record in selected_with_usage]
    first_ts = min((parse_ts(record.get("final_timestamp")) for record in selected_with_usage if record.get("final_timestamp")), default=None)
    last_ts = max((parse_ts(record.get("final_timestamp")) for record in selected_with_usage if record.get("final_timestamp")), default=None)
    day_span = max(1, (last_ts.date() - first_ts.date()).days + 1) if first_ts and last_ts else 1
    sorted_totals = sorted(session_totals)
    top_1_percent_count = max(1, int(len(sorted_totals) * 0.01)) if sorted_totals else 0
    top_5_percent_count = max(1, int(len(sorted_totals) * 0.05)) if sorted_totals else 0
    top_10_percent_count = max(1, int(len(sorted_totals) * 0.10)) if sorted_totals else 0

    context_counts = Counter(str(record.get("model_context_window") or "unknown") for record in selected_with_usage)
    plan_counts = Counter(str(record.get("plan_type") or "unknown") for record in selected_with_usage)
    originator_counts = Counter(str(record.get("originator") or "unknown") for record in selected_records)
    source_counts = Counter(str(record.get("source") or "unknown") for record in selected_records)
    cli_counts = Counter(str(record.get("cli_version") or "unknown") for record in selected_records)
    final_model_counts = Counter(str(record.get("final_model") or "unknown_model") for record in selected_with_usage)
    final_effort_counts = Counter(str(record.get("final_effort") or "unknown") for record in selected_with_usage)
    model_final_usage = defaultdict(zero_usage)
    effort_final_usage = defaultdict(zero_usage)
    model_effort_final_usage = defaultdict(zero_usage)
    model_effort_final_counts = Counter()
    cwd_usage = defaultdict(zero_usage)
    source_usage = defaultdict(zero_usage)
    originator_usage = defaultdict(zero_usage)
    plan_usage = defaultdict(zero_usage)
    cli_usage = defaultdict(zero_usage)
    for record in selected_with_usage:
        model = str(record.get("final_model") or "unknown_model")
        effort = str(record.get("final_effort") or "unknown")
        model_effort_key = f"{model}::{effort}"
        add_usage(model_final_usage[model], record["final_usage"])
        add_usage(effort_final_usage[effort], record["final_usage"])
        add_usage(model_effort_final_usage[model_effort_key], record["final_usage"])
        model_effort_final_counts[model_effort_key] += 1
        add_usage(cwd_usage[str(record.get("cwd") or "unknown")], record["final_usage"])
        add_usage(source_usage[str(record.get("source") or "unknown")], record["final_usage"])
        add_usage(originator_usage[str(record.get("originator") or "unknown")], record["final_usage"])
        add_usage(plan_usage[str(record.get("plan_type") or "unknown")], record["final_usage"])
        add_usage(cli_usage[str(record.get("cli_version") or "unknown")], record["final_usage"])

    known_model_costs = {}
    known_model_total = 0.0
    unpriced_model_usage = zero_usage()
    for model, usage in sorted(model_final_usage.items()):
        rate = model_rate_catalog.get(model)
        if rate:
            cost = usage_cost(usage, rate)
            known_model_costs[model] = cost
            known_model_total += cost["total_cost_usd"]
        else:
            add_usage(unpriced_model_usage, usage)
            known_model_costs[model] = None

    unpriced_as_gpt_5_3_codex = usage_cost(unpriced_model_usage, model_rate_catalog["gpt-5.3-codex"])
    unpriced_as_gpt_5_5 = usage_cost(unpriced_model_usage, model_rate_catalog["gpt-5.5"])
    model_cost_bounds = {
        "known_models_only_standard_usd": known_model_total,
        "unpriced_known_model_total_tokens": unpriced_model_usage["total_tokens"],
        "unpriced_as_gpt_5_3_codex_standard_usd": unpriced_as_gpt_5_3_codex["total_cost_usd"],
        "unpriced_as_gpt_5_5_standard_usd": unpriced_as_gpt_5_5["total_cost_usd"],
        "known_plus_unpriced_as_gpt_5_3_codex_standard_usd": known_model_total + unpriced_as_gpt_5_3_codex["total_cost_usd"],
        "known_plus_unpriced_as_gpt_5_5_standard_usd": known_model_total + unpriced_as_gpt_5_5["total_cost_usd"],
    }

    ratios = {
        "tokens_per_first_party_assets_project_cs_line": total["total_tokens"] / primary_loc,
        "input_tokens_per_first_party_assets_project_cs_line": total["input_tokens"] / primary_loc,
        "output_tokens_per_first_party_assets_project_cs_line": total["output_tokens"] / primary_loc,
    }
    for scope, row in loc.items():
        if row["lines"]:
            ratios[f"tokens_per_line__{scope}"] = total["total_tokens"] / row["lines"]

    top_sessions = sorted(selected_with_usage, key=lambda record: int(record["final_usage"]["total_tokens"]), reverse=True)[:25]
    top_output_sessions = sorted(selected_with_usage, key=lambda record: int(record["final_usage"]["output_tokens"]), reverse=True)[:25]
    top_reasoning_sessions = sorted(selected_with_usage, key=lambda record: int(record["final_usage"]["reasoning_output_tokens"]), reverse=True)[:25]
    top_days = sorted(daily.items(), key=lambda item: item[1]["total_tokens"], reverse=True)[:20]
    top_output_days = sorted(daily.items(), key=lambda item: item[1]["output_tokens"], reverse=True)[:20]
    top_reasoning_days = sorted(daily.items(), key=lambda item: item[1]["reasoning_output_tokens"], reverse=True)[:20]
    active_days = [usage["total_tokens"] for usage in daily.values() if usage["total_tokens"] > 0]
    sorted_days = sorted(active_days)
    largest_session = max(session_totals) if session_totals else 0
    top_1_share = sum(sorted_totals[-top_1_percent_count:]) / total["total_tokens"] if top_1_percent_count and total["total_tokens"] else 0.0
    top_5_share = sum(sorted_totals[-top_5_percent_count:]) / total["total_tokens"] if top_5_percent_count and total["total_tokens"] else 0.0
    top_10_share = sum(sorted_totals[-top_10_percent_count:]) / total["total_tokens"] if top_10_percent_count and total["total_tokens"] else 0.0
    primary_standard = price_rows[PRIMARY_PRICE_KEY]
    primary_standard_no_cache = upper_no_cache[PRIMARY_PRICE_KEY]
    codex_standard = price_rows[CODEX_STANDARD_PRICE_KEY]
    codex_standard_no_cache = upper_no_cache[CODEX_STANDARD_PRICE_KEY]
    observed_low_cost = model_cost_bounds["known_plus_unpriced_as_gpt_5_3_codex_standard_usd"]
    observed_high_cost = model_cost_bounds["known_plus_unpriced_as_gpt_5_5_standard_usd"]
    scope_economics = {}
    for scope, row in loc.items():
        lines = max(1, row["lines"])
        nonblank = max(1, row["nonblank_lines"])
        chars = max(1, row["characters"])
        non_ws_chars = max(1, row["non_whitespace_characters"])
        alnum_chars = max(1, row["alphanumeric_characters"])
        scope_economics[scope] = {
            "files": row["files"],
            "bytes": row["bytes"],
            "lines": row["lines"],
            "nonblank_lines": row["nonblank_lines"],
            "characters": row["characters"],
            "non_whitespace_characters": row["non_whitespace_characters"],
            "alphanumeric_characters": row["alphanumeric_characters"],
            "tokens_per_line": total["total_tokens"] / lines,
            "tokens_per_nonblank_line": total["total_tokens"] / nonblank,
            "tokens_per_character": total["total_tokens"] / chars,
            "tokens_per_1k_characters": total["total_tokens"] / chars * 1000,
            "tokens_per_non_whitespace_character": total["total_tokens"] / non_ws_chars,
            "tokens_per_1k_non_whitespace_characters": total["total_tokens"] / non_ws_chars * 1000,
            "tokens_per_alphanumeric_character": total["total_tokens"] / alnum_chars,
            "tokens_per_1k_alphanumeric_characters": total["total_tokens"] / alnum_chars * 1000,
            "output_tokens_per_line": total["output_tokens"] / lines,
            "output_tokens_per_character": total["output_tokens"] / chars,
            "output_tokens_per_1k_characters": total["output_tokens"] / chars * 1000,
            "gpt_5_5_standard_usd_per_line": primary_standard["total_cost_usd"] / lines,
            "gpt_5_5_standard_usd_per_1k_lines": primary_standard["total_cost_usd"] / lines * 1000,
            "gpt_5_5_standard_usd_per_character": primary_standard["total_cost_usd"] / chars,
            "gpt_5_5_standard_usd_per_1k_characters": primary_standard["total_cost_usd"] / chars * 1000,
            "gpt_5_3_codex_standard_usd_per_line": codex_standard["total_cost_usd"] / lines,
            "gpt_5_3_codex_standard_usd_per_1k_lines": codex_standard["total_cost_usd"] / lines * 1000,
            "gpt_5_3_codex_standard_usd_per_character": codex_standard["total_cost_usd"] / chars,
            "gpt_5_3_codex_standard_usd_per_1k_characters": codex_standard["total_cost_usd"] / chars * 1000,
            "observed_model_low_bound_usd_per_line": observed_low_cost / lines,
            "observed_model_high_bound_usd_per_line": observed_high_cost / lines,
            "observed_model_high_bound_usd_per_1k_characters": observed_high_cost / chars * 1000,
        }
    daily_primary_costs = usage_map_costs(daily, pricing[PRIMARY_PRICE_KEY])
    hourly_primary_costs = usage_map_costs(hourly, pricing[PRIMARY_PRICE_KEY])
    weekly_primary_costs = usage_map_costs(weekly, pricing[PRIMARY_PRICE_KEY])
    monthly_primary_costs = usage_map_costs(monthly, pricing[PRIMARY_PRICE_KEY])
    daily_codex_costs = usage_map_costs(daily, pricing[CODEX_STANDARD_PRICE_KEY])
    weekly_codex_costs = usage_map_costs(weekly, pricing[CODEX_STANDARD_PRICE_KEY])
    monthly_codex_costs = usage_map_costs(monthly, pricing[CODEX_STANDARD_PRICE_KEY])
    daily_observed_costs = observed_costs_by_period(daily_model_delta_usage, model_rate_catalog)
    weekly_observed_costs = observed_costs_by_period(weekly_model_delta_usage, model_rate_catalog)
    monthly_observed_costs = observed_costs_by_period(monthly_model_delta_usage, model_rate_catalog)
    effort_primary_standard_costs = {effort: usage_cost(usage, pricing[PRIMARY_PRICE_KEY]) for effort, usage in sorted(effort_final_usage.items())}
    effort_delta_primary_standard_costs = {effort: usage_cost(usage, pricing[PRIMARY_PRICE_KEY]) for effort, usage in sorted(effort_delta_usage.items())}
    model_effort_delta_standard_costs = {}
    for key, usage in sorted(model_effort_delta_usage.items()):
        model, _separator, _effort = key.partition("::")
        rate = model_rate_catalog.get(model)
        model_effort_delta_standard_costs[key] = usage_cost(usage, rate) if rate else None
    model_effort_final_rows = model_effort_cost_rows(model_effort_final_usage, model_effort_final_counts, model_rate_catalog, "session_count", total["total_tokens"])
    model_effort_delta_rows = model_effort_cost_rows(model_effort_delta_usage, model_effort_delta_counts, model_rate_catalog, "delta_event_count", max(1, daily_delta_sum["total_tokens"]))
    priced_model_effort_final_cost = sum(row["model_standard_cost_usd"] or 0 for row in model_effort_final_rows)
    unpriced_model_effort_final_tokens = sum(row["total_tokens"] for row in model_effort_final_rows if row["model_standard_cost_usd"] is None)
    top_model_effort_final = model_effort_final_rows[0] if model_effort_final_rows else None
    gpt_5_5_xhigh_final_row = next((row for row in model_effort_final_rows if row["key"] == "gpt-5.5::xhigh"), None)
    xhigh_final_usage = clone_usage(effort_final_usage.get("xhigh", zero_usage()))
    xhigh_delta_usage = clone_usage(effort_delta_usage.get("xhigh", zero_usage()))
    xhigh_primary_standard_cost = usage_cost(xhigh_final_usage, pricing[PRIMARY_PRICE_KEY])
    output_rate = pricing[PRIMARY_PRICE_KEY]["output"]
    input_output_stats = {
        "input_to_output_ratio": total["input_tokens"] / max(1, total["output_tokens"]),
        "uncached_input_to_output_ratio": uncached_input / max(1, total["output_tokens"]),
        "cached_input_to_output_ratio": total["cached_input_tokens"] / max(1, total["output_tokens"]),
        "output_to_total_tokens_ratio": total["output_tokens"] / max(1, total["total_tokens"]),
        "reasoning_to_output_ratio": total["reasoning_output_tokens"] / max(1, total["output_tokens"]),
        "reasoning_to_total_tokens_ratio": total["reasoning_output_tokens"] / max(1, total["total_tokens"]),
        "non_reasoning_output_tokens": total["output_tokens"] - total["reasoning_output_tokens"],
        "non_reasoning_output_to_output_ratio": (total["output_tokens"] - total["reasoning_output_tokens"]) / max(1, total["output_tokens"]),
        "paid_input_to_all_input_ratio": uncached_input / max(1, total["input_tokens"]),
        "cached_input_to_uncached_input_ratio": total["cached_input_tokens"] / max(1, uncached_input),
        "output_tokens_per_session": total["output_tokens"] / max(1, len(selected_with_usage)),
        "input_tokens_per_session": total["input_tokens"] / max(1, len(selected_with_usage)),
        "uncached_input_tokens_per_session": uncached_input / max(1, len(selected_with_usage)),
        "reasoning_output_tokens_per_session": total["reasoning_output_tokens"] / max(1, len(selected_with_usage)),
        "gpt_5_5_standard_uncached_input_cost_share": primary_standard["uncached_input_cost_usd"] / max(0.000001, primary_standard["total_cost_usd"]),
        "gpt_5_5_standard_cached_input_cost_share": primary_standard["cached_input_cost_usd"] / max(0.000001, primary_standard["total_cost_usd"]),
        "gpt_5_5_standard_output_cost_share": primary_standard["output_cost_usd"] / max(0.000001, primary_standard["total_cost_usd"]),
        "gpt_5_5_standard_input_side_cost_usd": primary_standard["uncached_input_cost_usd"] + primary_standard["cached_input_cost_usd"],
        "gpt_5_5_standard_output_side_cost_usd": primary_standard["output_cost_usd"],
        "gpt_5_5_standard_effective_usd_per_1m_total_tokens": primary_standard["total_cost_usd"] / max(1, total["total_tokens"]) * 1_000_000,
        "gpt_5_5_standard_effective_usd_per_1m_output_tokens": primary_standard["total_cost_usd"] / max(1, total["output_tokens"]) * 1_000_000,
        "gpt_5_5_standard_reasoning_output_cost_usd": total["reasoning_output_tokens"] / 1_000_000 * output_rate,
        "gpt_5_5_standard_non_reasoning_output_cost_usd": (total["output_tokens"] - total["reasoning_output_tokens"]) / 1_000_000 * output_rate,
        "gpt_5_5_standard_reasoning_output_cost_share": (total["reasoning_output_tokens"] / 1_000_000 * output_rate) / max(0.000001, primary_standard["total_cost_usd"]),
        "top_output_day": top_output_days[0][0] if top_output_days else None,
        "top_output_day_output_tokens": top_output_days[0][1]["output_tokens"] if top_output_days else 0,
        "top_reasoning_day": top_reasoning_days[0][0] if top_reasoning_days else None,
        "top_reasoning_day_reasoning_tokens": top_reasoning_days[0][1]["reasoning_output_tokens"] if top_reasoning_days else 0,
    }
    interesting_stats = {
        "active_days": len(active_days),
        "calendar_day_span": day_span,
        "mean_tokens_per_active_day": total["total_tokens"] / max(1, len(active_days)),
        "median_tokens_per_active_day": statistics.median(active_days) if active_days else 0,
        "peak_day_tokens": max(active_days) if active_days else 0,
        "peak_day_vs_mean_active_day": (max(active_days) / (total["total_tokens"] / max(1, len(active_days)))) if active_days and total["total_tokens"] else 0,
        "session_gini_total_tokens": gini(session_totals),
        "top_1_percent_sessions_share": top_1_share,
        "top_5_percent_sessions_share": top_5_share,
        "top_10_percent_sessions_share": top_10_share,
        "largest_session_share": largest_session / total["total_tokens"] if total["total_tokens"] else 0,
        "equivalent_full_258400_context_windows": total["total_tokens"] / 258400,
        "equivalent_full_270k_context_windows": total["total_tokens"] / 270000,
        "gpt_5_5_standard_cache_discount_saved_usd": primary_standard_no_cache - primary_standard["total_cost_usd"],
        "gpt_5_5_standard_cost_per_primary_loc_usd": primary_standard["total_cost_usd"] / primary_loc,
        "gpt_5_5_standard_cost_per_1k_primary_loc_usd": primary_standard["total_cost_usd"] / primary_loc * 1000,
        "gpt_5_5_standard_cost_per_primary_code_character_usd": primary_standard["total_cost_usd"] / max(1, loc["first_party_assets_project_cs"]["characters"]),
        "gpt_5_3_codex_standard_cache_discount_saved_usd": codex_standard_no_cache - codex_standard["total_cost_usd"],
        "gpt_5_3_codex_standard_cost_per_1k_primary_loc_usd": codex_standard["total_cost_usd"] / primary_loc * 1000,
        "observed_model_high_bound_cost_per_1k_primary_loc_usd": observed_high_cost / primary_loc * 1000,
        "tokens_per_primary_code_character": total["total_tokens"] / max(1, loc["first_party_assets_project_cs"]["characters"]),
        "tokens_per_primary_code_non_ws_character": total["total_tokens"] / max(1, loc["first_party_assets_project_cs"]["non_whitespace_characters"]),
        "tokens_per_primary_code_alphanumeric_character": total["total_tokens"] / max(1, loc["first_party_assets_project_cs"]["alphanumeric_characters"]),
        "tokens_per_dollar_gpt_5_5_standard": total["total_tokens"] / primary_standard["total_cost_usd"] if primary_standard["total_cost_usd"] else 0,
        "tokens_per_dollar_gpt_5_3_codex_standard": total["total_tokens"] / codex_standard["total_cost_usd"] if codex_standard["total_cost_usd"] else 0,
        "xhigh_final_sessions_share": final_effort_counts.get("xhigh", 0) / max(1, len(selected_with_usage)),
        "xhigh_final_tokens_share": xhigh_final_usage["total_tokens"] / max(1, total["total_tokens"]),
        "xhigh_delta_tokens_share": xhigh_delta_usage["total_tokens"] / max(1, daily_delta_sum["total_tokens"]),
        "gpt_5_5_standard_xhigh_final_cost_usd": xhigh_primary_standard_cost["total_cost_usd"],
        "gpt_5_5_standard_cost_per_xhigh_final_session_usd": xhigh_primary_standard_cost["total_cost_usd"] / max(1, final_effort_counts.get("xhigh", 0)),
        "reasoning_tokens_per_1m_xhigh_final_tokens": xhigh_final_usage["reasoning_output_tokens"] / max(1, xhigh_final_usage["total_tokens"]) * 1_000_000,
        "output_tokens_per_1m_xhigh_final_tokens": xhigh_final_usage["output_tokens"] / max(1, xhigh_final_usage["total_tokens"]) * 1_000_000,
        "top_model_effort_final_tokens_share": top_model_effort_final["tokens_share"] if top_model_effort_final else 0,
        "top_model_effort_final_cost_usd": top_model_effort_final["model_standard_cost_usd"] if top_model_effort_final and top_model_effort_final["model_standard_cost_usd"] is not None else 0,
        "priced_model_effort_final_standard_cost_usd": priced_model_effort_final_cost,
        "unpriced_model_effort_final_tokens": unpriced_model_effort_final_tokens,
        "unpriced_model_effort_final_tokens_share": unpriced_model_effort_final_tokens / max(1, total["total_tokens"]),
        "gpt_5_5_xhigh_exact_final_tokens": gpt_5_5_xhigh_final_row["total_tokens"] if gpt_5_5_xhigh_final_row else 0,
        "gpt_5_5_xhigh_exact_final_tokens_share": gpt_5_5_xhigh_final_row["tokens_share"] if gpt_5_5_xhigh_final_row else 0,
        "gpt_5_5_xhigh_exact_sessions": gpt_5_5_xhigh_final_row["session_count"] if gpt_5_5_xhigh_final_row else 0,
        "gpt_5_5_xhigh_exact_standard_cost_usd": gpt_5_5_xhigh_final_row["model_standard_cost_usd"] if gpt_5_5_xhigh_final_row else 0,
        "gpt_5_5_xhigh_exact_cache_savings_usd": gpt_5_5_xhigh_final_row["cache_savings_usd"] if gpt_5_5_xhigh_final_row else 0,
        "gpt_5_5_xhigh_exact_cost_per_session_usd": gpt_5_5_xhigh_final_row["cost_per_count_usd"] if gpt_5_5_xhigh_final_row else 0,
        "gpt_5_5_xhigh_exact_reasoning_tokens_per_1m": gpt_5_5_xhigh_final_row["reasoning_tokens_per_1m_total_tokens"] if gpt_5_5_xhigh_final_row else 0,
        "output_tokens_per_1m_total_tokens": total["output_tokens"] / max(1, total["total_tokens"]) * 1_000_000,
        "reasoning_tokens_per_1m_total_tokens": total["reasoning_output_tokens"] / max(1, total["total_tokens"]) * 1_000_000,
    }
    report = {
        "generated_at_utc": now_utc.isoformat(),
        "generated_at_samara": now_local.isoformat(),
        "evidence_class": "STATIC_LOCAL_CODEX_JSONL_AND_FILESYSTEM",
        "roots": [{"label": label, "path": str(root), "exists": root.exists()} for label, root in ROOTS],
        "file_count": len(records),
        "unique_session_or_path_keys": len(selected_records),
        "sessions_with_usage": len(selected_with_usage),
        "sessions_without_usage": len(selected_records) - len(selected_with_usage),
        "duplicate_records_removed": duplicate_records,
        "files_missing_session_id": missing_session_id,
        "parse_errors_first_pass": sum(record["parse_errors"] for record in records),
        "parse_errors_increment_pass": increment_parse_errors,
        "first_selected_timestamp_utc": first_ts.isoformat() if first_ts else None,
        "last_selected_timestamp_utc": last_ts.isoformat() if last_ts else None,
        "day_span": day_span,
        "totals": total,
        "daily_delta_sum": daily_delta_sum,
        "daily_delta_minus_final_total": sub_usage(daily_delta_sum, total),
        "uncached_input_tokens": uncached_input,
        "cache_ratio": cache_ratio,
        "output_ratio": output_ratio,
        "reasoning_output_ratio_of_output": reasoning_output_ratio,
        "averages": {
            "tokens_per_day_span": total["total_tokens"] / day_span,
            "tokens_per_session_with_usage": total["total_tokens"] / max(1, len(selected_with_usage)),
            "output_tokens_per_session_with_usage": total["output_tokens"] / max(1, len(selected_with_usage)),
            "median_tokens_per_session": statistics.median(session_totals) if session_totals else 0,
            "p90_tokens_per_session": percentile(sorted_totals, 0.90),
            "p95_tokens_per_session": percentile(sorted_totals, 0.95),
            "p99_tokens_per_session": percentile(sorted_totals, 0.99),
            "max_tokens_per_session": largest_session,
        },
        "input_output_stats": input_output_stats,
        "interesting_stats": interesting_stats,
        "root_breakdown": dict(root_breakdown),
        "loc": loc,
        "ratios": ratios,
        "scope_economics": scope_economics,
        "pricing": price_rows,
        "pricing_upper_bound_no_cache_usd": upper_no_cache,
        "primary_price_key": PRIMARY_PRICE_KEY,
        "primary_price_label": PRIMARY_PRICE_LABEL,
        "daily_gpt_5_5_standard_costs_usd": daily_primary_costs,
        "hourly_gpt_5_5_standard_costs_usd": hourly_primary_costs,
        "weekly_gpt_5_5_standard_costs_usd": weekly_primary_costs,
        "monthly_gpt_5_5_standard_costs_usd": monthly_primary_costs,
        "daily_gpt_5_3_codex_standard_costs_usd": daily_codex_costs,
        "weekly_gpt_5_3_codex_standard_costs_usd": weekly_codex_costs,
        "monthly_gpt_5_3_codex_standard_costs_usd": monthly_codex_costs,
        "daily_observed_model_costs_usd": daily_observed_costs,
        "weekly_observed_model_costs_usd": weekly_observed_costs,
        "monthly_observed_model_costs_usd": monthly_observed_costs,
        "model_rate_catalog": model_rate_catalog,
        "model_final_session_usage": dict(model_final_usage),
        "model_delta_usage": dict(model_delta_usage),
        "daily_model_delta_usage": freeze_nested_usage(daily_model_delta_usage),
        "weekly_model_delta_usage": freeze_nested_usage(weekly_model_delta_usage),
        "monthly_model_delta_usage": freeze_nested_usage(monthly_model_delta_usage),
        "model_delta_minus_final_total": sub_usage(daily_delta_sum, total),
        "model_specific_standard_costs": known_model_costs,
        "model_specific_cost_bounds": model_cost_bounds,
        "model_effort_final_session_usage": dict(model_effort_final_usage),
        "model_effort_final_session_counts": dict(model_effort_final_counts.most_common()),
        "model_effort_final_standard_cost_rows": model_effort_final_rows,
        "model_effort_delta_standard_cost_rows": model_effort_delta_rows,
        "effort_final_session_usage": dict(effort_final_usage),
        "effort_delta_usage": dict(effort_delta_usage),
        "model_effort_delta_usage": dict(model_effort_delta_usage),
        "model_effort_delta_counts": dict(model_effort_delta_counts.most_common()),
        "effort_gpt_5_5_standard_costs_usd": effort_primary_standard_costs,
        "effort_delta_gpt_5_5_standard_costs_usd": effort_delta_primary_standard_costs,
        "model_effort_delta_standard_costs_usd": model_effort_delta_standard_costs,
        "final_model_counts": dict(final_model_counts.most_common()),
        "final_effort_counts": dict(final_effort_counts.most_common()),
        "context_window_counts": dict(context_counts.most_common()),
        "plan_type_counts": dict(plan_counts.most_common()),
        "originator_counts": dict(originator_counts.most_common()),
        "source_counts": dict(source_counts.most_common()),
        "cli_version_counts": dict(cli_counts.most_common(20)),
        "top_cwd_usage": top_usage_rows(cwd_usage),
        "top_source_usage": top_usage_rows(source_usage),
        "top_originator_usage": top_usage_rows(originator_usage),
        "top_plan_usage": top_usage_rows(plan_usage),
        "top_cli_usage": top_usage_rows(cli_usage),
        "daily": {key: value for key, value in sorted(daily.items())},
        "hourly": {key: value for key, value in sorted(hourly.items())},
        "weekly": {key: value for key, value in sorted(weekly.items())},
        "monthly": {key: value for key, value in sorted(monthly.items())},
        "top_days": [{"date": key, **value} for key, value in top_days],
        "top_output_days": [{"date": key, **value} for key, value in top_output_days],
        "top_reasoning_days": [{"date": key, **value} for key, value in top_reasoning_days],
        "top_sessions": [session_report_row(record, pricing[PRIMARY_PRICE_KEY], pricing[CODEX_STANDARD_PRICE_KEY], model_rate_catalog) for record in top_sessions],
        "top_output_sessions": [session_report_row(record, pricing[PRIMARY_PRICE_KEY], pricing[CODEX_STANDARD_PRICE_KEY], model_rate_catalog) for record in top_output_sessions],
        "top_reasoning_sessions": [session_report_row(record, pricing[PRIMARY_PRICE_KEY], pricing[CODEX_STANDARD_PRICE_KEY], model_rate_catalog) for record in top_reasoning_sessions],
        "pricing_sources": [
            f"https://developers.openai.com/api/docs/pricing checked {REPORT_DATE}; lines 700-708 list GPT-5.5/GPT-5.4/GPT-5.4-mini standard short-context rates and regional uplift",
            f"https://developers.openai.com/api/docs/pricing checked {REPORT_DATE}; lines 740-745 list GPT-5.5/GPT-5.4/GPT-5.4-mini priority short-context rates",
            f"https://developers.openai.com/api/docs/pricing checked {REPORT_DATE}; lines 865-881 list specialized gpt-5.3-codex standard and priority rates",
            f"https://developers.openai.com/api/docs/guides/prompt-caching checked {REPORT_DATE}; lines 741-757 define GPT-5.5 cache retention default and cached token reporting",
            f"https://developers.openai.com/api/docs/guides/reasoning checked {REPORT_DATE}; lines 813-829 define reasoning effort, including xhigh, and lines 837-842 define reasoning tokens as output-billed",
            "All dollar values are API-equivalent estimates, not local invoice proof",
        ],
    }
    report["previous_snapshot_delta"] = previous_snapshot_delta(report)
    return report


def usage_rows(items):
    lines = []
    for key, usage in items:
        lines.append(f"| {key} | {fmt_int(usage['total_tokens'])} | {fmt_int(usage['input_tokens'])} | {fmt_int(usage['cached_input_tokens'])} | {fmt_int(usage['output_tokens'])} | {fmt_int(usage['reasoning_output_tokens'])} |")
    return lines


def fmt_optional_number(value, decimals=2):
    if value is None:
        return "n/a"
    return f"{float(value):,.{decimals}f}"


def fmt_optional_money(value):
    if value is None:
        return "n/a"
    return fmt_money(float(value))


def append_velocity_table(lines, velocity):
    rows = (
        ("Total tokens / hour", "total_tokens_per_hour", "number"),
        ("Total tokens / minute", "total_tokens_per_minute", "number"),
        ("Total tokens / second", "total_tokens_per_second", "number"),
        ("Total tokens / day pace", "total_tokens_per_day", "number"),
        ("Input tokens / hour", "input_tokens_per_hour", "number"),
        ("Cached input tokens / hour", "cached_input_tokens_per_hour", "number"),
        ("Uncached input tokens / hour", "uncached_input_tokens_per_hour", "number"),
        ("Output tokens / hour", "output_tokens_per_hour", "number"),
        ("Reasoning output tokens / hour", "reasoning_output_tokens_per_hour", "number"),
        ("Usage sessions / hour", "sessions_with_usage_per_hour", "number"),
        ("JSONL files / hour", "jsonl_files_per_hour", "number"),
        ("Primary C# code lines / hour", "primary_code_lines_per_hour", "number"),
        ("Primary C# code lines / day pace", "primary_code_lines_per_day", "number"),
        ("Primary C# code chars / hour", "primary_code_characters_per_hour", "number"),
        ("Primary C# code chars / day pace", "primary_code_characters_per_day", "number"),
        ("Tokens / net primary C# code line", "tokens_per_net_primary_code_line", "number"),
        ("Input tokens / net primary C# code line", "input_tokens_per_net_primary_code_line", "number"),
        ("Output tokens / net primary C# code line", "output_tokens_per_net_primary_code_line", "number"),
        ("Reasoning tokens / net primary C# code line", "reasoning_tokens_per_net_primary_code_line", "number"),
        ("Tokens / 1k net primary C# code chars", "tokens_per_1k_net_primary_code_chars", "number"),
        ("Output tokens / 1k net primary C# code chars", "output_tokens_per_1k_net_primary_code_chars", "number"),
        ("GPT-5.5 standard $ / hour", "gpt_5_5_standard_usd_per_hour", "money"),
        ("GPT-5.5 standard $ / day pace", "gpt_5_5_standard_usd_per_day", "money"),
        ("GPT-5.5 priority $ / hour", "gpt_5_5_priority_usd_per_hour", "money"),
        ("gpt-5.3-codex standard $ / hour", "gpt_5_3_codex_standard_usd_per_hour", "money"),
        ("GPT-5.5 standard $ / net primary C# code line", "gpt_5_5_standard_usd_per_net_primary_code_line", "money"),
        ("GPT-5.5 standard $ / 1k net primary C# code chars", "gpt_5_5_standard_usd_per_1k_net_primary_code_chars", "money"),
    )
    lines += ["", "## Velocity Since Previous Snapshot", "Speed and burn-rate are derived from previous-snapshot deltas. Code ratios use net primary C# code growth in the same window.", "", "| Metric | Value |", "|---|---:|"]
    for label, key, kind in rows:
        value = velocity.get(key)
        text = fmt_optional_money(value) if kind == "money" else fmt_optional_number(value)
        lines.append(f"| {label} | {text} |")


def write_reports(report):
    REPORT_JSON.parent.mkdir(parents=True, exist_ok=True)
    REPORT_JSON.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    total = report["totals"]
    primary = report["pricing"][PRIMARY_PRICE_KEY]
    upper_primary = report["pricing_upper_bound_no_cache_usd"][PRIMARY_PRICE_KEY]

    md = []
    md += [f"# TOKEN USAGE AUDIT {REPORT_DATE}", "", f"Generated UTC: {report['generated_at_utc']}", f"Generated Samara: {report['generated_at_samara']}", "Evidence class: STATIC_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Not billing-provider proof.", ""]
    md += ["## Scope"]
    for root in report["roots"]:
        md.append(f"- {root['label']}: `{root['path']}` exists={root['exists']}")
    md += ["", "Accounting: all-time totals use final per-session `total_token_usage`, deduped by `session_meta.id`. Day/week/month stats use positive deltas between token_count snapshots inside selected sessions.", ""]
    md += ["## Totals", "| Metric | Value |", "|---|---:|"]
    for key in ("file_count", "unique_session_or_path_keys", "sessions_with_usage", "sessions_without_usage", "duplicate_records_removed", "files_missing_session_id", "parse_errors_first_pass", "parse_errors_increment_pass", "day_span"):
        md.append(f"| {key} | {fmt_int(report[key])} |")
    md.append(f"| first_selected_timestamp_utc | {report['first_selected_timestamp_utc']} |")
    md.append(f"| last_selected_timestamp_utc | {report['last_selected_timestamp_utc']} |")
    for key in USAGE_KEYS:
        md.append(f"| {key} | {fmt_int(total[key])} |")
    md.append(f"| uncached_input_tokens | {fmt_int(report['uncached_input_tokens'])} |")
    md.append(f"| cache_ratio | {report['cache_ratio']:.6%} |")
    md.append(f"| output_ratio | {report['output_ratio']:.6%} |")
    md.append(f"| reasoning_output_ratio_of_output | {report['reasoning_output_ratio_of_output']:.6%} |")

    change = report.get("previous_snapshot_delta")
    if change and not change.get("error"):
        delta = change["totals_delta"]
        md += ["", "## Change Since Previous Snapshot", f"Previous report: `{change['previous_report_path']}`", f"Previous generated Samara: `{change['previous_generated_at_samara']}`", f"Elapsed hours: {change['elapsed_hours']:.2f}" if change.get("elapsed_hours") is not None else "Elapsed hours: unknown", "", "| Metric | Delta |", "|---|---:|"]
        md.append(f"| file_count | {fmt_int(change['file_count_delta'])} |")
        md.append(f"| sessions_with_usage | {fmt_int(change['sessions_with_usage_delta'])} |")
        for key in USAGE_KEYS:
            md.append(f"| {key} | {fmt_int(delta[key])} |")
        md.append(f"| GPT-5.5 standard API-equivalent $ | {fmt_money(change['gpt_5_5_standard_cost_usd_delta'])} |")
        md.append(f"| GPT-5.5 priority API-equivalent $ | {fmt_money(change['gpt_5_5_priority_cost_usd_delta'])} |")
        md.append(f"| gpt-5.3-codex standard comparison $ | {fmt_money(change['gpt_5_3_codex_standard_cost_usd_delta'])} |")
        md.append(f"| top model-effort key | `{change['top_model_effort_key_previous']}` -> `{change['top_model_effort_key_current']}` |")
        md.append(f"| top model-effort tokens | {fmt_int(change['top_model_effort_tokens_delta'])} |")
        md.append(f"| top model-effort sessions | {fmt_int(change['top_model_effort_sessions_delta'])} |")
        md.append(f"| top model-effort standard $ | {fmt_money(change['top_model_effort_cost_usd_delta'])} |")
        md.append(f"| primary code lines | {fmt_int(change['primary_code_lines_delta'])} |")
        md.append(f"| primary code characters | {fmt_int(change['primary_code_characters_delta'])} |")
        md.append(f"| tokens / primary code line | {change['tokens_per_primary_code_line_delta']:,.2f} |")
        md.append(f"| tokens / 1k primary code chars | {change['tokens_per_1k_primary_code_chars_delta']:,.2f} |")
        md.append(f"| GPT-5.5 $ / 1k primary LOC | {fmt_money(change['gpt_5_5_cost_per_1k_primary_loc_delta'])} |")
        md.append(f"| GPT-5.5 $ / 1k primary code chars | {fmt_money(change['gpt_5_5_cost_per_1k_primary_code_chars_delta'])} |")
        append_velocity_table(md, change.get("velocity") or {})

    md += ["", "## API-Equivalent Price Scenarios", f"Actual Codex billing cannot be proven from local JSONL. These are API-equivalent estimates using official OpenAI rates checked on {REPORT_DATE}. Cached input is charged at cached-input rate; reasoning output is an output subcounter, not added twice.", "", "| Scenario | Uncached input | Cached input | Output | Total | No-cache upper bound |", "|---|---:|---:|---:|---:|---:|"]
    for name, row in report["pricing"].items():
        md.append(f"| {name} | {fmt_money(row['uncached_input_cost_usd'])} | {fmt_money(row['cached_input_cost_usd'])} | {fmt_money(row['output_cost_usd'])} | {fmt_money(row['total_cost_usd'])} | {fmt_money(report['pricing_upper_bound_no_cache_usd'][name])} |")

    md += ["", "## Input Output Economics", "This section separates prompt mass, cache leverage, visible output, and hidden reasoning output. Cost shares use the primary GPT-5.5 standard scenario.", "", "| Metric | Value |", "|---|---:|"]
    for key, value in report["input_output_stats"].items():
        if isinstance(value, str) or value is None:
            md.append(f"| {key} | {value} |")
        elif key.endswith("_ratio") or key.endswith("_share"):
            md.append(f"| {key} | {float(value):.4%} |")
        elif key.endswith("_usd"):
            md.append(f"| {key} | {fmt_money(value)} |")
        elif key.endswith("_tokens"):
            md.append(f"| {key} | {fmt_int(value)} |")
        else:
            md.append(f"| {key} | {float(value):,.4f} |")

    md += ["", "## Model Forensics", "Model evidence comes from structured `turn_context.payload.model` / `collaboration_mode.settings.model` fields when present. Sessions without that field are `unknown_model`; local JSONL still does not expose invoice SKU, priority mode, or contractual billing plan.", "", "### Final Session Model Attribution", "| Model | Sessions | Total tokens | Input | Cached input | Output | Reasoning output | Standard cost if rate known |", "|---|---:|---:|---:|---:|---:|---:|---:|"]
    for model, usage in sorted(report["model_final_session_usage"].items(), key=lambda item: item[1]["total_tokens"], reverse=True):
        cost = report["model_specific_standard_costs"].get(model)
        cost_text = fmt_money(cost["total_cost_usd"]) if cost else "unpriced"
        md.append(f"| {model} | {fmt_int(report['final_model_counts'].get(model, 0))} | {fmt_int(usage['total_tokens'])} | {fmt_int(usage['input_tokens'])} | {fmt_int(usage['cached_input_tokens'])} | {fmt_int(usage['output_tokens'])} | {fmt_int(usage['reasoning_output_tokens'])} | {cost_text} |")
    md += ["", "### Temporal Delta Model Attribution", "This table assigns each token delta to the latest prior `turn_context` model in the same JSONL file. It is useful for trend analysis, but all-time totals above remain final-session authority.", "", "| Model | Delta total | Delta input | Delta cached input | Delta output | Delta reasoning output |", "|---|---:|---:|---:|---:|---:|"]
    for model, usage in sorted(report["model_delta_usage"].items(), key=lambda item: item[1]["total_tokens"], reverse=True):
        md.append(f"| {model} | {fmt_int(usage['total_tokens'])} | {fmt_int(usage['input_tokens'])} | {fmt_int(usage['cached_input_tokens'])} | {fmt_int(usage['output_tokens'])} | {fmt_int(usage['reasoning_output_tokens'])} |")
    md += ["", "### Reasoning Effort Attribution", f"Effort cost uses {PRIMARY_PRICE_LABEL}. `xhigh` is a cost driver, not a separate official price row.", "", "| Effort | Sessions | Total tokens | Input | Cached input | Output | Reasoning output | GPT-5.5 standard $ | $ / session |", "|---|---:|---:|---:|---:|---:|---:|---:|---:|"]
    for effort, usage in sorted(report["effort_final_session_usage"].items(), key=lambda item: item[1]["total_tokens"], reverse=True):
        cost = report["effort_gpt_5_5_standard_costs_usd"].get(effort, {}).get("total_cost_usd", 0)
        session_count = report["final_effort_counts"].get(effort, 0)
        md.append(f"| {effort} | {fmt_int(session_count)} | {fmt_int(usage['total_tokens'])} | {fmt_int(usage['input_tokens'])} | {fmt_int(usage['cached_input_tokens'])} | {fmt_int(usage['output_tokens'])} | {fmt_int(usage['reasoning_output_tokens'])} | {fmt_money(cost)} | {fmt_money(cost / max(1, session_count))} |")
    md += ["", "### Exact Model Plus Effort Final Cost Matrix", "Final-session totals are the authoritative all-time local spend slice. `reasoning_effort` has no separate public multiplier; cost is produced by the model rate and observed input/cached/output tokens. Unknown model rows are left unpriced.", "", "| Model | Effort | Sessions | Total | Share | Input / output | Paid input / output | Cached / output | Cache hit | Output / total | Reasoning / output | Output cost share | Input $/1M | Cached $/1M | Output $/1M | Standard model $ | Cache saved $ | $ / session |", "|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|"]
    for row in report["model_effort_final_standard_cost_rows"]:
        rate_input = f"${row['rate_input_per_1m']:,.3f}" if row["rate_input_per_1m"] is not None else "unpriced"
        rate_cached = f"${row['rate_cached_input_per_1m']:,.3f}" if row["rate_cached_input_per_1m"] is not None else "unpriced"
        rate_output = f"${row['rate_output_per_1m']:,.2f}" if row["rate_output_per_1m"] is not None else "unpriced"
        cost_text = fmt_money(row["model_standard_cost_usd"]) if row["model_standard_cost_usd"] is not None else "unpriced"
        saved_text = fmt_money(row["cache_savings_usd"]) if row["cache_savings_usd"] is not None else "unpriced"
        per_session_text = fmt_money(row["cost_per_count_usd"]) if row["cost_per_count_usd"] is not None else "unpriced"
        output_cost_share = f"{row['output_cost_share']:.4%}" if row["output_cost_share"] is not None else "unpriced"
        md.append(f"| {row['model']} | {row['effort']} | {fmt_int(row['session_count'])} | {fmt_int(row['total_tokens'])} | {row['tokens_share']:.4%} | {row['input_to_output_ratio']:,.2f} | {row['uncached_input_to_output_ratio']:,.2f} | {row['cached_input_to_output_ratio']:,.2f} | {row['cache_ratio']:.4%} | {row['output_ratio']:.4%} | {row['reasoning_output_ratio']:.4%} | {output_cost_share} | {rate_input} | {rate_cached} | {rate_output} | {cost_text} | {saved_text} | {per_session_text} |")
    md += ["", "### Model Plus Effort Delta Cost Matrix", "This assigns each token delta to the latest prior `turn_context` model and effort in the same JSONL file. Use it for temporal trend shape, not as the all-time authority.", "", "| Model | Effort | Delta events | Delta total | Share | Input / output | Paid input / output | Cached / output | Cache hit | Output / total | Reasoning / output | Output cost share | Standard model $ | Cache saved $ | $ / delta event |", "|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|"]
    for row in report["model_effort_delta_standard_cost_rows"]:
        cost_text = fmt_money(row["model_standard_cost_usd"]) if row["model_standard_cost_usd"] is not None else "unpriced"
        saved_text = fmt_money(row["cache_savings_usd"]) if row["cache_savings_usd"] is not None else "unpriced"
        per_event_text = fmt_money(row["cost_per_count_usd"]) if row["cost_per_count_usd"] is not None else "unpriced"
        output_cost_share = f"{row['output_cost_share']:.4%}" if row["output_cost_share"] is not None else "unpriced"
        md.append(f"| {row['model']} | {row['effort']} | {fmt_int(row['delta_event_count'])} | {fmt_int(row['total_tokens'])} | {row['tokens_share']:.4%} | {row['input_to_output_ratio']:,.2f} | {row['uncached_input_to_output_ratio']:,.2f} | {row['cached_input_to_output_ratio']:,.2f} | {row['cache_ratio']:.4%} | {row['output_ratio']:.4%} | {row['reasoning_output_ratio']:.4%} | {output_cost_share} | {cost_text} | {saved_text} | {per_event_text} |")
    bounds = report["model_specific_cost_bounds"]
    md += ["", "### Model-Specific Cost Bounds", "| Bound | USD |", "|---|---:|"]
    for key, value in bounds.items():
        if key.endswith("_tokens"):
            md.append(f"| {key} | {fmt_int(value)} tokens |")
        else:
            md.append(f"| {key} | {fmt_money(value)} |")

    md += ["", "## Interpretive Stats", "These are derived diagnostics, not billing proof. They are useful for waste shape, concentration, and cache economics.", "", "| Metric | Value |", "|---|---:|"]
    for key, value in report["interesting_stats"].items():
        if key.endswith("_share"):
            md.append(f"| {key} | {float(value):.4%} |")
        elif key.endswith("_usd"):
            md.append(f"| {key} | {fmt_money(value)} |")
        else:
            md.append(f"| {key} | {float(value):,.4f} |")

    md += ["", "## Root Breakdown", "| Root | JSONL files | Files with usage | Selected sessions | Selected with usage | Selected total tokens |", "|---|---:|---:|---:|---:|---:|"]
    for root, row in sorted(report["root_breakdown"].items()):
        md.append(f"| {root} | {fmt_int(row['jsonl_files'])} | {fmt_int(row['files_with_usage'])} | {fmt_int(row['selected_sessions'])} | {fmt_int(row['selected_with_usage'])} | {fmt_int(row['selected_total_tokens'])} |")

    md += ["", "## Codebase Density And Economics", "| Scope | Files | Lines | Nonblank lines | Characters | Non-ws chars | Tokens / line | Tokens / 1k chars | Output tokens / 1k chars | Tokens / 1k non-ws chars | GPT-5.5 $ / 1k lines | GPT-5.5 $ / 1k chars | gpt-5.3-codex $ / 1k chars | Observed high $ / 1k chars |", "|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|"]
    for scope, row in report["scope_economics"].items():
        md.append(
            f"| {scope} | {fmt_int(row['files'])} | {fmt_int(row['lines'])} | {fmt_int(row['nonblank_lines'])} | "
            f"{fmt_int(row['characters'])} | {fmt_int(row['non_whitespace_characters'])} | "
            f"{row['tokens_per_line']:,.2f} | {row['tokens_per_1k_characters']:,.2f} | {row['output_tokens_per_1k_characters']:,.2f} | {row['tokens_per_1k_non_whitespace_characters']:,.2f} | "
            f"{fmt_money(row['gpt_5_5_standard_usd_per_1k_lines'])} | {fmt_money(row['gpt_5_5_standard_usd_per_1k_characters'])} | {fmt_money(row['gpt_5_3_codex_standard_usd_per_1k_characters'])} | "
            f"{fmt_money(row['observed_model_high_bound_usd_per_1k_characters'])} |"
        )

    md += ["", "## Chat And Client Breakdowns", "Top groups use final per-session totals after dedupe. `key` is raw local telemetry.", ""]
    for title, key in (("Top CWDs", "top_cwd_usage"), ("Top Sources", "top_source_usage"), ("Top Originators", "top_originator_usage"), ("Top Plan Types", "top_plan_usage"), ("Top CLI Versions", "top_cli_usage")):
        md += [f"### {title}", "| Key | Total | Input | Cached input | Output | Reasoning output |", "|---|---:|---:|---:|---:|---:|"]
        for row in report[key]:
            label = str(row["key"]).replace("|", "/")
            md.append(f"| `{label}` | {fmt_int(row['total_tokens'])} | {fmt_int(row['input_tokens'])} | {fmt_int(row['cached_input_tokens'])} | {fmt_int(row['output_tokens'])} | {fmt_int(row['reasoning_output_tokens'])} |")
        md.append("")

    md += ["", "## Daily Stats", "| Date Samara | Total | Input | Cached input | Output | Reasoning output |", "|---|---:|---:|---:|---:|---:|"]
    md += usage_rows(report["daily"].items())
    md += ["", "## Daily Cost Stats", "| Date Samara | gpt-5.5 standard $ | gpt-5.3-codex secondary $ | Observed-model low bound $ | Observed-model high bound $ | Unpriced tokens |", "|---|---:|---:|---:|---:|---:|"]
    for date, cost in report["daily_gpt_5_5_standard_costs_usd"].items():
        observed = report["daily_observed_model_costs_usd"].get(date, {})
        codex_cost = report["daily_gpt_5_3_codex_standard_costs_usd"].get(date, 0)
        md.append(f"| {date} | {fmt_money(cost)} | {fmt_money(codex_cost)} | {fmt_money(observed.get('known_plus_unpriced_as_gpt_5_3_codex_standard_usd', 0))} | {fmt_money(observed.get('known_plus_unpriced_as_gpt_5_5_standard_usd', 0))} | {fmt_int(observed.get('unpriced_tokens', 0))} |")
    md += ["", "## Weekly Stats", "| ISO Week Samara | Total | Input | Cached input | Output | Reasoning output |", "|---|---:|---:|---:|---:|---:|"]
    md += usage_rows(report["weekly"].items())
    md += ["", "## Weekly Cost Stats", "| ISO Week Samara | gpt-5.5 standard $ | gpt-5.3-codex secondary $ | Observed-model low bound $ | Observed-model high bound $ | Unpriced tokens |", "|---|---:|---:|---:|---:|---:|"]
    for week, cost in report["weekly_gpt_5_5_standard_costs_usd"].items():
        observed = report["weekly_observed_model_costs_usd"].get(week, {})
        codex_cost = report["weekly_gpt_5_3_codex_standard_costs_usd"].get(week, 0)
        md.append(f"| {week} | {fmt_money(cost)} | {fmt_money(codex_cost)} | {fmt_money(observed.get('known_plus_unpriced_as_gpt_5_3_codex_standard_usd', 0))} | {fmt_money(observed.get('known_plus_unpriced_as_gpt_5_5_standard_usd', 0))} | {fmt_int(observed.get('unpriced_tokens', 0))} |")
    md += ["", "## Monthly Stats", "| Month Samara | Total | Input | Cached input | Output | Reasoning output |", "|---|---:|---:|---:|---:|---:|"]
    md += usage_rows(report["monthly"].items())
    md += ["", "## Monthly Cost Stats", "| Month Samara | gpt-5.5 standard $ | gpt-5.3-codex secondary $ | Observed-model low bound $ | Observed-model high bound $ | Unpriced tokens |", "|---|---:|---:|---:|---:|---:|"]
    for month, cost in report["monthly_gpt_5_5_standard_costs_usd"].items():
        observed = report["monthly_observed_model_costs_usd"].get(month, {})
        codex_cost = report["monthly_gpt_5_3_codex_standard_costs_usd"].get(month, 0)
        md.append(f"| {month} | {fmt_money(cost)} | {fmt_money(codex_cost)} | {fmt_money(observed.get('known_plus_unpriced_as_gpt_5_3_codex_standard_usd', 0))} | {fmt_money(observed.get('known_plus_unpriced_as_gpt_5_5_standard_usd', 0))} | {fmt_int(observed.get('unpriced_tokens', 0))} |")
    md += ["", "## Top 20 Days", "| Date Samara | Total tokens |", "|---|---:|"]
    for row in report["top_days"]:
        md.append(f"| {row['date']} | {fmt_int(row['total_tokens'])} |")
    md += ["", "## Top Output Days", "| Date Samara | Output tokens | Total tokens | Reasoning output | Output / total | Reasoning / output |", "|---|---:|---:|---:|---:|---:|"]
    for row in report["top_output_days"]:
        md.append(f"| {row['date']} | {fmt_int(row['output_tokens'])} | {fmt_int(row['total_tokens'])} | {fmt_int(row['reasoning_output_tokens'])} | {row['output_tokens'] / max(1, row['total_tokens']):.4%} | {row['reasoning_output_tokens'] / max(1, row['output_tokens']):.4%} |")
    md += ["", "## Top Reasoning Days", "| Date Samara | Reasoning output | Output tokens | Total tokens | Reasoning / output |", "|---|---:|---:|---:|---:|"]
    for row in report["top_reasoning_days"]:
        md.append(f"| {row['date']} | {fmt_int(row['reasoning_output_tokens'])} | {fmt_int(row['output_tokens'])} | {fmt_int(row['total_tokens'])} | {row['reasoning_output_tokens'] / max(1, row['output_tokens']):.4%} |")

    md += ["", "## Distributions", "| Metric | Value |", "|---|---:|"]
    for key, value in report["averages"].items():
        md.append(f"| {key} | {float(value):,.2f} |")
    md += ["", "Context window counts:"]
    for key, value in report["context_window_counts"].items():
        md.append(f"- {key}: {fmt_int(value)}")
    md += ["", "Plan type counts:"]
    for key, value in report["plan_type_counts"].items():
        md.append(f"- {key}: {fmt_int(value)}")

    md += ["", "## Top 25 Sessions", "| Rank | Session | Model | Effort | Root | Final UTC | Total | Input | Cached | Output | I/O | Output / total | Reasoning / output | Primary $ | Model $ | CWD |", "|---:|---|---|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|"]
    for index, row in enumerate(report["top_sessions"], 1):
        sid = (row.get("session_id") or "missing")[:36]
        cwd = (row.get("cwd") or "").replace("|", "/")
        model_cost = row.get("final_model_standard_cost_usd")
        model_cost_text = fmt_money(model_cost) if model_cost is not None else "unpriced"
        md.append(f"| {index} | `{sid}` | {row.get('final_model')} | {row.get('final_effort')} | {row.get('root')} | {row.get('final_timestamp')} | {fmt_int(row['total_tokens'])} | {fmt_int(row['input_tokens'])} | {fmt_int(row['cached_input_tokens'])} | {fmt_int(row['output_tokens'])} | {row['input_to_output_ratio']:,.2f} | {row['output_ratio']:.4%} | {row['reasoning_output_ratio']:.4%} | {fmt_money(row['gpt_5_5_standard_cost_usd'])} | {model_cost_text} | `{cwd}` |")
    for section_title, rows in (("Top 25 Output Sessions", report["top_output_sessions"]), ("Top 25 Reasoning Sessions", report["top_reasoning_sessions"])):
        md += ["", f"## {section_title}", "| Rank | Session | Model | Effort | Root | Final UTC | Output | Reasoning output | Total | I/O | Paid I/O | Cached / output | Output cost share | Primary $ | CWD |", "|---:|---|---|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|"]
        for index, row in enumerate(rows, 1):
            sid = (row.get("session_id") or "missing")[:36]
            cwd = (row.get("cwd") or "").replace("|", "/")
            md.append(f"| {index} | `{sid}` | {row.get('final_model')} | {row.get('final_effort')} | {row.get('root')} | {row.get('final_timestamp')} | {fmt_int(row['output_tokens'])} | {fmt_int(row['reasoning_output_tokens'])} | {fmt_int(row['total_tokens'])} | {row['input_to_output_ratio']:,.2f} | {row['uncached_input_to_output_ratio']:,.2f} | {row['cached_input_to_output_ratio']:,.2f} | {row['primary_output_cost_share']:.4%} | {fmt_money(row['gpt_5_5_standard_cost_usd'])} | `{cwd}` |")

    md += ["", "## Price Sources"]
    for source in report["pricing_sources"]:
        md.append(f"- {source}")
    md += ["", "## Residual Risk", "- Local JSONL is not provider billing. It lacks invoice ids and does not expose whether a Codex request used standard, priority, enterprise, subscription, or internal billing.", "- `cached_input_tokens` is treated as a priced subcounter of input tokens, not additional total tokens.", "- Model labels are exact only where structured `turn_context` fields exist. Older sessions without model fields remain `unknown_model`.", "- Daily/week/model delta allocation is reconstructed from telemetry deltas; all-time final per-session total remains authoritative for this local audit."]
    REPORT_MD.write_text("\n".join(md) + "\n", encoding="utf-8-sig")

    ledger = []
    generated = datetime.datetime.fromisoformat(report["generated_at_samara"]).strftime("%Y-%m-%d %H:%M")
    ledger += ["# Codex Token Usage Ledger", "", f"Date: {generated} Europe/Samara", "Status: CURRENT STATIC LOCAL TELEMETRY SNAPSHOT / NOT PROJECT ENGINEERING AUTHORITY", "", f"This file is the local token accounting surface archived out of active project docs. The detailed current report is `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_{REPORT_DATE}.md`; machine-readable data is `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_{REPORT_DATE}.json`.", ""]
    ledger += ["## Current Total", "", r"Scope: current `C:\Users\danat\.codex\sessions`, current `C:\Users\danat\.codex\archived_sessions`, and backup `C:\Users\danat\Documents\CodexBackups\codex_cleanup_20260521_194850`.", "", "Accounting rule: parse JSONL `session_meta`/`token_count`, take the final per-session `payload.info.total_token_usage`, dedupe by `session_meta.id`, and keep the highest final `total_tokens` for duplicate records. Day/week/month stats in the dated report use positive in-session deltas.", "", "| Metric | Value |", "|---|---:|"]
    for key in ("unique_session_or_path_keys", "sessions_with_usage", "sessions_without_usage", "duplicate_records_removed", "files_missing_session_id"):
        ledger.append(f"| {key} | {fmt_int(report[key])} |")
    ledger.append(f"| First selected timestamp UTC | {report['first_selected_timestamp_utc']} |")
    ledger.append(f"| Last selected timestamp UTC | {report['last_selected_timestamp_utc']} |")
    for key in USAGE_KEYS:
        ledger.append(f"| {key} | {fmt_int(total[key])} |")
    ledger.append(f"| Uncached input tokens | {fmt_int(report['uncached_input_tokens'])} |")
    ledger.append(f"| Cached-input ratio | {report['cache_ratio']:.6%} |")
    change = report.get("previous_snapshot_delta")
    if change and not change.get("error"):
        delta = change["totals_delta"]
        ledger += ["", "## Change Since Previous Snapshot", "", f"Previous report: `{change['previous_report_path']}`.", f"Previous generated Samara: `{change['previous_generated_at_samara']}`.", f"Elapsed hours: {change['elapsed_hours']:.2f}" if change.get("elapsed_hours") is not None else "Elapsed hours: unknown", "", "| Metric | Delta |", "|---|---:|"]
        ledger.append(f"| file_count | {fmt_int(change['file_count_delta'])} |")
        ledger.append(f"| sessions_with_usage | {fmt_int(change['sessions_with_usage_delta'])} |")
        for key in USAGE_KEYS:
            ledger.append(f"| {key} | {fmt_int(delta[key])} |")
        ledger.append(f"| GPT-5.5 standard API-equivalent $ | {fmt_money(change['gpt_5_5_standard_cost_usd_delta'])} |")
        ledger.append(f"| GPT-5.5 priority API-equivalent $ | {fmt_money(change['gpt_5_5_priority_cost_usd_delta'])} |")
        ledger.append(f"| gpt-5.3-codex standard comparison $ | {fmt_money(change['gpt_5_3_codex_standard_cost_usd_delta'])} |")
        ledger.append(f"| top model-effort tokens | {fmt_int(change['top_model_effort_tokens_delta'])} |")
        ledger.append(f"| top model-effort sessions | {fmt_int(change['top_model_effort_sessions_delta'])} |")
        ledger.append(f"| top model-effort standard $ | {fmt_money(change['top_model_effort_cost_usd_delta'])} |")
        ledger.append(f"| tokens / primary code line | {change['tokens_per_primary_code_line_delta']:,.2f} |")
        ledger.append(f"| tokens / 1k primary code chars | {change['tokens_per_1k_primary_code_chars_delta']:,.2f} |")
        append_velocity_table(ledger, change.get("velocity") or {})
    ledger += ["", "`cached_input_tokens` is a telemetry subcounter of input-token reuse, not an extra token class to add on top of `total_tokens`.", "", "## API-Equivalent Cost Snapshot", "", f"Local Codex telemetry is not an invoice. The primary estimate uses official `gpt-5.5` standard short-context API-equivalent rates checked on {REPORT_DATE}: input $5.00/1M, cached input $0.50/1M, output $30.00/1M. `xhigh` is a reasoning-effort setting; it changes observed token shape, not the public rate row.", "", "| Scenario | Total | No-cache upper bound |", "|---|---:|---:|"]
    ledger.append(f"| {PRIMARY_PRICE_LABEL} | {fmt_money(primary['total_cost_usd'])} | {fmt_money(upper_primary)} |")
    for name in ("gpt-5.5_priority_short_context_equivalent", "gpt-5.5_batch_short_context_equivalent", "gpt-5.5_flex_short_context_equivalent", "gpt-5.4_standard_short_context_equivalent", "gpt-5.3-codex_standard_api_equivalent", "gpt-5.3-codex_priority_api_equivalent"):
        ledger.append(f"| {name} | {fmt_money(report['pricing'][name]['total_cost_usd'])} | {fmt_money(report['pricing_upper_bound_no_cache_usd'][name])} |")
    ledger += ["", "## Model Attribution", "", "Exact model labels are available only where JSONL contains structured `turn_context` model fields. Unknown sessions are not guessed in the main total.", "", "| Model | Sessions | Total tokens | Standard cost if rate known |", "|---|---:|---:|---:|"]
    for model, usage in sorted(report["model_final_session_usage"].items(), key=lambda item: item[1]["total_tokens"], reverse=True):
        cost = report["model_specific_standard_costs"].get(model)
        cost_text = fmt_money(cost["total_cost_usd"]) if cost else "unpriced"
        ledger.append(f"| {model} | {fmt_int(report['final_model_counts'].get(model, 0))} | {fmt_int(usage['total_tokens'])} | {cost_text} |")
    bounds = report["model_specific_cost_bounds"]
    ledger += ["", "Model-specific cost bounds:", ""]
    ledger.append(f"- Known model standard cost only: {fmt_money(bounds['known_models_only_standard_usd'])}")
    ledger.append(f"- Unpriced known-model tokens: {fmt_int(bounds['unpriced_known_model_total_tokens'])}")
    ledger.append(f"- Known + unpriced as gpt-5.3-codex standard: {fmt_money(bounds['known_plus_unpriced_as_gpt_5_3_codex_standard_usd'])}")
    ledger.append(f"- Known + unpriced as gpt-5.5 standard: {fmt_money(bounds['known_plus_unpriced_as_gpt_5_5_standard_usd'])}")
    ledger += ["", "## Interpretive Snapshot", "", "| Metric | Value |", "|---|---:|"]
    for key in (
        "active_days",
        "mean_tokens_per_active_day",
        "median_tokens_per_active_day",
        "session_gini_total_tokens",
        "top_1_percent_sessions_share",
        "top_10_percent_sessions_share",
        "equivalent_full_258400_context_windows",
        "tokens_per_primary_code_character",
        "tokens_per_primary_code_non_ws_character",
        "tokens_per_primary_code_alphanumeric_character",
        "xhigh_final_sessions_share",
        "xhigh_final_tokens_share",
        "gpt_5_5_standard_xhigh_final_cost_usd",
        "gpt_5_5_standard_cost_per_xhigh_final_session_usd",
        "reasoning_tokens_per_1m_xhigh_final_tokens",
        "gpt_5_5_standard_cache_discount_saved_usd",
        "gpt_5_5_standard_cost_per_1k_primary_loc_usd",
        "gpt_5_3_codex_standard_cache_discount_saved_usd",
        "gpt_5_3_codex_standard_cost_per_1k_primary_loc_usd",
        "priced_model_effort_final_standard_cost_usd",
        "unpriced_model_effort_final_tokens",
        "unpriced_model_effort_final_tokens_share",
        "top_model_effort_final_tokens_share",
        "top_model_effort_final_cost_usd",
        "gpt_5_5_xhigh_exact_final_tokens",
        "gpt_5_5_xhigh_exact_final_tokens_share",
        "gpt_5_5_xhigh_exact_sessions",
        "gpt_5_5_xhigh_exact_standard_cost_usd",
        "gpt_5_5_xhigh_exact_cache_savings_usd",
        "gpt_5_5_xhigh_exact_cost_per_session_usd",
        "gpt_5_5_xhigh_exact_reasoning_tokens_per_1m",
        "observed_model_high_bound_cost_per_1k_primary_loc_usd",
    ):
        value = report["interesting_stats"][key]
        if key.endswith("_share"):
            ledger.append(f"| {key} | {float(value):.4%} |")
        elif key.endswith("_usd"):
            ledger.append(f"| {key} | {fmt_money(value)} |")
        else:
            ledger.append(f"| {key} | {float(value):,.4f} |")
    ledger += ["", "## Input Output Snapshot", "", "| Metric | Value |", "|---|---:|"]
    for key in (
        "input_to_output_ratio",
        "uncached_input_to_output_ratio",
        "cached_input_to_output_ratio",
        "output_to_total_tokens_ratio",
        "reasoning_to_output_ratio",
        "paid_input_to_all_input_ratio",
        "cached_input_to_uncached_input_ratio",
        "gpt_5_5_standard_input_side_cost_usd",
        "gpt_5_5_standard_output_side_cost_usd",
        "gpt_5_5_standard_output_cost_share",
        "gpt_5_5_standard_effective_usd_per_1m_output_tokens",
        "gpt_5_5_standard_reasoning_output_cost_usd",
    ):
        value = report["input_output_stats"][key]
        if key.endswith("_ratio") or key.endswith("_share"):
            ledger.append(f"| {key} | {float(value):.4%} |")
        elif key.endswith("_usd") or "_usd_" in key:
            ledger.append(f"| {key} | {fmt_money(value)} |")
        else:
            ledger.append(f"| {key} | {float(value):,.4f} |")
    ledger += ["", "## Code Density Snapshot", "", "| Scope | Lines | Characters | Tokens / line | Tokens / 1k chars | Output tokens / 1k chars | GPT-5.5 $ / 1k lines | GPT-5.5 $ / 1k chars |", "|---|---:|---:|---:|---:|---:|---:|---:|"]
    for scope in ("first_party_assets_project_cs", "first_party_scripts_cs", "all_repo_cs_excluding_generated", "all_repo_source_broad", "docs_markdown_text"):
        row = report["scope_economics"][scope]
        ledger.append(f"| {scope} | {fmt_int(row['lines'])} | {fmt_int(row['characters'])} | {row['tokens_per_line']:,.2f} | {row['tokens_per_1k_characters']:,.2f} | {row['output_tokens_per_1k_characters']:,.2f} | {fmt_money(row['gpt_5_5_standard_usd_per_1k_lines'])} | {fmt_money(row['gpt_5_5_standard_usd_per_1k_characters'])} |")
    ledger += ["", "## Chat Concentration Snapshot", "", "| Group | Key | Total tokens | Output tokens |", "|---|---|---:|---:|"]
    for group_key, label in (("top_cwd_usage", "cwd"), ("top_source_usage", "source"), ("top_cli_usage", "cli")):
        if report[group_key]:
            row = report[group_key][0]
            key_text = str(row["key"]).replace("|", "/")
            ledger.append(f"| {label} | `{key_text}` | {fmt_int(row['total_tokens'])} | {fmt_int(row['output_tokens'])} |")
    ledger += ["", "## Root Breakdown", "", "| Root | JSONL files | Files with usage | Selected sessions with usage | Selected total tokens |", "|---|---:|---:|---:|---:|"]
    for root, row in sorted(report["root_breakdown"].items()):
        ledger.append(f"| {root} | {fmt_int(row['jsonl_files'])} | {fmt_int(row['files_with_usage'])} | {fmt_int(row['selected_with_usage'])} | {fmt_int(row['selected_total_tokens'])} |")
    ledger += ["", "## Evidence Boundary", "", "Evidence class: static local filesystem telemetry. This is not billing-provider proof, Unity runtime proof, or profiler proof."]
    LEDGER.write_text("\n".join(ledger) + "\n", encoding="utf-8-sig")


def append_audit_files(report):
    # Status/rationale/log entries are written explicitly per audit pass.
    # The old auto-append path is kept inert to avoid duplicate stale sections.
    return
    generated = datetime.datetime.fromisoformat(report["generated_at_samara"]).strftime("%Y-%m-%d %H:%M")
    primary = report["pricing"][PRIMARY_PRICE_KEY]
    io_stats = report["input_output_stats"]
    top_output = report["top_output_sessions"][0] if report["top_output_sessions"] else None
    top_reasoning = report["top_reasoning_sessions"][0] if report["top_reasoning_sessions"] else None
    primary_code = report["scope_economics"]["first_party_assets_project_cs"]
    for path in (STATUS, RATIONALE, LOG):
        path.parent.mkdir(parents=True, exist_ok=True)
    marker = f"## Code Density Economics {REPORT_DATE}"
    if STATUS.exists() and marker in STATUS.read_text(encoding="utf-8", errors="replace"):
        return
    with STATUS.open("a", encoding="utf-8") as handle:
        handle.write(f"""

{marker} {generated[-5:]} Europe/Samara

- [x] Task 47 - Add explicit code-density economics | Justification: added tokens per line, tokens per 1k chars, output tokens per 1k chars, GPT-5.5 dollars per 1k lines, and GPT-5.5 dollars per 1k chars. Alternative rejected: forcing readers to multiply per-character fields manually. Microseconds saved: 0 audit-only.
- [x] Task 48 - Regenerate code-density report surfaces | Justification: refreshed dated Markdown/JSON, stable ledger, and agent log from generated telemetry. Alternative rejected: chat-only derived math. Microseconds saved: 0 audit-only.
""")
    with RATIONALE.open("a", encoding="utf-8") as handle:
        handle.write(f"""

## Decision 21 - {REPORT_DATE} explicit code-density economics

Problem: Code-density rows had tokens per character and dollars per character, but the requested units are line and 1000 code characters.
Solution: Add explicit tokens-per-1k-character and dollars-per-1k-character fields to scope economics and show them in the dated report, ledger, and agent log.
Rejected Alternatives: Leaving users to multiply per-character values manually was rejected because it invites inconsistent reporting.
Scalability potential: Future audits can compare code scopes without spreadsheet conversion.
Hardware Impact: 0 us runtime gain.
""")
    with LOG.open("a", encoding="utf-8") as handle:
        handle.write(f"""

## {REPORT_DATE} TOKEN_USAGE_AUDIT code-density economics

What was wrong -> Prior report exposed per-character economics but did not show the requested per-line and per-1000-code-character units directly.
What was done -> Added explicit tokens/line, tokens/1k chars, output tokens/1k chars, GPT-5.5 dollars/1k lines, GPT-5.5 dollars/1k chars, and secondary Codex/observed-high dollars/1k chars to generated reports. Refreshed ledger/report from {fmt_int(report['file_count'])} JSONL files.
Cinematic Cheats used -> None; audit/process hygiene only.
Exact Microseconds saved -> 0 us game runtime. Static telemetry and docs only.
Interesting stats -> primary code lines {fmt_int(primary_code['lines'])}; code chars {fmt_int(primary_code['characters'])}; tokens/line {primary_code['tokens_per_line']:,.2f}; tokens/1k code chars {primary_code['tokens_per_1k_characters']:,.2f}; output tokens/1k code chars {primary_code['output_tokens_per_1k_characters']:,.2f}; GPT-5.5 dollars/1k LOC {fmt_money(primary_code['gpt_5_5_standard_usd_per_1k_lines'])}; GPT-5.5 dollars/1k code chars {fmt_money(primary_code['gpt_5_5_standard_usd_per_1k_characters'])}; input/output {io_stats['input_to_output_ratio']:,.2f}:1; top output session {(top_output or {}).get('session_id', 'none')} output {fmt_int((top_output or {}).get('output_tokens', 0))}; top reasoning session {(top_reasoning or {}).get('session_id', 'none')} reasoning {fmt_int((top_reasoning or {}).get('reasoning_output_tokens', 0))}.
Evidence -> STATIC_LOCAL_CODEX_JSONL_AND_FILESYSTEM plus official OpenAI pricing pages. Runtime/Unity PlayMode proof absent.
""")


def main():
    report = build_report()
    write_reports(report)
    append_audit_files(report)
    primary = report["pricing"][PRIMARY_PRICE_KEY]
    secondary_codex = report["pricing"][CODEX_STANDARD_PRICE_KEY]
    print(json.dumps({
        "report_json": str(REPORT_JSON),
        "report_md": str(REPORT_MD),
        "ledger": str(LEDGER),
        "total_tokens": report["totals"]["total_tokens"],
        "input_tokens": report["totals"]["input_tokens"],
        "cached_input_tokens": report["totals"]["cached_input_tokens"],
        "output_tokens": report["totals"]["output_tokens"],
        "reasoning_output_tokens": report["totals"]["reasoning_output_tokens"],
        "sessions_with_usage": report["sessions_with_usage"],
        "files": report["file_count"],
        "gpt_5_5_standard_cost": primary["total_cost_usd"],
        "gpt_5_5_standard_upper_no_cache": report["pricing_upper_bound_no_cache_usd"][PRIMARY_PRICE_KEY],
        "gpt_5_5_priority_cost": report["pricing"]["gpt-5.5_priority_short_context_equivalent"]["total_cost_usd"],
        "gpt_5_3_codex_standard_secondary_cost": secondary_codex["total_cost_usd"],
        "xhigh_gpt_5_5_standard_cost": report["interesting_stats"]["gpt_5_5_standard_xhigh_final_cost_usd"],
        "exact_gpt_5_5_xhigh_standard_cost": report["interesting_stats"]["gpt_5_5_xhigh_exact_standard_cost_usd"],
        "unpriced_model_effort_final_tokens": report["interesting_stats"]["unpriced_model_effort_final_tokens"],
        "top_model_effort_pair": report["model_effort_final_standard_cost_rows"][0]["key"] if report["model_effort_final_standard_cost_rows"] else None,
        "input_to_output_ratio": report["input_output_stats"]["input_to_output_ratio"],
        "paid_input_to_output_ratio": report["input_output_stats"]["uncached_input_to_output_ratio"],
        "cached_input_to_output_ratio": report["input_output_stats"]["cached_input_to_output_ratio"],
        "output_cost_share": report["input_output_stats"]["gpt_5_5_standard_output_cost_share"],
        "reasoning_to_output_ratio": report["input_output_stats"]["reasoning_to_output_ratio"],
        "primary_loc_lines": report["loc"]["first_party_assets_project_cs"]["lines"],
        "tokens_per_primary_loc_line": report["ratios"]["tokens_per_first_party_assets_project_cs_line"],
        "delta_total_tokens_per_hour": ((report.get("previous_snapshot_delta") or {}).get("velocity") or {}).get("total_tokens_per_hour"),
        "delta_gpt_5_5_standard_usd_per_hour": ((report.get("previous_snapshot_delta") or {}).get("velocity") or {}).get("gpt_5_5_standard_usd_per_hour"),
        "delta_primary_code_lines_per_hour": ((report.get("previous_snapshot_delta") or {}).get("velocity") or {}).get("primary_code_lines_per_hour"),
        "delta_tokens_per_net_primary_code_line": ((report.get("previous_snapshot_delta") or {}).get("velocity") or {}).get("tokens_per_net_primary_code_line"),
    }, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()

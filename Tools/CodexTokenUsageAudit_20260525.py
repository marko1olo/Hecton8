import datetime
import json
import pathlib
import statistics
from collections import Counter, defaultdict

PROJECT = pathlib.Path(r"C:\hades\Hecton8")
ROOTS = [
    ("current_sessions", pathlib.Path(r"C:\Users\danat\.codex\sessions")),
    ("current_archived_sessions", pathlib.Path(r"C:\Users\danat\.codex\archived_sessions")),
    ("backup_cleanup_20260521_194850", pathlib.Path(r"C:\Users\danat\Documents\CodexBackups\codex_cleanup_20260521_194850")),
]

REPORT_JSON = PROJECT / "Docs" / "Reports" / "TOKEN_USAGE_AUDIT_2026-05-25.json"
REPORT_MD = PROJECT / "Docs" / "Reports" / "TOKEN_USAGE_AUDIT_2026-05-25.md"
LEDGER = PROJECT / "Docs" / "TOKEN_USAGE_LEDGER.md"
STATUS = PROJECT / "Docs" / "Tasks" / "Status_TOKEN_USAGE_AUDIT.md"
RATIONALE = PROJECT / "Docs" / "AgentLogs" / "Rationale_TOKEN_USAGE_AUDIT.md"
LOG = PROJECT / "Docs" / "AgentLogs" / "LOG_TOKEN_USAGE_AUDIT.md"

EXCLUDE_DIRS = {".git", ".vs", ".idea", "Library", "Temp", "Obj", "obj", "bin", "Logs", "UserSettings", "node_modules", ".gradle", ".cache"}
USAGE_KEYS = ("input_tokens", "cached_input_tokens", "output_tokens", "reasoning_output_tokens", "total_tokens")
UTC = datetime.timezone.utc
SAMARA = datetime.timezone(datetime.timedelta(hours=4))


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
    for path in PROJECT.rglob("*"):
        if not path.is_file():
            continue
        if set(path.parts) & EXCLUDE_DIRS:
            continue
        yield path


def count_lines(path):
    try:
        with path.open("rb") as handle:
            return sum(1 for _ in handle)
    except Exception:
        return 0


def count_loc():
    scopes = {
        "first_party_assets_project_cs": (PROJECT / "Assets" / "_Project", {".cs"}),
        "first_party_scripts_cs": (PROJECT / "Assets" / "_Project" / "Scripts", {".cs"}),
        "all_repo_cs_excluding_generated": (PROJECT, {".cs"}),
        "all_repo_source_broad": (PROJECT, {".cs", ".shader", ".hlsl", ".compute", ".cginc", ".uxml", ".uss", ".py", ".ps1", ".csproj", ".asmdef", ".json", ".md", ".txt"}),
        "tools_scripts": (PROJECT / "Tools", {".py", ".ps1"}),
        "docs_markdown_text": (PROJECT / "Docs", {".md", ".txt"}),
    }
    result = {key: {"files": 0, "lines": 0} for key in scopes}
    for path in iter_project_files():
        suffix = path.suffix.lower()
        for key, (prefix, suffixes) in scopes.items():
            try:
                in_scope = path.is_relative_to(prefix)
            except AttributeError:
                in_scope = str(path).lower().startswith(str(prefix).lower())
            if in_scope and suffix in suffixes:
                result[key]["files"] += 1
                result[key]["lines"] += count_lines(path)
    return result


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
    weekly = defaultdict(zero_usage)
    monthly = defaultdict(zero_usage)
    model_delta_usage = defaultdict(zero_usage)
    effort_delta_usage = defaultdict(zero_usage)
    model_effort_delta_usage = defaultdict(zero_usage)
    increment_parse_errors = 0
    for record in selected_with_usage:
        increments, errors = read_increment_events(pathlib.Path(record["path"]))
        increment_parse_errors += errors
        for ts, delta, model, effort in increments:
            local = ts.astimezone(SAMARA)
            add_usage(daily[local.date().isoformat()], delta)
            add_usage(weekly[week_key(local)], delta)
            add_usage(monthly[f"{local.year}-{local.month:02d}"], delta)
            add_usage(model_delta_usage[model or "unknown_model"], delta)
            add_usage(effort_delta_usage[effort or "unknown"], delta)
            add_usage(model_effort_delta_usage[f"{model or 'unknown_model'}::{effort or 'unknown'}"], delta)

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
        "gpt-5.4_mini_standard_equivalent": {"input": 0.75, "cached_input": 0.075, "output": 4.5},
    }
    model_rate_catalog = {
        "gpt-5.3-codex": {"input": 1.75, "cached_input": 0.175, "output": 14.0, "source": "developers.openai.com/api/docs/pricing specialized Codex standard"},
        "gpt-5.5": {"input": 5.0, "cached_input": 0.5, "output": 30.0, "source": "openai.com/api/pricing flagship standard under 270K context"},
        "gpt-5.4": {"input": 2.5, "cached_input": 0.25, "output": 15.0, "source": "openai.com/api/pricing flagship standard under 270K context"},
        "gpt-5.4-mini": {"input": 0.75, "cached_input": 0.075, "output": 4.5, "source": "openai.com/api/pricing flagship standard under 270K context"},
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
    for record in selected_with_usage:
        add_usage(model_final_usage[str(record.get("final_model") or "unknown_model")], record["final_usage"])
        add_usage(effort_final_usage[str(record.get("final_effort") or "unknown")], record["final_usage"])

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
    top_days = sorted(daily.items(), key=lambda item: item[1]["total_tokens"], reverse=True)[:20]
    active_days = [usage["total_tokens"] for usage in daily.values() if usage["total_tokens"] > 0]
    sorted_days = sorted(active_days)
    largest_session = max(session_totals) if session_totals else 0
    top_1_share = sum(sorted_totals[-top_1_percent_count:]) / total["total_tokens"] if top_1_percent_count and total["total_tokens"] else 0.0
    top_5_share = sum(sorted_totals[-top_5_percent_count:]) / total["total_tokens"] if top_5_percent_count and total["total_tokens"] else 0.0
    top_10_share = sum(sorted_totals[-top_10_percent_count:]) / total["total_tokens"] if top_10_percent_count and total["total_tokens"] else 0.0
    primary_standard = price_rows["gpt-5.3-codex_standard_api_equivalent"]
    primary_standard_no_cache = upper_no_cache["gpt-5.3-codex_standard_api_equivalent"]
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
        "gpt_5_3_codex_standard_cache_discount_saved_usd": primary_standard_no_cache - primary_standard["total_cost_usd"],
        "gpt_5_3_codex_standard_cost_per_primary_loc_usd": primary_standard["total_cost_usd"] / primary_loc,
        "gpt_5_3_codex_standard_cost_per_1k_primary_loc_usd": primary_standard["total_cost_usd"] / primary_loc * 1000,
        "tokens_per_dollar_gpt_5_3_codex_standard": total["total_tokens"] / primary_standard["total_cost_usd"] if primary_standard["total_cost_usd"] else 0,
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
        "interesting_stats": interesting_stats,
        "root_breakdown": dict(root_breakdown),
        "loc": loc,
        "ratios": ratios,
        "pricing": price_rows,
        "pricing_upper_bound_no_cache_usd": upper_no_cache,
        "model_rate_catalog": model_rate_catalog,
        "model_final_session_usage": dict(model_final_usage),
        "model_delta_usage": dict(model_delta_usage),
        "model_delta_minus_final_total": sub_usage(daily_delta_sum, total),
        "model_specific_standard_costs": known_model_costs,
        "model_specific_cost_bounds": model_cost_bounds,
        "effort_final_session_usage": dict(effort_final_usage),
        "effort_delta_usage": dict(effort_delta_usage),
        "model_effort_delta_usage": dict(model_effort_delta_usage),
        "final_model_counts": dict(final_model_counts.most_common()),
        "final_effort_counts": dict(final_effort_counts.most_common()),
        "context_window_counts": dict(context_counts.most_common()),
        "plan_type_counts": dict(plan_counts.most_common()),
        "originator_counts": dict(originator_counts.most_common()),
        "source_counts": dict(source_counts.most_common()),
        "cli_version_counts": dict(cli_counts.most_common(20)),
        "daily": {key: value for key, value in sorted(daily.items())},
        "weekly": {key: value for key, value in sorted(weekly.items())},
        "monthly": {key: value for key, value in sorted(monthly.items())},
        "top_days": [{"date": key, **value} for key, value in top_days],
        "top_sessions": [
            {
                "session_id": record.get("session_id"),
                "path": record.get("path"),
                "root": record.get("root"),
                "final_timestamp": record.get("final_timestamp"),
                "cwd": record.get("cwd"),
                "originator": record.get("originator"),
                "source": record.get("source"),
                "cli_version": record.get("cli_version"),
                "plan_type": record.get("plan_type"),
                "final_model": record.get("final_model") or "unknown_model",
                "final_effort": record.get("final_effort") or "unknown",
                **record.get("final_usage"),
            }
            for record in top_sessions
        ],
        "pricing_sources": [
            "https://developers.openai.com/api/docs/pricing lines 851-854 for gpt-5.3-codex standard",
            "https://developers.openai.com/api/docs/pricing lines 866-867 for gpt-5.3-codex priority",
            "https://openai.com/api/pricing/ lines 33-76 for GPT-5.5/GPT-5.4/GPT-5.4-mini standard short-context",
        ],
    }
    return report


def usage_rows(items):
    lines = []
    for key, usage in items:
        lines.append(f"| {key} | {fmt_int(usage['total_tokens'])} | {fmt_int(usage['input_tokens'])} | {fmt_int(usage['cached_input_tokens'])} | {fmt_int(usage['output_tokens'])} | {fmt_int(usage['reasoning_output_tokens'])} |")
    return lines


def write_reports(report):
    REPORT_JSON.parent.mkdir(parents=True, exist_ok=True)
    REPORT_JSON.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    total = report["totals"]
    primary = report["pricing"]["gpt-5.3-codex_standard_api_equivalent"]
    upper_primary = report["pricing_upper_bound_no_cache_usd"]["gpt-5.3-codex_standard_api_equivalent"]

    md = []
    md += ["# TOKEN USAGE AUDIT 2026-05-25", "", f"Generated UTC: {report['generated_at_utc']}", f"Generated Samara: {report['generated_at_samara']}", "Evidence class: STATIC_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Not billing-provider proof.", ""]
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

    md += ["", "## API-Equivalent Price Scenarios", "Actual Codex billing cannot be proven from local JSONL. These are API-equivalent estimates using official OpenAI rates current on 2026-05-25. Cached input is charged at cached-input rate; reasoning output is an output subcounter, not added twice.", "", "| Scenario | Uncached input | Cached input | Output | Total | No-cache upper bound |", "|---|---:|---:|---:|---:|---:|"]
    for name, row in report["pricing"].items():
        md.append(f"| {name} | {fmt_money(row['uncached_input_cost_usd'])} | {fmt_money(row['cached_input_cost_usd'])} | {fmt_money(row['output_cost_usd'])} | {fmt_money(row['total_cost_usd'])} | {fmt_money(report['pricing_upper_bound_no_cache_usd'][name])} |")

    md += ["", "## Model Forensics", "Model evidence comes from structured `turn_context.payload.model` / `collaboration_mode.settings.model` fields when present. Sessions without that field are `unknown_model`; local JSONL still does not expose invoice SKU, priority mode, or contractual billing plan.", "", "### Final Session Model Attribution", "| Model | Sessions | Total tokens | Input | Cached input | Output | Reasoning output | Standard cost if rate known |", "|---|---:|---:|---:|---:|---:|---:|---:|"]
    for model, usage in sorted(report["model_final_session_usage"].items(), key=lambda item: item[1]["total_tokens"], reverse=True):
        cost = report["model_specific_standard_costs"].get(model)
        cost_text = fmt_money(cost["total_cost_usd"]) if cost else "unpriced"
        md.append(f"| {model} | {fmt_int(report['final_model_counts'].get(model, 0))} | {fmt_int(usage['total_tokens'])} | {fmt_int(usage['input_tokens'])} | {fmt_int(usage['cached_input_tokens'])} | {fmt_int(usage['output_tokens'])} | {fmt_int(usage['reasoning_output_tokens'])} | {cost_text} |")
    md += ["", "### Temporal Delta Model Attribution", "This table assigns each token delta to the latest prior `turn_context` model in the same JSONL file. It is useful for trend analysis, but all-time totals above remain final-session authority.", "", "| Model | Delta total | Delta input | Delta cached input | Delta output | Delta reasoning output |", "|---|---:|---:|---:|---:|---:|"]
    for model, usage in sorted(report["model_delta_usage"].items(), key=lambda item: item[1]["total_tokens"], reverse=True):
        md.append(f"| {model} | {fmt_int(usage['total_tokens'])} | {fmt_int(usage['input_tokens'])} | {fmt_int(usage['cached_input_tokens'])} | {fmt_int(usage['output_tokens'])} | {fmt_int(usage['reasoning_output_tokens'])} |")
    md += ["", "### Reasoning Effort Attribution", "| Effort | Sessions | Total tokens | Input | Cached input | Output | Reasoning output |", "|---|---:|---:|---:|---:|---:|---:|"]
    for effort, usage in sorted(report["effort_final_session_usage"].items(), key=lambda item: item[1]["total_tokens"], reverse=True):
        md.append(f"| {effort} | {fmt_int(report['final_effort_counts'].get(effort, 0))} | {fmt_int(usage['total_tokens'])} | {fmt_int(usage['input_tokens'])} | {fmt_int(usage['cached_input_tokens'])} | {fmt_int(usage['output_tokens'])} | {fmt_int(usage['reasoning_output_tokens'])} |")
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

    md += ["", "## LOC And Token Ratios", "| Scope | Files | Lines | Tokens / line | Output tokens / line |", "|---|---:|---:|---:|---:|"]
    for scope, row in report["loc"].items():
        lines = row["lines"]
        tokens_per_line = total["total_tokens"] / lines if lines else 0
        output_per_line = total["output_tokens"] / lines if lines else 0
        md.append(f"| {scope} | {fmt_int(row['files'])} | {fmt_int(lines)} | {tokens_per_line:,.2f} | {output_per_line:,.4f} |")

    md += ["", "## Daily Stats", "| Date Samara | Total | Input | Cached input | Output | Reasoning output |", "|---|---:|---:|---:|---:|---:|"]
    md += usage_rows(report["daily"].items())
    md += ["", "## Weekly Stats", "| ISO Week Samara | Total | Input | Cached input | Output | Reasoning output |", "|---|---:|---:|---:|---:|---:|"]
    md += usage_rows(report["weekly"].items())
    md += ["", "## Monthly Stats", "| Month Samara | Total | Input | Cached input | Output | Reasoning output |", "|---|---:|---:|---:|---:|---:|"]
    md += usage_rows(report["monthly"].items())
    md += ["", "## Top 20 Days", "| Date Samara | Total tokens |", "|---|---:|"]
    for row in report["top_days"]:
        md.append(f"| {row['date']} | {fmt_int(row['total_tokens'])} |")

    md += ["", "## Distributions", "| Metric | Value |", "|---|---:|"]
    for key, value in report["averages"].items():
        md.append(f"| {key} | {float(value):,.2f} |")
    md += ["", "Context window counts:"]
    for key, value in report["context_window_counts"].items():
        md.append(f"- {key}: {fmt_int(value)}")
    md += ["", "Plan type counts:"]
    for key, value in report["plan_type_counts"].items():
        md.append(f"- {key}: {fmt_int(value)}")

    md += ["", "## Top 25 Sessions", "| Rank | Session | Model | Effort | Root | Final UTC | Total | Input | Cached | Output | CWD |", "|---:|---|---|---|---|---|---:|---:|---:|---:|---|"]
    for index, row in enumerate(report["top_sessions"], 1):
        sid = (row.get("session_id") or "missing")[:36]
        cwd = (row.get("cwd") or "").replace("|", "/")
        md.append(f"| {index} | `{sid}` | {row.get('final_model')} | {row.get('final_effort')} | {row.get('root')} | {row.get('final_timestamp')} | {fmt_int(row['total_tokens'])} | {fmt_int(row['input_tokens'])} | {fmt_int(row['cached_input_tokens'])} | {fmt_int(row['output_tokens'])} | `{cwd}` |")

    md += ["", "## Price Sources"]
    for source in report["pricing_sources"]:
        md.append(f"- {source}")
    md += ["", "## Residual Risk", "- Local JSONL is not provider billing. It lacks invoice ids and does not expose whether a Codex request used standard, priority, enterprise, subscription, or internal billing.", "- `cached_input_tokens` is treated as a priced subcounter of input tokens, not additional total tokens.", "- Model labels are exact only where structured `turn_context` fields exist. Older sessions without model fields remain `unknown_model`.", "- Daily/week/model delta allocation is reconstructed from telemetry deltas; all-time final per-session total remains authoritative for this local audit."]
    REPORT_MD.write_text("\n".join(md) + "\n", encoding="utf-8")

    ledger = []
    generated = datetime.datetime.fromisoformat(report["generated_at_samara"]).strftime("%Y-%m-%d %H:%M")
    ledger += ["# Codex Token Usage Ledger", "", f"Date: {generated} Europe/Samara", "Status: CURRENT STATIC LOCAL TELEMETRY SNAPSHOT", "", "This file is the stable token accounting surface. The detailed current report is `Docs/Reports/TOKEN_USAGE_AUDIT_2026-05-25.md`; machine-readable data is `Docs/Reports/TOKEN_USAGE_AUDIT_2026-05-25.json`.", ""]
    ledger += ["## Current Total", "", r"Scope: current `C:\Users\danat\.codex\sessions`, current `C:\Users\danat\.codex\archived_sessions`, and backup `C:\Users\danat\Documents\CodexBackups\codex_cleanup_20260521_194850`.", "", "Accounting rule: parse JSONL `session_meta`/`token_count`, take the final per-session `payload.info.total_token_usage`, dedupe by `session_meta.id`, and keep the highest final `total_tokens` for duplicate records. Day/week/month stats in the dated report use positive in-session deltas.", "", "| Metric | Value |", "|---|---:|"]
    for key in ("unique_session_or_path_keys", "sessions_with_usage", "sessions_without_usage", "duplicate_records_removed", "files_missing_session_id"):
        ledger.append(f"| {key} | {fmt_int(report[key])} |")
    ledger.append(f"| First selected timestamp UTC | {report['first_selected_timestamp_utc']} |")
    ledger.append(f"| Last selected timestamp UTC | {report['last_selected_timestamp_utc']} |")
    for key in USAGE_KEYS:
        ledger.append(f"| {key} | {fmt_int(total[key])} |")
    ledger.append(f"| Uncached input tokens | {fmt_int(report['uncached_input_tokens'])} |")
    ledger.append(f"| Cached-input ratio | {report['cache_ratio']:.6%} |")
    ledger += ["", "`cached_input_tokens` is a telemetry subcounter of input-token reuse, not an extra token class to add on top of `total_tokens`.", "", "## API-Equivalent Cost Snapshot", "", "Local Codex telemetry is not an invoice. The primary estimate uses official `gpt-5.3-codex` standard API-equivalent rates current on 2026-05-25: input $1.75/1M, cached input $0.175/1M, output $14/1M.", "", "| Scenario | Total | No-cache upper bound |", "|---|---:|---:|"]
    ledger.append(f"| gpt-5.3-codex standard API-equivalent | {fmt_money(primary['total_cost_usd'])} | {fmt_money(upper_primary)} |")
    for name in ("gpt-5.3-codex_priority_api_equivalent", "gpt-5.4_standard_short_context_equivalent", "gpt-5.5_standard_short_context_equivalent"):
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
    for key in ("active_days", "mean_tokens_per_active_day", "median_tokens_per_active_day", "session_gini_total_tokens", "top_1_percent_sessions_share", "top_10_percent_sessions_share", "equivalent_full_258400_context_windows", "gpt_5_3_codex_standard_cache_discount_saved_usd", "gpt_5_3_codex_standard_cost_per_1k_primary_loc_usd"):
        value = report["interesting_stats"][key]
        if key.endswith("_share"):
            ledger.append(f"| {key} | {float(value):.4%} |")
        elif key.endswith("_usd"):
            ledger.append(f"| {key} | {fmt_money(value)} |")
        else:
            ledger.append(f"| {key} | {float(value):,.4f} |")
    ledger += ["", "## Root Breakdown", "", "| Root | JSONL files | Files with usage | Selected sessions with usage | Selected total tokens |", "|---|---:|---:|---:|---:|"]
    for root, row in sorted(report["root_breakdown"].items()):
        ledger.append(f"| {root} | {fmt_int(row['jsonl_files'])} | {fmt_int(row['files_with_usage'])} | {fmt_int(row['selected_with_usage'])} | {fmt_int(row['selected_total_tokens'])} |")
    ledger += ["", "## Evidence Boundary", "", "Evidence class: static local filesystem telemetry. This is not billing-provider proof, Unity runtime proof, or profiler proof."]
    LEDGER.write_text("\n".join(ledger) + "\n", encoding="utf-8")


def append_audit_files(report):
    generated = datetime.datetime.fromisoformat(report["generated_at_samara"]).strftime("%Y-%m-%d %H:%M")
    primary = report["pricing"]["gpt-5.3-codex_standard_api_equivalent"]
    for path in (STATUS, RATIONALE, LOG):
        path.parent.mkdir(parents=True, exist_ok=True)
    marker = "## Model Forensics Refresh 2026-05-25"
    if STATUS.exists() and marker in STATUS.read_text(encoding="utf-8", errors="replace"):
        return
    with STATUS.open("a", encoding="utf-8") as handle:
        handle.write(f"""

{marker} {generated[-5:]} Europe/Samara

- [x] Task 17 - Extract structural model labels | Justification: parsed JSONL `turn_context` model fields instead of text-grepping prompts; DOD practice was evidence-class separation. Alternative rejected: inferring model from extension name or prompt text. Microseconds saved: 0 audit-only.
- [x] Task 18 - Add model-specific cost bounds | Justification: priced only model labels with official standard rates and isolated known-but-unpriced labels. Alternative rejected: pretending local JSONL proves billing SKU or priority tier. Microseconds saved: 0 audit-only.
- [x] Task 19 - Add interpretive token statistics | Justification: added concentration, cache-savings, context-window, daily/session distribution, and LOC-cost diagnostics as derived metrics. Alternative rejected: hiding all shape behind one aggregate total. Microseconds saved: 0 audit-only.
- [x] Task 20 - Reorder token documentation | Justification: kept one stable ledger plus one dated report and moved model/interpretive stats into those surfaces. Alternative rejected: creating scattered side reports. Microseconds saved: 0 audit-only.
""")
    with RATIONALE.open("a", encoding="utf-8") as handle:
        handle.write("""

## Decision 9 - 2026-05-25 model-price forensics

Problem: User requested more exact model pricing, but local Codex JSONL does not expose invoice SKU, subscription handling, or priority tier, and several exact model labels lack public rate rows.
Solution: Attribute tokens to exact structural model labels, price labels with official standard rates, and isolate known-but-unpriced labels into explicit bounds.
Rejected Alternatives: Treating every session as gpt-5.5 or every session as gpt-5.3-codex was rejected because it hides the evidence boundary.
Scalability potential: Low/Middle/High/Ultra runtime tiers unaffected; this is local telemetry accounting.
Hardware Impact: 0 us runtime gain.

## Decision 10 - 2026-05-25 documentation shape

Problem: Token docs risk becoming scattered across dated reports, ledger, and chat-only claims.
Solution: Keep `Docs/TOKEN_USAGE_LEDGER.md` as the stable summary and `Docs/Reports/TOKEN_USAGE_AUDIT_2026-05-25.md/.json` as the full forensic artifact.
Rejected Alternatives: Creating another standalone model-only report was rejected as documentation sprawl.
Scalability potential: Future audits have one stable entry point and one dated evidence artifact.
Hardware Impact: 0 us runtime gain.
""")
    with LOG.open("a", encoding="utf-8") as handle:
        handle.write(f"""

## 2026-05-25 TOKEN_USAGE_AUDIT model-price/statistics refresh

What was wrong -> Prior token report priced broad scenarios but did not separate structurally observed model labels from unknown historical sessions.
What was done -> Added model attribution, model-cost bounds, cache-savings, Pareto/Gini/session/day/context-window/LOC-cost diagnostics, and refreshed ledger/report from {fmt_int(report['file_count'])} JSONL files.
Cinematic Cheats used -> None; audit/process hygiene only.
Exact Microseconds saved -> 0 us game runtime. Static telemetry and docs only.
Token report -> Docs/Reports/TOKEN_USAGE_AUDIT_2026-05-25.md and .json. Total tokens {fmt_int(report['totals']['total_tokens'])}; all-as-gpt-5.3-codex standard API-equivalent {fmt_money(primary['total_cost_usd'])}; model-bound known+unpriced-as-gpt-5.5 standard {fmt_money(report['model_specific_cost_bounds']['known_plus_unpriced_as_gpt_5_5_standard_usd'])}.
Evidence -> STATIC_LOCAL_CODEX_JSONL_AND_FILESYSTEM plus official OpenAI pricing pages. Runtime/Unity PlayMode proof absent.
""")


def main():
    report = build_report()
    write_reports(report)
    append_audit_files(report)
    primary = report["pricing"]["gpt-5.3-codex_standard_api_equivalent"]
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
        "gpt_5_3_codex_standard_cost": primary["total_cost_usd"],
        "gpt_5_3_codex_standard_upper_no_cache": report["pricing_upper_bound_no_cache_usd"]["gpt-5.3-codex_standard_api_equivalent"],
        "primary_loc_lines": report["loc"]["first_party_assets_project_cs"]["lines"],
        "tokens_per_primary_loc_line": report["ratios"]["tokens_per_first_party_assets_project_cs_line"],
    }, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()

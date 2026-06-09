import datetime
import importlib.util
import json
import pathlib
from collections import defaultdict


PROJECT = pathlib.Path(r"C:\hades\Hecton8")
SAMARA = datetime.timezone(datetime.timedelta(hours=4))
UTC = datetime.timezone.utc
REPORT_DATE = datetime.datetime.now(SAMARA).date().isoformat()
TOKEN_REPORT_DIR = PROJECT / "Docs" / "DEPRECATED" / "Root_Docs_Noise_2026-05-26"
REPORT_JSON = TOKEN_REPORT_DIR / f"TOKEN_USAGE_AUDIT_{REPORT_DATE}.json"
REPORT_MD = TOKEN_REPORT_DIR / f"TOKEN_USAGE_AUDIT_{REPORT_DATE}.md"
LEDGER = TOKEN_REPORT_DIR / "TOKEN_USAGE_LEDGER.md"
AUDIT_SCRIPT = PROJECT / "Tools" / "CodexTokenUsageAudit_20260525.py"
USAGE_KEYS = ("input_tokens", "cached_input_tokens", "output_tokens", "reasoning_output_tokens", "total_tokens")
PRIMARY_PRICE_KEY = "gpt-5.5_standard_short_context_equivalent"
CODEX_STANDARD_PRICE_KEY = "gpt-5.3-codex_standard_api_equivalent"
GPT55_LONG_CONTEXT_PRICE_KEY = "gpt-5.5_standard_long_context_surcharge_upper_bound_equivalent"
GPT55_REGIONAL_PRICE_KEY = "gpt-5.5_standard_regional_10pct_equivalent"
LONG_CONTEXT_INPUT_TOKEN_TRIGGER = 272_000

PRICING = {
    "gpt-5.3-codex_standard_api_equivalent": {"input": 1.75, "cached_input": 0.175, "output": 14.0},
    "gpt-5.3-codex_priority_api_equivalent": {"input": 3.5, "cached_input": 0.35, "output": 28.0},
    "gpt-5.4_standard_short_context_equivalent": {"input": 2.5, "cached_input": 0.25, "output": 15.0},
    "gpt-5.5_standard_short_context_equivalent": {"input": 5.0, "cached_input": 0.5, "output": 30.0},
    "gpt-5.5_batch_short_context_equivalent": {"input": 2.5, "cached_input": 0.25, "output": 15.0},
    "gpt-5.5_flex_short_context_equivalent": {"input": 2.5, "cached_input": 0.25, "output": 15.0},
    "gpt-5.5_priority_short_context_equivalent": {"input": 12.5, "cached_input": 1.25, "output": 75.0},
    "gpt-5.4_mini_standard_equivalent": {"input": 0.75, "cached_input": 0.075, "output": 4.5},
    GPT55_LONG_CONTEXT_PRICE_KEY: {"input": 10.0, "cached_input": 1.0, "output": 45.0},
    GPT55_REGIONAL_PRICE_KEY: {"input": 5.5, "cached_input": 0.55, "output": 33.0},
}


def load_audit_module():
    spec = importlib.util.spec_from_file_location("token_audit", AUDIT_SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


audit = load_audit_module()


def zero_usage():
    return {key: 0 for key in USAGE_KEYS}


def clone_usage(usage):
    return {key: int((usage or {}).get(key, 0) or 0) for key in USAGE_KEYS}


def add_usage(target, source):
    for key in USAGE_KEYS:
        target[key] += int((source or {}).get(key, 0) or 0)
    return target


def sub_usage(left, right):
    return {key: int((left or {}).get(key, 0) or 0) - int((right or {}).get(key, 0) or 0) for key in USAGE_KEYS}


def parse_ts(value):
    return audit.parse_ts(value)


def fmt_int(value):
    return f"{int(value):,}"


def fmt_money(value):
    return f"${float(value):,.2f}"


def fmt_float(value, decimals=2):
    if value is None:
        return "n/a"
    return f"{float(value):,.{decimals}f}"


def safe_div(value, denominator):
    if not denominator:
        return None
    return float(value) / float(denominator)


def build_layperson_scale(total, delta, pricing, primary_cost_delta, velocity, cache_ratio):
    primary_total_cost = pricing[PRIMARY_PRICE_KEY]["total_cost_usd"]
    total_words = total["total_tokens"] * 0.75
    delta_words = delta["total_tokens"] * 0.75
    output_words = total["output_tokens"] * 0.75
    reasoning_words = total["reasoning_output_tokens"] * 0.75
    tokens_per_second = velocity.get("total_tokens_per_second") or 0.0
    dollars_per_hour = velocity.get("gpt_5_5_standard_usd_per_hour") or 0.0
    return {
        "assumptions": {
            "token_to_word_note": "Human-scale conversions use a rough English-text heuristic: 1 token ~= 0.75 words. Russian/code/tokenizer behavior varies; this is scale communication, not billing math.",
            "printed_page_words": 500,
            "book_words": 80_000,
            "reading_speed_words_per_minute": 250,
            "workday_reading_hours": 8,
            "reference_game_price_usd": 60,
            "reference_workstation_price_usd": 2000,
        },
        "all_time": {
            "approx_words": total_words,
            "approx_printed_pages_500_words": total_words / 500,
            "approx_80k_word_books": total_words / 80_000,
            "continuous_reading_years_at_250_wpm": total_words / (250 * 60 * 24 * 365),
            "workday_reading_years_at_250_wpm": total_words / (250 * 60 * 8 * 365),
            "gpt_5_5_standard_usd": primary_total_cost,
            "equivalent_60_usd_games": primary_total_cost / 60,
            "equivalent_2000_usd_workstations": primary_total_cost / 2000,
            "cache_ratio_percent": cache_ratio * 100,
            "uncached_input_ratio_percent": (1 - cache_ratio) * 100,
            "output_words_equivalent": output_words,
            "reasoning_words_equivalent": reasoning_words,
        },
        "since_previous_snapshot": {
            "approx_words": delta_words,
            "approx_printed_pages_500_words": delta_words / 500,
            "approx_80k_word_books": delta_words / 80_000,
            "gpt_5_5_standard_usd_delta": primary_cost_delta,
            "tokens_per_second": tokens_per_second,
            "approx_words_per_second": tokens_per_second * 0.75,
            "approx_pages_per_hour": safe_div((velocity.get("total_tokens_per_hour") or 0.0) * 0.75, 500),
            "gpt_5_5_standard_usd_per_hour": dollars_per_hour,
            "gpt_5_5_standard_usd_per_day_at_current_velocity": dollars_per_hour * 24,
        },
    }


def find_previous_report():
    current = datetime.date.fromisoformat(REPORT_DATE)
    if REPORT_JSON.exists():
        return REPORT_JSON
    for days_back in range(1, 14):
        candidate = TOKEN_REPORT_DIR / f"TOKEN_USAGE_AUDIT_{(current - datetime.timedelta(days=days_back)).isoformat()}.json"
        if candidate.exists():
            return candidate
    raise FileNotFoundError("previous token report not found")


def read_previous():
    path = find_previous_report()
    return path, json.loads(path.read_text(encoding="utf-8-sig"))


def usage_cost(usage, rate):
    cached = int(usage.get("cached_input_tokens", 0) or 0)
    input_tokens = int(usage.get("input_tokens", 0) or 0)
    output = int(usage.get("output_tokens", 0) or 0)
    uncached = max(0, input_tokens - cached)
    return {
        "uncached_input_cost_usd": uncached / 1_000_000 * rate["input"],
        "cached_input_cost_usd": cached / 1_000_000 * rate["cached_input"],
        "output_cost_usd": output / 1_000_000 * rate["output"],
    }


def price_row(usage, rate):
    row = usage_cost(usage, rate)
    row["total_cost_usd"] = row["uncached_input_cost_usd"] + row["cached_input_cost_usd"] + row["output_cost_usd"]
    return row


def no_cache_cost(usage, rate):
    return (int(usage.get("input_tokens", 0) or 0) / 1_000_000 * rate["input"]) + (int(usage.get("output_tokens", 0) or 0) / 1_000_000 * rate["output"])


def usage_map_costs(period_map, rate):
    return {key: price_row(usage, rate)["total_cost_usd"] for key, usage in sorted(period_map.items())}


def merge_period_map(previous_map):
    merged = defaultdict(zero_usage)
    for key, usage in (previous_map or {}).items():
        merged[key] = clone_usage(usage)
    return merged


def week_key(dt):
    iso = dt.isocalendar()
    return f"{iso.year}-W{iso.week:02d}"


def update_scope_economics(previous, total):
    try:
        loc = audit.count_loc()
    except Exception:
        return previous.get("loc") or {}, previous.get("scope_economics") or {}
    primary_cost = price_row(total, PRICING[PRIMARY_PRICE_KEY])["total_cost_usd"]
    codex_cost = price_row(total, PRICING[CODEX_STANDARD_PRICE_KEY])["total_cost_usd"]
    scope_economics = {}
    for scope, row in loc.items():
        lines = max(1, row["lines"])
        chars = max(1, row["characters"])
        non_ws = max(1, row["non_whitespace_characters"])
        scope_economics[scope] = {
            **row,
            "tokens_per_line": total["total_tokens"] / lines,
            "tokens_per_1k_characters": total["total_tokens"] / chars * 1000,
            "output_tokens_per_1k_characters": total["output_tokens"] / chars * 1000,
            "tokens_per_1k_non_whitespace_characters": total["total_tokens"] / non_ws * 1000,
            "gpt_5_5_standard_usd_per_1k_lines": primary_cost / lines * 1000,
            "gpt_5_5_standard_usd_per_1k_characters": primary_cost / chars * 1000,
            "gpt_5_3_codex_standard_usd_per_1k_characters": codex_cost / chars * 1000,
            "observed_model_high_bound_usd_per_1k_characters": primary_cost / chars * 1000,
        }
    return loc, scope_economics


def per_hour(value, elapsed_hours):
    if not elapsed_hours or elapsed_hours <= 0:
        return None
    return float(value) / elapsed_hours


def build_velocity(delta, elapsed_hours, primary_cost_delta, priority_cost_delta, codex_cost_delta, code_lines_delta, code_chars_delta, file_delta, sessions_delta):
    uncached_delta = delta["input_tokens"] - delta["cached_input_tokens"]

    def per_day(value):
        hourly = per_hour(value, elapsed_hours)
        return None if hourly is None else hourly * 24

    def per_minute(value):
        hourly = per_hour(value, elapsed_hours)
        return None if hourly is None else hourly / 60

    def per_second(value):
        hourly = per_hour(value, elapsed_hours)
        return None if hourly is None else hourly / 3600

    def per_unit(value, denominator):
        if denominator <= 0:
            return None
        return float(value) / denominator

    return {
        "total_tokens_per_hour": per_hour(delta["total_tokens"], elapsed_hours),
        "total_tokens_per_day": per_day(delta["total_tokens"]),
        "total_tokens_per_minute": per_minute(delta["total_tokens"]),
        "total_tokens_per_second": per_second(delta["total_tokens"]),
        "input_tokens_per_hour": per_hour(delta["input_tokens"], elapsed_hours),
        "cached_input_tokens_per_hour": per_hour(delta["cached_input_tokens"], elapsed_hours),
        "uncached_input_tokens_per_hour": per_hour(uncached_delta, elapsed_hours),
        "output_tokens_per_hour": per_hour(delta["output_tokens"], elapsed_hours),
        "reasoning_output_tokens_per_hour": per_hour(delta["reasoning_output_tokens"], elapsed_hours),
        "sessions_with_usage_per_hour": per_hour(sessions_delta, elapsed_hours),
        "jsonl_files_per_hour": per_hour(file_delta, elapsed_hours),
        "primary_code_lines_per_hour": per_hour(code_lines_delta, elapsed_hours),
        "primary_code_lines_per_day": per_day(code_lines_delta),
        "primary_code_characters_per_hour": per_hour(code_chars_delta, elapsed_hours),
        "primary_code_characters_per_day": per_day(code_chars_delta),
        "tokens_per_net_primary_code_line": per_unit(delta["total_tokens"], code_lines_delta),
        "input_tokens_per_net_primary_code_line": per_unit(delta["input_tokens"], code_lines_delta),
        "output_tokens_per_net_primary_code_line": per_unit(delta["output_tokens"], code_lines_delta),
        "reasoning_tokens_per_net_primary_code_line": per_unit(delta["reasoning_output_tokens"], code_lines_delta),
        "tokens_per_1k_net_primary_code_chars": per_unit(delta["total_tokens"] * 1000, code_chars_delta),
        "output_tokens_per_1k_net_primary_code_chars": per_unit(delta["output_tokens"] * 1000, code_chars_delta),
        "gpt_5_5_standard_usd_per_hour": per_hour(primary_cost_delta, elapsed_hours),
        "gpt_5_5_standard_usd_per_day": per_day(primary_cost_delta),
        "gpt_5_5_priority_usd_per_hour": per_hour(priority_cost_delta, elapsed_hours),
        "gpt_5_3_codex_standard_usd_per_hour": per_hour(codex_cost_delta, elapsed_hours),
        "gpt_5_5_standard_usd_per_net_primary_code_line": per_unit(primary_cost_delta, code_lines_delta),
        "gpt_5_5_standard_usd_per_1k_net_primary_code_chars": per_unit(primary_cost_delta * 1000, code_chars_delta),
    }


def build_report():
    previous_path, previous = read_previous()
    previous_snapshot_mode = "same_day_existing_report" if previous_path == REPORT_JSON else "prior_dated_report"
    now_utc = datetime.datetime.now(UTC)
    now_local = now_utc.astimezone(SAMARA)
    previous_generated = parse_ts(previous.get("generated_at_samara"))
    previous_last = parse_ts(previous.get("last_selected_timestamp_utc"))
    cutoff = previous_last or previous_generated
    if cutoff is None:
        cutoff = now_utc - datetime.timedelta(days=1)
    hourly_cutoff = now_utc - datetime.timedelta(hours=120)
    files = audit.collect_jsonl_files()
    changed_files = []
    for root, path, size, mtime in files:
        mtime_utc = datetime.datetime.fromtimestamp(mtime, UTC)
        if mtime_utc >= cutoff - datetime.timedelta(minutes=10):
            changed_files.append((root, path, size, mtime))

    delta_total = zero_usage()
    long_context_delta_usage = zero_usage()
    hourly = merge_period_map(previous.get("hourly") or {})
    daily = merge_period_map(previous.get("daily") or {})
    weekly = merge_period_map(previous.get("weekly") or {})
    monthly = merge_period_map(previous.get("monthly") or {})
    new_session_ids = set()
    parse_errors = 0
    max_event_ts = cutoff
    increment_events_after_cutoff = 0
    long_context_increment_events_after_cutoff = 0

    for _root, path, _size, _mtime in changed_files:
        record = audit.read_file_record(path)
        meta_ts = parse_ts(record.get("meta_timestamp"))
        if meta_ts and meta_ts > cutoff and audit.has_usage(record.get("final_usage")):
            new_session_ids.add(record.get("session_id") or str(path))
        increments, errors = audit.read_increment_events(path)
        parse_errors += errors
        for ts, delta, _model, _effort in increments:
            if ts <= cutoff:
                continue
            increment_events_after_cutoff += 1
            if int(delta.get("input_tokens", 0) or 0) > LONG_CONTEXT_INPUT_TOKEN_TRIGGER:
                long_context_increment_events_after_cutoff += 1
                add_usage(long_context_delta_usage, delta)
            max_event_ts = max(max_event_ts, ts)
            add_usage(delta_total, delta)
            local = ts.astimezone(SAMARA)
            add_usage(hourly[local.strftime("%Y-%m-%d %H:00")], delta)
            add_usage(daily[local.date().isoformat()], delta)
            add_usage(weekly[week_key(local)], delta)
            add_usage(monthly[f"{local.year}-{local.month:02d}"], delta)

    hourly = defaultdict(zero_usage, {
        key: value
        for key, value in hourly.items()
        if parse_ts(f"{key}:00+04:00") is None or parse_ts(f"{key}:00+04:00") >= hourly_cutoff
    })

    total = clone_usage(previous.get("totals") or {})
    add_usage(total, delta_total)
    pricing = {key: price_row(total, rate) for key, rate in PRICING.items()}
    upper_no_cache = {key: no_cache_cost(total, rate) for key, rate in PRICING.items()}
    loc, scope_economics = update_scope_economics(previous, total)
    previous_scope = (previous.get("scope_economics") or {}).get("first_party_assets_project_cs") or {}
    current_scope = (scope_economics or {}).get("first_party_assets_project_cs") or previous_scope
    code_lines_delta = int(current_scope.get("lines", 0) or 0) - int(previous_scope.get("lines", 0) or 0)
    code_chars_delta = int(current_scope.get("characters", 0) or 0) - int(previous_scope.get("characters", 0) or 0)
    elapsed_hours = (now_local - previous_generated.astimezone(SAMARA)).total_seconds() / 3600 if previous_generated else None
    primary_delta = pricing[PRIMARY_PRICE_KEY]["total_cost_usd"] - float(((previous.get("pricing") or {}).get(PRIMARY_PRICE_KEY) or {}).get("total_cost_usd", 0) or 0)
    priority_delta = pricing["gpt-5.5_priority_short_context_equivalent"]["total_cost_usd"] - float(((previous.get("pricing") or {}).get("gpt-5.5_priority_short_context_equivalent") or {}).get("total_cost_usd", 0) or 0)
    codex_delta = pricing[CODEX_STANDARD_PRICE_KEY]["total_cost_usd"] - float(((previous.get("pricing") or {}).get(CODEX_STANDARD_PRICE_KEY) or {}).get("total_cost_usd", 0) or 0)
    file_delta = len(files) - int(previous.get("file_count", 0) or 0)
    session_delta = len(new_session_ids)
    velocity = build_velocity(delta_total, elapsed_hours, primary_delta, priority_delta, codex_delta, code_lines_delta, code_chars_delta, file_delta, session_delta)
    uncached_input = max(0, total["input_tokens"] - total["cached_input_tokens"])
    long_context_row = pricing[GPT55_LONG_CONTEXT_PRICE_KEY]
    primary_row = pricing[PRIMARY_PRICE_KEY]
    regional_row = pricing[GPT55_REGIONAL_PRICE_KEY]
    long_context_delta = long_context_row["total_cost_usd"] - primary_row["total_cost_usd"]
    regional_delta = regional_row["total_cost_usd"] - primary_row["total_cost_usd"]
    long_context_regional_upper = long_context_row["total_cost_usd"] * 1.1
    base_delta_cost = price_row(delta_total, PRICING[PRIMARY_PRICE_KEY])["total_cost_usd"]
    post_cutoff_long_context_base_cost = price_row(long_context_delta_usage, PRICING[PRIMARY_PRICE_KEY])["total_cost_usd"]
    post_cutoff_long_context_surcharge_cost = price_row(long_context_delta_usage, PRICING[GPT55_LONG_CONTEXT_PRICE_KEY])["total_cost_usd"] - post_cutoff_long_context_base_cost
    layperson_scale = build_layperson_scale(total, delta_total, pricing, primary_delta, velocity, total["cached_input_tokens"] / max(1, total["input_tokens"]))

    report = {
        **previous,
        "schema": "hecton8.codex_token_usage.fast_refresh.v1",
        "generated_at_utc": now_utc.isoformat(),
        "generated_at_samara": now_local.isoformat(),
        "evidence_class": "FAST_INCREMENTAL_LOCAL_CODEX_JSONL_AND_FILESYSTEM",
        "fast_refresh_base_report": str(previous_path),
        "fast_refresh_base_mode": previous_snapshot_mode,
        "fast_refresh_cutoff_utc": cutoff.isoformat(),
        "fast_refresh_changed_jsonl_files_scanned": len(changed_files),
        "fast_refresh_hourly_jsonl_files_scanned": len(changed_files),
        "fast_refresh_increment_events_after_cutoff": increment_events_after_cutoff,
        "fast_refresh_long_context_increment_events_after_cutoff": long_context_increment_events_after_cutoff,
        "file_count": len(files),
        "sessions_with_usage": int(previous.get("sessions_with_usage", 0) or 0) + session_delta,
        "parse_errors_increment_pass": parse_errors,
        "last_selected_timestamp_utc": max_event_ts.isoformat() if max_event_ts else previous.get("last_selected_timestamp_utc"),
        "totals": total,
        "uncached_input_tokens": uncached_input,
        "cache_ratio": total["cached_input_tokens"] / max(1, total["input_tokens"]),
        "output_ratio": total["output_tokens"] / max(1, total["total_tokens"]),
        "reasoning_output_ratio_of_output": total["reasoning_output_tokens"] / max(1, total["output_tokens"]),
        "layperson_scale": layperson_scale,
        "pricing": pricing,
        "pricing_upper_bound_no_cache_usd": upper_no_cache,
        "primary_price_key": PRIMARY_PRICE_KEY,
        "primary_price_label": "gpt-5.5 standard under-272K-input API-equivalent",
        "pricing_context_rules": {
            "base_rate_note": "GPT-5.5 base API-equivalent uses input $5.00, cached input $0.50, output $30.00 per 1M tokens.",
            "long_context_trigger_input_tokens": LONG_CONTEXT_INPUT_TOKEN_TRIGGER,
            "long_context_surcharge_note": "Official pricing applies 2x input and 1.5x output when prompts exceed 272K input tokens. Local Codex JSONL does not expose provider-side per-request context classification, so this report adds an upper-bound sensitivity instead of pretending exact surcharge billing.",
            "regional_uplift_note": "Some regions can add about 10 percent. Local JSONL does not expose billing region, so this is a sensitivity row only.",
            "gpt_5_5_long_context_upper_bound_usd": long_context_row["total_cost_usd"],
            "gpt_5_5_long_context_upper_bound_delta_usd": long_context_delta,
            "gpt_5_5_regional_10pct_usd": regional_row["total_cost_usd"],
            "gpt_5_5_regional_10pct_delta_usd": regional_delta,
            "gpt_5_5_long_context_regional_10pct_upper_bound_usd": long_context_regional_upper,
            "post_cutoff_long_context_event_rule": "Lower-bound detector for post-cutoff positive delta events whose delta input_tokens exceeds 272000. Provider-side exact long-context classification remains unavailable in local JSONL; zero detected events is not proof of zero all-time surcharge.",
            "post_cutoff_long_context_event_evidence_class": "LOCAL_JSONL_DELTA_LOWER_BOUND_NOT_PROVIDER_INVOICE_CLASSIFICATION",
            "post_cutoff_long_context_event_count": long_context_increment_events_after_cutoff,
            "post_cutoff_long_context_event_usage": long_context_delta_usage,
            "post_cutoff_long_context_event_base_usd": post_cutoff_long_context_base_cost,
            "post_cutoff_long_context_event_surcharge_delta_usd": post_cutoff_long_context_surcharge_cost,
            "post_cutoff_base_delta_cost_usd": base_delta_cost,
        },
        "daily": {key: value for key, value in sorted(daily.items())},
        "hourly": {key: value for key, value in sorted(hourly.items())},
        "weekly": {key: value for key, value in sorted(weekly.items())},
        "monthly": {key: value for key, value in sorted(monthly.items())},
        "daily_gpt_5_5_standard_costs_usd": usage_map_costs(daily, PRICING[PRIMARY_PRICE_KEY]),
        "hourly_gpt_5_5_standard_costs_usd": usage_map_costs(hourly, PRICING[PRIMARY_PRICE_KEY]),
        "weekly_gpt_5_5_standard_costs_usd": usage_map_costs(weekly, PRICING[PRIMARY_PRICE_KEY]),
        "monthly_gpt_5_5_standard_costs_usd": usage_map_costs(monthly, PRICING[PRIMARY_PRICE_KEY]),
        "loc": loc or previous.get("loc") or {},
        "scope_economics": scope_economics or previous.get("scope_economics") or {},
        "pricing_sources": [
            f"https://developers.openai.com/api/docs/pricing checked {REPORT_DATE}; GPT-5.5 public API-equivalent rates used",
            f"https://developers.openai.com/api/docs/models/gpt-5.5 checked {REPORT_DATE}; long-context surcharge over 272K input is represented as a separate sensitivity, not exact invoice proof",
            f"https://developers.openai.com/api/docs/guides/prompt-caching checked {REPORT_DATE}; cached input is priced separately",
            f"https://developers.openai.com/api/docs/guides/reasoning checked {REPORT_DATE}; reasoning tokens remain output-billed",
            "All dollar values are API-equivalent estimates, not local invoice proof",
        ],
        "previous_snapshot_delta": {
            "previous_report_path": str(previous_path),
            "previous_snapshot_mode": previous_snapshot_mode,
            "previous_generated_at_samara": previous.get("generated_at_samara"),
            "elapsed_hours": elapsed_hours,
            "file_count_delta": file_delta,
            "sessions_with_usage_delta": session_delta,
            "totals_delta": delta_total,
            "gpt_5_5_standard_cost_usd_delta": primary_delta,
            "gpt_5_5_priority_cost_usd_delta": priority_delta,
            "gpt_5_3_codex_standard_cost_usd_delta": codex_delta,
            "primary_code_lines_delta": code_lines_delta,
            "primary_code_characters_delta": code_chars_delta,
            "velocity": velocity,
        },
    }
    return report


def write_reports(report):
    REPORT_JSON.parent.mkdir(parents=True, exist_ok=True)
    REPORT_JSON.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    total = report["totals"]
    primary = report["pricing"][PRIMARY_PRICE_KEY]
    pricing_context = report["pricing_context_rules"]
    scale = report["layperson_scale"]
    scale_total = scale["all_time"]
    scale_delta = scale["since_previous_snapshot"]
    change = report["previous_snapshot_delta"]
    velocity = change["velocity"]
    lines = [
        f"# TOKEN USAGE AUDIT FAST REFRESH {REPORT_DATE}",
        "",
        f"Generated UTC: {report['generated_at_utc']}",
        f"Generated Samara: {report['generated_at_samara']}",
        "Evidence class: FAST_INCREMENTAL_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Previous all-time snapshot plus post-cutoff JSONL deltas. Not billing-provider proof.",
        "",
        "## Totals",
        "",
        "| Metric | Value |",
        "|---|---:|",
        f"| file_count | {fmt_int(report['file_count'])} |",
        f"| sessions_with_usage | {fmt_int(report['sessions_with_usage'])} |",
        f"| input_tokens | {fmt_int(total['input_tokens'])} |",
        f"| cached_input_tokens | {fmt_int(total['cached_input_tokens'])} |",
        f"| output_tokens | {fmt_int(total['output_tokens'])} |",
        f"| reasoning_output_tokens | {fmt_int(total['reasoning_output_tokens'])} |",
        f"| total_tokens | {fmt_int(total['total_tokens'])} |",
        f"| GPT-5.5 standard under-272K API-equivalent | {fmt_money(primary['total_cost_usd'])} |",
        f"| GPT-5.5 long-context sensitivity upper bound | {fmt_money(pricing_context['gpt_5_5_long_context_upper_bound_usd'])} |",
        f"| GPT-5.5 long-context + regional sensitivity upper bound | {fmt_money(pricing_context['gpt_5_5_long_context_regional_10pct_upper_bound_usd'])} |",
        f"| GPT-5.5 regional +10% sensitivity | {fmt_money(pricing_context['gpt_5_5_regional_10pct_usd'])} |",
        "",
        "## Scale For Non-Specialists",
        "",
        "These are communication-scale analogies, not billing math. Assumption: 1 token is roughly 0.75 English words; code and Russian text vary.",
        "",
        "| Metric | Value |",
        "|---|---:|",
        f"| all-time approximate words | {fmt_int(scale_total['approx_words'])} |",
        f"| all-time 500-word printed pages | {fmt_int(scale_total['approx_printed_pages_500_words'])} |",
        f"| all-time 80k-word books | {fmt_int(scale_total['approx_80k_word_books'])} |",
        f"| continuous reading at 250 wpm | {fmt_float(scale_total['continuous_reading_years_at_250_wpm'])} years |",
        f"| 8h/day reading at 250 wpm | {fmt_float(scale_total['workday_reading_years_at_250_wpm'])} years |",
        f"| all-time $60 game equivalents | {fmt_float(scale_total['equivalent_60_usd_games'])} |",
        f"| all-time $2k workstation equivalents | {fmt_float(scale_total['equivalent_2000_usd_workstations'])} |",
        f"| cached input share | {fmt_float(scale_total['cache_ratio_percent'])}% |",
        f"| since previous snapshot approximate words | {fmt_int(scale_delta['approx_words'])} |",
        f"| since previous snapshot 500-word pages | {fmt_int(scale_delta['approx_printed_pages_500_words'])} |",
        f"| current burn approximate words / second | {fmt_float(scale_delta['approx_words_per_second'])} |",
        f"| current burn pages / hour | {fmt_float(scale_delta['approx_pages_per_hour'])} |",
        f"| current burn GPT-5.5 standard $ / day | {fmt_money(scale_delta['gpt_5_5_standard_usd_per_day_at_current_velocity'])} |",
        "",
        "## Increment Since Previous Snapshot",
        "",
        f"Previous report: `{change['previous_report_path']}`",
        f"Previous snapshot mode: `{change['previous_snapshot_mode']}`",
        f"Cutoff UTC: `{report['fast_refresh_cutoff_utc']}`",
        f"Changed JSONL files scanned: {fmt_int(report['fast_refresh_changed_jsonl_files_scanned'])}",
        f"Increment events after cutoff: {fmt_int(report['fast_refresh_increment_events_after_cutoff'])}",
        f"Post-cutoff long-context delta events detected (lower-bound): {fmt_int(report['fast_refresh_long_context_increment_events_after_cutoff'])}",
        "",
        "| Metric | Delta |",
        "|---|---:|",
        f"| total_tokens | {fmt_int(change['totals_delta']['total_tokens'])} |",
        f"| input_tokens | {fmt_int(change['totals_delta']['input_tokens'])} |",
        f"| cached_input_tokens | {fmt_int(change['totals_delta']['cached_input_tokens'])} |",
        f"| output_tokens | {fmt_int(change['totals_delta']['output_tokens'])} |",
        f"| reasoning_output_tokens | {fmt_int(change['totals_delta']['reasoning_output_tokens'])} |",
        f"| GPT-5.5 standard $ | {fmt_money(change['gpt_5_5_standard_cost_usd_delta'])} |",
        f"| primary C# lines | {fmt_int(change['primary_code_lines_delta'])} |",
        "",
        "## Velocity",
        "",
        "| Metric | Value |",
        "|---|---:|",
        f"| total tokens / hour | {velocity['total_tokens_per_hour']:,.2f} |",
        f"| total tokens / second | {velocity['total_tokens_per_second']:,.2f} |",
        f"| GPT-5.5 standard $ / hour | {fmt_money(velocity['gpt_5_5_standard_usd_per_hour'])} |",
        f"| primary C# lines / hour | {velocity['primary_code_lines_per_hour']:,.2f} |",
        f"| tokens / net primary C# line | {velocity['tokens_per_net_primary_code_line']:,.2f} |" if velocity["tokens_per_net_primary_code_line"] is not None else "| tokens / net primary C# line | n/a |",
        f"| post-cutoff detected long-context surcharge delta (lower-bound) | {fmt_money(pricing_context['post_cutoff_long_context_event_surcharge_delta_usd'])} |",
        "",
        "## Residual Risk",
        "",
        "- This fast refresh is exact for post-cutoff positive JSONL deltas in modified session files.",
        "- It inherits older all-time dimensions from the previous full snapshot.",
        "- Local JSONL still lacks billing SKU, invoice id, enterprise discount, and subscription route.",
        "- Local JSONL does not expose provider-side per-request long-context surcharge classification; the detected post-cutoff long-context counter is a lower-bound heuristic and the report includes a separate upper-bound sensitivity.",
    ]
    REPORT_MD.write_text("\n".join(lines) + "\n", encoding="utf-8-sig")
    ledger = [
        "# Codex Token Usage Ledger",
        "",
        f"Date: {datetime.datetime.fromisoformat(report['generated_at_samara']).strftime('%Y-%m-%d %H:%M')} Europe/Samara",
        "Status: CURRENT FAST INCREMENTAL LOCAL TELEMETRY SNAPSHOT / NOT PROJECT ENGINEERING AUTHORITY",
        "",
        f"Current report: `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_{REPORT_DATE}.md`.",
        "",
        "| Metric | Value |",
        "|---|---:|",
        f"| total_tokens | {fmt_int(total['total_tokens'])} |",
        f"| input_tokens | {fmt_int(total['input_tokens'])} |",
        f"| cached_input_tokens | {fmt_int(total['cached_input_tokens'])} |",
        f"| output_tokens | {fmt_int(total['output_tokens'])} |",
        f"| reasoning_output_tokens | {fmt_int(total['reasoning_output_tokens'])} |",
        f"| GPT-5.5 standard under-272K API-equivalent | {fmt_money(primary['total_cost_usd'])} |",
        f"| GPT-5.5 long-context sensitivity upper bound | {fmt_money(pricing_context['gpt_5_5_long_context_upper_bound_usd'])} |",
        f"| GPT-5.5 long-context + regional sensitivity upper bound | {fmt_money(pricing_context['gpt_5_5_long_context_regional_10pct_upper_bound_usd'])} |",
        f"| GPT-5.5 regional +10% sensitivity | {fmt_money(pricing_context['gpt_5_5_regional_10pct_usd'])} |",
        f"| approx all-time 500-word pages | {fmt_int(scale_total['approx_printed_pages_500_words'])} |",
        f"| approx all-time 80k-word books | {fmt_int(scale_total['approx_80k_word_books'])} |",
        f"| approx all-time continuous reading years at 250 wpm | {fmt_float(scale_total['continuous_reading_years_at_250_wpm'])} |",
        f"| cached input share | {fmt_float(scale_total['cache_ratio_percent'])}% |",
        f"| total tokens / hour since previous snapshot | {velocity['total_tokens_per_hour']:,.2f} |",
        f"| approx pages / hour since previous snapshot | {fmt_float(scale_delta['approx_pages_per_hour'])} |",
        f"| GPT-5.5 standard $ / hour since previous snapshot | {fmt_money(velocity['gpt_5_5_standard_usd_per_hour'])} |",
        "",
        "Evidence: local Codex JSONL plus official OpenAI pricing/cache/reasoning docs. Not invoice proof.",
    ]
    LEDGER.write_text("\n".join(ledger) + "\n", encoding="utf-8-sig")


def main():
    report = build_report()
    write_reports(report)
    velocity = report["previous_snapshot_delta"]["velocity"]
    print(json.dumps({
        "report_json": str(REPORT_JSON),
        "report_md": str(REPORT_MD),
        "ledger": str(LEDGER),
        "total_tokens": report["totals"]["total_tokens"],
        "delta_tokens": report["previous_snapshot_delta"]["totals_delta"]["total_tokens"],
        "tokens_per_hour": velocity["total_tokens_per_hour"],
        "gpt_5_5_standard_cost": report["pricing"][PRIMARY_PRICE_KEY]["total_cost_usd"],
        "chart_hour_buckets": len(report.get("hourly") or {}),
        "changed_jsonl_files_scanned": report["fast_refresh_changed_jsonl_files_scanned"],
    }, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()

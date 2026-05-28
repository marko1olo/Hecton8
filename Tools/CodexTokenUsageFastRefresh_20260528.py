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

PRICING = {
    "gpt-5.3-codex_standard_api_equivalent": {"input": 1.75, "cached_input": 0.175, "output": 14.0},
    "gpt-5.3-codex_priority_api_equivalent": {"input": 3.5, "cached_input": 0.35, "output": 28.0},
    "gpt-5.4_standard_short_context_equivalent": {"input": 2.5, "cached_input": 0.25, "output": 15.0},
    "gpt-5.5_standard_short_context_equivalent": {"input": 5.0, "cached_input": 0.5, "output": 30.0},
    "gpt-5.5_batch_short_context_equivalent": {"input": 2.5, "cached_input": 0.25, "output": 15.0},
    "gpt-5.5_flex_short_context_equivalent": {"input": 2.5, "cached_input": 0.25, "output": 15.0},
    "gpt-5.5_priority_short_context_equivalent": {"input": 12.5, "cached_input": 1.25, "output": 75.0},
    "gpt-5.4_mini_standard_equivalent": {"input": 0.75, "cached_input": 0.075, "output": 4.5},
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


def find_previous_report():
    current = datetime.date.fromisoformat(REPORT_DATE)
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
    hourly_files = []
    for root, path, size, mtime in files:
        mtime_utc = datetime.datetime.fromtimestamp(mtime, UTC)
        if mtime_utc >= cutoff - datetime.timedelta(minutes=10):
            changed_files.append((root, path, size, mtime))
        if mtime_utc >= hourly_cutoff - datetime.timedelta(minutes=10):
            hourly_files.append((root, path, size, mtime))

    delta_total = zero_usage()
    hourly = defaultdict(zero_usage)
    daily = merge_period_map(previous.get("daily") or {})
    weekly = merge_period_map(previous.get("weekly") or {})
    monthly = merge_period_map(previous.get("monthly") or {})
    new_session_ids = set()
    parse_errors = 0
    max_event_ts = cutoff
    increment_events_after_cutoff = 0

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
            max_event_ts = max(max_event_ts, ts)
            add_usage(delta_total, delta)
            local = ts.astimezone(SAMARA)
            add_usage(daily[local.date().isoformat()], delta)
            add_usage(weekly[week_key(local)], delta)
            add_usage(monthly[f"{local.year}-{local.month:02d}"], delta)

    seen_hourly_paths = {str(path).lower() for _root, path, _size, _mtime in changed_files}
    for _root, path, _size, _mtime in hourly_files:
        increments, errors = audit.read_increment_events(path)
        if str(path).lower() not in seen_hourly_paths:
            parse_errors += errors
        for ts, delta, _model, _effort in increments:
            if ts < hourly_cutoff:
                continue
            local = ts.astimezone(SAMARA)
            add_usage(hourly[local.strftime("%Y-%m-%d %H:00")], delta)

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

    report = {
        **previous,
        "schema": "hecton8.codex_token_usage.fast_refresh.v1",
        "generated_at_utc": now_utc.isoformat(),
        "generated_at_samara": now_local.isoformat(),
        "evidence_class": "FAST_INCREMENTAL_LOCAL_CODEX_JSONL_AND_FILESYSTEM",
        "fast_refresh_base_report": str(previous_path),
        "fast_refresh_cutoff_utc": cutoff.isoformat(),
        "fast_refresh_changed_jsonl_files_scanned": len(changed_files),
        "fast_refresh_hourly_jsonl_files_scanned": len(hourly_files),
        "fast_refresh_increment_events_after_cutoff": increment_events_after_cutoff,
        "file_count": len(files),
        "sessions_with_usage": int(previous.get("sessions_with_usage", 0) or 0) + session_delta,
        "parse_errors_increment_pass": parse_errors,
        "last_selected_timestamp_utc": max_event_ts.isoformat() if max_event_ts else previous.get("last_selected_timestamp_utc"),
        "totals": total,
        "uncached_input_tokens": uncached_input,
        "cache_ratio": total["cached_input_tokens"] / max(1, total["input_tokens"]),
        "output_ratio": total["output_tokens"] / max(1, total["total_tokens"]),
        "reasoning_output_ratio_of_output": total["reasoning_output_tokens"] / max(1, total["output_tokens"]),
        "pricing": pricing,
        "pricing_upper_bound_no_cache_usd": upper_no_cache,
        "primary_price_key": PRIMARY_PRICE_KEY,
        "primary_price_label": "gpt-5.5 standard short-context API-equivalent",
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
            f"https://developers.openai.com/api/docs/guides/prompt-caching checked {REPORT_DATE}; cached input is priced separately",
            f"https://developers.openai.com/api/docs/guides/reasoning checked {REPORT_DATE}; reasoning tokens remain output-billed",
            "All dollar values are API-equivalent estimates, not local invoice proof",
        ],
        "previous_snapshot_delta": {
            "previous_report_path": str(previous_path),
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
        f"| GPT-5.5 standard API-equivalent | {fmt_money(primary['total_cost_usd'])} |",
        "",
        "## Increment Since Previous Snapshot",
        "",
        f"Previous report: `{change['previous_report_path']}`",
        f"Cutoff UTC: `{report['fast_refresh_cutoff_utc']}`",
        f"Changed JSONL files scanned: {fmt_int(report['fast_refresh_changed_jsonl_files_scanned'])}",
        f"Increment events after cutoff: {fmt_int(report['fast_refresh_increment_events_after_cutoff'])}",
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
        "",
        "## Residual Risk",
        "",
        "- This fast refresh is exact for post-cutoff positive JSONL deltas in modified session files.",
        "- It inherits older all-time dimensions from the previous full snapshot.",
        "- Local JSONL still lacks billing SKU, invoice id, enterprise discount, and subscription route.",
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
        f"| GPT-5.5 standard API-equivalent | {fmt_money(primary['total_cost_usd'])} |",
        f"| total tokens / hour since previous snapshot | {velocity['total_tokens_per_hour']:,.2f} |",
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

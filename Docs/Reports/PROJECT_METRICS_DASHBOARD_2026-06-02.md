# Project Metrics Dashboard 2026-06-02

Generated Samara: `2026-06-02T16:15:59.485466+04:00`
Evidence class: static local Codex JSONL, git history, and filesystem scan. Token cost is API-equivalent, not invoice proof.

## Headline

| Metric | Value |
|---|---:|
| Total tokens | 129,631,981,374 |
| Input tokens | 129,184,482,782 |
| Cached input tokens | 124,245,990,784 |
| Output tokens | 446,464,992 |
| Reasoning output tokens | 137,637,121 |
| Sessions with usage | 3,080 |
| GPT-5.5 standard API-equivalent total | $100,209.41 |
| GPT-5.5 long-context sensitivity upper bound | $193,721.84 |
| GPT-5.5 long-context + regional upper bound | $213,094.02 |
| GPT-5.5 regional +10% sensitivity | $110,230.35 |
| Post-cutoff detected long-context delta events (lower-bound) | 0 |
| Post-cutoff detected long-context surcharge delta (lower-bound) | $0.00 |
| Post-cutoff long-context evidence class | `LOCAL_JSONL_DELTA_LOWER_BOUND_NOT_PROVIDER_INVOICE_CLASSIFICATION` |
| Tokens/hour since previous snapshot | 81,199,699 |
| GPT-5.5 standard USD/hour since previous snapshot | $61.96 |
| Primary C# LOC/hour since previous snapshot | 176.78 |
| Long-range chart windows | 7d, 14d, 30d, 60d, 90d |
| Chart count | 112 |

## Chart Index

- [Hourly total tokens - last 96h](#hourly_total_tokens_last_96h)
- [Hourly GPT-5.5 standard cost - last 96h](#hourly_cost_last_96h)
- [Hourly input/output stack - last 96h](#hourly_io_stack_last_96h)
- [Hourly output and reasoning output - last 96h](#hourly_output_reasoning_last_96h)
- [Hourly cache/output/reasoning ratios - last 96h](#hourly_ratios_last_96h)
- [Hourly cache discount saved - last 96h](#hourly_cache_savings_last_96h)
- [Hourly actual vs no-cache GPT-5.5 cost - last 96h](#hourly_actual_vs_no_cache_cost_last_96h)
- [Hourly effective USD per 1M total tokens - last 96h](#hourly_effective_cost_per_1m_last_96h)
- [Hourly output cost share - last 96h](#hourly_output_cost_share_last_96h)
- [Hourly human-scale burn - last 96h](#hourly_printed_pages_last_96h)
- [Hourly cached-to-uncached input ratio - last 96h](#hourly_cached_to_uncached_ratio_last_96h)
- [Token heatmap by day/hour - last 96h](#hourly_token_day_hour_heatmap_last_96h)
- [Token heatmap by weekday/hour - all available hours](#token_weekday_hour_heatmap_all)
- [Cost heatmap by weekday/hour - all available hours](#cost_weekday_hour_heatmap_all)
- [Daily total tokens - last 7 days](#daily_total_tokens_last_7d)
- [Daily GPT-5.5 standard cost - last 7 days](#daily_cost_last_7d)
- [Daily input/output stack - last 7 days](#daily_io_stack_last_7d)
- [Daily cache/output/reasoning ratios - last 7 days](#daily_ratios_last_7d)
- [Daily cache discount saved - last 7 days](#daily_cache_savings_last_7d)
- [Daily effective USD per 1M total tokens - last 7 days](#daily_effective_cost_per_1m_last_7d)
- [Daily total tokens - last 14 days](#daily_total_tokens_last_14d)
- [Daily GPT-5.5 standard cost - last 14 days](#daily_cost_last_14d)
- [Daily input/output stack - last 14 days](#daily_io_stack_last_14d)
- [Daily cache/output/reasoning ratios - last 14 days](#daily_ratios_last_14d)
- [Daily cache discount saved - last 14 days](#daily_cache_savings_last_14d)
- [Daily effective USD per 1M total tokens - last 14 days](#daily_effective_cost_per_1m_last_14d)
- [Daily total tokens - last 30 days](#daily_total_tokens_last_30d)
- [Daily GPT-5.5 standard cost - last 30 days](#daily_cost_last_30d)
- [Daily input/output stack - last 30 days](#daily_io_stack_last_30d)
- [Daily cache/output/reasoning ratios - last 30 days](#daily_ratios_last_30d)
- [Daily cache discount saved - last 30 days](#daily_cache_savings_last_30d)
- [Daily effective USD per 1M total tokens - last 30 days](#daily_effective_cost_per_1m_last_30d)
- [Daily total tokens - last 60 days](#daily_total_tokens_last_60d)
- [Daily GPT-5.5 standard cost - last 60 days](#daily_cost_last_60d)
- [Daily input/output stack - last 60 days](#daily_io_stack_last_60d)
- [Daily cache/output/reasoning ratios - last 60 days](#daily_ratios_last_60d)
- [Daily cache discount saved - last 60 days](#daily_cache_savings_last_60d)
- [Daily effective USD per 1M total tokens - last 60 days](#daily_effective_cost_per_1m_last_60d)
- [Daily total tokens - last 90 days](#daily_total_tokens_last_90d)
- [Daily GPT-5.5 standard cost - last 90 days](#daily_cost_last_90d)
- [Daily input/output stack - last 90 days](#daily_io_stack_last_90d)
- [Daily cache/output/reasoning ratios - last 90 days](#daily_ratios_last_90d)
- [Daily cache discount saved - last 90 days](#daily_cache_savings_last_90d)
- [Daily effective USD per 1M total tokens - last 90 days](#daily_effective_cost_per_1m_last_90d)
- [Daily total tokens](#daily_total_tokens)
- [Daily GPT-5.5 standard cost](#daily_cost)
- [Daily input/output stack](#daily_io_stack)
- [Daily output and reasoning output](#daily_output_reasoning)
- [Daily cache/output/reasoning ratios](#daily_ratios)
- [Daily cache discount saved](#daily_cache_savings)
- [Daily actual vs no-cache GPT-5.5 cost](#daily_actual_vs_no_cache_cost)
- [Daily effective USD per 1M total tokens](#daily_effective_cost_per_1m)
- [Daily output cost share](#daily_output_cost_share)
- [Daily human-scale burn](#daily_printed_pages)
- [Weekly total tokens](#weekly_total_tokens)
- [Weekly GPT-5.5 standard cost](#weekly_cost)
- [Weekly input/output stack](#weekly_io_stack)
- [Weekly output and reasoning output](#weekly_output_reasoning)
- [Weekly cache/output/reasoning ratios](#weekly_ratios)
- [Weekly cache discount saved](#weekly_cache_savings)
- [Weekly effective USD per 1M total tokens](#weekly_effective_cost_per_1m)
- [Weekly output cost share](#weekly_output_cost_share)
- [Top model+effort buckets by tokens](#model_effort_tokens_top20)
- [Top model+effort buckets by model-standard cost](#model_effort_cost_top20)
- [Top model+effort buckets by output tokens](#model_effort_output_top20)
- [Top model+effort buckets by reasoning output](#model_effort_reasoning_top20)
- [Top priced model+effort buckets by effective USD per 1M tokens](#model_effort_effective_cost_per_1m_top20)
- [Top sessions by total tokens](#top_sessions_total_tokens)
- [Top sessions by output tokens](#top_sessions_output_tokens)
- [Top sessions by reasoning output tokens](#top_sessions_reasoning_tokens)
- [Top sessions by GPT-5.5 standard cost](#top_sessions_cost)
- [Top CWD buckets by total tokens](#top_cwd_total_tokens)
- [Top telemetry sources by total tokens](#top_source_total_tokens)
- [Top originators by total tokens](#top_originator_total_tokens)
- [Top plan buckets by total tokens](#top_plan_total_tokens)
- [Top CLI versions by total tokens](#top_cli_total_tokens)
- [Current project lines by scope](#project_lines_by_scope)
- [Token density by current source scope](#token_density_by_scope)
- [GPT-5.5 standard cost per 1k current lines by scope](#cost_per_1k_lines_by_scope)
- [GPT-5.5 standard cost per 1k current characters by scope](#cost_per_1k_chars_by_scope)
- [Output tokens per 1k current characters by scope](#output_tokens_per_1k_chars_by_scope)
- [Project file counts by extension](#file_counts_by_extension)
- [Project bytes by extension](#bytes_by_extension)
- [Project text lines by extension](#lines_by_extension)
- [Average bytes per file by extension](#avg_bytes_per_file_by_extension)
- [Average text lines per file by extension](#avg_lines_per_file_by_extension)
- [Project file counts by root folder](#file_counts_by_root)
- [Documentation and audit artifact counts](#docs_artifact_counts)
- [Largest project files by bytes](#largest_files_by_bytes)
- [Largest text files by lines](#largest_text_files_by_lines)
- [Git commits by day since 2026-04-01](#git_commits_by_day)
- [Git churn by day since 2026-04-01](#git_churn_by_day)
- [Git insertions/deletions by day](#git_insertions_deletions_by_day)
- [Git files changed by day since 2026-04-01](#git_files_changed_by_day)
- [Git net lines by day since 2026-04-01](#git_net_lines_by_day)
- [Git churn per commit by day](#git_churn_per_commit_by_day)
- [Git commits by ISO week](#git_commits_by_week)
- [Git churn by ISO week](#git_churn_by_week)
- [Git files changed by ISO week](#git_files_changed_by_week)
- [Git net lines by ISO week](#git_net_lines_by_week)
- [Git churn per commit by ISO week](#git_churn_per_commit_by_week)
- [Git commit heatmap by Samara weekday/hour](#git_commit_weekday_hour_heatmap)
- [Git churn heatmap by Samara weekday/hour](#git_churn_weekday_hour_heatmap)
- [Daily tokens per committed changed line](#daily_tokens_per_git_changed_line)
- [Daily GPT-5.5 cost per committed changed line](#daily_cost_per_git_changed_line)
- [Daily tokens vs git churn](#daily_tokens_vs_git_churn)
- [Daily GPT-5.5 cost vs git churn](#daily_cost_vs_git_churn)
- [Current snapshot token velocity by class](#current_snapshot_token_velocity)
- [Current snapshot API-equivalent money velocity](#current_snapshot_money_velocity)
- [Current snapshot code and density velocity](#current_snapshot_code_velocity)
- [All-time token scale for non-specialists](#layperson_all_time_scale)
- [Current burn-rate scale for non-specialists](#layperson_current_burn_rate)

## Charts

### hourly_total_tokens_last_96h

![Hourly total tokens - last 96h](MetricCharts/2026-06-02/hourly_total_tokens_last_96h.png)

### hourly_cost_last_96h

![Hourly GPT-5.5 standard cost - last 96h](MetricCharts/2026-06-02/hourly_cost_last_96h.png)

### hourly_io_stack_last_96h

![Hourly input/output stack - last 96h](MetricCharts/2026-06-02/hourly_io_stack_last_96h.png)

### hourly_output_reasoning_last_96h

![Hourly output and reasoning output - last 96h](MetricCharts/2026-06-02/hourly_output_reasoning_last_96h.png)

### hourly_ratios_last_96h

![Hourly cache/output/reasoning ratios - last 96h](MetricCharts/2026-06-02/hourly_ratios_last_96h.png)

### hourly_cache_savings_last_96h

![Hourly cache discount saved - last 96h](MetricCharts/2026-06-02/hourly_cache_savings_last_96h.png)

### hourly_actual_vs_no_cache_cost_last_96h

![Hourly actual vs no-cache GPT-5.5 cost - last 96h](MetricCharts/2026-06-02/hourly_actual_vs_no_cache_cost_last_96h.png)

### hourly_effective_cost_per_1m_last_96h

![Hourly effective USD per 1M total tokens - last 96h](MetricCharts/2026-06-02/hourly_effective_cost_per_1m_last_96h.png)

### hourly_output_cost_share_last_96h

![Hourly output cost share - last 96h](MetricCharts/2026-06-02/hourly_output_cost_share_last_96h.png)

### hourly_printed_pages_last_96h

![Hourly human-scale burn - last 96h](MetricCharts/2026-06-02/hourly_printed_pages_last_96h.png)

### hourly_cached_to_uncached_ratio_last_96h

![Hourly cached-to-uncached input ratio - last 96h](MetricCharts/2026-06-02/hourly_cached_to_uncached_ratio_last_96h.png)

### hourly_token_day_hour_heatmap_last_96h

![Token heatmap by day/hour - last 96h](MetricCharts/2026-06-02/hourly_token_day_hour_heatmap_last_96h.png)

Evidence note: Last-96-hour total token pressure by local Samara day and hour.

### token_weekday_hour_heatmap_all

![Token heatmap by weekday/hour - all available hours](MetricCharts/2026-06-02/token_weekday_hour_heatmap_all.png)

Evidence note: All available hourly token pressure aggregated by Samara weekday and hour.

### cost_weekday_hour_heatmap_all

![Cost heatmap by weekday/hour - all available hours](MetricCharts/2026-06-02/cost_weekday_hour_heatmap_all.png)

Evidence note: All available hourly GPT-5.5 API-equivalent cost aggregated by Samara weekday and hour.

### daily_total_tokens_last_7d

![Daily total tokens - last 7 days](MetricCharts/2026-06-02/daily_total_tokens_last_7d.png)

Evidence note: Long-range token consumption window covering the last 7 calendar days with start/end/peak labels.

### daily_cost_last_7d

![Daily GPT-5.5 standard cost - last 7 days](MetricCharts/2026-06-02/daily_cost_last_7d.png)

Evidence note: Long-range GPT-5.5 API-equivalent cost window covering the last 7 calendar days with start/end/peak labels.

### daily_io_stack_last_7d

![Daily input/output stack - last 7 days](MetricCharts/2026-06-02/daily_io_stack_last_7d.png)

Evidence note: Long-range daily token composition window covering the last 7 calendar days.

### daily_ratios_last_7d

![Daily cache/output/reasoning ratios - last 7 days](MetricCharts/2026-06-02/daily_ratios_last_7d.png)

Evidence note: Long-range daily quality-of-usage ratios covering the last 7 calendar days.

### daily_cache_savings_last_7d

![Daily cache discount saved - last 7 days](MetricCharts/2026-06-02/daily_cache_savings_last_7d.png)

Evidence note: Long-range cache-discount value window covering the last 7 calendar days.

### daily_effective_cost_per_1m_last_7d

![Daily effective USD per 1M total tokens - last 7 days](MetricCharts/2026-06-02/daily_effective_cost_per_1m_last_7d.png)

Evidence note: Long-range effective blended token price window covering the last 7 calendar days.

### daily_total_tokens_last_14d

![Daily total tokens - last 14 days](MetricCharts/2026-06-02/daily_total_tokens_last_14d.png)

Evidence note: Long-range token consumption window covering the last 14 calendar days with start/end/peak labels.

### daily_cost_last_14d

![Daily GPT-5.5 standard cost - last 14 days](MetricCharts/2026-06-02/daily_cost_last_14d.png)

Evidence note: Long-range GPT-5.5 API-equivalent cost window covering the last 14 calendar days with start/end/peak labels.

### daily_io_stack_last_14d

![Daily input/output stack - last 14 days](MetricCharts/2026-06-02/daily_io_stack_last_14d.png)

Evidence note: Long-range daily token composition window covering the last 14 calendar days.

### daily_ratios_last_14d

![Daily cache/output/reasoning ratios - last 14 days](MetricCharts/2026-06-02/daily_ratios_last_14d.png)

Evidence note: Long-range daily quality-of-usage ratios covering the last 14 calendar days.

### daily_cache_savings_last_14d

![Daily cache discount saved - last 14 days](MetricCharts/2026-06-02/daily_cache_savings_last_14d.png)

Evidence note: Long-range cache-discount value window covering the last 14 calendar days.

### daily_effective_cost_per_1m_last_14d

![Daily effective USD per 1M total tokens - last 14 days](MetricCharts/2026-06-02/daily_effective_cost_per_1m_last_14d.png)

Evidence note: Long-range effective blended token price window covering the last 14 calendar days.

### daily_total_tokens_last_30d

![Daily total tokens - last 30 days](MetricCharts/2026-06-02/daily_total_tokens_last_30d.png)

Evidence note: Long-range token consumption window covering the last 30 calendar days with start/end/peak labels.

### daily_cost_last_30d

![Daily GPT-5.5 standard cost - last 30 days](MetricCharts/2026-06-02/daily_cost_last_30d.png)

Evidence note: Long-range GPT-5.5 API-equivalent cost window covering the last 30 calendar days with start/end/peak labels.

### daily_io_stack_last_30d

![Daily input/output stack - last 30 days](MetricCharts/2026-06-02/daily_io_stack_last_30d.png)

Evidence note: Long-range daily token composition window covering the last 30 calendar days.

### daily_ratios_last_30d

![Daily cache/output/reasoning ratios - last 30 days](MetricCharts/2026-06-02/daily_ratios_last_30d.png)

Evidence note: Long-range daily quality-of-usage ratios covering the last 30 calendar days.

### daily_cache_savings_last_30d

![Daily cache discount saved - last 30 days](MetricCharts/2026-06-02/daily_cache_savings_last_30d.png)

Evidence note: Long-range cache-discount value window covering the last 30 calendar days.

### daily_effective_cost_per_1m_last_30d

![Daily effective USD per 1M total tokens - last 30 days](MetricCharts/2026-06-02/daily_effective_cost_per_1m_last_30d.png)

Evidence note: Long-range effective blended token price window covering the last 30 calendar days.

### daily_total_tokens_last_60d

![Daily total tokens - last 60 days](MetricCharts/2026-06-02/daily_total_tokens_last_60d.png)

Evidence note: Long-range token consumption window covering the last 60 calendar days with start/end/peak labels.

### daily_cost_last_60d

![Daily GPT-5.5 standard cost - last 60 days](MetricCharts/2026-06-02/daily_cost_last_60d.png)

Evidence note: Long-range GPT-5.5 API-equivalent cost window covering the last 60 calendar days with start/end/peak labels.

### daily_io_stack_last_60d

![Daily input/output stack - last 60 days](MetricCharts/2026-06-02/daily_io_stack_last_60d.png)

Evidence note: Long-range daily token composition window covering the last 60 calendar days.

### daily_ratios_last_60d

![Daily cache/output/reasoning ratios - last 60 days](MetricCharts/2026-06-02/daily_ratios_last_60d.png)

Evidence note: Long-range daily quality-of-usage ratios covering the last 60 calendar days.

### daily_cache_savings_last_60d

![Daily cache discount saved - last 60 days](MetricCharts/2026-06-02/daily_cache_savings_last_60d.png)

Evidence note: Long-range cache-discount value window covering the last 60 calendar days.

### daily_effective_cost_per_1m_last_60d

![Daily effective USD per 1M total tokens - last 60 days](MetricCharts/2026-06-02/daily_effective_cost_per_1m_last_60d.png)

Evidence note: Long-range effective blended token price window covering the last 60 calendar days.

### daily_total_tokens_last_90d

![Daily total tokens - last 90 days](MetricCharts/2026-06-02/daily_total_tokens_last_90d.png)

Evidence note: Long-range token consumption window covering the last 90 calendar days with start/end/peak labels.

### daily_cost_last_90d

![Daily GPT-5.5 standard cost - last 90 days](MetricCharts/2026-06-02/daily_cost_last_90d.png)

Evidence note: Long-range GPT-5.5 API-equivalent cost window covering the last 90 calendar days with start/end/peak labels.

### daily_io_stack_last_90d

![Daily input/output stack - last 90 days](MetricCharts/2026-06-02/daily_io_stack_last_90d.png)

Evidence note: Long-range daily token composition window covering the last 90 calendar days.

### daily_ratios_last_90d

![Daily cache/output/reasoning ratios - last 90 days](MetricCharts/2026-06-02/daily_ratios_last_90d.png)

Evidence note: Long-range daily quality-of-usage ratios covering the last 90 calendar days.

### daily_cache_savings_last_90d

![Daily cache discount saved - last 90 days](MetricCharts/2026-06-02/daily_cache_savings_last_90d.png)

Evidence note: Long-range cache-discount value window covering the last 90 calendar days.

### daily_effective_cost_per_1m_last_90d

![Daily effective USD per 1M total tokens - last 90 days](MetricCharts/2026-06-02/daily_effective_cost_per_1m_last_90d.png)

Evidence note: Long-range effective blended token price window covering the last 90 calendar days.

### daily_total_tokens

![Daily total tokens](MetricCharts/2026-06-02/daily_total_tokens.png)

### daily_cost

![Daily GPT-5.5 standard cost](MetricCharts/2026-06-02/daily_cost.png)

### daily_io_stack

![Daily input/output stack](MetricCharts/2026-06-02/daily_io_stack.png)

### daily_output_reasoning

![Daily output and reasoning output](MetricCharts/2026-06-02/daily_output_reasoning.png)

### daily_ratios

![Daily cache/output/reasoning ratios](MetricCharts/2026-06-02/daily_ratios.png)

### daily_cache_savings

![Daily cache discount saved](MetricCharts/2026-06-02/daily_cache_savings.png)

### daily_actual_vs_no_cache_cost

![Daily actual vs no-cache GPT-5.5 cost](MetricCharts/2026-06-02/daily_actual_vs_no_cache_cost.png)

### daily_effective_cost_per_1m

![Daily effective USD per 1M total tokens](MetricCharts/2026-06-02/daily_effective_cost_per_1m.png)

### daily_output_cost_share

![Daily output cost share](MetricCharts/2026-06-02/daily_output_cost_share.png)

### daily_printed_pages

![Daily human-scale burn](MetricCharts/2026-06-02/daily_printed_pages.png)

### weekly_total_tokens

![Weekly total tokens](MetricCharts/2026-06-02/weekly_total_tokens.png)

### weekly_cost

![Weekly GPT-5.5 standard cost](MetricCharts/2026-06-02/weekly_cost.png)

### weekly_io_stack

![Weekly input/output stack](MetricCharts/2026-06-02/weekly_io_stack.png)

### weekly_output_reasoning

![Weekly output and reasoning output](MetricCharts/2026-06-02/weekly_output_reasoning.png)

### weekly_ratios

![Weekly cache/output/reasoning ratios](MetricCharts/2026-06-02/weekly_ratios.png)

### weekly_cache_savings

![Weekly cache discount saved](MetricCharts/2026-06-02/weekly_cache_savings.png)

### weekly_effective_cost_per_1m

![Weekly effective USD per 1M total tokens](MetricCharts/2026-06-02/weekly_effective_cost_per_1m.png)

### weekly_output_cost_share

![Weekly output cost share](MetricCharts/2026-06-02/weekly_output_cost_share.png)

### model_effort_tokens_top20

![Top model+effort buckets by tokens](MetricCharts/2026-06-02/model_effort_tokens_top20.png)

### model_effort_cost_top20

![Top model+effort buckets by model-standard cost](MetricCharts/2026-06-02/model_effort_cost_top20.png)

### model_effort_output_top20

![Top model+effort buckets by output tokens](MetricCharts/2026-06-02/model_effort_output_top20.png)

### model_effort_reasoning_top20

![Top model+effort buckets by reasoning output](MetricCharts/2026-06-02/model_effort_reasoning_top20.png)

### model_effort_effective_cost_per_1m_top20

![Top priced model+effort buckets by effective USD per 1M tokens](MetricCharts/2026-06-02/model_effort_effective_cost_per_1m_top20.png)

### top_sessions_total_tokens

![Top sessions by total tokens](MetricCharts/2026-06-02/top_sessions_total_tokens.png)

### top_sessions_output_tokens

![Top sessions by output tokens](MetricCharts/2026-06-02/top_sessions_output_tokens.png)

### top_sessions_reasoning_tokens

![Top sessions by reasoning output tokens](MetricCharts/2026-06-02/top_sessions_reasoning_tokens.png)

### top_sessions_cost

![Top sessions by GPT-5.5 standard cost](MetricCharts/2026-06-02/top_sessions_cost.png)

### top_cwd_total_tokens

![Top CWD buckets by total tokens](MetricCharts/2026-06-02/top_cwd_total_tokens.png)

### top_source_total_tokens

![Top telemetry sources by total tokens](MetricCharts/2026-06-02/top_source_total_tokens.png)

### top_originator_total_tokens

![Top originators by total tokens](MetricCharts/2026-06-02/top_originator_total_tokens.png)

### top_plan_total_tokens

![Top plan buckets by total tokens](MetricCharts/2026-06-02/top_plan_total_tokens.png)

### top_cli_total_tokens

![Top CLI versions by total tokens](MetricCharts/2026-06-02/top_cli_total_tokens.png)

### project_lines_by_scope

![Current project lines by scope](MetricCharts/2026-06-02/project_lines_by_scope.png)

### token_density_by_scope

![Token density by current source scope](MetricCharts/2026-06-02/token_density_by_scope.png)

### cost_per_1k_lines_by_scope

![GPT-5.5 standard cost per 1k current lines by scope](MetricCharts/2026-06-02/cost_per_1k_lines_by_scope.png)

### cost_per_1k_chars_by_scope

![GPT-5.5 standard cost per 1k current characters by scope](MetricCharts/2026-06-02/cost_per_1k_chars_by_scope.png)

### output_tokens_per_1k_chars_by_scope

![Output tokens per 1k current characters by scope](MetricCharts/2026-06-02/output_tokens_per_1k_chars_by_scope.png)

### file_counts_by_extension

![Project file counts by extension](MetricCharts/2026-06-02/file_counts_by_extension.png)

### bytes_by_extension

![Project bytes by extension](MetricCharts/2026-06-02/bytes_by_extension.png)

### lines_by_extension

![Project text lines by extension](MetricCharts/2026-06-02/lines_by_extension.png)

### avg_bytes_per_file_by_extension

![Average bytes per file by extension](MetricCharts/2026-06-02/avg_bytes_per_file_by_extension.png)

### avg_lines_per_file_by_extension

![Average text lines per file by extension](MetricCharts/2026-06-02/avg_lines_per_file_by_extension.png)

### file_counts_by_root

![Project file counts by root folder](MetricCharts/2026-06-02/file_counts_by_root.png)

### docs_artifact_counts

![Documentation and audit artifact counts](MetricCharts/2026-06-02/docs_artifact_counts.png)

### largest_files_by_bytes

![Largest project files by bytes](MetricCharts/2026-06-02/largest_files_by_bytes.png)

### largest_text_files_by_lines

![Largest text files by lines](MetricCharts/2026-06-02/largest_text_files_by_lines.png)

### git_commits_by_day

![Git commits by day since 2026-04-01](MetricCharts/2026-06-02/git_commits_by_day.png)

### git_churn_by_day

![Git churn by day since 2026-04-01](MetricCharts/2026-06-02/git_churn_by_day.png)

### git_insertions_deletions_by_day

![Git insertions/deletions by day](MetricCharts/2026-06-02/git_insertions_deletions_by_day.png)

### git_files_changed_by_day

![Git files changed by day since 2026-04-01](MetricCharts/2026-06-02/git_files_changed_by_day.png)

### git_net_lines_by_day

![Git net lines by day since 2026-04-01](MetricCharts/2026-06-02/git_net_lines_by_day.png)

### git_churn_per_commit_by_day

![Git churn per commit by day](MetricCharts/2026-06-02/git_churn_per_commit_by_day.png)

### git_commits_by_week

![Git commits by ISO week](MetricCharts/2026-06-02/git_commits_by_week.png)

### git_churn_by_week

![Git churn by ISO week](MetricCharts/2026-06-02/git_churn_by_week.png)

### git_files_changed_by_week

![Git files changed by ISO week](MetricCharts/2026-06-02/git_files_changed_by_week.png)

### git_net_lines_by_week

![Git net lines by ISO week](MetricCharts/2026-06-02/git_net_lines_by_week.png)

### git_churn_per_commit_by_week

![Git churn per commit by ISO week](MetricCharts/2026-06-02/git_churn_per_commit_by_week.png)

### git_commit_weekday_hour_heatmap

![Git commit heatmap by Samara weekday/hour](MetricCharts/2026-06-02/git_commit_weekday_hour_heatmap.png)

### git_churn_weekday_hour_heatmap

![Git churn heatmap by Samara weekday/hour](MetricCharts/2026-06-02/git_churn_weekday_hour_heatmap.png)

Evidence note: Committed changed-line pressure by Samara weekday and hour.

### daily_tokens_per_git_changed_line

![Daily tokens per committed changed line](MetricCharts/2026-06-02/daily_tokens_per_git_changed_line.png)

Evidence note: Correlation-only: token usage and git churn are grouped by calendar day, not causally matched per task.

### daily_cost_per_git_changed_line

![Daily GPT-5.5 cost per committed changed line](MetricCharts/2026-06-02/daily_cost_per_git_changed_line.png)

Evidence note: Correlation-only: token usage and git churn are grouped by calendar day, not causally matched per task.

### daily_tokens_vs_git_churn

![Daily tokens vs git churn](MetricCharts/2026-06-02/daily_tokens_vs_git_churn.png)

Evidence note: Correlation-only scatter: shows whether high-token days also had high committed churn.

### daily_cost_vs_git_churn

![Daily GPT-5.5 cost vs git churn](MetricCharts/2026-06-02/daily_cost_vs_git_churn.png)

Evidence note: Correlation-only scatter: shows cost pressure against committed churn.

### current_snapshot_token_velocity

![Current snapshot token velocity by class](MetricCharts/2026-06-02/current_snapshot_token_velocity.png)

### current_snapshot_money_velocity

![Current snapshot API-equivalent money velocity](MetricCharts/2026-06-02/current_snapshot_money_velocity.png)

### current_snapshot_code_velocity

![Current snapshot code and density velocity](MetricCharts/2026-06-02/current_snapshot_code_velocity.png)

### layperson_all_time_scale

![All-time token scale for non-specialists](MetricCharts/2026-06-02/layperson_all_time_scale.png)

### layperson_current_burn_rate

![Current burn-rate scale for non-specialists](MetricCharts/2026-06-02/layperson_current_burn_rate.png)

## Supporting Data

- Machine-readable dashboard: `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-06-02.json`
- Token report JSON: `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-06-02.json`
- OpenAI pricing source: https://developers.openai.com/api/docs/pricing
- GPT-5.5 model pricing source: https://developers.openai.com/api/docs/models/gpt-5.5
- Prompt caching source: https://developers.openai.com/api/docs/guides/prompt-caching
- Reasoning source: https://developers.openai.com/api/docs/guides/reasoning

## Residual Risk

- Local Codex JSONL is not billing-provider proof.
- Long-context post-cutoff detection is a lower-bound delta-event heuristic; exact provider-side surcharge classification is absent.
- Git churn and token-vs-git charts use committed history; uncommitted live-agent work is visible only after commit.
- Token-vs-git scatter plots are same-day correlations, not per-task causal attribution.
- Filesystem metrics exclude configured build/cache/archive directories.

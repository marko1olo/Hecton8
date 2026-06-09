# Token Usage Deep Analytics 2026-06-06

Generated Samara: `2026-06-06T15:13:17.919293+04:00`
Evidence class: STATIC_LOCAL_JSONL_REPORT_DERIVED. This is not provider invoice proof.

## Headline

| Metric | Value |
|---|---:|
| Total tokens | 138,912,242,896 |
| Input tokens | 138,427,944,497 |
| Cached input tokens | 133,102,804,608 |
| Output tokens | 483,264,799 |
| Reasoning output tokens | 147,905,141 |
| GPT-5.5 standard API-equivalent | $107,675.05 |
| 1h buckets | 120 |
| 4h buckets | 31 |
| 12h buckets | 11 |
| 1d buckets | 65 |
| Deep chart count | 602 |
| Current tokens/day forecast lane | 3,290,192,673 |
| 7d average tokens/day lane | 2,198,451,801 |
| 30d average tokens/day lane | 3,807,528,479 |

## Smoothing Contract

- Raw data is plotted on each time-series chart as a thin line.
- Rolling median and EMA overlays are trend aids; peaks and totals are not replaced by smoothing.
- 1h, 4h, and 12h charts derive from the current high-resolution hourly report window.
- 1d charts use the all-time daily report rows.

## Groups

- `composition`: 19 charts
- `cost_bands`: 19 charts
- `distributions`: 76 charts
- `efficiency`: 19 charts
- `forecast`: 2 charts
- `heatmaps`: 24 charts
- `outliers`: 40 charts
- `pareto`: 4 charts
- `ratio_pack`: 19 charts
- `time_series`: 380 charts

## Chart Index

### composition

- [1h all input/output composition](#1h_all_io_composition_stack)
- [1h last24h input/output composition](#1h_last24h_io_composition_stack)
- [1h last48h input/output composition](#1h_last48h_io_composition_stack)
- [1h last72h input/output composition](#1h_last72h_io_composition_stack)
- [1h last96h input/output composition](#1h_last96h_io_composition_stack)
- [4h all input/output composition](#4h_all_io_composition_stack)
- [4h last24h input/output composition](#4h_last24h_io_composition_stack)
- [4h last48h input/output composition](#4h_last48h_io_composition_stack)
- [4h last72h input/output composition](#4h_last72h_io_composition_stack)
- [4h last120h input/output composition](#4h_last120h_io_composition_stack)
- [12h all input/output composition](#12h_all_io_composition_stack)
- [12h last48h input/output composition](#12h_last48h_io_composition_stack)
- [12h last72h input/output composition](#12h_last72h_io_composition_stack)
- [12h last120h input/output composition](#12h_last120h_io_composition_stack)
- [1d all input/output composition](#1d_all_io_composition_stack)
- [1d last7d input/output composition](#1d_last7d_io_composition_stack)
- [1d last14d input/output composition](#1d_last14d_io_composition_stack)
- [1d last30d input/output composition](#1d_last30d_io_composition_stack)
- [1d last60d input/output composition](#1d_last60d_io_composition_stack)

### cost_bands

- [1h all cost sensitivity bands](#1h_all_cost_sensitivity_bands)
- [1h last24h cost sensitivity bands](#1h_last24h_cost_sensitivity_bands)
- [1h last48h cost sensitivity bands](#1h_last48h_cost_sensitivity_bands)
- [1h last72h cost sensitivity bands](#1h_last72h_cost_sensitivity_bands)
- [1h last96h cost sensitivity bands](#1h_last96h_cost_sensitivity_bands)
- [4h all cost sensitivity bands](#4h_all_cost_sensitivity_bands)
- [4h last24h cost sensitivity bands](#4h_last24h_cost_sensitivity_bands)
- [4h last48h cost sensitivity bands](#4h_last48h_cost_sensitivity_bands)
- [4h last72h cost sensitivity bands](#4h_last72h_cost_sensitivity_bands)
- [4h last120h cost sensitivity bands](#4h_last120h_cost_sensitivity_bands)
- [12h all cost sensitivity bands](#12h_all_cost_sensitivity_bands)
- [12h last48h cost sensitivity bands](#12h_last48h_cost_sensitivity_bands)
- [12h last72h cost sensitivity bands](#12h_last72h_cost_sensitivity_bands)
- [12h last120h cost sensitivity bands](#12h_last120h_cost_sensitivity_bands)
- [1d all cost sensitivity bands](#1d_all_cost_sensitivity_bands)
- [1d last7d cost sensitivity bands](#1d_last7d_cost_sensitivity_bands)
- [1d last14d cost sensitivity bands](#1d_last14d_cost_sensitivity_bands)
- [1d last30d cost sensitivity bands](#1d_last30d_cost_sensitivity_bands)
- [1d last60d cost sensitivity bands](#1d_last60d_cost_sensitivity_bands)

### distributions

- [1h distribution of total tokens](#1h_distribution_total_tokens)
- [1h log distribution of total tokens](#1h_log_distribution_total_tokens)
- [1h distribution of output tokens](#1h_distribution_output_tokens)
- [1h log distribution of output tokens](#1h_log_distribution_output_tokens)
- [1h distribution of reasoning output tokens](#1h_distribution_reasoning_output_tokens)
- [1h log distribution of reasoning output tokens](#1h_log_distribution_reasoning_output_tokens)
- [1h distribution of GPT-5.5 standard cost](#1h_distribution_cost_usd)
- [1h log distribution of GPT-5.5 standard cost](#1h_log_distribution_cost_usd)
- [1h distribution of cache savings](#1h_distribution_cache_savings_usd)
- [1h log distribution of cache savings](#1h_log_distribution_cache_savings_usd)
- [1h distribution of effective cost per total token](#1h_distribution_effective_usd_per_1m_total_tokens)
- [1h log distribution of effective cost per total token](#1h_log_distribution_effective_usd_per_1m_total_tokens)
- [1h distribution of cache ratio](#1h_distribution_cache_ratio)
- [1h log distribution of cache ratio](#1h_log_distribution_cache_ratio)
- [1h distribution of output ratio](#1h_distribution_output_ratio)
- [1h log distribution of output ratio](#1h_log_distribution_output_ratio)
- [1h distribution of reasoning/output ratio](#1h_distribution_reasoning_ratio)
- [1h log distribution of reasoning/output ratio](#1h_log_distribution_reasoning_ratio)
- [4h distribution of total tokens](#4h_distribution_total_tokens)
- [4h log distribution of total tokens](#4h_log_distribution_total_tokens)
- [4h distribution of output tokens](#4h_distribution_output_tokens)
- [4h log distribution of output tokens](#4h_log_distribution_output_tokens)
- [4h distribution of reasoning output tokens](#4h_distribution_reasoning_output_tokens)
- [4h log distribution of reasoning output tokens](#4h_log_distribution_reasoning_output_tokens)
- [4h distribution of GPT-5.5 standard cost](#4h_distribution_cost_usd)
- [4h log distribution of GPT-5.5 standard cost](#4h_log_distribution_cost_usd)
- [4h distribution of cache savings](#4h_distribution_cache_savings_usd)
- [4h log distribution of cache savings](#4h_log_distribution_cache_savings_usd)
- [4h distribution of effective cost per total token](#4h_distribution_effective_usd_per_1m_total_tokens)
- [4h log distribution of effective cost per total token](#4h_log_distribution_effective_usd_per_1m_total_tokens)
- [4h distribution of cost per output token](#4h_distribution_cost_per_1m_output_tokens)
- [4h log distribution of cost per output token](#4h_log_distribution_cost_per_1m_output_tokens)
- [4h distribution of cache ratio](#4h_distribution_cache_ratio)
- [4h log distribution of cache ratio](#4h_log_distribution_cache_ratio)
- [4h distribution of output ratio](#4h_distribution_output_ratio)
- [4h log distribution of output ratio](#4h_log_distribution_output_ratio)
- [4h distribution of reasoning/output ratio](#4h_distribution_reasoning_ratio)
- [4h log distribution of reasoning/output ratio](#4h_log_distribution_reasoning_ratio)
- [12h distribution of total tokens](#12h_distribution_total_tokens)
- [12h log distribution of total tokens](#12h_log_distribution_total_tokens)
- [12h distribution of output tokens](#12h_distribution_output_tokens)
- [12h log distribution of output tokens](#12h_log_distribution_output_tokens)
- [12h distribution of reasoning output tokens](#12h_distribution_reasoning_output_tokens)
- [12h log distribution of reasoning output tokens](#12h_log_distribution_reasoning_output_tokens)
- [12h distribution of GPT-5.5 standard cost](#12h_distribution_cost_usd)
- [12h log distribution of GPT-5.5 standard cost](#12h_log_distribution_cost_usd)
- [12h distribution of cache savings](#12h_distribution_cache_savings_usd)
- [12h log distribution of cache savings](#12h_log_distribution_cache_savings_usd)
- [12h distribution of effective cost per total token](#12h_distribution_effective_usd_per_1m_total_tokens)
- [12h log distribution of effective cost per total token](#12h_log_distribution_effective_usd_per_1m_total_tokens)
- [12h distribution of cost per output token](#12h_distribution_cost_per_1m_output_tokens)
- [12h log distribution of cost per output token](#12h_log_distribution_cost_per_1m_output_tokens)
- [12h distribution of cache ratio](#12h_distribution_cache_ratio)
- [12h log distribution of cache ratio](#12h_log_distribution_cache_ratio)
- [12h distribution of output ratio](#12h_distribution_output_ratio)
- [12h log distribution of output ratio](#12h_log_distribution_output_ratio)
- [12h distribution of reasoning/output ratio](#12h_distribution_reasoning_ratio)
- [12h log distribution of reasoning/output ratio](#12h_log_distribution_reasoning_ratio)
- [1d distribution of total tokens](#1d_distribution_total_tokens)
- [1d log distribution of total tokens](#1d_log_distribution_total_tokens)
- [1d distribution of output tokens](#1d_distribution_output_tokens)
- [1d log distribution of output tokens](#1d_log_distribution_output_tokens)
- [1d distribution of reasoning output tokens](#1d_distribution_reasoning_output_tokens)
- [1d log distribution of reasoning output tokens](#1d_log_distribution_reasoning_output_tokens)
- [1d distribution of GPT-5.5 standard cost](#1d_distribution_cost_usd)
- [1d log distribution of GPT-5.5 standard cost](#1d_log_distribution_cost_usd)
- [1d distribution of cache savings](#1d_distribution_cache_savings_usd)
- [1d log distribution of cache savings](#1d_log_distribution_cache_savings_usd)
- [1d distribution of effective cost per total token](#1d_distribution_effective_usd_per_1m_total_tokens)
- [1d log distribution of effective cost per total token](#1d_log_distribution_effective_usd_per_1m_total_tokens)
- [1d distribution of cache ratio](#1d_distribution_cache_ratio)
- [1d log distribution of cache ratio](#1d_log_distribution_cache_ratio)
- [1d distribution of output ratio](#1d_distribution_output_ratio)
- [1d log distribution of output ratio](#1d_log_distribution_output_ratio)
- [1d distribution of reasoning/output ratio](#1d_distribution_reasoning_ratio)
- [1d log distribution of reasoning/output ratio](#1d_log_distribution_reasoning_ratio)

### efficiency

- [1h all efficiency pack](#1h_all_efficiency_pack)
- [1h last24h efficiency pack](#1h_last24h_efficiency_pack)
- [1h last48h efficiency pack](#1h_last48h_efficiency_pack)
- [1h last72h efficiency pack](#1h_last72h_efficiency_pack)
- [1h last96h efficiency pack](#1h_last96h_efficiency_pack)
- [4h all efficiency pack](#4h_all_efficiency_pack)
- [4h last24h efficiency pack](#4h_last24h_efficiency_pack)
- [4h last48h efficiency pack](#4h_last48h_efficiency_pack)
- [4h last72h efficiency pack](#4h_last72h_efficiency_pack)
- [4h last120h efficiency pack](#4h_last120h_efficiency_pack)
- [12h all efficiency pack](#12h_all_efficiency_pack)
- [12h last48h efficiency pack](#12h_last48h_efficiency_pack)
- [12h last72h efficiency pack](#12h_last72h_efficiency_pack)
- [12h last120h efficiency pack](#12h_last120h_efficiency_pack)
- [1d all efficiency pack](#1d_all_efficiency_pack)
- [1d last7d efficiency pack](#1d_last7d_efficiency_pack)
- [1d last14d efficiency pack](#1d_last14d_efficiency_pack)
- [1d last30d efficiency pack](#1d_last30d_efficiency_pack)
- [1d last60d efficiency pack](#1d_last60d_efficiency_pack)

### forecast

- [Forecast fan: tokens](#forecast_fan_tokens)
- [Forecast fan: cost](#forecast_fan_cost)

### heatmaps

- [1h_day_hour heatmap of total tokens](#1h_day_hour_heatmap_total_tokens)
- [1h_weekday_hour heatmap of total tokens](#1h_weekday_hour_heatmap_total_tokens)
- [4h_day_slot heatmap of total tokens](#4h_day_slot_heatmap_total_tokens)
- [12h_day_slot heatmap of total tokens](#12h_day_slot_heatmap_total_tokens)
- [1h_day_hour heatmap of output tokens](#1h_day_hour_heatmap_output_tokens)
- [1h_weekday_hour heatmap of output tokens](#1h_weekday_hour_heatmap_output_tokens)
- [4h_day_slot heatmap of output tokens](#4h_day_slot_heatmap_output_tokens)
- [12h_day_slot heatmap of output tokens](#12h_day_slot_heatmap_output_tokens)
- [1h_day_hour heatmap of reasoning output tokens](#1h_day_hour_heatmap_reasoning_output_tokens)
- [1h_weekday_hour heatmap of reasoning output tokens](#1h_weekday_hour_heatmap_reasoning_output_tokens)
- [4h_day_slot heatmap of reasoning output tokens](#4h_day_slot_heatmap_reasoning_output_tokens)
- [12h_day_slot heatmap of reasoning output tokens](#12h_day_slot_heatmap_reasoning_output_tokens)
- [1h_day_hour heatmap of GPT-5.5 standard cost](#1h_day_hour_heatmap_cost_usd)
- [1h_weekday_hour heatmap of GPT-5.5 standard cost](#1h_weekday_hour_heatmap_cost_usd)
- [4h_day_slot heatmap of GPT-5.5 standard cost](#4h_day_slot_heatmap_cost_usd)
- [12h_day_slot heatmap of GPT-5.5 standard cost](#12h_day_slot_heatmap_cost_usd)
- [1h_day_hour heatmap of cost per output token](#1h_day_hour_heatmap_cost_per_1m_output_tokens)
- [1h_weekday_hour heatmap of cost per output token](#1h_weekday_hour_heatmap_cost_per_1m_output_tokens)
- [4h_day_slot heatmap of cost per output token](#4h_day_slot_heatmap_cost_per_1m_output_tokens)
- [12h_day_slot heatmap of cost per output token](#12h_day_slot_heatmap_cost_per_1m_output_tokens)
- [1h_day_hour heatmap of cache ratio](#1h_day_hour_heatmap_cache_ratio)
- [1h_weekday_hour heatmap of cache ratio](#1h_weekday_hour_heatmap_cache_ratio)
- [4h_day_slot heatmap of cache ratio](#4h_day_slot_heatmap_cache_ratio)
- [12h_day_slot heatmap of cache ratio](#12h_day_slot_heatmap_cache_ratio)

### outliers

- [1h outliers by total tokens](#1h_outliers_total_tokens_top16)
- [1h outliers by input tokens](#1h_outliers_input_tokens_top16)
- [1h outliers by uncached input tokens](#1h_outliers_uncached_input_tokens_top16)
- [1h outliers by output tokens](#1h_outliers_output_tokens_top16)
- [1h outliers by reasoning output tokens](#1h_outliers_reasoning_output_tokens_top16)
- [1h outliers by GPT-5.5 standard cost](#1h_outliers_cost_usd_top16)
- [1h outliers by no-cache cost](#1h_outliers_cost_no_cache_usd_top16)
- [1h outliers by cache savings](#1h_outliers_cache_savings_usd_top16)
- [1h outliers by effective cost per total token](#1h_outliers_effective_usd_per_1m_total_tokens_top16)
- [1h outliers by cost per output token](#1h_outliers_cost_per_1m_output_tokens_top16)
- [4h outliers by total tokens](#4h_outliers_total_tokens_top16)
- [4h outliers by input tokens](#4h_outliers_input_tokens_top16)
- [4h outliers by uncached input tokens](#4h_outliers_uncached_input_tokens_top16)
- [4h outliers by output tokens](#4h_outliers_output_tokens_top16)
- [4h outliers by reasoning output tokens](#4h_outliers_reasoning_output_tokens_top16)
- [4h outliers by GPT-5.5 standard cost](#4h_outliers_cost_usd_top16)
- [4h outliers by no-cache cost](#4h_outliers_cost_no_cache_usd_top16)
- [4h outliers by cache savings](#4h_outliers_cache_savings_usd_top16)
- [4h outliers by effective cost per total token](#4h_outliers_effective_usd_per_1m_total_tokens_top16)
- [4h outliers by cost per output token](#4h_outliers_cost_per_1m_output_tokens_top16)
- [12h outliers by total tokens](#12h_outliers_total_tokens_top16)
- [12h outliers by input tokens](#12h_outliers_input_tokens_top16)
- [12h outliers by uncached input tokens](#12h_outliers_uncached_input_tokens_top16)
- [12h outliers by output tokens](#12h_outliers_output_tokens_top16)
- [12h outliers by reasoning output tokens](#12h_outliers_reasoning_output_tokens_top16)
- [12h outliers by GPT-5.5 standard cost](#12h_outliers_cost_usd_top16)
- [12h outliers by no-cache cost](#12h_outliers_cost_no_cache_usd_top16)
- [12h outliers by cache savings](#12h_outliers_cache_savings_usd_top16)
- [12h outliers by effective cost per total token](#12h_outliers_effective_usd_per_1m_total_tokens_top16)
- [12h outliers by cost per output token](#12h_outliers_cost_per_1m_output_tokens_top16)
- [1d outliers by total tokens](#1d_outliers_total_tokens_top16)
- [1d outliers by input tokens](#1d_outliers_input_tokens_top16)
- [1d outliers by uncached input tokens](#1d_outliers_uncached_input_tokens_top16)
- [1d outliers by output tokens](#1d_outliers_output_tokens_top16)
- [1d outliers by reasoning output tokens](#1d_outliers_reasoning_output_tokens_top16)
- [1d outliers by GPT-5.5 standard cost](#1d_outliers_cost_usd_top16)
- [1d outliers by no-cache cost](#1d_outliers_cost_no_cache_usd_top16)
- [1d outliers by cache savings](#1d_outliers_cache_savings_usd_top16)
- [1d outliers by effective cost per total token](#1d_outliers_effective_usd_per_1m_total_tokens_top16)
- [1d outliers by cost per output token](#1d_outliers_cost_per_1m_output_tokens_top16)

### pareto

- [Top sessions by total tokens](#top_sessions_total_tokens_pareto)
- [Top sessions by GPT-5.5 standard cost](#top_sessions_cost_pareto)
- [Top sessions by output tokens](#top_sessions_output_pareto)
- [Top sessions by reasoning output](#top_sessions_reasoning_pareto)

### ratio_pack

- [1h all quality ratios](#1h_all_ratio_pack)
- [1h last24h quality ratios](#1h_last24h_ratio_pack)
- [1h last48h quality ratios](#1h_last48h_ratio_pack)
- [1h last72h quality ratios](#1h_last72h_ratio_pack)
- [1h last96h quality ratios](#1h_last96h_ratio_pack)
- [4h all quality ratios](#4h_all_ratio_pack)
- [4h last24h quality ratios](#4h_last24h_ratio_pack)
- [4h last48h quality ratios](#4h_last48h_ratio_pack)
- [4h last72h quality ratios](#4h_last72h_ratio_pack)
- [4h last120h quality ratios](#4h_last120h_ratio_pack)
- [12h all quality ratios](#12h_all_ratio_pack)
- [12h last48h quality ratios](#12h_last48h_ratio_pack)
- [12h last72h quality ratios](#12h_last72h_ratio_pack)
- [12h last120h quality ratios](#12h_last120h_ratio_pack)
- [1d all quality ratios](#1d_all_ratio_pack)
- [1d last7d quality ratios](#1d_last7d_ratio_pack)
- [1d last14d quality ratios](#1d_last14d_ratio_pack)
- [1d last30d quality ratios](#1d_last30d_ratio_pack)
- [1d last60d quality ratios](#1d_last60d_ratio_pack)

### time_series

- [1h all total tokens: raw vs smoothed](#1h_all_total_tokens_raw_median_ema)
- [1h all input tokens: raw vs smoothed](#1h_all_input_tokens_raw_median_ema)
- [1h all cached input tokens: raw vs smoothed](#1h_all_cached_input_tokens_raw_median_ema)
- [1h all uncached input tokens: raw vs smoothed](#1h_all_uncached_input_tokens_raw_median_ema)
- [1h all output tokens: raw vs smoothed](#1h_all_output_tokens_raw_median_ema)
- [1h all reasoning output tokens: raw vs smoothed](#1h_all_reasoning_output_tokens_raw_median_ema)
- [1h all GPT-5.5 standard cost: raw vs smoothed](#1h_all_cost_usd_raw_median_ema)
- [1h all no-cache cost: raw vs smoothed](#1h_all_cost_no_cache_usd_raw_median_ema)
- [1h all cache savings: raw vs smoothed](#1h_all_cache_savings_usd_raw_median_ema)
- [1h all long-context upper cost: raw vs smoothed](#1h_all_long_context_upper_cost_usd_raw_median_ema)
- [1h all effective cost per total token: raw vs smoothed](#1h_all_effective_usd_per_1m_total_tokens_raw_median_ema)
- [1h all cost per output token: raw vs smoothed](#1h_all_cost_per_1m_output_tokens_raw_median_ema)
- [1h all tokens per dollar: raw vs smoothed](#1h_all_tokens_per_usd_raw_median_ema)
- [1h all cache ratio: raw vs smoothed](#1h_all_cache_ratio_raw_median_ema)
- [1h all output ratio: raw vs smoothed](#1h_all_output_ratio_raw_median_ema)
- [1h all reasoning/output ratio: raw vs smoothed](#1h_all_reasoning_ratio_raw_median_ema)
- [1h all output cost share: raw vs smoothed](#1h_all_output_cost_share_raw_median_ema)
- [1h all output per input: raw vs smoothed](#1h_all_output_per_1m_input_tokens_raw_median_ema)
- [1h all reasoning per total: raw vs smoothed](#1h_all_reasoning_per_1m_total_tokens_raw_median_ema)
- [1h all human-scale pages: raw vs smoothed](#1h_all_printed_pages_500w_raw_median_ema)
- [1h last24h total tokens: raw vs smoothed](#1h_last24h_total_tokens_raw_median_ema)
- [1h last24h input tokens: raw vs smoothed](#1h_last24h_input_tokens_raw_median_ema)
- [1h last24h cached input tokens: raw vs smoothed](#1h_last24h_cached_input_tokens_raw_median_ema)
- [1h last24h uncached input tokens: raw vs smoothed](#1h_last24h_uncached_input_tokens_raw_median_ema)
- [1h last24h output tokens: raw vs smoothed](#1h_last24h_output_tokens_raw_median_ema)
- [1h last24h reasoning output tokens: raw vs smoothed](#1h_last24h_reasoning_output_tokens_raw_median_ema)
- [1h last24h GPT-5.5 standard cost: raw vs smoothed](#1h_last24h_cost_usd_raw_median_ema)
- [1h last24h no-cache cost: raw vs smoothed](#1h_last24h_cost_no_cache_usd_raw_median_ema)
- [1h last24h cache savings: raw vs smoothed](#1h_last24h_cache_savings_usd_raw_median_ema)
- [1h last24h long-context upper cost: raw vs smoothed](#1h_last24h_long_context_upper_cost_usd_raw_median_ema)
- [1h last24h effective cost per total token: raw vs smoothed](#1h_last24h_effective_usd_per_1m_total_tokens_raw_median_ema)
- [1h last24h cost per output token: raw vs smoothed](#1h_last24h_cost_per_1m_output_tokens_raw_median_ema)
- [1h last24h tokens per dollar: raw vs smoothed](#1h_last24h_tokens_per_usd_raw_median_ema)
- [1h last24h cache ratio: raw vs smoothed](#1h_last24h_cache_ratio_raw_median_ema)
- [1h last24h output ratio: raw vs smoothed](#1h_last24h_output_ratio_raw_median_ema)
- [1h last24h reasoning/output ratio: raw vs smoothed](#1h_last24h_reasoning_ratio_raw_median_ema)
- [1h last24h output cost share: raw vs smoothed](#1h_last24h_output_cost_share_raw_median_ema)
- [1h last24h output per input: raw vs smoothed](#1h_last24h_output_per_1m_input_tokens_raw_median_ema)
- [1h last24h reasoning per total: raw vs smoothed](#1h_last24h_reasoning_per_1m_total_tokens_raw_median_ema)
- [1h last24h human-scale pages: raw vs smoothed](#1h_last24h_printed_pages_500w_raw_median_ema)
- [1h last48h total tokens: raw vs smoothed](#1h_last48h_total_tokens_raw_median_ema)
- [1h last48h input tokens: raw vs smoothed](#1h_last48h_input_tokens_raw_median_ema)
- [1h last48h cached input tokens: raw vs smoothed](#1h_last48h_cached_input_tokens_raw_median_ema)
- [1h last48h uncached input tokens: raw vs smoothed](#1h_last48h_uncached_input_tokens_raw_median_ema)
- [1h last48h output tokens: raw vs smoothed](#1h_last48h_output_tokens_raw_median_ema)
- [1h last48h reasoning output tokens: raw vs smoothed](#1h_last48h_reasoning_output_tokens_raw_median_ema)
- [1h last48h GPT-5.5 standard cost: raw vs smoothed](#1h_last48h_cost_usd_raw_median_ema)
- [1h last48h no-cache cost: raw vs smoothed](#1h_last48h_cost_no_cache_usd_raw_median_ema)
- [1h last48h cache savings: raw vs smoothed](#1h_last48h_cache_savings_usd_raw_median_ema)
- [1h last48h long-context upper cost: raw vs smoothed](#1h_last48h_long_context_upper_cost_usd_raw_median_ema)
- [1h last48h effective cost per total token: raw vs smoothed](#1h_last48h_effective_usd_per_1m_total_tokens_raw_median_ema)
- [1h last48h cost per output token: raw vs smoothed](#1h_last48h_cost_per_1m_output_tokens_raw_median_ema)
- [1h last48h tokens per dollar: raw vs smoothed](#1h_last48h_tokens_per_usd_raw_median_ema)
- [1h last48h cache ratio: raw vs smoothed](#1h_last48h_cache_ratio_raw_median_ema)
- [1h last48h output ratio: raw vs smoothed](#1h_last48h_output_ratio_raw_median_ema)
- [1h last48h reasoning/output ratio: raw vs smoothed](#1h_last48h_reasoning_ratio_raw_median_ema)
- [1h last48h output cost share: raw vs smoothed](#1h_last48h_output_cost_share_raw_median_ema)
- [1h last48h output per input: raw vs smoothed](#1h_last48h_output_per_1m_input_tokens_raw_median_ema)
- [1h last48h reasoning per total: raw vs smoothed](#1h_last48h_reasoning_per_1m_total_tokens_raw_median_ema)
- [1h last48h human-scale pages: raw vs smoothed](#1h_last48h_printed_pages_500w_raw_median_ema)
- [1h last72h total tokens: raw vs smoothed](#1h_last72h_total_tokens_raw_median_ema)
- [1h last72h input tokens: raw vs smoothed](#1h_last72h_input_tokens_raw_median_ema)
- [1h last72h cached input tokens: raw vs smoothed](#1h_last72h_cached_input_tokens_raw_median_ema)
- [1h last72h uncached input tokens: raw vs smoothed](#1h_last72h_uncached_input_tokens_raw_median_ema)
- [1h last72h output tokens: raw vs smoothed](#1h_last72h_output_tokens_raw_median_ema)
- [1h last72h reasoning output tokens: raw vs smoothed](#1h_last72h_reasoning_output_tokens_raw_median_ema)
- [1h last72h GPT-5.5 standard cost: raw vs smoothed](#1h_last72h_cost_usd_raw_median_ema)
- [1h last72h no-cache cost: raw vs smoothed](#1h_last72h_cost_no_cache_usd_raw_median_ema)
- [1h last72h cache savings: raw vs smoothed](#1h_last72h_cache_savings_usd_raw_median_ema)
- [1h last72h long-context upper cost: raw vs smoothed](#1h_last72h_long_context_upper_cost_usd_raw_median_ema)
- [1h last72h effective cost per total token: raw vs smoothed](#1h_last72h_effective_usd_per_1m_total_tokens_raw_median_ema)
- [1h last72h cost per output token: raw vs smoothed](#1h_last72h_cost_per_1m_output_tokens_raw_median_ema)
- [1h last72h tokens per dollar: raw vs smoothed](#1h_last72h_tokens_per_usd_raw_median_ema)
- [1h last72h cache ratio: raw vs smoothed](#1h_last72h_cache_ratio_raw_median_ema)
- [1h last72h output ratio: raw vs smoothed](#1h_last72h_output_ratio_raw_median_ema)
- [1h last72h reasoning/output ratio: raw vs smoothed](#1h_last72h_reasoning_ratio_raw_median_ema)
- [1h last72h output cost share: raw vs smoothed](#1h_last72h_output_cost_share_raw_median_ema)
- [1h last72h output per input: raw vs smoothed](#1h_last72h_output_per_1m_input_tokens_raw_median_ema)
- [1h last72h reasoning per total: raw vs smoothed](#1h_last72h_reasoning_per_1m_total_tokens_raw_median_ema)
- [1h last72h human-scale pages: raw vs smoothed](#1h_last72h_printed_pages_500w_raw_median_ema)
- [1h last96h total tokens: raw vs smoothed](#1h_last96h_total_tokens_raw_median_ema)
- [1h last96h input tokens: raw vs smoothed](#1h_last96h_input_tokens_raw_median_ema)
- [1h last96h cached input tokens: raw vs smoothed](#1h_last96h_cached_input_tokens_raw_median_ema)
- [1h last96h uncached input tokens: raw vs smoothed](#1h_last96h_uncached_input_tokens_raw_median_ema)
- [1h last96h output tokens: raw vs smoothed](#1h_last96h_output_tokens_raw_median_ema)
- [1h last96h reasoning output tokens: raw vs smoothed](#1h_last96h_reasoning_output_tokens_raw_median_ema)
- [1h last96h GPT-5.5 standard cost: raw vs smoothed](#1h_last96h_cost_usd_raw_median_ema)
- [1h last96h no-cache cost: raw vs smoothed](#1h_last96h_cost_no_cache_usd_raw_median_ema)
- [1h last96h cache savings: raw vs smoothed](#1h_last96h_cache_savings_usd_raw_median_ema)
- [1h last96h long-context upper cost: raw vs smoothed](#1h_last96h_long_context_upper_cost_usd_raw_median_ema)
- [1h last96h effective cost per total token: raw vs smoothed](#1h_last96h_effective_usd_per_1m_total_tokens_raw_median_ema)
- [1h last96h cost per output token: raw vs smoothed](#1h_last96h_cost_per_1m_output_tokens_raw_median_ema)
- [1h last96h tokens per dollar: raw vs smoothed](#1h_last96h_tokens_per_usd_raw_median_ema)
- [1h last96h cache ratio: raw vs smoothed](#1h_last96h_cache_ratio_raw_median_ema)
- [1h last96h output ratio: raw vs smoothed](#1h_last96h_output_ratio_raw_median_ema)
- [1h last96h reasoning/output ratio: raw vs smoothed](#1h_last96h_reasoning_ratio_raw_median_ema)
- [1h last96h output cost share: raw vs smoothed](#1h_last96h_output_cost_share_raw_median_ema)
- [1h last96h output per input: raw vs smoothed](#1h_last96h_output_per_1m_input_tokens_raw_median_ema)
- [1h last96h reasoning per total: raw vs smoothed](#1h_last96h_reasoning_per_1m_total_tokens_raw_median_ema)
- [1h last96h human-scale pages: raw vs smoothed](#1h_last96h_printed_pages_500w_raw_median_ema)
- [4h all total tokens: raw vs smoothed](#4h_all_total_tokens_raw_median_ema)
- [4h all input tokens: raw vs smoothed](#4h_all_input_tokens_raw_median_ema)
- [4h all cached input tokens: raw vs smoothed](#4h_all_cached_input_tokens_raw_median_ema)
- [4h all uncached input tokens: raw vs smoothed](#4h_all_uncached_input_tokens_raw_median_ema)
- [4h all output tokens: raw vs smoothed](#4h_all_output_tokens_raw_median_ema)
- [4h all reasoning output tokens: raw vs smoothed](#4h_all_reasoning_output_tokens_raw_median_ema)
- [4h all GPT-5.5 standard cost: raw vs smoothed](#4h_all_cost_usd_raw_median_ema)
- [4h all no-cache cost: raw vs smoothed](#4h_all_cost_no_cache_usd_raw_median_ema)
- [4h all cache savings: raw vs smoothed](#4h_all_cache_savings_usd_raw_median_ema)
- [4h all long-context upper cost: raw vs smoothed](#4h_all_long_context_upper_cost_usd_raw_median_ema)
- [4h all effective cost per total token: raw vs smoothed](#4h_all_effective_usd_per_1m_total_tokens_raw_median_ema)
- [4h all cost per output token: raw vs smoothed](#4h_all_cost_per_1m_output_tokens_raw_median_ema)
- [4h all tokens per dollar: raw vs smoothed](#4h_all_tokens_per_usd_raw_median_ema)
- [4h all cache ratio: raw vs smoothed](#4h_all_cache_ratio_raw_median_ema)
- [4h all output ratio: raw vs smoothed](#4h_all_output_ratio_raw_median_ema)
- [4h all reasoning/output ratio: raw vs smoothed](#4h_all_reasoning_ratio_raw_median_ema)
- [4h all output cost share: raw vs smoothed](#4h_all_output_cost_share_raw_median_ema)
- [4h all output per input: raw vs smoothed](#4h_all_output_per_1m_input_tokens_raw_median_ema)
- [4h all reasoning per total: raw vs smoothed](#4h_all_reasoning_per_1m_total_tokens_raw_median_ema)
- [4h all human-scale pages: raw vs smoothed](#4h_all_printed_pages_500w_raw_median_ema)
- [4h last24h total tokens: raw vs smoothed](#4h_last24h_total_tokens_raw_median_ema)
- [4h last24h input tokens: raw vs smoothed](#4h_last24h_input_tokens_raw_median_ema)
- [4h last24h cached input tokens: raw vs smoothed](#4h_last24h_cached_input_tokens_raw_median_ema)
- [4h last24h uncached input tokens: raw vs smoothed](#4h_last24h_uncached_input_tokens_raw_median_ema)
- [4h last24h output tokens: raw vs smoothed](#4h_last24h_output_tokens_raw_median_ema)
- [4h last24h reasoning output tokens: raw vs smoothed](#4h_last24h_reasoning_output_tokens_raw_median_ema)
- [4h last24h GPT-5.5 standard cost: raw vs smoothed](#4h_last24h_cost_usd_raw_median_ema)
- [4h last24h no-cache cost: raw vs smoothed](#4h_last24h_cost_no_cache_usd_raw_median_ema)
- [4h last24h cache savings: raw vs smoothed](#4h_last24h_cache_savings_usd_raw_median_ema)
- [4h last24h long-context upper cost: raw vs smoothed](#4h_last24h_long_context_upper_cost_usd_raw_median_ema)
- [4h last24h effective cost per total token: raw vs smoothed](#4h_last24h_effective_usd_per_1m_total_tokens_raw_median_ema)
- [4h last24h cost per output token: raw vs smoothed](#4h_last24h_cost_per_1m_output_tokens_raw_median_ema)
- [4h last24h tokens per dollar: raw vs smoothed](#4h_last24h_tokens_per_usd_raw_median_ema)
- [4h last24h cache ratio: raw vs smoothed](#4h_last24h_cache_ratio_raw_median_ema)
- [4h last24h output ratio: raw vs smoothed](#4h_last24h_output_ratio_raw_median_ema)
- [4h last24h reasoning/output ratio: raw vs smoothed](#4h_last24h_reasoning_ratio_raw_median_ema)
- [4h last24h output cost share: raw vs smoothed](#4h_last24h_output_cost_share_raw_median_ema)
- [4h last24h output per input: raw vs smoothed](#4h_last24h_output_per_1m_input_tokens_raw_median_ema)
- [4h last24h reasoning per total: raw vs smoothed](#4h_last24h_reasoning_per_1m_total_tokens_raw_median_ema)
- [4h last24h human-scale pages: raw vs smoothed](#4h_last24h_printed_pages_500w_raw_median_ema)
- [4h last48h total tokens: raw vs smoothed](#4h_last48h_total_tokens_raw_median_ema)
- [4h last48h input tokens: raw vs smoothed](#4h_last48h_input_tokens_raw_median_ema)
- [4h last48h cached input tokens: raw vs smoothed](#4h_last48h_cached_input_tokens_raw_median_ema)
- [4h last48h uncached input tokens: raw vs smoothed](#4h_last48h_uncached_input_tokens_raw_median_ema)
- [4h last48h output tokens: raw vs smoothed](#4h_last48h_output_tokens_raw_median_ema)
- [4h last48h reasoning output tokens: raw vs smoothed](#4h_last48h_reasoning_output_tokens_raw_median_ema)
- [4h last48h GPT-5.5 standard cost: raw vs smoothed](#4h_last48h_cost_usd_raw_median_ema)
- [4h last48h no-cache cost: raw vs smoothed](#4h_last48h_cost_no_cache_usd_raw_median_ema)
- [4h last48h cache savings: raw vs smoothed](#4h_last48h_cache_savings_usd_raw_median_ema)
- [4h last48h long-context upper cost: raw vs smoothed](#4h_last48h_long_context_upper_cost_usd_raw_median_ema)
- [4h last48h effective cost per total token: raw vs smoothed](#4h_last48h_effective_usd_per_1m_total_tokens_raw_median_ema)
- [4h last48h cost per output token: raw vs smoothed](#4h_last48h_cost_per_1m_output_tokens_raw_median_ema)
- [4h last48h tokens per dollar: raw vs smoothed](#4h_last48h_tokens_per_usd_raw_median_ema)
- [4h last48h cache ratio: raw vs smoothed](#4h_last48h_cache_ratio_raw_median_ema)
- [4h last48h output ratio: raw vs smoothed](#4h_last48h_output_ratio_raw_median_ema)
- [4h last48h reasoning/output ratio: raw vs smoothed](#4h_last48h_reasoning_ratio_raw_median_ema)
- [4h last48h output cost share: raw vs smoothed](#4h_last48h_output_cost_share_raw_median_ema)
- [4h last48h output per input: raw vs smoothed](#4h_last48h_output_per_1m_input_tokens_raw_median_ema)
- [4h last48h reasoning per total: raw vs smoothed](#4h_last48h_reasoning_per_1m_total_tokens_raw_median_ema)
- [4h last48h human-scale pages: raw vs smoothed](#4h_last48h_printed_pages_500w_raw_median_ema)
- [4h last72h total tokens: raw vs smoothed](#4h_last72h_total_tokens_raw_median_ema)
- [4h last72h input tokens: raw vs smoothed](#4h_last72h_input_tokens_raw_median_ema)
- [4h last72h cached input tokens: raw vs smoothed](#4h_last72h_cached_input_tokens_raw_median_ema)
- [4h last72h uncached input tokens: raw vs smoothed](#4h_last72h_uncached_input_tokens_raw_median_ema)
- [4h last72h output tokens: raw vs smoothed](#4h_last72h_output_tokens_raw_median_ema)
- [4h last72h reasoning output tokens: raw vs smoothed](#4h_last72h_reasoning_output_tokens_raw_median_ema)
- [4h last72h GPT-5.5 standard cost: raw vs smoothed](#4h_last72h_cost_usd_raw_median_ema)
- [4h last72h no-cache cost: raw vs smoothed](#4h_last72h_cost_no_cache_usd_raw_median_ema)
- [4h last72h cache savings: raw vs smoothed](#4h_last72h_cache_savings_usd_raw_median_ema)
- [4h last72h long-context upper cost: raw vs smoothed](#4h_last72h_long_context_upper_cost_usd_raw_median_ema)
- [4h last72h effective cost per total token: raw vs smoothed](#4h_last72h_effective_usd_per_1m_total_tokens_raw_median_ema)
- [4h last72h cost per output token: raw vs smoothed](#4h_last72h_cost_per_1m_output_tokens_raw_median_ema)
- [4h last72h tokens per dollar: raw vs smoothed](#4h_last72h_tokens_per_usd_raw_median_ema)
- [4h last72h cache ratio: raw vs smoothed](#4h_last72h_cache_ratio_raw_median_ema)
- [4h last72h output ratio: raw vs smoothed](#4h_last72h_output_ratio_raw_median_ema)
- [4h last72h reasoning/output ratio: raw vs smoothed](#4h_last72h_reasoning_ratio_raw_median_ema)
- [4h last72h output cost share: raw vs smoothed](#4h_last72h_output_cost_share_raw_median_ema)
- [4h last72h output per input: raw vs smoothed](#4h_last72h_output_per_1m_input_tokens_raw_median_ema)
- [4h last72h reasoning per total: raw vs smoothed](#4h_last72h_reasoning_per_1m_total_tokens_raw_median_ema)
- [4h last72h human-scale pages: raw vs smoothed](#4h_last72h_printed_pages_500w_raw_median_ema)
- [4h last120h total tokens: raw vs smoothed](#4h_last120h_total_tokens_raw_median_ema)
- [4h last120h input tokens: raw vs smoothed](#4h_last120h_input_tokens_raw_median_ema)
- [4h last120h cached input tokens: raw vs smoothed](#4h_last120h_cached_input_tokens_raw_median_ema)
- [4h last120h uncached input tokens: raw vs smoothed](#4h_last120h_uncached_input_tokens_raw_median_ema)
- [4h last120h output tokens: raw vs smoothed](#4h_last120h_output_tokens_raw_median_ema)
- [4h last120h reasoning output tokens: raw vs smoothed](#4h_last120h_reasoning_output_tokens_raw_median_ema)
- [4h last120h GPT-5.5 standard cost: raw vs smoothed](#4h_last120h_cost_usd_raw_median_ema)
- [4h last120h no-cache cost: raw vs smoothed](#4h_last120h_cost_no_cache_usd_raw_median_ema)
- [4h last120h cache savings: raw vs smoothed](#4h_last120h_cache_savings_usd_raw_median_ema)
- [4h last120h long-context upper cost: raw vs smoothed](#4h_last120h_long_context_upper_cost_usd_raw_median_ema)
- [4h last120h effective cost per total token: raw vs smoothed](#4h_last120h_effective_usd_per_1m_total_tokens_raw_median_ema)
- [4h last120h cost per output token: raw vs smoothed](#4h_last120h_cost_per_1m_output_tokens_raw_median_ema)
- [4h last120h tokens per dollar: raw vs smoothed](#4h_last120h_tokens_per_usd_raw_median_ema)
- [4h last120h cache ratio: raw vs smoothed](#4h_last120h_cache_ratio_raw_median_ema)
- [4h last120h output ratio: raw vs smoothed](#4h_last120h_output_ratio_raw_median_ema)
- [4h last120h reasoning/output ratio: raw vs smoothed](#4h_last120h_reasoning_ratio_raw_median_ema)
- [4h last120h output cost share: raw vs smoothed](#4h_last120h_output_cost_share_raw_median_ema)
- [4h last120h output per input: raw vs smoothed](#4h_last120h_output_per_1m_input_tokens_raw_median_ema)
- [4h last120h reasoning per total: raw vs smoothed](#4h_last120h_reasoning_per_1m_total_tokens_raw_median_ema)
- [4h last120h human-scale pages: raw vs smoothed](#4h_last120h_printed_pages_500w_raw_median_ema)
- [12h all total tokens: raw vs smoothed](#12h_all_total_tokens_raw_median_ema)
- [12h all input tokens: raw vs smoothed](#12h_all_input_tokens_raw_median_ema)
- [12h all cached input tokens: raw vs smoothed](#12h_all_cached_input_tokens_raw_median_ema)
- [12h all uncached input tokens: raw vs smoothed](#12h_all_uncached_input_tokens_raw_median_ema)
- [12h all output tokens: raw vs smoothed](#12h_all_output_tokens_raw_median_ema)
- [12h all reasoning output tokens: raw vs smoothed](#12h_all_reasoning_output_tokens_raw_median_ema)
- [12h all GPT-5.5 standard cost: raw vs smoothed](#12h_all_cost_usd_raw_median_ema)
- [12h all no-cache cost: raw vs smoothed](#12h_all_cost_no_cache_usd_raw_median_ema)
- [12h all cache savings: raw vs smoothed](#12h_all_cache_savings_usd_raw_median_ema)
- [12h all long-context upper cost: raw vs smoothed](#12h_all_long_context_upper_cost_usd_raw_median_ema)
- [12h all effective cost per total token: raw vs smoothed](#12h_all_effective_usd_per_1m_total_tokens_raw_median_ema)
- [12h all cost per output token: raw vs smoothed](#12h_all_cost_per_1m_output_tokens_raw_median_ema)
- [12h all tokens per dollar: raw vs smoothed](#12h_all_tokens_per_usd_raw_median_ema)
- [12h all cache ratio: raw vs smoothed](#12h_all_cache_ratio_raw_median_ema)
- [12h all output ratio: raw vs smoothed](#12h_all_output_ratio_raw_median_ema)
- [12h all reasoning/output ratio: raw vs smoothed](#12h_all_reasoning_ratio_raw_median_ema)
- [12h all output cost share: raw vs smoothed](#12h_all_output_cost_share_raw_median_ema)
- [12h all output per input: raw vs smoothed](#12h_all_output_per_1m_input_tokens_raw_median_ema)
- [12h all reasoning per total: raw vs smoothed](#12h_all_reasoning_per_1m_total_tokens_raw_median_ema)
- [12h all human-scale pages: raw vs smoothed](#12h_all_printed_pages_500w_raw_median_ema)
- [12h last48h total tokens: raw vs smoothed](#12h_last48h_total_tokens_raw_median_ema)
- [12h last48h input tokens: raw vs smoothed](#12h_last48h_input_tokens_raw_median_ema)
- [12h last48h cached input tokens: raw vs smoothed](#12h_last48h_cached_input_tokens_raw_median_ema)
- [12h last48h uncached input tokens: raw vs smoothed](#12h_last48h_uncached_input_tokens_raw_median_ema)
- [12h last48h output tokens: raw vs smoothed](#12h_last48h_output_tokens_raw_median_ema)
- [12h last48h reasoning output tokens: raw vs smoothed](#12h_last48h_reasoning_output_tokens_raw_median_ema)
- [12h last48h GPT-5.5 standard cost: raw vs smoothed](#12h_last48h_cost_usd_raw_median_ema)
- [12h last48h no-cache cost: raw vs smoothed](#12h_last48h_cost_no_cache_usd_raw_median_ema)
- [12h last48h cache savings: raw vs smoothed](#12h_last48h_cache_savings_usd_raw_median_ema)
- [12h last48h long-context upper cost: raw vs smoothed](#12h_last48h_long_context_upper_cost_usd_raw_median_ema)
- [12h last48h effective cost per total token: raw vs smoothed](#12h_last48h_effective_usd_per_1m_total_tokens_raw_median_ema)
- [12h last48h cost per output token: raw vs smoothed](#12h_last48h_cost_per_1m_output_tokens_raw_median_ema)
- [12h last48h tokens per dollar: raw vs smoothed](#12h_last48h_tokens_per_usd_raw_median_ema)
- [12h last48h cache ratio: raw vs smoothed](#12h_last48h_cache_ratio_raw_median_ema)
- [12h last48h output ratio: raw vs smoothed](#12h_last48h_output_ratio_raw_median_ema)
- [12h last48h reasoning/output ratio: raw vs smoothed](#12h_last48h_reasoning_ratio_raw_median_ema)
- [12h last48h output cost share: raw vs smoothed](#12h_last48h_output_cost_share_raw_median_ema)
- [12h last48h output per input: raw vs smoothed](#12h_last48h_output_per_1m_input_tokens_raw_median_ema)
- [12h last48h reasoning per total: raw vs smoothed](#12h_last48h_reasoning_per_1m_total_tokens_raw_median_ema)
- [12h last48h human-scale pages: raw vs smoothed](#12h_last48h_printed_pages_500w_raw_median_ema)
- [12h last72h total tokens: raw vs smoothed](#12h_last72h_total_tokens_raw_median_ema)
- [12h last72h input tokens: raw vs smoothed](#12h_last72h_input_tokens_raw_median_ema)
- [12h last72h cached input tokens: raw vs smoothed](#12h_last72h_cached_input_tokens_raw_median_ema)
- [12h last72h uncached input tokens: raw vs smoothed](#12h_last72h_uncached_input_tokens_raw_median_ema)
- [12h last72h output tokens: raw vs smoothed](#12h_last72h_output_tokens_raw_median_ema)
- [12h last72h reasoning output tokens: raw vs smoothed](#12h_last72h_reasoning_output_tokens_raw_median_ema)
- [12h last72h GPT-5.5 standard cost: raw vs smoothed](#12h_last72h_cost_usd_raw_median_ema)
- [12h last72h no-cache cost: raw vs smoothed](#12h_last72h_cost_no_cache_usd_raw_median_ema)
- [12h last72h cache savings: raw vs smoothed](#12h_last72h_cache_savings_usd_raw_median_ema)
- [12h last72h long-context upper cost: raw vs smoothed](#12h_last72h_long_context_upper_cost_usd_raw_median_ema)
- [12h last72h effective cost per total token: raw vs smoothed](#12h_last72h_effective_usd_per_1m_total_tokens_raw_median_ema)
- [12h last72h cost per output token: raw vs smoothed](#12h_last72h_cost_per_1m_output_tokens_raw_median_ema)
- [12h last72h tokens per dollar: raw vs smoothed](#12h_last72h_tokens_per_usd_raw_median_ema)
- [12h last72h cache ratio: raw vs smoothed](#12h_last72h_cache_ratio_raw_median_ema)
- [12h last72h output ratio: raw vs smoothed](#12h_last72h_output_ratio_raw_median_ema)
- [12h last72h reasoning/output ratio: raw vs smoothed](#12h_last72h_reasoning_ratio_raw_median_ema)
- [12h last72h output cost share: raw vs smoothed](#12h_last72h_output_cost_share_raw_median_ema)
- [12h last72h output per input: raw vs smoothed](#12h_last72h_output_per_1m_input_tokens_raw_median_ema)
- [12h last72h reasoning per total: raw vs smoothed](#12h_last72h_reasoning_per_1m_total_tokens_raw_median_ema)
- [12h last72h human-scale pages: raw vs smoothed](#12h_last72h_printed_pages_500w_raw_median_ema)
- [12h last120h total tokens: raw vs smoothed](#12h_last120h_total_tokens_raw_median_ema)
- [12h last120h input tokens: raw vs smoothed](#12h_last120h_input_tokens_raw_median_ema)
- [12h last120h cached input tokens: raw vs smoothed](#12h_last120h_cached_input_tokens_raw_median_ema)
- [12h last120h uncached input tokens: raw vs smoothed](#12h_last120h_uncached_input_tokens_raw_median_ema)
- [12h last120h output tokens: raw vs smoothed](#12h_last120h_output_tokens_raw_median_ema)
- [12h last120h reasoning output tokens: raw vs smoothed](#12h_last120h_reasoning_output_tokens_raw_median_ema)
- [12h last120h GPT-5.5 standard cost: raw vs smoothed](#12h_last120h_cost_usd_raw_median_ema)
- [12h last120h no-cache cost: raw vs smoothed](#12h_last120h_cost_no_cache_usd_raw_median_ema)
- [12h last120h cache savings: raw vs smoothed](#12h_last120h_cache_savings_usd_raw_median_ema)
- [12h last120h long-context upper cost: raw vs smoothed](#12h_last120h_long_context_upper_cost_usd_raw_median_ema)
- [12h last120h effective cost per total token: raw vs smoothed](#12h_last120h_effective_usd_per_1m_total_tokens_raw_median_ema)
- [12h last120h cost per output token: raw vs smoothed](#12h_last120h_cost_per_1m_output_tokens_raw_median_ema)
- [12h last120h tokens per dollar: raw vs smoothed](#12h_last120h_tokens_per_usd_raw_median_ema)
- [12h last120h cache ratio: raw vs smoothed](#12h_last120h_cache_ratio_raw_median_ema)
- [12h last120h output ratio: raw vs smoothed](#12h_last120h_output_ratio_raw_median_ema)
- [12h last120h reasoning/output ratio: raw vs smoothed](#12h_last120h_reasoning_ratio_raw_median_ema)
- [12h last120h output cost share: raw vs smoothed](#12h_last120h_output_cost_share_raw_median_ema)
- [12h last120h output per input: raw vs smoothed](#12h_last120h_output_per_1m_input_tokens_raw_median_ema)
- [12h last120h reasoning per total: raw vs smoothed](#12h_last120h_reasoning_per_1m_total_tokens_raw_median_ema)
- [12h last120h human-scale pages: raw vs smoothed](#12h_last120h_printed_pages_500w_raw_median_ema)
- [1d all total tokens: raw vs smoothed](#1d_all_total_tokens_raw_median_ema)
- [1d all input tokens: raw vs smoothed](#1d_all_input_tokens_raw_median_ema)
- [1d all cached input tokens: raw vs smoothed](#1d_all_cached_input_tokens_raw_median_ema)
- [1d all uncached input tokens: raw vs smoothed](#1d_all_uncached_input_tokens_raw_median_ema)
- [1d all output tokens: raw vs smoothed](#1d_all_output_tokens_raw_median_ema)
- [1d all reasoning output tokens: raw vs smoothed](#1d_all_reasoning_output_tokens_raw_median_ema)
- [1d all GPT-5.5 standard cost: raw vs smoothed](#1d_all_cost_usd_raw_median_ema)
- [1d all no-cache cost: raw vs smoothed](#1d_all_cost_no_cache_usd_raw_median_ema)
- [1d all cache savings: raw vs smoothed](#1d_all_cache_savings_usd_raw_median_ema)
- [1d all long-context upper cost: raw vs smoothed](#1d_all_long_context_upper_cost_usd_raw_median_ema)
- [1d all effective cost per total token: raw vs smoothed](#1d_all_effective_usd_per_1m_total_tokens_raw_median_ema)
- [1d all cost per output token: raw vs smoothed](#1d_all_cost_per_1m_output_tokens_raw_median_ema)
- [1d all tokens per dollar: raw vs smoothed](#1d_all_tokens_per_usd_raw_median_ema)
- [1d all cache ratio: raw vs smoothed](#1d_all_cache_ratio_raw_median_ema)
- [1d all output ratio: raw vs smoothed](#1d_all_output_ratio_raw_median_ema)
- [1d all reasoning/output ratio: raw vs smoothed](#1d_all_reasoning_ratio_raw_median_ema)
- [1d all output cost share: raw vs smoothed](#1d_all_output_cost_share_raw_median_ema)
- [1d all output per input: raw vs smoothed](#1d_all_output_per_1m_input_tokens_raw_median_ema)
- [1d all reasoning per total: raw vs smoothed](#1d_all_reasoning_per_1m_total_tokens_raw_median_ema)
- [1d all human-scale pages: raw vs smoothed](#1d_all_printed_pages_500w_raw_median_ema)
- [1d last7d total tokens: raw vs smoothed](#1d_last7d_total_tokens_raw_median_ema)
- [1d last7d input tokens: raw vs smoothed](#1d_last7d_input_tokens_raw_median_ema)
- [1d last7d cached input tokens: raw vs smoothed](#1d_last7d_cached_input_tokens_raw_median_ema)
- [1d last7d uncached input tokens: raw vs smoothed](#1d_last7d_uncached_input_tokens_raw_median_ema)
- [1d last7d output tokens: raw vs smoothed](#1d_last7d_output_tokens_raw_median_ema)
- [1d last7d reasoning output tokens: raw vs smoothed](#1d_last7d_reasoning_output_tokens_raw_median_ema)
- [1d last7d GPT-5.5 standard cost: raw vs smoothed](#1d_last7d_cost_usd_raw_median_ema)
- [1d last7d no-cache cost: raw vs smoothed](#1d_last7d_cost_no_cache_usd_raw_median_ema)
- [1d last7d cache savings: raw vs smoothed](#1d_last7d_cache_savings_usd_raw_median_ema)
- [1d last7d long-context upper cost: raw vs smoothed](#1d_last7d_long_context_upper_cost_usd_raw_median_ema)
- [1d last7d effective cost per total token: raw vs smoothed](#1d_last7d_effective_usd_per_1m_total_tokens_raw_median_ema)
- [1d last7d cost per output token: raw vs smoothed](#1d_last7d_cost_per_1m_output_tokens_raw_median_ema)
- [1d last7d tokens per dollar: raw vs smoothed](#1d_last7d_tokens_per_usd_raw_median_ema)
- [1d last7d cache ratio: raw vs smoothed](#1d_last7d_cache_ratio_raw_median_ema)
- [1d last7d output ratio: raw vs smoothed](#1d_last7d_output_ratio_raw_median_ema)
- [1d last7d reasoning/output ratio: raw vs smoothed](#1d_last7d_reasoning_ratio_raw_median_ema)
- [1d last7d output cost share: raw vs smoothed](#1d_last7d_output_cost_share_raw_median_ema)
- [1d last7d output per input: raw vs smoothed](#1d_last7d_output_per_1m_input_tokens_raw_median_ema)
- [1d last7d reasoning per total: raw vs smoothed](#1d_last7d_reasoning_per_1m_total_tokens_raw_median_ema)
- [1d last7d human-scale pages: raw vs smoothed](#1d_last7d_printed_pages_500w_raw_median_ema)
- [1d last14d total tokens: raw vs smoothed](#1d_last14d_total_tokens_raw_median_ema)
- [1d last14d input tokens: raw vs smoothed](#1d_last14d_input_tokens_raw_median_ema)
- [1d last14d cached input tokens: raw vs smoothed](#1d_last14d_cached_input_tokens_raw_median_ema)
- [1d last14d uncached input tokens: raw vs smoothed](#1d_last14d_uncached_input_tokens_raw_median_ema)
- [1d last14d output tokens: raw vs smoothed](#1d_last14d_output_tokens_raw_median_ema)
- [1d last14d reasoning output tokens: raw vs smoothed](#1d_last14d_reasoning_output_tokens_raw_median_ema)
- [1d last14d GPT-5.5 standard cost: raw vs smoothed](#1d_last14d_cost_usd_raw_median_ema)
- [1d last14d no-cache cost: raw vs smoothed](#1d_last14d_cost_no_cache_usd_raw_median_ema)
- [1d last14d cache savings: raw vs smoothed](#1d_last14d_cache_savings_usd_raw_median_ema)
- [1d last14d long-context upper cost: raw vs smoothed](#1d_last14d_long_context_upper_cost_usd_raw_median_ema)
- [1d last14d effective cost per total token: raw vs smoothed](#1d_last14d_effective_usd_per_1m_total_tokens_raw_median_ema)
- [1d last14d cost per output token: raw vs smoothed](#1d_last14d_cost_per_1m_output_tokens_raw_median_ema)
- [1d last14d tokens per dollar: raw vs smoothed](#1d_last14d_tokens_per_usd_raw_median_ema)
- [1d last14d cache ratio: raw vs smoothed](#1d_last14d_cache_ratio_raw_median_ema)
- [1d last14d output ratio: raw vs smoothed](#1d_last14d_output_ratio_raw_median_ema)
- [1d last14d reasoning/output ratio: raw vs smoothed](#1d_last14d_reasoning_ratio_raw_median_ema)
- [1d last14d output cost share: raw vs smoothed](#1d_last14d_output_cost_share_raw_median_ema)
- [1d last14d output per input: raw vs smoothed](#1d_last14d_output_per_1m_input_tokens_raw_median_ema)
- [1d last14d reasoning per total: raw vs smoothed](#1d_last14d_reasoning_per_1m_total_tokens_raw_median_ema)
- [1d last14d human-scale pages: raw vs smoothed](#1d_last14d_printed_pages_500w_raw_median_ema)
- [1d last30d total tokens: raw vs smoothed](#1d_last30d_total_tokens_raw_median_ema)
- [1d last30d input tokens: raw vs smoothed](#1d_last30d_input_tokens_raw_median_ema)
- [1d last30d cached input tokens: raw vs smoothed](#1d_last30d_cached_input_tokens_raw_median_ema)
- [1d last30d uncached input tokens: raw vs smoothed](#1d_last30d_uncached_input_tokens_raw_median_ema)
- [1d last30d output tokens: raw vs smoothed](#1d_last30d_output_tokens_raw_median_ema)
- [1d last30d reasoning output tokens: raw vs smoothed](#1d_last30d_reasoning_output_tokens_raw_median_ema)
- [1d last30d GPT-5.5 standard cost: raw vs smoothed](#1d_last30d_cost_usd_raw_median_ema)
- [1d last30d no-cache cost: raw vs smoothed](#1d_last30d_cost_no_cache_usd_raw_median_ema)
- [1d last30d cache savings: raw vs smoothed](#1d_last30d_cache_savings_usd_raw_median_ema)
- [1d last30d long-context upper cost: raw vs smoothed](#1d_last30d_long_context_upper_cost_usd_raw_median_ema)
- [1d last30d effective cost per total token: raw vs smoothed](#1d_last30d_effective_usd_per_1m_total_tokens_raw_median_ema)
- [1d last30d cost per output token: raw vs smoothed](#1d_last30d_cost_per_1m_output_tokens_raw_median_ema)
- [1d last30d tokens per dollar: raw vs smoothed](#1d_last30d_tokens_per_usd_raw_median_ema)
- [1d last30d cache ratio: raw vs smoothed](#1d_last30d_cache_ratio_raw_median_ema)
- [1d last30d output ratio: raw vs smoothed](#1d_last30d_output_ratio_raw_median_ema)
- [1d last30d reasoning/output ratio: raw vs smoothed](#1d_last30d_reasoning_ratio_raw_median_ema)
- [1d last30d output cost share: raw vs smoothed](#1d_last30d_output_cost_share_raw_median_ema)
- [1d last30d output per input: raw vs smoothed](#1d_last30d_output_per_1m_input_tokens_raw_median_ema)
- [1d last30d reasoning per total: raw vs smoothed](#1d_last30d_reasoning_per_1m_total_tokens_raw_median_ema)
- [1d last30d human-scale pages: raw vs smoothed](#1d_last30d_printed_pages_500w_raw_median_ema)
- [1d last60d total tokens: raw vs smoothed](#1d_last60d_total_tokens_raw_median_ema)
- [1d last60d input tokens: raw vs smoothed](#1d_last60d_input_tokens_raw_median_ema)
- [1d last60d cached input tokens: raw vs smoothed](#1d_last60d_cached_input_tokens_raw_median_ema)
- [1d last60d uncached input tokens: raw vs smoothed](#1d_last60d_uncached_input_tokens_raw_median_ema)
- [1d last60d output tokens: raw vs smoothed](#1d_last60d_output_tokens_raw_median_ema)
- [1d last60d reasoning output tokens: raw vs smoothed](#1d_last60d_reasoning_output_tokens_raw_median_ema)
- [1d last60d GPT-5.5 standard cost: raw vs smoothed](#1d_last60d_cost_usd_raw_median_ema)
- [1d last60d no-cache cost: raw vs smoothed](#1d_last60d_cost_no_cache_usd_raw_median_ema)
- [1d last60d cache savings: raw vs smoothed](#1d_last60d_cache_savings_usd_raw_median_ema)
- [1d last60d long-context upper cost: raw vs smoothed](#1d_last60d_long_context_upper_cost_usd_raw_median_ema)
- [1d last60d effective cost per total token: raw vs smoothed](#1d_last60d_effective_usd_per_1m_total_tokens_raw_median_ema)
- [1d last60d cost per output token: raw vs smoothed](#1d_last60d_cost_per_1m_output_tokens_raw_median_ema)
- [1d last60d tokens per dollar: raw vs smoothed](#1d_last60d_tokens_per_usd_raw_median_ema)
- [1d last60d cache ratio: raw vs smoothed](#1d_last60d_cache_ratio_raw_median_ema)
- [1d last60d output ratio: raw vs smoothed](#1d_last60d_output_ratio_raw_median_ema)
- [1d last60d reasoning/output ratio: raw vs smoothed](#1d_last60d_reasoning_ratio_raw_median_ema)
- [1d last60d output cost share: raw vs smoothed](#1d_last60d_output_cost_share_raw_median_ema)
- [1d last60d output per input: raw vs smoothed](#1d_last60d_output_per_1m_input_tokens_raw_median_ema)
- [1d last60d reasoning per total: raw vs smoothed](#1d_last60d_reasoning_per_1m_total_tokens_raw_median_ema)
- [1d last60d human-scale pages: raw vs smoothed](#1d_last60d_printed_pages_500w_raw_median_ema)

## Charts

### composition

#### 1h_all_io_composition_stack

![1h all input/output composition](MetricChartsDeep/2026-06-06/1h_all_io_composition_stack.png)

Evidence note: Composition stack separates cached, uncached, and output token load.

#### 1h_last24h_io_composition_stack

![1h last24h input/output composition](MetricChartsDeep/2026-06-06/1h_last24h_io_composition_stack.png)

Evidence note: Composition stack separates cached, uncached, and output token load.

#### 1h_last48h_io_composition_stack

![1h last48h input/output composition](MetricChartsDeep/2026-06-06/1h_last48h_io_composition_stack.png)

Evidence note: Composition stack separates cached, uncached, and output token load.

#### 1h_last72h_io_composition_stack

![1h last72h input/output composition](MetricChartsDeep/2026-06-06/1h_last72h_io_composition_stack.png)

Evidence note: Composition stack separates cached, uncached, and output token load.

#### 1h_last96h_io_composition_stack

![1h last96h input/output composition](MetricChartsDeep/2026-06-06/1h_last96h_io_composition_stack.png)

Evidence note: Composition stack separates cached, uncached, and output token load.

#### 4h_all_io_composition_stack

![4h all input/output composition](MetricChartsDeep/2026-06-06/4h_all_io_composition_stack.png)

Evidence note: Composition stack separates cached, uncached, and output token load.

#### 4h_last24h_io_composition_stack

![4h last24h input/output composition](MetricChartsDeep/2026-06-06/4h_last24h_io_composition_stack.png)

Evidence note: Composition stack separates cached, uncached, and output token load.

#### 4h_last48h_io_composition_stack

![4h last48h input/output composition](MetricChartsDeep/2026-06-06/4h_last48h_io_composition_stack.png)

Evidence note: Composition stack separates cached, uncached, and output token load.

#### 4h_last72h_io_composition_stack

![4h last72h input/output composition](MetricChartsDeep/2026-06-06/4h_last72h_io_composition_stack.png)

Evidence note: Composition stack separates cached, uncached, and output token load.

#### 4h_last120h_io_composition_stack

![4h last120h input/output composition](MetricChartsDeep/2026-06-06/4h_last120h_io_composition_stack.png)

Evidence note: Composition stack separates cached, uncached, and output token load.

#### 12h_all_io_composition_stack

![12h all input/output composition](MetricChartsDeep/2026-06-06/12h_all_io_composition_stack.png)

Evidence note: Composition stack separates cached, uncached, and output token load.

#### 12h_last48h_io_composition_stack

![12h last48h input/output composition](MetricChartsDeep/2026-06-06/12h_last48h_io_composition_stack.png)

Evidence note: Composition stack separates cached, uncached, and output token load.

#### 12h_last72h_io_composition_stack

![12h last72h input/output composition](MetricChartsDeep/2026-06-06/12h_last72h_io_composition_stack.png)

Evidence note: Composition stack separates cached, uncached, and output token load.

#### 12h_last120h_io_composition_stack

![12h last120h input/output composition](MetricChartsDeep/2026-06-06/12h_last120h_io_composition_stack.png)

Evidence note: Composition stack separates cached, uncached, and output token load.

#### 1d_all_io_composition_stack

![1d all input/output composition](MetricChartsDeep/2026-06-06/1d_all_io_composition_stack.png)

Evidence note: Composition stack separates cached, uncached, and output token load.

#### 1d_last7d_io_composition_stack

![1d last7d input/output composition](MetricChartsDeep/2026-06-06/1d_last7d_io_composition_stack.png)

Evidence note: Composition stack separates cached, uncached, and output token load.

#### 1d_last14d_io_composition_stack

![1d last14d input/output composition](MetricChartsDeep/2026-06-06/1d_last14d_io_composition_stack.png)

Evidence note: Composition stack separates cached, uncached, and output token load.

#### 1d_last30d_io_composition_stack

![1d last30d input/output composition](MetricChartsDeep/2026-06-06/1d_last30d_io_composition_stack.png)

Evidence note: Composition stack separates cached, uncached, and output token load.

#### 1d_last60d_io_composition_stack

![1d last60d input/output composition](MetricChartsDeep/2026-06-06/1d_last60d_io_composition_stack.png)

Evidence note: Composition stack separates cached, uncached, and output token load.

### cost_bands

#### 1h_all_cost_sensitivity_bands

![1h all cost sensitivity bands](MetricChartsDeep/2026-06-06/1h_all_cost_sensitivity_bands.png)

Evidence note: Sensitivity bands are API-equivalent approximations, not invoice proof.

#### 1h_last24h_cost_sensitivity_bands

![1h last24h cost sensitivity bands](MetricChartsDeep/2026-06-06/1h_last24h_cost_sensitivity_bands.png)

Evidence note: Sensitivity bands are API-equivalent approximations, not invoice proof.

#### 1h_last48h_cost_sensitivity_bands

![1h last48h cost sensitivity bands](MetricChartsDeep/2026-06-06/1h_last48h_cost_sensitivity_bands.png)

Evidence note: Sensitivity bands are API-equivalent approximations, not invoice proof.

#### 1h_last72h_cost_sensitivity_bands

![1h last72h cost sensitivity bands](MetricChartsDeep/2026-06-06/1h_last72h_cost_sensitivity_bands.png)

Evidence note: Sensitivity bands are API-equivalent approximations, not invoice proof.

#### 1h_last96h_cost_sensitivity_bands

![1h last96h cost sensitivity bands](MetricChartsDeep/2026-06-06/1h_last96h_cost_sensitivity_bands.png)

Evidence note: Sensitivity bands are API-equivalent approximations, not invoice proof.

#### 4h_all_cost_sensitivity_bands

![4h all cost sensitivity bands](MetricChartsDeep/2026-06-06/4h_all_cost_sensitivity_bands.png)

Evidence note: Sensitivity bands are API-equivalent approximations, not invoice proof.

#### 4h_last24h_cost_sensitivity_bands

![4h last24h cost sensitivity bands](MetricChartsDeep/2026-06-06/4h_last24h_cost_sensitivity_bands.png)

Evidence note: Sensitivity bands are API-equivalent approximations, not invoice proof.

#### 4h_last48h_cost_sensitivity_bands

![4h last48h cost sensitivity bands](MetricChartsDeep/2026-06-06/4h_last48h_cost_sensitivity_bands.png)

Evidence note: Sensitivity bands are API-equivalent approximations, not invoice proof.

#### 4h_last72h_cost_sensitivity_bands

![4h last72h cost sensitivity bands](MetricChartsDeep/2026-06-06/4h_last72h_cost_sensitivity_bands.png)

Evidence note: Sensitivity bands are API-equivalent approximations, not invoice proof.

#### 4h_last120h_cost_sensitivity_bands

![4h last120h cost sensitivity bands](MetricChartsDeep/2026-06-06/4h_last120h_cost_sensitivity_bands.png)

Evidence note: Sensitivity bands are API-equivalent approximations, not invoice proof.

#### 12h_all_cost_sensitivity_bands

![12h all cost sensitivity bands](MetricChartsDeep/2026-06-06/12h_all_cost_sensitivity_bands.png)

Evidence note: Sensitivity bands are API-equivalent approximations, not invoice proof.

#### 12h_last48h_cost_sensitivity_bands

![12h last48h cost sensitivity bands](MetricChartsDeep/2026-06-06/12h_last48h_cost_sensitivity_bands.png)

Evidence note: Sensitivity bands are API-equivalent approximations, not invoice proof.

#### 12h_last72h_cost_sensitivity_bands

![12h last72h cost sensitivity bands](MetricChartsDeep/2026-06-06/12h_last72h_cost_sensitivity_bands.png)

Evidence note: Sensitivity bands are API-equivalent approximations, not invoice proof.

#### 12h_last120h_cost_sensitivity_bands

![12h last120h cost sensitivity bands](MetricChartsDeep/2026-06-06/12h_last120h_cost_sensitivity_bands.png)

Evidence note: Sensitivity bands are API-equivalent approximations, not invoice proof.

#### 1d_all_cost_sensitivity_bands

![1d all cost sensitivity bands](MetricChartsDeep/2026-06-06/1d_all_cost_sensitivity_bands.png)

Evidence note: Sensitivity bands are API-equivalent approximations, not invoice proof.

#### 1d_last7d_cost_sensitivity_bands

![1d last7d cost sensitivity bands](MetricChartsDeep/2026-06-06/1d_last7d_cost_sensitivity_bands.png)

Evidence note: Sensitivity bands are API-equivalent approximations, not invoice proof.

#### 1d_last14d_cost_sensitivity_bands

![1d last14d cost sensitivity bands](MetricChartsDeep/2026-06-06/1d_last14d_cost_sensitivity_bands.png)

Evidence note: Sensitivity bands are API-equivalent approximations, not invoice proof.

#### 1d_last30d_cost_sensitivity_bands

![1d last30d cost sensitivity bands](MetricChartsDeep/2026-06-06/1d_last30d_cost_sensitivity_bands.png)

Evidence note: Sensitivity bands are API-equivalent approximations, not invoice proof.

#### 1d_last60d_cost_sensitivity_bands

![1d last60d cost sensitivity bands](MetricChartsDeep/2026-06-06/1d_last60d_cost_sensitivity_bands.png)

Evidence note: Sensitivity bands are API-equivalent approximations, not invoice proof.

### distributions

#### 1h_distribution_total_tokens

![1h distribution of total tokens](MetricChartsDeep/2026-06-06/1h_distribution_total_tokens.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 1h_log_distribution_total_tokens

![1h log distribution of total tokens](MetricChartsDeep/2026-06-06/1h_log_distribution_total_tokens.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 1h_distribution_output_tokens

![1h distribution of output tokens](MetricChartsDeep/2026-06-06/1h_distribution_output_tokens.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 1h_log_distribution_output_tokens

![1h log distribution of output tokens](MetricChartsDeep/2026-06-06/1h_log_distribution_output_tokens.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 1h_distribution_reasoning_output_tokens

![1h distribution of reasoning output tokens](MetricChartsDeep/2026-06-06/1h_distribution_reasoning_output_tokens.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 1h_log_distribution_reasoning_output_tokens

![1h log distribution of reasoning output tokens](MetricChartsDeep/2026-06-06/1h_log_distribution_reasoning_output_tokens.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 1h_distribution_cost_usd

![1h distribution of GPT-5.5 standard cost](MetricChartsDeep/2026-06-06/1h_distribution_cost_usd.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 1h_log_distribution_cost_usd

![1h log distribution of GPT-5.5 standard cost](MetricChartsDeep/2026-06-06/1h_log_distribution_cost_usd.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 1h_distribution_cache_savings_usd

![1h distribution of cache savings](MetricChartsDeep/2026-06-06/1h_distribution_cache_savings_usd.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 1h_log_distribution_cache_savings_usd

![1h log distribution of cache savings](MetricChartsDeep/2026-06-06/1h_log_distribution_cache_savings_usd.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 1h_distribution_effective_usd_per_1m_total_tokens

![1h distribution of effective cost per total token](MetricChartsDeep/2026-06-06/1h_distribution_effective_usd_per_1m_total_tokens.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 1h_log_distribution_effective_usd_per_1m_total_tokens

![1h log distribution of effective cost per total token](MetricChartsDeep/2026-06-06/1h_log_distribution_effective_usd_per_1m_total_tokens.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 1h_distribution_cache_ratio

![1h distribution of cache ratio](MetricChartsDeep/2026-06-06/1h_distribution_cache_ratio.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 1h_log_distribution_cache_ratio

![1h log distribution of cache ratio](MetricChartsDeep/2026-06-06/1h_log_distribution_cache_ratio.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 1h_distribution_output_ratio

![1h distribution of output ratio](MetricChartsDeep/2026-06-06/1h_distribution_output_ratio.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 1h_log_distribution_output_ratio

![1h log distribution of output ratio](MetricChartsDeep/2026-06-06/1h_log_distribution_output_ratio.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 1h_distribution_reasoning_ratio

![1h distribution of reasoning/output ratio](MetricChartsDeep/2026-06-06/1h_distribution_reasoning_ratio.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 1h_log_distribution_reasoning_ratio

![1h log distribution of reasoning/output ratio](MetricChartsDeep/2026-06-06/1h_log_distribution_reasoning_ratio.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 4h_distribution_total_tokens

![4h distribution of total tokens](MetricChartsDeep/2026-06-06/4h_distribution_total_tokens.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 4h_log_distribution_total_tokens

![4h log distribution of total tokens](MetricChartsDeep/2026-06-06/4h_log_distribution_total_tokens.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 4h_distribution_output_tokens

![4h distribution of output tokens](MetricChartsDeep/2026-06-06/4h_distribution_output_tokens.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 4h_log_distribution_output_tokens

![4h log distribution of output tokens](MetricChartsDeep/2026-06-06/4h_log_distribution_output_tokens.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 4h_distribution_reasoning_output_tokens

![4h distribution of reasoning output tokens](MetricChartsDeep/2026-06-06/4h_distribution_reasoning_output_tokens.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 4h_log_distribution_reasoning_output_tokens

![4h log distribution of reasoning output tokens](MetricChartsDeep/2026-06-06/4h_log_distribution_reasoning_output_tokens.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 4h_distribution_cost_usd

![4h distribution of GPT-5.5 standard cost](MetricChartsDeep/2026-06-06/4h_distribution_cost_usd.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 4h_log_distribution_cost_usd

![4h log distribution of GPT-5.5 standard cost](MetricChartsDeep/2026-06-06/4h_log_distribution_cost_usd.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 4h_distribution_cache_savings_usd

![4h distribution of cache savings](MetricChartsDeep/2026-06-06/4h_distribution_cache_savings_usd.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 4h_log_distribution_cache_savings_usd

![4h log distribution of cache savings](MetricChartsDeep/2026-06-06/4h_log_distribution_cache_savings_usd.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 4h_distribution_effective_usd_per_1m_total_tokens

![4h distribution of effective cost per total token](MetricChartsDeep/2026-06-06/4h_distribution_effective_usd_per_1m_total_tokens.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 4h_log_distribution_effective_usd_per_1m_total_tokens

![4h log distribution of effective cost per total token](MetricChartsDeep/2026-06-06/4h_log_distribution_effective_usd_per_1m_total_tokens.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 4h_distribution_cost_per_1m_output_tokens

![4h distribution of cost per output token](MetricChartsDeep/2026-06-06/4h_distribution_cost_per_1m_output_tokens.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 4h_log_distribution_cost_per_1m_output_tokens

![4h log distribution of cost per output token](MetricChartsDeep/2026-06-06/4h_log_distribution_cost_per_1m_output_tokens.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 4h_distribution_cache_ratio

![4h distribution of cache ratio](MetricChartsDeep/2026-06-06/4h_distribution_cache_ratio.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 4h_log_distribution_cache_ratio

![4h log distribution of cache ratio](MetricChartsDeep/2026-06-06/4h_log_distribution_cache_ratio.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 4h_distribution_output_ratio

![4h distribution of output ratio](MetricChartsDeep/2026-06-06/4h_distribution_output_ratio.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 4h_log_distribution_output_ratio

![4h log distribution of output ratio](MetricChartsDeep/2026-06-06/4h_log_distribution_output_ratio.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 4h_distribution_reasoning_ratio

![4h distribution of reasoning/output ratio](MetricChartsDeep/2026-06-06/4h_distribution_reasoning_ratio.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 4h_log_distribution_reasoning_ratio

![4h log distribution of reasoning/output ratio](MetricChartsDeep/2026-06-06/4h_log_distribution_reasoning_ratio.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 12h_distribution_total_tokens

![12h distribution of total tokens](MetricChartsDeep/2026-06-06/12h_distribution_total_tokens.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 12h_log_distribution_total_tokens

![12h log distribution of total tokens](MetricChartsDeep/2026-06-06/12h_log_distribution_total_tokens.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 12h_distribution_output_tokens

![12h distribution of output tokens](MetricChartsDeep/2026-06-06/12h_distribution_output_tokens.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 12h_log_distribution_output_tokens

![12h log distribution of output tokens](MetricChartsDeep/2026-06-06/12h_log_distribution_output_tokens.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 12h_distribution_reasoning_output_tokens

![12h distribution of reasoning output tokens](MetricChartsDeep/2026-06-06/12h_distribution_reasoning_output_tokens.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 12h_log_distribution_reasoning_output_tokens

![12h log distribution of reasoning output tokens](MetricChartsDeep/2026-06-06/12h_log_distribution_reasoning_output_tokens.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 12h_distribution_cost_usd

![12h distribution of GPT-5.5 standard cost](MetricChartsDeep/2026-06-06/12h_distribution_cost_usd.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 12h_log_distribution_cost_usd

![12h log distribution of GPT-5.5 standard cost](MetricChartsDeep/2026-06-06/12h_log_distribution_cost_usd.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 12h_distribution_cache_savings_usd

![12h distribution of cache savings](MetricChartsDeep/2026-06-06/12h_distribution_cache_savings_usd.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 12h_log_distribution_cache_savings_usd

![12h log distribution of cache savings](MetricChartsDeep/2026-06-06/12h_log_distribution_cache_savings_usd.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 12h_distribution_effective_usd_per_1m_total_tokens

![12h distribution of effective cost per total token](MetricChartsDeep/2026-06-06/12h_distribution_effective_usd_per_1m_total_tokens.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 12h_log_distribution_effective_usd_per_1m_total_tokens

![12h log distribution of effective cost per total token](MetricChartsDeep/2026-06-06/12h_log_distribution_effective_usd_per_1m_total_tokens.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 12h_distribution_cost_per_1m_output_tokens

![12h distribution of cost per output token](MetricChartsDeep/2026-06-06/12h_distribution_cost_per_1m_output_tokens.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 12h_log_distribution_cost_per_1m_output_tokens

![12h log distribution of cost per output token](MetricChartsDeep/2026-06-06/12h_log_distribution_cost_per_1m_output_tokens.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 12h_distribution_cache_ratio

![12h distribution of cache ratio](MetricChartsDeep/2026-06-06/12h_distribution_cache_ratio.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 12h_log_distribution_cache_ratio

![12h log distribution of cache ratio](MetricChartsDeep/2026-06-06/12h_log_distribution_cache_ratio.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 12h_distribution_output_ratio

![12h distribution of output ratio](MetricChartsDeep/2026-06-06/12h_distribution_output_ratio.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 12h_log_distribution_output_ratio

![12h log distribution of output ratio](MetricChartsDeep/2026-06-06/12h_log_distribution_output_ratio.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 12h_distribution_reasoning_ratio

![12h distribution of reasoning/output ratio](MetricChartsDeep/2026-06-06/12h_distribution_reasoning_ratio.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 12h_log_distribution_reasoning_ratio

![12h log distribution of reasoning/output ratio](MetricChartsDeep/2026-06-06/12h_log_distribution_reasoning_ratio.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 1d_distribution_total_tokens

![1d distribution of total tokens](MetricChartsDeep/2026-06-06/1d_distribution_total_tokens.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 1d_log_distribution_total_tokens

![1d log distribution of total tokens](MetricChartsDeep/2026-06-06/1d_log_distribution_total_tokens.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 1d_distribution_output_tokens

![1d distribution of output tokens](MetricChartsDeep/2026-06-06/1d_distribution_output_tokens.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 1d_log_distribution_output_tokens

![1d log distribution of output tokens](MetricChartsDeep/2026-06-06/1d_log_distribution_output_tokens.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 1d_distribution_reasoning_output_tokens

![1d distribution of reasoning output tokens](MetricChartsDeep/2026-06-06/1d_distribution_reasoning_output_tokens.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 1d_log_distribution_reasoning_output_tokens

![1d log distribution of reasoning output tokens](MetricChartsDeep/2026-06-06/1d_log_distribution_reasoning_output_tokens.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 1d_distribution_cost_usd

![1d distribution of GPT-5.5 standard cost](MetricChartsDeep/2026-06-06/1d_distribution_cost_usd.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 1d_log_distribution_cost_usd

![1d log distribution of GPT-5.5 standard cost](MetricChartsDeep/2026-06-06/1d_log_distribution_cost_usd.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 1d_distribution_cache_savings_usd

![1d distribution of cache savings](MetricChartsDeep/2026-06-06/1d_distribution_cache_savings_usd.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 1d_log_distribution_cache_savings_usd

![1d log distribution of cache savings](MetricChartsDeep/2026-06-06/1d_log_distribution_cache_savings_usd.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 1d_distribution_effective_usd_per_1m_total_tokens

![1d distribution of effective cost per total token](MetricChartsDeep/2026-06-06/1d_distribution_effective_usd_per_1m_total_tokens.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 1d_log_distribution_effective_usd_per_1m_total_tokens

![1d log distribution of effective cost per total token](MetricChartsDeep/2026-06-06/1d_log_distribution_effective_usd_per_1m_total_tokens.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 1d_distribution_cache_ratio

![1d distribution of cache ratio](MetricChartsDeep/2026-06-06/1d_distribution_cache_ratio.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 1d_log_distribution_cache_ratio

![1d log distribution of cache ratio](MetricChartsDeep/2026-06-06/1d_log_distribution_cache_ratio.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 1d_distribution_output_ratio

![1d distribution of output ratio](MetricChartsDeep/2026-06-06/1d_distribution_output_ratio.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 1d_log_distribution_output_ratio

![1d log distribution of output ratio](MetricChartsDeep/2026-06-06/1d_log_distribution_output_ratio.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

#### 1d_distribution_reasoning_ratio

![1d distribution of reasoning/output ratio](MetricChartsDeep/2026-06-06/1d_distribution_reasoning_ratio.png)

Evidence note: Distribution uses non-zero period buckets only.

#### 1d_log_distribution_reasoning_ratio

![1d log distribution of reasoning/output ratio](MetricChartsDeep/2026-06-06/1d_log_distribution_reasoning_ratio.png)

Evidence note: Log-scale distribution preserves outlier visibility without clipping peaks.

### efficiency

#### 1h_all_efficiency_pack

![1h all efficiency pack](MetricChartsDeep/2026-06-06/1h_all_efficiency_pack.png)

Evidence note: Efficiency pack compares output yield, reasoning load, and cost per output token.

#### 1h_last24h_efficiency_pack

![1h last24h efficiency pack](MetricChartsDeep/2026-06-06/1h_last24h_efficiency_pack.png)

Evidence note: Efficiency pack compares output yield, reasoning load, and cost per output token.

#### 1h_last48h_efficiency_pack

![1h last48h efficiency pack](MetricChartsDeep/2026-06-06/1h_last48h_efficiency_pack.png)

Evidence note: Efficiency pack compares output yield, reasoning load, and cost per output token.

#### 1h_last72h_efficiency_pack

![1h last72h efficiency pack](MetricChartsDeep/2026-06-06/1h_last72h_efficiency_pack.png)

Evidence note: Efficiency pack compares output yield, reasoning load, and cost per output token.

#### 1h_last96h_efficiency_pack

![1h last96h efficiency pack](MetricChartsDeep/2026-06-06/1h_last96h_efficiency_pack.png)

Evidence note: Efficiency pack compares output yield, reasoning load, and cost per output token.

#### 4h_all_efficiency_pack

![4h all efficiency pack](MetricChartsDeep/2026-06-06/4h_all_efficiency_pack.png)

Evidence note: Efficiency pack compares output yield, reasoning load, and cost per output token.

#### 4h_last24h_efficiency_pack

![4h last24h efficiency pack](MetricChartsDeep/2026-06-06/4h_last24h_efficiency_pack.png)

Evidence note: Efficiency pack compares output yield, reasoning load, and cost per output token.

#### 4h_last48h_efficiency_pack

![4h last48h efficiency pack](MetricChartsDeep/2026-06-06/4h_last48h_efficiency_pack.png)

Evidence note: Efficiency pack compares output yield, reasoning load, and cost per output token.

#### 4h_last72h_efficiency_pack

![4h last72h efficiency pack](MetricChartsDeep/2026-06-06/4h_last72h_efficiency_pack.png)

Evidence note: Efficiency pack compares output yield, reasoning load, and cost per output token.

#### 4h_last120h_efficiency_pack

![4h last120h efficiency pack](MetricChartsDeep/2026-06-06/4h_last120h_efficiency_pack.png)

Evidence note: Efficiency pack compares output yield, reasoning load, and cost per output token.

#### 12h_all_efficiency_pack

![12h all efficiency pack](MetricChartsDeep/2026-06-06/12h_all_efficiency_pack.png)

Evidence note: Efficiency pack compares output yield, reasoning load, and cost per output token.

#### 12h_last48h_efficiency_pack

![12h last48h efficiency pack](MetricChartsDeep/2026-06-06/12h_last48h_efficiency_pack.png)

Evidence note: Efficiency pack compares output yield, reasoning load, and cost per output token.

#### 12h_last72h_efficiency_pack

![12h last72h efficiency pack](MetricChartsDeep/2026-06-06/12h_last72h_efficiency_pack.png)

Evidence note: Efficiency pack compares output yield, reasoning load, and cost per output token.

#### 12h_last120h_efficiency_pack

![12h last120h efficiency pack](MetricChartsDeep/2026-06-06/12h_last120h_efficiency_pack.png)

Evidence note: Efficiency pack compares output yield, reasoning load, and cost per output token.

#### 1d_all_efficiency_pack

![1d all efficiency pack](MetricChartsDeep/2026-06-06/1d_all_efficiency_pack.png)

Evidence note: Efficiency pack compares output yield, reasoning load, and cost per output token.

#### 1d_last7d_efficiency_pack

![1d last7d efficiency pack](MetricChartsDeep/2026-06-06/1d_last7d_efficiency_pack.png)

Evidence note: Efficiency pack compares output yield, reasoning load, and cost per output token.

#### 1d_last14d_efficiency_pack

![1d last14d efficiency pack](MetricChartsDeep/2026-06-06/1d_last14d_efficiency_pack.png)

Evidence note: Efficiency pack compares output yield, reasoning load, and cost per output token.

#### 1d_last30d_efficiency_pack

![1d last30d efficiency pack](MetricChartsDeep/2026-06-06/1d_last30d_efficiency_pack.png)

Evidence note: Efficiency pack compares output yield, reasoning load, and cost per output token.

#### 1d_last60d_efficiency_pack

![1d last60d efficiency pack](MetricChartsDeep/2026-06-06/1d_last60d_efficiency_pack.png)

Evidence note: Efficiency pack compares output yield, reasoning load, and cost per output token.

### forecast

#### forecast_fan_tokens

![Forecast fan: tokens](MetricChartsDeep/2026-06-06/forecast_fan_tokens.png)

Evidence note: Forecast compares current snapshot velocity with 7-day and 30-day averages.

#### forecast_fan_cost

![Forecast fan: cost](MetricChartsDeep/2026-06-06/forecast_fan_cost.png)

Evidence note: Forecast compares current snapshot velocity with 7-day and 30-day averages.

### heatmaps

#### 1h_day_hour_heatmap_total_tokens

![1h_day_hour heatmap of total tokens](MetricChartsDeep/2026-06-06/1h_day_hour_heatmap_total_tokens.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 1h_weekday_hour_heatmap_total_tokens

![1h_weekday_hour heatmap of total tokens](MetricChartsDeep/2026-06-06/1h_weekday_hour_heatmap_total_tokens.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 4h_day_slot_heatmap_total_tokens

![4h_day_slot heatmap of total tokens](MetricChartsDeep/2026-06-06/4h_day_slot_heatmap_total_tokens.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 12h_day_slot_heatmap_total_tokens

![12h_day_slot heatmap of total tokens](MetricChartsDeep/2026-06-06/12h_day_slot_heatmap_total_tokens.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 1h_day_hour_heatmap_output_tokens

![1h_day_hour heatmap of output tokens](MetricChartsDeep/2026-06-06/1h_day_hour_heatmap_output_tokens.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 1h_weekday_hour_heatmap_output_tokens

![1h_weekday_hour heatmap of output tokens](MetricChartsDeep/2026-06-06/1h_weekday_hour_heatmap_output_tokens.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 4h_day_slot_heatmap_output_tokens

![4h_day_slot heatmap of output tokens](MetricChartsDeep/2026-06-06/4h_day_slot_heatmap_output_tokens.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 12h_day_slot_heatmap_output_tokens

![12h_day_slot heatmap of output tokens](MetricChartsDeep/2026-06-06/12h_day_slot_heatmap_output_tokens.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 1h_day_hour_heatmap_reasoning_output_tokens

![1h_day_hour heatmap of reasoning output tokens](MetricChartsDeep/2026-06-06/1h_day_hour_heatmap_reasoning_output_tokens.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 1h_weekday_hour_heatmap_reasoning_output_tokens

![1h_weekday_hour heatmap of reasoning output tokens](MetricChartsDeep/2026-06-06/1h_weekday_hour_heatmap_reasoning_output_tokens.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 4h_day_slot_heatmap_reasoning_output_tokens

![4h_day_slot heatmap of reasoning output tokens](MetricChartsDeep/2026-06-06/4h_day_slot_heatmap_reasoning_output_tokens.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 12h_day_slot_heatmap_reasoning_output_tokens

![12h_day_slot heatmap of reasoning output tokens](MetricChartsDeep/2026-06-06/12h_day_slot_heatmap_reasoning_output_tokens.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 1h_day_hour_heatmap_cost_usd

![1h_day_hour heatmap of GPT-5.5 standard cost](MetricChartsDeep/2026-06-06/1h_day_hour_heatmap_cost_usd.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 1h_weekday_hour_heatmap_cost_usd

![1h_weekday_hour heatmap of GPT-5.5 standard cost](MetricChartsDeep/2026-06-06/1h_weekday_hour_heatmap_cost_usd.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 4h_day_slot_heatmap_cost_usd

![4h_day_slot heatmap of GPT-5.5 standard cost](MetricChartsDeep/2026-06-06/4h_day_slot_heatmap_cost_usd.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 12h_day_slot_heatmap_cost_usd

![12h_day_slot heatmap of GPT-5.5 standard cost](MetricChartsDeep/2026-06-06/12h_day_slot_heatmap_cost_usd.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 1h_day_hour_heatmap_cost_per_1m_output_tokens

![1h_day_hour heatmap of cost per output token](MetricChartsDeep/2026-06-06/1h_day_hour_heatmap_cost_per_1m_output_tokens.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 1h_weekday_hour_heatmap_cost_per_1m_output_tokens

![1h_weekday_hour heatmap of cost per output token](MetricChartsDeep/2026-06-06/1h_weekday_hour_heatmap_cost_per_1m_output_tokens.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 4h_day_slot_heatmap_cost_per_1m_output_tokens

![4h_day_slot heatmap of cost per output token](MetricChartsDeep/2026-06-06/4h_day_slot_heatmap_cost_per_1m_output_tokens.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 12h_day_slot_heatmap_cost_per_1m_output_tokens

![12h_day_slot heatmap of cost per output token](MetricChartsDeep/2026-06-06/12h_day_slot_heatmap_cost_per_1m_output_tokens.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 1h_day_hour_heatmap_cache_ratio

![1h_day_hour heatmap of cache ratio](MetricChartsDeep/2026-06-06/1h_day_hour_heatmap_cache_ratio.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 1h_weekday_hour_heatmap_cache_ratio

![1h_weekday_hour heatmap of cache ratio](MetricChartsDeep/2026-06-06/1h_weekday_hour_heatmap_cache_ratio.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 4h_day_slot_heatmap_cache_ratio

![4h_day_slot heatmap of cache ratio](MetricChartsDeep/2026-06-06/4h_day_slot_heatmap_cache_ratio.png)

Evidence note: Heatmap aggregates local Samara time buckets.

#### 12h_day_slot_heatmap_cache_ratio

![12h_day_slot heatmap of cache ratio](MetricChartsDeep/2026-06-06/12h_day_slot_heatmap_cache_ratio.png)

Evidence note: Heatmap aggregates local Samara time buckets.

### outliers

#### 1h_outliers_total_tokens_top16

![1h outliers by total tokens](MetricChartsDeep/2026-06-06/1h_outliers_total_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 1h_outliers_input_tokens_top16

![1h outliers by input tokens](MetricChartsDeep/2026-06-06/1h_outliers_input_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 1h_outliers_uncached_input_tokens_top16

![1h outliers by uncached input tokens](MetricChartsDeep/2026-06-06/1h_outliers_uncached_input_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 1h_outliers_output_tokens_top16

![1h outliers by output tokens](MetricChartsDeep/2026-06-06/1h_outliers_output_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 1h_outliers_reasoning_output_tokens_top16

![1h outliers by reasoning output tokens](MetricChartsDeep/2026-06-06/1h_outliers_reasoning_output_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 1h_outliers_cost_usd_top16

![1h outliers by GPT-5.5 standard cost](MetricChartsDeep/2026-06-06/1h_outliers_cost_usd_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 1h_outliers_cost_no_cache_usd_top16

![1h outliers by no-cache cost](MetricChartsDeep/2026-06-06/1h_outliers_cost_no_cache_usd_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 1h_outliers_cache_savings_usd_top16

![1h outliers by cache savings](MetricChartsDeep/2026-06-06/1h_outliers_cache_savings_usd_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 1h_outliers_effective_usd_per_1m_total_tokens_top16

![1h outliers by effective cost per total token](MetricChartsDeep/2026-06-06/1h_outliers_effective_usd_per_1m_total_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 1h_outliers_cost_per_1m_output_tokens_top16

![1h outliers by cost per output token](MetricChartsDeep/2026-06-06/1h_outliers_cost_per_1m_output_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 4h_outliers_total_tokens_top16

![4h outliers by total tokens](MetricChartsDeep/2026-06-06/4h_outliers_total_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 4h_outliers_input_tokens_top16

![4h outliers by input tokens](MetricChartsDeep/2026-06-06/4h_outliers_input_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 4h_outliers_uncached_input_tokens_top16

![4h outliers by uncached input tokens](MetricChartsDeep/2026-06-06/4h_outliers_uncached_input_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 4h_outliers_output_tokens_top16

![4h outliers by output tokens](MetricChartsDeep/2026-06-06/4h_outliers_output_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 4h_outliers_reasoning_output_tokens_top16

![4h outliers by reasoning output tokens](MetricChartsDeep/2026-06-06/4h_outliers_reasoning_output_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 4h_outliers_cost_usd_top16

![4h outliers by GPT-5.5 standard cost](MetricChartsDeep/2026-06-06/4h_outliers_cost_usd_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 4h_outliers_cost_no_cache_usd_top16

![4h outliers by no-cache cost](MetricChartsDeep/2026-06-06/4h_outliers_cost_no_cache_usd_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 4h_outliers_cache_savings_usd_top16

![4h outliers by cache savings](MetricChartsDeep/2026-06-06/4h_outliers_cache_savings_usd_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 4h_outliers_effective_usd_per_1m_total_tokens_top16

![4h outliers by effective cost per total token](MetricChartsDeep/2026-06-06/4h_outliers_effective_usd_per_1m_total_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 4h_outliers_cost_per_1m_output_tokens_top16

![4h outliers by cost per output token](MetricChartsDeep/2026-06-06/4h_outliers_cost_per_1m_output_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 12h_outliers_total_tokens_top16

![12h outliers by total tokens](MetricChartsDeep/2026-06-06/12h_outliers_total_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 12h_outliers_input_tokens_top16

![12h outliers by input tokens](MetricChartsDeep/2026-06-06/12h_outliers_input_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 12h_outliers_uncached_input_tokens_top16

![12h outliers by uncached input tokens](MetricChartsDeep/2026-06-06/12h_outliers_uncached_input_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 12h_outliers_output_tokens_top16

![12h outliers by output tokens](MetricChartsDeep/2026-06-06/12h_outliers_output_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 12h_outliers_reasoning_output_tokens_top16

![12h outliers by reasoning output tokens](MetricChartsDeep/2026-06-06/12h_outliers_reasoning_output_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 12h_outliers_cost_usd_top16

![12h outliers by GPT-5.5 standard cost](MetricChartsDeep/2026-06-06/12h_outliers_cost_usd_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 12h_outliers_cost_no_cache_usd_top16

![12h outliers by no-cache cost](MetricChartsDeep/2026-06-06/12h_outliers_cost_no_cache_usd_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 12h_outliers_cache_savings_usd_top16

![12h outliers by cache savings](MetricChartsDeep/2026-06-06/12h_outliers_cache_savings_usd_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 12h_outliers_effective_usd_per_1m_total_tokens_top16

![12h outliers by effective cost per total token](MetricChartsDeep/2026-06-06/12h_outliers_effective_usd_per_1m_total_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 12h_outliers_cost_per_1m_output_tokens_top16

![12h outliers by cost per output token](MetricChartsDeep/2026-06-06/12h_outliers_cost_per_1m_output_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 1d_outliers_total_tokens_top16

![1d outliers by total tokens](MetricChartsDeep/2026-06-06/1d_outliers_total_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 1d_outliers_input_tokens_top16

![1d outliers by input tokens](MetricChartsDeep/2026-06-06/1d_outliers_input_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 1d_outliers_uncached_input_tokens_top16

![1d outliers by uncached input tokens](MetricChartsDeep/2026-06-06/1d_outliers_uncached_input_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 1d_outliers_output_tokens_top16

![1d outliers by output tokens](MetricChartsDeep/2026-06-06/1d_outliers_output_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 1d_outliers_reasoning_output_tokens_top16

![1d outliers by reasoning output tokens](MetricChartsDeep/2026-06-06/1d_outliers_reasoning_output_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 1d_outliers_cost_usd_top16

![1d outliers by GPT-5.5 standard cost](MetricChartsDeep/2026-06-06/1d_outliers_cost_usd_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 1d_outliers_cost_no_cache_usd_top16

![1d outliers by no-cache cost](MetricChartsDeep/2026-06-06/1d_outliers_cost_no_cache_usd_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 1d_outliers_cache_savings_usd_top16

![1d outliers by cache savings](MetricChartsDeep/2026-06-06/1d_outliers_cache_savings_usd_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 1d_outliers_effective_usd_per_1m_total_tokens_top16

![1d outliers by effective cost per total token](MetricChartsDeep/2026-06-06/1d_outliers_effective_usd_per_1m_total_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

#### 1d_outliers_cost_per_1m_output_tokens_top16

![1d outliers by cost per output token](MetricChartsDeep/2026-06-06/1d_outliers_cost_per_1m_output_tokens_top16.png)

Evidence note: Ranks highest buckets by raw metric value; no smoothing is applied.

### pareto

#### top_sessions_total_tokens_pareto

![Top sessions by total tokens](MetricChartsDeep/2026-06-06/top_sessions_total_tokens_pareto.png)

Evidence note: Pareto chart is limited to rows present in the source report.

#### top_sessions_cost_pareto

![Top sessions by GPT-5.5 standard cost](MetricChartsDeep/2026-06-06/top_sessions_cost_pareto.png)

Evidence note: Pareto chart is limited to rows present in the source report.

#### top_sessions_output_pareto

![Top sessions by output tokens](MetricChartsDeep/2026-06-06/top_sessions_output_pareto.png)

Evidence note: Pareto chart is limited to rows present in the source report.

#### top_sessions_reasoning_pareto

![Top sessions by reasoning output](MetricChartsDeep/2026-06-06/top_sessions_reasoning_pareto.png)

Evidence note: Pareto chart is limited to rows present in the source report.

### ratio_pack

#### 1h_all_ratio_pack

![1h all quality ratios](MetricChartsDeep/2026-06-06/1h_all_ratio_pack.png)

Evidence note: Smoothed ratios show cache health, output yield, reasoning pressure, and output cost share.

#### 1h_last24h_ratio_pack

![1h last24h quality ratios](MetricChartsDeep/2026-06-06/1h_last24h_ratio_pack.png)

Evidence note: Smoothed ratios show cache health, output yield, reasoning pressure, and output cost share.

#### 1h_last48h_ratio_pack

![1h last48h quality ratios](MetricChartsDeep/2026-06-06/1h_last48h_ratio_pack.png)

Evidence note: Smoothed ratios show cache health, output yield, reasoning pressure, and output cost share.

#### 1h_last72h_ratio_pack

![1h last72h quality ratios](MetricChartsDeep/2026-06-06/1h_last72h_ratio_pack.png)

Evidence note: Smoothed ratios show cache health, output yield, reasoning pressure, and output cost share.

#### 1h_last96h_ratio_pack

![1h last96h quality ratios](MetricChartsDeep/2026-06-06/1h_last96h_ratio_pack.png)

Evidence note: Smoothed ratios show cache health, output yield, reasoning pressure, and output cost share.

#### 4h_all_ratio_pack

![4h all quality ratios](MetricChartsDeep/2026-06-06/4h_all_ratio_pack.png)

Evidence note: Smoothed ratios show cache health, output yield, reasoning pressure, and output cost share.

#### 4h_last24h_ratio_pack

![4h last24h quality ratios](MetricChartsDeep/2026-06-06/4h_last24h_ratio_pack.png)

Evidence note: Smoothed ratios show cache health, output yield, reasoning pressure, and output cost share.

#### 4h_last48h_ratio_pack

![4h last48h quality ratios](MetricChartsDeep/2026-06-06/4h_last48h_ratio_pack.png)

Evidence note: Smoothed ratios show cache health, output yield, reasoning pressure, and output cost share.

#### 4h_last72h_ratio_pack

![4h last72h quality ratios](MetricChartsDeep/2026-06-06/4h_last72h_ratio_pack.png)

Evidence note: Smoothed ratios show cache health, output yield, reasoning pressure, and output cost share.

#### 4h_last120h_ratio_pack

![4h last120h quality ratios](MetricChartsDeep/2026-06-06/4h_last120h_ratio_pack.png)

Evidence note: Smoothed ratios show cache health, output yield, reasoning pressure, and output cost share.

#### 12h_all_ratio_pack

![12h all quality ratios](MetricChartsDeep/2026-06-06/12h_all_ratio_pack.png)

Evidence note: Smoothed ratios show cache health, output yield, reasoning pressure, and output cost share.

#### 12h_last48h_ratio_pack

![12h last48h quality ratios](MetricChartsDeep/2026-06-06/12h_last48h_ratio_pack.png)

Evidence note: Smoothed ratios show cache health, output yield, reasoning pressure, and output cost share.

#### 12h_last72h_ratio_pack

![12h last72h quality ratios](MetricChartsDeep/2026-06-06/12h_last72h_ratio_pack.png)

Evidence note: Smoothed ratios show cache health, output yield, reasoning pressure, and output cost share.

#### 12h_last120h_ratio_pack

![12h last120h quality ratios](MetricChartsDeep/2026-06-06/12h_last120h_ratio_pack.png)

Evidence note: Smoothed ratios show cache health, output yield, reasoning pressure, and output cost share.

#### 1d_all_ratio_pack

![1d all quality ratios](MetricChartsDeep/2026-06-06/1d_all_ratio_pack.png)

Evidence note: Smoothed ratios show cache health, output yield, reasoning pressure, and output cost share.

#### 1d_last7d_ratio_pack

![1d last7d quality ratios](MetricChartsDeep/2026-06-06/1d_last7d_ratio_pack.png)

Evidence note: Smoothed ratios show cache health, output yield, reasoning pressure, and output cost share.

#### 1d_last14d_ratio_pack

![1d last14d quality ratios](MetricChartsDeep/2026-06-06/1d_last14d_ratio_pack.png)

Evidence note: Smoothed ratios show cache health, output yield, reasoning pressure, and output cost share.

#### 1d_last30d_ratio_pack

![1d last30d quality ratios](MetricChartsDeep/2026-06-06/1d_last30d_ratio_pack.png)

Evidence note: Smoothed ratios show cache health, output yield, reasoning pressure, and output cost share.

#### 1d_last60d_ratio_pack

![1d last60d quality ratios](MetricChartsDeep/2026-06-06/1d_last60d_ratio_pack.png)

Evidence note: Smoothed ratios show cache health, output yield, reasoning pressure, and output cost share.

### time_series

#### 1h_all_total_tokens_raw_median_ema

![1h all total tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_all_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_all_input_tokens_raw_median_ema

![1h all input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_all_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_all_cached_input_tokens_raw_median_ema

![1h all cached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_all_cached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_all_uncached_input_tokens_raw_median_ema

![1h all uncached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_all_uncached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_all_output_tokens_raw_median_ema

![1h all output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_all_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_all_reasoning_output_tokens_raw_median_ema

![1h all reasoning output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_all_reasoning_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_all_cost_usd_raw_median_ema

![1h all GPT-5.5 standard cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_all_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_all_cost_no_cache_usd_raw_median_ema

![1h all no-cache cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_all_cost_no_cache_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_all_cache_savings_usd_raw_median_ema

![1h all cache savings: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_all_cache_savings_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_all_long_context_upper_cost_usd_raw_median_ema

![1h all long-context upper cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_all_long_context_upper_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_all_effective_usd_per_1m_total_tokens_raw_median_ema

![1h all effective cost per total token: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_all_effective_usd_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_all_cost_per_1m_output_tokens_raw_median_ema

![1h all cost per output token: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_all_cost_per_1m_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_all_tokens_per_usd_raw_median_ema

![1h all tokens per dollar: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_all_tokens_per_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_all_cache_ratio_raw_median_ema

![1h all cache ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_all_cache_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_all_output_ratio_raw_median_ema

![1h all output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_all_output_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_all_reasoning_ratio_raw_median_ema

![1h all reasoning/output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_all_reasoning_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_all_output_cost_share_raw_median_ema

![1h all output cost share: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_all_output_cost_share_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_all_output_per_1m_input_tokens_raw_median_ema

![1h all output per input: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_all_output_per_1m_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_all_reasoning_per_1m_total_tokens_raw_median_ema

![1h all reasoning per total: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_all_reasoning_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_all_printed_pages_500w_raw_median_ema

![1h all human-scale pages: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_all_printed_pages_500w_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last24h_total_tokens_raw_median_ema

![1h last24h total tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last24h_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last24h_input_tokens_raw_median_ema

![1h last24h input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last24h_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last24h_cached_input_tokens_raw_median_ema

![1h last24h cached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last24h_cached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last24h_uncached_input_tokens_raw_median_ema

![1h last24h uncached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last24h_uncached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last24h_output_tokens_raw_median_ema

![1h last24h output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last24h_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last24h_reasoning_output_tokens_raw_median_ema

![1h last24h reasoning output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last24h_reasoning_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last24h_cost_usd_raw_median_ema

![1h last24h GPT-5.5 standard cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last24h_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last24h_cost_no_cache_usd_raw_median_ema

![1h last24h no-cache cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last24h_cost_no_cache_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last24h_cache_savings_usd_raw_median_ema

![1h last24h cache savings: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last24h_cache_savings_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last24h_long_context_upper_cost_usd_raw_median_ema

![1h last24h long-context upper cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last24h_long_context_upper_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last24h_effective_usd_per_1m_total_tokens_raw_median_ema

![1h last24h effective cost per total token: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last24h_effective_usd_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last24h_cost_per_1m_output_tokens_raw_median_ema

![1h last24h cost per output token: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last24h_cost_per_1m_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last24h_tokens_per_usd_raw_median_ema

![1h last24h tokens per dollar: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last24h_tokens_per_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last24h_cache_ratio_raw_median_ema

![1h last24h cache ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last24h_cache_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last24h_output_ratio_raw_median_ema

![1h last24h output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last24h_output_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last24h_reasoning_ratio_raw_median_ema

![1h last24h reasoning/output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last24h_reasoning_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last24h_output_cost_share_raw_median_ema

![1h last24h output cost share: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last24h_output_cost_share_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last24h_output_per_1m_input_tokens_raw_median_ema

![1h last24h output per input: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last24h_output_per_1m_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last24h_reasoning_per_1m_total_tokens_raw_median_ema

![1h last24h reasoning per total: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last24h_reasoning_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last24h_printed_pages_500w_raw_median_ema

![1h last24h human-scale pages: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last24h_printed_pages_500w_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last48h_total_tokens_raw_median_ema

![1h last48h total tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last48h_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last48h_input_tokens_raw_median_ema

![1h last48h input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last48h_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last48h_cached_input_tokens_raw_median_ema

![1h last48h cached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last48h_cached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last48h_uncached_input_tokens_raw_median_ema

![1h last48h uncached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last48h_uncached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last48h_output_tokens_raw_median_ema

![1h last48h output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last48h_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last48h_reasoning_output_tokens_raw_median_ema

![1h last48h reasoning output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last48h_reasoning_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last48h_cost_usd_raw_median_ema

![1h last48h GPT-5.5 standard cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last48h_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last48h_cost_no_cache_usd_raw_median_ema

![1h last48h no-cache cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last48h_cost_no_cache_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last48h_cache_savings_usd_raw_median_ema

![1h last48h cache savings: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last48h_cache_savings_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last48h_long_context_upper_cost_usd_raw_median_ema

![1h last48h long-context upper cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last48h_long_context_upper_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last48h_effective_usd_per_1m_total_tokens_raw_median_ema

![1h last48h effective cost per total token: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last48h_effective_usd_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last48h_cost_per_1m_output_tokens_raw_median_ema

![1h last48h cost per output token: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last48h_cost_per_1m_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last48h_tokens_per_usd_raw_median_ema

![1h last48h tokens per dollar: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last48h_tokens_per_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last48h_cache_ratio_raw_median_ema

![1h last48h cache ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last48h_cache_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last48h_output_ratio_raw_median_ema

![1h last48h output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last48h_output_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last48h_reasoning_ratio_raw_median_ema

![1h last48h reasoning/output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last48h_reasoning_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last48h_output_cost_share_raw_median_ema

![1h last48h output cost share: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last48h_output_cost_share_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last48h_output_per_1m_input_tokens_raw_median_ema

![1h last48h output per input: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last48h_output_per_1m_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last48h_reasoning_per_1m_total_tokens_raw_median_ema

![1h last48h reasoning per total: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last48h_reasoning_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last48h_printed_pages_500w_raw_median_ema

![1h last48h human-scale pages: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last48h_printed_pages_500w_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last72h_total_tokens_raw_median_ema

![1h last72h total tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last72h_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last72h_input_tokens_raw_median_ema

![1h last72h input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last72h_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last72h_cached_input_tokens_raw_median_ema

![1h last72h cached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last72h_cached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last72h_uncached_input_tokens_raw_median_ema

![1h last72h uncached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last72h_uncached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last72h_output_tokens_raw_median_ema

![1h last72h output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last72h_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last72h_reasoning_output_tokens_raw_median_ema

![1h last72h reasoning output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last72h_reasoning_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last72h_cost_usd_raw_median_ema

![1h last72h GPT-5.5 standard cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last72h_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last72h_cost_no_cache_usd_raw_median_ema

![1h last72h no-cache cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last72h_cost_no_cache_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last72h_cache_savings_usd_raw_median_ema

![1h last72h cache savings: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last72h_cache_savings_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last72h_long_context_upper_cost_usd_raw_median_ema

![1h last72h long-context upper cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last72h_long_context_upper_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last72h_effective_usd_per_1m_total_tokens_raw_median_ema

![1h last72h effective cost per total token: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last72h_effective_usd_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last72h_cost_per_1m_output_tokens_raw_median_ema

![1h last72h cost per output token: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last72h_cost_per_1m_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last72h_tokens_per_usd_raw_median_ema

![1h last72h tokens per dollar: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last72h_tokens_per_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last72h_cache_ratio_raw_median_ema

![1h last72h cache ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last72h_cache_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last72h_output_ratio_raw_median_ema

![1h last72h output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last72h_output_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last72h_reasoning_ratio_raw_median_ema

![1h last72h reasoning/output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last72h_reasoning_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last72h_output_cost_share_raw_median_ema

![1h last72h output cost share: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last72h_output_cost_share_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last72h_output_per_1m_input_tokens_raw_median_ema

![1h last72h output per input: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last72h_output_per_1m_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last72h_reasoning_per_1m_total_tokens_raw_median_ema

![1h last72h reasoning per total: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last72h_reasoning_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last72h_printed_pages_500w_raw_median_ema

![1h last72h human-scale pages: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last72h_printed_pages_500w_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last96h_total_tokens_raw_median_ema

![1h last96h total tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last96h_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last96h_input_tokens_raw_median_ema

![1h last96h input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last96h_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last96h_cached_input_tokens_raw_median_ema

![1h last96h cached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last96h_cached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last96h_uncached_input_tokens_raw_median_ema

![1h last96h uncached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last96h_uncached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last96h_output_tokens_raw_median_ema

![1h last96h output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last96h_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last96h_reasoning_output_tokens_raw_median_ema

![1h last96h reasoning output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last96h_reasoning_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last96h_cost_usd_raw_median_ema

![1h last96h GPT-5.5 standard cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last96h_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last96h_cost_no_cache_usd_raw_median_ema

![1h last96h no-cache cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last96h_cost_no_cache_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last96h_cache_savings_usd_raw_median_ema

![1h last96h cache savings: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last96h_cache_savings_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last96h_long_context_upper_cost_usd_raw_median_ema

![1h last96h long-context upper cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last96h_long_context_upper_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last96h_effective_usd_per_1m_total_tokens_raw_median_ema

![1h last96h effective cost per total token: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last96h_effective_usd_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last96h_cost_per_1m_output_tokens_raw_median_ema

![1h last96h cost per output token: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last96h_cost_per_1m_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last96h_tokens_per_usd_raw_median_ema

![1h last96h tokens per dollar: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last96h_tokens_per_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last96h_cache_ratio_raw_median_ema

![1h last96h cache ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last96h_cache_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last96h_output_ratio_raw_median_ema

![1h last96h output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last96h_output_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last96h_reasoning_ratio_raw_median_ema

![1h last96h reasoning/output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last96h_reasoning_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last96h_output_cost_share_raw_median_ema

![1h last96h output cost share: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last96h_output_cost_share_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last96h_output_per_1m_input_tokens_raw_median_ema

![1h last96h output per input: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last96h_output_per_1m_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last96h_reasoning_per_1m_total_tokens_raw_median_ema

![1h last96h reasoning per total: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last96h_reasoning_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1h_last96h_printed_pages_500w_raw_median_ema

![1h last96h human-scale pages: raw vs smoothed](MetricChartsDeep/2026-06-06/1h_last96h_printed_pages_500w_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_all_total_tokens_raw_median_ema

![4h all total tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_all_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_all_input_tokens_raw_median_ema

![4h all input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_all_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_all_cached_input_tokens_raw_median_ema

![4h all cached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_all_cached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_all_uncached_input_tokens_raw_median_ema

![4h all uncached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_all_uncached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_all_output_tokens_raw_median_ema

![4h all output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_all_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_all_reasoning_output_tokens_raw_median_ema

![4h all reasoning output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_all_reasoning_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_all_cost_usd_raw_median_ema

![4h all GPT-5.5 standard cost: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_all_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_all_cost_no_cache_usd_raw_median_ema

![4h all no-cache cost: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_all_cost_no_cache_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_all_cache_savings_usd_raw_median_ema

![4h all cache savings: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_all_cache_savings_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_all_long_context_upper_cost_usd_raw_median_ema

![4h all long-context upper cost: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_all_long_context_upper_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_all_effective_usd_per_1m_total_tokens_raw_median_ema

![4h all effective cost per total token: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_all_effective_usd_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_all_cost_per_1m_output_tokens_raw_median_ema

![4h all cost per output token: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_all_cost_per_1m_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_all_tokens_per_usd_raw_median_ema

![4h all tokens per dollar: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_all_tokens_per_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_all_cache_ratio_raw_median_ema

![4h all cache ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_all_cache_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_all_output_ratio_raw_median_ema

![4h all output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_all_output_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_all_reasoning_ratio_raw_median_ema

![4h all reasoning/output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_all_reasoning_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_all_output_cost_share_raw_median_ema

![4h all output cost share: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_all_output_cost_share_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_all_output_per_1m_input_tokens_raw_median_ema

![4h all output per input: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_all_output_per_1m_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_all_reasoning_per_1m_total_tokens_raw_median_ema

![4h all reasoning per total: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_all_reasoning_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_all_printed_pages_500w_raw_median_ema

![4h all human-scale pages: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_all_printed_pages_500w_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last24h_total_tokens_raw_median_ema

![4h last24h total tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last24h_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last24h_input_tokens_raw_median_ema

![4h last24h input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last24h_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last24h_cached_input_tokens_raw_median_ema

![4h last24h cached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last24h_cached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last24h_uncached_input_tokens_raw_median_ema

![4h last24h uncached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last24h_uncached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last24h_output_tokens_raw_median_ema

![4h last24h output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last24h_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last24h_reasoning_output_tokens_raw_median_ema

![4h last24h reasoning output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last24h_reasoning_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last24h_cost_usd_raw_median_ema

![4h last24h GPT-5.5 standard cost: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last24h_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last24h_cost_no_cache_usd_raw_median_ema

![4h last24h no-cache cost: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last24h_cost_no_cache_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last24h_cache_savings_usd_raw_median_ema

![4h last24h cache savings: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last24h_cache_savings_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last24h_long_context_upper_cost_usd_raw_median_ema

![4h last24h long-context upper cost: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last24h_long_context_upper_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last24h_effective_usd_per_1m_total_tokens_raw_median_ema

![4h last24h effective cost per total token: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last24h_effective_usd_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last24h_cost_per_1m_output_tokens_raw_median_ema

![4h last24h cost per output token: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last24h_cost_per_1m_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last24h_tokens_per_usd_raw_median_ema

![4h last24h tokens per dollar: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last24h_tokens_per_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last24h_cache_ratio_raw_median_ema

![4h last24h cache ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last24h_cache_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last24h_output_ratio_raw_median_ema

![4h last24h output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last24h_output_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last24h_reasoning_ratio_raw_median_ema

![4h last24h reasoning/output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last24h_reasoning_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last24h_output_cost_share_raw_median_ema

![4h last24h output cost share: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last24h_output_cost_share_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last24h_output_per_1m_input_tokens_raw_median_ema

![4h last24h output per input: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last24h_output_per_1m_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last24h_reasoning_per_1m_total_tokens_raw_median_ema

![4h last24h reasoning per total: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last24h_reasoning_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last24h_printed_pages_500w_raw_median_ema

![4h last24h human-scale pages: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last24h_printed_pages_500w_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last48h_total_tokens_raw_median_ema

![4h last48h total tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last48h_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last48h_input_tokens_raw_median_ema

![4h last48h input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last48h_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last48h_cached_input_tokens_raw_median_ema

![4h last48h cached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last48h_cached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last48h_uncached_input_tokens_raw_median_ema

![4h last48h uncached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last48h_uncached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last48h_output_tokens_raw_median_ema

![4h last48h output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last48h_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last48h_reasoning_output_tokens_raw_median_ema

![4h last48h reasoning output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last48h_reasoning_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last48h_cost_usd_raw_median_ema

![4h last48h GPT-5.5 standard cost: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last48h_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last48h_cost_no_cache_usd_raw_median_ema

![4h last48h no-cache cost: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last48h_cost_no_cache_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last48h_cache_savings_usd_raw_median_ema

![4h last48h cache savings: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last48h_cache_savings_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last48h_long_context_upper_cost_usd_raw_median_ema

![4h last48h long-context upper cost: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last48h_long_context_upper_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last48h_effective_usd_per_1m_total_tokens_raw_median_ema

![4h last48h effective cost per total token: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last48h_effective_usd_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last48h_cost_per_1m_output_tokens_raw_median_ema

![4h last48h cost per output token: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last48h_cost_per_1m_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last48h_tokens_per_usd_raw_median_ema

![4h last48h tokens per dollar: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last48h_tokens_per_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last48h_cache_ratio_raw_median_ema

![4h last48h cache ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last48h_cache_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last48h_output_ratio_raw_median_ema

![4h last48h output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last48h_output_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last48h_reasoning_ratio_raw_median_ema

![4h last48h reasoning/output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last48h_reasoning_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last48h_output_cost_share_raw_median_ema

![4h last48h output cost share: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last48h_output_cost_share_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last48h_output_per_1m_input_tokens_raw_median_ema

![4h last48h output per input: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last48h_output_per_1m_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last48h_reasoning_per_1m_total_tokens_raw_median_ema

![4h last48h reasoning per total: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last48h_reasoning_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last48h_printed_pages_500w_raw_median_ema

![4h last48h human-scale pages: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last48h_printed_pages_500w_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last72h_total_tokens_raw_median_ema

![4h last72h total tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last72h_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last72h_input_tokens_raw_median_ema

![4h last72h input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last72h_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last72h_cached_input_tokens_raw_median_ema

![4h last72h cached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last72h_cached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last72h_uncached_input_tokens_raw_median_ema

![4h last72h uncached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last72h_uncached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last72h_output_tokens_raw_median_ema

![4h last72h output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last72h_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last72h_reasoning_output_tokens_raw_median_ema

![4h last72h reasoning output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last72h_reasoning_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last72h_cost_usd_raw_median_ema

![4h last72h GPT-5.5 standard cost: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last72h_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last72h_cost_no_cache_usd_raw_median_ema

![4h last72h no-cache cost: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last72h_cost_no_cache_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last72h_cache_savings_usd_raw_median_ema

![4h last72h cache savings: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last72h_cache_savings_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last72h_long_context_upper_cost_usd_raw_median_ema

![4h last72h long-context upper cost: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last72h_long_context_upper_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last72h_effective_usd_per_1m_total_tokens_raw_median_ema

![4h last72h effective cost per total token: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last72h_effective_usd_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last72h_cost_per_1m_output_tokens_raw_median_ema

![4h last72h cost per output token: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last72h_cost_per_1m_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last72h_tokens_per_usd_raw_median_ema

![4h last72h tokens per dollar: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last72h_tokens_per_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last72h_cache_ratio_raw_median_ema

![4h last72h cache ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last72h_cache_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last72h_output_ratio_raw_median_ema

![4h last72h output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last72h_output_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last72h_reasoning_ratio_raw_median_ema

![4h last72h reasoning/output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last72h_reasoning_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last72h_output_cost_share_raw_median_ema

![4h last72h output cost share: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last72h_output_cost_share_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last72h_output_per_1m_input_tokens_raw_median_ema

![4h last72h output per input: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last72h_output_per_1m_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last72h_reasoning_per_1m_total_tokens_raw_median_ema

![4h last72h reasoning per total: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last72h_reasoning_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last72h_printed_pages_500w_raw_median_ema

![4h last72h human-scale pages: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last72h_printed_pages_500w_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last120h_total_tokens_raw_median_ema

![4h last120h total tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last120h_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last120h_input_tokens_raw_median_ema

![4h last120h input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last120h_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last120h_cached_input_tokens_raw_median_ema

![4h last120h cached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last120h_cached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last120h_uncached_input_tokens_raw_median_ema

![4h last120h uncached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last120h_uncached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last120h_output_tokens_raw_median_ema

![4h last120h output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last120h_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last120h_reasoning_output_tokens_raw_median_ema

![4h last120h reasoning output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last120h_reasoning_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last120h_cost_usd_raw_median_ema

![4h last120h GPT-5.5 standard cost: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last120h_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last120h_cost_no_cache_usd_raw_median_ema

![4h last120h no-cache cost: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last120h_cost_no_cache_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last120h_cache_savings_usd_raw_median_ema

![4h last120h cache savings: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last120h_cache_savings_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last120h_long_context_upper_cost_usd_raw_median_ema

![4h last120h long-context upper cost: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last120h_long_context_upper_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last120h_effective_usd_per_1m_total_tokens_raw_median_ema

![4h last120h effective cost per total token: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last120h_effective_usd_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last120h_cost_per_1m_output_tokens_raw_median_ema

![4h last120h cost per output token: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last120h_cost_per_1m_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last120h_tokens_per_usd_raw_median_ema

![4h last120h tokens per dollar: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last120h_tokens_per_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last120h_cache_ratio_raw_median_ema

![4h last120h cache ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last120h_cache_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last120h_output_ratio_raw_median_ema

![4h last120h output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last120h_output_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last120h_reasoning_ratio_raw_median_ema

![4h last120h reasoning/output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last120h_reasoning_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last120h_output_cost_share_raw_median_ema

![4h last120h output cost share: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last120h_output_cost_share_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last120h_output_per_1m_input_tokens_raw_median_ema

![4h last120h output per input: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last120h_output_per_1m_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last120h_reasoning_per_1m_total_tokens_raw_median_ema

![4h last120h reasoning per total: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last120h_reasoning_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 4h_last120h_printed_pages_500w_raw_median_ema

![4h last120h human-scale pages: raw vs smoothed](MetricChartsDeep/2026-06-06/4h_last120h_printed_pages_500w_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_all_total_tokens_raw_median_ema

![12h all total tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_all_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_all_input_tokens_raw_median_ema

![12h all input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_all_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_all_cached_input_tokens_raw_median_ema

![12h all cached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_all_cached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_all_uncached_input_tokens_raw_median_ema

![12h all uncached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_all_uncached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_all_output_tokens_raw_median_ema

![12h all output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_all_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_all_reasoning_output_tokens_raw_median_ema

![12h all reasoning output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_all_reasoning_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_all_cost_usd_raw_median_ema

![12h all GPT-5.5 standard cost: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_all_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_all_cost_no_cache_usd_raw_median_ema

![12h all no-cache cost: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_all_cost_no_cache_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_all_cache_savings_usd_raw_median_ema

![12h all cache savings: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_all_cache_savings_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_all_long_context_upper_cost_usd_raw_median_ema

![12h all long-context upper cost: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_all_long_context_upper_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_all_effective_usd_per_1m_total_tokens_raw_median_ema

![12h all effective cost per total token: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_all_effective_usd_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_all_cost_per_1m_output_tokens_raw_median_ema

![12h all cost per output token: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_all_cost_per_1m_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_all_tokens_per_usd_raw_median_ema

![12h all tokens per dollar: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_all_tokens_per_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_all_cache_ratio_raw_median_ema

![12h all cache ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_all_cache_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_all_output_ratio_raw_median_ema

![12h all output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_all_output_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_all_reasoning_ratio_raw_median_ema

![12h all reasoning/output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_all_reasoning_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_all_output_cost_share_raw_median_ema

![12h all output cost share: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_all_output_cost_share_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_all_output_per_1m_input_tokens_raw_median_ema

![12h all output per input: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_all_output_per_1m_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_all_reasoning_per_1m_total_tokens_raw_median_ema

![12h all reasoning per total: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_all_reasoning_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_all_printed_pages_500w_raw_median_ema

![12h all human-scale pages: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_all_printed_pages_500w_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last48h_total_tokens_raw_median_ema

![12h last48h total tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last48h_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last48h_input_tokens_raw_median_ema

![12h last48h input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last48h_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last48h_cached_input_tokens_raw_median_ema

![12h last48h cached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last48h_cached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last48h_uncached_input_tokens_raw_median_ema

![12h last48h uncached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last48h_uncached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last48h_output_tokens_raw_median_ema

![12h last48h output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last48h_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last48h_reasoning_output_tokens_raw_median_ema

![12h last48h reasoning output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last48h_reasoning_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last48h_cost_usd_raw_median_ema

![12h last48h GPT-5.5 standard cost: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last48h_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last48h_cost_no_cache_usd_raw_median_ema

![12h last48h no-cache cost: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last48h_cost_no_cache_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last48h_cache_savings_usd_raw_median_ema

![12h last48h cache savings: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last48h_cache_savings_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last48h_long_context_upper_cost_usd_raw_median_ema

![12h last48h long-context upper cost: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last48h_long_context_upper_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last48h_effective_usd_per_1m_total_tokens_raw_median_ema

![12h last48h effective cost per total token: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last48h_effective_usd_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last48h_cost_per_1m_output_tokens_raw_median_ema

![12h last48h cost per output token: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last48h_cost_per_1m_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last48h_tokens_per_usd_raw_median_ema

![12h last48h tokens per dollar: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last48h_tokens_per_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last48h_cache_ratio_raw_median_ema

![12h last48h cache ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last48h_cache_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last48h_output_ratio_raw_median_ema

![12h last48h output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last48h_output_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last48h_reasoning_ratio_raw_median_ema

![12h last48h reasoning/output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last48h_reasoning_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last48h_output_cost_share_raw_median_ema

![12h last48h output cost share: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last48h_output_cost_share_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last48h_output_per_1m_input_tokens_raw_median_ema

![12h last48h output per input: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last48h_output_per_1m_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last48h_reasoning_per_1m_total_tokens_raw_median_ema

![12h last48h reasoning per total: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last48h_reasoning_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last48h_printed_pages_500w_raw_median_ema

![12h last48h human-scale pages: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last48h_printed_pages_500w_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last72h_total_tokens_raw_median_ema

![12h last72h total tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last72h_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last72h_input_tokens_raw_median_ema

![12h last72h input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last72h_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last72h_cached_input_tokens_raw_median_ema

![12h last72h cached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last72h_cached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last72h_uncached_input_tokens_raw_median_ema

![12h last72h uncached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last72h_uncached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last72h_output_tokens_raw_median_ema

![12h last72h output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last72h_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last72h_reasoning_output_tokens_raw_median_ema

![12h last72h reasoning output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last72h_reasoning_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last72h_cost_usd_raw_median_ema

![12h last72h GPT-5.5 standard cost: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last72h_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last72h_cost_no_cache_usd_raw_median_ema

![12h last72h no-cache cost: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last72h_cost_no_cache_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last72h_cache_savings_usd_raw_median_ema

![12h last72h cache savings: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last72h_cache_savings_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last72h_long_context_upper_cost_usd_raw_median_ema

![12h last72h long-context upper cost: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last72h_long_context_upper_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last72h_effective_usd_per_1m_total_tokens_raw_median_ema

![12h last72h effective cost per total token: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last72h_effective_usd_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last72h_cost_per_1m_output_tokens_raw_median_ema

![12h last72h cost per output token: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last72h_cost_per_1m_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last72h_tokens_per_usd_raw_median_ema

![12h last72h tokens per dollar: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last72h_tokens_per_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last72h_cache_ratio_raw_median_ema

![12h last72h cache ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last72h_cache_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last72h_output_ratio_raw_median_ema

![12h last72h output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last72h_output_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last72h_reasoning_ratio_raw_median_ema

![12h last72h reasoning/output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last72h_reasoning_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last72h_output_cost_share_raw_median_ema

![12h last72h output cost share: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last72h_output_cost_share_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last72h_output_per_1m_input_tokens_raw_median_ema

![12h last72h output per input: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last72h_output_per_1m_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last72h_reasoning_per_1m_total_tokens_raw_median_ema

![12h last72h reasoning per total: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last72h_reasoning_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last72h_printed_pages_500w_raw_median_ema

![12h last72h human-scale pages: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last72h_printed_pages_500w_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last120h_total_tokens_raw_median_ema

![12h last120h total tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last120h_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last120h_input_tokens_raw_median_ema

![12h last120h input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last120h_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last120h_cached_input_tokens_raw_median_ema

![12h last120h cached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last120h_cached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last120h_uncached_input_tokens_raw_median_ema

![12h last120h uncached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last120h_uncached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last120h_output_tokens_raw_median_ema

![12h last120h output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last120h_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last120h_reasoning_output_tokens_raw_median_ema

![12h last120h reasoning output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last120h_reasoning_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last120h_cost_usd_raw_median_ema

![12h last120h GPT-5.5 standard cost: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last120h_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last120h_cost_no_cache_usd_raw_median_ema

![12h last120h no-cache cost: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last120h_cost_no_cache_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last120h_cache_savings_usd_raw_median_ema

![12h last120h cache savings: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last120h_cache_savings_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last120h_long_context_upper_cost_usd_raw_median_ema

![12h last120h long-context upper cost: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last120h_long_context_upper_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last120h_effective_usd_per_1m_total_tokens_raw_median_ema

![12h last120h effective cost per total token: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last120h_effective_usd_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last120h_cost_per_1m_output_tokens_raw_median_ema

![12h last120h cost per output token: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last120h_cost_per_1m_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last120h_tokens_per_usd_raw_median_ema

![12h last120h tokens per dollar: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last120h_tokens_per_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last120h_cache_ratio_raw_median_ema

![12h last120h cache ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last120h_cache_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last120h_output_ratio_raw_median_ema

![12h last120h output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last120h_output_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last120h_reasoning_ratio_raw_median_ema

![12h last120h reasoning/output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last120h_reasoning_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last120h_output_cost_share_raw_median_ema

![12h last120h output cost share: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last120h_output_cost_share_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last120h_output_per_1m_input_tokens_raw_median_ema

![12h last120h output per input: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last120h_output_per_1m_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last120h_reasoning_per_1m_total_tokens_raw_median_ema

![12h last120h reasoning per total: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last120h_reasoning_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 12h_last120h_printed_pages_500w_raw_median_ema

![12h last120h human-scale pages: raw vs smoothed](MetricChartsDeep/2026-06-06/12h_last120h_printed_pages_500w_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_all_total_tokens_raw_median_ema

![1d all total tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_all_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_all_input_tokens_raw_median_ema

![1d all input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_all_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_all_cached_input_tokens_raw_median_ema

![1d all cached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_all_cached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_all_uncached_input_tokens_raw_median_ema

![1d all uncached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_all_uncached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_all_output_tokens_raw_median_ema

![1d all output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_all_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_all_reasoning_output_tokens_raw_median_ema

![1d all reasoning output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_all_reasoning_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_all_cost_usd_raw_median_ema

![1d all GPT-5.5 standard cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_all_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_all_cost_no_cache_usd_raw_median_ema

![1d all no-cache cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_all_cost_no_cache_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_all_cache_savings_usd_raw_median_ema

![1d all cache savings: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_all_cache_savings_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_all_long_context_upper_cost_usd_raw_median_ema

![1d all long-context upper cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_all_long_context_upper_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_all_effective_usd_per_1m_total_tokens_raw_median_ema

![1d all effective cost per total token: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_all_effective_usd_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_all_cost_per_1m_output_tokens_raw_median_ema

![1d all cost per output token: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_all_cost_per_1m_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_all_tokens_per_usd_raw_median_ema

![1d all tokens per dollar: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_all_tokens_per_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_all_cache_ratio_raw_median_ema

![1d all cache ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_all_cache_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_all_output_ratio_raw_median_ema

![1d all output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_all_output_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_all_reasoning_ratio_raw_median_ema

![1d all reasoning/output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_all_reasoning_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_all_output_cost_share_raw_median_ema

![1d all output cost share: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_all_output_cost_share_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_all_output_per_1m_input_tokens_raw_median_ema

![1d all output per input: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_all_output_per_1m_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_all_reasoning_per_1m_total_tokens_raw_median_ema

![1d all reasoning per total: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_all_reasoning_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_all_printed_pages_500w_raw_median_ema

![1d all human-scale pages: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_all_printed_pages_500w_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last7d_total_tokens_raw_median_ema

![1d last7d total tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last7d_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last7d_input_tokens_raw_median_ema

![1d last7d input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last7d_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last7d_cached_input_tokens_raw_median_ema

![1d last7d cached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last7d_cached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last7d_uncached_input_tokens_raw_median_ema

![1d last7d uncached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last7d_uncached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last7d_output_tokens_raw_median_ema

![1d last7d output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last7d_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last7d_reasoning_output_tokens_raw_median_ema

![1d last7d reasoning output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last7d_reasoning_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last7d_cost_usd_raw_median_ema

![1d last7d GPT-5.5 standard cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last7d_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last7d_cost_no_cache_usd_raw_median_ema

![1d last7d no-cache cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last7d_cost_no_cache_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last7d_cache_savings_usd_raw_median_ema

![1d last7d cache savings: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last7d_cache_savings_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last7d_long_context_upper_cost_usd_raw_median_ema

![1d last7d long-context upper cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last7d_long_context_upper_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last7d_effective_usd_per_1m_total_tokens_raw_median_ema

![1d last7d effective cost per total token: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last7d_effective_usd_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last7d_cost_per_1m_output_tokens_raw_median_ema

![1d last7d cost per output token: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last7d_cost_per_1m_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last7d_tokens_per_usd_raw_median_ema

![1d last7d tokens per dollar: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last7d_tokens_per_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last7d_cache_ratio_raw_median_ema

![1d last7d cache ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last7d_cache_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last7d_output_ratio_raw_median_ema

![1d last7d output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last7d_output_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last7d_reasoning_ratio_raw_median_ema

![1d last7d reasoning/output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last7d_reasoning_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last7d_output_cost_share_raw_median_ema

![1d last7d output cost share: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last7d_output_cost_share_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last7d_output_per_1m_input_tokens_raw_median_ema

![1d last7d output per input: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last7d_output_per_1m_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last7d_reasoning_per_1m_total_tokens_raw_median_ema

![1d last7d reasoning per total: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last7d_reasoning_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last7d_printed_pages_500w_raw_median_ema

![1d last7d human-scale pages: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last7d_printed_pages_500w_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last14d_total_tokens_raw_median_ema

![1d last14d total tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last14d_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last14d_input_tokens_raw_median_ema

![1d last14d input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last14d_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last14d_cached_input_tokens_raw_median_ema

![1d last14d cached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last14d_cached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last14d_uncached_input_tokens_raw_median_ema

![1d last14d uncached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last14d_uncached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last14d_output_tokens_raw_median_ema

![1d last14d output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last14d_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last14d_reasoning_output_tokens_raw_median_ema

![1d last14d reasoning output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last14d_reasoning_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last14d_cost_usd_raw_median_ema

![1d last14d GPT-5.5 standard cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last14d_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last14d_cost_no_cache_usd_raw_median_ema

![1d last14d no-cache cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last14d_cost_no_cache_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last14d_cache_savings_usd_raw_median_ema

![1d last14d cache savings: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last14d_cache_savings_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last14d_long_context_upper_cost_usd_raw_median_ema

![1d last14d long-context upper cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last14d_long_context_upper_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last14d_effective_usd_per_1m_total_tokens_raw_median_ema

![1d last14d effective cost per total token: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last14d_effective_usd_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last14d_cost_per_1m_output_tokens_raw_median_ema

![1d last14d cost per output token: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last14d_cost_per_1m_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last14d_tokens_per_usd_raw_median_ema

![1d last14d tokens per dollar: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last14d_tokens_per_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last14d_cache_ratio_raw_median_ema

![1d last14d cache ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last14d_cache_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last14d_output_ratio_raw_median_ema

![1d last14d output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last14d_output_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last14d_reasoning_ratio_raw_median_ema

![1d last14d reasoning/output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last14d_reasoning_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last14d_output_cost_share_raw_median_ema

![1d last14d output cost share: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last14d_output_cost_share_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last14d_output_per_1m_input_tokens_raw_median_ema

![1d last14d output per input: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last14d_output_per_1m_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last14d_reasoning_per_1m_total_tokens_raw_median_ema

![1d last14d reasoning per total: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last14d_reasoning_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last14d_printed_pages_500w_raw_median_ema

![1d last14d human-scale pages: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last14d_printed_pages_500w_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last30d_total_tokens_raw_median_ema

![1d last30d total tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last30d_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last30d_input_tokens_raw_median_ema

![1d last30d input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last30d_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last30d_cached_input_tokens_raw_median_ema

![1d last30d cached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last30d_cached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last30d_uncached_input_tokens_raw_median_ema

![1d last30d uncached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last30d_uncached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last30d_output_tokens_raw_median_ema

![1d last30d output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last30d_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last30d_reasoning_output_tokens_raw_median_ema

![1d last30d reasoning output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last30d_reasoning_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last30d_cost_usd_raw_median_ema

![1d last30d GPT-5.5 standard cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last30d_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last30d_cost_no_cache_usd_raw_median_ema

![1d last30d no-cache cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last30d_cost_no_cache_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last30d_cache_savings_usd_raw_median_ema

![1d last30d cache savings: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last30d_cache_savings_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last30d_long_context_upper_cost_usd_raw_median_ema

![1d last30d long-context upper cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last30d_long_context_upper_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last30d_effective_usd_per_1m_total_tokens_raw_median_ema

![1d last30d effective cost per total token: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last30d_effective_usd_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last30d_cost_per_1m_output_tokens_raw_median_ema

![1d last30d cost per output token: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last30d_cost_per_1m_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last30d_tokens_per_usd_raw_median_ema

![1d last30d tokens per dollar: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last30d_tokens_per_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last30d_cache_ratio_raw_median_ema

![1d last30d cache ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last30d_cache_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last30d_output_ratio_raw_median_ema

![1d last30d output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last30d_output_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last30d_reasoning_ratio_raw_median_ema

![1d last30d reasoning/output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last30d_reasoning_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last30d_output_cost_share_raw_median_ema

![1d last30d output cost share: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last30d_output_cost_share_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last30d_output_per_1m_input_tokens_raw_median_ema

![1d last30d output per input: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last30d_output_per_1m_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last30d_reasoning_per_1m_total_tokens_raw_median_ema

![1d last30d reasoning per total: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last30d_reasoning_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last30d_printed_pages_500w_raw_median_ema

![1d last30d human-scale pages: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last30d_printed_pages_500w_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last60d_total_tokens_raw_median_ema

![1d last60d total tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last60d_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last60d_input_tokens_raw_median_ema

![1d last60d input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last60d_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last60d_cached_input_tokens_raw_median_ema

![1d last60d cached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last60d_cached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last60d_uncached_input_tokens_raw_median_ema

![1d last60d uncached input tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last60d_uncached_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last60d_output_tokens_raw_median_ema

![1d last60d output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last60d_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last60d_reasoning_output_tokens_raw_median_ema

![1d last60d reasoning output tokens: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last60d_reasoning_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last60d_cost_usd_raw_median_ema

![1d last60d GPT-5.5 standard cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last60d_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last60d_cost_no_cache_usd_raw_median_ema

![1d last60d no-cache cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last60d_cost_no_cache_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last60d_cache_savings_usd_raw_median_ema

![1d last60d cache savings: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last60d_cache_savings_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last60d_long_context_upper_cost_usd_raw_median_ema

![1d last60d long-context upper cost: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last60d_long_context_upper_cost_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last60d_effective_usd_per_1m_total_tokens_raw_median_ema

![1d last60d effective cost per total token: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last60d_effective_usd_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last60d_cost_per_1m_output_tokens_raw_median_ema

![1d last60d cost per output token: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last60d_cost_per_1m_output_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last60d_tokens_per_usd_raw_median_ema

![1d last60d tokens per dollar: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last60d_tokens_per_usd_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last60d_cache_ratio_raw_median_ema

![1d last60d cache ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last60d_cache_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last60d_output_ratio_raw_median_ema

![1d last60d output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last60d_output_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last60d_reasoning_ratio_raw_median_ema

![1d last60d reasoning/output ratio: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last60d_reasoning_ratio_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last60d_output_cost_share_raw_median_ema

![1d last60d output cost share: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last60d_output_cost_share_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last60d_output_per_1m_input_tokens_raw_median_ema

![1d last60d output per input: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last60d_output_per_1m_input_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last60d_reasoning_per_1m_total_tokens_raw_median_ema

![1d last60d reasoning per total: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last60d_reasoning_per_1m_total_tokens_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

#### 1d_last60d_printed_pages_500w_raw_median_ema

![1d last60d human-scale pages: raw vs smoothed](MetricChartsDeep/2026-06-06/1d_last60d_printed_pages_500w_raw_median_ema.png)

Evidence note: Raw values are preserved; smoothing overlays rolling median and EMA for trend readability.

## Residual Risk

- Local Codex JSONL/report data is not billing-provider invoice proof.
- Recent high-resolution buckets depend on the current token report's retained hourly window.
- Long-context and regional cost bands are sensitivity approximations.
- Smoothing overlays are visualization aids and are not used to replace raw totals.

# TOKEN USAGE AUDIT 2026-05-25

Generated UTC: 2026-05-25T03:51:38.482884+00:00
Generated Samara: 2026-05-25T07:51:38.482884+04:00
Evidence class: STATIC_LOCAL_CODEX_JSONL_AND_FILESYSTEM. Not billing-provider proof.

## Scope
- current_sessions: `C:\Users\danat\.codex\sessions` exists=True
- current_archived_sessions: `C:\Users\danat\.codex\archived_sessions` exists=True
- backup_cleanup_20260521_194850: `C:\Users\danat\Documents\CodexBackups\codex_cleanup_20260521_194850` exists=True

Accounting: all-time totals use final per-session `total_token_usage`, deduped by `session_meta.id`. Day/week/month stats use positive deltas between token_count snapshots inside selected sessions.

## Totals
| Metric | Value |
|---|---:|
| file_count | 2,745 |
| unique_session_or_path_keys | 2,648 |
| sessions_with_usage | 2,622 |
| sessions_without_usage | 26 |
| duplicate_records_removed | 97 |
| files_missing_session_id | 2 |
| parse_errors_first_pass | 0 |
| parse_errors_increment_pass | 0 |
| day_span | 53 |
| first_selected_timestamp_utc | 2026-04-03T17:11:28.591000+00:00 |
| last_selected_timestamp_utc | 2026-05-25T03:53:08.200000+00:00 |
| input_tokens | 95,520,333,024 |
| cached_input_tokens | 91,768,379,008 |
| output_tokens | 332,176,227 |
| reasoning_output_tokens | 105,571,322 |
| total_tokens | 95,853,026,051 |
| uncached_input_tokens | 3,751,954,016 |
| cache_ratio | 96.072089% |
| output_ratio | 0.346547% |
| reasoning_output_ratio_of_output | 31.781721% |

## API-Equivalent Price Scenarios
Actual Codex billing cannot be proven from local JSONL. These are API-equivalent estimates using official OpenAI rates current on 2026-05-25. Cached input is charged at cached-input rate; reasoning output is an output subcounter, not added twice.

| Scenario | Uncached input | Cached input | Output | Total | No-cache upper bound |
|---|---:|---:|---:|---:|---:|
| gpt-5.3-codex_standard_api_equivalent | $6,565.92 | $16,059.47 | $4,650.47 | $27,275.85 | $171,811.05 |
| gpt-5.3-codex_priority_api_equivalent | $13,131.84 | $32,118.93 | $9,300.93 | $54,551.71 | $343,622.10 |
| gpt-5.4_standard_short_context_equivalent | $9,379.89 | $22,942.09 | $4,982.64 | $37,304.62 | $243,783.48 |
| gpt-5.5_standard_short_context_equivalent | $18,759.77 | $45,884.19 | $9,965.29 | $74,609.25 | $487,566.95 |
| gpt-5.4_mini_standard_equivalent | $2,813.97 | $6,882.63 | $1,494.79 | $11,191.39 | $73,135.04 |

## Model Forensics
Model evidence comes from structured `turn_context.payload.model` / `collaboration_mode.settings.model` fields when present. Sessions without that field are `unknown_model`; local JSONL still does not expose invoice SKU, priority mode, or contractual billing plan.

### Final Session Model Attribution
| Model | Sessions | Total tokens | Input | Cached input | Output | Reasoning output | Standard cost if rate known |
|---|---:|---:|---:|---:|---:|---:|---:|
| gpt-5.5 | 2,377 | 84,185,952,294 | 83,900,097,012 | 80,647,326,976 | 285,338,482 | 87,007,760 | $65,147.67 |
| gpt-5.4 | 232 | 11,564,030,598 | 11,517,784,890 | 11,025,437,696 | 46,245,708 | 18,360,012 | $4,680.91 |
| gpt-5.2-codex | 3 | 85,512,992 | 85,044,900 | 79,787,648 | 468,092 | 157,917 | unpriced |
| gpt-5.1-codex-mini | 3 | 13,472,930 | 13,374,833 | 12,237,952 | 98,097 | 32,307 | unpriced |
| gpt-5.4-mini | 1 | 2,818,965 | 2,801,127 | 2,652,800 | 17,838 | 12,029 | $0.39 |
| gpt-5.3-codex | 3 | 1,096,113 | 1,088,533 | 879,744 | 7,580 | 1,132 | $0.63 |
| gpt-5.2 | 3 | 142,159 | 141,729 | 56,192 | 430 | 165 | unpriced |

### Temporal Delta Model Attribution
This table assigns each token delta to the latest prior `turn_context` model in the same JSONL file. It is useful for trend analysis, but all-time totals above remain final-session authority.

| Model | Delta total | Delta input | Delta cached input | Delta output | Delta reasoning output |
|---|---:|---:|---:|---:|---:|
| gpt-5.5 | 82,814,884,813 | 82,534,751,029 | 79,339,657,984 | 280,133,784 | 85,149,323 |
| gpt-5.4 | 13,002,550,593 | 12,950,641,924 | 12,396,688,640 | 51,908,669 | 20,348,155 |
| gpt-5.2-codex | 31,468,079 | 31,351,204 | 28,951,680 | 116,875 | 50,048 |
| gpt-5.3-codex | 22,822,547 | 22,773,200 | 21,537,152 | 49,347 | 20,889 |
| gpt-5.4-mini | 5,851,626 | 5,821,586 | 5,453,824 | 30,040 | 15,652 |
| gpt-5.1-codex-mini | 995,678 | 991,379 | 583,168 | 4,299 | 2,368 |
| gpt-5.2 | 142,159 | 141,729 | 56,192 | 430 | 165 |

### Reasoning Effort Attribution
| Effort | Sessions | Total tokens | Input | Cached input | Output | Reasoning output |
|---|---:|---:|---:|---:|---:|---:|
| xhigh | 1,784 | 86,008,517,118 | 85,714,256,723 | 82,445,320,320 | 293,743,595 | 92,104,646 |
| high | 242 | 8,560,732,798 | 8,528,100,943 | 8,153,021,312 | 32,631,855 | 11,873,117 |
| medium | 585 | 1,277,225,063 | 1,271,463,875 | 1,164,613,632 | 5,761,188 | 1,589,652 |
| low | 11 | 6,551,072 | 6,511,483 | 5,423,744 | 39,589 | 3,907 |

### Model-Specific Cost Bounds
| Bound | USD |
|---|---:|
| known_models_only_standard_usd | $69,829.60 |
| unpriced_known_model_total_tokens | 99,128,081 tokens |
| unpriced_as_gpt_5_3_codex_standard_usd | $35.39 |
| unpriced_as_gpt_5_5_standard_usd | $95.44 |
| known_plus_unpriced_as_gpt_5_3_codex_standard_usd | $69,864.98 |
| known_plus_unpriced_as_gpt_5_5_standard_usd | $69,925.03 |

## Interpretive Stats
These are derived diagnostics, not billing proof. They are useful for waste shape, concentration, and cache economics.

| Metric | Value |
|---|---:|
| active_days | 53.0000 |
| calendar_day_span | 53.0000 |
| mean_tokens_per_active_day | 1,808,547,661.3396 |
| median_tokens_per_active_day | 753,420,707.0000 |
| peak_day_tokens | 11,101,068,200.0000 |
| peak_day_vs_mean_active_day | 6.1381 |
| session_gini_total_tokens | 0.7797 |
| top_1_percent_sessions_share | 17.9399% |
| top_5_percent_sessions_share | 43.8356% |
| top_10_percent_sessions_share | 61.6215% |
| largest_session_share | 1.8043% |
| equivalent_full_258400_context_windows | 370,948.2432 |
| equivalent_full_270k_context_windows | 355,011.2076 |
| gpt_5_3_codex_standard_cache_discount_saved_usd | $144,535.20 |
| gpt_5_3_codex_standard_cost_per_primary_loc_usd | $0.02 |
| gpt_5_3_codex_standard_cost_per_1k_primary_loc_usd | $15.43 |
| tokens_per_dollar_gpt_5_3_codex_standard | 3,514,208.1876 |
| output_tokens_per_1m_total_tokens | 3,465.4746 |
| reasoning_tokens_per_1m_total_tokens | 1,101.3875 |

## Root Breakdown
| Root | JSONL files | Files with usage | Selected sessions | Selected with usage | Selected total tokens |
|---|---:|---:|---:|---:|---:|
| backup_cleanup_20260521_194850 | 1,048 | 1,029 | 1,020 | 1,001 | 57,856,335,910 |
| current_archived_sessions | 1 | 1 | 1 | 1 | 157,103 |
| current_sessions | 1,696 | 1,689 | 1,627 | 1,620 | 37,996,533,038 |

## LOC And Token Ratios
| Scope | Files | Lines | Tokens / line | Output tokens / line |
|---|---:|---:|---:|---:|
| first_party_assets_project_cs | 2,483 | 1,767,679 | 54,225.36 | 187.9166 |
| first_party_scripts_cs | 2,406 | 1,742,740 | 55,001.33 | 190.6057 |
| all_repo_cs_excluding_generated | 6,271 | 2,470,274 | 38,802.59 | 134.4694 |
| all_repo_source_broad | 14,704 | 9,977,670 | 9,606.75 | 33.2920 |
| tools_scripts | 220 | 106,530 | 899,774.96 | 3,118.1473 |
| docs_markdown_text | 4,808 | 2,994,209 | 32,012.80 | 110.9396 |

## Daily Stats
| Date Samara | Total | Input | Cached input | Output | Reasoning output |
|---|---:|---:|---:|---:|---:|
| 2026-04-03 | 46,914,153 | 46,800,020 | 44,751,232 | 114,133 | 48,190 |
| 2026-04-04 | 217,726,420 | 217,069,509 | 208,059,520 | 656,911 | 300,112 |
| 2026-04-05 | 147,145,632 | 146,568,964 | 139,083,648 | 576,668 | 224,979 |
| 2026-04-06 | 67,295,113 | 67,012,995 | 63,155,968 | 282,118 | 117,563 |
| 2026-04-07 | 231,980,232 | 231,129,121 | 221,723,648 | 851,111 | 275,631 |
| 2026-04-08 | 365,440,905 | 364,224,654 | 344,372,608 | 1,216,251 | 362,385 |
| 2026-04-09 | 537,602,734 | 535,920,945 | 510,756,352 | 1,681,789 | 542,534 |
| 2026-04-10 | 120,758,983 | 120,219,221 | 112,022,016 | 539,762 | 132,393 |
| 2026-04-11 | 161,493,346 | 160,993,566 | 155,746,688 | 499,780 | 191,242 |
| 2026-04-12 | 180,617,991 | 179,746,201 | 169,569,664 | 871,790 | 323,436 |
| 2026-04-13 | 217,604,650 | 216,856,479 | 205,986,816 | 748,171 | 248,235 |
| 2026-04-14 | 272,823,843 | 272,002,972 | 258,142,208 | 820,871 | 308,844 |
| 2026-04-15 | 462,585,355 | 461,073,509 | 440,721,792 | 1,511,846 | 540,429 |
| 2026-04-16 | 517,255,910 | 515,457,159 | 493,700,608 | 1,798,751 | 605,000 |
| 2026-04-17 | 660,349,124 | 658,271,205 | 635,097,728 | 2,077,919 | 701,678 |
| 2026-04-18 | 489,036,261 | 487,172,418 | 467,197,056 | 1,863,843 | 785,244 |
| 2026-04-19 | 338,544,554 | 337,184,425 | 324,000,896 | 1,360,129 | 594,235 |
| 2026-04-20 | 491,688,782 | 489,332,881 | 468,158,208 | 2,355,901 | 880,188 |
| 2026-04-21 | 670,066,703 | 667,417,339 | 643,610,112 | 2,649,364 | 1,034,410 |
| 2026-04-22 | 589,417,352 | 587,150,956 | 560,861,952 | 2,266,396 | 823,033 |
| 2026-04-23 | 753,420,707 | 749,344,244 | 716,932,352 | 4,076,463 | 2,027,319 |
| 2026-04-24 | 574,647,336 | 571,997,731 | 551,393,664 | 2,649,605 | 1,212,386 |
| 2026-04-25 | 556,431,838 | 553,985,815 | 532,980,608 | 2,446,023 | 1,203,739 |
| 2026-04-26 | 279,605,255 | 278,028,301 | 261,297,792 | 1,576,954 | 794,589 |
| 2026-04-27 | 540,705,543 | 538,398,669 | 512,477,184 | 2,306,874 | 1,011,603 |
| 2026-04-28 | 630,972,538 | 628,248,279 | 601,294,720 | 2,724,259 | 1,078,469 |
| 2026-04-29 | 2,371,242,522 | 2,361,791,317 | 2,264,046,208 | 9,451,205 | 3,341,938 |
| 2026-04-30 | 1,934,301,823 | 1,928,793,392 | 1,864,962,304 | 5,508,431 | 1,908,761 |
| 2026-05-01 | 1,658,252,483 | 1,654,281,664 | 1,603,181,312 | 3,970,819 | 1,255,115 |
| 2026-05-02 | 600,356,890 | 598,655,185 | 576,509,184 | 1,701,705 | 510,010 |
| 2026-05-03 | 1,137,400,165 | 1,133,992,954 | 1,093,655,552 | 3,407,211 | 931,949 |
| 2026-05-04 | 1,099,097,709 | 1,096,107,341 | 1,055,105,792 | 2,990,368 | 1,005,607 |
| 2026-05-05 | 1,877,396,348 | 1,871,704,737 | 1,778,847,232 | 5,691,611 | 1,909,201 |
| 2026-05-06 | 1,499,172,953 | 1,495,181,228 | 1,428,096,256 | 3,991,725 | 1,387,800 |
| 2026-05-07 | 2,454,657,515 | 2,447,960,532 | 2,361,763,968 | 6,696,983 | 2,182,677 |
| 2026-05-08 | 2,251,279,157 | 2,243,508,273 | 2,151,188,736 | 7,770,884 | 2,489,958 |
| 2026-05-09 | 3,680,140,530 | 3,667,936,699 | 3,537,070,336 | 12,203,831 | 3,727,560 |
| 2026-05-10 | 1,107,882,179 | 1,104,049,311 | 1,057,653,504 | 3,832,868 | 1,390,915 |
| 2026-05-11 | 2,534,804,277 | 2,525,927,322 | 2,404,871,424 | 8,876,955 | 2,955,420 |
| 2026-05-12 | 2,022,348,386 | 2,015,898,746 | 1,933,376,256 | 6,449,640 | 2,023,604 |
| 2026-05-13 | 4,316,279,171 | 4,303,116,896 | 4,168,047,360 | 13,162,275 | 4,084,874 |
| 2026-05-14 | 1,909,997,371 | 1,903,285,458 | 1,827,978,240 | 6,711,913 | 2,217,603 |
| 2026-05-15 | 4,068,018,530 | 4,052,913,416 | 3,901,611,520 | 15,105,114 | 4,582,586 |
| 2026-05-16 | 3,057,382,237 | 3,046,891,842 | 2,921,180,800 | 10,490,395 | 3,372,832 |
| 2026-05-17 | 2,825,460,950 | 2,815,484,794 | 2,708,110,976 | 9,976,156 | 3,066,818 |
| 2026-05-18 | 3,135,985,060 | 3,123,214,081 | 2,987,810,176 | 12,770,979 | 4,073,419 |
| 2026-05-19 | 6,373,573,208 | 6,349,219,636 | 6,065,698,176 | 24,353,572 | 7,357,653 |
| 2026-05-20 | 7,029,317,551 | 7,003,018,861 | 6,732,588,416 | 26,298,690 | 7,229,638 |
| 2026-05-21 | 11,101,068,200 | 11,060,628,884 | 10,592,076,288 | 40,439,316 | 12,217,203 |
| 2026-05-22 | 4,536,739,481 | 4,521,106,542 | 4,352,767,488 | 15,632,939 | 4,671,535 |
| 2026-05-23 | 4,820,332,622 | 4,804,177,372 | 4,627,110,272 | 16,155,250 | 4,746,761 |
| 2026-05-24 | 4,895,972,931 | 4,881,900,914 | 4,747,163,776 | 14,072,017 | 3,566,006 |
| 2026-05-25 | 1,458,123,986 | 1,454,117,076 | 1,407,362,048 | 4,006,910 | 1,011,291 |

## Weekly Stats
| ISO Week Samara | Total | Input | Cached input | Output | Reasoning output |
|---|---:|---:|---:|---:|---:|
| 2026-W14 | 411,786,205 | 410,438,493 | 391,894,400 | 1,347,712 | 573,281 |
| 2026-W15 | 1,665,189,304 | 1,659,246,703 | 1,577,346,944 | 5,942,601 | 1,945,184 |
| 2026-W16 | 2,958,199,697 | 2,948,018,167 | 2,824,847,104 | 10,181,530 | 3,783,665 |
| 2026-W17 | 3,915,277,973 | 3,897,257,267 | 3,735,234,688 | 18,020,706 | 7,975,664 |
| 2026-W18 | 8,873,231,964 | 8,844,161,460 | 8,516,126,464 | 29,070,504 | 10,037,845 |
| 2026-W19 | 13,969,626,391 | 13,926,448,121 | 13,369,725,824 | 43,178,270 | 14,093,718 |
| 2026-W20 | 20,734,290,922 | 20,663,518,474 | 19,865,176,576 | 70,772,448 | 22,303,737 |
| 2026-W21 | 41,892,989,053 | 41,743,266,290 | 40,105,214,592 | 149,722,763 | 43,862,215 |
| 2026-W22 | 1,458,123,986 | 1,454,117,076 | 1,407,362,048 | 4,006,910 | 1,011,291 |

## Monthly Stats
| Month Samara | Total | Input | Cached input | Output | Reasoning output |
|---|---:|---:|---:|---:|---:|
| 2026-04 | 14,427,675,605 | 14,372,192,287 | 13,772,103,552 | 55,483,318 | 21,618,565 |
| 2026-05 | 81,451,039,890 | 81,174,279,764 | 78,020,825,088 | 276,760,126 | 83,968,035 |

## Top 20 Days
| Date Samara | Total tokens |
|---|---:|
| 2026-05-21 | 11,101,068,200 |
| 2026-05-20 | 7,029,317,551 |
| 2026-05-19 | 6,373,573,208 |
| 2026-05-24 | 4,895,972,931 |
| 2026-05-23 | 4,820,332,622 |
| 2026-05-22 | 4,536,739,481 |
| 2026-05-13 | 4,316,279,171 |
| 2026-05-15 | 4,068,018,530 |
| 2026-05-09 | 3,680,140,530 |
| 2026-05-18 | 3,135,985,060 |
| 2026-05-16 | 3,057,382,237 |
| 2026-05-17 | 2,825,460,950 |
| 2026-05-11 | 2,534,804,277 |
| 2026-05-07 | 2,454,657,515 |
| 2026-04-29 | 2,371,242,522 |
| 2026-05-08 | 2,251,279,157 |
| 2026-05-12 | 2,022,348,386 |
| 2026-04-30 | 1,934,301,823 |
| 2026-05-14 | 1,909,997,371 |
| 2026-05-05 | 1,877,396,348 |

## Distributions
| Metric | Value |
|---|---:|
| tokens_per_day_span | 1,808,547,661.34 |
| tokens_per_session_with_usage | 36,557,218.17 |
| output_tokens_per_session_with_usage | 126,688.11 |
| median_tokens_per_session | 3,490,594.50 |
| p90_tokens_per_session | 102,578,302.00 |
| p95_tokens_per_session | 164,740,705.00 |
| p99_tokens_per_session | 372,392,894.00 |
| max_tokens_per_session | 1,729,485,513.00 |

Context window counts:
- 258400: 2,622

Plan type counts:
- free: 2,610
- team: 10
- unknown: 2

## Top 25 Sessions
| Rank | Session | Model | Effort | Root | Final UTC | Total | Input | Cached | Output | CWD |
|---:|---|---|---|---|---|---:|---:|---:|---:|---|
| 1 | `019e42fa-4ec0-7e32-8384-f0756a3470c0` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T03:52:17.602000+00:00 | 1,729,485,513 | 1,725,959,766 | 1,677,824,128 | 3,525,747 | `c:\hades` |
| 2 | `019e2099-6961-71b3-abdc-f8fdb0c1576c` | gpt-5.5 | xhigh | backup_cleanup_20260521_194850 | 2026-05-20T01:19:43.715000+00:00 | 1,305,764,480 | 1,302,806,591 | 1,262,117,376 | 2,957,889 | `c:\hades` |
| 3 | `019e3dbf-eddb-7ab0-84b6-aa5b097a2b68` | gpt-5.5 | xhigh | current_sessions | 2026-05-21T18:58:24.572000+00:00 | 1,300,668,055 | 1,296,761,551 | 1,261,152,128 | 3,906,504 | `c:\hades` |
| 4 | `019e42c1-57ec-7701-a1d7-7b5fbb073503` | gpt-5.5 | xhigh | current_sessions | 2026-05-23T01:11:23.296000+00:00 | 1,167,862,097 | 1,163,767,930 | 1,128,039,680 | 4,094,167 | `c:\hades` |
| 5 | `019e54e5-7e0f-7d61-b2a8-7d3cf00593f8` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T03:53:03.131000+00:00 | 710,671,196 | 708,764,157 | 691,892,736 | 1,907,039 | `c:\hades` |
| 6 | `019e54e3-0619-7d00-bd04-709b7ec1949e` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T03:53:08.200000+00:00 | 707,946,516 | 706,128,899 | 686,922,624 | 1,817,617 | `c:\hades` |
| 7 | `019e558b-99b3-73a2-b1b0-744e2c7adaf8` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T02:21:00.701000+00:00 | 621,711,391 | 620,254,496 | 605,311,232 | 1,456,895 | `c:\hades` |
| 8 | `019e5249-c70f-7263-880f-3531b581aad4` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T00:17:04.800000+00:00 | 610,793,317 | 608,951,910 | 594,295,296 | 1,841,407 | `c:\hades` |
| 9 | `019e54e1-c11c-71b0-9b8a-21b8efdcde8c` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T03:45:53.363000+00:00 | 606,552,538 | 604,959,514 | 590,735,104 | 1,593,024 | `c:\hades` |
| 10 | `019e558a-e142-7303-8f57-df6d1d2d75d4` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T03:52:12.109000+00:00 | 558,065,507 | 556,542,086 | 540,886,272 | 1,523,421 | `c:\hades` |
| 11 | `019e42d0-0f2a-72c2-a688-9241371dd6e4` | gpt-5.5 | xhigh | current_sessions | 2026-05-20T23:54:50.549000+00:00 | 548,201,085 | 546,656,842 | 535,127,040 | 1,544,243 | `c:\hades` |
| 12 | `019e480b-c231-7d60-ac6c-130c5f52e788` | gpt-5.5 | xhigh | current_sessions | 2026-05-22T09:05:28.159000+00:00 | 528,011,366 | 526,558,944 | 512,782,336 | 1,452,422 | `c:\hades` |
| 13 | `019e42d6-563e-7d31-ad70-d983432fe8d1` | gpt-5.5 | xhigh | current_sessions | 2026-05-21T15:23:03.424000+00:00 | 523,659,412 | 521,826,682 | 502,747,776 | 1,832,730 | `c:\hades` |
| 14 | `019e1859-0e01-77b2-a8c6-b5586ccc5c8c` | gpt-5.5 | xhigh | backup_cleanup_20260521_194850 | 2026-05-14T10:56:23.755000+00:00 | 518,697,166 | 517,631,477 | 503,886,080 | 1,065,689 | `c:\hades` |
| 15 | `019e4328-e29d-7163-b59e-f2841cce7c18` | gpt-5.5 | xhigh | current_sessions | 2026-05-21T16:09:09.486000+00:00 | 517,149,254 | 515,515,364 | 496,384,640 | 1,633,890 | `c:\hades` |
| 16 | `019e559e-fe08-72d2-8a16-4deb504b9a8f` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T03:35:55.207000+00:00 | 512,144,428 | 510,572,140 | 496,116,608 | 1,572,288 | `c:\hades` |
| 17 | `019e4b4d-3686-7c23-b2a3-a04e2101ce5c` | gpt-5.5 | xhigh | current_sessions | 2026-05-22T07:38:58.454000+00:00 | 508,129,959 | 506,761,687 | 494,648,448 | 1,368,272 | `c:\hades` |
| 18 | `019e3700-e461-7b83-b037-ecaceb36f169` | gpt-5.5 | xhigh | backup_cleanup_20260521_194850 | 2026-05-20T22:25:39.731000+00:00 | 504,011,103 | 502,231,620 | 479,605,632 | 1,779,483 | `c:\hades` |
| 19 | `019e5488-d058-7152-8dda-15c6e68fd5d5` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T01:31:58.063000+00:00 | 502,547,222 | 500,775,886 | 483,416,704 | 1,771,336 | `c:\hades` |
| 20 | `019e559a-8add-70d0-a8b6-d80ae1bce573` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T02:44:34.994000+00:00 | 501,605,986 | 500,149,065 | 486,301,824 | 1,456,921 | `c:\hades` |
| 21 | `019d6329-de82-74e2-83ca-450539a61cec` | gpt-5.4 | high | backup_cleanup_20260521_194850 | 2026-04-09T13:02:36.778000+00:00 | 490,407,394 | 488,890,828 | 466,945,664 | 1,516,566 | `c:\hades\Hecton8` |
| 22 | `019e3974-b286-7191-a24e-53679b314dd5` | gpt-5.5 | xhigh | backup_cleanup_20260521_194850 | 2026-05-21T00:37:51.363000+00:00 | 469,509,921 | 467,091,035 | 445,224,192 | 2,418,886 | `c:\hades` |
| 23 | `019dde7c-df90-7791-b4b4-d49c8450a9be` | gpt-5.5 | xhigh | backup_cleanup_20260521_194850 | 2026-05-03T17:53:18.109000+00:00 | 468,267,072 | 467,232,128 | 455,689,472 | 1,034,944 | `c:\hades\Hecton8` |
| 24 | `019e54df-35af-7c60-8da3-67d86a082648` | gpt-5.5 | xhigh | current_sessions | 2026-05-25T03:48:57.507000+00:00 | 437,556,306 | 436,260,605 | 423,413,120 | 1,295,701 | `c:\hades` |
| 25 | `019d67a6-6823-7b82-94f9-a3167b8e0286` | gpt-5.4 | high | backup_cleanup_20260521_194850 | 2026-04-09T11:13:03.573000+00:00 | 429,064,399 | 427,653,439 | 407,748,992 | 1,410,960 | `c:\hades\Hecton8` |

## Price Sources
- https://developers.openai.com/api/docs/pricing lines 851-854 for gpt-5.3-codex standard
- https://developers.openai.com/api/docs/pricing lines 866-867 for gpt-5.3-codex priority
- https://openai.com/api/pricing/ lines 33-76 for GPT-5.5/GPT-5.4/GPT-5.4-mini standard short-context

## Residual Risk
- Local JSONL is not provider billing. It lacks invoice ids and does not expose whether a Codex request used standard, priority, enterprise, subscription, or internal billing.
- `cached_input_tokens` is treated as a priced subcounter of input tokens, not additional total tokens.
- Model labels are exact only where structured `turn_context` fields exist. Older sessions without model fields remain `unknown_model`.
- Daily/week/model delta allocation is reconstructed from telemetry deltas; all-time final per-session total remains authoritative for this local audit.

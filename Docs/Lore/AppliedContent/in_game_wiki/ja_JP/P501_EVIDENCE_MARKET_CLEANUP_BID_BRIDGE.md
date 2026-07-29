---
packet_id: P501_EVIDENCE_MARKET_CLEANUP_BID_BRIDGE
release_set_id: RS100_PUBLIC_EVIDENCE_CLEANUP_CONFLICT_BRIDGE
article_id: EVIDENCE_MARKET_CLEANUP_BID_BRIDGE
unlock_id: unlock.p501_evidence_market_cleanup_bid_bridge
poi_tags: poi.public_archive_receiver_shelf;poi.evidence_market_terminal
biome_tags: biome.deep_archive
locale: ja_JP
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "証拠清掃入札"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 0
prereq_packet_ids: P500_PUBLIC_ARCHIVE_RECEIVER_AMBIGUITY_BRIDGE
next_packet_ids: P502_CLAIMANT_SAFE_SUMMARY_CONFLICT_BRIDGE
---

# 証拠清掃入札

清掃入札は判決ではない。証拠経路に対する購入指示だ。依頼内容は、タグを乾かすこと、ラベルを通常名に整えること、断片をサルベージロットへ移すこと、公開を遅らせること、作業員名を支払い区分へ変換すること、または生の物証を請求者向けの安全な要約の後ろに埋めることかもしれない。重要なのは、記録が変わる前に金が現れる点だ。支払った側は有罪かもしれないし、恐れているだけかもしれないし、雑だったか、時間を買っただけかもしれない。入札そのものが示すのは証拠の筋への圧力であって、断片の真偽ではない。

入札は順序で読む。元ラベル、入札元、エスクロー保留、処理者アカウント、清掃後ラベル、保管移転、物証経路は一緒に残す必要がある。支払い前に清掃後ラベルが出ているなら、通常のアーカイブ処理かもしれない。ラベル変更前に支払いが来ているなら、誰かが経路変更を買った。元ラベルを清掃後ラベルの横に残せ。そうしなければアーカイブ自体が清掃の一部になる。

## Scanner

清掃入札 // 証拠経路に対する有償依頼。必要: 元ラベル、入札元、エスクロー保留、処理者アカウント、清掃後ラベル、保管移転、物証経路。

## Terminal

証拠清掃入札
支払いを判決として扱うな。
ラベル変更前の支払い = 購入された経路変更。
支払い前のラベル変更 = 通常のアーカイブ処理の可能性。
次に必要な証拠: エスクロー保留、処理者アカウント、保管移転、旧ラベル、清掃後ラベル、物証経路。
処置: 物証経路が解決するまで両方のラベルを保持する。

## Audio

ラベルが変わる前に支払いが見つかった。古い名前を画面に残せ。

## Field Note

清掃後ラベルだけでアーカイブするな。旧ラベルと支払い時刻こそ証拠だ。

<!-- In-Game Wiki; generated from P501_EVIDENCE_MARKET_CLEANUP_BID_BRIDGE/ja_JP. -->

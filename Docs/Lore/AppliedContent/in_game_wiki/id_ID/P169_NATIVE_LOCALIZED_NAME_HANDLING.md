---
packet_id: P169_NATIVE_LOCALIZED_NAME_HANDLING
release_set_id: RS034_WORKER_NAME_JOB_EVIDENCE_TABLE
article_id: worker_evidence.native_localized_name_handling
unlock_id: unlock.native_localized_name_handling
poi_tags: poi.localized_name_policy;poi.rtl_name_strip
biome_tags: biome.claim_admin;biome.shallow_annex
locale: id_ID
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "Protokol Lokalisasi Nama Asli"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
prereq_packet_ids: P1336_TRANSCRIPT_DAMAGE_BAND_OFFSET_FIELD_ARTICLE
---

# Protokol Lokalisasi Nama Asli

Lokalisasi nama asli melindungi bukti pekerja dari kecelakaan interface. Pemain tidak boleh melihat nama tergencet, terbalik menjadi omong kosong, setengah diterjemahkan fallback, atau diganti sisa debug Inggris.

Aturannya sederhana: identitas pribadi ditulis per locale, sementara sistem di sekitarnya diterjemahkan normal. Jika bahasa memerlukan bentuk pendek untuk lencana, bentuk itu ditulis dan di-bake, bukan diciptakan di runtime.

## Scanner

NAME LOC // Strip ini ditulis, bukan diterjemahkan langsung. Orang itu selamat di interface hanya jika interface berhenti berimprovisasi.

## Terminal

LOKALISASI NAMA // Nama pribadi, strip pendek, dan fragmen lencana di-bake per locale. Jabatan, departemen, izin rute, dan catatan sif dilokalkan di sekelilingnya. RTL dan CJK memerlukan bentuk pendek tertulis, pemenggalan aman, dan tanpa rekomposisi live di scanner, UI loker, terminal, atau wiki eksternal.

## Audio

Nama yang merusak UI bukan penghormatan. Itu koloni menghapus pekerja dua kali.

## Field Note

Jangan biarkan runtime fallback mengganti nama pekerja mati. Nama rusak adalah penghapusan lain.

<!-- In-Game Wiki; generated from P169_NATIVE_LOCALIZED_NAME_HANDLING/id_ID. -->

---
packet_id: P169_NATIVE_LOCALIZED_NAME_HANDLING
release_set_id: RS034_WORKER_NAME_JOB_EVIDENCE_TABLE
article_id: worker_evidence.native_localized_name_handling
unlock_id: unlock.native_localized_name_handling
poi_tags: poi.localized_name_policy;poi.rtl_name_strip
biome_tags: biome.claim_admin;biome.shallow_annex
locale: en_US
surface: external_site
source_voice: Website Public
spoiler_tier: 
title: "Native Name Localization Protocol"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: source_authority
localization_flags: 0
---

# Native Name Localization Protocol

Native Name Localization Protocol defines how HECTON-8 preserves worker identity across 15 player languages. Personal names, badge strips and compact display variants are authored and baked per locale. Surrounding vocabulary localizes separately: job titles, departments, shift roles, route permissions and scanner labels.

The goal is both narrative and technical. A worker cannot be respected in English and mangled in Arabic, Hebrew, Japanese, Korean, Chinese, Russian or Polish. The localization model therefore treats names as evidence objects. They must fit the interface, survive export to in-game wiki and site pages, and never depend on live translation at runtime.

## Scanner

NAME LOC // This strip is authored, not live-translated. The person survives the interface only if the interface stops improvising.

## Terminal

NAME LOCALIZATION // Personal names, short name strips and badge fragments are baked per locale. Job titles, departments, route permissions and shift notes localize around them. RTL and CJK builds require authored short forms, line-break-safe name strips and no live recomposition in the scanner, locker UI, terminals or external wiki exports.

## Audio

A name that breaks the UI is not respect. It is the colony deleting the worker twice.

## Field Note

Never let a runtime fallback rename a dead worker. A broken name is another form of erasure.

<!-- External Site; generated from P169_NATIVE_LOCALIZED_NAME_HANDLING/en_US. -->

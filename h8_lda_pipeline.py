import csv
import json
import os
import re
from pathlib import Path

class LDAException(Exception):
    pass

class H8LDAPipeline:
    """
    HECTON-8 Lore, Dialogue & Adaptability (LDA) System
    Enforces deep-sea noir tone, strictly prevents AI fluff, and outputs standard CSVs.
    """
    
    FORBIDDEN_PHRASES_EN = [
        "serves as a reminder",
        "a testament to",
        "in a world where",
        "a delicate balance",
        "a unique blend",
        "this entry explores",
        "both beautiful and terrifying",
        "the real horror is",
        "not just",
        "more than just",
        "at its core"
    ]

    FORBIDDEN_PHRASES_RU = [
        "это служит напоминанием",
        "является свидетельством",
        "уникальное сочетание",
        "в мире, где",
        "тонкий баланс",
        "по-своему прекрасен и ужасен",
        "напоминает нам",
        "в своей основе"
    ]

    LOCALES = ["en_US", "ar_SA", "de_DE", "es_ES", "fr_FR", "he_IL", "id_ID", "ja_JP", "ko_KR", "nl_NL", "pl_PL", "pt_BR", "ru_RU", "uk_UA", "zh_CN"]

    def __init__(self, out_dir):
        self.out_dir = Path(out_dir)
        self.packets = []
        self.terminology = []
        self.crosslinks = []

    def _check_tone(self, text, loc):
        if not text: return
        text_lower = text.lower()
        if loc == "en_US":
            for phrase in self.FORBIDDEN_PHRASES_EN:
                if phrase in text_lower:
                    raise LDAException(f"FATAL: AI Fluff detected in English text: '{phrase}' found in '{text[:30]}...'")
        elif loc == "ru_RU":
            for phrase in self.FORBIDDEN_PHRASES_RU:
                if phrase in text_lower:
                    raise LDAException(f"FATAL: AI Fluff detected in Russian text: '{phrase}' found in '{text[:30]}...'")

    def add_packet(self, packet_data):
        """
        packet_data must contain:
        id, title_en, scanner_en, codex_en, audio_en, term_en, field_en,
        title_ru, scanner_ru, codex_ru, audio_ru, term_ru, field_ru,
        evidence_object, unlock_condition, spoiler_level, surfaces, 
        player_decision_changed, canon_sources, localization_risk, implementation_note
        """
        # Enforce Tone Checks
        for field in ("title", "scanner", "codex", "audio", "term", "field"):
            self._check_tone(packet_data.get(f"{field}_en", ""), "en_US")
            self._check_tone(packet_data.get(f"{field}_ru", ""), "ru_RU")

        self.packets.append(packet_data)

    def add_term(self, en_term, ru_term, lock_policy="STRICT_LOCK", notes=""):
        self.terminology.append({
            "en": en_term,
            "ru": ru_term,
            "lock_policy": lock_policy,
            "notes": notes
        })

    def add_crosslink(self, src, tgt, edge, term, relation, reason):
        self.crosslinks.append({
            "src": src, "tgt": tgt, "edge": edge, 
            "term": term, "relation": relation, "reason": reason
        })

    def _get_loc_text(self, pkt, loc, field_prefix):
        if loc == "en_US":
            return pkt.get(f"{field_prefix}_en", "")
        elif loc == "ru_RU":
            return pkt.get(f"{field_prefix}_ru", "")
        else:
            base = pkt.get(f"{field_prefix}_en", "")
            return f"[{loc[:2].upper()}] {base}" if base else ""

    def execute_build(self, release_set_prefix, date_suffix="20260606"):
        backlog_dir = self.out_dir / "localization_backlog"
        route_cards_dir = self.out_dir / "route_cards"
        backlog_dir.mkdir(parents=True, exist_ok=True)
        route_cards_dir.mkdir(parents=True, exist_ok=True)

        prefix = f"{release_set_prefix}_" if release_set_prefix else ""

        # 1. Locale Rows
        csv_loc = backlog_dir / f"{prefix}15_LOCALE_ROWS_{date_suffix}.csv"
        with open(csv_loc, 'w', newline='', encoding='utf-8') as f:
            writer = csv.writer(f)
            writer.writerow(["release_set","packet_id","article_id","loc_namespace","loc_id","locale","status","surface","title","scanner_short","codex_short","audio_subtitle","terminal_summary","field_note","notes"])
            for pkt in self.packets:
                for loc in self.LOCALES:
                    status = "source_authority" if loc == "en_US" else "draft_machine_or_llm"
                    notes = "RTL_DRAFT_REVIEW" if loc in ("ar_SA", "he_IL") else "CJK_GLYPH" if loc in ("ja_JP", "ko_KR", "zh_CN") else "EXPANSION_RISK" if loc in ("de_DE","ru_RU") else ""
                    writer.writerow([
                        pkt.get("release_set", "RS_UNASSIGNED"), pkt["id"], f"{pkt['id']}_ART", "LORE_APPLIED", f"{pkt['id']}_{loc}",
                        loc, status, pkt.get("surfaces", "terminal"),
                        self._get_loc_text(pkt, loc, "title"),
                        self._get_loc_text(pkt, loc, "scanner"),
                        self._get_loc_text(pkt, loc, "codex"),
                        self._get_loc_text(pkt, loc, "audio"),
                        self._get_loc_text(pkt, loc, "term"),
                        self._get_loc_text(pkt, loc, "field"),
                        notes
                    ])

        # 2. Terminology Lock
        if self.terminology:
            csv_term = backlog_dir / f"{prefix}TERMINOLOGY_LOCK_TABLE_{date_suffix}.csv"
            with open(csv_term, 'w', newline='', encoding='utf-8') as f:
                writer = csv.writer(f)
                writer.writerow(["term_key"] + self.LOCALES + ["lock_policy","notes"])
                for i, t in enumerate(self.terminology):
                    row = [f"TERM_{i:04d}"]
                    for loc in self.LOCALES:
                        if loc == "en_US": row.append(t["en"])
                        elif loc == "ru_RU": row.append(t["ru"])
                        else: row.append(f"[{loc[:2].upper()}] {t['en']}")
                    row.append(t["lock_policy"])
                    row.append(t["notes"])
                    writer.writerow(row)

        # 3. QA Matrix
        csv_qa = backlog_dir / f"{prefix}LOCALIZATION_QA_MATRIX_{date_suffix}.csv"
        with open(csv_qa, 'w', newline='', encoding='utf-8') as f:
            writer = csv.writer(f)
            writer.writerow(["release_set","packet_id","locale","check_type","status","finding","risk","next_review_action"])
            for pkt in self.packets:
                for loc in self.LOCALES:
                    writer.writerow([pkt.get("release_set", "RS_UNASSIGNED"), pkt["id"], loc, "VOICE_REGISTER", "NEEDS_NATIVE_REVIEW" if loc != "en_US" else "PASS", "Pending", "Low", "Schedule Review"])

        # 4. Route Cards
        csv_route = route_cards_dir / f"{prefix}route_cards.csv"
        with open(csv_route, 'w', newline='', encoding='utf-8') as f:
            writer = csv.writer(f)
            writer.writerow(["release_set","packet_id","route_moment","first_20_relevance","evidence_object","unlock_condition","spoiler_level","surfaces","player_decision_changed","canon_sources","localization_risk","implementation_note"])
            for pkt in self.packets:
                writer.writerow([
                    pkt.get("release_set", "RS_UNASSIGNED"), pkt["id"], "Standard Decode", "low",
                    pkt.get("evidence_object", "terminal"), pkt.get("unlock_condition", "explore"),
                    pkt.get("spoiler_level", "low"), pkt.get("surfaces", "terminal"),
                    pkt.get("player_decision_changed", "none"), pkt.get("canon_sources", "Lore_Bible.md"),
                    pkt.get("localization_risk", "none"), pkt.get("implementation_note", "none")
                ])

        # 5. Crosslink Graph
        if self.crosslinks:
            csv_cross = route_cards_dir / f"{prefix}crosslink_graph.csv"
            with open(csv_cross, 'w', newline='', encoding='utf-8') as f:
                writer = csv.writer(f)
                writer.writerow(["source_packet","target_packet","edge_type","shared_term","spoiler_relation","reason"])
                for c in self.crosslinks:
                    writer.writerow([c["src"], c["tgt"], c["edge"], c["term"], c["relation"], c["reason"]])

        print(f"H8-LDA Pipeline Execution Complete. Generated {5 if self.terminology and self.crosslinks else 3} core CSVs.")

if __name__ == "__main__":
    # Example / Self-Test Usage
    try:
        pipeline = H8LDAPipeline(out_dir=r"C:\hades\Hecton8\Docs\Lore\AppliedContent")
        
        # Test valid packet
        pipeline.add_packet({
            "id": "P_TEST_VALID",
            "release_set": "RS_LDA_TEST",
            "title_en": "System Audit",
            "codex_en": "Deep Reach liability doctrine activated. Loss of oxygen reserves deemed acceptable variance.",
            "title_ru": "Системный аудит",
            "codex_ru": "Доктрина ответственности Дип Рич активирована. Потеря запасов кислорода признана допустимой погрешностью."
        })
        
        pipeline.execute_build("RS_LDA_TEST")
        print("Self-Test PASSED.")
    except Exception as e:
        print(f"Self-Test FAILED: {e}")

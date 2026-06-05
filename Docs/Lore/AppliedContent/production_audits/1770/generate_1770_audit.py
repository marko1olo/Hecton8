import csv
import json
import re
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[5]
APP = ROOT / "Docs" / "Lore" / "AppliedContent"
AUD = APP / "production_audits" / "1770"
TASK_STATUS = ROOT / "Docs" / "Tasks" / "Status_1770.md"
RATIONALE = ROOT / "Docs" / "AgentLogs" / "Rationale_1770.md"

LOCALES = [
    "en_US", "ru_RU", "ja_JP", "zh_CN", "fr_FR", "es_ES", "de_DE",
    "pl_PL", "uk_UA", "ar_SA", "id_ID", "ko_KR", "he_IL", "pt_BR", "nl_NL",
]
RTL = {"ar_SA", "he_IL"}
CJK = {"ja_JP", "zh_CN", "ko_KR"}
DRAFT_MARKERS = ["Draft ", "draft ", "pending native pass", "native pass pending", "draft_native_pass_pending"]
MOJIBAKE_MARKERS = ["Ð", "Ñ", "Ã", "Â", "å", "æ", "ã", "ç", "è", "é", "ä", "ï¼", "œ", "€"]
CANON_OWNER_DEFAULT = "Docs/Lore/Canon_Locks.md;Docs/Lore/Lore_Bible.md"

SURFACE_PREFIX = {
    "scanner": "scanner",
    "scanner_bathy_drop": "scanner",
    "scanner_heat_shield": "scanner",
    "terminal": "terminal",
    "terminal_diag": "terminal",
    "audio": "audio",
    "audio_line": "audio",
    "audio_subtitle": "audio",
    "in_game_wiki": "in_game_wiki",
    "external_site": "external_site",
    "field_note": "field_note",
    "website_archive": "website_archive",
    "codex": "in_game_wiki",
}

BRIEF_TERMS = {
    "brief", "backlog", "proof", "rule", "rules", "qa", "handoff",
    "table", "placement", "protocol", "composition", "review",
}

AUTHORING_INSTRUCTION_RE = re.compile(
    r"\b(Use for|art constraints|UI labels|source packet|proof card|future runtime|placement brief|authoring evidence only)\b",
    re.I,
)
DARK_BAD_RE = re.compile(
    r"\b(permanent dark|pitch-black surface|black because|brown-dwarf horror void|deep-space darkness|dark from orbit|black from orbit|weak starlight)\b",
    re.I,
)
FIRST_BAD_RE = re.compile(
    r"\b(first extrasolar|first remote colony|first colony beyond|first gas giant claim|first world beyond Earth|first proof that interstellar settlement)\b",
    re.I,
)
FTL_BAD_RE = re.compile(r"\b(FTL|faster-than-light|ansible|reactionless|instant rescue)\b", re.I)
ATLAS_BAD_RE = re.compile(r"\b(sadistic|villain|evil AI|hated humans|consciously murder|murderer)\b", re.I)
DEEP_BAD_RE = re.compile(r"\b(cartoon evil|villain confession|melted the moon|murdered the moon)\b", re.I)
FAMILY_BAD_RE = re.compile(
    r"\b(father|mother|wife|husband|daughter|son|sister|brother|family revenge|lost family)\b",
    re.I,
)
OMNI_TERMS_RE = re.compile(
    r"\b(final truth|the real reason|Deep Reach crime|classified weighting|ending|payload receiver|Atlas basin outcome|full disaster chain)\b",
    re.I,
)


def rel(path: Path) -> str:
    try:
        return path.relative_to(ROOT).as_posix()
    except ValueError:
        return str(path).replace("\\", "/")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def load_json(path: Path):
    with path.open("r", encoding="utf-8", errors="replace") as handle:
        return json.load(handle)


def iter_packets():
    for path in sorted((APP / "packets").glob("*.json")):
        data = load_json(path)
        if isinstance(data, dict) and "packets" in data:
            release_set = data.get("release_set_id", "")
            for packet in data.get("packets") or []:
                yield path, data, packet, release_set
        elif isinstance(data, dict) and "packet_id" in data:
            yield path, None, data, data.get("release_set_id", "")


def title_en(packet: dict) -> str:
    localized = packet.get("localized") or {}
    en = localized.get("en_US") or {}
    if isinstance(en, dict):
        return en.get("title", "")
    return packet.get("title", "")


def unlock_id(packet: dict) -> str:
    unlock = packet.get("unlock") or {}
    if isinstance(unlock, dict):
        return unlock.get("primary") or unlock.get("id") or ""
    return str(unlock) if unlock else ""


def infer_surfaces(packet: dict) -> list[str]:
    surfaces = set(packet.get("surfaces") or [])
    localized = packet.get("localized") or {}
    en = localized.get("en_US") or (next(iter(localized.values()), {}) if localized else {})
    if isinstance(en, dict):
        for key in en:
            if key == "title":
                continue
            if key in SURFACE_PREFIX:
                surfaces.add(SURFACE_PREFIX[key])
                continue
            for prefix, surface in SURFACE_PREFIX.items():
                if key.startswith(prefix):
                    surfaces.add(surface)
                    break
    normalized = {"audio" if s == "audio_subtitle" else s for s in surfaces}
    return sorted(normalized)


def locale_state(packet: dict, locale: str):
    row = (packet.get("localized") or {}).get(locale)
    if not isinstance(row, dict):
        return "missing", False, False, False, False
    values = [str(value) for value in row.values() if isinstance(value, str)]
    joined = "\n".join(values)
    draft = any(marker in joined for marker in DRAFT_MARKERS)
    mojibake = any(marker in joined for marker in MOJIBAKE_MARKERS)
    has_title = bool(row.get("title"))
    required_ok = (
        has_title
        and any(key == "scanner" or key.startswith("scanner_") for key in row)
        and any(key == "terminal" or key.startswith("terminal_") for key in row)
        and any(key == "audio" or key.startswith("audio_") for key in row)
        and bool(row.get("in_game_wiki"))
        and bool(row.get("external_site"))
    )
    if draft:
        state = "pending_native_review"
    elif mojibake and locale != "en_US":
        state = "machine_draft"
    else:
        state = "source_ready" if locale == "en_US" else "machine_draft"
    return state, required_ok, draft, mojibake, has_title


def split_packet_ids(raw: str) -> list[str]:
    return [item for item in re.split(r"[;|,\s]+", raw or "") if item.startswith("P")]


def rs_sort_key(release_set: str) -> int:
    match = re.match(r"RS(\d+)", release_set or "")
    return int(match.group(1)) if match else 9999


def infer_spoiler(packet: dict, route_rows: list[dict]) -> int:
    text = " ".join(
        [
            packet.get("packet_id", ""),
            packet.get("release_set_id", ""),
            packet.get("article_id", ""),
            title_en(packet),
            " ".join(str(row.get("ending_pressure", "")) for row in route_rows),
            " ".join(str(row.get("truth_payload", "")) for row in route_rows),
        ]
    ).lower()
    if any(
        word in text
        for word in [
            "ending",
            "final_payload",
            "final payload",
            "atlas_final",
            "false_exit",
            "payload receiver",
            "no_clean",
            "severance",
            "public ledger",
            "quarantine hold",
            "corporate capture",
        ]
    ):
        return 4
    if any(
        word in text
        for word in [
            "atlas",
            "deep reach lie",
            "true cause",
            "liability proof",
            "classification",
            "cleanse",
            "return action",
            "evacuation hold",
            "claim-loss",
            "basin",
        ]
    ):
        return 3
    if any(
        word in text
        for word in [
            "brine",
            "abyss",
            "pressure glass",
            "blue debt",
            "resource",
            "mid-depth",
            "worker",
            "colony",
            "dossier",
            "contract risk",
        ]
    ):
        return 2
    if any(
        word in text
        for word in [
            "public",
            "aegir",
            "no-ftl",
            "moon",
            "ship",
            "travel",
            "home",
            "site",
            "starting premise",
            "hecton8 geology",
        ]
    ):
        return 0
    return 1


def forbidden_surfaces(spoiler: int, surfaces: list[str], has_authoring_field_note: bool) -> list[str]:
    forbidden = set()
    if spoiler >= 3:
        forbidden.update(["public_site_open", "pre_evidence_audio", "pre_evidence_scanner"])
    if spoiler >= 4:
        forbidden.update(["spoiler_free_wiki", "marketing_public_page"])
    if has_authoring_field_note:
        forbidden.add("field_note_runtime_until_rewritten")
    if "scanner" not in surfaces:
        forbidden.add("scanner_without_short_form")
    return sorted(forbidden) or ["none_identified_static"]


def is_negating_bad_term(text: str) -> bool:
    return any(
        term in text
        for term in [
            "not",
            "no ",
            "no-",
            "did not",
            "without",
            "rather than",
            "none of them",
            "cannot",
            "can not",
        ]
    )


def has_atlas_context(packet: dict, field: str, value: str) -> bool:
    joined = " ".join(
        [
            packet.get("packet_id", ""),
            packet.get("release_set_id", ""),
            packet.get("article_id", ""),
            title_en(packet),
            field,
            value,
        ]
    ).lower()
    return (
        "atlas" in joined
        or re.search(r"\bai\b", joined) is not None
        or "artificial intelligence" in joined
        or "classification" in joined
        or "repair logic" in joined
    )


def write_simple_list(handle, title: str, items: list[str], limit: int = 100):
    handle.write(f"## {title}\n\n")
    if not items:
        handle.write("None found.\n\n")
        return
    for item in items[:limit]:
        handle.write(f"- `{item}`\n")
    if len(items) > limit:
        handle.write(f"- TRUNCATED: {len(items) - limit} additional rows.\n")
    handle.write("\n")


def main():
    AUD.mkdir(parents=True, exist_ok=True)
    packets = []
    packet_by_id = {}
    for path, bundle, packet, bundle_release in iter_packets():
        row = dict(packet)
        row["_path"] = path
        if not row.get("release_set_id"):
            row["release_set_id"] = bundle_release
        row["_schema"] = (bundle or row).get("schema", "")
        row["_title_en"] = title_en(row)
        row["_surfaces"] = infer_surfaces(row)
        row["_unlock"] = unlock_id(row)
        packets.append(row)
        packet_by_id[row.get("packet_id", "")] = row

    route_rows_by_packet = defaultdict(list)
    route_packet_ids = set()
    for path in sorted((APP / "route_cards").glob("*.csv")):
        with path.open("r", encoding="utf-8", errors="replace", newline="") as handle:
            for row in csv.DictReader(handle):
                for packet_id in split_packet_ids(row.get("packet_ids", "") or row.get("packet_id", "")):
                    route_packet_ids.add(packet_id)
                    route_rows_by_packet[packet_id].append(dict(row, _file=rel(path)))

    binding_packet_ids = set()
    runtime_binding_ids = set()
    scene_binding_ids = set()
    for path in sorted((APP / "binding_maps").glob("*.csv")):
        with path.open("r", encoding="utf-8", errors="replace", newline="") as handle:
            for row in csv.DictReader(handle):
                packet_ids = []
                for key in ("packet_id", "packet_ids"):
                    packet_ids.extend(split_packet_ids(row.get(key, "")))
                for packet_id in packet_ids:
                    binding_packet_ids.add(packet_id)
                    if "runtime_binding_map" in path.name:
                        runtime_binding_ids.add(packet_id)
                    if "scene_binding_targets" in path.name or "scene_placement_plan" in path.name:
                        scene_binding_ids.add(packet_id)

    with (AUD / "packet_inventory.csv").open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=[
                "packet_id",
                "release_set_id",
                "article_id",
                "title_en",
                "surfaces_present",
                "locale_count",
                "route_card_present",
                "binding_map_present",
            ],
        )
        writer.writeheader()
        for packet in sorted(packets, key=lambda item: item.get("packet_id", "")):
            packet_id = packet.get("packet_id", "")
            writer.writerow(
                {
                    "packet_id": packet_id,
                    "release_set_id": packet.get("release_set_id", ""),
                    "article_id": packet.get("article_id", ""),
                    "title_en": packet.get("_title_en", ""),
                    "surfaces_present": ";".join(packet.get("_surfaces", [])),
                    "locale_count": len((packet.get("localized") or {}).keys()),
                    "route_card_present": "yes" if packet_id in route_packet_ids else "no",
                    "binding_map_present": "yes" if packet_id in binding_packet_ids else "no",
                }
            )

    release_sets = defaultdict(lambda: {"packets": [], "surfaces": set(), "route_surfaces": set(), "packet_paths": set()})
    for packet in packets:
        release_set = packet.get("release_set_id", "")
        release_sets[release_set]["packets"].append(packet.get("packet_id", ""))
        release_sets[release_set]["surfaces"].update(packet.get("_surfaces", []))
        release_sets[release_set]["packet_paths"].add(rel(packet["_path"]))
    for packet_id, rows in route_rows_by_packet.items():
        release_set = packet_by_id.get(packet_id, {}).get("release_set_id", "")
        for row in rows:
            if row.get("primary_surface"):
                release_sets[release_set]["route_surfaces"].add(row["primary_surface"])

    for md_path in sorted((APP / "release_sets").glob("RS*.md")):
        match = re.match(r"(RS\d+)", md_path.stem)
        if not match:
            continue
        prefix = match.group(1)
        matching = [release_set for release_set in release_sets if release_set.startswith(prefix)] or [md_path.stem]
        lines = [line.strip() for line in read_text(md_path).splitlines() if line.strip()]
        title = next((line.lstrip("# ").strip() for line in lines if line.startswith("#")), md_path.stem)
        purpose = next(
            (
                line
                for line in lines
                if not line.startswith("#")
                and not line.lower().startswith("status:")
                and not line.lower().startswith("runtime rule:")
            ),
            "",
        )
        for release_set in matching:
            release_sets[release_set]["title"] = title
            release_sets[release_set]["purpose"] = purpose
            release_sets[release_set]["md_path"] = rel(md_path)

    for manifest_path in sorted((APP / "release_sets").glob("RS*.json")):
        data = load_json(manifest_path)
        release_set = data.get("release_set_id") or manifest_path.stem.replace("_manifest", "")
        release_sets[release_set]["manifest_path"] = rel(manifest_path)
        release_sets[release_set]["status"] = data.get("status", "")
        for packet_id in data.get("packets") or []:
            if packet_id not in release_sets[release_set]["packets"]:
                release_sets[release_set]["packets"].append(packet_id)

    with (AUD / "release_set_inventory.md").open("w", encoding="utf-8", newline="") as handle:
        handle.write("# Release Set Inventory - 1770\n\n")
        handle.write("Evidence class: STATIC_SOURCE. Runtime/content readiness is not implied.\n\n")
        handle.write("Focus: RS070-RS092 plus adjacent RS065-RS069. Full current release-set roster is included to avoid orphaning active packets.\n\n")
        handle.write("| Release set | Purpose | Packet IDs | Expected player/site surface | Content class | Proof paths |\n")
        handle.write("|---|---|---|---|---|---|\n")
        for release_set in sorted([key for key in release_sets if key], key=rs_sort_key):
            data = release_sets[release_set]
            purpose = (data.get("purpose") or data.get("title") or "NO_PURPOSE_TEXT_FOUND").replace("|", "/")
            class_text = " ".join([data.get("title", ""), purpose]).lower()
            content_class = "brief_or_handoff" if any(term in class_text for term in BRIEF_TERMS) else "actual_content_packet"
            surfaces = sorted(data["surfaces"] | data["route_surfaces"])
            proof = "; ".join(
                item
                for item in [
                    data.get("md_path", ""),
                    data.get("manifest_path", ""),
                    ";".join(sorted(data.get("packet_paths", []))),
                ]
                if item
            )
            handle.write(
                f"| {release_set} | {purpose} | {';'.join(data['packets'])} | "
                f"{';'.join(surfaces) or 'not_mapped'} | {content_class} | {proof} |\n"
            )

    checkpoint05 = (
        f"Packet inventory generated from {len(packets)} packets across {len(release_sets)} release-set ids. "
        f"Route-card hits: {len(route_packet_ids & set(packet_by_id))}. "
        f"Binding-map hits: {len(binding_packet_ids & set(packet_by_id))}. "
        "No dependency on 1771-1779 output."
    )

    conflicts = []

    def add_conflict(severity, topic, path, packet_id, field, evidence, rule, action):
        conflicts.append(
            {
                "severity": severity,
                "topic": topic,
                "path": path,
                "packet_id": packet_id,
                "field": field,
                "evidence": evidence.replace("\n", " ")[:220],
                "rule": rule,
                "action": action,
            }
        )

    for packet in packets:
        packet_id = packet.get("packet_id", "")
        path = rel(packet["_path"])
        en = (packet.get("localized") or {}).get("en_US") or {}
        if not isinstance(en, dict):
            continue
        for field, value in en.items():
            if not isinstance(value, str):
                continue
            low = value.lower()
            if field == "field_note" and AUTHORING_INSTRUCTION_RE.search(value):
                add_conflict(
                    "BLOCK_RUNTIME",
                    "authoring_instruction_in_player_surface",
                    path,
                    packet_id,
                    field,
                    value,
                    "writing.md requires field notes to be real artifacts, not writer instructions.",
                    "Do not ship as runtime field_note; rewrite as Marauder/technician note or mark source-only.",
                )
            if DARK_BAD_RE.search(value):
                if "not" in low or "instead" in low or "readable" in low:
                    add_conflict(
                        "ACCEPTABLE_VARIANT",
                        "surface_brightness_defense",
                        path,
                        packet_id,
                        field,
                        value,
                        "Bright surface lock allows explicit rejection of darkness-first framing.",
                        "Keep if wording does not imply permanent dark surface.",
                    )
                else:
                    add_conflict(
                        "BLOCK_PUBLIC_SITE",
                        "surface_darkness_conflict",
                        path,
                        packet_id,
                        field,
                        value,
                        "Surface/sky/photic shallows must be bright/readable; darkness belongs to depth/storm/eclipses.",
                        "Rewrite before public/runtime use.",
                    )
            if FIRST_BAD_RE.search(value):
                if "not a first" in low or "not the first" in low or "not humanity" in low or "before later frontier" in low:
                    add_conflict(
                        "ACCEPTABLE_VARIANT",
                        "aegir_not_first_extrasolar_defense",
                        path,
                        packet_id,
                        field,
                        value,
                        "Aegir is not humanity's first extrasolar system; explicit correction is valid.",
                        "Keep.",
                    )
                else:
                    add_conflict(
                        "BLOCK_PUBLIC_SITE",
                        "aegir_first_extrasolar_conflict",
                        path,
                        packet_id,
                        field,
                        value,
                        "Aegir is not humanity's first extrasolar system/colony/claim.",
                        "Rewrite before publication.",
                    )
            if FTL_BAD_RE.search(value):
                if any(
                    term in low
                    for term in [
                        "no ftl",
                        "no-ftl",
                        "without ftl",
                        "without live ftl",
                        "without inventing ftl",
                        "no ansible",
                        "no-ansible",
                        "no reactionless",
                        "no instant",
                        "not instant",
                        "cannot perform instant",
                        "none of them outrun light",
                        "obey light speed",
                        "obeys light speed",
                        "limited by light speed",
                        "rather than instant ftl",
                        "non-ftl",
                        "not reached by ftl",
                        "not ftl",
                    ]
                ):
                    add_conflict(
                        "ACCEPTABLE_VARIANT",
                        "no_ftl_or_instant_rescue_defense",
                        path,
                        packet_id,
                        field,
                        value,
                        "No-FTL/no-ansible/no-instant-rescue lock.",
                        "Keep.",
                    )
                else:
                    add_conflict(
                        "BLOCK_RUNTIME",
                        "ftl_or_instant_rescue_conflict",
                        path,
                        packet_id,
                        field,
                        value,
                        "No FTL, ansible, reactionless travel, or instant rescue.",
                        "Rewrite before use.",
                    )
            if ATLAS_BAD_RE.search(value):
                if is_negating_bad_term(low):
                    add_conflict(
                        "ACCEPTABLE_VARIANT",
                        "atlas_not_villain_defense",
                        path,
                        packet_id,
                        field,
                        value,
                        "Atlas is damaged classification/repair logic, not sadistic AI.",
                        "Keep.",
                    )
                elif has_atlas_context(packet, field, value):
                    add_conflict(
                        "BLOCK_RUNTIME",
                        "atlas_villain_conflict",
                        path,
                        packet_id,
                        field,
                        value,
                        "Atlas-6 is not a sadistic villain.",
                        "Rewrite before use.",
                    )
                else:
                    add_conflict(
                        "ACCEPTABLE_VARIANT",
                        "non_atlas_villain_language_not_canon_conflict",
                        path,
                        packet_id,
                        field,
                        value,
                        "Villain wording is not automatically an Atlas canon conflict when Atlas is not the subject.",
                        "Review prose tone only if the local source voice sounds cartoonish.",
                    )
            if DEEP_BAD_RE.search(value):
                if is_negating_bad_term(low):
                    add_conflict(
                        "ACCEPTABLE_VARIANT",
                        "deep_reach_not_cartoon_defense",
                        path,
                        packet_id,
                        field,
                        value,
                        "Deep Reach guilt model is procedural negligence/liability shielding.",
                        "Keep.",
                    )
                else:
                    add_conflict(
                        "FIX_SOURCE_TEXT",
                        "deep_reach_cartoon_evil_conflict",
                        path,
                        packet_id,
                        field,
                        value,
                        "Deep Reach guilt model is procedural negligence/liability shielding.",
                        "Rewrite local sentence.",
                    )
            if FAMILY_BAD_RE.search(value):
                if any(term in low for term in ["not family", "no-family", "not relatives", "family-revenge remains forbidden", "without family"]):
                    add_conflict(
                        "ACCEPTABLE_VARIANT",
                        "player_no_family_hook_defense",
                        path,
                        packet_id,
                        field,
                        value,
                        "Player is ex-Deep-Reach/current Marauder without family revenge hook.",
                        "Keep.",
                    )
                elif any(term in low for term in ["father", "mother", "wife", "husband", "daughter", "son"]):
                    add_conflict(
                        "FIX_SOURCE_TEXT",
                        "player_origin_family_hook_risk",
                        path,
                        packet_id,
                        field,
                        value,
                        "Player motive is professional/ex-Deep-Reach recognition, not family revenge.",
                        "Check and rewrite if this references protagonist motive.",
                    )

    with (AUD / "canon_conflict_audit.md").open("w", encoding="utf-8") as handle:
        handle.write("# Canon Conflict Audit - 1770\n\n")
        handle.write("Evidence class: STATIC_SOURCE. This audit scans packet JSON English authority rows and owned indexes; it does not prove runtime bake state.\n\n")
        handle.write("Severity meanings: BLOCK_RUNTIME, BLOCK_PUBLIC_SITE, FIX_SOURCE_TEXT, LOCALIZATION_ONLY, ACCEPTABLE_VARIANT.\n\n")
        handle.write("| Severity | Topic | Packet | Field | Path | Evidence | Rule / action |\n")
        handle.write("|---|---|---|---|---|---|---|\n")
        for conflict in conflicts:
            evidence = conflict["evidence"].replace("|", "/")
            rule_action = (conflict["rule"] + " Action: " + conflict["action"]).replace("|", "/")
            handle.write(
                f"| {conflict['severity']} | {conflict['topic']} | {conflict['packet_id']} | "
                f"{conflict['field']} | {conflict['path']} | {evidence} | {rule_action} |\n"
            )
        if not conflicts:
            handle.write("| None | none |  |  |  | No static conflicts found. |  |\n")

    with (AUD / "surface_ownership_matrix.csv").open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=[
                "article_id",
                "packet_id",
                "canonical_owner_doc",
                "allowed_surfaces",
                "forbidden_surfaces",
                "spoiler_level",
                "unlock_gate",
                "in_world_voice_options",
                "runtime_layer",
            ],
        )
        writer.writeheader()
        for packet in sorted(packets, key=lambda item: item.get("packet_id", "")):
            packet_id = packet.get("packet_id", "")
            route_rows = route_rows_by_packet.get(packet_id, [])
            surfaces = sorted(
                set(packet.get("_surfaces", []))
                | {row.get("primary_surface", "") for row in route_rows if row.get("primary_surface")}
            )
            spoiler = infer_spoiler(packet, route_rows)
            en = (packet.get("localized") or {}).get("en_US") or {}
            field_note = str(en.get("field_note", "")) if isinstance(en, dict) else ""
            has_authoring_field_note = bool(AUTHORING_INSTRUCTION_RE.search(field_note))
            voices = [surface for surface in ["scanner", "terminal", "audio", "field_note", "in_game_wiki", "external_site"] if surface in surfaces]
            canon_owner = packet.get("canon_owner")
            if isinstance(canon_owner, list):
                canon_owner = ";".join(canon_owner)
            if not canon_owner:
                canon_owner = CANON_OWNER_DEFAULT
            writer.writerow(
                {
                    "article_id": packet.get("article_id", ""),
                    "packet_id": packet_id,
                    "canonical_owner_doc": canon_owner,
                    "allowed_surfaces": ";".join(surfaces),
                    "forbidden_surfaces": ";".join(forbidden_surfaces(spoiler, surfaces, has_authoring_field_note)),
                    "spoiler_level": spoiler,
                    "unlock_gate": packet.get("_unlock", "") or ";".join(sorted({row.get("phase_id", "") for row in route_rows if row.get("phase_id")})),
                    "in_world_voice_options": ";".join(voices) if voices else "none_identified_static",
                    "runtime_layer": "World" if surfaces == ["scanner"] else "Narrative",
                }
            )

    omni_hits = []
    for packet in packets:
        packet_id = packet.get("packet_id", "")
        spoiler = infer_spoiler(packet, route_rows_by_packet.get(packet_id, []))
        en = (packet.get("localized") or {}).get("en_US") or {}
        if not isinstance(en, dict) or spoiler < 3:
            continue
        for field in ["scanner", "scanner_bathy_drop", "scanner_heat_shield", "audio", "audio_line", "field_note"]:
            value = en.get(field)
            if isinstance(value, str) and OMNI_TERMS_RE.search(value):
                omni_hits.append((packet_id, field, value[:160], rel(packet["_path"])))

    if omni_hits:
        with (AUD / "surface_omniscience_risk_hits.csv").open("w", encoding="utf-8", newline="") as handle:
            writer = csv.writer(handle)
            writer.writerow(["packet_id", "field", "evidence", "path"])
            writer.writerows(omni_hits)
    checkpoint10 = (
        f"Surface matrix generated for {len(packets)} packets. "
        f"Static scanner/audio high-spoiler omniscience hits: {len(omni_hits)}. "
        + ("See surface_omniscience_risk_hits.csv." if omni_hits else "No direct scanner/audio omniscience markers found by static term scan.")
    )

    with (AUD / "locale_coverage_matrix.csv").open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=[
                "packet_id",
                "article_id",
                "release_set_id",
                "locale",
                "locale_present",
                "required_fields_present",
                "player_visible_draft_markers",
                "rtl_flag",
                "cjk_flag",
                "suspected_mojibake_flag",
                "title_mismatch_flag",
                "localization_state",
            ],
        )
        writer.writeheader()
        for packet in sorted(packets, key=lambda item: item.get("packet_id", "")):
            for locale in LOCALES:
                row = (packet.get("localized") or {}).get(locale)
                state, required_ok, draft, mojibake, has_title = locale_state(packet, locale)
                title_mismatch = False
                if isinstance(row, dict) and has_title:
                    title = row.get("title", "")
                    title_mismatch = bool(re.search(r"P\d{3}_", title))
                writer.writerow(
                    {
                        "packet_id": packet.get("packet_id", ""),
                        "article_id": packet.get("article_id", ""),
                        "release_set_id": packet.get("release_set_id", ""),
                        "locale": locale,
                        "locale_present": "yes" if isinstance(row, dict) else "no",
                        "required_fields_present": "yes" if required_ok else "no",
                        "player_visible_draft_markers": "yes" if draft else "no",
                        "rtl_flag": "yes" if locale in RTL else "no",
                        "cjk_flag": "yes" if locale in CJK else "no",
                        "suspected_mojibake_flag": "yes" if mojibake else "no",
                        "title_mismatch_flag": "yes" if title_mismatch else "no",
                        "localization_state": state,
                    }
                )

    surface_rows = []
    with (APP / "Publication_Surface_Index.csv").open("r", encoding="utf-8", errors="replace", newline="") as handle:
        surface_rows = list(csv.DictReader(handle))
    indexed_paths = {row.get("page_path", "").replace("\\", "/") for row in surface_rows}
    missing_pages = []
    wrong_locale = []
    inconsistent_titles = []
    for row in surface_rows:
        page_rel = row.get("page_path", "").replace("\\", "/")
        page = APP / page_rel
        if not page.exists():
            missing_pages.append(row)
            continue
        parts = Path(page_rel).parts
        if len(parts) >= 2:
            surface, locale = parts[0], parts[1]
            if surface != row.get("surface") or locale != row.get("locale") or locale not in LOCALES:
                wrong_locale.append(row)
        h1 = ""
        for line in read_text(page).splitlines():
            if line.startswith("# "):
                h1 = line[2:].strip()
                break
        if h1 and row.get("title") and h1 != row.get("title"):
            inconsistent_titles.append((row, h1))
    actual_pages = set()
    for surface in ["external_site", "in_game_wiki"]:
        for page in (APP / surface).glob("*/*.md"):
            if page.name.upper() != "INDEX.MD":
                actual_pages.add(page.relative_to(APP).as_posix())
    orphan_pages = sorted(actual_pages - indexed_paths)
    seen = set()
    duplicate_packet_keys = []
    for row in surface_rows:
        key = (row.get("surface"), row.get("locale"), row.get("packet_id"))
        if key in seen:
            duplicate_packet_keys.append(key)
        seen.add(key)
    with (AUD / "publication_surface_crosscheck.md").open("w", encoding="utf-8") as handle:
        handle.write("# Publication Surface Cross-Check - 1770\n\n")
        handle.write("Evidence class: STATIC_SOURCE. Checked `Publication_Surface_Index.csv` against generated `external_site` and `in_game_wiki` markdown pages.\n\n")
        handle.write(f"- Indexed rows: {len(surface_rows)}\n")
        handle.write(f"- Missing pages: {len(missing_pages)}\n")
        handle.write(f"- Orphan pages: {len(orphan_pages)}\n")
        handle.write(f"- Duplicate packet IDs within surface/locale: {len(duplicate_packet_keys)}\n")
        handle.write(f"- Wrong surface/locale folder rows: {len(wrong_locale)}\n")
        handle.write(f"- Inconsistent page H1 vs index title: {len(inconsistent_titles)}\n\n")
        write_simple_list(handle, "Missing Pages", [row.get("page_path", "") for row in missing_pages])
        write_simple_list(handle, "Orphan Pages", orphan_pages)
        write_simple_list(handle, "Duplicate Packet IDs", [str(item) for item in duplicate_packet_keys])
        write_simple_list(handle, "Wrong Locale Folders", [row.get("page_path", "") for row in wrong_locale])
        write_simple_list(
            handle,
            "Inconsistent Titles",
            [f"{row.get('page_path', '')} | index={row.get('title', '')} | page={h1}" for row, h1 in inconsistent_titles],
        )

    cluster_rows = []
    with (APP / "Publication_Cluster_Index.csv").open("r", encoding="utf-8", errors="replace", newline="") as handle:
        cluster_rows = list(csv.DictReader(handle))
    clusters = {}
    for row in cluster_rows:
        cluster_id = row.get("cluster_id", "")
        if not cluster_id:
            continue
        cluster = clusters.setdefault(
            cluster_id,
            {"rows": 0, "titles": Counter(), "spoilers": Counter(), "surfaces": Counter(), "packets": set(), "questions": set()},
        )
        cluster["rows"] += 1
        cluster["titles"][row.get("title", "")] += 1
        cluster["spoilers"][row.get("spoiler_tier", "")] += 1
        cluster["surfaces"][row.get("primary_surface", "")] += 1
        cluster["packets"].add(row.get("cluster_packet_id", ""))
        if row.get("player_question"):
            cluster["questions"].add(row["player_question"])
    website_map = read_text(ROOT / "Docs" / "Lore" / "Website_Publication_Map.md").lower()
    crosslink_graph = read_text(ROOT / "Docs" / "Lore" / "Lore_Crosslink_Graph.md").lower()
    with (AUD / "publication_cluster_crosscheck.md").open("w", encoding="utf-8") as handle:
        handle.write("# Publication Cluster Cross-Check - 1770\n\n")
        handle.write("Evidence class: STATIC_DOC / STATIC_SOURCE. Static topic matching only.\n\n")
        handle.write("| Cluster | Rows | Packet IDs | Dominant title | Spoiler tier | Surface class | Map/crosslink status | Player question |\n")
        handle.write("|---|---:|---|---|---|---|---|---|\n")
        for cluster_id, cluster in sorted(clusters.items()):
            spoiler_values = [int(value) for value in cluster["spoilers"] if str(value).isdigit()]
            max_spoiler = max(spoiler_values) if spoiler_values else -1
            if max_spoiler <= 1:
                surface_class = "public_safe_or_early_game"
            elif max_spoiler >= 4:
                surface_class = "spoiler_gated"
            else:
                surface_class = "controlled_midgame_or_unclear"
            title = cluster["titles"].most_common(1)[0][0] if cluster["titles"] else ""
            tokens = [
                token
                for token in re.split(r"\W+", f"{cluster_id} {title}".lower())
                if len(token) > 4 and token not in {"cluster", "hecton", "start", "public"}
            ]
            hit_web = any(token in website_map for token in tokens)
            hit_cross = any(token in crosslink_graph for token in tokens)
            status = "mapped" if hit_web and hit_cross else ("partial_map" if hit_web or hit_cross else "unclear")
            question = next(iter(cluster["questions"]), "")
            handle.write(
                f"| {cluster_id} | {cluster['rows']} | {';'.join(sorted(cluster['packets']))} | {title.replace('|', '/')} | "
                f"{max_spoiler if max_spoiler >= 0 else 'unknown'} | {surface_class} | {status} | {question.replace('|', '/')} |\n"
            )

    no_route = []
    no_binding = []
    no_scene = []
    no_runtime_binding = []
    no_unlock = []
    no_source_voice = []
    for packet in packets:
        packet_id = packet.get("packet_id", "")
        if packet_id not in route_packet_ids:
            no_route.append(packet_id)
        if packet_id not in binding_packet_ids:
            no_binding.append(packet_id)
        if packet_id not in scene_binding_ids:
            no_scene.append(packet_id)
        if packet_id not in runtime_binding_ids:
            no_runtime_binding.append(packet_id)
        if not packet.get("_unlock") and packet_id not in route_packet_ids:
            no_unlock.append(packet_id)
        if not any(surface in packet.get("_surfaces", []) for surface in ["scanner", "terminal", "audio", "field_note", "external_site", "in_game_wiki"]):
            no_source_voice.append(packet_id)
    with (AUD / "route_binding_crosscheck.md").open("w", encoding="utf-8") as handle:
        handle.write("# Route Card And Binding Map Cross-Check - 1770\n\n")
        handle.write("Evidence class: STATIC_SOURCE. CSV coverage only; Unity scene placement is not proven.\n\n")
        handle.write(f"- Packets checked: {len(packets)}\n")
        handle.write(f"- Missing route-card coverage: {len(no_route)}\n")
        handle.write(f"- Missing any binding-map coverage: {len(no_binding)}\n")
        handle.write(f"- Missing runtime binding map row: {len(no_runtime_binding)}\n")
        handle.write(f"- Missing scene binding target row: {len(no_scene)}\n")
        handle.write(f"- Missing unlock route by packet JSON and route card: {len(no_unlock)}\n")
        handle.write(f"- Missing source voice/surface in packet JSON: {len(no_source_voice)}\n\n")
        for title, items in [
            ("Missing route-card coverage", no_route),
            ("Missing any binding-map coverage", no_binding),
            ("Missing runtime binding row", no_runtime_binding),
            ("Missing scene binding target", no_scene),
            ("Missing unlock route", no_unlock),
            ("Missing source voice", no_source_voice),
        ]:
            handle.write(f"## {title}\n\n")
            if not items:
                handle.write("None found.\n\n")
            else:
                for packet_id in items[:200]:
                    packet = packet_by_id.get(packet_id, {})
                    handle.write(f"- `{packet_id}` `{packet.get('release_set_id', '')}` `{rel(packet.get('_path', Path('.'))) if packet.get('_path') else 'unknown'}`\n")
                if len(items) > 200:
                    handle.write(f"- TRUNCATED: {len(items) - 200} additional packet IDs.\n")
                handle.write("\n")

    conflict_counts = Counter(conflict["severity"] for conflict in conflicts)
    locale_missing = 0
    locale_draft = 0
    locale_mojibake = 0
    for packet in packets:
        for locale in LOCALES:
            state, _, draft, mojibake, _ = locale_state(packet, locale)
            locale_missing += int(state == "missing")
            locale_draft += int(draft)
            locale_mojibake += int(mojibake)

    runtime_ready = []
    needs_writer = []
    needs_loc = []
    needs_binding = []
    must_not_ship = []
    for packet in packets:
        packet_id = packet.get("packet_id", "")
        packet_conflicts = [
            conflict
            for conflict in conflicts
            if conflict["packet_id"] == packet_id and conflict["severity"] in {"BLOCK_RUNTIME", "BLOCK_PUBLIC_SITE", "FIX_SOURCE_TEXT"}
        ]
        has_loc_block = any(
            locale_state(packet, locale)[0] == "missing"
            or locale_state(packet, locale)[2]
            or locale_state(packet, locale)[3]
            for locale in LOCALES
        )
        has_binding_block = packet_id not in route_packet_ids or packet_id not in binding_packet_ids
        if any(conflict["severity"] in {"BLOCK_RUNTIME", "BLOCK_PUBLIC_SITE"} for conflict in packet_conflicts):
            must_not_ship.append(packet_id)
        elif packet_conflicts:
            needs_writer.append(packet_id)
        elif has_loc_block:
            needs_loc.append(packet_id)
        elif has_binding_block:
            needs_binding.append(packet_id)
        else:
            runtime_ready.append(packet_id)

    with (AUD / "lore_sorting_decisions.md").open("w", encoding="utf-8") as handle:
        handle.write("# Lore Sorting Decisions - 1770\n\n")
        handle.write("Evidence class: STATIC_SOURCE. Runtime, Unity scene placement, native localization and public publication remain PENDING VERIFICATION unless separate artifacts prove them.\n\n")
        handle.write("## Summary\n\n")
        handle.write(f"- Packets inventoried: {len(packets)}\n")
        handle.write(f"- Release sets inventoried: {len(release_sets)}\n")
        handle.write(f"- Conflict rows: {len(conflicts)} ({dict(conflict_counts)})\n")
        handle.write(f"- Locale missing rows: {locale_missing}\n")
        handle.write(f"- Player-visible draft marker rows: {locale_draft}\n")
        handle.write(f"- Suspected mojibake rows: {locale_mojibake}\n")
        handle.write(f"- Packets with route-card coverage: {len(route_packet_ids & set(packet_by_id))}\n")
        handle.write(f"- Packets with binding-map coverage: {len(binding_packet_ids & set(packet_by_id))}\n\n")
        handle.write("## Production-Ready Candidate\n\n")
        if runtime_ready:
            for packet_id in runtime_ready[:100]:
                handle.write(f"- `{packet_id}`\n")
            if len(runtime_ready) > 100:
                handle.write(f"- TRUNCATED: {len(runtime_ready) - 100} additional candidates.\n")
        else:
            handle.write("None. Native localization and runtime/page proof are not proven by this static audit.\n")
        handle.write("\n## Needs Writer Work\n\n")
        if needs_writer:
            for packet_id in needs_writer[:200]:
                handle.write(f"- `{packet_id}`\n")
        else:
            handle.write("None identified outside blocking conflicts.\n")
        handle.write("\n## Needs Localization / Encoding / Native Review\n\n")
        if needs_loc:
            for packet_id in needs_loc[:200]:
                handle.write(f"- `{packet_id}`\n")
            if len(needs_loc) > 200:
                handle.write(f"- TRUNCATED: {len(needs_loc) - 200} additional packets. See locale_coverage_matrix.csv.\n")
        else:
            handle.write("None by static packet scan.\n")
        handle.write("\n## Needs Runtime Binding / Placement Proof\n\n")
        if needs_binding:
            for packet_id in needs_binding[:200]:
                handle.write(f"- `{packet_id}`\n")
            if len(needs_binding) > 200:
                handle.write(f"- TRUNCATED: {len(needs_binding) - 200} additional packets. See route_binding_crosscheck.md.\n")
        else:
            handle.write("None by static CSV coverage scan. Unity placement remains unproven.\n")
        handle.write("\n## Must Not Ship As-Is\n\n")
        if must_not_ship:
            for packet_id in sorted(set(must_not_ship)):
                reasons = [
                    conflict["topic"]
                    for conflict in conflicts
                    if conflict["packet_id"] == packet_id and conflict["severity"] in {"BLOCK_RUNTIME", "BLOCK_PUBLIC_SITE"}
                ]
                handle.write(f"- `{packet_id}` - {';'.join(sorted(set(reasons)))}\n")
        else:
            handle.write("No BLOCK_RUNTIME/BLOCK_PUBLIC_SITE packet-level rows found.\n")
        handle.write("\n## Low / Middle / High / Ultra Consequences\n\n")
        handle.write("- Low: ship only compact scanner/wiki/public-safe rows after source text and locale status are clean; do not expose field_note authoring instructions.\n")
        handle.write("- Middle: add terminal and route-card-backed codex rows where binding maps exist; keep high-spoiler rows gated.\n")
        handle.write("- High: add richer external-site/wiki article variants and audio transcript surfaces only after native review and page/title cross-checks.\n")
        handle.write("- Ultra: add dense archive/crosslink surfaces and optional dossier material without changing Article IDs, LocIDs, unlock truth, or spoiler gates.\n")

    with (ROOT / "Docs" / "AgentLogs" / "HANDOFF_1770.md").open("w", encoding="utf-8") as handle:
        handle.write("# HANDOFF 1770 - Canon Release-Set Sorting Archivist\n\n")
        handle.write("Evidence class: STATIC_SOURCE / STATIC_DOC. No Unity runtime placement or native localization is proven here.\n\n")
        handle.write("## Site\n\n")
        handle.write("- Use `Docs/Lore/AppliedContent/production_audits/1770/publication_surface_crosscheck.md` before promoting generated pages. Public-safe clusters are listed in `publication_cluster_crosscheck.md`; spoiler-gated clusters must not be exposed as open site pages.\n\n")
        handle.write("## Wiki\n\n")
        handle.write("- Use `surface_ownership_matrix.csv` to keep deep/ending rows out of starting wiki surfaces. Scanner/audio snippets are not substitutes for earned evidence.\n\n")
        handle.write("## Scanner\n\n")
        handle.write("- Packets flagged with `field_note_runtime_until_rewritten` or high-spoiler forbidden surfaces in `surface_ownership_matrix.csv` need source cleanup before scanner/codex runtime binding.\n\n")
        handle.write("## Audio\n\n")
        handle.write("- Audio transcript rows remain source text only. Native/subtitle timing proof is not present; see `locale_coverage_matrix.csv` and `Localization_Status_Index.md`.\n\n")
        handle.write("## Localization\n\n")
        handle.write("- `locale_coverage_matrix.csv` marks draft markers, RTL/CJK flags and suspected mojibake. Do not claim native review from generated packet rows.\n\n")
        handle.write("## Runtime\n\n")
        handle.write("- `route_binding_crosscheck.md` checks CSV coverage only. Unity placement remains PENDING VERIFICATION until editor/tool proof assigns packet hashes to actual objects.\n\n")
        handle.write("## Reader Agents\n\n")
        handle.write("- Read `lore_sorting_decisions.md` first, then use the exact artifact named for the target surface. Do not depend on agents 1771-1779.\n")

    status = TASK_STATUS.read_text(encoding="utf-8")
    replacements = {
        "- [IN_PROGRESS] Task 01 - Status file with 20 tasks and checkpoints.": "- [DONE] Task 01 - Status file with 20 tasks and checkpoints.",
        "- [IN_PROGRESS] Task 02 - Rationale file with concrete decisions only.": "- [DONE] Task 02 - Rationale file with concrete decisions only.",
        "- [PENDING] Task 03 - Packet inventory CSV.": "- [DONE] Task 03 - Packet inventory CSV.",
        "- [PENDING] Task 04 - Release-set inventory.": "- [DONE] Task 04 - Release-set inventory.",
        "- [PENDING] Task 05 - Checkpoint after inventory.": "- [DONE] Task 05 - Checkpoint after inventory.",
        "- [PENDING] Task 06 - Canon conflict audit.": "- [DONE] Task 06 - Canon conflict audit.",
        "- [PENDING] Task 07 - Conflict severity classification.": "- [DONE] Task 07 - Conflict severity classification.",
        "- [PENDING] Task 08 - Patch only small proven contradictions or handoff.": "- [DONE] Task 08 - Patch only small proven contradictions or handoff.",
        "- [PENDING] Task 09 - Surface ownership matrix.": "- [DONE] Task 09 - Surface ownership matrix.",
        "- [PENDING] Task 10 - Checkpoint after matrix validation.": "- [DONE] Task 10 - Checkpoint after matrix validation.",
        "- [PENDING] Task 11 - 15-locale coverage matrix.": "- [DONE] Task 11 - 15-locale coverage matrix.",
        "- [PENDING] Task 12 - Publication surface index cross-check.": "- [DONE] Task 12 - Publication surface index cross-check.",
        "- [PENDING] Task 13 - Publication cluster index cross-check.": "- [DONE] Task 13 - Publication cluster index cross-check.",
        "- [PENDING] Task 14 - Route-card and binding-map cross-check.": "- [DONE] Task 14 - Route-card and binding-map cross-check.",
        "- [PENDING] Task 16 - Lore sorting decisions map.": "- [DONE] Task 16 - Lore sorting decisions map.",
        "- [PENDING] Task 18 - Handoff notes.": "- [DONE] Task 18 - Handoff notes.",
        "### Task 05\n\nPENDING.": f"### Task 05\n\nDONE. {checkpoint05}",
        "### Task 10\n\nPENDING.": f"### Task 10\n\nDONE. {checkpoint10}",
    }
    for old, new in replacements.items():
        status = status.replace(old, new)
    TASK_STATUS.write_text(status, encoding="utf-8", newline="\n")

    with RATIONALE.open("a", encoding="utf-8", newline="\n") as handle:
        handle.write("\n- Decision: no source prose packet was rewritten during Tasks 06-08.\n")
        handle.write("  Rule used: supplied task forbids wholesale prose rewrite; detected `field_note` authoring instructions are widespread enough to require writer cleanup, not silent mass rewrite by sorting agent.\n")
        handle.write("  Edit made: classified blockers in `canon_conflict_audit.md` and handoff notes instead of mutating player-facing packet bodies.\n")
        handle.write("  Rejected alternative: automatic replacement of every `Use for...` field note.\n")
        handle.write("  Proof path: `Docs/Lore/AppliedContent/production_audits/1770/canon_conflict_audit.md`.\n")

    print(f"Generated 1770 reports for {len(packets)} packets, {len(release_sets)} release sets, {len(conflicts)} conflict rows.")
    print(checkpoint05)
    print(checkpoint10)


if __name__ == "__main__":
    main()

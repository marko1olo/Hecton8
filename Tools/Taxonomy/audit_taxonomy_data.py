#!/usr/bin/env python3
"""Hard-data audit for the offline HECTON-8 taxonomy payload."""

from __future__ import annotations

import json
import re
import struct
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
TAXONOMY_PATH = ROOT / "Data" / "Localization" / "en_US_Taxonomy.json"
BINARY_PATH = ROOT / "Data" / "Localization" / "en_US_Taxonomy.h8bin"
BINARY_AUDIT_PATH = ROOT / "Docs" / "AgentLogs" / "TaxonomyBinaryAudit_XENO_TAXONOMY_WRITER.json"
PROJECT_ATLAS_PATH = ROOT / "Docs" / "PROJECT_ATLAS.md"
ECONOMY_MONTE_CARLO_PATH = ROOT / "Docs" / "Reports" / "Economy_MonteCarlo_Audit.json"
TAXONOMY_ECONOMY_PATH = ROOT / "Docs" / "AgentLogs" / "TaxonomyEconomyMillionStep_XENO_TAXONOMY_WRITER.json"
ECONOMY_GRAPH_JSON_PATH = ROOT / "Docs" / "Reports" / "Economy_Integrity_Audit_XENO_TAXONOMY_WRITER.json"
REPORT_JSON_PATH = ROOT / "Docs" / "AgentLogs" / "TaxonomyDataAudit_XENO_TAXONOMY_WRITER.json"
REPORT_MD_PATH = ROOT / "Docs" / "AgentLogs" / "TaxonomyDataAudit_XENO_TAXONOMY_WRITER.md"

FNV_OFFSET = 0x811C9DC5
FNV_PRIME = 0x01000193
ALIGNMENT_BYTES = 16
HEADER_FORMAT = "<4sHHIIIIIIII24s"
RECORD_FORMAT = "<IIIIIIIIIIII"

STERILE_TERMS = (
    "pristine",
    "sleek",
    "utopian",
    "clean sci-fi",
    "nanotech",
    "quantum",
)

INDUSTRIAL_TERMS = (
    "pressure",
    "silt",
    "brine",
    "rust",
    "hull",
    "scar",
    "tendon",
    "vent",
    "jaw",
    "bladder",
    "bone",
    "ash",
    "corridor",
    "hinge",
    "plate",
    "gill",
    "shell",
    "root",
    "tube",
    "blade",
    "sampler",
    "drill",
    "laser",
    "node",
    "heat",
    "current",
    "route",
    "lab",
    "wound",
    "mouth",
    "autopsy",
    "biopsy",
    "necropsy",
)


def fnv1a_utf16le(value: str) -> int:
    h = FNV_OFFSET
    for b in value.encode("utf-16le"):
        h ^= b
        h = (h * FNV_PRIME) & 0xFFFFFFFF
    return h


def hx(value: str) -> str:
    return f"0x{fnv1a_utf16le(value):08X}"


def load_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8-sig") as handle:
        return json.load(handle)


def collect_id_hashes(entry: dict[str, Any]) -> list[tuple[str, str]]:
    pairs = [(entry["LocID"], entry["Hash"])]
    if "EntityID" in entry:
        pairs.append((entry["EntityID"], entry["EntityHash"]))
    if "BaseEntityID" in entry:
        pairs.append((entry["BaseEntityID"], entry["BaseEntityHash"]))
    pairs.extend(entry.get("BiomeHashes", {}).items())
    pairs.extend(entry.get("FamilyHashes", {}).items())
    return pairs


def audit_hashes(entries: list[dict[str, Any]]) -> dict[str, Any]:
    owners: dict[str, set[str]] = {}
    mismatches: list[str] = []
    for entry in entries:
        for id_value, hash_value in collect_id_hashes(entry):
            owners.setdefault(hash_value, set()).add(id_value)
            if hash_value != hx(id_value):
                mismatches.append(f"{id_value}:{hash_value}!={hx(id_value)}")
    collisions = {
        hash_value: sorted(values)
        for hash_value, values in owners.items()
        if len(values) > 1
    }
    return {
        "distinctHashCount": len(owners),
        "mismatches": mismatches,
        "collisions": collisions,
        "collisionCount": len(collisions),
        "passed": not mismatches and not collisions,
    }


def audit_lore(entries: list[dict[str, Any]]) -> dict[str, Any]:
    sterile_hits: list[str] = []
    industrial_misses: list[str] = []
    clinical_misses: list[str] = []
    for entry in entries:
        text = entry["Text"].lower()
        for term in STERILE_TERMS:
            if term in text:
                sterile_hits.append(f"{entry['LocID']}:{term}")
        if not any(term in text for term in INDUSTRIAL_TERMS):
            industrial_misses.append(entry["LocID"])
        if not any(marker in text for marker in ("autopsy", "biopsy", "necropsy")):
            clinical_misses.append(entry["LocID"])
    return {
        "sterileHits": sterile_hits,
        "industrialMisses": industrial_misses,
        "clinicalMisses": clinical_misses,
        "passed": not sterile_hits and not industrial_misses and not clinical_misses,
    }


def audit_binary() -> dict[str, Any]:
    result: dict[str, Any] = {
        "exists": BINARY_PATH.exists(),
        "path": str(BINARY_PATH.relative_to(ROOT)).replace("\\", "/"),
    }
    if not BINARY_PATH.exists():
        result["passed"] = False
        return result

    blob = BINARY_PATH.read_bytes()
    result["fileSizeBytes"] = len(blob)
    result["fileAligned16"] = len(blob) % ALIGNMENT_BYTES == 0
    result["headerStruct"] = HEADER_FORMAT
    result["recordStruct"] = RECORD_FORMAT
    result["structFormatsExplicitLittleEndian"] = HEADER_FORMAT.startswith("<") and RECORD_FORMAT.startswith("<")
    result["headerSizeBytes"] = struct.calcsize(HEADER_FORMAT)
    result["recordSizeBytes"] = struct.calcsize(RECORD_FORMAT)
    result["recordAligned16"] = result["recordSizeBytes"] % ALIGNMENT_BYTES == 0
    if BINARY_AUDIT_PATH.exists():
        binary_audit = load_json(BINARY_AUDIT_PATH)
        result["textOffsetMisalignments"] = [
            item["LocID"]
            for item in binary_audit.get("textOffsets", [])
            if item.get("aligned16") is not True
            or item.get("toasterAligned16") is not True
            or item.get("rtxAligned16") is not True
        ]
        result["tierPayloadsPresent"] = all(
            item.get("toasterLength", 0) > 0 and item.get("rtxLength", 0) > 0
            for item in binary_audit.get("textOffsets", [])
        )
    else:
        result["textOffsetMisalignments"] = ["missing binary audit"]
        result["tierPayloadsPresent"] = False
    result["passed"] = (
        result["fileAligned16"]
        and result["structFormatsExplicitLittleEndian"]
        and result["recordAligned16"]
        and not result["textOffsetMisalignments"]
        and result["tierPayloadsPresent"]
    )
    return result


def audit_atlas() -> dict[str, Any]:
    text = PROJECT_ATLAS_PATH.read_text(encoding="utf-8")
    assembly_match = re.search(r"Static scan found `(\d+)` first-party", text)
    domain_rows = re.findall(r"^\|\s*(\d+)\s*\|", text, flags=re.MULTILINE)
    unique_domains = {int(value) for value in domain_rows if value.isdigit()}
    has_85_heading = "### 85 Identified Domains" in text
    return {
        "assemblyCount": int(assembly_match.group(1)) if assembly_match else None,
        "domainIndexCount": len(unique_domains),
        "has85DomainHeading": has_85_heading,
        "fitsAtlas": int(assembly_match.group(1)) == 83 if assembly_match else False,
        "fits85DomainMap": has_85_heading and len(unique_domains) >= 85,
    }


def audit_economy() -> dict[str, Any]:
    result: dict[str, Any] = {
        "taxonomyMillionStepExists": TAXONOMY_ECONOMY_PATH.exists(),
        "monteCarloExists": ECONOMY_MONTE_CARLO_PATH.exists(),
        "graphAuditExists": ECONOMY_GRAPH_JSON_PATH.exists(),
    }
    if TAXONOMY_ECONOMY_PATH.exists():
        monte = load_json(TAXONOMY_ECONOMY_PATH)
        final_summary = monte.get("summary", {})
        result["economyEvidenceSource"] = str(TAXONOMY_ECONOMY_PATH.relative_to(ROOT)).replace("\\", "/")
        result["players"] = monte.get("players")
        result["maxNodesPerPlayer"] = monte.get("maxNodesPerPlayer")
    elif ECONOMY_MONTE_CARLO_PATH.exists():
        monte = load_json(ECONOMY_MONTE_CARLO_PATH)
        final_summary = monte.get("final_summary", {})
        params = monte.get("params", {})
        result["economyEvidenceSource"] = str(ECONOMY_MONTE_CARLO_PATH.relative_to(ROOT)).replace("\\", "/")
        result["players"] = params.get("players")
        result["maxNodesPerPlayer"] = params.get("max_nodes")
        result["monteCarloSteps"] = final_summary.get("monte_carlo_steps")
        result["millionStepAuditPassed"] = final_summary.get("million_step_audit_passed")
        result["failures"] = final_summary.get("failures")
        result["p99Minutes"] = final_summary.get("p99_minutes")
        result["thresholdMinutes"] = params.get("threshold_minutes")
    else:
        final_summary = {}
    if final_summary:
        result["monteCarloSteps"] = final_summary.get("monte_carlo_steps")
        result["millionStepAuditPassed"] = final_summary.get("million_step_audit_passed")
        result["failures"] = final_summary.get("failures")
        result["p99Minutes"] = final_summary.get("p99_minutes")
    if ECONOMY_GRAPH_JSON_PATH.exists():
        graph = load_json(ECONOMY_GRAPH_JSON_PATH)
        result["cycleCount"] = graph.get("graph", {}).get("cycle_count")
        result["positiveProfitCycleCount"] = graph.get("value_integrity", {}).get("positive_profit_cycle_count", 0)
        result["auditStatus"] = graph.get("status")
    result["passed"] = (
        result.get("millionStepAuditPassed") is True
        and result.get("failures") == 0
        and result.get("cycleCount") == 0
        and result.get("positiveProfitCycleCount", 0) == 0
    )
    return result


def build_markdown(report: dict[str, Any]) -> str:
    status = "PASS" if report["passed"] else "FAIL"
    lines = [
        "# Taxonomy Data Audit - XENO_TAXONOMY_WRITER",
        "",
        f"Status: {status}",
        f"Hash collisions: {report['hashAudit']['collisionCount']}",
        f"Binary aligned 16: {report['binaryAudit']['fileAligned16']}",
        f"Endian structs: {report['binaryAudit']['structFormatsExplicitLittleEndian']}",
        f"Atlas asmdefs/domains: {report['atlasAudit']['assemblyCount']} / {report['atlasAudit']['domainIndexCount']}",
        f"Monte Carlo steps: {report['economyAudit'].get('monteCarloSteps')}",
        f"Economy cycle count: {report['economyAudit'].get('cycleCount')}",
        "",
        "Math audit: taxonomy emits no physics LUTs/matrices; LV coefficients are source-copied, visual overkill fields are deterministic presentation metadata.",
        "H-Phi audit: stateless JSON/binary lookup data, no runtime private state added; runtime score remains PENDING VERIFICATION.",
    ]
    return "\n".join(lines) + "\n"


def main() -> int:
    taxonomy = load_json(TAXONOMY_PATH)
    entries = taxonomy["entries"]
    report = {
        "agent": "XENO_TAXONOMY_WRITER",
        "taxonomyPath": str(TAXONOMY_PATH.relative_to(ROOT)).replace("\\", "/"),
        "counts": taxonomy.get("counts", {}),
        "mathAudit": taxonomy.get("mathAudit", {}),
        "hPhiAudit": taxonomy.get("hPhiAudit", {}),
        "hashAudit": audit_hashes(entries),
        "loreAudit": audit_lore(entries),
        "binaryAudit": audit_binary(),
        "atlasAudit": audit_atlas(),
        "economyAudit": audit_economy(),
    }
    report["passed"] = all(
        (
            report["hashAudit"]["passed"],
            report["loreAudit"]["passed"],
            report["binaryAudit"]["passed"],
            report["atlasAudit"]["fits85DomainMap"],
            report["economyAudit"]["passed"],
        )
    )
    REPORT_JSON_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_JSON_PATH.write_text(json.dumps(report, indent=2, sort_keys=True), encoding="utf-8")
    REPORT_MD_PATH.write_text(build_markdown(report), encoding="utf-8")
    if not report["passed"]:
        print(f"TAXONOMY DATA AUDIT FAIL report={REPORT_JSON_PATH}")
        return 1
    print(
        "TAXONOMY DATA AUDIT PASS "
        f"hashCollisions={report['hashAudit']['collisionCount']} "
        f"binaryAligned16={report['binaryAudit']['fileAligned16']} "
        f"monteCarloSteps={report['economyAudit'].get('monteCarloSteps')} "
        f"atlasDomains={report['atlasAudit']['domainIndexCount']}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

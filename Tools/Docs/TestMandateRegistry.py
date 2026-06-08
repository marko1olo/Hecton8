#!/usr/bin/env python3
"""Static gate for the HECTON-8 mandate registry.

This checks the mandate files as a registry, not as Unity runtime proof.
It intentionally avoids launching Unity, import, builds, or profilers.
"""

from __future__ import annotations

import argparse
import os
import re
import shutil
import sys
import uuid
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SKILLS_DIR = ROOT / ".agents-skills"
README = SKILLS_DIR / "README.md"
MIN_MANDATE_BYTES = 1000

ALLOWED_PREFIXES = {
    "AI",
    "ANIM",
    "ARCH",
    "AUD",
    "AUDIO",
    "CI",
    "CORE",
    "CTRL",
    "DATA",
    "DBG",
    "GPU",
    "LOGI",
    "MANDATE",
    "MATH",
    "NET",
    "OPT",
    "PHYS",
    "PROG",
    "PROJECT",
    "QA",
    "REND",
    "STRM",
    "TOOL",
    "UI",
    "VOX",
}

BANNED_WEAK_LANGUAGE = re.compile(r"\b(should|recommended|recommend|maybe|consider|good enough)\b", re.IGNORECASE)
AMBIGUOUS_PRODUCTION_LANGUAGE = re.compile(
    r"\b(best effort|best-effort|when possible|if possible|nice to have|probably|hope|assume|stub|placeholder)\b",
    re.IGNORECASE,
)
AMBIGUOUS_ALLOWED_CONTEXT = re.compile(
    r"\b(forbid|forbidden|banned|no|not|never|avoid|reject|rejected|legacy|historical|diagnostic|editor-only|template|applies even|explicitly)\b",
    re.IGNORECASE,
)
STALE_INVENTORY = re.compile(r"\b(35 distilled|73 mandates|79 mandates|old mandate count)\b", re.IGNORECASE)
TODO_TERMS = re.compile(r"\b(TODO|TBD|FIXME)\b", re.IGNORECASE)
REPORT_LOOP_TERMS = re.compile(r"\b(report-only|report only|summary only|status file only|rationale file only)\b", re.IGNORECASE)
FALSE_READY_LABELS = re.compile(
    r"\b(PRODUCTION READY|RUNTIME READY|SHIP READY|FINAL VERIFIED|STATUS:\s*VERIFIED|STATUS:\s*COMPLETE)\b",
    re.IGNORECASE,
)
OLD_UNITY_VERSION = re.compile(r"\b(Unity\s+20(?:18|19|2[0-3])|20(?:18|19|2[0-3])\s+LTS|Unity\s+2023\.1\+)\b", re.IGNORECASE)
LEGACY_HECTON_ASSEMBLY = re.compile(r"\bHecton\.(?:Core|Simulation|Voxel|EngineAbstraction|UnityBackend|ModdingFacade)\b")
RAW_DMI_TOKEN = "DrawMeshInstancedIndirect"
RAW_DMI_ALLOWED_CONTEXT = re.compile(r"\b(forbid|forbidden|no|not|remove|reject|rejected|unless|legacy|raw)\b", re.IGNORECASE)
MPB_TOKEN = "MaterialPropertyBlock"
MPB_ALLOWED_CONTEXT = re.compile(r"\b(forbid|forbidden|no|not|zero|only|ui|debug|gizmo|particle|legacy|exception|rejected|must not)\b", re.IGNORECASE)
DANGEROUS_RUNTIME_TOKENS = (
    ("Camera.main", re.compile(r"\b(forbid|forbidden|banned|no|not|avoid|reject|rejected|legacy|cached|injected)\b", re.IGNORECASE)),
    ("FindObjectOfType", re.compile(r"\b(forbid|forbidden|banned|no|not|avoid|reject|rejected|legacy|historical)\b", re.IGNORECASE)),
    ("GameObject.Find", re.compile(r"\b(forbid|forbidden|banned|no|not|avoid|reject|rejected|legacy|historical)\b", re.IGNORECASE)),
    ("DontDestroyOnLoad", re.compile(r"\b(forbid|forbidden|banned|no|not|avoid|reject|rejected|legacy|historical)\b", re.IGNORECASE)),
    ("Resources.Load", re.compile(r"\b(forbid|forbidden|banned|no|not|avoid|reject|rejected|legacy|historical)\b", re.IGNORECASE)),
    ("StartCoroutine", re.compile(r"\b(forbid|forbidden|banned|no|not|avoid|reject|rejected|legacy|historical)\b", re.IGNORECASE)),
    ("BinaryFormatter", re.compile(r"\b(forbid|forbidden|banned|no|not|avoid|reject|rejected|legacy|historical)\b", re.IGNORECASE)),
    ("JsonUtility.FromJson", re.compile(r"\b(forbid|forbidden|banned|no|not|avoid|reject|rejected|legacy|historical)\b", re.IGNORECASE)),
    ("File.ReadAllText", re.compile(r"\b(forbid|forbidden|banned|no|not|avoid|reject|rejected|legacy|historical)\b", re.IGNORECASE)),
    ("File.ReadAllBytes", re.compile(r"\b(forbid|forbidden|banned|no|not|avoid|reject|rejected|legacy|historical)\b", re.IGNORECASE)),
)
COMMAND_LANGUAGE = re.compile(
    r"\[(RULE|FORBID|REQUIRE|REQ|STATUS|BROKEN|UPDATED|PENDING|SOURCE)\]|\b(MUST|NEVER|FORBIDDEN|REJECTED?)\b",
    re.IGNORECASE,
)
PROOF_LANGUAGE = re.compile(
    r"\b(Evidence|Proof|Gate|Profiler|Artifact|PENDING VERIFICATION|PENDING RUNTIME VERIFICATION|Engineering Data|GCMonitor|Player capture|Test log|Memory Profiler)\b",
    re.IGNORECASE,
)
LOCAL_PATH_REFERENCE = re.compile(
    r"(?P<path>(?:/?(?:Assets|Docs|Tools|Packages|ProjectSettings|UserSettings|\.agents-skills|\.agent|\.cursor|\.github|\.codexrules)[\\/][^`\s\)\],;:\"<>]+))"
)
STALE_SOURCE_CLAIM = re.compile(r"\b(Static scan still finds|still contains)\b", re.IGNORECASE)
STALE_SOURCE_ALLOWED_CONTEXT = re.compile(
    r"\b(PENDING SOURCE CHECK|fresh scoped scan|historical|historically|owner status must be proven)\b",
    re.IGNORECASE,
)
VISUAL_PARITY_MANDATES = {
    "CORE_Damage_System_Hull_Integrity_VFX_Feedback.txt",
    "CORE_Weather_Abyssal_FlowField_Currents.txt",
    "OPT_Premium_Approximation_Protocol.txt",
    "PHYS_Fluid_Incursion_Interior.txt",
    "REND_Abyssal_Lighting_Voxel_Occlusion_Shadows.txt",
    "REND_Foveated_Simulation_LOD.txt",
    "REND_GPU_Driven_Animation_VAT.txt",
    "REND_Instanced_Flora_Physics.txt",
    "REND_Shader_Noir_Aesthetics_Dithering_Fog.txt",
    "REND_Terrain_VirtualTexturing.txt",
    "REND_URP_Graphics_HotPath_Optimization_HLOD.txt",
    "REND_VFX_Fluid_Aesthetics_Compute_Particles.txt",
    "STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt",
    "STRM_Async_Asset_Upload_Texture_Settings.txt",
    "TOOL_Procedural_Wreckage_Generator.txt",
    "UI_Data_Streaming_ZeroGC_Optimization.txt",
    "UI_Diegetic_Physical_Interfaces.txt",
    "VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt",
    "VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt",
    "VOX_Voxel_World_Logic_Carving_Persistence.txt",
}
VISUAL_PARITY_TERMS = (
    "Visual Reference Parity Gate",
    "best-known internal baseline",
    "April/previously-in-development",
    "VISUAL_ROUTE_INVALID",
)


@dataclass
class Finding:
    severity: str
    path: Path
    message: str
    line: int | None = None

    def format(self) -> str:
        location = rel(self.path)
        if self.line is not None:
            location = f"{location}:{self.line}"
        return f"{self.severity}: {location}: {self.message}"


def rel(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def writable_temp_root() -> Path:
    candidates = [
        os.environ.get("H8_TOOL_TMP"),
        str(ROOT / "Temp" / "ToolSelfTests"),
        "C:/tmp",
        os.environ.get("TMP"),
        os.environ.get("TEMP"),
    ]
    for raw_candidate in candidates:
        if not raw_candidate:
            continue
        candidate = Path(raw_candidate)
        try:
            candidate.mkdir(parents=True, exist_ok=True)
            probe_dir = candidate / ".h8_write_probe_dir"
            probe_dir.mkdir(exist_ok=True)
            probe = probe_dir / "probe.txt"
            probe.write_text("", encoding="utf-8")
            try:
                probe.unlink()
                probe_dir.rmdir()
            except OSError:
                pass
            return candidate
        except OSError:
            continue
    raise RuntimeError("No writable temp directory for mandate-registry self-test")


class SelfTestDirectory:
    def __init__(self, prefix: str) -> None:
        self.path = writable_temp_root() / f"{prefix}{uuid.uuid4().hex}"

    def __enter__(self) -> str:
        self.path.mkdir(parents=True, exist_ok=True)
        return str(self.path)

    def __exit__(self, exc_type, exc, tb) -> bool:
        shutil.rmtree(self.path, ignore_errors=True)
        return False


def add(findings: list[Finding], severity: str, path: Path, message: str, line: int | None = None) -> None:
    findings.append(Finding(severity=severity, path=path, message=message, line=line))


def mandate_files(skills_dir: Path) -> list[Path]:
    return sorted(skills_dir.glob("*.txt"))


def inventory_count(readme_text: str) -> int | None:
    match = re.search(r"Current inventory:\s*`?(\d+)`?\s+`?\.txt`?\s+mandates", readme_text)
    if not match:
        return None
    return int(match.group(1))


def meaningful_first_line(text: str) -> str:
    for line in text.splitlines():
        stripped = line.strip()
        if stripped:
            return stripped
    return ""


def line_hits(text: str, pattern: re.Pattern[str]) -> list[tuple[int, str]]:
    hits: list[tuple[int, str]] = []
    for index, line in enumerate(text.splitlines(), 1):
        if pattern.search(line):
            hits.append((index, line.strip()))
    return hits


def normalized_local_path(raw_path: str) -> str:
    return raw_path.strip().strip("`'\"").rstrip(".,;:").lstrip("/\\").replace("\\", "/")


def should_skip_local_path_reference(line: str, path_text: str) -> bool:
    if "://" in line:
        return True
    if any(marker in path_text for marker in ("[", "]", "{", "}", "<", ">", "*")):
        return True
    lowered = line.lower()
    if "example" in lowered or "placeholder" in lowered or "template" in lowered:
        return True
    return False


def check_readme(readme: Path, skills_dir: Path, findings: list[Finding]) -> None:
    if not readme.exists():
        add(findings, "ERROR", readme, "missing mandate registry README")
        return
    text = read_text(readme)
    expected = inventory_count(text)
    actual = len(mandate_files(skills_dir))
    if expected is None:
        add(findings, "ERROR", readme, "missing parseable Current inventory line")
    elif expected != actual:
        add(findings, "ERROR", readme, f"inventory {expected} != actual {actual}")
    for needle in (
        "Mandate language is command language",
        "PENDING VERIFICATION",
        "False verification language",
        "Treating report volume",
        "Visual Reference Parity Gate",
    ):
        if needle not in text:
            add(findings, "ERROR", readme, f"missing registry doctrine: {needle}")


def check_mandate(path: Path, strict_format: bool, findings: list[Finding]) -> None:
    try:
        text = read_text(path)
    except UnicodeDecodeError as exc:
        add(findings, "ERROR", path, f"not UTF-8 readable: {exc}")
        return

    if len(text.strip()) == 0:
        add(findings, "ERROR", path, "empty mandate")
        return
    if len(text.encode("utf-8")) < MIN_MANDATE_BYTES:
        add(findings, "ERROR", path, f"mandate is suspiciously short (<{MIN_MANDATE_BYTES} bytes)")

    prefix = path.name.split("_", 1)[0].split(".", 1)[0]
    if prefix not in ALLOWED_PREFIXES:
        add(findings, "ERROR", path, f"unexpected mandate filename prefix {prefix}")

    first = meaningful_first_line(text)
    if first.startswith("```"):
        severity = "ERROR" if strict_format else "WARN"
        add(findings, severity, path, "starts with a top-level markdown fence; remove exported wrapper fence")
    elif not (first.startswith("#") or re.match(r"^[A-Z0-9_ ./-]+:", first)):
        add(findings, "WARN", path, f"first line is not a clear title or command label: {first[:80]}")

    if not COMMAND_LANGUAGE.search(text):
        add(findings, "ERROR", path, "missing command-language marker such as [RULE], [FORBID], MUST, or REJECT")
    if not PROOF_LANGUAGE.search(text):
        add(findings, "ERROR", path, "missing proof/evidence/gate language")

    for label, pattern in (
        ("weak mandate language", BANNED_WEAK_LANGUAGE),
        ("stale mandate inventory", STALE_INVENTORY),
        ("unfinished placeholder", TODO_TERMS),
        ("report-loop completion wording", REPORT_LOOP_TERMS),
        ("false readiness label", FALSE_READY_LABELS),
    ):
        for line_number, line in line_hits(text, pattern):
            add(findings, "ERROR", path, f"{label}: {line[:140]}", line_number)

    for line_number, line in line_hits(text, AMBIGUOUS_PRODUCTION_LANGUAGE):
        if not AMBIGUOUS_ALLOWED_CONTEXT.search(line):
            add(findings, "ERROR", path, f"ambiguous production escape clause: {line[:140]}", line_number)

    for line_number, line in line_hits(text, OLD_UNITY_VERSION):
        is_lts_legacy_note = path.name == "PROJECT_LTS_Compatibility_Layer.txt" and (
            "not current project authority" in line or "Do not create" in line or "older illustrative" in line
        )
        if not is_lts_legacy_note:
            add(findings, "ERROR", path, f"old Unity version reference in active mandate text: {line[:140]}", line_number)

    for line_number, line in line_hits(text, LEGACY_HECTON_ASSEMBLY):
        if path.name != "PROJECT_LTS_Compatibility_Layer.txt":
            add(findings, "ERROR", path, f"legacy Hecton.* assembly name outside LTS compatibility example: {line[:140]}", line_number)
    if path.name == "PROJECT_LTS_Compatibility_Layer.txt":
        for needle in (
            "Copying any legacy `Hecton.*` assembly name",
            "The current project route is live `Hecton8.*` asmdefs plus Unity 6000.4 proof",
        ):
            if needle not in text:
                add(findings, "ERROR", path, f"LTS compatibility mandate missing legacy-example guard: {needle}")

    if path.name in VISUAL_PARITY_MANDATES:
        for needle in VISUAL_PARITY_TERMS:
            if needle not in text:
                add(findings, "ERROR", path, f"visual mandate missing parity term: {needle}")

    for line_number, line in enumerate(text.splitlines(), 1):
        if STALE_SOURCE_CLAIM.search(line) and not STALE_SOURCE_ALLOWED_CONTEXT.search(line):
            add(findings, "ERROR", path, f"stale hard source claim lacks fresh-scan guard: {line.strip()[:140]}", line_number)
        if RAW_DMI_TOKEN in line and not RAW_DMI_ALLOWED_CONTEXT.search(line):
            add(findings, "ERROR", path, f"raw DrawMeshInstancedIndirect appears as active route instead of rejected/exception text: {line.strip()[:140]}", line_number)
        if MPB_TOKEN in line and not MPB_ALLOWED_CONTEXT.search(line):
            add(findings, "ERROR", path, f"MaterialPropertyBlock appears as active standard-geometry route instead of rejected/UI/debug exception text: {line.strip()[:140]}", line_number)
        for token, allowed_context in DANGEROUS_RUNTIME_TOKENS:
            if token in line and not allowed_context.search(line):
                add(findings, "ERROR", path, f"dangerous runtime token appears as active mandate route: {line.strip()[:140]}", line_number)
        for match in LOCAL_PATH_REFERENCE.finditer(line):
            local_path = normalized_local_path(match.group("path"))
            if should_skip_local_path_reference(line, local_path):
                continue
            if not (ROOT / local_path).exists():
                add(findings, "ERROR", path, f"dead local path reference: {local_path}", line_number)


def check_registry(skills_dir: Path, readme: Path, strict_format: bool) -> list[Finding]:
    findings: list[Finding] = []
    check_readme(readme, skills_dir, findings)

    files = mandate_files(skills_dir)
    names = [path.name for path in files]
    if len(names) != len(set(names)):
        add(findings, "ERROR", skills_dir, "duplicate mandate filenames")

    for path in files:
        check_mandate(path, strict_format, findings)

    return findings


def run_self_test() -> int:
    with SelfTestDirectory(prefix="h8_mandates_") as raw:
        root = Path(raw)
        skills = root / ".agents-skills"
        skills.mkdir()
        readme = skills / "README.md"
        readme.write_text(
            "\n".join(
                [
                    "# Registry",
                    "Current inventory: `1` `.txt` mandates plus this `README.md` registry index.",
                    "Mandate language is command language.",
                    "PENDING VERIFICATION",
                    "False verification language",
                    "Treating report volume",
                    "Visual Reference Parity Gate",
                ]
            ),
            encoding="utf-8",
        )
        (skills / "OPT_Test_Mandate.txt").write_text(
            "\n".join(
                [
                    "# Test mandate",
                    "[RULE] Production work changes source or proof.",
                    "[FORBID] False verification language.",
                    "[REQUIRE] Evidence artifact path and profiler proof before readiness claims.",
                    "Status remains PENDING VERIFICATION until runtime proof exists.",
                ]
            )
            + "\n"
            + ("x" * 1100),
            encoding="utf-8",
        )
        if any(f.severity == "ERROR" for f in check_registry(skills, readme, strict_format=False)):
            print("MANDATE_REGISTRY_SELFTEST=FAIL")
            print("- positive fixture failed")
            return 1

        (skills / "DATA_Bad_Save.txt").write_text("maybe good enough\n", encoding="utf-8")
        bad = check_registry(skills, readme, strict_format=False)
        required = (
            "inventory 1 != actual 2",
            "suspiciously short",
            "weak mandate language",
            "missing command-language marker",
        )
        for needle in required:
            if not any(needle in finding.message for finding in bad):
                print("MANDATE_REGISTRY_SELFTEST=FAIL")
                print(f"- missing negative fixture finding: {needle}")
                return 1

        (skills / "REND_Bad_Indirect.txt").write_text(
            "# Bad render mandate\n[RULE] Swarms MUST use DrawMeshInstancedIndirect.\nEvidence: PENDING VERIFICATION.\n"
            + ("x" * 1100),
            encoding="utf-8",
        )
        render_bad = check_registry(skills, readme, strict_format=False)
        if not any("raw DrawMeshInstancedIndirect appears as active route" in finding.message for finding in render_bad):
            print("MANDATE_REGISTRY_SELFTEST=FAIL")
            print("- raw DrawMeshInstancedIndirect active-route fixture passed")
            return 1

        (skills / "PROJECT_Bad_Version.txt").write_text(
            "# Bad project mandate\n[RULE] Use Unity 2023.1+ snapshots.\nEvidence: PENDING VERIFICATION.\n"
            + ("x" * 1100),
            encoding="utf-8",
        )
        version_bad = check_registry(skills, readme, strict_format=False)
        if not any("old Unity version reference" in finding.message for finding in version_bad):
            print("MANDATE_REGISTRY_SELFTEST=FAIL")
            print("- old Unity version fixture passed")
            return 1

        (skills / "REND_Bad_Mpb.txt").write_text(
            "# Bad MPB mandate\n[RULE] Use MaterialPropertyBlock for per-instance world geometry color.\nEvidence: PENDING VERIFICATION.\n"
            + ("x" * 1100),
            encoding="utf-8",
        )
        mpb_bad = check_registry(skills, readme, strict_format=False)
        if not any("MaterialPropertyBlock appears as active standard-geometry route" in finding.message for finding in mpb_bad):
            print("MANDATE_REGISTRY_SELFTEST=FAIL")
            print("- active MaterialPropertyBlock world-geometry fixture passed")
            return 1

        (skills / "QA_Bad_Path.txt").write_text(
            "# Bad path mandate\n[RULE] Read `Docs/ThisFileDoesNotExist.md` before work.\nEvidence: PENDING VERIFICATION.\n"
            + ("x" * 1100),
            encoding="utf-8",
        )
        path_bad = check_registry(skills, readme, strict_format=False)
        if not any("dead local path reference" in finding.message for finding in path_bad):
            print("MANDATE_REGISTRY_SELFTEST=FAIL")
            print("- dead local path fixture passed")
            return 1

        (skills / "QA_Bad_Stale_Source.txt").write_text(
            "# Bad stale source mandate\n[BROKEN] Assets/_Project/Scripts/Foo.cs still contains Task.Run.\nEvidence: PENDING VERIFICATION.\n"
            + ("x" * 1100),
            encoding="utf-8",
        )
        stale_bad = check_registry(skills, readme, strict_format=False)
        if not any("stale hard source claim" in finding.message for finding in stale_bad):
            print("MANDATE_REGISTRY_SELFTEST=FAIL")
            print("- stale hard source claim fixture passed")
            return 1

        (skills / "ARCH_Bad_Runtime_Token.txt").write_text(
            "# Bad runtime token mandate\n[RULE] Resolve the owner with FindObjectOfType<T>() during boot.\nEvidence: PENDING VERIFICATION.\n"
            + ("x" * 1100),
            encoding="utf-8",
        )
        runtime_token_bad = check_registry(skills, readme, strict_format=False)
        if not any("dangerous runtime token appears as active mandate route" in finding.message for finding in runtime_token_bad):
            print("MANDATE_REGISTRY_SELFTEST=FAIL")
            print("- active dangerous runtime token fixture passed")
            return 1

        (skills / "QA_Bad_Ambiguous.txt").write_text(
            "# Bad ambiguous mandate\n[RULE] Use a placeholder path if possible for production recovery.\nEvidence: PENDING VERIFICATION.\n"
            + ("x" * 1100),
            encoding="utf-8",
        )
        ambiguous_bad = check_registry(skills, readme, strict_format=False)
        if not any("ambiguous production escape clause" in finding.message for finding in ambiguous_bad):
            print("MANDATE_REGISTRY_SELFTEST=FAIL")
            print("- ambiguous production language fixture passed")
            return 1

        (skills / "REND_Shader_Noir_Aesthetics_Dithering_Fog.txt").write_text(
            "# Bad visual mandate\n[RULE] Fog MUST be cheap.\nEvidence: PENDING VERIFICATION.\n"
            + ("x" * 1100),
            encoding="utf-8",
        )
        visual_bad = check_registry(skills, readme, strict_format=False)
        if not any("visual mandate missing parity term" in finding.message for finding in visual_bad):
            print("MANDATE_REGISTRY_SELFTEST=FAIL")
            print("- visual parity mandate fixture passed")
            return 1

    print("MANDATE_REGISTRY_SELFTEST=PASS")
    return 0


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--strict-format", action="store_true", help="treat top-level markdown fences as errors")
    parser.add_argument("--self-test", action="store_true", help="run internal positive and negative fixtures")
    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    if args.self_test:
        return run_self_test()

    findings = check_registry(SKILLS_DIR, README, strict_format=args.strict_format)
    for finding in findings:
        print(finding.format())

    errors = [finding for finding in findings if finding.severity == "ERROR"]
    warnings = [finding for finding in findings if finding.severity == "WARN"]
    if errors:
        print("MANDATE_REGISTRY_CHECK=FAIL")
        print(f"errors={len(errors)} warnings={len(warnings)} mandates={len(mandate_files(SKILLS_DIR))}")
        return 1

    print("MANDATE_REGISTRY_CHECK=PASS")
    print(f"errors=0 warnings={len(warnings)} mandates={len(mandate_files(SKILLS_DIR))}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

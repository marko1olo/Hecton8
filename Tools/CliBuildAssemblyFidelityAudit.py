#!/usr/bin/env python3
"""Measure how far a hand-written CLI .csproj drifts from Unity's real assembly.

Evidence class: STATIC_SOURCE. This tool compares the file set a hand-written
`.csproj` compiles against the file set Unity actually puts in the assembly of
the same name, derived from the `.asmdef` ownership graph. It does not compile
anything and it does not prove compile health.

WHY THIS EXISTS
    The lock-free CLI bypass (`dotnet build Hecton8.Core.csproj`) is the only way
    to type-check C# while another agent holds `Temp/UnityLockfile`. Its verdict
    is only worth what its file set is worth. `Hecton8.Core.csproj` is
    hand-written and gitignored: it globs the whole source tree and subtracts a
    maintained-by-hand Remove list, so it does not respect asmdef boundaries.
    Drift is invisible and silently makes "0 errors" mean less than it reads.

    Two failure directions, and only one of them is loud:
      EXTRA   - files the csproj compiles that Unity puts in a DIFFERENT assembly.
                Loud: duplicate types surface as CS0433 downstream.
      MISSING - files Unity compiles INTO this assembly that the csproj skips.
                Silent, and the dangerous one: a real error in those files
                cannot be seen by the bypass. That is a false green.

INSTRUMENT SELF-TEST
    Every scan in this workstream produced a wrong answer before a right one, so
    this one refuses to report until it has re-detected known-answer cases (see
    SELF_TEST_CASES). A scan that reports "clean" and cannot detect its own
    motivating example is worth nothing. `--check-dll` grounds those cases in
    Unity's own compiled output instead of in this file's assumptions.

LIMITATION
    Ownership is resolved by nearest-ancestor `.asmdef`. `.asmref` files reassign
    ownership and are NOT modelled; there are currently none under the audited
    root, and the tool fails loudly if that changes.

USAGE
    python Tools/CliBuildAssemblyFidelityAudit.py
    python Tools/CliBuildAssemblyFidelityAudit.py --check-dll
    python Tools/CliBuildAssemblyFidelityAudit.py --emit-compile-items

EXIT CODES
    0 file set matches Unity exactly
    1 drift found
    2 instrument self-test failed - the report is suppressed, believe nothing
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_SOURCE_ROOT = REPO_ROOT / "Assets" / "_Project" / "Scripts"
DEFAULT_ASSEMBLY = "Hecton8.Core"
SCRIPT_ASSEMBLIES = REPO_ROOT / "Library" / "ScriptAssemblies"

# Known-answer cases the instrument must reproduce before its report is trusted.
# Each is (relative .cs path, expected verdict, type name, assembly Unity really
# compiles it into). Verified 2026-07-27 against Library/ScriptAssemblies.
SELF_TEST_CASES = [
    (
        "PureLogic/Systems/VerletCableSimulator.cs",
        "EXTRA",
        "VerletCableSimulator",
        "Hecton8.PureLogic",
    ),
    (
        "Audio/Editor/AbyssalAcousticsTunerWindow.cs",
        "MISSING",
        "AbyssalAcousticsTunerWindow",
        "Hecton8.Core",
    ),
]


def glob_to_regex(pattern: str) -> re.Pattern:
    """Translate the MSBuild glob subset these csproj files use into a regex.

    Handles `**` (zero or more directories) and `*` (no separator). MSBuild
    collapses `a/**/b` to also match `a/b`, which a naive translation misses.
    """
    normalised = pattern.replace("\\", "/")
    out = []
    i = 0
    while i < len(normalised):
        if normalised.startswith("/**/", i):
            out.append("(?:/.*)?/")
            i += 4
        elif normalised.startswith("**/", i):
            out.append("(?:.*/)?")
            i += 3
        elif normalised.startswith("**", i):
            out.append(".*")
            i += 2
        elif normalised[i] == "*":
            out.append("[^/]*")
            i += 1
        else:
            out.append(re.escape(normalised[i]))
            i += 1
    return re.compile("^" + "".join(out) + "$", re.IGNORECASE)


def all_cs_files(root: Path) -> list[str]:
    found = []
    for dirpath, _dirnames, filenames in os.walk(root):
        for name in filenames:
            if name.endswith(".cs"):
                rel = Path(dirpath, name).resolve().relative_to(REPO_ROOT)
                found.append(rel.as_posix())
    return sorted(found)


def asmdef_owners(root: Path) -> dict[str, str]:
    """Map every directory holding an .asmdef to the assembly name it declares."""
    owners: dict[str, str] = {}
    for dirpath, _dirnames, filenames in os.walk(root):
        for name in filenames:
            if not name.endswith(".asmdef"):
                continue
            path = Path(dirpath, name)
            try:
                declared = json.loads(path.read_text(encoding="utf-8-sig")).get("name")
            except (OSError, ValueError) as exc:
                raise SystemExit(f"unreadable asmdef {path}: {exc}")
            if not declared:
                raise SystemExit(f"asmdef without a name: {path}")
            key = path.parent.resolve().relative_to(REPO_ROOT).as_posix()
            owners[key] = declared
    return owners


def owning_assembly(rel_cs: str, owners: dict[str, str]) -> str | None:
    """Nearest-ancestor .asmdef wins - Unity's own ownership rule."""
    directory = Path(rel_cs).parent.as_posix()
    while True:
        if directory in owners:
            return owners[directory]
        if "/" not in directory:
            return None
        directory = directory.rsplit("/", 1)[0]


def csproj_compile_set(csproj: Path, universe: list[str]) -> set[str]:
    text = csproj.read_text(encoding="utf-8")
    selected: set[str] = set()
    # Include and Remove are order-sensitive in MSBuild; replay them in file order.
    for match in re.finditer(r"<Compile\s+(Include|Remove)=\"([^\"]+)\"", text):
        verb, pattern = match.group(1), match.group(2)
        matcher = glob_to_regex(pattern)
        hits = {f for f in universe if matcher.match(f)}
        if verb == "Include":
            selected |= hits
        else:
            selected -= hits
    return selected


def dll_contains(assembly: str, type_name: str) -> bool | None:
    dll = SCRIPT_ASSEMBLIES / f"{assembly}.dll"
    if not dll.is_file():
        return None
    needle = type_name.encode("ascii")
    return needle in dll.read_bytes()


def run_self_test(compiled: set[str], unity: set[str], source_root: Path, check_dll: bool) -> list[str]:
    failures = []
    for rel_suffix, expected, type_name, real_assembly in SELF_TEST_CASES:
        rel = (source_root / rel_suffix).resolve().relative_to(REPO_ROOT).as_posix()
        if not (REPO_ROOT / rel).is_file():
            failures.append(f"self-test source vanished: {rel} - refresh SELF_TEST_CASES")
            continue
        if expected == "EXTRA":
            observed = rel in compiled and rel not in unity
        else:
            observed = rel in unity and rel not in compiled
        if not observed:
            failures.append(
                f"self-test case {rel} was NOT detected as {expected} - "
                "the glob translation or the ownership model is wrong"
            )
        if check_dll:
            present = dll_contains(real_assembly, type_name)
            if present is None:
                failures.append(
                    f"--check-dll asked for Library/ScriptAssemblies/{real_assembly}.dll "
                    "and it is not there; run Unity once or drop the flag"
                )
            elif not present:
                failures.append(
                    f"Unity's {real_assembly}.dll does not contain {type_name}; "
                    "the known-answer case is stale, not the code"
                )
    return failures


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--csproj", default=None, help="defaults to <assembly>.csproj at the repo root")
    parser.add_argument("--assembly", default=DEFAULT_ASSEMBLY)
    parser.add_argument("--source-root", default=str(DEFAULT_SOURCE_ROOT))
    parser.add_argument("--check-dll", action="store_true", help="ground the self-test in Unity's compiled output")
    parser.add_argument("--emit-compile-items", action="store_true", help="print a correct <ItemGroup> and exit")
    parser.add_argument("--list-limit", type=int, default=15)
    args = parser.parse_args()

    source_root = Path(args.source_root).resolve()
    csproj = Path(args.csproj).resolve() if args.csproj else REPO_ROOT / f"{args.assembly}.csproj"
    if not source_root.is_dir():
        raise SystemExit(f"no such source root: {source_root}")
    if not csproj.is_file():
        raise SystemExit(f"no such csproj: {csproj} (they are gitignored - run Unity or write one)")

    strays = [p.as_posix() for p in source_root.rglob("*.asmref")]
    if strays:
        raise SystemExit(
            "asmref files appeared under the audited root and this tool does not model them:\n  "
            + "\n  ".join(strays)
        )

    universe = all_cs_files(source_root)
    owners = asmdef_owners(source_root)
    unity = {f for f in universe if owning_assembly(f, owners) == args.assembly}
    compiled = csproj_compile_set(csproj, universe)

    failures = run_self_test(compiled, unity, source_root, args.check_dll)
    if failures:
        print("INSTRUMENT SELF-TEST FAILED - report suppressed")
        for failure in failures:
            print(f"  {failure}")
        return 2

    extra = sorted(compiled - unity)
    missing = sorted(unity - compiled)

    if args.emit_compile_items:
        print("  <ItemGroup>")
        print(f'    <Compile Include="{source_root.relative_to(REPO_ROOT).as_posix()}/**/*.cs" />')
        for directory in sorted({d for d in owners if d != source_root.relative_to(REPO_ROOT).as_posix()}):
            print(f'    <Compile Remove="{directory}/**/*.cs" />')
        print("  </ItemGroup>")
        return 0

    print(f"CLI BUILD FIDELITY  assembly={args.assembly}  csproj={csproj.name}")
    print(f"  self-test           PASSED ({len(SELF_TEST_CASES)} known-answer cases{', DLL-grounded' if args.check_dll else ''})")
    print(f"  Unity compiles      {len(unity)} .cs")
    print(f"  csproj compiles     {len(compiled)} .cs")
    print(f"  EXTRA   (loud)      {len(extra)} .cs the csproj compiles and Unity puts elsewhere")
    print(f"  MISSING (false green) {len(missing)} .cs Unity compiles here and the csproj never sees")

    by_assembly: dict[str, int] = {}
    for rel in extra:
        by_assembly[owning_assembly(rel, owners) or "<no asmdef>"] = (
            by_assembly.get(owning_assembly(rel, owners) or "<no asmdef>", 0) + 1
        )
    if by_assembly:
        print("\n  EXTRA files really belong to:")
        for name, count in sorted(by_assembly.items(), key=lambda kv: -kv[1]):
            print(f"    {count:5d}  {name}")

    if missing:
        print(f"\n  MISSING sample (first {args.list_limit}):")
        for rel in missing[: args.list_limit]:
            print(f"    {rel}")
        if len(missing) > args.list_limit:
            print(f"    ... and {len(missing) - args.list_limit} more")

    return 0 if not extra and not missing else 1


if __name__ == "__main__":
    sys.exit(main())

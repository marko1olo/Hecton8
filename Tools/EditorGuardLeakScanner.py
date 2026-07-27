"""Detects runtime members that a player build cannot see.

An `#if UNITY_EDITOR` region opened too wide swallows methods that unguarded runtime code
calls. Nothing surfaces in the Editor, because the Editor always defines UNITY_EDITOR, and
Unity's generated .csproj also compiles in editor mode - so `dotnet build` is blind to it
too. The break only appears when someone finally produces a player build.

Found four real instances in the HECTON-8 runtime tree: SubmarineDynamicsRuntime (16
members, the whole submarine boot chain), ShinobuPhysiologyRuntime (8, job-buffer locking
and tick registration), AdaptiveStemAudioMixer (1, the per-tick narrative-override gate),
HectonSurvivalSystem (1, an overload pair split by the guard).

Usage:
    python -B Tools/EditorGuardLeakScanner.py                  scan the runtime tree
    python -B Tools/EditorGuardLeakScanner.py --path <file>    scan one file
    python -B Tools/EditorGuardLeakScanner.py --self-test      run the built-in fixtures

Exit code 1 when violations are found, so it can be used as a gate.

Reports candidates, not proven breaks. Each hit needs a read: this is a brace/regex model,
not a C# parser. It does not resolve partial classes split across files, extension methods,
or `#elif` chains carrying three or more alternate definitions.

KNOWN BLIND SPOT - overload split by a guard. Members are grouped by (type, name), not by
signature, so when one overload is guarded and another is not, the scanner treats the name
as always present and stays silent. Overload *resolution* still fails in a player build if
the call binds to the guarded signature. This is the HectonSurvivalSystem
TryGetInjectedItemParameters shape: the string overload was guarded, the ItemData overload
was not, and the latter called the former. That instance was found by an earlier, noisier
revision of this scanner and fixed by hand. Grouping by arity was tried and rejected -
multi-line signatures made it fall back to name-only matching, which reintroduced the
false-positive class this scanner exists to avoid. Pinned by
test_known_blind_spot_overload_split_by_guard; if you teach the scanner signatures, that
test will fail and should be promoted to a detection case.
"""

import argparse
import glob
import os
import re
import sys

TYPE_RE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*"
    r"(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|unsafe|\s)*"
    r"\b(?:class|struct)\s+(\w+)"
)
DECL_RE = re.compile(r"^\s*(?:private|internal|public|protected)[\w\s<>,\[\]\.]*?\b(\w+)\s*\(")
IF_RE = re.compile(r"#\s*if\s+(.*)")
ENDIF_RE = re.compile(r"#\s*endif")
ELSE_RE = re.compile(r"#\s*(else|elif)")

# Symbols that are absent from a release player build. A guard whose every disjunct is one
# of these compiles out; a guard containing anything else may still hold, so it is ignored.
PLAYER_ABSENT = ("UNITY_EDITOR", "DEVELOPMENT_BUILD", "UNITY_INCLUDE_TESTS")
ELSE_MARK = "<else-of> "


def _classify(lines):
    """Return (guarded, condition, chain_id) per 1-based line."""
    count = len(lines)
    guarded = [False] * (count + 2)
    condition = [None] * (count + 2)
    chain = [None] * (count + 2)
    stack = []
    chain_counter = 0
    for index, raw in enumerate(lines, 1):
        text = raw.strip()
        opened = IF_RE.match(text)
        if opened:
            chain_counter += 1
            stack.append([opened.group(1).strip(), chain_counter])
            guarded[index] = True
            continue
        if ENDIF_RE.match(text):
            guarded[index] = True
            if stack:
                stack.pop()
            continue
        if ELSE_RE.match(text):
            guarded[index] = True
            if stack:
                stack[-1][0] = ELSE_MARK + stack[-1][0]
            continue
        guarded[index] = bool(stack)
        if stack:
            condition[index] = " && ".join(entry[0] for entry in stack)
            chain[index] = stack[-1][1]
    if stack:
        return None
    return guarded, condition, chain


def _owners(lines):
    """Map each 1-based line to its innermost enclosing class/struct name."""
    owner = [None] * (len(lines) + 2)
    stack = []
    brace = 0
    pending = None
    for index, raw in enumerate(lines, 1):
        declared = TYPE_RE.match(raw)
        if declared:
            pending = declared.group(1)
        for char in re.sub(r"//.*", "", raw):
            if char == "{":
                brace += 1
                if pending:
                    stack.append((pending, brace))
                    pending = None
            elif char == "}":
                if stack and stack[-1][1] == brace:
                    stack.pop()
                brace -= 1
        owner[index] = stack[-1][0] if stack else None
    return owner


def compiled_out_of_player(condition):
    """True when the guard condition is false in a release player build."""
    if not condition or ELSE_MARK in condition:
        return False
    for disjunct in condition.split("||"):
        token = disjunct.strip().strip("()")
        if "!" in token:
            return False
        if not any(absent in token for absent in PLAYER_ABSENT):
            return False
    return True


def scan_text(lines, path="<memory>"):
    classified = _classify(lines)
    if classified is None:
        return []
    guarded, condition, chain = classified
    owner = _owners(lines)

    declarations = {}
    for index, raw in enumerate(lines, 1):
        declared = DECL_RE.match(raw)
        if declared:
            key = (owner[index], declared.group(1))
            declarations.setdefault(key, []).append(
                (index, guarded[index], chain[index], condition[index])
            )

    violations = []
    for (type_name, member), sites in declarations.items():
        # An unguarded declaration anywhere means the symbol always exists.
        if any(not is_guarded for _, is_guarded, _, _ in sites):
            continue
        # Two or more declarations in one #if/#else chain are alternate definitions.
        if len(sites) >= 2 and len({site_chain for _, _, site_chain, _ in sites}) == 1:
            continue
        decl_line, _, _, decl_condition = sites[0]
        if not compiled_out_of_player(decl_condition):
            continue
        declared_at = {site_line for site_line, _, _, _ in sites}
        call_re = re.compile(r"(?<![\w.])" + re.escape(member) + r"\s*\(")
        for index, raw in enumerate(lines, 1):
            if guarded[index] or owner[index] != type_name or index in declared_at:
                continue
            if "new " + member in raw:
                continue
            if call_re.search(raw):
                violations.append(
                    {
                        "path": path,
                        "type": type_name,
                        "member": member,
                        "declared": decl_line,
                        "called": index,
                        "condition": decl_condition,
                    }
                )
                break
    return violations


def scan_file(path):
    try:
        with open(path, encoding="utf-8", errors="replace") as handle:
            lines = handle.readlines()
    except OSError:
        return []
    return scan_text(lines, path)


def scan_tree(root="Assets/_Project/Scripts"):
    found = []
    for path in glob.glob(os.path.join(root, "**", "*.cs"), recursive=True):
        if os.sep + "Editor" + os.sep in path:
            continue
        found.extend(scan_file(path))
    return found


BROKEN_FIXTURE = """
public class Widget
{
    private void Boot()
    {
        Prepare();
    }
#if UNITY_EDITOR
    private void Prepare()
    {
    }
#endif
}
""".splitlines(keepends=True)

ELSE_PAIR_FIXTURE = """
public class Widget
{
    private void Boot()
    {
        Prepare();
    }
#if UNITY_EDITOR
    private void Prepare()
    {
    }
#else
    private void Prepare()
    {
    }
#endif
}
""".splitlines(keepends=True)

DEV_BUILD_FIXTURE = """
public class Widget
{
    private void Boot()
    {
        Prepare();
    }
#if UNITY_ADDRESSABLES_EXIST
    private void Prepare()
    {
    }
#endif
}
""".splitlines(keepends=True)

TWO_TYPES_FIXTURE = """
public class Alpha
{
    private void Run()
    {
        Execute();
    }
    private void Execute()
    {
    }
}
public class Beta
{
#if UNITY_EDITOR
    private void Execute()
    {
    }
#endif
}
""".splitlines(keepends=True)


def self_test():
    cases = [
        ("guarded declaration, unguarded call", BROKEN_FIXTURE, 1),
        ("#if/#else alternate definitions", ELSE_PAIR_FIXTURE, 0),
        ("guard that still holds in a player", DEV_BUILD_FIXTURE, 0),
        ("same member name in two types", TWO_TYPES_FIXTURE, 0),
    ]
    failures = 0
    for label, fixture, expected in cases:
        actual = len(scan_text(fixture, "<fixture>"))
        status = "PASS" if actual == expected else "FAIL"
        if actual != expected:
            failures += 1
        print(f"  [{status}] {label}: expected {expected}, got {actual}")
    print(f"self-test: {len(cases) - failures} passed, {failures} failed")
    return 1 if failures else 0


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--path", help="scan a single file instead of the tree")
    parser.add_argument("--root", default="Assets/_Project/Scripts", help="tree root to scan")
    parser.add_argument("--self-test", action="store_true", help="run the built-in fixtures")
    args = parser.parse_args()

    if args.self_test:
        return self_test()

    violations = scan_file(args.path) if args.path else scan_tree(args.root)
    if not violations:
        print("EditorGuardLeakScanner: no guarded declarations reachable from unguarded code.")
        return 0

    by_path = {}
    for entry in violations:
        by_path.setdefault(entry["path"], []).append(entry)
    print(f"EditorGuardLeakScanner: {len(violations)} candidates in {len(by_path)} files")
    for path, entries in sorted(by_path.items(), key=lambda item: -len(item[1])):
        print(f"  {len(entries):3d}  {path}  [{entries[0]['condition']}]")
        for entry in entries:
            print(
                f"         {entry['type']}.{entry['member']}"
                f" declared {entry['declared']} -> called unguarded {entry['called']}"
            )
    print("\nCandidates, not proven breaks. Read each before changing it.")
    return 1


if __name__ == "__main__":
    sys.exit(main())

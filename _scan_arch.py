# -*- coding: utf-8 -*-
"""Read-only architecture evidence scan. Writes results to _arch_scan_*.txt"""
from __future__ import annotations
import os
import re
from collections import Counter

ROOT = r"Assets/_Project/Scripts"
OUT = {
    "loc": "_arch_scan_loc.txt",
    "dve": "_arch_scan_dve.txt",
    "struct": "_arch_scan_structbool.txt",
    "update": "_arch_scan_update.txt",
    "coro": "_arch_scan_coro.txt",
    "find": "_arch_scan_find.txt",
    "signal": "_arch_scan_signal.txt",
    "complete": "_arch_scan_sd_complete.txt",
}


def path_norm(p: str) -> str:
    return p.replace("/", "\\")


def is_excluded(path: str, extra: tuple[str, ...] = ()) -> bool:
    low = path_norm(path).lower()
    banned = (
        "\\tests\\",
        "\\editor\\",
        "\\qa\\",
        "\\headless\\",
        "\\modding\\",
        "\\moddingapi\\",
    ) + extra
    for b in banned:
        if b in low:
            return True
    base = os.path.basename(low)
    if "test" in base and base.endswith(".cs"):
        # keep non-test names that merely contain 'test' carefully:
        if any(x in base for x in ("test.cs", "tests.cs", "smoketester", "edittests", "tester.cs")):
            return True
        if base.endswith("test.cs") or ".test." in base or base.startswith("test"):
            return True
    if ".editor." in base or base.endswith("editor.cs"):
        return True
    return False


def is_runtime_script_path(path: str) -> bool:
    low = path_norm(path).lower()
    if "\\tests\\" in low or low.endswith("\\tests"):
        return False
    if "\\editor\\" in low or low.endswith("\\editor"):
        return False
    return path.lower().endswith(".cs")


def count_lines(path: str) -> int:
    with open(path, "rb") as fh:
        return sum(1 for _ in fh)


def scan_loc() -> None:
    results = []
    for dirpath, dirnames, filenames in os.walk(ROOT):
        # prune Tests/Editor dirs
        dirnames[:] = [d for d in dirnames if d.lower() not in ("tests", "editor")]
        low = path_norm(dirpath).lower()
        if "\\tests" in low or "\\editor" in low:
            continue
        for f in filenames:
            if not f.endswith(".cs"):
                continue
            path = os.path.join(dirpath, f)
            if not is_runtime_script_path(path):
                continue
            try:
                n = count_lines(path)
            except OSError:
                continue
            if n >= 2500:
                results.append((n, path_norm(path)))
    results.sort(reverse=True)
    lines = [f"TOTAL_FILES_GE_2500={len(results)}"]
    for n, p in results[:40]:
        lines.append(f"{n}\t{p}")
    open(OUT["loc"], "w", encoding="utf-8").write("\n".join(lines))
    print(f"[loc] {len(results)} files >= 2500")


def scan_dve() -> None:
    # Prefer prebuilt rg -c output if present; else walk
    raw = "_arch_scan_dve_raw.txt"
    c = Counter()
    total = 0
    if os.path.isfile(raw) and os.path.getsize(raw) > 0:
        for line in open(raw, encoding="utf-8", errors="replace"):
            line = line.strip()
            if not line or ":" not in line:
                continue
            path, n_s = line.rsplit(":", 1)
            try:
                n = int(n_s)
            except ValueError:
                continue
            c[path_norm(path)] = n
            total += n
    else:
        for dirpath, _, filenames in os.walk("Assets"):
            if "Library" in dirpath or "obj" in dirpath:
                continue
            for f in filenames:
                if not f.endswith(".cs"):
                    continue
                path = os.path.join(dirpath, f)
                try:
                    text = open(path, encoding="utf-8", errors="replace").read()
                except OSError:
                    continue
                n = text.count("DataVaultExempt")
                if n:
                    c[path_norm(path)] = n
                    total += n
    lines = [f"TOTAL={total}"]
    for path, n in c.most_common(30):
        lines.append(f"{n}\t{path}")
    open(OUT["dve"], "w", encoding="utf-8").write("\n".join(lines))
    print(f"[dve] total={total} files={len(c)}")


STRUCT_LAYOUT_RE = re.compile(r"\[StructLayout\b")
STRUCT_DECL_RE = re.compile(r"\bstruct\s+(\w+)")
CLASS_DECL_RE = re.compile(r"\bclass\s+(\w+)")
BOOL_FIELD_RE = re.compile(
    r"(?:public|private|internal|protected|unsafe)?\s*(?:readonly\s+)?bool\s+(\w+)\s*[;=]"
)
BOOL_FIELD_SEMI_RE = re.compile(
    r"(?:public|private|internal|protected)?\s*(?:readonly\s+)?bool\s+(\w+)\s*;"
)


def scan_struct_bool() -> None:
    results = []
    seen = set()
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d.lower() not in ("tests", "editor")]
        low = path_norm(dirpath).lower()
        if "\\tests" in low or "\\editor" in low:
            continue
        for f in filenames:
            if not f.endswith(".cs"):
                continue
            path = os.path.join(dirpath, f)
            if not is_runtime_script_path(path):
                continue
            try:
                lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
            except OSError:
                continue
            i = 0
            while i < len(lines):
                if not STRUCT_LAYOUT_RE.search(lines[i]):
                    i += 1
                    continue
                # find struct within next 25 lines
                struct_name = None
                struct_idx = None
                for j in range(i, min(len(lines), i + 25)):
                    if CLASS_DECL_RE.search(lines[j]) and not STRUCT_DECL_RE.search(lines[j]):
                        break
                    m = STRUCT_DECL_RE.search(lines[j])
                    if m:
                        struct_name = m.group(1)
                        struct_idx = j
                        break
                if not struct_name:
                    i += 1
                    continue
                key = (path_norm(path), struct_name)
                if key in seen:
                    i += 1
                    continue
                seen.add(key)
                # parse body
                depth = 0
                started = False
                bools = []
                for j in range(struct_idx, min(len(lines), struct_idx + 500)):
                    s = lines[j]
                    # strip comments roughly
                    if "//" in s:
                        s_code = s.split("//", 1)[0]
                    else:
                        s_code = s
                    if not started:
                        if "{" in s_code:
                            started = True
                            depth += s_code.count("{") - s_code.count("}")
                        continue
                    depth += s_code.count("{") - s_code.count("}")
                    # only look at depth==1 field lines (struct body)
                    # allow nested briefly but prefer simple field match
                    if "(" in s_code and ")" in s_code and "bool" in s_code and not s_code.strip().endswith(";"):
                        # likely method signature
                        if depth > 0:
                            pass
                    else:
                        for rx in (BOOL_FIELD_SEMI_RE, BOOL_FIELD_RE):
                            for bm in rx.finditer(s_code):
                                name = bm.group(1)
                                if name not in bools and name not in ("true", "false"):
                                    bools.append(name)
                    if started and depth <= 0:
                        break
                if bools:
                    results.append((path_norm(path), struct_name, bools))
                i += 1
    lines_out = [f"TOTAL_STRUCTS_WITH_BOOL={len(results)}"]
    for path, name, bools in results[:80]:
        lines_out.append(f"{path} | {name} | {', '.join(bools)}")
    if len(results) > 80:
        lines_out.append(f"... truncated {len(results) - 80} more")
    open(OUT["struct"], "w", encoding="utf-8").write("\n".join(lines_out))
    print(f"[struct] structs_with_bool={len(results)}")


UPDATE_RE = re.compile(r"\bvoid\s+(Update|LateUpdate|FixedUpdate)\s*\(")
CORO_RE = re.compile(r"\bStartCoroutine\s*\(")
FIND_RE = re.compile(
    r"(GameObject\.Find\s*\(|FindObjectOfType\s*[<\(]|FindObjectsOfType\s*[<\(]|"
    r"FindObjectsByType\s*[<\(]|FindFirstObjectByType\s*[<\(]|FindAnyObjectByType\s*[<\(]|"
    r"Camera\.main\b|Resources\.Load\s*[<\(])"
)


def scan_hotpath() -> None:
    updates = []
    coros = []
    finds = []
    for dirpath, dirnames, filenames in os.walk(ROOT):
        # keep walking but filter per-file; still prune Tests/Editor deep trees for speed where possible
        for f in filenames:
            if not f.endswith(".cs"):
                continue
            path = os.path.join(dirpath, f)
            pnorm = path_norm(path)
            try:
                file_lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
            except OSError:
                continue
            for idx, line in enumerate(file_lines, 1):
                code = line.split("//", 1)[0]
                if UPDATE_RE.search(code):
                    if not is_excluded(pnorm, ()):
                        # extra: skip QA/Headless already in is_excluded
                        updates.append(f"{pnorm}:{idx}:{line.strip()}")
                if CORO_RE.search(code):
                    if not is_excluded(pnorm, ()):  # excludes Modding too
                        coros.append(f"{pnorm}:{idx}:{line.strip()}")
                if FIND_RE.search(code):
                    # Find excludes Tests/Editor only (not Modding/QA by task wording for Find)
                    low = pnorm.lower()
                    if "\\tests\\" in low or "\\editor\\" in low:
                        continue
                    base = os.path.basename(low)
                    if "test" in base and any(
                        x in base for x in ("test.cs", "tests.cs", "smoketester", "edittests", "tester.cs")
                    ):
                        continue
                    if ".editor." in base or base.endswith("editor.cs"):
                        continue
                    finds.append(f"{pnorm}:{idx}:{line.strip()}")
    open(OUT["update"], "w", encoding="utf-8").write(
        f"TOTAL={len(updates)}\n" + "\n".join(updates)
    )
    open(OUT["coro"], "w", encoding="utf-8").write(
        f"TOTAL={len(coros)}\n" + "\n".join(coros)
    )
    open(OUT["find"], "w", encoding="utf-8").write(
        f"TOTAL={len(finds)}\n" + "\n".join(finds)
    )
    print(f"[hot] update={len(updates)} coro={len(coros)} find={len(finds)}")


def scan_signal_managed() -> None:
    """Find SignalBus usages that look managed-typed, and payload structs with string/class/bool."""
    # 1) Collect SignalBus type args used at publish sites
    publish_re = re.compile(
        r"SignalBus\s*<\s*([\w\.]+)\s*>\s*\.\s*(TryPush|TryPushTracked|Publish|TryPublish|Push)\s*"
    )
    type_uses = Counter()
    sites = []
    type_def_files = {}
    for dirpath, _, filenames in os.walk(ROOT):
        for f in filenames:
            if not f.endswith(".cs"):
                continue
            path = os.path.join(dirpath, f)
            try:
                text = open(path, encoding="utf-8", errors="replace").read()
            except OSError:
                continue
            for m in publish_re.finditer(text):
                tname = m.group(1)
                type_uses[tname] += 1
                # line number
                ln = text.count("\n", 0, m.start()) + 1
                line = text.splitlines()[ln - 1].strip()
                sites.append((path_norm(path), ln, tname, m.group(2), line))

    # 2) For each unique type, try to find struct/class definition and check fields
    unique_types = sorted(type_uses.keys())
    managed_hits = []
    bool_heavy = []
    for tname in unique_types:
        simple = tname.split(".")[-1]
        # search definition
        found = None
        kind = None
        fields_bool = []
        fields_string = []
        fields_classy = []
        for dirpath, _, filenames in os.walk(ROOT):
            for f in filenames:
                if not f.endswith(".cs"):
                    continue
                path = os.path.join(dirpath, f)
                try:
                    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
                except OSError:
                    continue
                for i, line in enumerate(lines):
                    m = re.search(rf"\b(struct|class|record)\s+{re.escape(simple)}\b", line)
                    if not m:
                        continue
                    kind = m.group(1)
                    # parse body briefly
                    depth = 0
                    started = False
                    for j in range(i, min(len(lines), i + 300)):
                        s = lines[j]
                        sc = s.split("//", 1)[0]
                        if not started:
                            if "{" in sc:
                                started = True
                                depth += sc.count("{") - sc.count("}")
                            continue
                        depth += sc.count("{") - sc.count("}")
                        # string field
                        for sm in re.finditer(r"\bstring\s+(\w+)\s*[;=]", sc):
                            fields_string.append(sm.group(1))
                        for sm in re.finditer(r"\bbool\s+(\w+)\s*[;=]", sc):
                            fields_bool.append(sm.group(1))
                        # class-like managed refs (rough): not primitive, ends without *
                        for sm in re.finditer(
                            r"\b(public|private|internal|protected)\s+(?:readonly\s+)?"
                            r"([A-Z][\w\.]*)\s+(\w+)\s*[;=]",
                            sc,
                        ):
                            typ = sm.group(2)
                            if typ in (
                                "int",
                                "uint",
                                "float",
                                "double",
                                "long",
                                "ulong",
                                "short",
                                "ushort",
                                "byte",
                                "sbyte",
                                "bool",
                                "char",
                                "Vector2",
                                "Vector3",
                                "Vector4",
                                "Quaternion",
                                "Color",
                                "Color32",
                                "float2",
                                "float3",
                                "float4",
                                "int2",
                                "int3",
                                "int4",
                                "uint2",
                                "uint3",
                                "uint4",
                                "quaternion",
                                "half",
                                "half2",
                                "half3",
                                "half4",
                                "FixedString32Bytes",
                                "FixedString64Bytes",
                                "FixedString128Bytes",
                                "FixedString512Bytes",
                                "FixedString4096Bytes",
                                "NativeArray",
                                "JobHandle",
                                "Entity",
                                "Hash128",
                                "GUID",
                                "Rect",
                                "Bounds",
                                "Matrix4x4",
                                "Pose",
                            ):
                                continue
                            if typ.endswith("Signal") or typ.endswith("DTO") or typ.endswith("Id"):
                                # still could be struct; skip classification as managed solely by name
                                continue
                            # UnityEngine.Object-ish common
                            if typ in (
                                "string",
                                "object",
                                "Object",
                                "GameObject",
                                "Transform",
                                "Component",
                                "MonoBehaviour",
                                "ScriptableObject",
                                "Material",
                                "Mesh",
                                "Texture",
                                "Texture2D",
                                "AudioClip",
                                "AnimationCurve",
                                "List",
                                "Dictionary",
                                "Action",
                                "Delegate",
                            ):
                                fields_classy.append(f"{typ} {sm.group(3)}")
                        if started and depth <= 0:
                            break
                    found = path_norm(path)
                    break
                if found:
                    break
            if found:
                break
        if kind == "class":
            managed_hits.append((tname, "CLASS", found, type_uses[tname]))
        if fields_string:
            managed_hits.append(
                (tname, f"string_fields={fields_string}", found, type_uses[tname])
            )
        if fields_classy:
            managed_hits.append(
                (tname, f"managed_fields={fields_classy}", found, type_uses[tname])
            )
        if len(fields_bool) >= 3:
            bool_heavy.append((tname, fields_bool, found, type_uses[tname]))
        elif fields_bool:
            bool_heavy.append((tname, fields_bool, found, type_uses[tname]))

    out = []
    out.append(f"PUBLISH_SITES={len(sites)}")
    out.append(f"UNIQUE_PAYLOAD_TYPES={len(unique_types)}")
    out.append("--- managed/class/string payload findings ---")
    if not managed_hits:
        out.append("NONE_MANAGED_STRING_OR_CLASS_PAYLOAD_FOUND")
    else:
        for h in managed_hits:
            out.append(f"{h[0]} | {h[1]} | def={h[2]} | uses={h[3]}")
    out.append("--- bool fields on payload types (all found) ---")
    for tname, fields, found, uses in bool_heavy:
        out.append(f"{tname} | bools={fields} | def={found} | uses={uses}")
    out.append("--- sample publish sites (first 40) ---")
    for p, ln, t, meth, line in sites[:40]:
        out.append(f"{p}:{ln}:{meth}<{t}> {line}")
    open(OUT["signal"], "w", encoding="utf-8").write("\n".join(out))
    print(f"[signal] sites={len(sites)} managed_hits={len(managed_hits)}")


def scan_dispatcher_complete() -> None:
    path = os.path.join(ROOT, "Core", "SystemDispatcher.cs")
    if not os.path.isfile(path):
        open(OUT["complete"], "w", encoding="utf-8").write("MISSING SystemDispatcher.cs")
        print("[sd] missing")
        return
    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
    out = []
    out.append(f"FILE={path_norm(path)} LINES={len(lines)}")
    # Find Schedule + Complete patterns
    schedule_lines = []
    complete_lines = []
    same_frame_suspects = []
    for i, line in enumerate(lines, 1):
        code = line.split("//", 1)[0]
        if re.search(r"\bSchedule\s*\(", code) and "ProfilerMarker" not in code:
            schedule_lines.append((i, line.rstrip()))
        if re.search(r"\.Complete\s*\(", code) or re.search(r"\bComplete\s*\(\s*\)", code):
            complete_lines.append((i, line.rstrip()))
        # same-line Schedule().Complete()
        if re.search(r"Schedule\s*\(.*\)\s*\.Complete\s*\(", code):
            same_frame_suspects.append((i, "SAME_LINE_SCHEDULE_COMPLETE", line.rstrip()))

    # window: if Schedule then Complete within next 40 lines in same method-ish block without phase comment
    for si, sline in schedule_lines:
        for ci, cline in complete_lines:
            if ci < si:
                continue
            if ci - si > 35:
                break
            # skip if Complete is on a different stored handle far away — still flag proximity
            window = "\n".join(lines[si - 1 : ci])
            # named completion windows markers
            named = re.search(
                r"(CompletionWindow|CompleteSimulation|CompleteFixed|PostSimulation|PreSimulation|"
                r"VisualSync|CompleteJobs|DrainCompleted|named completion|JobHandle\.CombineDependencies|"
                r"CompletePending|EndSimulation|CompleteFrame)",
                window,
                re.I,
            )
            # if schedule and complete are very close and not clearly in a Complete* method name context
            context_above = "\n".join(lines[max(0, si - 30) : si])
            in_complete_method = re.search(
                r"void\s+\w*Complete\w*\s*\(|void\s+\w*Drain\w*\s*\(|CompletePending|CompleteSimulationJobs",
                context_above,
            )
            if ci - si <= 15 and not in_complete_method:
                same_frame_suspects.append(
                    (
                        si,
                        f"NEAR_COMPLETE_at_{ci}_delta_{ci-si}"
                        + ("_HAS_NAMED_MARKER" if named else "_NO_NAMED_MARKER"),
                        sline.strip(),
                    )
                )
                same_frame_suspects.append((ci, "COMPLETE_LINE", cline.strip()))

    out.append(f"SCHEDULE_HIT_COUNT={len(schedule_lines)}")
    out.append(f"COMPLETE_HIT_COUNT={len(complete_lines)}")
    out.append("--- all Complete() lines ---")
    for i, l in complete_lines:
        out.append(f"{i}:{l}")
    out.append("--- Schedule lines (subset) ---")
    for i, l in schedule_lines[:80]:
        out.append(f"{i}:{l}")
    out.append("--- same-frame suspects ---")
    if not same_frame_suspects:
        out.append("NONE")
    else:
        # dedupe
        seen = set()
        for item in same_frame_suspects:
            key = (item[0], item[1])
            if key in seen:
                continue
            seen.add(key)
            out.append(f"{item[0]}:[{item[1]}] {item[2]}")
    open(OUT["complete"], "w", encoding="utf-8").write("\n".join(out))
    print(f"[sd] schedule={len(schedule_lines)} complete={len(complete_lines)} suspects={len(same_frame_suspects)}")


def main() -> None:
    print("scan start")
    scan_loc()
    scan_dve()
    scan_struct_bool()
    scan_hotpath()
    scan_signal_managed()
    scan_dispatcher_complete()
    print("scan done")


if __name__ == "__main__":
    main()

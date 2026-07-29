#!/usr/bin/env python
"""Which SignalBus lanes have a writer but no reader (or a reader but no writer)? Static, with two controls.

WHY A STATIC SCAN IS THE ONLY WAY TO SEE THIS
---------------------------------------------
SignalBus consumption is strictly PULL-based. There is NO callback and NO subscription registration to
inspect at runtime: the per-lane dispatch table holds only Dispose/Flush/CopyTelemetry function pointers
(Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:308-333), so a registered lane proves nothing
about anybody reading it. A lane with no reader drains to nobody every frame. A lane with no writer hands
its reader an empty snapshot forever. Neither logs, neither throws, and the telemetry counters on a
never-pushed lane read exactly like the counters on a healthy idle lane. Nothing observable distinguishes
"wired" from "half-wired" at runtime, so the call sites are the only evidence there is.

WHAT COUNTS AS A SITE
---------------------
Member names come from the real API in SignalBusRuntime.cs, not from guessing - the bus is NOT
Publish/Subscribe. Writers: Push / TryPush / TryPushTracked / TryEnqueueBounded plus the job writer-handle
accessors. Readers: GetFrameSnapshot / GetSignals / GetFrameSnapshotArray / TryConsumeFrame / TryGetLatest /
TransformSnapshot / FilterSnapshot. Configure / EnsureInitialized / Dispose and the telemetry counters are
NEITHER: a lane that is configured, registered and flushed every frame and read by nobody is exactly the
failure this tool exists to expose, so letting Configure count as wiring would hide it.

THREE FILTERS THAT CHANGE THE ANSWER
------------------------------------
1. COMMENTS AND STRING LITERALS ARE NOT CODE. Measured on this tree: 212 textual occurrences of
   SignalBus<X>.Member outside the scan root resolve to 4 real call sites - the other 208 are inside string
   literals in tests that assert on source text. Inside GlobalSignals.LegacyFacade.cs, 93 occurrences are
   inside [Obsolete("... Use SignalBus<T>.TryConsumeFrame ...")] MESSAGE TEXT. A text-only count sees 186
   sites in that file where 93 exist. The report prints this phantom count per file so the number is
   auditable and not a claim you have to take on trust.
2. COMPILE-DEAD SITES. A member marked [Obsolete(..., true)] cannot be called at all - a call is CS0619, a
   compile ERROR - so its body is not a reader. Without this filter GlobalSignals.LegacyFacade.cs alone
   donates a fake TryConsumeFrame to dozens of lanes and orphans look healthy. Detected by attribute, not
   by filename, so a second legacy file cannot slip through.
3. USING-ALIASES. `using CoreCombatDamageSignal = ...Signals.CombatDamageSignal;` means
   SignalBus<CoreCombatDamageSignal> is the CombatDamageSignal lane. Unresolved, one lane splits into two
   halves and both look half-wired. Seven aliases in this tree do exactly that.

MANDATORY POSITIVE CONTROLS
---------------------------
Every run resolves two lanes KNOWN to be fully wired, and asserts the publish AND the consume side at an
exact expected file:line. Both are wrapped in an owner facade (TryPublish / TryPushTracked / TryDequeue /
TryConsumeFrame helpers) on purpose: most lanes in this project are used that way, so the controls prove the
scan sees THROUGH a facade rather than only finding bare call sites. If either control fails, the classifier
is broken and every negative result in the run is MEANINGLESS - the run says so in those words and exits
non-zero, instead of letting a parse failure look like a finding. This is not ceremony: the sibling tool
Tools/AuditGuidReachability.py exists because a text-only search silently under-reported, and its own first
control choice was wrong in a way only a control caught.

WHAT THIS TOOL CANNOT KNOW
--------------------------
A zero here is a QUESTION, not a verdict.
  * A write-only lane can be CORRECT BY DESIGN. Telemetry, black-box and crash-forensics lanes are
    published so an external reader or a post-mortem dump can pick them up; having no in-tree consumer is
    the point. This tool cannot tell those apart from an abandoned lane, and it does not try.
  * A read-only lane can be CORRECT. A lane written from a Burst job through OpenParallelWriter /
    ParallelWriter hands a writer handle to code this scan cannot follow, and a lane fed by an editor tool,
    a test harness, a mod through HectonEventBus, or generated code has a producer outside the scan root.
    Run --outside to see what lives beyond the root instead of guessing.
  * #if-disabled branches are counted as LIVE code. Scenes, prefabs, ScriptableObjects, other assemblies
    and reflection are not read at all.
  * The owner type/member printed against each site is best-effort text attribution for triage, not a parse.
So this tool says WHERE TO LOOK. It never says a lane is dead. The last time something in this repo was
declared dead on a static reachability walk, a tool deleted a node from the authored world graph.

USAGE, from the repo root:

    python -B Tools/AuditSignalLaneWiring.py              # full report
    python -B Tools/AuditSignalLaneWiring.py --check      # summary + drift only, non-zero exit on drift
    python -B Tools/AuditSignalLaneWiring.py --lane NAME  # every site for one lane, for triage
    python -B Tools/AuditSignalLaneWiring.py --outside    # producers/consumers OUTSIDE the scan root
"""
import bisect
import os
import re
import sys

SCAN_ROOT = os.path.join("Assets", "_Project", "Scripts")
OUTSIDE_ROOT = "Assets"

# Real member names, read out of Core/Signals/SignalBusRuntime.cs. Line numbers are the declarations.
PUBLISH_MEMBERS = {
    "Push": "SignalBusRuntime.cs:669",
    "TryPush": "SignalBusRuntime.cs:678",
    "TryPushTracked": "SignalBusRuntime.cs:722",
    "TryEnqueueBounded": "SignalBusRuntime.cs:733",
    # Writer-handle accessors: weaker evidence than a push. They hand a ring writer to a job and the real
    # enqueue happens where this scan cannot follow it. Counted as a producer, flagged in the lane lists.
    "OpenParallelWriter": "SignalBusRuntime.cs:480",
    "ParallelWriter": "SignalBusRuntime.cs:497",
    "RingParallelWriter": "SignalBusRuntime.cs:506",
    "ParallelWriterBudget": "SignalBusRuntime.cs:487",
}
WRITER_HANDLE_MEMBERS = frozenset(
    ("OpenParallelWriter", "ParallelWriter", "RingParallelWriter", "ParallelWriterBudget"))

CONSUME_MEMBERS = {
    "GetFrameSnapshot": "SignalBusRuntime.cs:773",
    "GetSignals": "SignalBusRuntime.cs:786",
    "GetFrameSnapshotArray": "SignalBusRuntime.cs:792",
    "TryConsumeFrame": "SignalBusRuntime.cs:806",
    "TryGetLatest": "SignalBusRuntime.cs:765",
    "TransformSnapshot": "SignalBusRuntime.cs:820",
    "FilterSnapshot": "SignalBusRuntime.cs:848",
}

# NEITHER. Lifecycle and telemetry touch a lane without moving a payload through it. That is the exact state
# this tool exists to expose, so they must never launder a lane into "wired".
LIFECYCLE_MEMBERS = frozenset((
    "Configure", "ConfigureCacheLineCritical", "EnsureInitialized", "Dispose", "FlushPostSimulation",
))
TELEMETRY_MEMBERS = frozenset((
    "LaneHash", "SnapshotCount", "SnapshotGeneration", "DroppedLastFlush", "LoadShedTotal",
    "CorruptedSignalTotal", "PeakQueuedLastFlush", "HasNativeStorage",
))

CLASS_LIVE = "LIVE (producer and consumer)"
CLASS_PUBLISH_ONLY = "PUBLISHED, NEVER CONSUMED"
CLASS_CONSUME_ONLY = "CONSUMED, NEVER PUBLISHED"
CLASS_DEAD = "DEAD AT BOTH ENDS"
CLASS_ORDER = (CLASS_LIVE, CLASS_PUBLISH_ONLY, CLASS_CONSUME_ONLY, CLASS_DEAD)

# Regression guard. These are THIS TOOL's own measurements on 2026-07-29, not somebody's recon: a baseline
# the tool cannot reproduce is an alarm that fires every run and therefore guards nothing. Update it
# deliberately, in the same commit as the wiring change that moved it, with the reason in the message.
BASELINE = {
    "lanes": 296,
    CLASS_LIVE: 157,
    CLASS_PUBLISH_ONLY: 114,
    CLASS_CONSUME_ONLY: 8,
    CLASS_DEAD: 17,
    "compile_dead_sites": 93,
}

# The 2026-07-29 hand recon, kept so the difference is on the record instead of being quietly overwritten.
# Reproduced exactly: the 296-lane universe. Contradicted with a mechanism: its 186 compile-dead sites are a
# TEXT count - 93 of those 186 are inside [Obsolete("...")] message strings, not call sites (see PHANTOMS in
# the report, which recomputes both numbers every run). Its class counts differ from this tool's by
# LIVE +2 / PUBLISHED-ONLY +1 / CONSUMED-ONLY -1 / DEAD -2, i.e. the recon saw two producers and one
# consumer that this tool does not. That residual is NOT explained. No exclusion policy, member
# classification, or scan root reproduces it, so do not treat either number as settled for those lanes:
# resolve them by hand with --lane before anyone acts on them.
RECON_2026_07_29 = {
    "lanes": 296,
    CLASS_LIVE: 159,
    CLASS_PUBLISH_ONLY: 115,
    CLASS_CONSUME_ONLY: 7,
    CLASS_DEAD: 15,
    "compile_dead_sites": 186,
}

# lane, file, expected publish line, expected consume line.
CONTROLS = (
    ("WorldChunkPhysicsBakedSignal",
     "Assets/_Project/Scripts/World/WorldChunkPhysicsBakedEvents.cs", 79, 92),
    ("TerrainChunkGeneratedSignal",
     "Assets/_Project/Scripts/TerrainChunkGeneratedEvents.cs", 45, 59),
)

SIGNALBUS_SITE = re.compile(r"\bSignalBus\s*<\s*([A-Za-z0-9_.]+)\s*>\s*\.\s*([A-Za-z0-9_]+)")
USING_ALIAS = re.compile(r"^\s*using\s+([A-Za-z0-9_]+)\s*=\s*([A-Za-z0-9_.:]+)\s*;", re.MULTILINE)
STRUCT_DECL = re.compile(r"\b(?:readonly\s+|partial\s+|unsafe\s+|ref\s+)*struct\s+([A-Za-z0-9_]+)")
OBSOLETE_ATTR = re.compile(r"\bObsolete(?:Attribute)?\s*\(")
TYPE_DECL = re.compile(r"\b(?:class|struct|interface|record)\s+([A-Za-z0-9_]+)")
MEMBER_HEADER = re.compile(r"([A-Za-z0-9_]+)\s*(?:<[^<>]*>)?\s*\(.*\)\s*(?:where\b[^{]*)?$", re.DOTALL)
MEMBER_ARROW = re.compile(r"([A-Za-z0-9_]+)\s*(?:<[^<>]*>)?\s*\([^()]*\)\s*=>")
PROPERTY_HEADER = re.compile(r"([A-Za-z0-9_]+)\s*$")
NOT_A_MEMBER = frozenset((
    "if", "for", "foreach", "while", "do", "else", "switch", "case", "catch", "try", "finally", "using",
    "lock", "fixed", "unsafe", "checked", "unchecked", "return", "get", "set", "add", "remove", "new",
    "nameof", "typeof", "sizeof", "default", "await", "yield", "throw", "when", "select", "where",
))


def blank_noncode(text):
    """Replace comment and string-literal content with spaces, preserving length and line numbers.

    Load-bearing, not hygiene. Without it a commented-out `// SignalBus<X>.TryConsumeFrame(...)` and the
    prose inside `[Obsolete("... Use SignalBus<T>.TryConsumeFrame ...", true)]` both read as live consumers.
    That single mistake is worth 93 phantom sites in one file here.
    """
    out = list(text)
    length = len(text)
    i = 0
    while i < length:
        ch = text[i]
        if ch == "/" and i + 1 < length and text[i + 1] == "/":
            end = text.find("\n", i)
            end = length if end < 0 else end
            for k in range(i, end):
                out[k] = " "
            i = end
            continue
        if ch == "/" and i + 1 < length and text[i + 1] == "*":
            end = text.find("*/", i + 2)
            end = length if end < 0 else end + 2
            for k in range(i, end):
                if out[k] != "\n":
                    out[k] = " "
            i = end
            continue
        if ch == '"' and text.startswith('"""', i):
            close = text.find('"""', i + 3)
            if close >= 0:
                for k in range(i, close + 3):
                    if out[k] != "\n":
                        out[k] = " "
                i = close + 3
                continue
        if ch == "@" and i + 1 < length and text[i + 1] == '"':
            end = i + 2
            while end < length:
                if text[end] == '"':
                    if end + 1 < length and text[end + 1] == '"':
                        end += 2
                        continue
                    end += 1
                    break
                end += 1
            for k in range(i, min(end, length)):
                if out[k] != "\n":
                    out[k] = " "
            i = end
            continue
        if ch in '"\'':
            quote = ch
            end = i + 1
            while end < length:
                if text[end] == "\\":
                    end += 2
                    continue
                if text[end] == quote or text[end] == "\n":
                    end += 1
                    break
                end += 1
            for k in range(i, min(end, length)):
                if out[k] != "\n":
                    out[k] = " "
            i = end
            continue
        i += 1
    return "".join(out)


def match_bracket(text, pos, opener, closer):
    """Index just past the closer matching the opener at pos, or -1 when unbalanced."""
    depth = 0
    length = len(text)
    while pos < length:
        if text[pos] == opener:
            depth += 1
        elif text[pos] == closer:
            depth -= 1
            if depth == 0:
                return pos + 1
        pos += 1
    return -1


def split_top_level(args):
    """Split an attribute argument list on commas at depth 0."""
    parts = []
    depth = 0
    start = 0
    for i, ch in enumerate(args):
        if ch in "([{<":
            depth += 1
        elif ch in ")]}>":
            depth -= 1
        elif ch == "," and depth == 0:
            parts.append(args[start:i])
            start = i + 1
    parts.append(args[start:])
    return parts


def skip_attribute_groups(text, pos):
    """Advance past whitespace and any further [Attribute] groups after an attribute's closing bracket."""
    length = len(text)
    while pos < length:
        while pos < length and text[pos].isspace():
            pos += 1
        if pos < length and text[pos] == "[":
            end = match_bracket(text, pos, "[", "]")
            if end < 0:
                return pos
            pos = end
            continue
        return pos
    return pos


def member_span_after(text, pos):
    """(start, end) of the member declared at pos: block body, expression body, or bare declaration."""
    length = len(text)
    depth = 0
    while pos < length:
        ch = text[pos]
        if ch in "([":
            depth += 1
        elif ch in ")]":
            depth = max(0, depth - 1)
        elif depth == 0:
            if ch == "{":
                end = match_bracket(text, pos, "{", "}")
                return None if end < 0 else (pos, end)
            if ch == ";":
                return (pos, pos + 1)
            if ch == "=" and pos + 1 < length and text[pos + 1] == ">":
                scan = pos + 2
                inner = 0
                while scan < length:
                    if text[scan] in "([{":
                        inner += 1
                    elif text[scan] in ")]}":
                        inner -= 1
                    elif text[scan] == ";" and inner <= 0:
                        return (pos, scan + 1)
                    scan += 1
                return None
        pos += 1
    return None


def compile_dead_spans(code):
    """Spans of members marked [Obsolete(..., true)]. A call to one is CS0619, so the body is not code."""
    spans = []
    failures = 0
    for match in OBSOLETE_ATTR.finditer(code):
        open_paren = match.end() - 1
        args_end = match_bracket(code, open_paren, "(", ")")
        if args_end < 0:
            failures += 1
            continue
        args = split_top_level(code[open_paren + 1:args_end - 1])
        if len(args) < 2:
            continue  # [Obsolete] / [Obsolete("msg")] - a warning only, so the call site is REAL
        if not any(re.fullmatch(r"\s*(?:error\s*:\s*)?true\s*", arg) for arg in args[1:]):
            continue  # error:false - still compiles, still a real call site
        bracket_end = code.find("]", args_end - 1)
        if bracket_end < 0:
            failures += 1
            continue
        span = member_span_after(code, skip_attribute_groups(code, bracket_end + 1))
        if span is None:
            failures += 1
            continue
        spans.append((match.start(), span[1]))

    spans.sort()
    merged = []
    for start, end in spans:
        if merged and start <= merged[-1][1]:
            merged[-1] = (merged[-1][0], max(merged[-1][1], end))
        else:
            merged.append((start, end))
    return merged, failures


def brace_scopes(code):
    """(body_start, body_end, header) for every brace body, header = the declaration text preceding it."""
    scopes = []
    stack = []
    boundary = 0
    for i, ch in enumerate(code):
        if ch == "{":
            stack.append((i, code[boundary:i]))
            boundary = i + 1
        elif ch == "}":
            if stack:
                start, header = stack.pop()
                scopes.append((start, i, header))
            boundary = i + 1
        elif ch == ";":
            boundary = i + 1
    return scopes


def header_member_name(header):
    """Member name from a declaration header, or None when the header is not a member declaration."""
    stripped = header.strip()
    if not stripped or stripped.endswith(("=", "=>", ",")):
        return None
    if TYPE_DECL.search(stripped):
        return None
    match = MEMBER_HEADER.search(stripped)
    if match and match.group(1) not in NOT_A_MEMBER:
        return match.group(1)
    if "(" in stripped or "=" in stripped:
        return None
    match = PROPERTY_HEADER.search(stripped)
    if match and match.group(1) not in NOT_A_MEMBER:
        return match.group(1)  # property or indexer body
    return None


def attribute_site(code, offset, scopes, boundaries):
    """(owner_type, owner_member) for the site at offset. Best-effort, for triage only."""
    owner_type = None
    owner_member = None
    best_type_start = -1
    best_member_start = -1
    for start, end, header in scopes:
        if not (start < offset < end):
            continue
        type_match = TYPE_DECL.search(header)
        if type_match and start > best_type_start:
            best_type_start, owner_type = start, type_match.group(1)
        name = header_member_name(header)
        if name and start > best_member_start:
            best_member_start, owner_member = start, name

    if owner_member is None:
        # Expression-bodied member: no brace body, so the header is in the current statement fragment.
        index = bisect.bisect_left(boundaries, offset) - 1
        fragment = code[boundaries[index] + 1:offset] if index >= 0 else code[:offset]
        arrow = None
        for arrow in MEMBER_ARROW.finditer(fragment):
            pass
        if arrow and arrow.group(1) not in NOT_A_MEMBER:
            owner_member = arrow.group(1)
    return owner_type, owner_member


class Site(object):
    __slots__ = ("path", "line", "lane", "raw_lane", "member", "kind", "owner_type", "owner_member", "dead")

    def __init__(self, path, line, lane, raw_lane, member, kind, owner_type, owner_member, dead):
        self.path = path
        self.line = line
        self.lane = lane
        self.raw_lane = raw_lane
        self.member = member
        self.kind = kind
        self.owner_type = owner_type
        self.owner_member = owner_member
        self.dead = dead

    @property
    def owner(self):
        return "%s.%s" % (self.owner_type or "?", self.owner_member or "?")

    @property
    def where(self):
        return "%s:%d" % (self.path, self.line)


class FileScan(object):
    __slots__ = ("sites", "declared", "aliases", "parse_failures", "phantoms")

    def __init__(self):
        self.sites = []
        self.declared = ()
        self.aliases = {}
        self.parse_failures = 0
        self.phantoms = 0


def classify_member(member):
    if member in PUBLISH_MEMBERS:
        return "PUBLISH"
    if member in CONSUME_MEMBERS:
        return "CONSUME"
    if member in LIFECYCLE_MEMBERS:
        return "LIFECYCLE"
    if member in TELEMETRY_MEMBERS:
        return "TELEMETRY"
    return "UNKNOWN"


def scan_file(path):
    result = FileScan()
    try:
        raw = open(path, encoding="utf-8", errors="replace").read()
    except OSError:
        return result

    if "SignalBus" not in raw:
        result.declared = STRUCT_DECL.findall(raw)  # lane payloads are declared in files that never use them
        return result

    code = blank_noncode(raw)
    result.declared = STRUCT_DECL.findall(code)
    result.phantoms = (len(SIGNALBUS_SITE.findall(raw)) - len(SIGNALBUS_SITE.findall(code)))
    for match in USING_ALIAS.finditer(code):
        result.aliases[match.group(1)] = match.group(2).replace("global::", "").split(".")[-1]

    dead_spans, result.parse_failures = compile_dead_spans(code)
    dead_starts = [span[0] for span in dead_spans]
    scopes = brace_scopes(code)
    boundaries = [i for i, ch in enumerate(code) if ch in ";{}"]
    newlines = [i for i, ch in enumerate(code) if ch == "\n"]
    rel = path.replace(os.sep, "/")

    for match in SIGNALBUS_SITE.finditer(code):
        offset = match.start()
        index = bisect.bisect_right(dead_starts, offset) - 1
        dead = index >= 0 and offset < dead_spans[index][1]
        raw_lane = match.group(1).split(".")[-1]
        owner_type, owner_member = attribute_site(code, offset, scopes, boundaries)
        result.sites.append(Site(
            rel, bisect.bisect_right(newlines, offset) + 1,
            result.aliases.get(raw_lane, raw_lane), raw_lane, match.group(2),
            classify_member(match.group(2)), owner_type, owner_member, dead))
    return result


def scan_tree(root, skip_prefix=None):
    sites = []
    declared_structs = set()
    aliases = {}
    parse_failures = {}
    phantoms = {}
    files = 0
    for directory, subdirs, filenames in os.walk(root):
        subdirs[:] = [name for name in subdirs if name not in ("Library", "Temp", "obj", ".git")]
        posix_dir = directory.replace(os.sep, "/")
        if skip_prefix and posix_dir.startswith(skip_prefix):
            continue
        for filename in sorted(filenames):
            if not filename.endswith(".cs"):
                continue
            files += 1
            path = os.path.join(directory, filename)
            scan = scan_file(path)
            sites += scan.sites
            declared_structs.update(scan.declared)
            rel = path.replace(os.sep, "/")
            if scan.parse_failures:
                parse_failures[rel] = scan.parse_failures
            if scan.phantoms:
                phantoms[rel] = scan.phantoms
            for name, target in scan.aliases.items():
                aliases.setdefault(name, (target, rel))
    return sites, declared_structs, aliases, parse_failures, phantoms, files


def classify(sites, declared_structs):
    """Group sites per lane. A lane exists when its type argument resolves to a struct declared in the tree."""
    lanes = {}
    for site in sites:
        if site.lane not in declared_structs:
            continue  # a generic parameter (T, TSignal) or a type declared outside the scan
        record = lanes.setdefault(site.lane, {"PUBLISH": [], "CONSUME": [], "OTHER": [], "dead": []})
        if site.dead:
            record["dead"].append(site)
        elif site.kind in ("PUBLISH", "CONSUME"):
            record[site.kind].append(site)
        else:
            record["OTHER"].append(site)

    verdicts = {}
    for lane, record in lanes.items():
        if record["PUBLISH"] and record["CONSUME"]:
            verdicts[lane] = CLASS_LIVE
        elif record["PUBLISH"]:
            verdicts[lane] = CLASS_PUBLISH_ONLY
        elif record["CONSUME"]:
            verdicts[lane] = CLASS_CONSUME_ONLY
        else:
            verdicts[lane] = CLASS_DEAD
    return lanes, verdicts


def print_controls(lanes):
    print("POSITIVE CONTROLS - two lanes known to be fully wired, both through an owner facade")
    passed = 0
    for lane, path, publish_line, consume_line in CONTROLS:
        record = lanes.get(lane)
        publish_ok = bool(record) and any(
            site.path == path and site.line == publish_line for site in record["PUBLISH"])
        consume_ok = bool(record) and any(
            site.path == path and site.line == consume_line for site in record["CONSUME"])
        passed += 1 if publish_ok and consume_ok else 0
        print("  %-30s publish %s:%-4d %-8s consume %s:%-4d %s"
              % (lane, os.path.basename(path), publish_line, "FOUND" if publish_ok else "MISSING",
                 os.path.basename(path), consume_line, "FOUND" if consume_ok else "MISSING"))
    print("  CONTROLS: %d/%d PASS" % (passed, len(CONTROLS)))
    if passed != len(CONTROLS):
        print("  *** THE METHOD IS BROKEN. Every negative result below is MEANINGLESS: a lane reported with")
        print("  *** no consumer may simply be a lane this run failed to parse. Fix the tool, not the code.")
    print()
    return passed == len(CONTROLS)


def print_filters(sites, aliases, declared_structs, parse_failures, phantoms):
    dead_sites = [site for site in sites if site.dead]
    per_file = {}
    for site in dead_sites:
        per_file.setdefault(site.path, []).append(site)

    print("FILTER 1 - PHANTOMS: SignalBus<X>.Member inside a comment or string literal is NOT a call site")
    print("  textual occurrences discarded: %d" % sum(phantoms.values()))
    for path, count in sorted(phantoms.items(), key=lambda kv: (-kv[1], kv[0]))[:5]:
        print("    %-62s %4d" % (path, count))
    print("    (a text-only count reports these as real wiring; most are [Obsolete] message prose)")
    print()

    print("FILTER 2 - COMPILE-DEAD: [Obsolete(..., true)] member bodies, calling one is CS0619")
    print("  real call sites excluded: %d, in %d file(s)" % (len(dead_sites), len(per_file)))
    for path, group in sorted(per_file.items(), key=lambda kv: (-len(kv[1]), kv[0])):
        kinds = {}
        for site in group:
            kinds[site.kind] = kinds.get(site.kind, 0) + 1
        print("    %-58s %4d  (%s)" % (path, len(group),
                                       ", ".join("%s %d" % kv for kv in sorted(kinds.items()))))
    if parse_failures:
        print("  *** %d [Obsolete(...,true)] attribute(s) whose member body could not be delimited. Their"
              % sum(parse_failures.values()))
        print("  *** call sites are counted as LIVE, so an orphan may be HIDDEN. Fix these first: %s"
              % ", ".join(sorted(parse_failures)))
    print()

    print("FILTER 3 - USING-ALIASES: SignalBus<alias> is the aliased lane, not a lane of its own")
    used = sorted({site.raw_lane for site in sites if site.raw_lane != site.lane})
    for name in used:
        target, path = aliases.get(name, ("?", "?"))
        print("    %-38s -> %-34s %s" % (name, target, path))
    if not used:
        print("    none in use")
    print()


def print_summary(lanes, verdicts, sites, files):
    counts = {name: 0 for name in CLASS_ORDER}
    for verdict in verdicts.values():
        counts[verdict] += 1
    live_sites = [site for site in sites if not site.dead]
    unknown = [site for site in sites if site.kind == "UNKNOWN"]

    print("SUMMARY")
    print("  .cs files scanned                    %d" % files)
    print("  real SignalBus<T> call sites         %d" % len(sites))
    print("    compile-dead, excluded             %d" % (len(sites) - len(live_sites)))
    print("    PUBLISH                            %d"
          % len([s for s in live_sites if s.kind == "PUBLISH"]))
    print("    CONSUME                            %d"
          % len([s for s in live_sites if s.kind == "CONSUME"]))
    print("    lifecycle/telemetry - NEITHER      %d"
          % len([s for s in live_sites if s.kind in ("LIFECYCLE", "TELEMETRY")]))
    print("    UNKNOWN member, NOT classified     %d%s"
          % (len(unknown), "" if not unknown else "   <-- see below, the counts are WRONG until fixed"))
    print("  distinct lanes                       %d" % len(verdicts))
    for name in CLASS_ORDER:
        print("    %-34s %d" % (name, counts[name]))
    if unknown:
        print()
        print("  *** SignalBus members this tool cannot classify. Until they are added to PUBLISH_MEMBERS /")
        print("  *** CONSUME_MEMBERS / LIFECYCLE_MEMBERS / TELEMETRY_MEMBERS every count above is suspect:")
        for member in sorted({site.member for site in unknown}):
            example = next(site for site in unknown if site.member == member)
            print("        %-32s e.g. %s" % (member, example.where))
    print()
    counts["lanes"] = len(verdicts)
    counts["compile_dead_sites"] = len(sites) - len(live_sites)
    return counts


def print_drift(counts):
    keys = ("lanes",) + CLASS_ORDER + ("compile_dead_sites",)
    print("REGRESSION GUARD - vs this tool's own 2026-07-29 baseline")
    drift = False
    for key in keys:
        delta = counts[key] - BASELINE[key]
        drift = drift or bool(delta)
        print("    %-34s now %4d   baseline %4d   %s"
              % (key, counts[key], BASELINE[key], "same" if delta == 0 else "%+d  DRIFT" % delta))
    if drift:
        print("  A lane moving PUBLISHED-NEVER-CONSUMED -> LIVE is a fix. The reverse is a REGRESSION:")
        print("  somebody deleted the last reader of a lane and nothing anywhere failed. Diff the per-class")
        print("  lists below against the previous run to see which lane moved.")
    print()

    print("CROSS-CHECK - vs the 2026-07-29 hand recon (see RECON_2026_07_29 in this file)")
    for key in keys:
        delta = counts[key] - RECON_2026_07_29[key]
        print("    %-34s here %4d   recon %4d   %s"
              % (key, counts[key], RECON_2026_07_29[key], "agree" if delta == 0 else "%+d" % delta))
    print("  The 296-lane universe is reproduced exactly. The recon's %d compile-dead sites is a TEXT count:"
          % RECON_2026_07_29["compile_dead_sites"])
    print("  93 of them are inside [Obsolete(\"... SignalBus<T>.TryConsumeFrame ...\")] message strings, which")
    print("  FILTER 1 above counts and discards. The residual class differences are NOT explained - treat")
    print("  those lanes as unresolved and check them with --lane before anyone acts on them.")
    print()
    return drift


def print_lane_lists(lanes, verdicts):
    for name in CLASS_ORDER:
        members = sorted(lane for lane, verdict in verdicts.items() if verdict == name)
        print("%s  (%d)" % (name, len(members)))
        for lane in members:
            record = lanes[lane]
            notes = []
            if name != CLASS_LIVE:
                if record["PUBLISH"] and all(
                        site.member in WRITER_HANDLE_MEMBERS for site in record["PUBLISH"]):
                    notes.append("producer is a JOB WRITER HANDLE only, the enqueue is unscannable")
                if record["dead"]:
                    notes.append("%d compile-dead site(s) excluded" % len(record["dead"]))
                if not record["PUBLISH"] and not record["CONSUME"] and record["OTHER"]:
                    notes.append("configured/flushed by %s, so it registers and drains to NOBODY"
                                 % record["OTHER"][0].owner)
            print("  %-52s %s" % (lane, "; ".join(notes)))
            bucket = "PUBLISH" if name == CLASS_PUBLISH_ONLY else "CONSUME" if name == CLASS_CONSUME_ONLY \
                else None
            if bucket:
                owners = sorted({site.owner for site in record[bucket]})
                print("      %s: %s%s"
                      % ("writers" if bucket == "PUBLISH" else "readers", ", ".join(owners[:4]),
                         " (+%d more)" % (len(owners) - 4) if len(owners) > 4 else ""))
        print()


def print_one_lane(lanes, verdicts, wanted):
    matches = sorted(lane for lane in lanes if wanted.lower() in lane.lower())
    if not matches:
        print("no lane matching %r." % wanted)
        print("A lane is the SignalBus<T> TYPE ARGUMENT - not the owner class, not the BufferID, and not")
        print("the internal DTO. Searching the DTO name has already nearly produced a false dead-code")
        print("report on a fully wired pipeline here.")
        return 1
    for lane in matches:
        record = lanes[lane]
        print("%s  ->  %s" % (lane, verdicts[lane]))
        for bucket in ("PUBLISH", "CONSUME", "OTHER", "dead"):
            label = "COMPILE-DEAD, excluded" if bucket == "dead" else bucket
            print("  %s (%d)" % (label, len(record[bucket])))
            for site in sorted(record[bucket], key=lambda s: (s.path, s.line)):
                print("      %-58s %-24s %s" % (site.where, site.member, site.owner))
        print()
    return 0


def print_outside(lanes, declared_structs):
    """Answer 'maybe the reader lives elsewhere' with data instead of a caveat."""
    scan_prefix = SCAN_ROOT.replace(os.sep, "/")
    sites, _, _, _, phantoms, files = scan_tree(OUTSIDE_ROOT, skip_prefix=scan_prefix)
    print("OUTSIDE THE SCAN ROOT - %d .cs files under %s/ excluding %s/"
          % (files, OUTSIDE_ROOT, scan_prefix))
    print("  textual occurrences discarded as comment/string: %d" % sum(phantoms.values()))
    real = [site for site in sites if site.lane in declared_structs and not site.dead]
    print("  real call sites: %d" % len(real))
    for site in sorted(real, key=lambda s: (s.path, s.line)):
        verdict = "lane not in scan root" if site.lane not in lanes else ""
        print("    %-58s %-22s %-20s %s" % (site.where, site.lane, site.member + " " + site.kind, verdict))
    rescued = sorted({site.lane for site in real
                      if site.kind == "PUBLISH" and site.lane in lanes and not lanes[site.lane]["PUBLISH"]})
    rescued_readers = sorted({site.lane for site in real
                              if site.kind == "CONSUME" and site.lane in lanes
                              and not lanes[site.lane]["CONSUME"]})
    print("  lanes whose ONLY producer is out here (a test-only producer is NOT gameplay wiring): %s"
          % (", ".join(rescued) or "none"))
    print("  lanes whose ONLY consumer is out here: %s" % (", ".join(rescued_readers) or "none"))
    print()


def main():
    args = sys.argv[1:]
    if any(arg in ("-h", "--help") for arg in args):
        print(__doc__)
        raise SystemExit(2)
    if not os.path.isdir(SCAN_ROOT):
        print("run me from the repo root: %s not found" % SCAN_ROOT)
        raise SystemExit(2)

    sites, declared_structs, aliases, parse_failures, phantoms, files = scan_tree(SCAN_ROOT)
    lanes, verdicts = classify(sites, declared_structs)

    print("SIGNAL LANE WIRING AUDIT - static, because SignalBus consumption is PULL-based and registers no")
    print("subscriber. A lane with no reader drains to nobody every frame and NOTHING LOGS IT.")
    print("scan root: %s" % SCAN_ROOT.replace(os.sep, "/"))
    print()
    controls_ok = print_controls(lanes)

    if args and args[0] == "--lane":
        if len(args) < 2:
            print("--lane needs a lane name")
            raise SystemExit(2)
        code = print_one_lane(lanes, verdicts, args[1])
        raise SystemExit(code if controls_ok else 1)

    if args and args[0] == "--outside":
        print_outside(lanes, declared_structs)
        raise SystemExit(0 if controls_ok else 1)

    print_filters(sites, aliases, declared_structs, parse_failures, phantoms)
    counts = print_summary(lanes, verdicts, sites, files)
    drift = print_drift(counts)

    if args and args[0] == "--check":
        raise SystemExit(1 if drift or not controls_ok else 0)

    print_lane_lists(lanes, verdicts)
    print("A zero is a QUESTION, not a verdict. Telemetry lanes are legitimately write-only, job producers")
    print("hand out a writer handle this scan cannot follow, and a producer may live outside the scan root")
    print("(run --outside). Read the header of this file before you delete anything.")
    raise SystemExit(0 if controls_ok else 1)


if __name__ == "__main__":
    main()

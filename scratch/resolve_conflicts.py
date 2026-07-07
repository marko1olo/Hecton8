import subprocess
import sys
import os
import re
import json

def run_git(args, cwd, check=False):
    try:
        res = subprocess.run(args, cwd=cwd, capture_output=True, check=check)
        out = res.stdout.decode("utf-8", errors="replace").strip()
        err = res.stderr.decode("utf-8", errors="replace").strip()
        return out, res.returncode
    except Exception as e:
        return str(e), -1

def is_test_file(path):
    b = os.path.basename(path).lower()
    return (b.endswith("tests.cs") or b.endswith("test.cs") or
            b.endswith("_test.py") or b.startswith("test_") or
            "/tests/" in path or "/test/" in path or "/__tests__/" in path)

def is_lock_file(path):
    b = os.path.basename(path).lower()
    return b in ("package-lock.json", "yarn.lock", "pnpm-lock.yaml")

def is_txt_file(path):
    return path.endswith(".txt") or path.endswith(".md") or path.endswith(".json")

def resolve_conflict_file(filepath, repo_dir):
    """
    Resolve a single conflicted file.
    Returns: "union", "ours", "theirs", or "manual"
    """
    if is_lock_file(filepath):
        run_git(["git", "checkout", "--ours", filepath], repo_dir)
        run_git(["git", "add", filepath], repo_dir)
        return "ours"
    
    if is_txt_file(filepath):
        run_git(["git", "checkout", "--ours", filepath], repo_dir)
        run_git(["git", "add", filepath], repo_dir)
        return "ours"
    
    if is_test_file(filepath):
        full_path = os.path.join(repo_dir, filepath)
        try:
            with open(full_path, encoding="utf-8", errors="replace") as f:
                content = f.read()
        except Exception:
            run_git(["git", "checkout", "--ours", filepath], repo_dir)
            run_git(["git", "add", filepath], repo_dir)
            return "ours"
        
        resolved = resolve_union(content)
        if resolved is not None:
            with open(full_path, "w", encoding="utf-8") as f:
                f.write(resolved)
            run_git(["git", "add", filepath], repo_dir)
            return "union"
        else:
            run_git(["git", "checkout", "--ours", filepath], repo_dir)
            run_git(["git", "add", filepath], repo_dir)
            return "ours"
    
    # Production code: keep HEAD (ours) to preserve stability
    run_git(["git", "checkout", "--ours", filepath], repo_dir)
    run_git(["git", "add", filepath], repo_dir)
    return "ours"

def resolve_union(content):
    """
    Parse git conflict markers and produce union of both sides.
    """
    if "<<<<<<< HEAD" not in content:
        return content
    
    lines = content.split("\n")
    result = []
    i = 0
    
    while i < len(lines):
        line = lines[i]
        if line.startswith("<<<<<<< "):
            ours = []
            theirs = []
            i += 1
            
            while i < len(lines) and not lines[i].startswith("=======") and not lines[i].startswith(">>>>>>> "):
                ours.append(lines[i])
                i += 1
            
            if i < len(lines) and lines[i].startswith("======="):
                i += 1
            
            while i < len(lines) and not lines[i].startswith(">>>>>>> "):
                theirs.append(lines[i])
                i += 1
            
            if i < len(lines):
                i += 1
            
            # Union: include ours first, then theirs (avoiding exact duplicate lines)
            result.extend(ours)
            ours_set = set(l.strip() for l in ours if l.strip())
            for tline in theirs:
                if tline.strip() not in ours_set or not tline.strip():
                    result.append(tline)
        else:
            result.append(line)
            i += 1
    
    return "\n".join(result)

def merge_with_resolution(repo_dir, branch):
    """Attempt to merge a branch and auto-resolve conflicts."""
    out, code = run_git(
        ["git", "merge", "--no-ff", "-m", f"Merge {branch} (conflict-resolved)", branch],
        repo_dir
    )
    
    if code == 0:
        return "merged_clean", []
    
    conflict_out, _ = run_git(
        ["git", "diff", "--name-only", "--diff-filter=U"],
        repo_dir
    )
    conflict_files = [f.strip() for f in conflict_out.split("\n") if f.strip()]
    
    if not conflict_files:
        run_git(["git", "merge", "--abort"], repo_dir)
        return "aborted", []
    
    resolution_log = []
    manual_needed = []
    
    for filepath in conflict_files:
        strategy = resolve_conflict_file(filepath, repo_dir)
        resolution_log.append(f"{filepath}: {strategy}")
        if strategy == "manual":
            manual_needed.append(filepath)
    
    if manual_needed:
        run_git(["git", "merge", "--abort"], repo_dir)
        return "aborted_manual", manual_needed
    
    commit_msg = (
        f"Merge {branch}\n\n"
        f"Auto-resolved conflicts:\n" +
        "\n".join(f"  - {r}" for r in resolution_log)
    )
    commit_out, commit_code = run_git(
        ["git", "commit", "--no-edit", "-m", commit_msg],
        repo_dir
    )
    
    if commit_code == 0:
        return "merged_resolved", resolution_log
    else:
        run_git(["git", "merge", "--abort"], repo_dir)
        return "commit_failed", [commit_out]

def main():
    if len(sys.argv) < 3:
        print("Usage: python resolve_conflicts.py <repo_dir> <pr_merge_report.json>")
        sys.exit(1)
    
    repo_dir = os.path.abspath(sys.argv[1])
    report_path = sys.argv[2]
    
    with open(report_path, encoding="utf-8") as f:
        report = json.load(f)
    
    conflict_branches = [item["branch"] for item in report.get("conflict_branches", [])]
    print(f"Resolving {len(conflict_branches)} conflicted branches in {repo_dir}")
    
    results = {
        "merged_clean": [],
        "merged_resolved": [],
        "aborted_manual": [],
        "aborted": [],
        "commit_failed": []
    }
    
    for branch in conflict_branches:
        print(f"\nRetrying: {branch}")
        status, info = merge_with_resolution(repo_dir, branch)
        results[status].append({"branch": branch, "info": info})
        print(f"  -> {status}" + (f" ({', '.join(info[:2])})" if info else ""))
    
    print(f"\n{'='*60}")
    print(f"CONFLICT RESOLUTION RESULTS")
    print(f"{'='*60}")
    print(f"  Merged clean:        {len(results['merged_clean'])}")
    print(f"  Merged (resolved):   {len(results['merged_resolved'])}")
    print(f"  Aborted (manual):    {len(results['aborted_manual'])}")
    print(f"  Aborted (other):     {len(results['aborted'])}")
    print(f"  Commit failed:       {len(results['commit_failed'])}")
    
    res_path = os.path.join(repo_dir, "pr_conflict_resolution.json")
    with open(res_path, "w", encoding="utf-8") as f:
        json.dump(results, f, indent=2, ensure_ascii=False)
    print(f"\nResolution report: {res_path}")

if __name__ == "__main__":
    main()

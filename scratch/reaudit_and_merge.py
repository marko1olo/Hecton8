import subprocess
import sys
import os
import re
import json

def run_git(args, cwd, check=True):
    try:
        res = subprocess.run(args, cwd=cwd, capture_output=True, check=check)
        return res.stdout.decode("utf-8", errors="replace").strip(), res.returncode
    except subprocess.CalledProcessError as e:
        return e.stderr.decode("utf-8", errors="replace").strip(), e.returncode
    except Exception as e:
        return str(e), -1

def get_diff_files(repo_dir, branch):
    out, _ = run_git(["git", "diff", "--name-only", f"HEAD...{branch}"], repo_dir)
    return [f.strip() for f in out.split("\n") if f.strip()]

def get_diff_content(repo_dir, branch):
    out, _ = run_git(["git", "diff", f"HEAD...{branch}"], repo_dir)
    return out

def get_diff_stat(repo_dir, branch):
    out, _ = run_git(["git", "diff", "--shortstat", f"HEAD...{branch}"], repo_dir)
    m = re.search(r'(\d+) insertions?\(\+\)', out)
    ins = int(m.group(1)) if m else 0
    m = re.search(r'(\d+) deletions?\(-\)', out)
    dels = int(m.group(1)) if m else 0
    return ins + dels

def is_test_file(filepath):
    basename = os.path.basename(filepath).lower()
    ext = os.path.splitext(filepath)[1].lower()
    return (
        "test" in basename or
        "spec" in basename or
        "mock" in basename or
        filepath.startswith("tests/") or
        "/test/" in filepath or
        "/tests/" in filepath or
        basename.startswith("test_")
    )

def reaudit_branch(repo_dir, branch, original):
    files = get_diff_files(repo_dir, branch)
    total_changes = get_diff_stat(repo_dir, branch)
    
    if total_changes == 0 or not files:
        return "REJECT", "Empty branch - no diff against main."
    
    if total_changes > 500:
        return "MANUAL_REVIEW", f"Large diff ({total_changes} lines) - requires manual review."
    
    diff = get_diff_content(repo_dir, branch)
    reason = original.get("reason", "")
    
    # Correcting "Memory allocation in loop" on test files
    if "Memory allocation" in reason or "Performance" in reason:
        all_tests = all(is_test_file(f) for f in files)
        if all_tests:
            return "ACCEPT", f"Test file improvements/fixtures - loops and allocations are acceptable in tests."
        
    return original.get("verdict", "REJECT"), reason

def merge_branch(repo_dir, branch, dry_run=False):
    cmd = ["git", "merge", "--no-ff", "-m", f"Merge {branch}", branch]
    if dry_run:
        print(f"  [DRY-RUN] Would merge: {branch}")
        return True, []
    
    out, code = run_git(cmd, repo_dir, check=False)
    if code == 0:
        return True, []
    else:
        # Get conflicts
        conflict_out, _ = run_git(["git", "diff", "--name-only", "--diff-filter=U"], repo_dir, check=False)
        conflict_files = [f.strip() for f in conflict_out.split("\n") if f.strip()]
        # Abort merge
        run_git(["git", "merge", "--abort"], repo_dir, check=False)
        return False, conflict_files

def main():
    if len(sys.argv) < 2:
        print("Usage: python reaudit_and_merge.py <repo_dir> [--dry-run]")
        sys.exit(1)
    
    repo_dir = os.path.abspath(sys.argv[1])
    dry_run = "--dry-run" in sys.argv
    
    report_path = os.path.join(repo_dir, "pr_audit_report.json")
    if not os.path.exists(report_path):
        print(f"No audit report found at {report_path}")
        sys.exit(1)
    
    with open(report_path, encoding="utf-8") as f:
        original_report = json.load(f)
    
    print(f"\n{'='*60}")
    print(f"RE-AUDITING {repo_dir}")
    print(f"{'='*60}")
    
    corrected = {}
    for branch, info in original_report.items():
        if info["verdict"] == "REJECT":
            new_verdict, new_reason = reaudit_branch(repo_dir, branch, info)
            if new_verdict != info["verdict"]:
                print(f"  CORRECTED: {branch}")
                print(f"    WAS: REJECT ({info['reason'][:60]}...)")
                print(f"    NOW: {new_verdict} ({new_reason})")
            corrected[branch] = {**info, "verdict": new_verdict, "reason": new_reason, "corrected": new_verdict != info["verdict"]}
        else:
            corrected[branch] = info
    
    corrected_path = os.path.join(repo_dir, "pr_audit_corrected.json")
    with open(corrected_path, "w", encoding="utf-8") as f:
        json.dump(corrected, f, indent=2, ensure_ascii=False)
    
    accepts = {k:v for k,v in corrected.items() if v["verdict"] == "ACCEPT"}
    rejects = {k:v for k,v in corrected.items() if v["verdict"] == "REJECT"}
    manuals = {k:v for k,v in corrected.items() if v["verdict"] == "MANUAL_REVIEW"}
    
    print(f"\nCorrected totals: ACCEPT={len(accepts)} | REJECT={len(rejects)} | MANUAL_REVIEW={len(manuals)}")
    
    print(f"\n{'='*60}")
    print(f"AUTO-MERGING {len(accepts)} ACCEPT branches")
    if dry_run:
        print("(DRY RUN MODE)")
    print(f"{'='*60}")
    
    merged = []
    failed = []
    for branch in accepts:
        print(f"\nMerging: {branch}")
        success, conflicts = merge_branch(repo_dir, branch, dry_run=dry_run)
        if success:
            print(f"  OK")
            merged.append(branch)
        else:
            print(f"  CONFLICT in: {conflicts}")
            failed.append({"branch": branch, "conflicts": conflicts})
    
    final_report = {
        "summary": {
            "total": len(corrected),
            "merged": len(merged),
            "rejected": len(rejects),
            "manual_review": len(manuals),
            "conflicts": len(failed)
        },
        "merged_branches": merged,
        "rejected_branches": {k:v["reason"] for k,v in rejects.items()},
        "manual_review_branches": {k:v["reason"] for k,v in manuals.items()},
        "conflict_branches": failed
    }
    
    final_path = os.path.join(repo_dir, "pr_merge_report.json")
    with open(final_path, "w", encoding="utf-8") as f:
        json.dump(final_report, f, indent=2, ensure_ascii=False)
    
    print(f"\n{'='*60}")
    print(f"DONE. Final report: {final_path}")
    print(f"  Merged: {len(merged)}")
    print(f"  Rejected (legit): {len(rejects)}")
    print(f"  Manual review needed: {len(manuals)}")
    print(f"  Merge conflicts: {len(failed)}")
    print(f"{'='*60}")

if __name__ == "__main__":
    main()

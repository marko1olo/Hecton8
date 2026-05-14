# COMPUTE VALIDATION FORENSICS

Status: AUDIT COMPLETE
Snapshot: 2026-05-15T03:34-03:39+04:00
Source: `.codex/state_5.sqlite` + top-30 rollout JSONL files
Method: read-only parse of validation-relevant tool calls and their outputs

## Boundary

This file does not compile the project. It audits historical validation attempts inside `.codex` rollouts.

Evidence strength:
- Strong: command/tool call counts, exit-code counts, visible `error CS####` strings.
- Medium: build/test/Unity command buckets from command text.
- Weak: `test` bucket, because PowerShell `Test-Path` and grep text can include `test` without being a test suite.
- Not proven: current build status, final post-thread correctness, H-Phi movement.

One broad validation parse timed out before this file. The completed pass below deliberately inspected only validation-relevant calls and outputs.

## Aggregate

| Metric | Value |
|---|---:|
| Live SQLite all-thread tokens | 44,119,468,183 |
| Top-30 tokens | 9,492,793,103 |
| Top-30 share | 21.516% |
| Top-30 `apply_patch` calls | 14,015 |
| `git diff --check` command hits | 1,695 |
| `dotnet` / `msbuild` command hits | 2,937 |
| Unity-related shell command hits | 5,782 |
| Weak test keyword command hits | 2,985 |
| Git diff/status command hits | 4,467 |
| Unity tool calls | 7,649 |
| Validation outputs inspected | 17,885 |
| Exit code 0 outputs | 15,510 |
| Non-zero exit outputs | 2,374 |
| No explicit exit code outputs | 1 |
| Non-zero validation output rate | 13.274% |
| Outputs containing `error CS####` | 1,297 |
| Outputs containing compile-fail signals | 935 |
| Outputs containing build-success signals | 746 |
| Outputs containing test-success signals | 0 |
| Outputs containing test-fail signals | 327 |
| LF-to-CRLF warning outputs | 5,076 |
| Exception/traceback-like outputs | 998 |

Hard read: these expensive threads did try to validate. They also carried a large amount of failed validation. The audit cannot honestly call the top-30 burn clean.

## Validation Rows

Column format:
- `Checks`: `diffcheck/dotnet/unityShell/weakTest/git/unityTools`
- `Exit`: `zero/nonzero/noExit`
- `Signals`: `csError/compileFail/buildOk/testOk/testFail/exception`

| # | Thread | Tokens | Patch | Checks | Outputs | Exit | Signals | Title hint |
|---:|---|---:|---:|---|---:|---|---|---|
| 1 | `019e1859-0e01-77b2-a8c6-b5586ccc5c8c` | 518,697,166 | 399 | 32/283/535/113/77/402 | 1,040 | 823/216/1 | 165/28/6/0/24/92 | console/UI |
| 2 | `019d6329-de82-74e2-83ca-450539a61cec` | 490,407,394 | 561 | 2/0/173/117/47/1,382 | 340 | 309/31/0 | 14/0/0/0/20/33 | master plan / flora |
| 3 | `019dde7c-df90-7791-b4b4-d49c8450a9be` | 468,267,072 | 889 | 98/201/338/102/125/177 | 864 | 740/124/0 | 64/66/106/0/5/35 | split monoliths |
| 4 | `019d67a6-6823-7b82-94f9-a3167b8e0286` | 429,064,399 | 733 | 120/2/183/302/72/896 | 677 | 649/28/0 | 16/2/0/0/116/49 | master plan |
| 5 | `019dcf19-407b-75f2-99e4-54d0217d9d14` | 408,633,638 | 1,010 | 0/73/100/12/54/51 | 249 | 172/77/0 | 42/29/35/0/1/18 | fix compile blockers |
| 6 | `019dfc26-b869-7bf3-a254-de3f0a8111e9` | 349,084,791 | 489 | 111/121/107/397/150/108 | 886 | 776/110/0 | 30/4/9/0/5/45 | basin detection |
| 7 | `019def23-b6e4-7d72-9992-a10a17f0d7db` | 340,869,732 | 287 | 9/241/677/156/123/241 | 1,205 | 1,038/167/0 | 66/75/35/0/24/149 | generic greeting |
| 8 | `019dfd9c-337f-7842-81b5-e4b862462b87` | 333,924,928 | 500 | 85/40/91/152/186/175 | 554 | 502/52/0 | 13/21/8/0/8/22 | quest progression |
| 9 | `019dda15-a011-7a12-a62c-1bc748a269a3` | 310,515,372 | 357 | 82/187/355/85/195/199 | 905 | 783/122/0 | 58/52/55/0/3/33 | xeno-botany prompt |
| 10 | `019dda14-db04-74b0-91a0-e1088c40bc88` | 308,909,822 | 426 | 60/170/107/45/150/198 | 532 | 504/28/0 | 54/30/60/0/4/24 | procedural flora |
| 11 | `019dfd94-84ae-7b53-b689-cc893af60675` | 306,217,896 | 514 | 127/34/117/115/290/63 | 683 | 621/62/0 | 16/4/2/0/0/25 | fabricator UI |
| 12 | `019dfd9d-e339-7601-9dca-e945563ed5ff` | 306,165,757 | 483 | 127/89/86/15/316/66 | 634 | 587/47/0 | 10/7/22/0/5/9 | predator AI |
| 13 | `019def93-fcb8-7960-8196-412d6f9ef869` | 296,179,586 | 347 | 45/192/277/208/172/225 | 894 | 751/143/0 | 55/65/17/0/5/55 | abyssal VFX |
| 14 | `019dd8e6-6149-7bb3-8900-cc0f69f9b12f` | 292,161,127 | 372 | 83/59/231/121/208/201 | 702 | 628/74/0 | 72/66/21/0/12/57 | foundation/docs |
| 15 | `019dfc29-331e-7c21-b2ba-b6af81f9445d` | 290,450,970 | 432 | 124/14/120/34/346/71 | 638 | 583/55/0 | 13/9/4/0/1/15 | headless simulation |
| 16 | `019dfd93-edaf-78d1-a75b-2786eb254071` | 288,545,985 | 333 | 60/120/222/115/169/159 | 686 | 569/117/0 | 39/27/22/0/7/25 | procedural audio |
| 17 | `019dfe4e-0a73-7eb1-a5ba-d201ae041c1c` | 283,175,153 | 505 | 53/53/59/101/183/53 | 449 | 408/41/0 | 13/10/17/0/9/39 | bootstrap governance |
| 18 | `019ddea2-fe00-7c62-b0c3-25b81e28794c` | 282,042,919 | 328 | 8/107/323/63/113/182 | 614 | 469/145/0 | 62/49/29/0/2/16 | PDA events |
| 19 | `019dcffb-783a-7690-b720-8ac0ceb29c3b` | 277,725,417 | 442 | 12/24/110/21/105/493 | 272 | 235/37/0 | 42/24/3/0/2/10 | native buffer/AUP |
| 20 | `019d925a-0bf6-7c02-832f-bd2ef5cf13ca` | 275,724,279 | 367 | 0/5/213/10/43/503 | 271 | 242/29/0 | 42/3/0/0/8/17 | fauna/audio |
| 21 | `019dfd50-3d75-7c21-a49c-d1369048a927` | 274,451,591 | 490 | 32/406/70/158/141/16 | 809 | 626/183/0 | 101/112/157/0/8/49 | docs |
| 22 | `019dda12-f963-7133-9e7e-65774a6601c2` | 274,181,378 | 377 | 33/247/184/27/183/148 | 678 | 548/130/0 | 62/75/68/0/5/15 | item interactions |
| 23 | `019dfd9d-3d69-75b1-86ac-d6837fbe922c` | 272,789,160 | 381 | 78/66/111/35/215/139 | 505 | 463/42/0 | 39/23/14/0/8/6 | HUD render graph |
| 24 | `019d9259-d4c0-7751-b30a-ba423b90929e` | 269,031,291 | 444 | 0/11/99/13/34/181 | 157 | 131/26/0 | 12/1/0/0/2/10 | localization |
| 25 | `019dda14-b01c-7423-a02d-d7cd84914afb` | 262,370,401 | 438 | 59/18/188/27/130/292 | 422 | 387/35/0 | 36/27/9/0/1/6 | base integrity |
| 26 | `019dd8d8-8d18-7fd2-8336-334fd3be0e14` | 261,225,332 | 304 | 70/9/334/68/188/242 | 669 | 622/47/0 | 80/68/2/0/21/60 | systems/docs |
| 27 | `019dabac-3c47-74f2-8150-6da883dd6b88` | 259,318,739 | 523 | 25/29/50/15/19/131 | 140 | 100/40/0 | 10/11/7/0/1/4 | boids |
| 28 | `019def94-ec22-78d1-b0e3-1b61c192a31a` | 257,037,410 | 389 | 39/72/142/255/107/224 | 616 | 522/94/0 | 28/25/12/0/4/22 | habitat stress |
| 29 | `019dfe4f-4f6f-7e40-8126-43f1b5a93a20` | 253,340,615 | 426 | 113/44/107/29/214/100 | 507 | 473/34/0 | 17/10/23/0/7/41 | black box telemetry |
| 30 | `019dcffb-ddcd-78e1-8504-eef0baa9a02d` | 252,283,783 | 469 | 8/20/73/74/112/331 | 287 | 249/38/0 | 26/12/3/0/9/17 | save/load |

## Worst Validation Debt By Non-Zero Outputs

| Rank | Thread | Non-zero outputs | CS errors | Compile-fail signals | Title hint |
|---:|---|---:|---:|---:|---|
| 1 | `019e1859-0e01-77b2-a8c6-b5586ccc5c8c` | 216 | 165 | 28 | console/UI |
| 2 | `019dfd50-3d75-7c21-a49c-d1369048a927` | 183 | 101 | 112 | docs |
| 3 | `019def23-b6e4-7d72-9992-a10a17f0d7db` | 167 | 66 | 75 | generic greeting |
| 4 | `019ddea2-fe00-7c62-b0c3-25b81e28794c` | 145 | 62 | 49 | PDA events |
| 5 | `019def93-fcb8-7960-8196-412d6f9ef869` | 143 | 55 | 65 | abyssal VFX |
| 6 | `019dda12-f963-7133-9e7e-65774a6601c2` | 130 | 62 | 75 | item interactions |
| 7 | `019dde7c-df90-7791-b4b4-d49c8450a9be` | 124 | 64 | 66 | split monoliths |
| 8 | `019dda15-a011-7a12-a62c-1bc748a269a3` | 122 | 58 | 52 | xeno-botany |
| 9 | `019dfd93-edaf-78d1-a75b-2786eb254071` | 117 | 39 | 27 | procedural audio |
| 10 | `019dfc26-b869-7bf3-a254-de3f0a8111e9` | 110 | 30 | 4 | basin detection |

## Operational Conclusion

The top-30 burn shows heavy patching plus heavy validation attempts. That is better than silent patch spam, but the failure signal is too large to ignore.

Required next proof before calling a high-burn thread productive:
1. Current final diff for the thread's hot files.
2. Current compile/test pass after all concurrent edits settle.
3. Specific error delta removed, not just "ran build".
4. Quality delta: H-Phi, regression count, or a comparable project metric.

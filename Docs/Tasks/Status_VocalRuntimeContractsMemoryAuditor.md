[x] Read mandate set | DOD: zero-GC/DataVault/ARM64/DSP/registry/telemetry mandates read before audit | Alternative rejected: source-only scan without governing constraints | Estimate: 450 us
[x] Read assigned source files only | DOD: line-numbered CLI reads for VocalBankPlaybackRuntime.cs and VocalBankContracts.cs | Alternative rejected: editor/MCP partial reads that may truncate | Estimate: 1200 us
[x] Static allocation/contract scan | DOD: Select-String pattern scan for managed alloc/log/catch/native/layout/numeric guards | Alternative rejected: manual-only grep from memory | Estimate: 900 us
[x] Report concrete findings | DOD: line-specific findings, no source edits, no fake pass | Alternative rejected: broad architecture commentary outside assigned files | Estimate: 700 us

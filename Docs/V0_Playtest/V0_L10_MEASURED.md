# V0_L10_MEASURED — Swim hop2 FAIL ledger

- **When:** extracted 2026-07-31 16:49 from `Docs/AgentLogs/h8_playprobe_v0_L10.log`
- **Log size:** 2202829 bytes
- **Probe:** H8_HeadlessPlayModeProbe (playmode, no -nographics, no -quit)
- **Verdict: FAIL (Swim locomotion intent)**

## Gates (measured)

| Gate | Result | Evidence |
|------|--------|----------|
| GATE 1 inputServiceRegistered | OPEN | `inputServiceRegistered=True` |
| GATE 2 inputEnabled / switchToPlayer | OPEN | `inputEnabled=True switchToPlayerInputCalled=True` |
| blockMask | CLEAR | `blockMask=0x00000000` |
| immersion | WET | immersionMax samples present (see log) |
| movementIntent01max | **ZERO** | intent never left 0 — FAIL |
| INPUTHOP hop2 (GetState via TryReadFrame) | **ABSENT** | only hop1 (SampleObservables) observed; hop2 never printed |

## Root-cause elevation (post-L10 dig)

1. **Dual-buffer sole-cause DOWNGRADED.** CaptureState applies automation override then
   assigns `_currentState`; Publish syncs MoveDelta. INPUTHOP showed `currentStateMove=(0,1)`
   and `postMaskMove=(0,1)`. If HPM called GetState it would see move.
2. **Menu hop2-starve ELEVATED.** `SampleGameplayLocomotionInputForFixedStep` returns early
   (zeros `_input*`, never calls `ProcessPlayerInputFrame`) when
   `IsGameplayInputBlockedByMenu()` — PDA / Fabricator / Pause open.
   `TryReadFrame` only hits GetState (hop2) when input manager non-null AND player map enabled.
3. Driver TickSettle previously called `SwitchToPlayerInput` once and did **not** force-close
   PDA/Fab/Pause — menus can keep player map disabled / block locomotion for the whole swim window.

## Key log excerpts (truncated)

```
L29: C:\hades\Hecton8\Docs\AgentLogs\h8_playprobe_v0_L10.json
L31: C:\hades\Hecton8\Docs\AgentLogs\h8_playprobe_v0_L10.log
L1733: [H8_PLAYPROBE] START scene=Assets/_Project/Scenes/00_BOOTSTRAP.unity warmupFrames=240 gameplayFrames=0 batchmode=True saveLeg=on saveSlot=0 saveSeconds=60 artifact='C:\hades\Hecton8\Docs\AgentLogs\h8_playprobe_v0_L10.json'
L11431: [PlayerToolManager] STARTERGRANT deferred - the player inventory refused at least one assigned quick-slot tool item. The grant is now retried from the tick lane until the inventory can accept, so this line is printed once. refused=4 granted=0 firstSlot=0 firstItemHash=-1760594330 refusalMask=0x1E (b
L12268: [H8_INPUTHOP] readHop=1 obs=240 | lateFrameTick=35 pumpFired=1 presimTick=362 presimSubsteps=390 | captureRan=358 captureSkippedByFrameGuard=34 | overrideApplied=3 overrideRejected=355 lastOverrideMove=(0,1) | blockMaskNonZero=0 postMaskMove=(0,1) | publishAttempt=390 publishGuardFail=0 publishBuffe
L12286: [H8_INPUTHOP] readHop=1 obs=1200 | lateFrameTick=35 pumpFired=1 presimTick=364 presimSubsteps=392 | captureRan=360 captureSkippedByFrameGuard=34 | overrideApplied=5 overrideRejected=355 lastOverrideMove=(0,1) | blockMaskNonZero=0 postMaskMove=(0,1) | publishAttempt=392 publishGuardFail=0 publishBuff
L12304: [H8_INPUTHOP] readHop=1 obs=3600 | lateFrameTick=35 pumpFired=1 presimTick=386 presimSubsteps=416 | captureRan=378 captureSkippedByFrameGuard=40 | overrideApplied=23 overrideRejected=355 lastOverrideMove=(0,1) | blockMaskNonZero=0 postMaskMove=(0,1) | publishAttempt=416 publishGuardFail=0 publishBuf
L15353: [H8_WORLDDRIVER] VERBSWEEP complete step=17/16 raised=17/17 arrivedInResolvedSnapshot=0/17 dispatcherCommands=0/13 consumerConfirmed=0 | overrideFlagSeen=True overridesPublished=353065 inputEnabled=True blockMask=0x00000000 lastResolvedButtons=0x00000000 atFrame=460 - NOTHING ARRIVED: not one raised
L15360: Hecton8.EditorTools.Diagnostics.H8_HeadlessWorldDriver:WriteVerbSweepLog (bool) (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs:4238)
L15361: Hecton8.EditorTools.Diagnostics.H8_HeadlessWorldDriver:FlushVerbSweepLog (bool) (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs:4166)
L15362: Hecton8.EditorTools.Diagnostics.H8_HeadlessWorldDriver:TickVerbSweep () (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs:4017)
L15378: Hecton8.EditorTools.Diagnostics.H8_HeadlessWorldDriver:WriteVerbSweepLog (bool) (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs:4241)
L15379: Hecton8.EditorTools.Diagnostics.H8_HeadlessWorldDriver:FlushVerbSweepLog (bool) (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs:4166)
L15380: Hecton8.EditorTools.Diagnostics.H8_HeadlessWorldDriver:TickVerbSweep () (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs:4017)
L15396: Hecton8.EditorTools.Diagnostics.H8_HeadlessWorldDriver:WriteVerbSweepLog (bool) (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs:4241)
L15397: Hecton8.EditorTools.Diagnostics.H8_HeadlessWorldDriver:FlushVerbSweepLog (bool) (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs:4166)
L15398: Hecton8.EditorTools.Diagnostics.H8_HeadlessWorldDriver:TickVerbSweep () (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs:4017)
L15414: Hecton8.EditorTools.Diagnostics.H8_HeadlessWorldDriver:WriteVerbSweepLog (bool) (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs:4241)
L15415: Hecton8.EditorTools.Diagnostics.H8_HeadlessWorldDriver:FlushVerbSweepLog (bool) (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs:4166)
L15416: Hecton8.EditorTools.Diagnostics.H8_HeadlessWorldDriver:TickVerbSweep () (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs:4017)
L15432: Hecton8.EditorTools.Diagnostics.H8_HeadlessWorldDriver:WriteVerbSweepLog (bool) (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs:4241)
L15433: Hecton8.EditorTools.Diagnostics.H8_HeadlessWorldDriver:FlushVerbSweepLog (bool) (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs:4166)
L15434: Hecton8.EditorTools.Diagnostics.H8_HeadlessWorldDriver:TickVerbSweep () (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs:4017)
L15450: Hecton8.EditorTools.Diagnostics.H8_HeadlessWorldDriver:WriteVerbSweepLog (bool) (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs:4241)
L15451: Hecton8.EditorTools.Diagnostics.H8_HeadlessWorldDriver:FlushVerbSweepLog (bool) (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs:4166)
...
L22209: Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe:ReportRouteMoments () (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs:2320)
L22216: [H8_PLAYPROBE] MOMENT   PASS          WorldLoad          active gameplay scene '02_HECTON_WORLD' finished loading after 12s; loaded scenes=2; unloading in background=01_MAIN_MENU
L22223: Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe:ReportRouteMoments () (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs:2320)
L22230: [H8_PLAYPROBE] MOMENT   NOT_EXERCISED FirstExit          CONTENT-BLOCKED: no life-pod or drop-pod prefab exists in the project. LifePodSeatStrapLatch, DropPodSeatController and LifePodTactilePrologueController are referenced by zero scenes and zero prefabs, so there is no exit to drive. A driver can
L22237: Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe:ReportRouteMoments () (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs:2320)
L22244: [H8_PLAYPROBE] MOMENT   FAIL          Swim               driver published 77306 input overrides; movementIntent01max=0.000 immersionMax=1.000 depthSampled=True depth=0.000..0.000 span=0.000m oxygen 139.240->139.240 pressure 1.000->1.000 vitalsFlags[o2=False pressure=False depth=False] inputServiceRe
L22251: Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe:ReportRouteMoments () (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs:2320)
L22258: [H8_PLAYPROBE] MOMENT   BLOCKED       Resource           node 'rn_0_0_90001' would not deplete: health=260.000->0.444 normalized=0.002 vulnerabilityMask=0x00000020[Laser] requiredToolClass=Laser after 6.002s / 47705 driver ticks - driverEffect=PlasmaCut capability=0x00000031[Cut|Burn|Laser] pulses=4
L22265: Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe:ReportRouteMoments () (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs:2320)
L22272: [H8_PLAYPROBE] MOMENT   BLOCKED       Tool               PlayerToolManager reports slotCount=4 and IsToolAvailableInSlot is false for every slot, so no tool could be selected on this route [INVENTORY inventoryComponent=present enabled=True gridBound=True version 0->0 InventoryChangedSignal lane=0 - 
L22279: Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe:ReportRouteMoments () (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs:2320)
L22286: [H8_PLAYPROBE] MOMENT   BLOCKED       CraftRepairBuild   Fabricator is live with visibleRecipes=0 totalRecipes=0 lockedRecipes=0 but CanCraft is false for all of them; the Resource leg delivered nothing, so no recipe/repair can consume a resource on this route [SCHEDULE phase=Craft wall=15.044s tick
L22293: Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe:ReportRouteMoments () (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs:2320)
L22300: [H8_PLAYPROBE] MOMENT   BLOCKED       Mission            12 quests are authored and the graph is ready, but nothing completed. authored=12 graphReady=True autoActivated=2 activations=2 completions=0 reverts=0 transitionsLogged=2 genuineCompletions=0 selfCompletions=0
L22307: Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe:ReportRouteMoments () (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs:2320)
L22314: [H8_PLAYPROBE] MOMENT   NOT_EXERCISED Hazard             CONTENT-BLOCKED: no hazard is ever instantiated. RadiationHazardGrid, EnvironmentalHazard, ThermalVentRuntime, HectonHazardSource and HostileFlora have zero AddComponent call sites anywhere, so no hazard exists to create a decision. A driver c
L22321: Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe:ReportRouteMoments () (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs:2320)
L22328: [H8_PLAYPROBE] MOMENT   PARTIAL       SaveLoad           save half observed: TryRequestSave(slot 'slot_0') changed 1 file(s) under 'C:/Users/Admin/AppData/LocalLow/Danat Games/Hecton8' in 1,0s (byteDelta=0). The LOAD half of this row is not exercised by this probe, so the row is not accepted.
L22335: Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe:ReportRouteMoments () (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs:2320)
L22342: [H8_PLAYPROBE] MOMENT   PARTIAL       Proof              run log + per-phase clock table + save directory diff written to 'C:\hades\Hecton8\Docs\AgentLogs\h8_playprobe_v0_L10.json'; run-repeatability now has a producer as well - no comparable state hash this run (state=NeverSampled), slowTickDiscard
L22349: Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe:ReportRouteMoments () (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs:2320)
L22356: [H8_PLAYPROBE] MOMENTS pass=2 partial=2 fail=1 blocked=4 notExercised=2 of 11 Required Route rows. Only pass is acceptance; partial means one half of a two-part row was observed and the row is NOT accepted.
L22363: Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe:ReportRouteMoments () (at Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs:2324)
L22370: [H8_PLAYPROBE] RESULT 1 Required Route row(s) reported FAIL on a run that was asked to start the game, so the exit code now reflects them. Read the MOMENT lines above for which rows and why.
L22418: [H8_INPUTHOP] readHop=0 obs=353066 | lateFrameTick=35 pumpFired=1 presimTick=4213 presimSubsteps=4496 | captureRan=4127 captureSkippedByFrameGuard=371 | overrideApplied=983 overrideRejected=3144 lastOverrideMove=(0,0) | blockMaskNonZero=0 postMaskMove=(0,0) | publishAttempt=4496 publishGuardFail=403
```

## Metrics snapshot

- movementIntent01max samples: `['0.000']`
- immersionMax samples: `['1.000']`
- blockMask samples: `['0x00000000', '0x00000000']`
- readHop samples unique: `['0', '1']` count=4

## STARTERGRANT (separate lane, not fixed in L10)

- refusalMask / vault lanes: see STARTERGRANT lines in log (historically 0x1E vault dead).
- VERBSWEEP: raised vs arrivedInResolvedSnapshot — see log.

## Product fix planned for L11 (implemented before L11 probe)

1. `HectonFabricatorUI.ForceCloseMenu()` public → real `CloseMenu()` (SwitchToPlayerInput).
2. Driver `EnsureGameplayLocomotionInputReady()` force-closes PDA/Pause/Fab via product APIs,
   then `SwitchToPlayerInput` every settle/swim tick.
3. LatchSwim detail: `pdaOpen fabOpen pauseOpen inputEnabledNow`.

## Honesty

- No mocks. Feature without gameplay = DECLINED.
- This ledger is FAIL. Do not treat L10 as PASS.
- hop DiagRecordReadObservation prints hop of threshold-crossing obs, not max hop — hop1 traffic can mask hop2, but **no hop2 line appeared at all** across INPUTHOP.

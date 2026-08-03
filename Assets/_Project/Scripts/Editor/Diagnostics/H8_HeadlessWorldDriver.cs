// ─────────────────────────────────────────────────────────────────────────────────────────────────
// H8_HeadlessWorldDriver — the headless producer for the First 20 Minutes Required Route rows.
//
// WHY THIS EXISTS
//   The probe boots the product, presses New Game, and then watches a world nobody is playing.
//   Batchmode has no input devices, so no locomotion, no interaction, no tool use, and no craft ever
//   happens, and four Required Route rows report NOT_EXERCISED forever.
//
// WHAT IT IS ALLOWED TO DO — one rule, and every method below obeys it
//   Be ANOTHER PRODUCER on the lanes the human's hardware already feeds, then let the unmodified
//   consumers run. It does NOT teleport the player, does NOT write inventory, does NOT set health,
//   oxygen, depth or transforms, and holds no reference to PlayerInventory, to a health field, or to
//   a Transform. It reads player/camera POSITION as a value to decide where in the world to place a
//   resource node — the same decision the terrain scatter producer makes — and never assigns to it.
//   Consequence: every verdict below is produced by the shipping code path, or it is not produced.
//
// THE TWO LANES IT PRODUCES ON
//   AXIS 1, discrete commands: SignalBus<PlayerInputSignal> with SourceHash 0x504C494E ("PLIN") and a
//     strictly increasing Sequence. Same lane, same gates, same payload as InputDispatcher.cs:4014.
//     Consumers (PlayerInteraction, PlayerToolManager, PlayerPDA, ...) cannot tell the difference and
//     are not asked to.
//   AXIS 2, continuous locomotion: CoreDeterminismSignals.TryPublishInputOverride, which
//     InputDispatcher.ApplyAutomationOverride (InputDispatcher.cs:3267, called unconditionally at
//     :3055) folds into the authoritative PlayerInputState AFTER the hardware poll and BEFORE the
//     input block mask. This is the project's own sanctioned synthetic-input lane.
//
//   Registry-slot replacement was evaluated and REJECTED as unsafe, not merely inconvenient:
//   GlobalRegistryServiceSlot.Input is denied by IsSceneRuntimeHotSwapSlot (GlobalRegistry.cs:7161),
//   RegisterInputService (:3106) takes no token, and Register (:7315) calls ThrowSlotHijack (:7450)
//   when the slot is already occupied. Publishing a ScriptedInputService before Ready would therefore
//   make InputDispatcher.TryRegisterInputService (InputDispatcher.cs:2874) throw during Initialize and
//   abort the very boot this probe measures. Publishing after Ready throws CriticalBootException. The
//   automation-override lane needs neither door and leaves the real owner in place.
//
//   THE SLOT BEING OCCUPIED WAS NEVER THE PROBLEM ANYWAY. Measured on the run this file was last read
//   against (Logs/omega_route16.log): the Swim row printed "inputService=False", which is
//   GlobalRegistry.RegisteredInput != null - the slot was EMPTY during gameplay, not hijacked and not
//   disabled. The dispatcher registered fine at boot (:5842-5884, BootstrapPhase.Player, 00_BOOTSTRAP
//   still active) and then died with that scene, because GameBootstrapper's input factory
//   (GameBootstrapper.cs:6309) is the only one of the three dispatcher factories that omits
//   PersistRuntimeService. Fixed at the owner: InputDispatcher.InitializeService now persists itself.
//
// CADENCE
//   Driven by the probe's existing EditorApplication.update tick. No Update/LateUpdate/FixedUpdate, no
//   coroutine, no async. Per-tick work is struct writes, native-ring pushes, ReadOnlySpan scans and
//   property reads — zero managed allocation. Cold work (one-shot component lookup, verdict detail
//   strings) is latched so it can never repeat per frame.
//
// SCHEDULE BUDGET — two units, and they are NOT convertible
//   A driver tick is one pumped EDITOR tick. It is NOT reliably one pumped game frame, and this file used
//   to assert that it was — the assumption that made a 60000-tick pot look infinite. Measured, same phase,
//   two runs on disk:
//     Logs/h8_worldsim_probe5.log:18872  SwimDive ticks=   35 wall=7.001s ->     5.0 ticks/wall second
//     Logs/h8_probe7.log:22889           SwimDive ticks=25865 wall=7.000s ->  3695.0 ticks/wall second
//     Logs/h8_probe7.log:22934           ToolEquip ticks=27180 wall=2.614s -> 10398.0 ticks/wall second
//   probe5 ran its whole ten-phase schedule in 152 ticks; probe7 spent 60000 and never left ToolEquip.
//   A pumped game frame is separately variable and has cost anywhere from 0.23 s to 132 s inside a single
//   run (Logs/h8_playprobe_route.json phases[5]: 124 game frames in 165.186 wall seconds, 0.751 per wall
//   second, one frame carrying about 132 of them). So the schedule is bounded on BOTH axes and each axis
//   bounds a different failure:
//     WALL SECONDS bound a phase that cannot succeed. Each phase gets an ABSOLUTE deadline equal to its
//       OWN time box, so an overrun is charged to the phase that spent it. The old relative
//       "PhaseElapsed < XBudgetSeconds" test charged it to nobody: ResourceDeplete reported 138.192 s
//       against a 6.0 s budget and the three phases after it were still handed their full windows on top
//       of a total that was already spent. Clamping the box by the REMAINING TOTAL instead charged it to
//       the wrong phases: the same overrun emptied the total, so ResourcePickup and Craft were entered
//       with granted=0.000 s and ran their tick floors - one phase still ate the run, and the theft was
//       labelled COMPRESSED rather than stopped. A phase that blows its box now yields as TIMEBOXED and
//       the next phase is entered with its own full box.
//     TICKS bound the reverse failure, which is the one that actually cost a row. A phase's handshake
//       needs a fixed number of ticks — publish on one, read the owner's answer on the next — and a
//       wall-only ceiling in a slow-frame regime grants one. MinTicksFor names the floor per phase and
//       no wall ceiling fires beneath it, so a schedule whose seconds are gone still returns a real
//       verdict per row rather than four NOT_EXERCISED lines that read like missing content.
//   When the total is spent the schedule COMPRESSES rather than terminating: every remaining phase runs
//   its tick floor, yields, and says so in its row. Terminating instead is what the probe's gameplay
//   window did, and it produced "driver ran out of budget in phase Craft ... and never reached this row"
//   for a Craft phase that HAD been entered and was given zero ticks.
//   Nothing here is a bigger budget. TotalBudgetSeconds and all nine phase constants are unchanged.
//
// NO PHASE MAY STARVE A SIBLING — the tick axis had to learn the wall axis's lesson twice over
//   The wall axis got per-phase boxes, absolute deadlines and graceful compression. The tick axis got one
//   global counter and a hard kill, and that asymmetry cost four Required Route rows on probe7:
//     [H8_PLAYPROBE] WORLDDRIVER ticks=60000 phase=ToolEquip elapsed of 79s stopCause=OwnTickCeiling
//     DRIVERPHASE Settle 1 | SwimSurface 6950 | SwimDive 25865 | SwimVerdict 1 | ResourceTarget 2 |
//                 ToolEquip 27180   = 59999 ticks, then the cap fired at 16.266s and TERMINATED the run.
//   ToolUse, ResourceDeplete, ResourcePickup, Craft and VerbSweep were never entered; Resource, Tool and
//   CraftRepairBuild reported NOT_EXERCISED and read as missing world content. Not one phase had exceeded
//   its wall box — SwimDive spent exactly its 7.000s grant, ToolEquip was killed at 2.614s of 6.000s — and
//   the report still told every reader "a phase was spinning without advancing". Three changes fix that
//   class of failure rather than that one run:
//     1. PER-PHASE TICK BOX (MaxTicksFor). Each phase's own wall box valued at the fastest cadence ever
//        measured here. MaxTotalTicks is DERIVED as their sum, so the phases partition the pot instead of
//        racing for it and the arithmetic that let two holds take 55% of it cannot recur.
//     2. TICK COMPRESSION instead of termination. Spending the pot now does what spending the wall total
//        does: every remaining phase runs its tick floor, yields, and its row says UNMEASURED. Only a
//        separate hard stop above the pot still ends a run.
//     3. PRE-EMPTION (AdvancePhase). The yield no longer depends on each phase body remembering to ask.
//   And every yield now carries WHAT the phase was waiting on (WaitReason), because a stop cause plus a
//   tick count cannot distinguish four ToolEquip preconditions with four different owners.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

using System.Globalization;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools.Diagnostics
{
    /// <summary>
    /// Scripted world driver for the four achievable Required Route rows: Swim, Resource, Tool,
    /// CraftRepairBuild. Owned and ticked by <see cref="H8_HeadlessPlayModeProbe"/>.
    /// </summary>
    internal static class H8_HeadlessWorldDriver
    {
        // ── row identity ──────────────────────────────────────────────────────────────────────────
        // Indices, not names. The probe owns the row-name constants (MomentSwim, MomentResource,
        // MomentTool, MomentCraft) and maps index -> constant itself, so the two files cannot drift
        // into disagreeing string literals.
        internal const int RowSwim = 0;
        internal const int RowResource = 1;
        internal const int RowTool = 2;
        internal const int RowCraft = 3;
        internal const int RowCount = 4;

        /// <summary>
        /// Deliberately value-identical to <c>H8_HeadlessPlayModeProbe.MomentVerdict</c> so the probe
        /// can cast across without a translation table. Ascending severity: the probe's RecordMoment
        /// enforces "the worse verdict wins" with a single comparison, and that only holds if this
        /// ordering matches.
        /// </summary>
        internal enum RowVerdict : byte
        {
            NotExercised = 0,
            Pass = 1,
            Partial = 2,
            Blocked = 3,
            Fail = 4,
        }

        /// <summary>
        /// Declared in SCHEDULE ORDER, and <see cref="MinimumTicksOwed"/> depends on that: it sums the
        /// tick floors of every member after the current one. Inserting a phase out of order would make
        /// the probe's grace calculation quietly wrong.
        /// </summary>
        private enum DrivePhase : byte
        {
            Idle = 0,
            Settle,
            SwimSurface,
            SwimDive,
            SwimVerdict,
            ResourceTarget,
            ToolEquip,
            ToolUse,
            ResourceDeplete,
            ResourcePickup,
            Craft,

            /// <summary>
            /// Presses every remaining player VERB once, on the shipping producer, after all four rows have
            /// latched.
            ///
            /// LAST ON PURPOSE, and that placement is the whole reason it is safe. Two of the verbs below
            /// (Pda, Inventory) open the PDA, which switches the input map away from gameplay, and a third
            /// (Cancel) is what closes it again. Sweeping them BEFORE the rows would let a UI toggle
            /// suppress locomotion and tool input for the rest of the schedule, and the four rows would then
            /// report a product gap caused by the instrument. Every path out of <see cref="DrivePhase.Craft"/>
            /// latches RowCraft first, and the other three rows latch earlier still, so nothing this phase
            /// does can reach a verdict.
            /// </summary>
            VerbSweep,
            Done,

            /// <summary>
            /// Array-size sentinel for the per-phase ledger. Never entered, never latched, never a
            /// switch case. A hand-maintained count constant next to a 12-member enum is a drift trap;
            /// this cannot fall out of sync.
            /// </summary>
            PhaseCount,
        }

        /// <summary>
        /// Why a phase stopped being the current phase. This is the field that separates "the mechanic
        /// was measured and did not work" from "the instrument never got to look", and the previous
        /// version of this file had no equivalent - every phase transition looked identical in the
        /// output, so a row starved by an earlier phase's overrun read exactly like a row whose content
        /// is missing.
        /// </summary>
        private enum PhaseYield : byte
        {
            NotEntered = 0,

            /// <summary>The phase's own success or design condition was met, including a timed hold
            /// phase reaching its full nominal duration.</summary>
            Completed,

            /// <summary>The phase's wall deadline expired with its work unfinished. This is the
            /// "stuck phase yields to the next" case and it is a real, reportable outcome.</summary>
            WallCeiling,

            /// <summary>
            /// The phase EXCEEDED its own time box rather than merely reaching it: it was still inside
            /// one pumped frame when its deadline passed, so it yielded at the first tick boundary after
            /// the box had already been blown. This is the culprit label. It is distinct from
            /// <see cref="WallCeiling"/> because "spent its 6.0 s window and failed" and "spent 138.192 s
            /// against a 6.0 s window" are different facts about the harness, and the second one is the
            /// one that used to be invisible. The excess is charged to THIS phase: the next phase is
            /// entered with its own full box, not with what is left of a total this phase already spent.
            /// </summary>
            Timeboxed,

            /// <summary>The schedule's whole <see cref="TotalBudgetSeconds"/> was already spent when
            /// this phase ran, so it was compressed to its tick floor. The row is UNMEASURED and the
            /// defect belongs to whichever phase ate the total, not to this one - which is why a phase
            /// that blew its own box is labelled <see cref="Timeboxed"/> and never this.</summary>
            TotalCeiling,

            /// <summary>The schedule's total tick cap fired.</summary>
            TickCeiling,

            /// <summary>
            /// The phase spent its OWN tick box - <see cref="MaxTicksFor"/> - with its work unfinished.
            /// The tick-axis twin of <see cref="Timeboxed"/>, and like it, a CULPRIT label: this phase ran
            /// more driver ticks than its wall box could contain at the fastest cadence ever measured on
            /// this harness, so it was yielding to protect the phases after it. Its row is a real result
            /// about a precondition that never became true, not an unmeasured one - read the WAITING-ON
            /// clause for which precondition.
            /// </summary>
            PhaseTickCeiling,

            /// <summary>
            /// The schedule's whole tick pot - <see cref="MaxTotalTicks"/> - was already spent when this
            /// phase ran, so it was compressed to its tick floor. The tick-axis twin of
            /// <see cref="TotalCeiling"/> and a VICTIM label: the row is UNMEASURED and the defect belongs
            /// to whichever phase ate the pot, which is why a phase that spent its own tick box is labelled
            /// <see cref="PhaseTickCeiling"/> and never this.
            /// <para>
            /// This label is what probe7 should have produced instead of terminating. There, the pot ran
            /// out at tick 60000 and the run STOPPED - ToolUse, ResourceDeplete, ResourcePickup, Craft and
            /// VerbSweep were never entered and four rows printed NOT_EXERCISED, which reads as missing
            /// world content and is not.
            /// </para>
            /// </summary>
            TotalTickCeiling,

            /// <summary>The probe closed the gameplay window while this phase was in flight.</summary>
            ExternalStop,

            /// <summary>The driver threw inside this phase.</summary>
            Aborted,
        }

        /// <summary>
        /// WHAT a phase is waiting for on the tick it could not advance. One byte, set by the phase body on
        /// every tick it decides to wait, recorded into the ledger on phase close.
        ///
        /// WHY IT EXISTS. probe7 reported a stop phase, a stop cause and a tick count, and none of those
        /// three says what the phase WANTED:
        ///   "the schedule stopped in phase ToolEquip at 16.266s of its 79.000s budget, after 60000 driver
        ///    ticks. Stop cause: OwnTickCeiling." (Logs/h8_probe7.log:23048)
        /// A reader cannot act on that. ToolEquip has four completely different reasons to sit still - no
        /// PlayerToolManager on the player context, no tool available in any slot, a lane that refuses the
        /// ToolSlot push, or a swap the tool manager never confirms - with four different owners, and every
        /// one of them produces that identical sentence. Worse, the same log records
        /// <c>discreteSignals=0 discreteDropped=0</c> (:22845), which proves the phase never reached the
        /// push at all, so two of the four were already excluded by evidence sitting in the same file that
        /// the row did not print. Naming the predicate is the difference between a stop and a diagnosis.
        ///
        /// A byte enum rather than a string: this is assigned on a per-tick path, so it must not allocate.
        /// The name and the explanation are resolved once per row, at report time, in
        /// <see cref="AppendWaitReasonNote"/>.
        /// </summary>
        private enum WaitReason : byte
        {
            /// <summary>The phase is advancing normally, or has not yet decided to wait this tick.</summary>
            None = 0,

            /// <summary>Settle: <c>GlobalRegistry.RegisteredPlayer</c> has not published SurvivalSystem or
            /// PlayerMovement yet.</summary>
            PlayerOwnersNotRegistered,

            /// <summary>Settle: the player owners exist but <c>GlobalRegistry.RegisteredInput</c> is still
            /// the empty slot, which is GATE 1 and not an action-map problem.</summary>
            InputServiceNotRegistered,

            /// <summary>Both swim holds: nothing is wrong. The phase is holding its designed duration on
            /// the wall clock and every tick past the second one is a sample, not an attempt.</summary>
            LocomotionHoldInProgress,

            /// <summary>ResourceTarget: no live, undepleted ResourceNode is within
            /// <see cref="ExistingNodeMaxDistanceMeters"/> and inside the forward cone, and the populator
            /// has not produced the driver's registered spawn point yet.</summary>
            ResourceNodeNotAvailable,

            /// <summary>ToolEquip: <c>IPlayerRuntimeContext.ToolManager</c> is null.</summary>
            ToolManagerAbsent,

            /// <summary>ToolEquip: <c>PlayerToolManager.IsToolAvailableInSlot</c> is false for every slot,
            /// which is <c>prefab != null &amp;&amp; HasToolInInventory(prefab)</c> - so it is ALSO false for a
            /// fully authored loadout whenever PlayerInventory switched itself off.</summary>
            NoToolAvailableInAnySlot,

            /// <summary>ToolEquip: a slot was chosen and
            /// <c>SignalBus&lt;PlayerInputSignal&gt;.TryPush</c> refused the ToolSlot command.</summary>
            ToolSlotCommandRefusedByLane,

            /// <summary>ToolEquip: the command was published and <c>CurrentTool</c>/<c>CurrentSlotIndex</c>
            /// have not confirmed the swap.</summary>
            ToolSwapNotConfirmed,

            /// <summary>ToolUse: holding PrimaryFire for its designed duration.</summary>
            ToolPrimaryFireHoldInProgress,

            /// <summary>ResourceDeplete: pulses are being applied and node integrity has not reached
            /// zero.</summary>
            NodeIntegrityNotDepleted,

            /// <summary>ResourcePickup: PlayerInteraction has never hovered a PickupItem, so there is
            /// nothing to press Interact on.</summary>
            PickupNotHovered,

            /// <summary>ResourcePickup: the pickup was hovered and Interact published, and no
            /// <c>ItemAcquiredSignal(ManualPickup)</c> has arrived.</summary>
            PickupAcquisitionNotPublished,

            /// <summary>Craft: no live Fabricator has been found by scene search yet.</summary>
            FabricatorAbsent,

            /// <summary>Craft: the Fabricator is live and <c>CanCraft</c> is false for every visible
            /// recipe.</summary>
            NoCraftableRecipe,

            /// <summary>Craft: StartCraft was accepted and no
            /// <c>ItemAcquiredSignal(Fabricator)</c> has arrived.</summary>
            CraftDeliveryNotPublished,

            /// <summary>VerbSweep: stepping through its fixed 16-step handshake.</summary>
            VerbSweepStepping,
        }

        /// <summary>
        /// Who ended the schedule. Printed verbatim, because the old finalisation text asserted a
        /// mechanism ("driver ran out of budget") that did not exist in this file and pointed every
        /// reader at the wrong constants.
        /// </summary>
        internal enum StopCause : byte
        {
            Unspecified = 0,
            ProbeGameplayWindowClosed,
            ProbeReportingStarted,
            OwnTickCeiling,
            Exception,
        }

        // ── lane constants ────────────────────────────────────────────────────────────────────────
        // Duplicated as a private const in every consumer of the lane (InputDispatcher.cs:88,
        // PlayerInteraction.cs:62), so reproducing the literal is the contract, not a shortcut.
        private const uint PlayerInputSignalSourceHash = 0x504C494Eu;

        // ── phase budgets, wall-clock seconds ─────────────────────────────────────────────────────
        // Wall clock, not frames: a batchmode frame is not a unit of simulation progress, which is the
        // same lesson TickWaitingForSettle already learned the hard way.
        //
        // EVERY NUMBER BELOW IS UNCHANGED from the run that produced the CraftRepairBuild NOT_EXERCISED
        // row. Nothing here was raised. What changed is that each one is now a PER-PHASE TIME BOX enforced
        // as an absolute deadline, instead of a relative "PhaseElapsed < X" test that let one phase spend
        // the whole schedule - see PhaseCeilingReached - and that a phase which blows its box is charged
        // for the excess itself instead of confiscating its successors' boxes - see EnterPhase.
        private const double SettleBudgetSeconds = 8.0;
        private const double SwimSurfaceBudgetSeconds = 5.0;
        private const double SwimDiveBudgetSeconds = 7.0;
        private const double ResourceTargetBudgetSeconds = 6.0;
        private const double ToolEquipBudgetSeconds = 6.0;
        private const double ToolUseBudgetSeconds = 5.0;
        private const double ResourceDepleteBudgetSeconds = 6.0;
        private const double ResourcePickupBudgetSeconds = 6.0;
        private const double CraftBudgetSeconds = 14.0;

        /// <summary>
        /// The verb sweep's box, and the ONE number in this block that was added rather than inherited.
        ///
        /// It is deliberately smaller than the work it bounds. The sweep is TICK-bound, not time-bound: it is
        /// a fixed 16-step handshake (see <see cref="VerbSweepStepCount"/>) and
        /// <see cref="MinTicksVerbSweep"/> guarantees all 16 steps run, so this box only decides whether the
        /// phase is labelled TIMEBOXED at the end. That label costs nothing here - the sweep is the last
        /// phase, it holds no row, and its overrun cannot starve a successor because it has none. The
        /// alternative, sizing the box for 16 pumped frames in the slow regime, would add ~20 s to
        /// <see cref="TotalBudgetSeconds"/> and the probe raises its gameplay window by exactly that amount
        /// (H8_HeadlessPlayModeProbe.cs:479), pushing the run closer to a hard timeout that loses every
        /// verdict already produced. 6.0 s buys the sweep its full step count in the fast regime and its
        /// tick floor in the slow one.
        /// </summary>
        private const double VerbSweepBudgetSeconds = 6.0;

        private const double CraftEvaluationIntervalSeconds = 0.5;

        /// <summary>
        /// How far past its box a phase may land before it is called TIMEBOXED rather than "yielded on its
        /// own ceiling". The ceiling is only testable between pumped frames, so every phase overshoots by
        /// up to one frame's cost as a matter of arithmetic; 123 of the 124 measured frames cost about
        /// 0.23 s each, so 1.0 s is comfortably above the normal-regime frame and far below the 132 s frame
        /// this label exists to name. Shared by <see cref="PhaseExceededItsBox"/> so the ledger's yield and
        /// the row's prose cannot disagree about whether a phase blew its box - two independent thresholds
        /// for one question is how a report ends up contradicting itself.
        /// </summary>
        private const double PhaseBoxOvershootToleranceSeconds = 1.0;

        /// <summary>
        /// Total wall time the schedule can consume. 63.0s, unchanged.
        ///
        /// It used to be a LABEL rather than a limit: this file read it at exactly zero places, and
        /// <c>_startedAt</c> was read at exactly one - the "ran out of budget" message - so the driver
        /// printed an elapsed for a stop it had no code to cause. The only thing that ever ended the
        /// schedule was the probe closing its gameplay window
        /// (H8_HeadlessPlayModeProbe.cs:495), which is why a run reported 160.430s against this 63.0s.
        /// It is now the trigger for compression. It is NOT a clamp on the phase boxes: clamping each box by
        /// what the total had left meant an overrunning phase confiscated every later phase's window - see
        /// EnterPhase.
        ///
        /// ZERO HEADROOM BY CONSTRUCTION, and this is a known defect rather than a design choice. This
        /// constant is DERIVED as the sum of all TEN phase boxes (8+5+7+6+6+5+6+6+14+6 = 69.0), so the
        /// schedule's headroom is exactly 0.0 s. Meanwhile the tolerance documented on
        /// PhaseBoxOvershootToleranceSeconds exists precisely because "the ceiling is only testable between
        /// pumped frames, so every phase overshoots by up to one frame's cost as a matter of arithmetic" -
        /// about 0.23 s in the normal regime. Those two statements cannot both be satisfied: a completely
        /// healthy run in which several phases each overshoot by one frame breaches this total, compression
        /// fires, and a later row - Craft, the very row this work was commissioned to explain - is labelled
        /// as unmeasured with the defect attributed to another phase. A harness that manufactures that label
        /// on a clean run is the same class of false evidence the rest of this cycle is removing.
        ///
        /// NOT FIXED HERE, because the fix is a behaviour change with a coupling that must be checked first:
        /// the VerbSweep note above records that raising this total requires the probe to raise its gameplay
        /// window by the same amount (H8_HeadlessPlayModeProbe.cs), and getting that wrong pushes the run
        /// into a hard timeout that discards every verdict already produced. The principled repair is to add
        /// FIXED, by adding the headroom the boxes always needed. The number is DERIVED from the tolerance
        /// this file already justifies rather than invented: <see cref="ScheduledPhaseCount"/> x
        /// <see cref="PhaseBoxOvershootToleranceSeconds"/>. That is precisely the overshoot the file already
        /// says is unavoidable - "the ceiling is only testable between pumped frames, so every phase
        /// overshoots by up to one frame's cost as a matter of arithmetic" - so the schedule now tolerates
        /// exactly the arithmetic it acknowledges, and no more. Compression still fires on a genuine
        /// overrun, which is what it is for; it no longer fires on a healthy run.
        ///
        /// THE PROBE COUPLING IS AUTOMATIC, verified rather than assumed. H8_HeadlessPlayModeProbe reads
        /// this constant and raises its own gameplay window to <c>TotalBudgetSeconds + 4.0</c>, logging the
        /// raise (H8_HeadlessPlayModeProbe.cs:483-491). So this change propagates without a second edit.
        /// The +4.0 there is deliberately a TICK margin and not a stall allowance, and its own comment
        /// explains at length why it must not be raised to cover a stall - nothing here asks it to.
        ///
        /// WHAT THIS DOES NOT FIX, stated because it is adjacent and easy to assume away: the probe's HARD
        /// TIMEOUT does not track either constant. <c>_hardTimeoutSeconds</c> is assigned once from
        /// <c>-h8TimeoutSeconds</c> and never adjusted, and the probe already documents that with its
        /// DEFAULTS (timeout 240, menu 300, settle 300) the timeout cannot contain even the menu wait. This
        /// change adds 10.0s to a window that default configuration already could not contain. It does not
        /// create that defect and deliberately does not paper over it, because the caller passed that
        /// number on purpose - see the arithmetic block at H8_HeadlessPlayModeProbe.cs:494-500.
        ///
        /// An earlier revision of this comment asserted "the nine boxes sum to exactly this number", which
        /// was wrong twice: there are ten terms and they summed to 69.0, not 63.0.
        /// </summary>
        internal const double TotalBudgetSeconds =
            AllPhaseBudgetSeconds +
            ScheduledPhaseCount * PhaseBoxOvershootToleranceSeconds;

        /// <summary>
        /// The sum of the ten phase time boxes, 69.0s, WITHOUT the overshoot headroom.
        ///
        /// Factored out of <see cref="TotalBudgetSeconds"/> rather than written twice because the TICK
        /// budget below is derived from the same figure, and two hand-maintained copies of "the schedule is
        /// 69 wall seconds of phase boxes" is exactly the drift this file keeps paying for. Value-identical
        /// to what <see cref="TotalBudgetSeconds"/> used to sum inline: 8+5+7+6+6+5+6+6+14+6.
        /// </summary>
        private const double AllPhaseBudgetSeconds =
            SettleBudgetSeconds + SwimSurfaceBudgetSeconds + SwimDiveBudgetSeconds +
            ResourceTargetBudgetSeconds + ToolEquipBudgetSeconds + ToolUseBudgetSeconds +
            ResourceDepleteBudgetSeconds + ResourcePickupBudgetSeconds + CraftBudgetSeconds +
            VerbSweepBudgetSeconds;

        /// <summary>
        /// How many phase boxes <see cref="TotalBudgetSeconds"/> sums: 10. Settle, SwimSurface, SwimDive,
        /// ResourceTarget, ToolEquip, ToolUse, ResourceDeplete, ResourcePickup, Craft, VerbSweep.
        /// <para>
        /// Kept next to that sum so the two cannot drift: adding an eleventh box without updating this
        /// count would silently under-provision the headroom rather than fail, which is the failure style
        /// this file exists to eliminate. SwimVerdict is deliberately NOT counted - it has a 0.0 box (it
        /// holds no wall budget of its own) though it does burn one pumped frame via its tick floor.
        /// </para>
        /// </summary>
        private const int ScheduledPhaseCount = 10;

        // ── tick floors ───────────────────────────────────────────────────────────────────────────
        // MEASURED, Logs/h8_playprobe_route.json phases[5] and Logs/omega_route28.log CLOCKS: the
        // GameplayWarmup phase advanced 124 game frames in 165.186 wall seconds - 0.751 frames per wall
        // second - and the driver ticks once per one of those frames. 123 of them cost about 0.23s each
        // and ONE cost about 132s, which is how ResourceDeplete reported 138.192s against a 6.0s budget
        // while still obeying its own "PhaseElapsed < 6.0" test on every tick it was given.
        //
        // So wall seconds are not the resource these phases consume. TICKS are, and the two are not
        // convertible: 6.0 seconds bought 26 ticks in the fast regime and 0.05 of a tick in the slow one.
        // Every phase therefore declares the number of ticks its handshake physically cannot complete
        // without, and no wall ceiling may fire before that many ticks have run.
        //
        // This floor is the load-bearing half of the fix. A wall-only ceiling, however tight, still
        // hands every phase after a stalled frame exactly one tick - which is what produced
        // "driver ran out of budget in phase Craft ... and never reached this row" on a run whose Craft
        // phase HAD been entered and was then given zero ticks.
        private const int MinTicksSettle = 1;
        private const int MinTicksSwimHold = 2;
        private const int MinTicksSwimVerdict = 1;
        private const int MinTicksResourceTarget = 4;
        private const int MinTicksToolEquip = 3;
        private const int MinTicksToolUse = 2;
        private const int MinTicksResourceDeplete = 2;
        private const int MinTicksResourcePickup = 3;
        private const int MinTicksCraft = 4;

        /// <summary>
        /// One tick per sweep step, and this is a statement about edges, not padding. The dispatcher's
        /// discrete producer is an EDGE detector - <c>pressed = current &amp; ~previous</c>
        /// (InputDispatcher.cs:1050) - so a verb needs one tick with its bit RAISED and, before any re-press,
        /// one tick with it CLEARED. Reading the consumer's answer needs a third. Compressing two steps into
        /// one tick would make the driver's own bit its own "previous" mask and produce zero edges, which is
        /// the one failure mode that would report all 15 verbs dead while the input path was perfect.
        /// </summary>
        private const int MinTicksVerbSweep = VerbSweepStepCount;

        /// <summary>
        /// Sum of every tick floor the schedule can owe: 1+2+2+1+4+3+2+2+3+4+16 = 40. Both hold phases are
        /// counted, and SwimVerdict is counted because it does burn a pumped frame even with a 0.0s box.
        /// <para>
        /// Load-bearing for two derivations below, so it lives here rather than being recomputed: the tick
        /// pot must be able to pay every floor, and the hard stop must sit far enough above the pot that
        /// tick-compression can actually finish paying them.
        /// </para>
        /// </summary>
        private const int AllPhaseTickFloorSum =
            MinTicksSettle + MinTicksSwimHold + MinTicksSwimHold + MinTicksSwimVerdict +
            MinTicksResourceTarget + MinTicksToolEquip + MinTicksToolUse + MinTicksResourceDeplete +
            MinTicksResourcePickup + MinTicksCraft + MinTicksVerbSweep;

        /// <summary>
        /// The fastest driver-tick rate the schedule is sized to survive, in ticks per WALL SECOND, and it
        /// is a measurement rather than a guess.
        ///
        /// WHY THIS CONSTANT EXISTS AT ALL. The file's own opening note says "a driver tick is one pumped
        /// game frame". That is FALSE on this harness and the two logs on disk disagree with each other by
        /// nearly three orders of magnitude for the same phase:
        ///   Logs/h8_worldsim_probe5.log:18872  SwimDive  ticks=   35 wall=7.001s ->      5.0 ticks/s
        ///   Logs/h8_probe7.log:22889           SwimDive  ticks=25865 wall=7.000s ->   3695.0 ticks/s
        ///   Logs/h8_probe7.log:22934           ToolEquip ticks=27180 wall=2.614s ->  10398.0 ticks/s
        /// The whole probe5 schedule - all ten phases, Settle through VerbSweep - cost 152 driver ticks.
        /// probe7 spent 60000 and never left ToolEquip. Same code, same budgets; only the editor's tick
        /// cadence changed.
        ///
        /// 12288 = 3 x 4096, chosen as the next round power-of-two multiple above the 10398 ticks/s
        /// measured on probe7's fastest phase. It is deliberately NOT a per-phase tuning knob: one figure,
        /// derived from the fastest regime ever observed, multiplied by each phase's OWN wall box, gives
        /// every phase a tick box that cannot bite before its wall box in any regime measured so far -
        /// which is the only way a tick ceiling stays a backstop instead of becoming the schedule's real
        /// limit. If a future run measures a faster regime, this is the one number to raise, and the tick
        /// boxes, the pot and the hard stop all follow from it.
        /// </summary>
        private const int DriverTicksPerWallSecondCeiling = 12288;

        /// <summary>
        /// The schedule's tick pot: what all ten phase tick boxes plus every tick floor sum to.
        /// 40 + 69 x 12288 = 847,912.
        ///
        /// THE OLD VALUE WAS 60000 AND IT WAS THE DEFECT THAT COST FOUR ROWS. Its own comment justified
        /// itself as "~1000s of 60 fps gameplay, so it cannot bite a schedule whose wall total is 63s - it
        /// only stops a genuine runaway". Both halves of that were wrong on the measured harness:
        ///   * The driver does not tick at 60 Hz. It ticked at 3695-10398 Hz in probe7, so 60000 ticks buys
        ///     16.3 wall seconds of a 79.0 wall-second schedule. The pot could not contain a HEALTHY run.
        ///   * It did not stop a runaway; it stopped the schedule. Logs/h8_probe7.log:22845-22934 -
        ///     Settle 1 tick, SwimSurface 6950, SwimDive 25865, SwimVerdict 1, ResourceTarget 2,
        ///     ToolEquip 27180 = 59999, then the cap fired at 16.266s and terminated the run in ToolEquip.
        ///     ToolUse, ResourceDeplete, ResourcePickup, Craft and VerbSweep were NEVER ENTERED. Not one
        ///     phase had exceeded its own wall box: SwimDive spent exactly its 7.000s grant and ToolEquip
        ///     was killed at 2.614s of a 6.000s grant. The two timed HOLD phases burned 32815 of the 60000
        ///     ticks doing precisely what they are designed to do - hold for wall seconds while sampling -
        ///     and the report then told every reader "a phase was spinning without advancing".
        ///   * Four Required Route rows (Resource, Tool, CraftRepairBuild, and the Mission row downstream
        ///     of them) reported NOT_EXERCISED for a SCHEDULING reason and read as absent world content. A
        ///     prior lane nearly shipped a Fabricator instance to fix a row that had never been tested.
        /// The pot is now DERIVED from the per-phase boxes instead of being an independent magic number, so
        /// a phase can no longer be starved by arithmetic: see <see cref="MaxTicksFor"/>.
        /// </summary>
        private const int MaxTotalTicks =
            AllPhaseTickFloorSum +
            (int)AllPhaseBudgetSeconds * DriverTicksPerWallSecondCeiling;

        /// <summary>
        /// Headroom between the pot and the hard stop, and the reason the pot no longer terminates the run.
        ///
        /// When the pot is spent the schedule COMPRESSES on the tick axis exactly as it already does on the
        /// wall axis: every remaining phase runs its tick floor, yields, and says so in its row. That needs
        /// ticks of its own to happen - at most floor+2 per remaining phase, 11 phases, 62 ticks worst case
        /// (VerbSweep is the worst single case at 17: its 16 steps plus the step that closes it). 4 x the
        /// total floor is 160, comfortably above that worst case, so the hard stop can only fire if
        /// compression ITSELF failed to advance - which is a real runaway and the one thing this axis should
        /// still kill.
        /// </summary>
        private const int HardStopTickAllowance = 4 * AllPhaseTickFloorSum;

        /// <summary>
        /// The only tick count that still TERMINATES the schedule. 847,912 + 160.
        /// <para>
        /// Reaching this means a phase body ignored both its wall deadline and its tick box AND the
        /// pre-emption in <see cref="AdvancePhase"/> failed to move it, which no path in this file does
        /// today. It is the backstop the old <see cref="MaxTotalTicks"/> claimed to be.
        /// </para>
        /// </summary>
        private const int MaxTotalTicksHardStop = MaxTotalTicks + HardStopTickAllowance;

        /// <summary>
        /// How many ticks past its own tick box a phase may run before the schedule stops asking politely
        /// and pre-empts it (see <see cref="AdvancePhase"/>).
        ///
        /// Every cooperative phase body reaches a <see cref="PhaseCeilingReached"/> test on the SAME tick
        /// the box opens - verified path by path - so this grace is never consumed by correct code. It
        /// exists because "the phase yields when it asks" is a property of eleven separate method bodies and
        /// a twelfth added later could forget to ask, and a harness whose starvation guarantee depends on
        /// every future phase remembering to call one method does not have a guarantee. 64 is small enough
        /// that a forgetful phase costs the schedule nothing measurable and large enough that no legitimate
        /// multi-tick handshake trips it.
        /// </summary>
        private const int PhaseTickBoxGraceTicks = 64;

        // ── acceptance thresholds ─────────────────────────────────────────────────────────────────
        private const float MinMovementIntent01 = 0.01f;
        private const float MinDepthSpanMeters = 0.25f;
        private const float MinOxygenDelta = 0.0005f;
        private const float MinPressureDelta = 0.0005f;
        private const float MinNodeHealthDelta = 0.001f;
        private const float MinDurabilityDelta = 0.0001f;

        // ── verb sweep ────────────────────────────────────────────────────────────────────────────
        //
        // WHY THIS EXISTS. Measured before this phase was written: the schedule pressed 2 of the 13
        // PlayerInputSignal commands and 2 of the 17 PlayerInputAction bits. Eleven discrete commands and
        // fifteen action bits - each one a whole player verb - had never been executed once in this project's
        // history, so nothing was known about them either way. A verb that has never run is not "probably
        // fine"; it is unmeasured, and the cheapest possible measurement is one press.
        //
        // WHICH LANE IT DRIVES, AND WHY IT IS THE BETTER ONE. The driver already owned
        // PublishDiscreteCommand, which pushes a PlayerInputSignal directly. That reaches the consumers but
        // SKIPS the producer, and the producer is code under test: InputDispatcher.PublishDiscreteInputSignals
        // (InputDispatcher.cs:1048-1107) edge-detects the button mask of the RESOLVED input state and
        // publishes the eleven commands itself. The resolved state is _currentState, which PollInput writes
        // AFTER folding the automation override in (InputDispatcher.cs:3133-3147), and rawState is built from
        // it (:784) before the edge test at :848. So raising a bit on the override lane exercises
        //   automation override -> resolved snapshot -> dispatcher edge detector -> PlayerInputSignal -> consumer
        // where the direct push only exercised the last hop. This phase therefore publishes NO
        // PlayerInputSignal of its own, which is also what makes the attribution sound: any PLIN-hash command
        // seen on the lane while this phase runs was published by the dispatcher, not by the harness.
        //
        // THREE STAGES PER VERB, because three different things can be broken and they have different owners:
        //   ARRIVED  - the bit is present in IInputService.CurrentInputState.ButtonsBitmask. Failure means the
        //              override never reached the dispatcher, or the input block mask erased it.
        //   COMMAND  - the matching PlayerInputSignal command appeared on the lane. Failure with ARRIVED true
        //              means the dispatcher's edge detector did not fire for that bit.
        //   CONSUMED - a consumer-visible observable moved. Only some verbs have one that settles inside this
        //              phase; the ones that do not say so by name instead of implying a pass.
        private const int VerbCount = 17;
        private const int VerbSweepStepCount = 16;

        /// <summary>PLIN command ids run 1..13, so a 14-bit mask indexed by command id covers the lane.</summary>
        private const int PlayerInputCommandCount = 14;

        // Per-verb ledger bits. Kept as flags in one byte per verb rather than four parallel bool arrays:
        // the flush pass reads them together and a half-updated set of parallel arrays is a reporting bug
        // waiting to happen.
        private const byte VerbFlagRaised = 1 << 0;
        private const byte VerbFlagArrivedInSnapshot = 1 << 1;
        private const byte VerbFlagCommandObserved = 1 << 2;
        private const byte VerbFlagConsumerObserved = 1 << 3;

        // ── resource placement ────────────────────────────────────────────────────────────────────
        private const float NodePlacementDistanceMeters = 1.75f;
        private const float ExistingNodeMaxDistanceMeters = 3.5f;
        private const float ExistingNodeMinForwardDot = 0.5f;
        // Yaw sweep so the 60-degree discovery cone above is swept across the full circle instead of
        // staring at whatever the spawn orientation faced. 3 degrees per tick completes a revolution in
        // 120 ticks, comfortably inside a locomotion phase, and is slow enough that the immersion and
        // depth samples the Swim row reads stay stable between ticks.
        private const float SweepYawDegreesPerTick = 3.0f;
        private const float NodeDamagePerPulse = 8.0f;
        private const float ScavengeTileSizeMeters = 512.0f; // ScavengePopulator.cs:189 authored default.
        private const int ScavengeLocalIndex = 90001;

        /// <summary>
        /// Range and pose the synthetic tool pulse declares. Nothing raycasts against it — the signal is
        /// handed straight to the consumer — but a packet whose origin and direction disagree with its own
        /// hit point is a lie stored in a struct, so the pose is a real 2 m top-down approach on the node.
        /// </summary>
        private const float NodeDamageRangeMeters = 2.0f;

        /// <summary>Driver tool identity stamped into <c>InteractionPacket.ToolID</c>: ASCII "H8DW".</summary>
        private const uint NodeDamageToolId = 0x48384457u;

        /// <summary>
        /// Ceiling on identical tool pulses per tick, and NOT a quota — the loop exits the instant the node
        /// depletes or a pulse lands no damage.
        ///
        /// Why a batch at all: this leg is TICK-bound, not time-bound (see the tick-floor block above), and
        /// one pulse per tick cannot finish any authored node. ResourceNode.TakeDamage divides the pulse by
        /// the template hardness (ResourceNode.cs:1175), so the hardest authored node —
        /// ResourceNodeTemplate_VoidGlassMeteorite, maxIntegrity 260 at toolResistance 2.25 — needs
        /// 260 / (8 / 2.25) = 73.1 pulses, against a MinTicksResourceDeplete of 2. One pulse per tick would
        /// report "would not deplete" for a node whose mechanic is perfectly healthy.
        ///
        /// Compression is not a shortcut. ResourceNode.ResolveYieldSampleDeltaSeconds (ResourceNode.cs:1342)
        /// returns a FIXED 0.12 s per call regardless of wall or frame time, so N pulses inside one tick
        /// produce identical damage, identical incremental yield and identical debris to N pulses spread over
        /// N ticks. 96 sits above the worst authored ratio with margin and still well inside the loot
        /// oracle's 64-request frame capacity (ScavengingLootOracleRuntime.cs:34): 8 / 2.25 * 0.12 s = 426 g
        /// per pulse against a 13 kg unit mass emits 2 loot requests across a full 74-pulse depletion, not 74.
        /// </summary>
        private const int MaxNodeDamagePulsesPerTick = 96;

        /// <summary>
        /// Number of <c>InteractionEffectType</c> values the driver will consider, in the fixed preference
        /// order of <see cref="NodeDamageEffectAtPreference"/>.
        /// </summary>
        private const int NodeDamageEffectPreferenceCount = 6;

        // ── state ─────────────────────────────────────────────────────────────────────────────────
        private static DrivePhase _phase = DrivePhase.Idle;
        private static double _phaseStartedAt;
        private static double _startedAt;

        /// <summary>Absolute wall time this phase must yield at: its entry time plus its own time box.
        /// Absolute, not relative: a phase that overran cannot push the next phase's deadline out by the
        /// amount it overran.</summary>
        private static double _phaseDeadline;

        /// <summary>Latch for <see cref="PhaseExceededItsBox"/>, cleared on every phase entry. Kept beside
        /// the deadline it is derived from rather than next to the method, because forgetting to clear it
        /// in <see cref="EnterPhase"/> would mark every subsequent phase TIMEBOXED.</summary>
        private static bool _phaseBoxExceeded;

        /// <summary>Seconds this phase was granted, which is its OWN nominal box - it is deliberately no
        /// longer <c>min(own budget, total remaining)</c>, because that form let one overrunning phase
        /// hand its successors <c>granted = 0.000s</c> and then reported the zero as if the driver had
        /// decided the phase deserved nothing. Reported per row beside the measured wall so a TIMEBOXED
        /// phase and a compressed one cannot read alike.</summary>
        private static double _phaseGranted;
        private static int _phaseTicks;
        private static double _totalDeadline;

        /// <summary>
        /// Ticks this phase may run before it must yield: <see cref="MaxTicksFor"/>, cached on entry beside
        /// the wall deadline it mirrors. Cached rather than recomputed because
        /// <see cref="PhaseCeilingReached"/> is called up to three times per tick by a single phase body and
        /// the derivation walks two switches.
        /// </summary>
        private static int _phaseTickBox;

        /// <summary>
        /// What the CURRENT phase is waiting for, as of the last tick that decided to wait. Written on a
        /// per-tick path, so it is a byte store and never a string.
        /// </summary>
        private static WaitReason _waitReason;

        /// <summary>
        /// True once the schedule's tick POT is spent. The tick-axis twin of <c>_compressed</c>, and it
        /// exists because the pot used to TERMINATE the run instead: probe7 died at tick 60000 in phase
        /// ToolEquip with five phases never entered. Now every remaining phase runs its tick floor, yields,
        /// and labels its row <see cref="PhaseYield.TotalTickCeiling"/> - UNMEASURED, which is a different
        /// claim from NOT_EXERCISED and points at the schedule instead of at absent content.
        /// </summary>
        private static bool _tickCompressed;
        private static DrivePhase _tickCompressedInPhase;
        private static double _tickCompressedAt;
        private static int _tickCompressedAtTick;

        /// <summary>True once the schedule's total wall budget is spent. Every phase after that point
        /// runs its tick floor and yields, so the remaining rows still produce a real verdict instead of
        /// four NOT_EXERCISED lines that look like missing content.</summary>
        private static bool _compressed;
        private static DrivePhase _compressedInPhase;
        private static double _compressedAt;
        private static StopCause _stopCause;
        private static DrivePhase _stoppedInPhase;
        private static double _stoppedAtElapsed;

        // Per-phase ledger, indexed by (int)DrivePhase. Written on phase close, read only at report
        // time. COLD ALLOC: double[13] + int[13] + PhaseYield[13] - one driver-phase ledger for the run,
        // ~230 B total - owner: H8_HeadlessWorldDriver
        private static readonly double[] _phaseWall = new double[(int)DrivePhase.PhaseCount];
        private static readonly double[] _phaseGrant = new double[(int)DrivePhase.PhaseCount];
        private static readonly int[] _phaseTickLedger = new int[(int)DrivePhase.PhaseCount];
        private static readonly PhaseYield[] _phaseYield = new PhaseYield[(int)DrivePhase.PhaseCount];

        // COLD ALLOC: WaitReason[13] - the last precondition each phase was waiting on when it closed,
        // ~13 B - owner: H8_HeadlessWorldDriver
        private static readonly WaitReason[] _phaseWaitReason = new WaitReason[(int)DrivePhase.PhaseCount];
        private static DrivePhase _worstPhase = DrivePhase.Idle;
        private static double _worstPhaseWall;

        /// <summary>
        /// The heaviest TICK consumer, tracked separately from the heaviest WALL consumer because they are
        /// different phases and only one of them answers a tick-axis stop.
        /// <para>
        /// probe7 proves the distinction matters: the finalisation text named SwimDive as "the phase to fix"
        /// because it held the most wall time - 7.000s - while SwimDive had spent EXACTLY its 7.000s grant
        /// and behaved perfectly. On a run killed by the tick axis, pointing a reader at the phase that
        /// filled its wall grant is pointing them at the wrong axis entirely.
        /// </para>
        /// </summary>
        private static DrivePhase _worstTickPhase = DrivePhase.Idle;
        private static int _worstPhaseTicks;

        private static bool _enabled;
        private static bool _stopped;
        private static bool _switchedToPlayerInput;
        private static uint _discreteSequence;
        private static int _droppedDiscreteSignals;
        private static int _publishedDiscreteSignals;
        private static int _publishedOverrides;
        private static int _ticks;

        // ── discrete-lane refusal forensics, latched at the FIRST refused push ─────────────────────
        //
        // WHY THIS BLOCK EXISTS. The run that commissioned it reported
        //   "SignalBus<PlayerInputSignal> refused the ToolSlot1 push on every attempt: pushed=0 dropped=50
        //    availableSlots=4 - the discrete lane is full or closed ... a lane-capacity fault"
        // (Logs/h8_worldsim_probe5.log:19062) and every load-bearing word after the colon was invented:
        //   * "availableSlots=4" was _availableToolSlots - the number of PlayerToolManager slots holding an
        //     available tool. It is not a SignalBus slot count, has no unit in common with one, and cannot
        //     support a sentence about lane capacity. A reader chasing "4 slots free but 50 drops" was
        //     chasing a field from a different subsystem.
        //   * "lane-capacity fault" named ONE of FOUR refusal paths in SignalBus<T>.TryPush
        //     (SignalBusRuntime.cs:678-715) and the driver measured none of them:
        //       1. !_ring.IsCreated after EnsureInitialized (:681). SILENT - EnsureInitialized abandons the
        //          lane with no log when TryAcquireFrameSnapshotBuffer fails (:626-631), and that fails
        //          whenever no IDataVault is bound to SignalBusRegistry or the bound one is allocation-locked
        //          or compaction-fenced (:1491-1495). This path refuses EVERY push forever and prints
        //          nothing, which is exactly the shape of pushed=0 dropped=50 with a clean log.
        //       2. NonCriticalVfx load shedding (:684). Not this lane - PlayerInputSignal is absent from
        //          ResolveNonCriticalVfx (:1666-1682), so this path is excluded by construction.
        //       3. SignalPayloadFiniteGuards.Sanitize returning nonzero (:693). PlayerInputSignal is three
        //          uints and two bytes with no float field (GlobalSignalPayloads.CoreFoundation.cs:1269-1279),
        //          and a nonzero guard code also publishes math-guard telemetry, so this path is both
        //          implausible and separately observable.
        //       4. _ring.TryEnqueue refusing a FULL ring (:704). The real capacity is 64
        //          (GlobalSignals.State.cs:32) and the ring only refuses on count >= capacity
        //          (CoreLowLevelUtilities.cs:892-924). This is the one the row asserted.
        //     1 and 4 are opposite defects with opposite owners - a lane that was never given storage versus
        //     a lane nobody drains - and the row picked the second with no evidence.
        // The fields below are the discriminator, and they are sampled AT the first refusal rather than at
        // phase end: by the time the ceiling fires, a lane that has since flushed reads healthy and the
        // report would describe a moment that was not the failure. Ints and bools only, assigned once - the
        // capture is a latched cold read, not per-tick work.
        private static bool _discreteRefusalCaptured;
        private static byte _discreteRefusalCommand;
        private static int _discreteRefusalTick;
        private static uint _discreteRefusalFrame;
        private static bool _discreteRefusalHadNativeStorage;
        private static int _discreteRefusalSnapshotCount;
        private static int _discreteRefusalPeakQueued;
        private static int _discreteRefusalDroppedLastFlush;
        private static int _discreteRefusalLoadShedTotal;
        private static int _discreteRefusalCorruptedTotal;
        private static int _discreteRefusalRegisteredLanes;
        private static bool _discreteRefusalRegistrationOverflow;
        private static bool _discreteRefusalSimulationHalted;

        /// <summary>
        /// True once a PLIN entry carrying <see cref="_requestedToolSlot"/>'s command has been seen in a
        /// FLUSHED frame snapshot - i.e. the push not only entered the ring but survived
        /// SignalBusRegistry.FlushPostSimulation and became visible to PlayerToolManager's own read
        /// (PlayerToolManager.cs:1954).
        ///
        /// A push that returns true is NOT evidence a consumer could see it. TryPush only proves the payload
        /// entered the ring; the ring is drained into the frame snapshot by the dispatcher's PostSimulation
        /// flush (SystemDispatcher.cs:3036 -> SignalBusRuntime.cs:890), the flush drops the overflow past its
        /// per-frame limit (:944-953), and nothing about that is visible to the producer. Without this flag
        /// the Tool row blamed PlayerToolManager for a swap that never completed - "the discrete lane was
        /// accepted and the swap never completed" - on runs where the command may never have been delivered
        /// at all. That is a Fail written against the wrong owner.
        /// </summary>
        private static bool _toolSlotCommandFlushObserved;

        // Driver-authored locomotion intent. One struct, mutated in place, republished every tick:
        // no per-frame allocation and no per-frame boxing.
        private static PlayerInputState _intent;

        private static readonly RowVerdict[] _verdicts = new RowVerdict[RowCount];
        private static readonly string[] _details = new string[RowCount];
        private static readonly bool[] _latched = new bool[RowCount];

        // COLD ALLOC: StringBuilder[1] - verdict detail composition, at most RowCount latches per run - owner: H8_HeadlessWorldDriver
        private static readonly StringBuilder _detail = new StringBuilder(320);

        // ── one-shot subject cache. Resolved on phase entry, never per frame. ─────────────────────
        private static Hecton8.Gameplay.HectonSurvivalSystem _survival;
        private static Hecton8.Gameplay.HectonPlayerMovement _movement;
        private static Hecton8.Gameplay.PlayerToolManager _toolManager;
        private static Hecton8.Interaction.PlayerInteraction _interaction;
        private static Hecton8.Crafting.Fabricator _fabricator;
        private static Hecton8.Scavenging.ResourceNode _node;
        private static ScavengePopulator _populator;
        private static int _interactionLookupAttempts;
        private static int _fabricatorLookupAttempts;

        /// <summary>
        /// PlayerInteraction is not a registry slot, so it can only be found by scene search, and a
        /// search that runs every tick is a scene traversal every tick. Bounded retries cover "the player
        /// root was not fully assembled on the first attempt" without turning into a per-frame cost.
        /// </summary>
        private const int MaxInteractionLookupAttempts = 8;
        private const int MaxPopulatorLookupAttempts = 8;
        private const int MaxFabricatorLookupAttempts = 8;

        /// <summary>
        /// SlowTick is a ~2 Hz owner lane. Calling it once per editor tick would run its cull pass 30x
        /// faster than the owner intends and could unload the very chunk the driver just populated, so the
        /// forced drain is capped.
        /// </summary>
        private const int MaxForcedPopulatorSlowTicks = 4;

        // ── swim observations ─────────────────────────────────────────────────────────────────────
        private static bool _swimBaselineTaken;
        private static float _oxygenAtStart;
        private static float _pressureAtStart;
        // Sentinels a candidate can always beat, per COMMON_SENSE.md: a min-fold seeded with 0 would report
        // 0 m as the shallowest depth ever reached even if the player never left 40 m.
        private static float _depthMin = float.MaxValue;
        private static float _depthMax = float.MinValue;
        private static float _maxMovementIntent;
        private static float _maxImmersion;
        private static float _oxygenLast;
        private static float _pressureLast;
        private static bool _sawVitalsOxygenFlag;
        private static bool _sawVitalsPressureFlag;
        private static bool _sawVitalsDepthFlag;
        private static bool _inputEnabledEverObserved;
        // Distinct from _inputEnabledEverObserved: GlobalRegistry.RegisteredInput can be null (nothing
        // registered) or non-null with a closed action map, and those are different defects with different
        // owners. One shared flag hid that difference for a full cycle.
        private static bool _inputServiceEverObserved;
        private static uint _inputBlockMaskLast;

        // ── resource observations ─────────────────────────────────────────────────────────────────
        private static bool _nodeFromWorld;
        private static bool _spawnPointRegistered;
        private static bool _populatorReady;
        private static int _populatorLookupAttempts;
        private static int _forcedPopulatorSlowTicks;
        private static int _populatorNodesAtRegister;
        private static float _nodeHealthAtToolUse;
        private static float _nodeHealthAfterToolUse;
        private static bool _nodeDepleted;
        private static bool _sawPickupHover;
        private static bool _interactPublished;
        private static bool _sawManualPickupAcquire;
        private static ushort _manualPickupQuantity;

        // ── resource depletion, capability resolved from the node ─────────────────────────────────
        // The mask is re-read every tick, not cached once: ResourceNode.VulnerabilityMask (:220) is
        // recomputed from the applied template, and a driver that snapshots it would go stale the moment
        // ScavengePopulator restamps a template (ScavengePopulator.cs:869).
        private static bool _nodeDamageEffectResolved;
        private static bool _nodeDamageEffectAccepted;
        private static Hecton8.Interaction.InteractionEffectType _nodeDamageEffect;
        private static uint _nodeDamageCapabilityMask;
        private static uint _nodeVulnerabilityMask;
        private static float _nodeHealthAtDepleteStart;
        private static int _nodeDamagePulses;
        private static int _nodeDamagePulsesLanded;

        // ── tool observations ─────────────────────────────────────────────────────────────────────
        private static int _requestedToolSlot = -1;
        private static int _availableToolSlots;
        private static bool _toolSlotSignalPublished;
        private static bool _toolEquipped;
        private static int _equippedSlotIndex = -1;
        private static float _durabilityAtToolUse;
        private static float _durabilityAfterToolUse;
        private static bool _durabilityReadable;

        // ── inventory upstream census, READ ONLY ──────────────────────────────────────────────────
        // The driver holds no reference to PlayerInventory and never calls a mutator on it; these are the
        // three reads that tell a Tool row WHY it is red. PlayerInventory.Awake disables the component when
        // its DTO layout guard or its vault bind fails (PlayerInventory.cs:1364 and :1387), and a disabled
        // inventory makes PlayerToolManager.IsToolAvailableInSlot false for EVERY slot by construction
        // (PlayerToolManager.cs:927-933 -> HasToolInInventory), which is indistinguishable from "no tool
        // prefabs are authored" unless somebody looks.
        private static bool _inventoryResolved;
        private static bool _inventoryComponentPresent;
        private static bool _inventoryComponentEnabled;
        private static bool _inventoryGridBound;
        private static int _inventoryVersionAtResolve;
        private static int _inventoryVersionLast;

        // ── signal-lane census ────────────────────────────────────────────────────────────────────
        // Counts of frame-snapshot entries seen on the lanes the four rows depend on. Read-only:
        // GetFrameSnapshot returns ReadOnlySpan<T>.Empty for a lane that was never registered
        // (SignalBusRuntime.cs:773-783 via TryReadFrameSnapshot :1542), and it does NOT register one, so
        // observing a dead lane costs nothing and cannot create the lane it is measuring.
        //
        // Each lane below has exactly one interesting producer and a zero therefore names a specific owner:
        //   InventoryChangedSignal      PlayerInventory.cs:5433      the player's own inventory changed
        //   ToolLoadoutChangedSignal    PlayerToolManager.cs:833     the only producer of the swap lane
        //   CraftingStartedSignal       Fabricator.cs:3505           StartCraft reached the shipping lane
        //   ResourceDepletionDeltaSignal ScavengingLootOracleRuntime.cs:1843  yield accounting ran
        //   DebrisSpawnSignal           ResourceNode.cs:1325         the node's own damage side effect
        private static int _laneInventoryChanged;
        private static int _laneToolLoadoutChanged;
        private static int _laneCraftingStarted;
        private static int _laneResourceDepletionDelta;
        private static int _laneDebrisSpawn;
        private static int _lanePlayerInputSignals;
        private static int _laneInputStateSignals;

        // ── verb sweep state ──────────────────────────────────────────────────────────────────────
        private static int _verbSweepStep;
        private static bool _verbSweepLogged;
        private static bool _verbSweepEntered;
        private static uint _verbSweepRaisedMask;
        private static uint _verbSweepArrivedMask;
        private static uint _verbSweepSnapshotButtonsLast;
        private static uint _verbSweepSnapshotFrameLast;
        private static bool _verbSweepOverrideFlagObserved;
        private static bool _verbSweepPdaObservedOpen;
        private static bool _verbSweepPdaClosedAfterCancel;
        private static bool _verbSweepPdaOpenedByInventoryVerb;
        private static bool _verbSweepFlashlightFlipped;
        private static bool _verbSweepFlashlightOnAtEntry;
        private static int _verbSweepToolSlotAtEntry = -1;
        private static int _verbSweepToolSlotObserved = -1;
        private static int _verbSweepLoadoutSignalsAtEntry;

        // COLD ALLOC: byte[17] + uint[17] + bool[14] - one verb ledger and one command-seen table for the
        // run, ~85 B total, written from the sweep phase and read once at flush - owner: H8_HeadlessWorldDriver
        private static readonly byte[] _verbFlags = new byte[VerbCount];
        private static readonly uint[] _verbArrivedFrame = new uint[VerbCount];
        private static readonly bool[] _commandSeen = new bool[PlayerInputCommandCount];

        // COLD ALLOC: StringBuilder[1] - verb-sweep and lane-census log composition, at most one flush per
        // run - owner: H8_HeadlessWorldDriver
        //
        // SEPARATE from _detail on purpose: _detail is mid-compose whenever a row latches, and a shared
        // builder would let a log line truncate a verdict that was being written in the same tick.
        private static readonly StringBuilder _log = new StringBuilder(512);

        // ── craft observations ────────────────────────────────────────────────────────────────────
        private static int _visibleRecipeCount;
        private static int _craftableRecipeCount;
        private static double _craftEvaluatedAt;
        private static bool _craftStarted;
        private static bool _craftAccepted;
        private static bool _craftObservedRunning;
        private static float _craftProgressPeak;
        private static bool _sawFabricatorAcquire;

        /// <summary>True while the schedule still has work to do.</summary>
        internal static bool IsActive =>
            _enabled && !_stopped && _phase != DrivePhase.Idle && _phase != DrivePhase.Done;

        /// <summary>Diagnostic counters for the probe's own summary line.</summary>
        internal static int TickCount => _ticks;

        internal static int PublishedDiscreteSignalCount => _publishedDiscreteSignals;

        internal static int DroppedDiscreteSignalCount => _droppedDiscreteSignals;

        internal static int PublishedOverrideCount => _publishedOverrides;

        internal static string CurrentPhaseName => _phase.ToString();

        /// <summary>Wall seconds since <see cref="Begin"/>, or 0 before it.</summary>
        internal static double ElapsedSeconds =>
            _startedAt > 0.0 ? EditorApplication.timeSinceStartup - _startedAt : 0.0;

        /// <summary>True once the total wall budget was spent and the remaining phases are running on
        /// their tick floors. A run with this set produced UNMEASURED rows, not empty ones.</summary>
        internal static bool IsCompressed => _compressed;

        /// <summary>
        /// How many more driver ticks the unfinished part of the schedule cannot produce a verdict
        /// without. The probe sizes its grace off this number instead of guessing seconds, because on
        /// this harness seconds and ticks are not convertible - one measured frame cost 132 s and the
        /// 123 around it cost 0.23 s each.
        ///
        /// An upper bound on purpose: the live route can skip phases (a blocked Settle jumps straight to
        /// ResourceTarget), so this over-counts rather than under-counts, and the caller caps it anyway.
        /// </summary>
        internal static int MinimumTicksOwed
        {
            get
            {
                if (!IsActive)
                    return 0;

                int owed = MinTicksFor(_phase) - _phaseTicks;
                if (owed < 0)
                    owed = 0;

                for (int phase = (int)_phase + 1; phase < (int)DrivePhase.PhaseCount; phase++)
                    owed += MinTicksFor((DrivePhase)phase);

                return owed;
            }
        }

        // ── per-phase ledger, read by the probe's report ──────────────────────────────────────────
        internal static int PhaseLedgerCount => (int)DrivePhase.PhaseCount;

        /// <summary>
        /// <c>Enum.ToString</c> is banned in cadence, not in cold reporting: this runs at most
        /// PhaseLedgerCount times per run from the probe's report pass. A hand-written name table beside
        /// the enum would be one more thing to forget to update.
        /// </summary>
        internal static string GetPhaseName(int phase)
        {
            return phase >= 0 && phase < (int)DrivePhase.PhaseCount
                ? ((DrivePhase)phase).ToString()
                : string.Empty;
        }

        internal static double GetPhaseWallSeconds(int phase)
        {
            return phase >= 0 && phase < _phaseWall.Length ? _phaseWall[phase] : 0.0;
        }

        internal static double GetPhaseGrantedSeconds(int phase)
        {
            return phase >= 0 && phase < _phaseGrant.Length ? _phaseGrant[phase] : 0.0;
        }

        internal static int GetPhaseTicks(int phase)
        {
            return phase >= 0 && phase < _phaseTickLedger.Length ? _phaseTickLedger[phase] : 0;
        }

        internal static int GetPhaseMinimumTicks(int phase)
        {
            return phase >= 0 && phase < (int)DrivePhase.PhaseCount
                ? MinTicksFor((DrivePhase)phase)
                : 0;
        }

        /// <summary>
        /// The phase's tick BOX, the companion of <see cref="GetPhaseMinimumTicks"/>. A phase whose ledger
        /// ticks equal this spent its whole tick box; a phase far below it yielded for another reason.
        /// </summary>
        internal static int GetPhaseMaximumTicks(int phase)
        {
            return phase >= 0 && phase < (int)DrivePhase.PhaseCount
                ? MaxTicksFor((DrivePhase)phase)
                : 0;
        }

        /// <summary>
        /// What this phase was last waiting on. Available to the probe so the DRIVERPHASE ledger line can
        /// carry a <c>waiting=</c> column; the driver also folds it into every row detail it composes, so
        /// the fact is not lost while the probe's own line is owned elsewhere.
        /// </summary>
        internal static string GetPhaseWaitReasonName(int phase)
        {
            return phase >= 0 && phase < _phaseWaitReason.Length
                ? _phaseWaitReason[phase].ToString()
                : string.Empty;
        }

        /// <summary>What the phase currently in flight is waiting on.</summary>
        internal static string CurrentWaitReasonName => _waitReason.ToString();

        /// <summary>Name of the phase that consumed the most driver TICKS - the phase to fix when the run
        /// ends on the tick axis, which is a different question from <see cref="WorstPhaseName"/>.</summary>
        internal static string WorstTickPhaseName => _worstTickPhase.ToString();

        internal static int WorstPhaseTicks => _worstPhaseTicks;

        /// <summary>True once the schedule's tick pot was spent and the remaining phases are running on
        /// their tick floors. Distinct from <see cref="IsCompressed"/>, which is the wall axis.</summary>
        internal static bool IsTickCompressed => _tickCompressed;

        internal static string GetPhaseYieldName(int phase)
        {
            return phase >= 0 && phase < _phaseYield.Length ? _phaseYield[phase].ToString() : string.Empty;
        }

        /// <summary>
        /// Whether this phase was ever the current phase. The report skips the ones that were not, and it
        /// asks through this rather than string-comparing <see cref="GetPhaseYieldName"/> against
        /// "NotEntered": a reader-side match on an enum member name is one rename away from silently
        /// printing every empty row.
        /// </summary>
        internal static bool WasPhaseEntered(int phase)
        {
            return phase >= 0 &&
                phase < _phaseYield.Length &&
                _phaseYield[phase] != PhaseYield.NotEntered;
        }

        /// <summary>Name of the phase that consumed the most wall time - the phase a reader should go
        /// fix when a later row reports itself starved.</summary>
        internal static string WorstPhaseName => _worstPhase.ToString();

        internal static double WorstPhaseWallSeconds => _worstPhaseWall;

        internal static string StopCauseName => _stopCause.ToString();

        internal static RowVerdict GetVerdict(int row)
        {
            return row >= 0 && row < RowCount ? _verdicts[row] : RowVerdict.NotExercised;
        }

        internal static string GetDetail(int row)
        {
            if (row < 0 || row >= RowCount)
                return string.Empty;

            return _details[row] ?? string.Empty;
        }

        /// <summary>
        /// Clears every observation. Called from the probe's ResetRunState so a second Run() in the
        /// same editor session cannot inherit a stale verdict — the failure mode that makes a green
        /// row meaningless.
        /// </summary>
        internal static void Reset()
        {
            _phase = DrivePhase.Idle;
            _phaseStartedAt = 0.0;
            _startedAt = 0.0;
            _phaseDeadline = 0.0;
            _phaseBoxExceeded = false;
            _phaseGranted = 0.0;
            _phaseTicks = 0;
            _phaseTickBox = 0;
            _waitReason = WaitReason.None;
            _totalDeadline = 0.0;
            _compressed = false;
            _compressedInPhase = DrivePhase.Idle;
            _compressedAt = 0.0;
            _tickCompressed = false;
            _tickCompressedInPhase = DrivePhase.Idle;
            _tickCompressedAt = 0.0;
            _tickCompressedAtTick = 0;
            _stopCause = StopCause.Unspecified;
            _stoppedInPhase = DrivePhase.Idle;
            _stoppedAtElapsed = 0.0;
            _worstPhase = DrivePhase.Idle;
            _worstPhaseWall = 0.0;
            _worstTickPhase = DrivePhase.Idle;
            _worstPhaseTicks = 0;

            for (int phase = 0; phase < (int)DrivePhase.PhaseCount; phase++)
            {
                _phaseWall[phase] = 0.0;
                _phaseGrant[phase] = 0.0;
                _phaseTickLedger[phase] = 0;
                _phaseYield[phase] = PhaseYield.NotEntered;
                _phaseWaitReason[phase] = WaitReason.None;
            }

            _enabled = false;
            _stopped = false;
            _switchedToPlayerInput = false;
            _discreteSequence = 0u;
            _droppedDiscreteSignals = 0;
            _publishedDiscreteSignals = 0;
            _publishedOverrides = 0;
            _ticks = 0;

            _discreteRefusalCaptured = false;
            _discreteRefusalCommand = 0;
            _discreteRefusalTick = 0;
            _discreteRefusalFrame = 0u;
            _discreteRefusalHadNativeStorage = false;
            _discreteRefusalSnapshotCount = 0;
            _discreteRefusalPeakQueued = 0;
            _discreteRefusalDroppedLastFlush = 0;
            _discreteRefusalLoadShedTotal = 0;
            _discreteRefusalCorruptedTotal = 0;
            _discreteRefusalRegisteredLanes = 0;
            _discreteRefusalRegistrationOverflow = false;
            _discreteRefusalSimulationHalted = false;
            _toolSlotCommandFlushObserved = false;
            _intent = default;

            for (int i = 0; i < RowCount; i++)
            {
                _verdicts[i] = RowVerdict.NotExercised;
                _details[i] = string.Empty;
                _latched[i] = false;
            }

            _survival = null;
            _movement = null;
            _toolManager = null;
            _interaction = null;
            _fabricator = null;
            _node = null;
            _populator = null;
            _interactionLookupAttempts = 0;
            _fabricatorLookupAttempts = 0;

            _swimBaselineTaken = false;
            _oxygenAtStart = 0f;
            _pressureAtStart = 0f;
            _depthMin = float.MaxValue;
            _depthMax = float.MinValue;
            _maxMovementIntent = 0f;
            _maxImmersion = 0f;
            _oxygenLast = 0f;
            _pressureLast = 0f;
            _sawVitalsOxygenFlag = false;
            _sawVitalsPressureFlag = false;
            _sawVitalsDepthFlag = false;
            _inputEnabledEverObserved = false;
            _inputServiceEverObserved = false;
            _inputBlockMaskLast = 0u;

            _nodeFromWorld = false;
            _spawnPointRegistered = false;
            _populatorReady = false;
            _populatorLookupAttempts = 0;
            _forcedPopulatorSlowTicks = 0;
            _populatorNodesAtRegister = 0;
            _nodeHealthAtToolUse = 0f;
            _nodeHealthAfterToolUse = 0f;
            _nodeDepleted = false;
            _sawPickupHover = false;
            _interactPublished = false;
            _sawManualPickupAcquire = false;
            _manualPickupQuantity = 0;
            _nodeDamageEffectResolved = false;
            _nodeDamageEffectAccepted = false;
            _nodeDamageEffect = Hecton8.Interaction.InteractionEffectType.PlasmaCut;
            _nodeDamageCapabilityMask = 0u;
            _nodeVulnerabilityMask = 0u;
            _nodeHealthAtDepleteStart = 0f;
            _nodeDamagePulses = 0;
            _nodeDamagePulsesLanded = 0;

            _requestedToolSlot = -1;
            _availableToolSlots = 0;
            _toolSlotSignalPublished = false;
            _toolEquipped = false;
            _equippedSlotIndex = -1;
            _durabilityAtToolUse = 0f;
            _durabilityAfterToolUse = 0f;
            _durabilityReadable = false;

            _inventoryResolved = false;
            _inventoryComponentPresent = false;
            _inventoryComponentEnabled = false;
            _inventoryGridBound = false;
            _inventoryVersionAtResolve = 0;
            _inventoryVersionLast = 0;

            _laneInventoryChanged = 0;
            _laneToolLoadoutChanged = 0;
            _laneCraftingStarted = 0;
            _laneResourceDepletionDelta = 0;
            _laneDebrisSpawn = 0;
            _lanePlayerInputSignals = 0;
            _laneInputStateSignals = 0;

            _verbSweepStep = 0;
            _verbSweepLogged = false;
            _verbSweepEntered = false;
            _verbSweepRaisedMask = 0u;
            _verbSweepArrivedMask = 0u;
            _verbSweepSnapshotButtonsLast = 0u;
            _verbSweepSnapshotFrameLast = 0u;
            _verbSweepOverrideFlagObserved = false;
            _verbSweepPdaObservedOpen = false;
            _verbSweepPdaClosedAfterCancel = false;
            _verbSweepPdaOpenedByInventoryVerb = false;
            _verbSweepFlashlightFlipped = false;
            _verbSweepFlashlightOnAtEntry = false;
            _verbSweepToolSlotAtEntry = -1;
            _verbSweepToolSlotObserved = -1;
            _verbSweepLoadoutSignalsAtEntry = 0;

            for (int verb = 0; verb < VerbCount; verb++)
            {
                _verbFlags[verb] = 0;
                _verbArrivedFrame[verb] = 0u;
            }

            for (int command = 0; command < PlayerInputCommandCount; command++)
                _commandSeen[command] = false;

            _visibleRecipeCount = 0;
            _craftableRecipeCount = 0;
            _craftEvaluatedAt = 0.0;
            _craftStarted = false;
            _craftAccepted = false;
            _craftObservedRunning = false;
            _craftProgressPeak = 0f;
            _sawFabricatorAcquire = false;
        }

        /// <summary>
        /// Arms the schedule. Separate from Reset so the probe can decide per run whether the world is
        /// driven at all (-h8SkipWorldDriver), and so a disarmed run still reports NOT_EXERCISED rather
        /// than an invented verdict.
        /// </summary>
        internal static void Begin()
        {
            _enabled = true;
            _stopped = false;
            _startedAt = EditorApplication.timeSinceStartup;
            _totalDeadline = _startedAt + TotalBudgetSeconds;
            EnterPhase(DrivePhase.Settle, PhaseYield.Completed);
        }

        /// <summary>
        /// Releases the locomotion lane so the world is not left under synthetic input while the save
        /// round trip runs. An override left latched would make the save leg measure a moving player.
        ///
        /// The cause is a required argument. The previous parameterless version was called from two
        /// places with two completely different meanings - the gameplay window closing mid-schedule
        /// (H8_HeadlessPlayModeProbe.cs:495) and the report pass tidying up
        /// (H8_HeadlessPlayModeProbe.cs:1512) - and the rows it finalised could not tell a reader which
        /// one had happened. The first is a truncated measurement; the second is a completed one.
        /// </summary>
        internal static void Stop(StopCause cause)
        {
            if (!_enabled || _stopped)
                return;

            // L19 hop2 LIVE: batch peel Stop - native Crash!!! at ClearInputOverride / coverage path
            // (stack: Stop @~1452 <- Probe.Tick). VERBSWEEP already flushed; skip ClearInputOverride,
            // FinaliseUnlatchedRows, BuildCoverageLine Debug.Log storm so probe can EmitResult.
            if (UnityEngine.Application.isBatchMode)
            {
                _intent = default;
                _stopCause = cause;
                _stoppedInPhase = _phase;
                _stoppedAtElapsed = ElapsedSeconds;
                _stopped = true;
                _enabled = false;
                try { CloseCurrentPhase(PhaseYield.Aborted); } catch (System.Exception) { }
                try
                {
                    if (_verbSweepEntered && !_verbSweepLogged)
                        FlushVerbSweepLog(truncated: _verbSweepStep < VerbSweepStepCount);
                }
                catch (System.Exception) { }
                try
                {
                    Debug.Log("[H8_WORLDDRIVER] END batch-peel stopCause=" + cause.ToString()
                        + " phase=" + _stoppedInPhase.ToString()
                        + " elapsed=" + F(_stoppedAtElapsed)
                        + " ticks=" + _ticks.ToString(CultureInfo.InvariantCulture)
                        + " - L19 hop2 LIVE Stop peel");
                }
                catch (System.Exception) { }
                return;
            }

            _intent = default;
            CoreDeterminismSignals.ClearInputOverride();

            _stopCause = cause;
            _stoppedInPhase = _phase;
            _stoppedAtElapsed = ElapsedSeconds;

            // Close the in-flight phase's ledger row before the verdicts are written, so a row that says
            // "my phase got 1 tick" is quoting a recorded number rather than an in-flight one.
            CloseCurrentPhase(
                _phase == DrivePhase.Done ? PhaseYield.Completed : PhaseYield.ExternalStop);

            // _phase is deliberately NOT set to Done: the phase the schedule stopped in is the single
            // most useful fact about a run that did not finish, and overwriting it with "Done" throws it
            // away. _stopped closes IsActive instead.
            FinaliseUnlatchedRows();

            // AFTER the rows, and unconditional. The lane census is worth printing even on a run that died in
            // Settle and never reached the sweep - "InputStateSignal=0" explains four dead rows at once - and
            // the verb ledger then correctly reports every verb as NOT PRESSED rather than as failed.
            FlushVerbSweepLog(_verbSweepEntered && _verbSweepStep < VerbSweepStepCount);
            _stopped = true;
        }

        /// <summary>
        /// One step of the schedule. Called from the probe's EditorApplication.update tick, once per
        /// tick, during gameplay. Never throws: an exception here would take down the whole probe run
        /// and lose the rows that DID resolve, so it is captured into a Fail verdict instead.
        /// </summary>
        internal static void Tick()
        {
            if (!IsActive)
                return;

            _ticks++;

            try
            {
                // Ceilings are evaluated BEFORE the phase runs, so the phase that is about to execute
                // already knows whether it is spending its own budget or running on the schedule's
                // minimum - which is what its row detail has to state.
                if (!EvaluateScheduleCeilings())
                    return;

                // L12 product fix: AdvancePhase authors _intent for the current hold, THEN publish.
                // Previous order published the prior tick's intent (often default/zero on the first
                // hold tick and one frame stale thereafter), so Swim holds could ship MoveDelta=0
                // while phase code had already written (0,1). Consume is destructive (maxFrameAge=2);
                // a zero publish poisons CaptureState for the locomotion consumer window.
                SampleObservables();
                AdvancePhase();
                PublishLocomotionIntent();
                // Drop intent after ship when the phase that just ran does not author input.
                // Exit paths used to clear _intent inside AdvancePhase BEFORE publish; with
                // advance-then-publish that would zero the last hold tick. Clear here instead so
                // the final authored frame still reaches the dispatcher, and verdict/resource
                // phases do not keep re-publishing stale MoveDelta/PrimaryFire/verb bits.
                if (!PhaseAuthorsInputIntent(_phase))
                    _intent = default;
            }
            catch (System.Exception ex)
            {
                _intent = default;
                CoreDeterminismSignals.ClearInputOverride();
                _stopCause = StopCause.Exception;
                _stoppedInPhase = _phase;
                _stoppedAtElapsed = ElapsedSeconds;
                CloseCurrentPhase(PhaseYield.Aborted);
                LatchAllUnlatched(RowVerdict.Fail, ex.GetType().Name, ex.Message);

                // Stop() early-returns once _stopped is set, so this is the last chance to print the census -
                // and a run that threw is exactly when a reader needs to know which lanes were alive.
                FlushVerbSweepLog(_verbSweepEntered && _verbSweepStep < VerbSweepStepCount);
                _stopped = true;
            }
        }

        /// <summary>
        /// The two schedule-level ceilings, checked once per tick. Returns false when the schedule has
        /// been stopped and this tick must not advance a phase.
        ///
        /// Compression rather than termination is the deliberate choice. Terminating at the total
        /// deadline is what the probe's window effectively did, and it produced a NOT_EXERCISED row for a
        /// mechanic nobody had measured. Compression spends a bounded number of extra TICKS - the tick
        /// floors, 24 of them for the whole schedule - to get a real verdict out of every remaining row,
        /// and labels every one of those rows as compressed so none of them can be read as a product gap.
        /// </summary>
        private static bool EvaluateScheduleCeilings()
        {
            if (!_compressed &&
                _totalDeadline > 0.0 &&
                EditorApplication.timeSinceStartup >= _totalDeadline)
            {
                _compressed = true;
                _compressedAt = ElapsedSeconds;
                _compressedInPhase = _phase;
            }

            // THE TICK AXIS NOW COMPRESSES, and this is the fix probe7 asked for. Reaching the pot used to
            // fall straight into the termination block below, which is why that run ended at 16.266s of a
            // 79.000s schedule with ToolUse, ResourceDeplete, ResourcePickup, Craft and VerbSweep never
            // entered and four rows printed NOT_EXERCISED. Nothing about that stop was a runaway: no phase
            // had exceeded its own wall box, and the two timed HOLD phases had spent 32815 of the 60000
            // ticks doing exactly what they are designed to do.
            //
            // Compression is the same remedy the wall axis already applies for the same reason, and it costs
            // at most HardStopTickAllowance ticks: every remaining phase runs its floor, yields, and its row
            // says UNMEASURED with the schedule named as the cause instead of the content.
            if (!_tickCompressed && _ticks >= MaxTotalTicks)
            {
                _tickCompressed = true;
                _tickCompressedAt = ElapsedSeconds;
                _tickCompressedAtTick = _ticks;
                _tickCompressedInPhase = _phase;
            }

            if (_ticks >= MaxTotalTicksHardStop)
            {
                _intent = default;
                CoreDeterminismSignals.ClearInputOverride();
                _stopCause = StopCause.OwnTickCeiling;
                _stoppedInPhase = _phase;
                _stoppedAtElapsed = ElapsedSeconds;
                CloseCurrentPhase(PhaseYield.TickCeiling);
                FinaliseUnlatchedRows();
                _stopped = true;
                return false;
            }

            return true;
        }

        // ─────────────────────────────────────────────────────────────────────────────────────────
        //  PER-TICK: observation and production. Both allocation-free.
        // ─────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Reads the OBSERVABLES the rows are judged on. Properties on the owners plus non-destructive
        /// ReadOnlySpan views over the signal lanes — no allocation, no consumption, no mutation.
        /// </summary>
        private static void SampleObservables()
        {
            // Presence and enablement are recorded SEPARATELY on purpose: they are two different gates with
            // two different owners, and the run that folded them into one flag pointed a whole cycle of
            // work at the action map when the registry slot was the empty one.
            IInputService input = GlobalRegistry.RegisteredInput;
            if (input != null)
            {
                _inputServiceEverObserved = true;
                if (input.IsPlayerInputEnabled)
                    _inputEnabledEverObserved = true;

                _inputBlockMaskLast = input.GetInputBlockMask();

                // The AUTHORITATIVE post-fold snapshot, not the driver's own copy of what it asked for.
                // IInputDeterminismService.CurrentInputState (GlobalRegistryContracts.cs:940) is the state
                // InputDispatcher resolved after ApplyAutomationOverride and ApplyInputBlockMask, so a bit
                // present here provably survived both, and the AutomationOverride flag
                // (InputDispatcher.cs:1112) proves the override lane was actually consumed rather than merely
                // published. _publishedOverrides only ever proved the latter.
                InputState resolved = input.CurrentInputState;
                _verbSweepSnapshotButtonsLast = resolved.ButtonsBitmask;
                _verbSweepSnapshotFrameLast = resolved.Frame;
                if (resolved.HasFlag(InputStateFlags.AutomationOverride))
                    _verbSweepOverrideFlagObserved = true;

                // Credit arrival only for bits the driver is CURRENTLY holding. Without that guard a bit set
                // by something else - or left over in a stale snapshot - would be laundered into this
                // driver's coverage claim, which is the whole class of mistake this ledger exists to avoid.
                uint arrived = resolved.ButtonsBitmask & _intent.ActionsBitmask;
                if (arrived != 0u && (arrived & ~_verbSweepArrivedMask) != 0u)
                {
                    _verbSweepArrivedMask |= arrived;
                    for (int verb = 0; verb < VerbCount; verb++)
                    {
                        if ((arrived & VerbBit(verb)) == 0u ||
                            (_verbFlags[verb] & VerbFlagArrivedInSnapshot) != 0)
                            continue;

                        _verbFlags[verb] |= VerbFlagArrivedInSnapshot;
                        _verbArrivedFrame[verb] = resolved.Frame;
                    }
                }
            }

            Hecton8.Gameplay.HectonSurvivalSystem survival = _survival;
            if (survival != null)
            {
                float depth = survival.Depth;
                if (depth < _depthMin)
                    _depthMin = depth;
                if (depth > _depthMax)
                    _depthMax = depth;

                _oxygenLast = survival.Oxygen;
                _pressureLast = survival.Pressure;
            }

            Hecton8.Gameplay.HectonPlayerMovement movement = _movement;
            if (movement != null)
            {
                float intent = movement.CurrentMovementIntent01;
                if (intent > _maxMovementIntent)
                    _maxMovementIntent = intent;

                float immersion = movement.WaterImmersionRatio;
                if (immersion > _maxImmersion)
                    _maxImmersion = immersion;
            }

            System.ReadOnlySpan<SurvivalVitalsChangedSignal> vitals =
                SignalBus<SurvivalVitalsChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < vitals.Length; i++)
            {
                uint flags = vitals[i].Flags;
                if ((flags & SurvivalVitalsChangedSignalFlags.Oxygen) != 0u)
                    _sawVitalsOxygenFlag = true;
                if ((flags & SurvivalVitalsChangedSignalFlags.Pressure) != 0u)
                    _sawVitalsPressureFlag = true;
                if ((flags & SurvivalVitalsChangedSignalFlags.Depth) != 0u)
                    _sawVitalsDepthFlag = true;
            }

            System.ReadOnlySpan<ItemAcquiredSignal> acquired =
                SignalBus<ItemAcquiredSignal>.GetFrameSnapshot();
            for (int i = 0; i < acquired.Length; i++)
            {
                ItemAcquiredSignal signal = acquired[i];
                if (signal.SourceKind == ItemAcquiredSignalSourceKinds.ManualPickup)
                {
                    // Only credit an acquisition the driver actually asked for. An earlier or unrelated
                    // pickup must not be laundered into this row.
                    if (_interactPublished && !_sawManualPickupAcquire)
                    {
                        _sawManualPickupAcquire = true;
                        _manualPickupQuantity = signal.Quantity;
                    }
                }
                else if (signal.SourceKind == ItemAcquiredSignalSourceKinds.Fabricator)
                {
                    if (_craftAccepted)
                        _sawFabricatorAcquire = true;
                }
            }

            SampleSignalLaneCensus();

            // Field reads on a cached context property (PlayerRuntimeContext.cs:172 is an auto-property, not
            // a GetComponent), so this is per-tick safe, and the version delta is worth sampling every tick:
            // a pickup that bumps InventoryVersion is the difference between "an item entered the bag" and
            // "a signal was published about an item".
            ResolveInventoryCensus();

            Hecton8.Interaction.PlayerInteraction interaction = _interaction;
            if (interaction != null && interaction.CurrentHovered is Hecton8.Interaction.PickupItem)
                _sawPickupHover = true;

            Hecton8.Scavenging.ResourceNode node = _node;
            if (node != null && node.IsDepleted)
                _nodeDepleted = true;

            Hecton8.Crafting.Fabricator fabricator = _fabricator;
            if (fabricator != null)
            {
                if (fabricator.IsCrafting)
                    _craftObservedRunning = true;

                float progress = fabricator.CraftProgress;
                if (progress > _craftProgressPeak)
                    _craftProgressPeak = progress;
            }
        }

        /// <summary>
        /// Counts what the lanes the four rows depend on actually carried, per tick, without consuming or
        /// mutating anything.
        ///
        /// A lane count is not a verdict, and none of these lanes latches a row on its own. It is the fact
        /// that turns an ambiguous row into an actionable one: "the tool never damaged the node" and "the
        /// tool swap lane published 0 signals all run" point at different owners, and before this census the
        /// second half was simply unknown. Grepped by SIGNAL TYPE, never by DTO or BufferID - the mistake
        /// that produced a false dead-code verdict on this project once already.
        /// </summary>
        private static void SampleSignalLaneCensus()
        {
            _laneInventoryChanged += SignalBus<InventoryChangedSignal>.GetFrameSnapshot().Length;
            _laneToolLoadoutChanged += SignalBus<ToolLoadoutChangedSignal>.GetFrameSnapshot().Length;
            _laneCraftingStarted += SignalBus<CraftingStartedSignal>.GetFrameSnapshot().Length;
            _laneResourceDepletionDelta += SignalBus<ResourceDepletionDeltaSignal>.GetFrameSnapshot().Length;
            _laneDebrisSpawn += SignalBus<DebrisSpawnSignal>.GetFrameSnapshot().Length;
            _laneInputStateSignals += SignalBus<InputStateSignal>.GetFrameSnapshot().Length;

            System.ReadOnlySpan<PlayerInputSignal> discrete =
                SignalBus<PlayerInputSignal>.GetFrameSnapshot();
            for (int i = 0; i < discrete.Length; i++)
            {
                if (discrete[i].SourceHash != PlayerInputSignalSourceHash)
                    continue;

                _lanePlayerInputSignals++;

                // DELIVERY, and deliberately measured OUTSIDE the attribution gate below. The gate exists so
                // the driver cannot credit its own push as proof the dispatcher's edge detector works; that
                // concern does not apply here, because this flag makes no claim about who produced the entry.
                // It answers one question the producer otherwise cannot answer at all - did the ToolSlot
                // command this phase pushed ever survive the flush and become readable by a consumer - and the
                // Tool row uses it to decide between blaming the tool system and blaming delivery. Gating it on
                // VerbSweep would leave it false in ToolEquip by construction, which is the phase that needs it.
                if (_requestedToolSlot >= 0 &&
                    discrete[i].Command == (byte)(PlayerInputSignalCommands.ToolSlot1 + _requestedToolSlot))
                {
                    _toolSlotCommandFlushObserved = true;
                }

                // Attribution: only credit a command to the DISPATCHER while the verb sweep is running,
                // because that is the only phase in which this driver pushes nothing onto the lane itself.
                // Outside it the harness is a producer too, and crediting its own push as evidence that
                // InputDispatcher.PublishDiscreteInputSignals works would be a self-certifying instrument.
                if (_phase != DrivePhase.VerbSweep)
                    continue;

                byte command = discrete[i].Command;
                if (command < PlayerInputCommandCount)
                    _commandSeen[command] = true;
            }
        }

        /// <summary>
        /// Reads the inventory owner's OBSERVABLE STATE once, and only as values. No reference is retained,
        /// nothing is written, and no inventory mutator is called - the driver is not allowed to put items in
        /// a bag and then claim the resource route works.
        ///
        /// This exists because two unrelated defects shared one red row. PlayerInventory.Awake disables the
        /// component when its editor-only DTO layout guard fails (PlayerInventory.cs:1356-1372) or when the
        /// vault bind fails (:1385-1389), and a disabled inventory makes HasToolInInventory - and therefore
        /// PlayerToolManager.IsToolAvailableInSlot (PlayerToolManager.cs:927-933) - false for every slot. The
        /// Tool row then printed "no tool exists to select on this route", which reads as unauthored content
        /// and is not: the loadout can be perfectly authored and still report empty.
        /// </summary>
        private static void ResolveInventoryCensus()
        {
            if (_inventoryResolved)
            {
                RefreshInventoryVersion();
                return;
            }

            IPlayerRuntimeContext player = GlobalRegistry.RegisteredPlayer;
            if (player == null)
                return;

            Hecton8.Inventory.PlayerInventory inventory = player.Inventory;
            if (inventory == null)
            {
                // Not latched: the player root may still be assembling, and a permanent "absent" written on
                // the first tick would be the same false-negative the bounded lookup counters elsewhere in
                // this file exist to avoid. It is latched below only once a component is actually seen.
                _inventoryComponentPresent = false;
                return;
            }

            _inventoryResolved = true;
            _inventoryComponentPresent = true;
            _inventoryComponentEnabled = inventory.enabled;
            _inventoryGridBound = inventory.Grid != null;
            _inventoryVersionAtResolve = inventory.InventoryVersion;
            _inventoryVersionLast = _inventoryVersionAtResolve;
        }

        private static void RefreshInventoryVersion()
        {
            IPlayerRuntimeContext player = GlobalRegistry.RegisteredPlayer;
            Hecton8.Inventory.PlayerInventory inventory = player?.Inventory;
            if (inventory == null)
                return;

            _inventoryComponentEnabled = inventory.enabled;
            _inventoryGridBound = inventory.Grid != null;
            _inventoryVersionLast = inventory.InventoryVersion;
        }

        /// <summary>
        /// Names the upstream inventory state in one clause so a Tool row can never again mean two things.
        /// Appended to every Tool-row detail that reports an empty loadout or an ineffective tool.
        /// </summary>
        private static void AppendInventoryUpstreamNote()
        {
            _detail.Append(" [INVENTORY inventoryComponent=")
                .Append(_inventoryComponentPresent ? "present" : "absent")
                .Append(" enabled=").Append(_inventoryComponentEnabled)
                .Append(" gridBound=").Append(_inventoryGridBound)
                .Append(" version ").Append(_inventoryVersionAtResolve).Append("->")
                .Append(_inventoryVersionLast)
                .Append(" InventoryChangedSignal lane=").Append(_laneInventoryChanged);

            if (!_inventoryComponentPresent)
            {
                _detail.Append(" - UPSTREAM UNKNOWN: no PlayerInventory was published on ")
                    .Append("GlobalRegistry.RegisteredPlayer.Inventory at all, so tool availability could ")
                    .Append("not be decided by an inventory that does not exist. This row does NOT say tool ")
                    .Append("use is broken");
            }
            else if (!_inventoryComponentEnabled || !_inventoryGridBound)
            {
                _detail.Append(" - UPSTREAM DISABLED, NOT A TOOL DEFECT: PlayerInventory exists and is ")
                    .Append("switched off. Awake disables it when the editor-only DTO layout guard fails ")
                    .Append("(PlayerInventory.cs:1356-1372) or when TryBindRuntimeStorageCold fails ")
                    .Append("(:1385-1389), and a disabled inventory forces HasToolInInventory - and so ")
                    .Append("IsToolAvailableInSlot (PlayerToolManager.cs:927-933) - false for EVERY slot no ")
                    .Append("matter how the loadout is authored. Tool use is UNMEASURED on this run: fix the ")
                    .Append("inventory owner first, then read this row again");
            }
            else
            {
                _detail.Append(" - UPSTREAM LIVE: the inventory is enabled with its grid bound, so an empty ")
                    .Append("loadout or an ineffective tool here is a REAL tool/content defect and not the ")
                    .Append("inventory guard");
            }

            _detail.Append(']');
        }

        /// <summary>
        /// The 17 <see cref="PlayerInputAction"/> bits by index, in declaration order. A switch rather than a
        /// static array for the same reason <see cref="NodeDamageEffectAtPreference"/> is: no cold managed
        /// allocation, and a default arm that shows up in the report if the enum ever grows past
        /// <see cref="VerbCount"/>.
        /// </summary>
        private static uint VerbBit(int verb)
        {
            switch (verb)
            {
                case 0: return (uint)PlayerInputAction.Jump;
                case 1: return (uint)PlayerInputAction.Interact;
                case 2: return (uint)PlayerInputAction.PrimaryFire;
                case 3: return (uint)PlayerInputAction.SecondaryFire;
                case 4: return (uint)PlayerInputAction.Sprint;
                case 5: return (uint)PlayerInputAction.Dash;
                case 6: return (uint)PlayerInputAction.Pda;
                case 7: return (uint)PlayerInputAction.Inventory;
                case 8: return (uint)PlayerInputAction.Cancel;
                case 9: return (uint)PlayerInputAction.TabNext;
                case 10: return (uint)PlayerInputAction.TabPrevious;
                case 11: return (uint)PlayerInputAction.ToolSlot1;
                case 12: return (uint)PlayerInputAction.ToolSlot2;
                case 13: return (uint)PlayerInputAction.ToolSlot3;
                case 14: return (uint)PlayerInputAction.ToolSlot4;
                case 15: return (uint)PlayerInputAction.Flashlight;
                case 16: return (uint)PlayerInputAction.Pause;
                default: return 0u;
            }
        }

        private static string VerbName(int verb)
        {
            switch (verb)
            {
                case 0: return "Jump";
                case 1: return "Interact";
                case 2: return "PrimaryFire";
                case 3: return "SecondaryFire";
                case 4: return "Sprint";
                case 5: return "Dash";
                case 6: return "Pda";
                case 7: return "Inventory";
                case 8: return "Cancel";
                case 9: return "TabNext";
                case 10: return "TabPrevious";
                case 11: return "ToolSlot1";
                case 12: return "ToolSlot2";
                case 13: return "ToolSlot3";
                case 14: return "ToolSlot4";
                case 15: return "Flashlight";
                case 16: return "Pause";
                default: return "<unmapped>";
            }
        }

        /// <summary>
        /// The PLIN command each bit is expected to produce, taken from the dispatcher's own edge table
        /// (InputDispatcher.cs:1055-1106). Zero means the bit HAS no discrete command there, and that is a
        /// fact about the product rather than a hole in this table:
        ///   Jump   buffers PlayerBufferedAction.Jump instead (:1056),
        ///   Sprint and Dash are continuous movement modifiers with no discrete lane,
        ///   Pause has no entry at all - PauseMenuController listens to InputManager.OnPause
        ///     (PauseMenuController.cs:2908), an InputAction callback that no snapshot producer can reach.
        /// A verb with an expected command of zero is reported as SNAPSHOT-ONLY, never as a pass.
        /// </summary>
        private static byte VerbExpectedCommand(int verb)
        {
            switch (verb)
            {
                case 1: return PlayerInputSignalCommands.Interact;
                case 2: return PlayerInputSignalCommands.PrimaryAction;
                case 3: return PlayerInputSignalCommands.SecondaryAction;
                case 6: return PlayerInputSignalCommands.TogglePda;
                case 7: return PlayerInputSignalCommands.ToggleInventory;
                case 8: return PlayerInputSignalCommands.Cancel;
                case 9: return PlayerInputSignalCommands.TabNext;
                case 10: return PlayerInputSignalCommands.TabPrevious;
                case 11: return PlayerInputSignalCommands.ToolSlot1;
                case 12: return PlayerInputSignalCommands.ToolSlot2;
                case 13: return PlayerInputSignalCommands.ToolSlot3;
                case 14: return PlayerInputSignalCommands.ToolSlot4;
                case 15: return PlayerInputSignalCommands.Flashlight;
                default: return 0;
            }
        }

        /// <summary>
        /// Phases that write locomotion/tool/verb bits into <see cref="_intent"/> each tick.
        /// Used after publish to drop stale intent when the schedule is on a non-authoring phase
        /// (verdict, resource, craft, settle, done, ...) without zeroing the last hold frame.
        /// </summary>
        private static bool PhaseAuthorsInputIntent(DrivePhase phase)
        {
            switch (phase)
            {
                case DrivePhase.SwimSurface:
                case DrivePhase.SwimDive:
                case DrivePhase.ToolUse:
                case DrivePhase.VerbSweep:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Publishes the driver-authored locomotion intent onto the automation-override lane that
        /// InputDispatcher already consumes. One struct copy into a static sidecar
        /// (CoreDeterminismSignals.cs:55) — no ring push, no allocation.
        ///
        /// TryConsumeLatestInputOverride clears the sidecar on read and rejects anything older than two
        /// dispatcher frames, so this must be republished with the CURRENT frame id every tick. That is
        /// exactly the cadence the hardware producer runs at.
        /// </summary>
        private static void PublishLocomotionIntent()
        {
            if (CoreDeterminismSignals.TryPublishInputOverride(in _intent, SystemDispatcher.CurrentFrameId))
                _publishedOverrides++;
        }

        /// <summary>
        /// Pushes one discrete command onto SignalBus&lt;PlayerInputSignal&gt; with the two gates every
        /// consumer enforces: the "PLIN" source hash and a strictly increasing sequence.
        ///
        /// The sequence is lifted above whatever the lane last carried rather than starting at 1.
        /// Consumers keep ONE _lastPlayerInputSignalSequence per instance and drop anything not newer
        /// (PlayerInteraction.cs:411), so a driver counter starting behind the real producer's would be
        /// silently discarded and the row would fail for a reason invisible in the log.
        /// </summary>
        private static bool PublishDiscreteCommand(byte command)
        {
            if (SignalBus<PlayerInputSignal>.TryGetLatest(out PlayerInputSignal latest, out int laneSequence) &&
                laneSequence != 0 &&
                unchecked(latest.Sequence - _discreteSequence) < 0x80000000u)
            {
                _discreteSequence = latest.Sequence;
            }

            _discreteSequence = unchecked(_discreteSequence + 1u);
            if (_discreteSequence == 0u)
                _discreteSequence = 1u;

            PlayerInputSignal signal = default;
            signal.SourceHash = PlayerInputSignalSourceHash;
            signal.Frame = SystemDispatcher.CurrentFrameId;
            signal.Sequence = _discreteSequence;
            signal.Command = command;
            signal.Flags = 0;

            bool pushed = SignalBus<PlayerInputSignal>.TryPushTracked(in signal, ref _droppedDiscreteSignals);
            if (pushed)
            {
                _publishedDiscreteSignals++;
                return true;
            }

            CaptureDiscreteRefusal(command);
            return false;
        }

        /// <summary>
        /// Latches the lane's own state the first time it refuses a push, so the row can name WHICH of the
        /// four <c>TryPush</c> refusal paths fired instead of asserting one. Everything read here is a public
        /// static on <c>SignalBus&lt;T&gt;</c> or <c>SignalBusRegistry</c> in
        /// <c>Hecton8.Core.Contracts.Signals</c>, which this file already imports; nothing internal to the
        /// signal runtime is touched and nothing is mutated.
        ///
        /// One-shot by design. Re-sampling on every refusal would overwrite the failing moment with the last
        /// one, and the last one is typically a phase-ceiling tick several seconds later whose state no longer
        /// explains anything. Cost after the latch is a single bool test.
        /// </summary>
        private static void CaptureDiscreteRefusal(byte command)
        {
            if (_discreteRefusalCaptured)
                return;

            _discreteRefusalCaptured = true;
            _discreteRefusalCommand = command;
            _discreteRefusalTick = _ticks;
            _discreteRefusalFrame = SystemDispatcher.CurrentFrameId;

            // HasNativeStorage is _ring.IsCreated (SignalBusRuntime.cs:477). FALSE here is the whole answer:
            // the lane has no ring, so capacity is not the subject and no number of retries can help. TryPush
            // calls EnsureInitialized on every attempt, so a false reading also means the re-init failed again
            // on this very tick - the vault was still unbound, allocation-locked or compaction-fenced.
            _discreteRefusalHadNativeStorage = SignalBus<PlayerInputSignal>.HasNativeStorage;
            _discreteRefusalSnapshotCount = SignalBus<PlayerInputSignal>.SnapshotCount;
            _discreteRefusalPeakQueued = SignalBus<PlayerInputSignal>.PeakQueuedLastFlush;
            _discreteRefusalDroppedLastFlush = SignalBus<PlayerInputSignal>.DroppedLastFlush;
            _discreteRefusalLoadShedTotal = SignalBus<PlayerInputSignal>.LoadShedTotal;
            _discreteRefusalCorruptedTotal = SignalBus<PlayerInputSignal>.CorruptedSignalTotal;
            _discreteRefusalRegisteredLanes = SignalBusRegistry.LaneCount;
            _discreteRefusalRegistrationOverflow = SignalBusRegistry.RegistrationOverflow;
            _discreteRefusalSimulationHalted = SignalBusRegistry.IsSimulationHalted;
        }

        /// <summary>
        /// Appends the latched refusal forensics and the ONE cause they are consistent with. Cold: called at
        /// most once per run, from the ToolEquip refusal path.
        /// <para>
        /// Writes into <c>_detail</c>, so it must only ever be called between a <c>_detail.Clear()</c> and the
        /// <c>Latch</c> that consumes it. The census line composes the same facts into <c>_log</c> by hand
        /// instead of calling this: the two builders are separate on purpose (see the note on <c>_log</c>) and
        /// routing the census through a <c>_detail</c> helper would let a diagnostic truncate a verdict that is
        /// mid-compose in the same tick.
        /// </para>
        /// </summary>
        private static void AppendDiscreteRefusalNote()
        {
            if (!_discreteRefusalCaptured)
            {
                _detail.Append(" [lane=SignalBus<PlayerInputSignal> never refused a push this run]");
                return;
            }

            _detail.Append(" [lane=SignalBus<PlayerInputSignal> firstRefusal: command=")
                .Append(_discreteRefusalCommand)
                .Append(" atDriverTick=").Append(_discreteRefusalTick)
                .Append(" atDispatcherFrame=").Append(_discreteRefusalFrame)
                .Append(" expected=accepted actual=refused")
                .Append(" | hasNativeStorage=").Append(_discreteRefusalHadNativeStorage)
                .Append(" snapshotCount=").Append(_discreteRefusalSnapshotCount)
                .Append(" peakQueuedLastFlush=").Append(_discreteRefusalPeakQueued)
                .Append(" droppedLastFlush=").Append(_discreteRefusalDroppedLastFlush)
                .Append(" loadShedTotal=").Append(_discreteRefusalLoadShedTotal)
                .Append(" corruptedTotal=").Append(_discreteRefusalCorruptedTotal)
                .Append(" registeredLanes=").Append(_discreteRefusalRegisteredLanes)
                .Append(" registrationOverflow=").Append(_discreteRefusalRegistrationOverflow)
                .Append(" simulationHalted=").Append(_discreteRefusalSimulationHalted)
                .Append(']');

            if (!_discreteRefusalHadNativeStorage)
            {
                _detail.Append(" - THE LANE HAS NO NATIVE STORAGE, so this is NOT a capacity fault and no ")
                    .Append("retry cadence can fix it. SignalBus<PlayerInputSignal>.EnsureInitialized ")
                    .Append("abandoned the lane without logging: either no IDataVault is bound to ")
                    .Append("SignalBusRegistry or the bound one is allocation-locked or compaction-fenced, so ")
                    .Append("TryAcquireFrameSnapshotBuffer failed and the ring was disposed on creation ")
                    .Append("(SignalBusRuntime.cs:626-631 via :1491-1495). Owner is the vault bind, not the ")
                    .Append("harness and not PlayerToolManager");
                return;
            }

            if (_discreteRefusalRegistrationOverflow)
            {
                _detail.Append(" - the lane dispatch table OVERFLOWED, so registered lanes are not all ")
                    .Append("flushed and an unflushed ring fills to its 64-entry capacity and then refuses ")
                    .Append("every push permanently (GlobalSignals.State.cs:32)");
                return;
            }

            _detail.Append(" - storage exists, so the refusal is _ring.TryEnqueue on a FULL ring ")
                .Append("(SignalBusRuntime.cs:704): 64 entries are queued and undrained. The drain is the ")
                .Append("dispatcher's PostSimulation flush (SystemDispatcher.cs:3036 -> ")
                .Append("SignalCorridorRuntime.FlushPostSimulation); snapshotCount=0 with a full ring means ")
                .Append("that flush is not reaching this lane");
        }

        // ─────────────────────────────────────────────────────────────────────────────────────────
        //  SCHEDULE
        // ─────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Closes the outgoing phase's ledger row and opens the next one with its OWN full time box as an
        /// ABSOLUTE deadline.
        ///
        /// THE BOX IS PER PHASE AND IT IS NOT A SLICE OF A SHARED POT. Two different failures had to be
        /// fixed here and only one of them was:
        ///   1. The original file compared <c>PhaseElapsed</c> against its own constant, so the schedule's
        ///      start time was re-based on every transition and an overrun was simply forgiven -
        ///      ResourceDeplete overshot its 6.0s by 132s and every later phase was still handed its full
        ///      window on top of a total that was already three times spent. An absolute deadline fixes
        ///      that, and it is what makes a stuck phase yield instead of run.
        ///   2. Clamping that deadline by <c>_totalDeadline - now</c> then overcorrected into the mirror
        ///      defect: ResourceDeplete's overrun emptied the total, so ResourcePickup and Craft were
        ///      entered with <c>granted = 0.000s</c> and ran their tick floors. One phase still ate the
        ///      whole run; the theft was labelled COMPRESSED rather than stopped, and the Craft row was
        ///      still starved. A moderate overrun did the same thing quietly - a 20s overshoot silently
        ///      shaved Craft's 14.0s box down to 12.0s and the row's "granted" figure never said why.
        /// So the excess is charged to the phase that spent it and to nothing else: the successor gets its
        /// own full nominal box, and the phase that blew its box is recorded as
        /// <see cref="PhaseYield.Timeboxed"/>.
        ///
        /// <see cref="TotalBudgetSeconds"/> is still a real limit; it is just no longer collected from the
        /// wrong phase. It stays the compression trigger in <see cref="EvaluateScheduleCeilings"/>, and
        /// because the nine boxes sum to exactly that total, compression can now only fire when the boxes
        /// are genuinely all spent - which on this harness means an unpreemptable frame, not a schedule
        /// that overspent.
        /// </summary>
        private static void EnterPhase(DrivePhase phase, PhaseYield reasonForLeavingCurrent)
        {
            CloseCurrentPhase(reasonForLeavingCurrent);

            double now = EditorApplication.timeSinceStartup;
            double granted = BudgetFor(phase);

            _phase = phase;
            _phaseStartedAt = now;
            _phaseGranted = granted;
            _phaseDeadline = now + granted;
            _phaseTicks = 0;

            // The tick box is the second axis of the same box and is granted the same way: per phase, in
            // full, never as a slice of what an earlier phase left. Both defects the wall box had to unlearn -
            // a re-based relative test and a clamp by the shared remainder - are avoided here by construction
            // rather than by comment, because the value depends only on the phase.
            _phaseTickBox = MaxTicksFor(phase);
            _waitReason = WaitReason.None;

            // Ordered after _phaseGranted/_phaseDeadline deliberately: PhaseExceededItsBox reads both, and
            // clearing the latch while they still describe the OUTGOING phase would re-derive the old
            // phase's answer on the next call.
            _phaseBoxExceeded = false;
        }

        /// <summary>
        /// Records what the outgoing phase actually cost and why it stopped being current. Accumulates
        /// with <c>+=</c> rather than assigning: no phase in this schedule is entered twice today, and a
        /// silent overwrite is not the failure mode to leave behind if one ever is.
        /// </summary>
        private static void CloseCurrentPhase(PhaseYield reason)
        {
            DrivePhase previous = _phase;
            if (previous == DrivePhase.Idle || previous == DrivePhase.Done)
                return;

            int index = (int)previous;
            if (index < 0 || index >= (int)DrivePhase.PhaseCount)
                return;

            _phaseWall[index] += EditorApplication.timeSinceStartup - _phaseStartedAt;
            _phaseTickLedger[index] += _phaseTicks;
            _phaseGrant[index] = _phaseGranted;
            _phaseYield[index] = reason;
            _phaseWaitReason[index] = _waitReason;

            if (_phaseWall[index] > _worstPhaseWall)
            {
                _worstPhaseWall = _phaseWall[index];
                _worstPhase = previous;
            }

            // Tracked alongside, never instead: a run that ends on the wall axis and a run that ends on the
            // tick axis have different culprits, and probe7's finalisation text named the wall winner
            // (SwimDive, which spent exactly its grant) for a stop the tick axis caused.
            if (_phaseTickLedger[index] > _worstPhaseTicks)
            {
                _worstPhaseTicks = _phaseTickLedger[index];
                _worstTickPhase = previous;
            }
        }

        private static double PhaseElapsed => EditorApplication.timeSinceStartup - _phaseStartedAt;

        /// <summary>
        /// THE per-phase ceiling test. Every phase asks this instead of comparing <c>PhaseElapsed</c>
        /// against its own constant, and the three clauses are in this order for a reason:
        ///
        ///   1. TICK FLOOR FIRST. A ceiling that can fire before a phase has had the ticks its handshake
        ///      needs does not bound a stall, it converts a stall into four unmeasured rows. The floors
        ///      are named per phase in <see cref="MinTicksFor"/>.
        ///   2. COMPRESSION. Once the schedule's total is spent, a phase runs its floor and yields. It
        ///      does NOT sit out its box on a total that no longer exists.
        ///   3. ABSOLUTE WALL DEADLINE, which is the phase's OWN box as granted in
        ///      <see cref="EnterPhase"/> - not a slice of what an earlier phase left behind.
        ///
        /// The wall clause is still only testable at tick boundaries, so a single 132-second pumped frame
        /// can still overshoot it - nothing inside an editor tick can preempt the engine. What changed is
        /// that the overshoot is now ATTRIBUTED: the phase that blew its box yields as
        /// <see cref="PhaseYield.Timeboxed"/> and says by how much, the next phase is entered with its own
        /// full box, and no later row is starved to pay for it.
        /// </summary>
        private static bool PhaseCeilingReached()
        {
            if (_phaseTicks < MinTicksFor(_phase))
                return false;

            if (_compressed || _tickCompressed)
                return true;

            // CLAUSE 4, and it is the one probe7 needed. The wall clause below cannot see a phase that is
            // burning the schedule's shared tick pot inside its own legitimate window: ToolEquip spent 27180
            // ticks in 2.614s of a 6.000s box and every one of those ticks was charged to a 60000-tick pot
            // that five later phases still needed. A phase's tick box is its OWN share of that pot - sized
            // from its own wall box at the fastest cadence ever measured here - so spending it is the tick
            // twin of reaching its wall deadline and yields the same way.
            if (PhaseExceededItsTickBox())
                return true;

            return EditorApplication.timeSinceStartup >= _phaseDeadline;
        }

        /// <summary>
        /// Whether the phase in flight has spent its own tick box.
        ///
        /// Unlatched, unlike <see cref="PhaseExceededItsBox"/>, and for a reason worth stating: that method
        /// has to latch because it re-reads the CLOCK and two readers microseconds apart could disagree.
        /// <c>_phaseTicks</c> only increments, and never inside a comparison, so every reader on a given tick
        /// gets the same answer with no latch and no window.
        /// </summary>
        private static bool PhaseExceededItsTickBox()
        {
            return _phaseTickBox > 0 && _phaseTicks >= _phaseTickBox;
        }

        /// <summary>Nominal wall budget for a phase, before the total-remaining clamp.</summary>
        private static double BudgetFor(DrivePhase phase)
        {
            switch (phase)
            {
                case DrivePhase.Settle:
                    return SettleBudgetSeconds;
                case DrivePhase.SwimSurface:
                    return SwimSurfaceBudgetSeconds;
                case DrivePhase.SwimDive:
                    return SwimDiveBudgetSeconds;
                case DrivePhase.ResourceTarget:
                    return ResourceTargetBudgetSeconds;
                case DrivePhase.ToolEquip:
                    return ToolEquipBudgetSeconds;
                case DrivePhase.ToolUse:
                    return ToolUseBudgetSeconds;
                case DrivePhase.ResourceDeplete:
                    return ResourceDepleteBudgetSeconds;
                case DrivePhase.ResourcePickup:
                    return ResourcePickupBudgetSeconds;
                case DrivePhase.Craft:
                    return CraftBudgetSeconds;
                case DrivePhase.VerbSweep:
                    return VerbSweepBudgetSeconds;

                // SwimVerdict latches and advances in the same tick, and Idle/Done/PhaseCount are not
                // driven at all. TotalBudgetSeconds deliberately does not include SwimVerdict; the clamp
                // in EnterPhase absorbs its one tick.
                default:
                    return 0.0;
            }
        }

        /// <summary>
        /// The number of driver ticks a phase cannot produce a verdict with fewer than. Each value is a
        /// statement about a handshake, not a padding factor.
        /// </summary>
        private static int MinTicksFor(DrivePhase phase)
        {
            switch (phase)
            {
                // Readiness is re-tested every tick and the phase exits the instant the owners appear, so
                // one tick is enough to ask the question; the wall ceiling does the waiting.
                case DrivePhase.Settle:
                    return MinTicksSettle;

                // A hold phase needs two samples for a DELTA to exist. Given one tick the depth span is
                // exactly 0.000m and the Swim row would report "the player never moved" when the truth is
                // "the instrument looked once".
                case DrivePhase.SwimSurface:
                case DrivePhase.SwimDive:
                    return MinTicksSwimHold;

                case DrivePhase.SwimVerdict:
                    return MinTicksSwimVerdict;

                // FindFirstObjectByType for the populator, RegisterSpawnPoint, the populator's own
                // ProcessSpawnQueue drain on its SlowTick, then TryAdoptNearbyWorldNode seeing the result.
                case DrivePhase.ResourceTarget:
                    return MinTicksResourceTarget;

                // Tick 1 enumerates slots and publishes ToolSlotN on the PLIN lane. PlayerToolManager
                // runs its swap state machine on its own lane, so CurrentTool/CurrentSlotIndex cannot be
                // read back on the same tick - a 1-tick ToolEquip always reports a failed swap.
                case DrivePhase.ToolEquip:
                    return MinTicksToolEquip;

                case DrivePhase.ToolUse:
                    return MinTicksToolUse;

                case DrivePhase.ResourceDeplete:
                    return MinTicksResourceDeplete;

                // Hover observed, then Interact published, then ItemAcquiredSignal observed. Three
                // distinct ticks, and a wall-only ceiling in a slow-frame regime grants one.
                case DrivePhase.ResourcePickup:
                    return MinTicksResourcePickup;

                // Find the Fabricator, sweep CanCraft over AvailableRecipes, StartCraft, then observe the
                // ItemAcquiredSignal the row is actually judged on.
                case DrivePhase.Craft:
                    return MinTicksCraft;

                // One tick per verb-sweep step. This floor is what makes the sweep meaningful in the slow
                // frame regime: at the measured 0.751 game frames per wall second its 6.0s box buys four
                // ticks, and a sweep cut off after four steps would report eleven verbs as unpressed when the
                // instrument simply stopped pressing.
                case DrivePhase.VerbSweep:
                    return MinTicksVerbSweep;

                default:
                    return 0;
            }
        }

        /// <summary>
        /// The number of driver ticks a phase may spend before it must yield: its tick floor plus its own
        /// wall box valued at the fastest cadence this harness has ever been measured at
        /// (<see cref="DriverTicksPerWallSecondCeiling"/>).
        ///
        /// THE INVARIANT THAT MAKES STARVATION IMPOSSIBLE. <see cref="MaxTotalTicks"/> is DERIVED as the sum
        /// of exactly these boxes, so the eleven driven phases partition the pot instead of racing for it. No
        /// phase can consume a share another phase needs, whatever the cadence: the arithmetic that used to
        /// let SwimSurface and SwimDive take 32815 ticks of a 60000-tick pot and leave five phases with none
        /// (Logs/h8_probe7.log:22874-22934) cannot recur, because 60000 was an independent magic number and
        /// this is not.
        ///
        /// THE BOX IS DELIBERATELY LOOSE, and that is not a contradiction. Valuing each wall second at 12288
        /// ticks means a phase reaches its tick box before its wall deadline only when the cadence exceeds
        /// the fastest ever recorded here - i.e. only when the tick axis has genuinely become the binding
        /// one. In every regime already measured (5-10398 ticks/s) each phase still yields on its wall box
        /// exactly as it does today, so this changes no measurement that currently works: probe5's whole
        /// 152-tick schedule and probe7's per-phase wall yields both sit far inside their boxes. What changes
        /// is that the schedule no longer dies when the pot is smaller than the run.
        ///
        /// Floor plus box, not the box alone, so the floor is always payable: a phase whose box rounded to
        /// zero (SwimVerdict, 0.0s) still gets the one tick its handshake needs.
        /// </summary>
        private static int MaxTicksFor(DrivePhase phase)
        {
            // (int) truncation is exact for every phase budget - all ten are whole seconds - and BudgetFor
            // returns 0.0 for SwimVerdict/Idle/Done, which is why the floor is added rather than multiplied.
            return MinTicksFor(phase) + (int)BudgetFor(phase) * DriverTicksPerWallSecondCeiling;
        }

        /// <summary>
        /// Whether the phase currently in flight went PAST its box rather than merely reaching it.
        ///
        /// Called while <c>_phase</c> is still the outgoing phase - every caller runs before
        /// <see cref="CloseCurrentPhase"/> re-bases <c>_phaseStartedAt</c> - so <see cref="PhaseElapsed"/>
        /// and <c>_phaseGranted</c> both still describe that phase. A phase with no box (SwimVerdict,
        /// Idle, Done) cannot exceed one and is excluded rather than being labelled by a slow single tick.
        ///
        /// LATCHED once true, and that is not an optimisation. The answer is asked twice per phase close -
        /// once by AppendPhaseCeilingNote composing the row's prose, once by CeilingYield writing the
        /// ledger - and each ask re-read the clock, so a phase ending within microseconds of
        /// <c>box + tolerance</c> could have printed TIMEBOXED in the row and recorded WallCeiling in the
        /// ledger. Elapsed time only grows inside a phase, so latching cannot change the answer; it only
        /// removes the window where the two readers disagree.
        /// </summary>
        private static bool PhaseExceededItsBox()
        {
            if (_phaseBoxExceeded)
                return true;

            _phaseBoxExceeded = _phaseGranted > 0.0 &&
                PhaseElapsed > _phaseGranted + PhaseBoxOvershootToleranceSeconds;
            return _phaseBoxExceeded;
        }

        /// <summary>
        /// The reason to record when a WORK phase yields on its ceiling. Three distinguishable outcomes,
        /// and the ORDER of the tests is the point:
        ///
        /// A phase that blew its OWN box is the culprit and is labelled TIMEBOXED even when that same
        /// overshoot is what spent the schedule's total on the same tick. Testing <c>_compressed</c> first
        /// would label the 138.192s ResourceDeplete phase TotalCeiling - whose documented meaning is
        /// "compressed by an EARLIER phase's overrun, read this row as UNMEASURED" - and send every reader
        /// looking at the phase before the one that actually ate the clock. That is the same
        /// blame-the-wrong-phase mistake the whole per-phase ledger exists to stop making.
        /// </summary>
        private static PhaseYield CeilingYield()
        {
            if (PhaseExceededItsBox())
                return PhaseYield.Timeboxed;

            // CULPRIT LABELS BEFORE VICTIM LABELS, on both axes, for the reason above. A phase that spent
            // its own tick box is the tick-axis culprit and must not be filed as TotalTickCeiling, whose
            // documented meaning is "an EARLIER phase ate the pot, read this row as UNMEASURED".
            if (PhaseExceededItsTickBox())
                return PhaseYield.PhaseTickCeiling;

            if (_tickCompressed)
                return PhaseYield.TotalTickCeiling;

            return _compressed ? PhaseYield.TotalCeiling : PhaseYield.WallCeiling;
        }

        /// <summary>
        /// The reason to record when a TIMED HOLD phase yields. Reaching the wall ceiling is that
        /// phase's designed completion, so it is not a failure - with two exceptions that are opposite
        /// errors and must not share a label:
        ///   TIMEBOXED, the hold ran far LONGER than designed, so any threshold the row met may have been
        ///     met by an accident of duration and the schedule paid for it.
        ///   TotalCeiling, the hold ran SHORTER than designed because the schedule's total was already
        ///     spent, so any threshold the row missed was never given the time to be crossed.
        /// </summary>
        private static PhaseYield HoldYield()
        {
            if (PhaseExceededItsBox())
                return PhaseYield.Timeboxed;

            // A hold that ran out of TICKS is the third error and it is not Completed: it held for LESS wall
            // time than designed because the cadence spent its share of the pot early, so a threshold it
            // missed was never given the duration to be crossed. Filing that as Completed is how a depth span
            // of 0.000m would read as a swimmer that never moved.
            if (PhaseExceededItsTickBox())
                return PhaseYield.PhaseTickCeiling;

            if (_tickCompressed)
                return PhaseYield.TotalTickCeiling;

            return _compressed ? PhaseYield.TotalCeiling : PhaseYield.Completed;
        }

        private static void AdvancePhase()
        {
            _phaseTicks++;

            // PRE-EMPTION, and it is the guarantee rather than the mechanism. Every clause of
            // PhaseCeilingReached is only as good as the phase body that ASKS it, and "each phase remembers
            // to ask" is a property of eleven method bodies plus every one added later. This is the same
            // discipline enforced from outside them, so a phase that never asks still cannot hold the
            // schedule: it loses the phase, not the run, and its row says which precondition it died on.
            if (_phaseTickBox > 0 && _phaseTicks > _phaseTickBox + PhaseTickBoxGraceTicks)
            {
                ForceYieldStalledPhase();
                return;
            }

            switch (_phase)
            {
                case DrivePhase.Settle:
                    TickSettle();
                    break;
                case DrivePhase.SwimSurface:
                    TickSwimSurface();
                    break;
                case DrivePhase.SwimDive:
                    TickSwimDive();
                    break;
                case DrivePhase.SwimVerdict:
                    LatchSwimVerdict();
                    EnterPhase(DrivePhase.ResourceTarget, PhaseYield.Completed);
                    break;
                case DrivePhase.ResourceTarget:
                    TickResourceTarget();
                    break;
                case DrivePhase.ToolEquip:
                    TickToolEquip();
                    break;
                case DrivePhase.ToolUse:
                    TickToolUse();
                    break;
                case DrivePhase.ResourceDeplete:
                    TickResourceDeplete();
                    break;
                case DrivePhase.ResourcePickup:
                    TickResourcePickup();
                    break;
                case DrivePhase.Craft:
                    TickCraft();
                    break;
                case DrivePhase.VerbSweep:
                    TickVerbSweep();
                    break;
            }
        }

        /// <summary>
        /// Ends a phase that ran past its tick box WITHOUT asking <see cref="PhaseCeilingReached"/>, and
        /// hands the schedule to that phase's own failure successor.
        ///
        /// It latches the phase's row rather than leaving it to <see cref="FinaliseUnlatchedRows"/> because
        /// the two produce different claims and only one of them is true here. The finalisation text says
        /// "the schedule stopped" - it did not; only this phase did, and the phases after it still ran their
        /// floors. NOT_EXERCISED is still the verdict: a pre-empted phase measured nothing, and the named
        /// precondition is what the reader needs, not a manufactured Fail.
        /// </summary>
        private static void ForceYieldStalledPhase()
        {
            DrivePhase stalled = _phase;
            int row = RowOwnedBy(stalled);
            if (row >= 0 && !_latched[row])
            {
                _detail.Clear();
                _detail.Append("PHASE PRE-EMPTED: ").Append(stalled.ToString()).Append(" ran ")
                    .Append(_phaseTicks)
                    .Append(" driver ticks - past its ").Append(_phaseTickBox)
                    .Append("-tick box plus ").Append(PhaseTickBoxGraceTicks)
                    .Append(" ticks of grace - without ever reaching its own ceiling test, so the schedule ")
                    .Append("pre-empted it and the phases after it still got their tick floors. This row is ")
                    .Append("UNKNOWN, not negative, and the phase body is the defect: it has a path that ")
                    .Append("returns without asking PhaseCeilingReached");
                AppendWaitReasonNote();
                AppendPhaseCeilingNote();
                Latch(row, RowVerdict.NotExercised);
            }

            EnterPhase(CeilingSuccessorOf(stalled), PhaseYield.PhaseTickCeiling);
        }

        /// <summary>
        /// Which phase a pre-empted phase hands off to: the successor that phase's OWN ceiling path uses,
        /// not the next enum member.
        ///
        /// The two differ where it matters. ToolEquip's ceiling paths go to ResourceDeplete and skip ToolUse,
        /// because ToolUse holds PrimaryFire for a tool that was never equipped and would spend its whole box
        /// proving nothing; ResourceDeplete's ceiling path goes to Craft and skips ResourcePickup for the same
        /// class of reason. Walking the enum instead would hand each pre-emption to a phase its predecessor
        /// has already established cannot work.
        /// </summary>
        private static DrivePhase CeilingSuccessorOf(DrivePhase phase)
        {
            switch (phase)
            {
                case DrivePhase.Settle:
                    return DrivePhase.ResourceTarget;
                case DrivePhase.SwimSurface:
                    return DrivePhase.SwimDive;
                case DrivePhase.SwimDive:
                    return DrivePhase.SwimVerdict;
                case DrivePhase.SwimVerdict:
                    return DrivePhase.ResourceTarget;
                case DrivePhase.ResourceTarget:
                    return DrivePhase.ToolEquip;
                case DrivePhase.ToolEquip:
                case DrivePhase.ToolUse:
                    return DrivePhase.ResourceDeplete;
                case DrivePhase.ResourceDeplete:
                case DrivePhase.ResourcePickup:
                    return DrivePhase.Craft;
                case DrivePhase.Craft:
                    return DrivePhase.VerbSweep;
                default:
                    return DrivePhase.Done;
            }
        }

        /// <summary>
        /// Which row a phase would have latched, or -1 for a phase that holds none. The inverse of
        /// <see cref="TerminalPhaseFor"/> and deliberately NOT derived from it: several phases feed one row,
        /// so the mapping is many-to-one in this direction and a reversed lookup table would have to pick a
        /// winner. VerbSweep returns -1 by design - it runs after every row has latched and is forbidden to
        /// reach a verdict.
        /// </summary>
        private static int RowOwnedBy(DrivePhase phase)
        {
            switch (phase)
            {
                case DrivePhase.Settle:
                case DrivePhase.SwimSurface:
                case DrivePhase.SwimDive:
                case DrivePhase.SwimVerdict:
                    return RowSwim;
                case DrivePhase.ResourceTarget:
                case DrivePhase.ResourceDeplete:
                case DrivePhase.ResourcePickup:
                    return RowResource;
                case DrivePhase.ToolEquip:
                case DrivePhase.ToolUse:
                    return RowTool;
                case DrivePhase.Craft:
                    return RowCraft;
                default:
                    return -1;
            }
        }

        /// <summary>
        /// Waits for the owners the rows are judged against, then opens the gameplay input map through
        /// the real entry point the UI layer uses when a menu closes.
        ///
        /// THREE INDEPENDENT GATES stand between this driver and locomotion, and collapsing them into one
        /// boolean is exactly how this front stayed mis-diagnosed for a cycle - a row that printed
        /// "inputService=False" was read as "input is disabled" when it meant "there is no input service
        /// at all":
        ///   GATE 1, service presence. GlobalRegistry.RegisteredInput (GlobalRegistry.cs:949) is the RAW
        ///     slot and returns null when nothing is registered; it does not substitute the NoOp proxy the
        ///     way GlobalRegistry.Input does. Every consumer null-checks it before anything else
        ///     (HectonPlayerInputHandler.cs:37, HectonPlayerMovement.cs:7992, PlayerToolManager.cs:414), so
        ///     a null slot suppresses input for a reason that has nothing to do with action maps.
        ///   GATE 2, IInputService.IsPlayerInputEnabled. Resolves to
        ///     _nativeInputManager.IsPlayerInputEnabled (InputDispatcher.cs:409) and from there to
        ///     InputActionMap.enabled (InputManager.cs:277). SwitchToPlayerInput below is the same public
        ///     entry PauseMenuController.cs:728 and HectonFabricatorUI.cs:644 use when a menu closes - the
        ///     driver opens the gate the way a human's first input does, and never forces the flag or reads
        ///     past it.
        ///   GATE 3, the input block mask, sampled in SampleObservables and printed with the Swim row. It
        ///     zeroes the folded override AFTER both gates above are open (InputDispatcher.cs:3060), so a
        ///     nonzero mask is a third, separately named failure.
        /// A settle that gives up now names the gate that was shut instead of printing one ambiguous flag.
        /// </summary>

        /// <summary>
        /// Product-correct locomotion readiness for scripted swim/route holds:
        /// force-close PDA / fabricator / pause via their real close APIs (restoring player maps),
        /// then always SwitchToPlayerInput. Called every settle and swim hold tick so a mid-window
        /// SwitchToUIInput or sticky IsOpen cannot starve hop2 (GetState) for the whole phase.
        /// </summary>
        private static void EnsureGameplayLocomotionInputReady()
        {
            // PDA — public ForceClose restores player input map.
            if (Hecton8.UI.PlayerPDA.IsOpen)
            {
                Hecton8.UI.PlayerPDA pda = Hecton8.UI.PlayerPDA.ActiveRuntimeInstance;
                if (pda == null)
                {
                    IPlayerRuntimeContext player = GlobalRegistry.RegisteredPlayer;
                    if (player != null)
                        pda = player.PlayerPDA as Hecton8.UI.PlayerPDA;
                }

                if (pda != null)
                    pda.ForceClose();
            }

            // Pause — public Close -> ApplyClosedState(restorePlayerInput: true).
            if (Hecton8.UI.PauseMenuController.IsAnyOpen)
            {
                Hecton8.UI.PauseMenuController pause = Hecton8.UI.PauseMenuController.ActiveRuntimeInstance;
                if (pause != null)
                    pause.Close();
            }

            // Fabricator — public ForceCloseMenu (product API added for this path).
            if (Hecton8.UI.HectonFabricatorUI.IsMenuOpen)
            {
                Hecton8.UI.HectonFabricatorUI fab =
                    UnityEngine.Object.FindFirstObjectByType<Hecton8.UI.HectonFabricatorUI>();
                if (fab != null)
                    fab.ForceCloseMenu();
            }

            IInputService input = GlobalRegistry.RegisteredInput;
            if (input != null)
            {
                input.SwitchToPlayerInput();
                _switchedToPlayerInput = true;
            }

            // L13: Re-assert HPM Player fixed-tick registration so SampleGameplay/GetState (hop2)
            // runs during Swim. Suit/juice no longer gate sampling (HPM FixedTick L13), but a
            // missed TryRegisterFixedTickable still starves the entire locomotion read path.
            // Fully-qualify type: editor asm has no using Hecton8.Gameplay (CS0246 on bare name).
            Hecton8.Gameplay.HectonPlayerMovement movement =
                _movement
                ?? UnityEngine.Object.FindFirstObjectByType<Hecton8.Gameplay.HectonPlayerMovement>();
            if (movement != null)
            {
                movement.EnsureDispatcherRegistration();
                if (_movement == null)
                    _movement = movement;
            }
        }

        private static void TickSettle()
        {
            IPlayerRuntimeContext player = GlobalRegistry.RegisteredPlayer;
            if (player != null)
            {
                if (_survival == null)
                    _survival = player.SurvivalSystem;
                if (_movement == null)
                    _movement = player.PlayerMovement;
                if (_toolManager == null)
                    _toolManager = player.ToolManager;
            }

            TryResolveInteraction();

            // Force-close any leftover menu (PDA/Fab/Pause) and re-assert the player action map
            // every settle tick. L10 measured hop2-starve: SampleGameplayLocomotionInputForFixedStep
            // short-circuits on IsGameplayInputBlockedByMenu without calling GetState, and a single
            // SwitchToPlayerInput at first settle loses to later SwitchToUIInput (MainMenu bind,
            // sticky IsOpen). Product close APIs only — no mock input path.
            EnsureGameplayLocomotionInputReady();
            IInputService input = GlobalRegistry.RegisteredInput;

            bool ready = _survival != null && _movement != null && input != null;
            if (!ready)
            {
                // Named PER GATE rather than as one "not ready", for the same reason the three booleans this
                // row used to print were replaced: an empty registry slot and an unassembled player root are
                // different defects with different owners.
                _waitReason = input == null
                    ? WaitReason.InputServiceNotRegistered
                    : WaitReason.PlayerOwnersNotRegistered;

                if (!PhaseCeilingReached())
                    return;
            }

            if (!ready)
            {
                LatchSettleBlocked(input);
                EnterPhase(DrivePhase.ResourceTarget, CeilingYield());
                return;
            }

            _oxygenAtStart = _survival.Oxygen;
            _pressureAtStart = _survival.Pressure;
            _swimBaselineTaken = true;
            EnterPhase(DrivePhase.SwimSurface, PhaseYield.Completed);
        }

        /// <summary>
        /// Names the gate the settle phase died on. The previous version printed three bare booleans and
        /// the third one, labelled "inputService", was the presence check - so a missing service and a
        /// closed action map produced the SAME text and two cycles of work aimed at the wrong mechanism.
        /// Every branch below states the mechanism and where its owner lives, because a row detail that
        /// cannot be acted on is not a diagnostic.
        ///
        /// Runs at most once per run: the caller only reaches it after the settle budget expires, and Latch
        /// refuses a second write to a latched row.
        /// </summary>
        private static void LatchSettleBlocked(IInputService input)
        {
            _detail.Clear();
            _detail.Append("no drivable player after ").Append(F(PhaseElapsed))
                .Append("s: survival=").Append(_survival != null)
                .Append(" movement=").Append(_movement != null)
                .Append(" inputServiceRegistered=").Append(input != null)
                .Append(" inputEnabled=").Append(_inputEnabledEverObserved)
                .Append(" switchToPlayerInputCalled=").Append(_switchedToPlayerInput)
                .Append(" blockMask=0x").Append(_inputBlockMaskLast.ToString("X8", CultureInfo.InvariantCulture));

            if (input == null)
            {
                _detail.Append(" - BLOCKER GATE 1 (service absent, NOT disabled): GlobalRegistry's Input ")
                    .Append("slot is empty, so IsPlayerInputEnabled was never reachable and the leading ")
                    .Append("null check in HectonPlayerInputHandler.cs:37 rejected every frame. The slot's ")
                    .Append("only producer is GameBootstrapper.EnsureInputDispatcherRegistered ")
                    .Append("(GameBootstrapper.cs:6309), which runs once in BootstrapPhase.Player - if the ")
                    .Append("dispatcher it creates does not outlive the scene it was created in, ")
                    .Append("OnDestroy -> TryUnregisterInputService empties the slot and nothing refills it");
            }
            else if (!_inputEnabledEverObserved)
            {
                _detail.Append(" - BLOCKER GATE 2 (service present, map closed): SwitchToPlayerInput was ")
                    .Append("invoked on the registered service and IsPlayerInputEnabled still never read ")
                    .Append("true, so InputManager.EnablePlayerInput did not leave the Player action map ")
                    .Append("enabled (InputManager.cs:1019 and :277). Nothing this driver may legitimately ")
                    .Append("do can open that gate - the owner has to");
            }
            else
            {
                _detail.Append(" - BLOCKER: the input route was OPEN; the missing owner is a player ")
                    .Append("component (HectonSurvivalSystem / HectonPlayerMovement off ")
                    .Append("GlobalRegistry.RegisteredPlayer), not input");
            }

            AppendPhaseCeilingNote();
            Latch(RowSwim, RowVerdict.Blocked);
        }

        /// <summary>
        /// Deterministic yaw sweep for the locomotion phases.
        /// </summary>
        /// <remarks>
        /// LookDelta was hardwired to Vector2.zero in every phase, so the driver never turned the camera and
        /// content discovery was confined to whatever the spawn orientation happened to face.
        /// TryAdoptNearbyWorldNode gates on ExistingNodeMinForwardDot 0.5 - a 60 degree half-cone - within
        /// ExistingNodeMaxDistanceMeters 3.5, and PlayerInteraction's hover raycast follows the same camera.
        /// A node four metres away or seventy degrees off-axis was invisible, which is why the harness had to
        /// register its own scatter point 1.75 m dead ahead to make the Resource row reachable at all: the
        /// success path was content the driver placed for itself.
        ///
        /// Derived from the tick counter, never from a clock or Random, so two runs sweep identically -
        /// determinism is already fragile enough on this lane without the driver adding to it. One full
        /// revolution per 120 ticks at 3 degrees each, applied as a per-tick yaw delta because LookDelta is
        /// a mouse-style delta rather than an absolute angle. Pitch stays zero: the vertical axis is driven
        /// through VerticalDelta, and pitching the camera would fight the immersion and depth sampling the
        /// Swim row measures.
        /// </remarks>
        private static Vector2 SweepLookDelta()
        {
            return new Vector2(SweepYawDegreesPerTick, 0f);
        }

        private static void TickSwimSurface()
        {
            // Keep menus closed and player map enabled for the whole hold. One-shot settle is not
            // enough if anything re-opens UI mid-window (L10: hop2 never fired).
            EnsureGameplayLocomotionInputReady();

            // Forward plus ascend. VerticalDelta is the surface/dive axis
            // (HectonPlayerInputHandler.cs:37-53 reads it straight off the snapshot).
            _intent.MoveDelta = new Vector2(0f, 1f);
            _intent.LookDelta = SweepLookDelta();
            _intent.VerticalDelta = 1f;
            _intent.ActionsBitmask = 0u;
            _intent.CurrentInputSchemeHash = 0u;

            // A hold is not blocked on anything, and saying so explicitly matters: without it a hold would
            // report the previous phase's reason, and a reader would chase a precondition on a phase whose
            // only job is to occupy wall time.
            _waitReason = WaitReason.LocomotionHoldInProgress;

            if (PhaseCeilingReached())
                EnterPhase(DrivePhase.SwimDive, HoldYield());
        }

        private static void TickSwimDive()
        {
            EnsureGameplayLocomotionInputReady();

            _intent.MoveDelta = new Vector2(0f, 1f);
            _intent.LookDelta = SweepLookDelta();
            _intent.VerticalDelta = -1f;
            _intent.ActionsBitmask = (uint)PlayerInputAction.Sprint;
            _intent.CurrentInputSchemeHash = 0u;
            _waitReason = WaitReason.LocomotionHoldInProgress;

            if (PhaseCeilingReached())
            {
                // Do not clear _intent here: Tick publishes after AdvancePhase, so the last dive
                // frame must still ship MoveDelta. Non-authoring phases clear after publish.
                EnterPhase(DrivePhase.SwimVerdict, HoldYield());
            }
        }

        /// <summary>
        /// Swim row acceptance. Three independent facts, and the row only passes when all three hold:
        ///   1. the driver's MoveDelta reached the locomotion owner (CurrentMovementIntent01 moved),
        ///   2. depth actually spanned a range (surface AND dive happened, not just one),
        ///   3. oxygen/pressure were readable and moved, corroborated by the vitals lane.
        /// Anything less is Partial or Blocked with the measured numbers attached, because "the harness
        /// ran" is not the same claim as "the route is proven".
        /// </summary>
        private static void LatchSwimVerdict()
        {
            if (_latched[RowSwim])
                return;

            if (!_swimBaselineTaken)
                return;

            // Sentinel-safe: _depthMin seeds at float.MaxValue and _depthMax at float.MinValue, so an
            // unsampled fold reads as ordered-backwards rather than as a 6.8e38 metre span. Reporting the
            // sentinel as a measurement is exactly the silent-degeneracy trap this project keeps hitting.
            bool depthSampled = _depthMax >= _depthMin;
            float depthMinShown = depthSampled ? _depthMin : 0f;
            float depthMaxShown = depthSampled ? _depthMax : 0f;
            float depthSpan = depthSampled ? _depthMax - _depthMin : 0f;
            float oxygenDelta = Mathf.Abs(_oxygenLast - _oxygenAtStart);
            float pressureDelta = Mathf.Abs(_pressureLast - _pressureAtStart);
            bool intentReachedMovement = _maxMovementIntent >= MinMovementIntent01;
            bool depthMoved = depthSpan >= MinDepthSpanMeters;
            bool vitalsMoved =
                oxygenDelta >= MinOxygenDelta ||
                pressureDelta >= MinPressureDelta ||
                _sawVitalsOxygenFlag ||
                _sawVitalsPressureFlag ||
                _sawVitalsDepthFlag;

            _detail.Clear();
            _detail.Append("driver published ").Append(_publishedOverrides)
                .Append(" input overrides; movementIntent01max=").Append(F(_maxMovementIntent))
                .Append(" immersionMax=").Append(F(_maxImmersion))
                .Append(" depthSampled=").Append(depthSampled)
                .Append(" depth=").Append(F(depthMinShown)).Append("..").Append(F(depthMaxShown))
                .Append(" span=").Append(F(depthSpan))
                .Append("m oxygen ").Append(F(_oxygenAtStart)).Append("->").Append(F(_oxygenLast))
                .Append(" pressure ").Append(F(_pressureAtStart)).Append("->").Append(F(_pressureLast))
                .Append(" vitalsFlags[o2=").Append(_sawVitalsOxygenFlag)
                .Append(" pressure=").Append(_sawVitalsPressureFlag)
                .Append(" depth=").Append(_sawVitalsDepthFlag)
                .Append("] inputServiceRegistered=").Append(_inputServiceEverObserved)
                .Append(" inputEnabled=").Append(_inputEnabledEverObserved)
                .Append(" switchToPlayerInputCalled=").Append(_switchedToPlayerInput)
                .Append(" blockMask=0x").Append(_inputBlockMaskLast.ToString("X8", CultureInfo.InvariantCulture))
                .Append(" pdaOpen=").Append(Hecton8.UI.PlayerPDA.IsOpen)
                .Append(" fabOpen=").Append(Hecton8.UI.HectonFabricatorUI.IsMenuOpen)
                .Append(" pauseOpen=").Append(Hecton8.UI.PauseMenuController.IsAnyOpen)
                .Append(" inputEnabledNow=").Append(
                    GlobalRegistry.RegisteredInput != null &&
                    GlobalRegistry.RegisteredInput.IsPlayerInputEnabled);

            if (!_inputServiceEverObserved)
            {
                _detail.Append(" - BLOCKER GATE 1: GlobalRegistry's Input slot was EMPTY for the whole ")
                    .Append("window. No IInputService was ever registered, so IsPlayerInputEnabled was ")
                    .Append("never reachable and the leading null check in HectonPlayerInputHandler.cs:37 ")
                    .Append("rejected every frame. This is a registration lifetime defect, not a disabled ")
                    .Append("action map");
                Latch(RowSwim, RowVerdict.Blocked);
                return;
            }

            if (!_inputEnabledEverObserved)
            {
                _detail.Append(" - BLOCKER GATE 2: the service was registered but ")
                    .Append("IInputService.IsPlayerInputEnabled was false for the whole window, so ")
                    .Append("HectonPlayerInputHandler.TryReadFrame refused every frame and no locomotion ")
                    .Append("producer can reach movement in this configuration");
                Latch(RowSwim, RowVerdict.Blocked);
                return;
            }

            // GATE 3, narrowed to the ONE bit that can erase locomotion. BlockLook, BlockTools and
            // BlockDiscrete leave MoveDelta and VerticalDelta untouched (InputDispatcher.cs:3121-3141), so
            // failing the Swim row on them would be a false negative on a route that did move.
            if ((_inputBlockMaskLast & (uint)Hecton8.Core.InputBlockMaskFlags.BlockMovement) != 0u)
            {
                _detail.Append(" - BLOCKER GATE 3: both input gates were open but BlockMovement was set, ")
                    .Append("and ApplyInputBlockMask (InputDispatcher.cs:3121) zeroes MoveDelta and ")
                    .Append("VerticalDelta AFTER the driver's override is folded in, so the intent was ")
                    .Append("erased before any consumer could see it. DropPodSeatController.cs:505 is the ")
                    .Append("only setter of that mask in this project");
                Latch(RowSwim, RowVerdict.Blocked);
                return;
            }

            if (intentReachedMovement && depthMoved && vitalsMoved)
            {
                Latch(RowSwim, RowVerdict.Pass);
                return;
            }

            if (intentReachedMovement)
            {
                _detail.Append(" - navigate observed, but ");
                if (!depthMoved)
                    _detail.Append("depth never spanned ").Append(F(MinDepthSpanMeters)).Append("m ");
                if (!vitalsMoved)
                    _detail.Append("no oxygen/pressure/depth change was published ");
                _detail.Append("- row NOT accepted");

                // The depth span is a function of how long the two hold phases actually held. If they
                // were compressed to their tick floors, "depth never spanned 0.25m" is a statement about
                // the schedule, not about the swimmer, and the row has to say which.
                AppendClosedPhaseNote(DrivePhase.SwimSurface);
                AppendClosedPhaseNote(DrivePhase.SwimDive);
                Latch(RowSwim, RowVerdict.Partial);
                return;
            }

            _detail.Append(" - FAIL: the input path was open but the driver's MoveDelta never reached ")
                .Append("HectonPlayerMovement");
            AppendClosedPhaseNote(DrivePhase.SwimSurface);
            AppendClosedPhaseNote(DrivePhase.SwimDive);
            Latch(RowSwim, RowVerdict.Fail);
        }

        /// <summary>
        /// Obtains a resource node the honest way, in priority order:
        ///   1. an existing world node near and in front of the player — real authored/scattered content,
        ///   2. failing that, register a spawn point on ScavengePopulator.RegisterSpawnPoint, the same
        ///      public producer entry point HectonScatterOutput.cs:184 and HectonVoxelEngine.cs:15202 use,
        ///      and let the populator's own ProcessSpawnQueue instantiate it.
        /// Placement in front of the player is a scatter decision, not a player mutation: the driver
        /// chooses where world content goes and never moves the player to it.
        /// </summary>
        private static void TickResourceTarget()
        {
            if (_node != null && !_node.IsDepleted)
            {
                EnterPhase(DrivePhase.ToolEquip, PhaseYield.Completed);
                return;
            }

            if (TryAdoptNearbyWorldNode())
            {
                _nodeFromWorld = true;
                EnterPhase(DrivePhase.ToolEquip, PhaseYield.Completed);
                return;
            }

            _waitReason = WaitReason.ResourceNodeNotAvailable;

            if (!_spawnPointRegistered)
            {
                TryRegisterDriverSpawnPoint();

                // The registration attempt is bounded by its own lookup counters, so falling through to
                // the ceiling test costs nothing and stops a phase whose populator never appears from
                // holding the schedule open until its wall budget runs out.
                if (!PhaseCeilingReached())
                    return;
            }

            // The populator drains its queue on ISlowTickable cadence. SlowTick() is public and is the
            // owner's own entry point, so calling it forces the drain without reaching inside.
            ScavengePopulator populator = _populator;
            if (populator != null &&
                populator.IsServiceReady &&
                _forcedPopulatorSlowTicks < MaxForcedPopulatorSlowTicks)
            {
                _forcedPopulatorSlowTicks++;
                populator.SlowTick();
            }

            if (TryAdoptNearbyWorldNode())
            {
                EnterPhase(DrivePhase.ToolEquip, PhaseYield.Completed);
                return;
            }

            if (!PhaseCeilingReached())
                return;

            int registryCount = Hecton8.Scavenging.ResourceNode.WorldStateRegistryCount;
            _detail.Clear();
            _detail.Append("no interactable resource node exists: ResourceNode world-state registry=")
                .Append(registryCount)
                .Append(" nodes, none within ").Append(F(ExistingNodeMaxDistanceMeters))
                .Append("m and in front of the player; populator=");

            if (populator == null)
                _detail.Append("absent from every loaded scene");
            else if (!_populatorReady)
                _detail.Append("present but IsServiceReady=false");
            else
                _detail.Append("ready, spawnPointRegistered=").Append(_spawnPointRegistered)
                    .Append(" activeNodesBefore=").Append(_populatorNodesAtRegister)
                    .Append(" activeNodesNow=").Append(populator.TotalActiveNodes)
                    .Append(" pending=").Append(populator.PendingSpawnCount)
                    .Append(" (a null SelectResourcePrefab means the scene's ScavengePopulator loot ")
                    .Append("tables are unauthored)");

            AppendPhaseCeilingNote();
            Latch(RowResource, RowVerdict.Blocked);
            EnterPhase(DrivePhase.ToolEquip, CeilingYield());
        }

        /// <summary>
        /// Scans the node owner's own static world-state registry — allocation-free, unlike a scene
        /// search — for a live node the player could actually reach and see.
        /// </summary>
        private static bool TryAdoptNearbyWorldNode()
        {
            if (!TryReadPlayerEye(out Vector3 eyePosition, out Vector3 eyeForward))
                return false;

            int count = Hecton8.Scavenging.ResourceNode.WorldStateRegistryCount;
            float bestDistanceSq = ExistingNodeMaxDistanceMeters * ExistingNodeMaxDistanceMeters;
            Hecton8.Scavenging.ResourceNode best = null;

            for (int i = 0; i < count; i++)
            {
                Hecton8.Scavenging.ResourceNode candidate = Hecton8.Scavenging.ResourceNode.GetWorldStateRegistryAt(i);
                if (candidate == null || candidate.IsDepleted || !candidate.gameObject.activeInHierarchy)
                    continue;

                Vector3 toCandidate = candidate.transform.position - eyePosition;
                float distanceSq = toCandidate.sqrMagnitude;
                if (distanceSq > bestDistanceSq || distanceSq <= 0.0001f)
                    continue;

                if (Vector3.Dot(eyeForward, toCandidate / Mathf.Sqrt(distanceSq)) < ExistingNodeMinForwardDot)
                    continue;

                bestDistanceSq = distanceSq;
                best = candidate;
            }

            if (best == null)
                return false;

            _node = best;
            return true;
        }

        private static void TryRegisterDriverSpawnPoint()
        {
            if (_populator == null)
            {
                if (_populatorLookupAttempts >= MaxPopulatorLookupAttempts)
                    return;

                _populatorLookupAttempts++;
                _populator = UnityEngine.Object.FindFirstObjectByType<ScavengePopulator>(FindObjectsInactive.Exclude);
                if (_populator == null)
                    return;
            }

            _populatorReady = _populator.IsServiceReady;
            if (!_populatorReady)
                return;

            if (!TryReadPlayerEye(out Vector3 eyePosition, out Vector3 eyeForward))
                return;

            Vector3 spawnPosition = eyePosition + eyeForward * NodePlacementDistanceMeters;

            // Chunk coord uses the populator's own floor-division convention
            // (ScavengePopulator.WorldToChunkCoord, :1121) with the authored default tile size. The
            // producer supplies this coord; a mismatch only affects the populator's streaming
            // bookkeeping, and the node itself is then located through ResourceNode's world-state
            // registry rather than through that bookkeeping.
            Vector2Int chunkCoord = new Vector2Int(
                Mathf.FloorToInt(spawnPosition.x / ScavengeTileSizeMeters),
                Mathf.FloorToInt(spawnPosition.z / ScavengeTileSizeMeters));

            _populatorNodesAtRegister = _populator.TotalActiveNodes;
            _populator.RegisterSpawnPoint(
                spawnPosition,
                Quaternion.identity,
                Vector3.one,
                chunkCoord,
                ScavengeLocalIndex,
                Hecton8.Caves.SpawnContext.Surface);
            _spawnPointRegistered = true;
        }

        /// <summary>
        /// Selects a tool slot through the REAL discrete lane. PlayerToolManager consumes
        /// SignalBus&lt;PlayerInputSignal&gt; ToolSlot1..ToolSlot4 exactly as it does for a key press;
        /// SwitchToSlot is not called directly, so the swap state machine, availability gate and
        /// loadout signals all run unmodified.
        /// </summary>
        private static void TickToolEquip()
        {
            // L19 hop2 LIVE: batch peel TickToolEquip - native Crash!!! at Latch/ceiling path
            // (stack: TickToolEquip -> AdvancePhase -> Tick -> Probe.Tick) after STARTERGRANT.
            // Skip tool slot publish / PlayerToolManager swap under batch so schedule can reach RESULT.
            if (UnityEngine.Application.isBatchMode)
            {
                if (!PhaseCeilingReached())
                    return;
                _detail.Clear();
                _detail.Append("L19 hop2 LIVE: TickToolEquip peeled under batch (native crash path)");
                Latch(RowTool, RowVerdict.Blocked);
                EnterPhase(DrivePhase.ResourceDeplete, CeilingYield());
                return;
            }
            Hecton8.Gameplay.PlayerToolManager manager = _toolManager;
            if (manager == null)
            {
                IPlayerRuntimeContext player = GlobalRegistry.RegisteredPlayer;
                manager = player?.ToolManager;
                _toolManager = manager;
            }

            if (manager == null)
            {
                _waitReason = WaitReason.ToolManagerAbsent;

                if (!PhaseCeilingReached())
                    return;

                _detail.Clear();
                _detail.Append("no PlayerToolManager published on the player runtime context, so no tool ")
                    .Append("slot can be selected");
                AppendWaitReasonNote();
                AppendPhaseCeilingNote();
                Latch(RowTool, RowVerdict.Blocked);
                EnterPhase(DrivePhase.ResourceDeplete, CeilingYield());
                return;
            }

            if (!_toolSlotSignalPublished)
            {
                int slotCount = manager.SlotCount;
                _availableToolSlots = 0;
                int chosen = -1;
                for (int slot = 0; slot < slotCount && slot < 4; slot++)
                {
                    if (!manager.IsToolAvailableInSlot(slot))
                        continue;

                    _availableToolSlots++;
                    if (chosen < 0)
                        chosen = slot;
                }

                if (chosen < 0)
                {
                    // THE PRECONDITION probe7 SPENT 27180 TICKS ON AND NEVER NAMED. That run recorded
                    // discreteSignals=0 dropped=0 (Logs/h8_probe7.log:22845), so PublishDiscreteCommand was
                    // never called even once and the phase was sitting in exactly this branch - yet the row
                    // it produced said only "the schedule stopped in phase ToolEquip ... Stop cause:
                    // OwnTickCeiling", which is true of all four of this phase's waits.
                    _waitReason = WaitReason.NoToolAvailableInAnySlot;

                    if (!PhaseCeilingReached())
                        return;

                    // "No tool exists to select" was the WRONG claim to make from this observation, and it is
                    // the claim this row used to make. IsToolAvailableInSlot is
                    // `prefab != null && HasToolInInventory(prefab)` (PlayerToolManager.cs:927-933), so a
                    // fully authored loadout reports false for every slot the moment the INVENTORY is off -
                    // and PlayerInventory.Awake switches itself off on a DTO-layout or vault-bind failure
                    // (PlayerInventory.cs:1364, :1387). Two unrelated defects, one red row, and the row named
                    // the wrong one. The note below decides which of them this run actually hit.
                    _detail.Clear();
                    _detail.Append("PlayerToolManager reports slotCount=").Append(slotCount)
                        .Append(" and IsToolAvailableInSlot is false for every slot, so no tool could be ")
                        .Append("selected on this route");
                    AppendInventoryUpstreamNote();
                    AppendWaitReasonNote();
                    AppendPhaseCeilingNote();
                    Latch(RowTool, RowVerdict.Blocked);
                    EnterPhase(DrivePhase.ResourceDeplete, CeilingYield());
                    return;
                }

                _requestedToolSlot = chosen;
                _toolSlotSignalPublished =
                    PublishDiscreteCommand((byte)(PlayerInputSignalCommands.ToolSlot1 + chosen));

                if (_toolSlotSignalPublished)
                {
                    _waitReason = WaitReason.ToolSwapNotConfirmed;
                    return;
                }

                _waitReason = WaitReason.ToolSlotCommandRefusedByLane;

                // A DROPPED push is the one path in this phase that could loop forever: the flag stays
                // false, the next tick re-enumerates the slots, chooses the same slot, and tries again
                // with no ceiling test between attempts, so the ceiling below is what bounds it.
                //
                // The retry cadence is still correct and is deliberately NOT increased. Requirement-shaped
                // temptation here is to push harder; every one of the four refusal paths says that is useless.
                // Three of them (no ring, load shed, guard rejection) are independent of cadence, and the
                // fourth - a full ring - is refusing precisely because nobody is DRAINING it, so a second push
                // in the same frame cannot fit either. One attempt per pumped frame is the fastest cadence that
                // gives the dispatcher's PostSimulation flush a chance to run between attempts, which is the
                // only thing that can free a slot. What was missing was never pressure, it was the CAUSE:
                // CaptureDiscreteRefusal latches it on the first refusal and the detail below names it.
                //
                // An earlier version of this comment asserted "TryPushTracked returns false when the lane is
                // full, which is a lane-capacity fact". That was the seed of a false verdict. TryPush has four
                // independent refusal paths (SignalBusRuntime.cs:678-715) and capacity is only one of them; the
                // silent one - a lane that was never given native storage - is both more likely on this harness
                // and attributable to a completely different owner. See the forensics block beside
                // _discreteRefusalCaptured.
                if (!PhaseCeilingReached())
                    return;

                _detail.Clear();
                _detail.Append("SignalBus<PlayerInputSignal> refused the ToolSlot")
                    .Append(chosen + 1)
                    .Append(" push on every attempt: pushed=").Append(_publishedDiscreteSignals)
                    .Append(" dropped=").Append(_droppedDiscreteSignals)
                    // RELABELLED, not renamed for taste. This counter is PlayerToolManager slots holding an
                    // available tool; the previous text printed it as "availableSlots" inside a sentence about
                    // SignalBus capacity, which invited every reader to treat a tool-manager number as free
                    // lane slots. It is kept because it proves the phase HAD a slot to press - the press is
                    // what failed - but it is now named for the subsystem it came from.
                    .Append(" toolSlotsWithAvailableTool=").Append(_availableToolSlots)
                    .Append(" - no consumer ever saw the command");
                AppendDiscreteRefusalNote();
                AppendWaitReasonNote();
                AppendPhaseCeilingNote();
                Latch(RowTool, RowVerdict.NotExercised);
                EnterPhase(DrivePhase.ResourceDeplete, CeilingYield());
                return;
            }

            Hecton8.Gameplay.PlayerTool current = manager.CurrentTool;
            if (current != null && manager.CurrentSlotIndex == _requestedToolSlot)
            {
                _toolEquipped = true;
                _equippedSlotIndex = manager.CurrentSlotIndex;
                _durabilityAtToolUse = current.CurrentDurability;
                _durabilityReadable = true;
                _nodeHealthAtToolUse = _node != null ? _node.CurrentHealth : 0f;
                EnterPhase(DrivePhase.ToolUse, PhaseYield.Completed);
                return;
            }

            _waitReason = WaitReason.ToolSwapNotConfirmed;

            if (!PhaseCeilingReached())
                return;

            _detail.Clear();
            _detail.Append("published PlayerInputSignal command ToolSlot").Append(_requestedToolSlot + 1)
                .Append(" on the PLIN lane (toolSlotsWithAvailableTool=").Append(_availableToolSlots)
                .Append(", pushed=").Append(_publishedDiscreteSignals)
                .Append(", dropped=").Append(_droppedDiscreteSignals)
                .Append(", commandSeenInFlushedSnapshot=").Append(_toolSlotCommandFlushObserved)
                .Append(", laneSnapshotEntriesThisRun=").Append(_lanePlayerInputSignals)
                .Append(") but CurrentTool stayed null and CurrentSlotIndex=")
                .Append(manager.CurrentSlotIndex).Append(" after ").Append(F((float)PhaseElapsed))
                .Append("s at dispatcherFrame=").Append(SystemDispatcher.CurrentFrameId);

            // THE VERDICT NOW DEPENDS ON DELIVERY, and it did not before. The old text asserted "the discrete
            // lane was accepted and the swap never completed" and latched Fail - a defect written against
            // PlayerToolManager - on the strength of TryPush returning true. TryPush returning true only
            // proves the payload entered the RING. PlayerToolManager reads the FRAME SNAPSHOT
            // (PlayerToolManager.cs:1954), which the dispatcher's PostSimulation flush fills
            // (SystemDispatcher.cs:3036), and that flush can drop the queued overflow past its per-frame limit
            // (SignalBusRuntime.cs:944-953) or not run for this lane at all. On the run that motivated this
            // change the census recorded PlayerInputSignal[PLIN]=0 for all 152 ticks
            // (Logs/h8_worldsim_probe5.log:10915) - not one PLIN entry was ever visible in a flushed snapshot -
            // so "accepted" was never true of anything a consumer could read, and a Fail on the tool system
            // would have been evidence-free. Undelivered is BLOCKED with the delivery owner named; delivered
            // and unhandled is the only honest Fail.
            if (_toolSlotCommandFlushObserved)
            {
                _detail.Append(" - the command WAS visible in a flushed frame snapshot and the swap still ")
                    .Append("never completed, so this is PlayerToolManager's own state machine");
                AppendWaitReasonNote();
                AppendPhaseCeilingNote();
                Latch(RowTool, RowVerdict.Fail);
                EnterPhase(DrivePhase.ResourceDeplete, CeilingYield());
                return;
            }

            _detail.Append(" - the push entered the ring but the command was NEVER visible in a flushed frame ")
                .Append("snapshot, so PlayerToolManager was never given the chance to read it and this row ")
                .Append("measures DELIVERY, not the tool system. Owner is the PostSimulation flush of ")
                .Append("SignalBus<PlayerInputSignal> (SystemDispatcher.cs:3036 -> ")
                .Append("SignalCorridorRuntime.FlushPostSimulation -> SignalBusRuntime.cs:890)");
            AppendWaitReasonNote();
            AppendPhaseCeilingNote();
            Latch(RowTool, RowVerdict.Blocked);
            EnterPhase(DrivePhase.ResourceDeplete, CeilingYield());
        }

        /// <summary>
        /// Holds PrimaryFire on the continuous lane. Tool firing is NOT on the signal lane:
        /// PlayerToolManager polls inputState.HasAction(PrimaryFire) (PlayerToolManager.cs:418) and calls
        /// PlayerTool.UsePrimary itself, so the only honest way to fire a tool is to be the input
        /// snapshot's producer.
        ///
        /// The row's "useful" half is proven by a downstream effect the driver did not write: node
        /// health falling, or the tool spending its own durability doing work.
        /// </summary>
        private static void TickToolUse()
        {
            // L19 hop2 LIVE: batch peel TickToolUse - depends on equipped tool; skip under batch.
            if (UnityEngine.Application.isBatchMode)
            {
                if (!PhaseCeilingReached())
                    return;
                EnterPhase(DrivePhase.ResourceDeplete, CeilingYield());
                return;
            }
            _intent.MoveDelta = Vector2.zero;
            _intent.LookDelta = Vector2.zero;
            _intent.VerticalDelta = 0f;
            _intent.ActionsBitmask = (uint)PlayerInputAction.PrimaryFire;
            _intent.CurrentInputSchemeHash = 0u;
            _waitReason = WaitReason.ToolPrimaryFireHoldInProgress;

            if (!PhaseCeilingReached())
                return;

            // Captured before the detail is composed and before EnterPhase re-bases _phaseStartedAt.
            // This is the MEASURED hold duration; the line below used to print the ToolUseBudgetSeconds
            // constant in its place and call it "held PrimaryFire for 5.000s". On the measured run one
            // pumped frame cost 132 seconds, so that claim could be wrong by a factor of 25 and it was
            // formatted exactly like a measurement.
            double heldSeconds = PhaseElapsed;

            // Intent clear deferred to post-publish (PhaseAuthorsInputIntent) so the final
            // PrimaryFire frame still reaches CaptureState this tick.
            Hecton8.Gameplay.PlayerToolManager manager = _toolManager;
            Hecton8.Gameplay.PlayerTool current = manager != null ? manager.CurrentTool : null;
            _durabilityAfterToolUse = current != null ? current.CurrentDurability : _durabilityAtToolUse;
            _nodeHealthAfterToolUse = _node != null ? _node.CurrentHealth : _nodeHealthAtToolUse;

            float healthDelta = _nodeHealthAtToolUse - _nodeHealthAfterToolUse;
            float durabilityDelta = _durabilityAtToolUse - _durabilityAfterToolUse;
            bool nodeDamaged = _node != null && healthDelta >= MinNodeHealthDelta;
            bool toolSpentItself = _durabilityReadable && durabilityDelta >= MinDurabilityDelta;

            _detail.Clear();
            _detail.Append("equipConfirmed=").Append(_toolEquipped)
                .Append(" slot ").Append(_equippedSlotIndex)
                .Append(" via the PLIN ToolSlot").Append(_requestedToolSlot + 1)
                .Append(" signal (tool=").Append(current != null ? current.GetType().Name : "null")
                .Append("), then held PrimaryFire on the input snapshot for ")
                .Append(F(heldSeconds)).Append("s over ").Append(_phaseTicks)
                .Append(" driver ticks: nodeHealth ")
                .Append(F(_nodeHealthAtToolUse)).Append("->").Append(F(_nodeHealthAfterToolUse))
                .Append(" durability ").Append(F(_durabilityAtToolUse)).Append("->")
                .Append(F(_durabilityAfterToolUse))
                .Append(" inputEnabled=").Append(_inputEnabledEverObserved);

            if (nodeDamaged || toolSpentItself)
            {
                _detail.Append(" - equip and downstream effect both observed");
                Latch(RowTool, RowVerdict.Pass);
            }
            else
            {
                _detail.Append(" - equip observed, no downstream effect");
                if (!_inputEnabledEverObserved)
                    _detail.Append("; IsPlayerInputEnabled was false, so PlayerToolManager never read the ")
                        .Append("snapshot and UsePrimary was never called");
                else if (_node == null)
                    _detail.Append("; there was no resource node in front of the player for the tool to act on");

                // The lane counts turn "no downstream effect" from an observation into a diagnosis. A zero
                // ToolLoadoutChangedSignal count means the swap never published on its only producer's lane
                // (PlayerToolManager.cs:833) even though CurrentSlotIndex read back correctly, which is a
                // different defect from a tool that swapped and then did nothing.
                _detail.Append("; lanes ToolLoadoutChangedSignal=").Append(_laneToolLoadoutChanged)
                    .Append(" DebrisSpawnSignal=").Append(_laneDebrisSpawn)
                    .Append(" ResourceDepletionDeltaSignal=").Append(_laneResourceDepletionDelta);

                // Verdict split, and this is the point of the whole disambiguation: an inventory that is
                // switched off upstream makes this row UNMEASURABLE, and calling that Partial would file it as
                // "tool use half works". BLOCKED is the probe's word for "attempted and obstructed at runtime"
                // (H8_HeadlessPlayModeProbe.cs:1881), which is exactly what a disabled inventory does.
                bool inventoryUpstreamDown =
                    !_inventoryComponentPresent || !_inventoryComponentEnabled || !_inventoryGridBound;

                _detail.Append(inventoryUpstreamDown
                    ? " - row NOT accepted, and NOT attributed to tool use"
                    : " - row NOT accepted");
                AppendInventoryUpstreamNote();
                AppendPhaseCeilingNote();
                Latch(RowTool, inventoryUpstreamDown ? RowVerdict.Blocked : RowVerdict.Partial);
            }

            EnterPhase(DrivePhase.ResourceDeplete, HoldYield());
        }

        /// <summary>
        /// Finishes the node off through ResourceNode.ApplyInteractionSignal (ResourceNode.cs:535) — the
        /// method the shipping dispatcher actually calls for THIS target type — so the node runs its own
        /// TrySpawnLoot and produces a real PickupItem.
        ///
        /// IT USED TO CALL ApplyCutDamage, AND THAT WAS THE BUG. ResourceNode implements both ICuttable and
        /// IInteractionSignalConsumer (ResourceNode.cs:22). EquipmentInteractionHandler.DispatchCutDamage
        /// resolves the consumer FIRST (EquipmentInteractionHandler.cs:929-933) and only falls back to
        /// ICuttable.ApplyCutDamage (:935-936) for targets that do not implement it — a fallback the shipping
        /// code therefore never takes for a resource node. The driver was exercising the one path the product
        /// does not use, and that path hardcodes ToolCapabilityMasks.Cut (ResourceNode.cs:519).
        ///
        /// Cut is unreachable content, not a near miss: NOT ONE of the 27 authored ResourceNodeTemplate
        /// assets under Assets/_Project/Data/Scavenging/ResourceNodes sets requiredToolClass=Knife, so
        /// ApplyCutDamage can never damage an authored node. The measured row —
        /// vulnerabilityMask=0x00000020, bit 5, ToolCapabilityMasks.Laser — was the template being correct
        /// and the driver asking with the wrong verb.
        ///
        /// CENSUS, re-measured 2026-07-29 by counting requiredToolClass across all 27 assets: Any(0)=8,
        /// Drill(2)=6, Laser(3)=13, Knife(1)=0, Salvage(4)=0. Sums to 27. An earlier revision of this
        /// comment said "7 are Any, 6 Drill, 12 Laser, 2 Salvage" and was wrong on three of the four terms.
        /// It mattered because those phantom 2 Salvage templates were then reported below as authored content
        /// stranded without an interaction verb — a content gap this instrument invented. There is no Salvage
        /// content and no Knife content. Only the Knife/Cut conclusion above survives re-measurement, and it
        /// survives more strongly: Knife is zero of 27, exactly as claimed.
        ///
        /// The verb is now READ from the node every tick and never hardcoded, so a template retuned from
        /// Laser to Drill needs no change here.
        ///
        /// Deliberately AFTER the tool phase: the tool gets first claim on the damage, and this leg never
        /// contributes to the Tool row's verdict.
        /// </summary>
        private static void TickResourceDeplete()
        {
            // L19 hop2 LIVE: batch peel TickResourceDeplete - native Crash!!! at
            // PersistentWorldRegistry.TryResolveRegistryChunkId via ApplyNodeDamagePulses
            // (stack: TickResourceDeplete -> TakeDamage -> RegisterPersistentDepletion).
            // Skip real node damage under batch so schedule can advance past ResourceDeplete.
            if (UnityEngine.Application.isBatchMode)
            {
                if (!PhaseCeilingReached())
                    return;
                _detail.Clear();
                _detail.Append("L19 hop2 LIVE: TickResourceDeplete peeled under batch (PWR chunk resolve AV)");
                Latch(RowResource, RowVerdict.Blocked);
                EnterPhase(DrivePhase.VerbSweep, CeilingYield());
                return;
            }
            Hecton8.Scavenging.ResourceNode node = _node;
            if (node == null)
            {
                // A node that depleted and then went away is the SUCCESS path, not a missing node: the
                // loot prefab outlives it. Skipping straight to Craft here would discard a pickup that is
                // sitting in the world waiting to be interacted with.
                EnterPhase(
                    _nodeDepleted ? DrivePhase.ResourcePickup : DrivePhase.Craft,
                    PhaseYield.Completed);
                return;
            }

            if (node.IsDepleted)
            {
                _nodeDepleted = true;
                EnterPhase(DrivePhase.ResourcePickup, PhaseYield.Completed);
                return;
            }

            ApplyNodeDamagePulses(node);
            _waitReason = WaitReason.NodeIntegrityNotDepleted;

            if (!PhaseCeilingReached())
                return;

            if (_latched[RowResource])
            {
                EnterPhase(DrivePhase.Craft, CeilingYield());
                return;
            }

            _detail.Clear();
            AppendResourceDepleteDetail(node);

            // THE row this front exists for. On the measured run this phase reported 138.192s against a
            // 6.0s budget, and the reason is not that the test was missing: it is that the test is only
            // reachable at tick boundaries and ONE pumped frame in this phase cost about 132 seconds
            // (123 of the run's 124 frames cost ~0.23s each). The ticks count above is what makes that
            // legible - "138.192s" alone reads like 600 damage applications and it was a handful.
            AppendPhaseCeilingNote();
            Latch(RowResource, RowVerdict.Blocked);
            EnterPhase(DrivePhase.Craft, CeilingYield());
        }

        /// <summary>
        /// Applies identical tool pulses to the adopted node through a capability the node actually accepts.
        ///
        /// The mask is re-read from the node EVERY tick, not snapshotted: ResourceNode.VulnerabilityMask
        /// (ResourceNode.cs:220) recomputes from the applied template, and ScavengePopulator can restamp a
        /// template after the spawn (ScavengePopulator.cs:869). The effect type is then resolved through the
        /// owner's own effect-type-to-capability table (EquipmentInteractionContracts.cs:96), so this driver
        /// holds no second copy of the capability contract and cannot disagree with it.
        /// </summary>
        private static void ApplyNodeDamagePulses(Hecton8.Scavenging.ResourceNode node)
        {
            uint vulnerabilityMask = node.VulnerabilityMask;
            if (!_nodeDamageEffectResolved || vulnerabilityMask != _nodeVulnerabilityMask)
            {
                if (_nodeDamagePulses == 0)
                    _nodeHealthAtDepleteStart = node.CurrentHealth;

                _nodeDamageEffectResolved = true;
                _nodeVulnerabilityMask = vulnerabilityMask;
                _nodeDamageEffectAccepted = TryResolveNodeDamageEffect(
                    vulnerabilityMask,
                    out _nodeDamageEffect,
                    out _nodeDamageCapabilityMask);
            }

            if (!_nodeDamageEffectAccepted)
                return;

            Vector3 hitPoint = node.transform.position;
            for (int pulse = 0; pulse < MaxNodeDamagePulsesPerTick; pulse++)
            {
                float healthBeforePulse = node.CurrentHealth;
                Hecton8.Interaction.InteractionSignal signal = BuildNodeDamageSignal(hitPoint);
                node.ApplyInteractionSignal(in signal, hitPoint);
                _nodeDamagePulses++;

                if (node.IsDepleted)
                {
                    _nodeDamagePulsesLanded++;
                    _nodeDepleted = true;
                    return;
                }

                // A pulse that changed nothing is one of three real mechanics: the capability gate refused it
                // (ResourceNode.cs:541), the template's steam-explosion route consumed it
                // (ResourceNode.cs:548-552), or depletion was reached and rolled back because TrySpawnLoot
                // failed (ResourceNode.cs:1199-1203). Spinning the remaining pulses against any of them would
                // burn the tick and teach the row nothing, so the batch stops and the detail names which.
                if (node.CurrentHealth >= healthBeforePulse)
                    return;

                _nodeDamagePulsesLanded++;
            }
        }

        /// <summary>
        /// Picks the first effect type whose resolved capability mask intersects the node's vulnerability
        /// mask.
        ///
        /// The order is a taste decision, not an implementation detail. PlasmaCut first because the
        /// LaserCutter publishes exactly that effect type (LaserCutter.cs:1718) with capability
        /// PlasmaCut = Cut|Burn|Laser (LaserCutter.cs:2531), and Tool_LaserCutter_Held is authored into
        /// starter slot 3 of Player.prefab — so for a node that accepts more than one verb the driver uses
        /// the one the player actually carries at minute zero. Drill second: it is the only verb that reaches
        /// the metal-vein class, and SeafloorDrillTool publishes it (SeafloorDrillTool.cs:222). The rest are
        /// scanned last so an authored mask nobody anticipated still resolves instead of blocking the row.
        ///
        /// Returning false is a real finding, not a fallback to force. The driver reports it instead of
        /// reaching for ResourceNode.TakeDamage(float) (ResourceNode.cs:568), which has NO capability gate
        /// and would turn any genuine content gap into a green row.
        ///
        /// CORRECTED 2026-07-29. This paragraph used to justify itself with "the 2 Salvage-class templates
        /// have no interaction verb at all". There are ZERO Salvage-class templates: counting
        /// requiredToolClass across all 27 assets in Assets/_Project/Data/Scavenging/ResourceNodes gives
        /// Any(0)=8, Drill(2)=6, Laser(3)=13, Knife(1)=0, Salvage(4)=0. The stranded-content claim was
        /// manufactured by this comment, and a reader could have spent a day looking for two assets that do
        /// not exist. It is still TRUE that no effect type resolves to ToolCapabilityMasks.Salvage in
        /// ResolveCapabilityMask, so the guard is still correct and still worth keeping — it is unreached by
        /// authored content today, which makes it a guard against future content, not evidence of a gap.
        /// </summary>
        private static bool TryResolveNodeDamageEffect(
            uint vulnerabilityMask,
            out Hecton8.Interaction.InteractionEffectType effectType,
            out uint capabilityMask)
        {
            for (int i = 0; i < NodeDamageEffectPreferenceCount; i++)
            {
                Hecton8.Interaction.InteractionEffectType candidate = NodeDamageEffectAtPreference(i);
                uint candidateMask = Hecton8.Interaction.ToolCapabilityMasks.ResolveCapabilityMask(candidate);
                if (candidateMask == 0u || (vulnerabilityMask & candidateMask) == 0u)
                    continue;

                effectType = candidate;
                capabilityMask = candidateMask;
                return true;
            }

            effectType = Hecton8.Interaction.InteractionEffectType.PlasmaCut;
            capabilityMask = 0u;
            return false;
        }

        /// <summary>
        /// Preference order as a switch rather than a static array: an array field would be a cold managed
        /// allocation for six enum values, and a switch cannot silently disagree with
        /// <see cref="NodeDamageEffectPreferenceCount"/> without the default arm showing up in the report.
        /// </summary>
        private static Hecton8.Interaction.InteractionEffectType NodeDamageEffectAtPreference(int index)
        {
            switch (index)
            {
                case 0:
                    return Hecton8.Interaction.InteractionEffectType.PlasmaCut;
                case 1:
                    return Hecton8.Interaction.InteractionEffectType.Drill;
                case 2:
                    return Hecton8.Interaction.InteractionEffectType.Torch;
                case 3:
                    return Hecton8.Interaction.InteractionEffectType.Weld;
                case 4:
                    return Hecton8.Interaction.InteractionEffectType.Boil;
                default:
                    return Hecton8.Interaction.InteractionEffectType.Harpoon;
            }
        }

        /// <summary>
        /// Builds one tool pulse shaped exactly like the LaserCutter's (LaserCutter.cs:1703-1719): a packet
        /// carrying tool id, pose, power, range, mode and state flags, wrapped in a signal carrying the
        /// delivered power, the hit normal and the effect type.
        ///
        /// Power and PowerDelivered are both NodeDamagePerPulse so ResourceNode.TakeDamage receives
        /// amount == toolPower (ResourceNode.cs:555-557) — numerically identical to what ApplyCutDamage passed
        /// (ResourceNode.cs:523-530). Damage, incremental yield and debris therefore do not drift by one gram
        /// against the leg this replaces, and for the common PlasmaCut case the loot-oracle tool mask is also
        /// unchanged: ResolveLootOracleToolMask maps PlasmaCut to ToolMaskCutter (ResourceNode.cs:966-969),
        /// which is the exact value ApplyCutDamage set (ResourceNode.cs:522).
        ///
        /// TargetInstanceID stays 0. It is the queue's collider-identity field, and this signal is handed
        /// straight to the consumer instead of published, so nothing resolves it: ResourceNode's consumer
        /// (ResourceNode.cs:535-566) reads PowerDelivered, EffectType, Source.Power and HitNormal only. A
        /// fabricated id would be a value no receiver checks. HitPoint is unread on this route for the same
        /// reason — the absolute-universe convention belongs to the publish path
        /// (EquipmentInteractionHandler.cs:842) — so it carries the runtime point it was taken from rather
        /// than a fake AUP triple.
        /// </summary>
        private static Hecton8.Interaction.InteractionSignal BuildNodeDamageSignal(Vector3 hitPoint)
        {
            Vector3 origin = hitPoint + Vector3.up * NodeDamageRangeMeters;
            Hecton8.Interaction.InteractionPacket packet = new Hecton8.Interaction.InteractionPacket(
                NodeDamageToolId,
                new Unity.Mathematics.float3(origin.x, origin.y, origin.z),
                new Unity.Mathematics.float3(0f, -1f, 0f),
                NodeDamagePerPulse,
                NodeDamageRangeMeters,
                (byte)Hecton8.Interaction.ToolActionMode.Primary,
                (byte)Hecton8.Interaction.ToolStateBits.Active,
                SystemDispatcher.CurrentFrameId);

            return new Hecton8.Interaction.InteractionSignal(
                packet,
                0,
                new Unity.Mathematics.float3(hitPoint.x, hitPoint.y, hitPoint.z),
                new Unity.Mathematics.float3(0f, 1f, 0f),
                NodeDamagePerPulse,
                (byte)_nodeDamageEffect,
                0);
        }

        /// <summary>
        /// Reports the leg in terms a reader can act on: which capability the node accepts BY NAME, which
        /// verb the driver chose, how many pulses were attempted, how many landed, and which of the four
        /// distinguishable outcomes occurred.
        ///
        /// The message this replaces asserted "the template does not accept the Cut capability" from a
        /// hardcoded assumption. It happened to be true, and it still told the reader to go and decode bit 5
        /// by hand. Every clause below is measured.
        /// </summary>
        private static void AppendResourceDepleteDetail(Hecton8.Scavenging.ResourceNode node)
        {
            _detail.Append("node '").Append(node.UniqueId)
                .Append("' would not deplete: health=").Append(F(_nodeHealthAtDepleteStart))
                .Append("->").Append(F(node.CurrentHealth))
                .Append(" normalized=").Append(F(node.HealthNormalized))
                .Append(" vulnerabilityMask=0x")
                .Append(_nodeVulnerabilityMask.ToString("X8", CultureInfo.InvariantCulture))
                .Append('[');
            AppendCapabilityNames(_nodeVulnerabilityMask);
            _detail.Append("] requiredToolClass=").Append(ResolveHarvestToolClassName(node))
                .Append(" after ").Append(F((float)PhaseElapsed))
                .Append("s / ").Append(_phaseTicks).Append(" driver ticks - ");

            if (!_nodeDamageEffectAccepted)
            {
                _detail.Append("NO InteractionEffectType resolves to that capability, so no verb in ")
                    .Append("ToolCapabilityMasks.ResolveCapabilityMask can damage this node at all. That is a ")
                    .Append("gap in the tool capability table, not a driver setting, and no PickupItem was ")
                    .Append("ever produced");
                return;
            }

            _detail.Append("driverEffect=").Append(ResolveEffectTypeName(_nodeDamageEffect))
                .Append(" capability=0x")
                .Append(_nodeDamageCapabilityMask.ToString("X8", CultureInfo.InvariantCulture))
                .Append('[');
            AppendCapabilityNames(_nodeDamageCapabilityMask);
            _detail.Append("] pulses=").Append(_nodeDamagePulses)
                .Append(" landed=").Append(_nodeDamagePulsesLanded).Append(' ');

            if (_nodeDamagePulsesLanded == 0)
            {
                if (NodeTemplateTriggersSteamExplosion(node))
                    _detail.Append("- the capability WAS accepted and every pulse was absorbed by the ")
                        .Append("template's steam-explosion route (ResourceNode.cs:548-552): ")
                        .Append("triggersSteamExplosionWithoutThermalShield is set and no ThermalShield ")
                        .Append("upgrade is present, so damage is refused by design. This node needs a ")
                        .Append("thermal-shielded tool, not a driver change");
                else
                    _detail.Append("- the masks intersect but not one pulse reduced integrity, so the refusal ")
                        .Append("is downstream of CanApplyToolCapability (ResourceNode.cs:595)");

                _detail.Append(", so no PickupItem was ever produced");
                return;
            }

            if (_nodeDamagePulses > _nodeDamagePulsesLanded)
                _detail.Append("- damage landed and then a pulse stopped changing integrity, which is the ")
                    .Append("failed-TrySpawnLoot rollback in ResourceNode.TakeDamage (:1199-1203): depletion ")
                    .Append("WAS reached and refused because the loot could not be queued");
            else
                _detail.Append("- damage landed on every pulse and the leg ran out of ticks before integrity ")
                    .Append("reached zero");

            _detail.Append(", so no PickupItem was ever produced");
        }

        /// <summary>
        /// Writes the set capability bits by name so a reader never has to decode a hex mask by hand. This is
        /// the whole reason the front existed: "vulnerabilityMask=0x00000020" cost a full investigation to
        /// mean "Laser".
        /// </summary>
        private static void AppendCapabilityNames(uint mask)
        {
            int written = 0;
            written = AppendCapabilityName(mask, Hecton8.Interaction.ToolCapabilityMasks.Cut, "Cut", written);
            written = AppendCapabilityName(mask, Hecton8.Interaction.ToolCapabilityMasks.Drill, "Drill", written);
            written = AppendCapabilityName(mask, Hecton8.Interaction.ToolCapabilityMasks.Grab, "Grab", written);
            written = AppendCapabilityName(mask, Hecton8.Interaction.ToolCapabilityMasks.Stun, "Stun", written);
            written = AppendCapabilityName(mask, Hecton8.Interaction.ToolCapabilityMasks.Burn, "Burn", written);
            written = AppendCapabilityName(mask, Hecton8.Interaction.ToolCapabilityMasks.Laser, "Laser", written);
            written = AppendCapabilityName(mask, Hecton8.Interaction.ToolCapabilityMasks.Bash, "Bash", written);
            written = AppendCapabilityName(mask, Hecton8.Interaction.ToolCapabilityMasks.Salvage, "Salvage", written);
            if (written == 0)
                _detail.Append("none");
        }

        private static int AppendCapabilityName(uint mask, uint bit, string name, int written)
        {
            if ((mask & bit) == 0u)
                return written;

            if (written > 0)
                _detail.Append('|');

            _detail.Append(name);
            return written + 1;
        }

        /// <summary>
        /// Switch, not Enum.ToString: the hot-path law bans Enum.ToString outright and a latch path that
        /// allocates once per run is still a habit worth not forming. A null template is reported explicitly
        /// because ResolveRequiredToolCapabilityMask returns uint.MaxValue for it (ResourceNode.cs:603-608) —
        /// an all-bits mask is "accepts everything", not "accepts nothing", and the two read alike in hex.
        /// </summary>
        private static string ResolveHarvestToolClassName(Hecton8.Scavenging.ResourceNode node)
        {
            Hecton8.Scavenging.ResourceNodeTemplate template = node.ResourceTemplate;
            if (template == null)
                return "<none: no template applied, so the mask defaults to uint.MaxValue>";

            switch (template.RequiredToolClass)
            {
                case Hecton8.Scavenging.ResourceNodeTemplate.HarvestToolClass.Any:
                    return "Any";
                case Hecton8.Scavenging.ResourceNodeTemplate.HarvestToolClass.Knife:
                    return "Knife";
                case Hecton8.Scavenging.ResourceNodeTemplate.HarvestToolClass.Drill:
                    return "Drill";
                case Hecton8.Scavenging.ResourceNodeTemplate.HarvestToolClass.Laser:
                    return "Laser";
                case Hecton8.Scavenging.ResourceNodeTemplate.HarvestToolClass.Salvage:
                    return "Salvage";
                default:
                    return "<unmapped>";
            }
        }

        private static string ResolveEffectTypeName(Hecton8.Interaction.InteractionEffectType effectType)
        {
            switch (effectType)
            {
                case Hecton8.Interaction.InteractionEffectType.Drill:
                    return "Drill";
                case Hecton8.Interaction.InteractionEffectType.Harpoon:
                    return "Harpoon";
                case Hecton8.Interaction.InteractionEffectType.Weld:
                    return "Weld";
                case Hecton8.Interaction.InteractionEffectType.PlasmaCut:
                    return "PlasmaCut";
                case Hecton8.Interaction.InteractionEffectType.Torch:
                    return "Torch";
                case Hecton8.Interaction.InteractionEffectType.Boil:
                    return "Boil";
                default:
                    return "<unmapped>";
            }
        }

        private static bool NodeTemplateTriggersSteamExplosion(Hecton8.Scavenging.ResourceNode node)
        {
            Hecton8.Scavenging.ResourceNodeTemplate template = node.ResourceTemplate;
            return template != null && template.TriggersSteamExplosionWithoutThermalShield;
        }

        /// <summary>
        /// Waits for the player's own throttled hover probe to acquire the dropped PickupItem, then
        /// publishes the Interact command. Nothing about the hover is faked: PlayerInteraction resolves
        /// it with its own raycast against InteractableRegistry, so a pass here means the item was really
        /// in reach and really in front of the camera.
        ///
        /// The row's observable is ItemAcquiredSignal with SourceKind ManualPickup — proof the item
        /// traversed the real acquisition path. An inventory count would only prove a list accepts an
        /// element.
        /// </summary>
        private static void TickResourcePickup()
        {
            if (_sawManualPickupAcquire)
            {
                if (!_latched[RowResource])
                {
                    _detail.Clear();
                    _detail.Append("node depleted")
                        .Append(_nodeFromWorld ? " (existing world node)" : " (driver-registered scatter point)")
                        .Append("; its PickupItem was hovered by PlayerInteraction's own raycast and the ")
                        .Append("PLIN Interact command was consumed - ItemAcquiredSignal sourceKind=")
                        .Append(ItemAcquiredSignalSourceKinds.ManualPickup)
                        .Append(" quantity=").Append(_manualPickupQuantity)
                        .Append(" observed on SignalBus<ItemAcquiredSignal>");
                    Latch(RowResource, RowVerdict.Pass);
                }

                EnterPhase(DrivePhase.Craft, PhaseYield.Completed);
                return;
            }

            TryResolveInteraction();

            if (_sawPickupHover && !_interactPublished)
                _interactPublished = PublishDiscreteCommand(PlayerInputSignalCommands.Interact);

            // Two waits, one phase, opposite owners: nothing to press versus pressed and nothing delivered.
            _waitReason = _sawPickupHover
                ? WaitReason.PickupAcquisitionNotPublished
                : WaitReason.PickupNotHovered;

            if (!PhaseCeilingReached())
                return;

            if (!_latched[RowResource])
            {
                _detail.Clear();
                _detail.Append("node depleted=").Append(_nodeDepleted)
                    .Append(", PickupItem hovered=").Append(_sawPickupHover)
                    .Append(", Interact command published=").Append(_interactPublished)
                    .Append(", ItemAcquiredSignal(ManualPickup) observed=false after ")
                    .Append(F((float)PhaseElapsed)).Append("s / ").Append(_phaseTicks)
                    .Append(" driver ticks");

                // The verdict is chosen first and latched last, so the ceiling note lands inside the
                // detail Latch stores. Appending after Latch and re-assigning _details would work and
                // would also be the one place in this file that writes a latched row behind Latch's back.
                RowVerdict verdict;
                if (_interaction == null)
                {
                    _detail.Append(" - INSTRUMENT LIMIT: no PlayerInteraction component was found in ")
                        .Append(MaxInteractionLookupAttempts)
                        .Append(" scene searches, so hover could not be observed at all. This row's ")
                        .Append("verdict is unknown, not negative");
                    verdict = RowVerdict.NotExercised;
                }
                else if (!_sawPickupHover)
                {
                    _detail.Append(" - PlayerInteraction never hovered a PickupItem, so either depletion ")
                        .Append("produced no loot prefab or the drop is outside reach / off the ")
                        .Append("interactable layer mask. The world object did NOT reach inventory");
                    verdict = RowVerdict.Partial;
                }
                else
                {
                    _detail.Append(" - the pickup was hovered and the real Interact command was consumed, ")
                        .Append("but no acquisition was published");
                    verdict = RowVerdict.Fail;
                }

                AppendPhaseCeilingNote();
                Latch(RowResource, verdict);
            }

            EnterPhase(DrivePhase.Craft, CeilingYield());
        }

        /// <summary>
        /// Crafts one recipe on the authored Fabricator through StartCraft (:834). CanCraft is consulted
        /// first and its answer is reported verbatim: a recipe that cannot be crafted because the
        /// resource leg delivered nothing is a content/ordering fact worth reading, not something to
        /// route around by writing ingredients into inventory.
        /// </summary>
        private static void TickCraft()
        {
            if (_sawFabricatorAcquire)
            {
                if (!_latched[RowCraft])
                {
                    _detail.Clear();
                    _detail.Append("StartCraft accepted a recipe on the authored Fabricator and ")
                        .Append("ItemAcquiredSignal sourceKind=")
                        .Append(ItemAcquiredSignalSourceKinds.Fabricator)
                        .Append(" was observed; craftProgressPeak=").Append(F(_craftProgressPeak))
                        .Append(" craftableRecipes=").Append(_craftableRecipeCount)
                        .Append(" of visible=").Append(_visibleRecipeCount);
                    Latch(RowCraft, RowVerdict.Pass);
                }

                EnterPhase(DrivePhase.VerbSweep, PhaseYield.Completed);
                return;
            }

            if (_fabricator == null && _fabricatorLookupAttempts < MaxFabricatorLookupAttempts)
            {
                _fabricatorLookupAttempts++;
                _fabricator = UnityEngine.Object.FindFirstObjectByType<Hecton8.Crafting.Fabricator>(
                    FindObjectsInactive.Exclude);
            }

            Hecton8.Crafting.Fabricator fabricator = _fabricator;
            if (fabricator == null)
            {
                _waitReason = WaitReason.FabricatorAbsent;

                if (!PhaseCeilingReached() &&
                    _fabricatorLookupAttempts < MaxFabricatorLookupAttempts)
                {
                    return;
                }

                _detail.Clear();
                _detail.Append("no live Fabricator component found in ").Append(_fabricatorLookupAttempts)
                    .Append(" scene searches, so no recipe can be started");
                AppendPhaseCeilingNote();
                Latch(RowCraft, RowVerdict.Blocked);
                EnterPhase(DrivePhase.VerbSweep, CeilingYield());
                return;
            }

            if (!_craftStarted)
            {
                // CanCraft walks every ingredient of every recipe against inventory. Re-asking 60 times a
                // second for 14 seconds is a real cost for an answer that only changes when fabricator
                // power or inventory changes, so the sweep runs on a throttle.
                //
                // The throttle is deliberately bypassed once the ceiling is reached, so the final verdict
                // is composed from a fresh sweep instead of one that could be half a second stale. In a
                // compressed schedule the phase has only its tick floor, and skipping the sweep on those
                // ticks would report craftableRecipes=0 without ever having asked.
                double now = EditorApplication.timeSinceStartup;
                if (_craftEvaluatedAt > 0.0 &&
                    now - _craftEvaluatedAt < CraftEvaluationIntervalSeconds &&
                    !PhaseCeilingReached())
                {
                    return;
                }

                _craftEvaluatedAt = now;

                System.Collections.Generic.IReadOnlyList<Hecton8.Crafting.RecipeData> recipes =
                    fabricator.AvailableRecipes;
                _visibleRecipeCount = recipes != null ? recipes.Count : 0;
                _craftableRecipeCount = 0;
                Hecton8.Crafting.RecipeData chosen = null;

                for (int i = 0; i < _visibleRecipeCount; i++)
                {
                    Hecton8.Crafting.RecipeData recipe = recipes[i];
                    if (recipe == null || !fabricator.CanCraft(recipe))
                        continue;

                    _craftableRecipeCount++;
                    if (chosen == null)
                        chosen = recipe;
                }

                if (chosen == null)
                {
                    _waitReason = WaitReason.NoCraftableRecipe;

                    if (!PhaseCeilingReached())
                        return;

                    _detail.Clear();
                    _detail.Append("Fabricator is live with visibleRecipes=").Append(_visibleRecipeCount)
                        .Append(" totalRecipes=").Append(fabricator.TotalRecipeCount)
                        .Append(" lockedRecipes=").Append(fabricator.LockedRecipeCount)
                        .Append(" but CanCraft is false for all of them; the Resource leg delivered ")
                        .Append(_sawManualPickupAcquire ? "1 acquisition" : "nothing")
                        .Append(", so no recipe/repair can consume a resource on this route");
                    AppendPhaseCeilingNote();
                    Latch(RowCraft, RowVerdict.Blocked);
                    EnterPhase(DrivePhase.VerbSweep, CeilingYield());
                    return;
                }

                _craftStarted = true;
                _craftAccepted = fabricator.StartCraft(chosen);

                if (!_craftAccepted)
                {
                    _detail.Clear();
                    _detail.Append("CanCraft approved a recipe and StartCraft then refused it - ")
                        .Append("craftableRecipes=").Append(_craftableRecipeCount)
                        .Append(" of visible=").Append(_visibleRecipeCount)
                        .Append("; the two gates disagree");
                    Latch(RowCraft, RowVerdict.Fail);
                    EnterPhase(DrivePhase.VerbSweep, PhaseYield.Completed);
                    return;
                }

                return;
            }

            _waitReason = WaitReason.CraftDeliveryNotPublished;

            if (!PhaseCeilingReached())
                return;

            _detail.Clear();
            _detail.Append("StartCraft accepted (isCraftingObserved=").Append(_craftObservedRunning)
                .Append(", craftProgressPeak=").Append(F(_craftProgressPeak))
                .Append(") but no ItemAcquiredSignal sourceKind=")
                .Append(ItemAcquiredSignalSourceKinds.Fabricator)
                // MEASURED, not the CraftBudgetSeconds constant this used to print. The constant is 14.0
                // and the phase's real window is min(14.0, whatever the schedule has left), so the old
                // text asserted a 14-second wait on a phase that could have been granted 0.
                .Append(" arrived within ").Append(F((float)PhaseElapsed))
                .Append("s / ").Append(_phaseTicks)
                .Append(" driver ticks - the craft was consumed but never delivered, row NOT accepted");
            AppendPhaseCeilingNote();
            Latch(RowCraft, RowVerdict.Partial);
            EnterPhase(DrivePhase.VerbSweep, CeilingYield());
        }

        // ─────────────────────────────────────────────────────────────────────────────────────────
        //  VERB SWEEP — every remaining player verb, once, on the shipping producer
        // ─────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Presses the 17 <see cref="PlayerInputAction"/> bits in eight fixed groups of two steps each.
        ///
        /// TWO STEPS PER GROUP IS NOT PADDING. The dispatcher's discrete producer is an edge detector over the
        /// RESOLVED input state (InputDispatcher.cs:1050). Tick advances the phase (authors the mask) then
        /// PublishLocomotionIntent ships it the same driver tick; LateFrame CaptureState folds the override
        /// after that. SampleVerbSweepObservables still runs at the START of the phase body, so it reads the
        /// prior frame's resolved state - a mask written on step k is first observable on step k+1, and a
        /// stable edge needs the bit held through step k+1. A one-step-per-group sweep would drop each bit
        /// before its arrival could be observed and would report all fifteen verbs dead on a healthy path.
        ///
        /// The mask is ASSIGNED, never OR-ed, so entering a new group drops the previous group's bits in the
        /// same step. That is deliberate: the drop is what makes the next press of an already-pressed bit a
        /// real 0-to-1 edge, and the PDA sequence below depends on it.
        ///
        /// GROUP ORDER IS A SAFETY CONTRACT, not a preference. Group 4 opens the PDA (ToggleInventory ->
        /// PlayerPDA.HandleInventoryInput, PlayerPDA.cs:2130), group 5 only means anything while it is open
        /// (HandleTabNextInput returns immediately when it is not), and group 6's Pda verb toggles it shut
        /// again (HandlePDAInput enqueues -1 when IsOpen). The sweep therefore ENDS with the PDA closed and the
        /// gameplay input map restored, which matters because the probe's save leg runs immediately after.
        /// </summary>
        private static void TickVerbSweep()
        {
            if (!_verbSweepEntered)
            {
                _verbSweepEntered = true;
                CaptureVerbSweepBaseline();
            }

            SampleVerbSweepObservables();
            _waitReason = WaitReason.VerbSweepStepping;

            int step = _verbSweepStep;
            _verbSweepStep = step + 1;

            if (step >= VerbSweepStepCount)
            {
                // Intent clear deferred to post-publish after EnterPhase(Done).
                FlushVerbSweepLog(false);

                // HoldYield, not a bare Completed. The sweep is a fixed-length hold, so reaching its last step
                // IS its designed completion - but a sweep whose 16 ticks cost far more than its box still owes
                // the reader the TIMEBOXED label instead of a clean green ledger row.
                EnterPhase(DrivePhase.Done, HoldYield());
                return;
            }

            // Locomotion is deliberately dead for the whole sweep: a moving player keeps changing what
            // PlayerInteraction hovers and what depth the survival owner reports, and every observable below
            // is meant to be able to change for exactly one reason.
            _intent.MoveDelta = Vector2.zero;
            _intent.LookDelta = Vector2.zero;
            _intent.VerticalDelta = 0f;
            _intent.CurrentInputSchemeHash = 0u;
            _intent.ActionsBitmask = VerbSweepGroupMask(step >> 1);
            _verbSweepRaisedMask |= _intent.ActionsBitmask;

            for (int verb = 0; verb < VerbCount; verb++)
            {
                if ((_intent.ActionsBitmask & VerbBit(verb)) != 0u)
                    _verbFlags[verb] |= VerbFlagRaised;
            }
        }

        /// <summary>
        /// The bit group for a sweep group index. Groups 0-3 drive independent consumers that cannot interfere;
        /// groups 4-7 are the UI sequence and their ORDER is load-bearing (see <see cref="TickVerbSweep"/>).
        /// Pause rides with Cancel in the last group because nothing consumes it off a snapshot at all -
        /// InputDispatcher.cs:3230 is its only other appearance in the project and that line PRODUCES it - so
        /// it cannot interact with anything.
        /// </summary>
        private static uint VerbSweepGroupMask(int group)
        {
            switch (group)
            {
                case 0:
                    return (uint)PlayerInputAction.Jump |
                        (uint)PlayerInputAction.Sprint |
                        (uint)PlayerInputAction.Dash;
                case 1:
                    return (uint)PlayerInputAction.Interact |
                        (uint)PlayerInputAction.PrimaryFire |
                        (uint)PlayerInputAction.SecondaryFire;
                case 2:
                    return (uint)PlayerInputAction.Flashlight;
                case 3:
                    return (uint)PlayerInputAction.ToolSlot1 |
                        (uint)PlayerInputAction.ToolSlot2 |
                        (uint)PlayerInputAction.ToolSlot3 |
                        (uint)PlayerInputAction.ToolSlot4;
                case 4:
                    return (uint)PlayerInputAction.Inventory;
                case 5:
                    return (uint)PlayerInputAction.TabNext |
                        (uint)PlayerInputAction.TabPrevious;
                case 6:
                    return (uint)PlayerInputAction.Pda;
                default:
                    return (uint)PlayerInputAction.Cancel |
                        (uint)PlayerInputAction.Pause;
            }
        }

        private static void CaptureVerbSweepBaseline()
        {
            _verbSweepLoadoutSignalsAtEntry = _laneToolLoadoutChanged;
            _verbSweepToolSlotAtEntry = _toolManager != null ? _toolManager.CurrentSlotIndex : -1;

            IPlayerRuntimeContext player = GlobalRegistry.RegisteredPlayer;
            Hecton8.Gameplay.PlayerFlashlight flashlight = player != null ? player.Flashlight : null;
            _verbSweepFlashlightOnAtEntry = flashlight != null && flashlight.IsOn;
        }

        /// <summary>
        /// Samples the consumer-side observables EVERY sweep tick rather than only at group boundaries.
        ///
        /// PlayerPDA does not act on a command in the frame it receives it - HandleInventoryInput enqueues a
        /// state command (PlayerPDA.cs:2178) that the PDA's own lane drains - and PlayerToolManager runs its
        /// swap on its own lane too. A boundary-only read would miss a transition that landed one frame late
        /// and report a working verb as dead. Latched booleans, never cleared, so a transition seen once
        /// cannot be un-seen by a later sample.
        /// </summary>
        private static void SampleVerbSweepObservables()
        {
            bool pdaOpen = Hecton8.UI.PlayerPDA.IsOpen;
            if (pdaOpen)
            {
                _verbSweepPdaObservedOpen = true;

                // Attribution windows, and they are deliberately one step LATE rather than one step early.
                // The mask for group 4 is written at the END of step 8, so step 9 is the first tick on which
                // an open PDA can be that verb's doing; crediting it from step 8 would hand the Inventory verb
                // a PDA that something else had already opened. Groups 0-3 raise no UI bit at all, so before
                // step 9 an open PDA is somebody else's and this ledger says nothing about it.
                if (_verbSweepStep >= 9)
                    _verbSweepPdaOpenedByInventoryVerb = true;
            }
            else if (_verbSweepPdaObservedOpen && _verbSweepStep >= 13)
            {
                // Same rule for the close: the Pda verb's mask is written at the end of step 12, so step 13 is
                // the first tick on which a closed PDA can be attributed to it.
                _verbSweepPdaClosedAfterCancel = true;
            }

            IPlayerRuntimeContext player = GlobalRegistry.RegisteredPlayer;
            Hecton8.Gameplay.PlayerFlashlight flashlight = player != null ? player.Flashlight : null;
            if (flashlight != null && flashlight.IsOn != _verbSweepFlashlightOnAtEntry)
                _verbSweepFlashlightFlipped = true;

            Hecton8.Gameplay.PlayerToolManager manager = _toolManager;
            if (manager != null)
            {
                int slot = manager.CurrentSlotIndex;
                if (slot >= 0 && slot != _verbSweepToolSlotAtEntry)
                    _verbSweepToolSlotObserved = slot;
            }
        }

        /// <summary>
        /// Writes the coverage ledger to the run log.
        ///
        /// IT GOES TO Debug.Log AND NOT INTO A ROW, and that is the honest shape. The probe's route table has
        /// exactly four driver-owned rows (H8_HeadlessPlayModeProbe.cs:1807-1810) and each one is a
        /// First-20-Minutes acceptance criterion; a verb census is not one of those criteria, so folding it
        /// into Swim or Tool would either dilute a row that means something specific or invent a fifth row the
        /// probe cannot read. It prints as its own marked block instead, and every line carries the lane, the
        /// expected value, the measured value and the frame - an assert that logs only FAILED wastes the whole
        /// editor run it dies in.
        ///
        /// <paramref name="truncated"/> is true when the schedule was stopped from outside while the sweep was
        /// still walking its steps. A partial sweep is worth printing - eleven of these verbs had never been
        /// pressed once in this project's history - but it must not read as a complete census, so the step it
        /// reached is stated.
        /// </summary>
        private static void FlushVerbSweepLog(bool truncated)
        {
            if (_verbSweepLogged)
                return;

            _verbSweepLogged = true;

            // Its own try/catch, and not because the reads below are risky. This is called from Stop() and
            // from Tick()'s exception handler, and both of those run AFTER the four row verdicts have been
            // written: a throw escaping from here would leave EditorApplication.update carrying an exception
            // out of the probe and lose an entire run's worth of rows for the sake of a diagnostic block.
            try
            {
                WriteVerbSweepLog(truncated);
            }
            catch (System.Exception ex)
            {
                Debug.Log(
                    "[H8_WORLDDRIVER] VERBSWEEP log flush threw " + ex.GetType().Name + ": " + ex.Message +
                    " - the coverage ledger is lost for this run; the four route rows above are unaffected");
            }
        }

        private static void WriteVerbSweepLog(bool truncated)
        {
            // L19 hop2 LIVE: LogVerbRow/WriteVerbSweepLog mono fatal under batch after VERBSWEEP complete.
            if (UnityEngine.Application.isBatchMode)
            {
                Debug.Log("[H8_WORLDDRIVER] VERBSWEEP " + (truncated ? "TRUNCATED" : "complete") +
                    " batch-peel (skipped per-verb LogVerbRow)");
                return;
            }
            int raised = 0;
            int arrived = 0;
            int commanded = 0;
            int expectedCommands = 0;
            int consumerConfirmed = 0;

            for (int verb = 0; verb < VerbCount; verb++)
            {
                byte flags = _verbFlags[verb];
                if ((flags & VerbFlagRaised) == 0)
                    continue;

                raised++;
                if ((flags & VerbFlagArrivedInSnapshot) != 0)
                    arrived++;

                byte expected = VerbExpectedCommand(verb);
                if (expected == 0)
                    continue;

                expectedCommands++;
                if (_commandSeen[expected])
                {
                    _verbFlags[verb] |= VerbFlagCommandObserved;
                    commanded++;
                }
            }

            ApplyVerbConsumerObservations();
            for (int verb = 0; verb < VerbCount; verb++)
            {
                if ((_verbFlags[verb] & VerbFlagConsumerObserved) != 0)
                    consumerConfirmed++;
            }

            _log.Clear();
            _log.Append("[H8_WORLDDRIVER] VERBSWEEP ")
                .Append(truncated ? "TRUNCATED" : "complete")
                .Append(" step=").Append(_verbSweepStep).Append('/').Append(VerbSweepStepCount)
                .Append(" raised=").Append(raised).Append('/').Append(VerbCount)
                .Append(" arrivedInResolvedSnapshot=").Append(arrived).Append('/').Append(raised)
                .Append(" dispatcherCommands=").Append(commanded).Append('/').Append(expectedCommands)
                .Append(" consumerConfirmed=").Append(consumerConfirmed)
                .Append(" | overrideFlagSeen=").Append(_verbSweepOverrideFlagObserved)
                .Append(" overridesPublished=").Append(_publishedOverrides)
                .Append(" inputEnabled=").Append(_inputEnabledEverObserved)
                .Append(" blockMask=0x")
                .Append(_inputBlockMaskLast.ToString("X8", CultureInfo.InvariantCulture))
                .Append(" lastResolvedButtons=0x")
                .Append(_verbSweepSnapshotButtonsLast.ToString("X8", CultureInfo.InvariantCulture))
                .Append(" atFrame=").Append(_verbSweepSnapshotFrameLast);

            if (raised > 0 && arrived == 0)
                _log.Append(" - NOTHING ARRIVED: not one raised bit appeared in ")
                    .Append("IInputService.CurrentInputState.ButtonsBitmask, so the failure is upstream of ")
                    .Append("every consumer and no verb below is a consumer verdict. Read overrideFlagSeen ")
                    .Append("first: false means TryConsumeLatestInputOverride never accepted the publish ")
                    .Append("(InputDispatcher.cs:3345); true with a nonzero blockMask means ")
                    .Append("ApplyInputBlockMask erased the bits after the fold (InputDispatcher.cs:3199)");

            Debug.Log(_log.ToString());

            for (int verb = 0; verb < VerbCount; verb++)
                LogVerbRow(verb);

            LogLaneCensus();
        }

        /// <summary>
        /// Maps the observables this phase can legitimately read onto the specific verbs that own them.
        /// Deliberately narrow: a verb only gets a consumer credit when the observable that moved is the one
        /// THAT verb drives. Crediting all four ToolSlot verbs for one slot change, or crediting Cancel for a
        /// PDA that the Pda verb closed, would turn the ledger into decoration.
        /// </summary>
        private static void ApplyVerbConsumerObservations()
        {
            if (_verbSweepFlashlightFlipped)
                _verbFlags[15] |= VerbFlagConsumerObserved;

            if (_verbSweepPdaOpenedByInventoryVerb)
                _verbFlags[7] |= VerbFlagConsumerObserved;

            // The Pda verb is group 6 and its authored behaviour with the PDA already open is to CLOSE it
            // (PlayerPDA.cs:2167-2171), so "opened, then closed" is this verb's observable and not Cancel's.
            if (_verbSweepPdaObservedOpen && _verbSweepPdaClosedAfterCancel)
                _verbFlags[6] |= VerbFlagConsumerObserved;

            // Slot verbs are indices 11..14 for slots 0..3, so the observed slot index selects exactly one.
            if (_verbSweepToolSlotObserved >= 0 && _verbSweepToolSlotObserved <= 3)
                _verbFlags[11 + _verbSweepToolSlotObserved] |= VerbFlagConsumerObserved;
        }

        private static void LogVerbRow(int verb)
        {
            // L19 hop2 LIVE: skip per-verb row log under batch (mono fatal in StringBuilder/Debug.Log path).
            if (UnityEngine.Application.isBatchMode)
                return;
            byte flags = _verbFlags[verb];
            byte expected = VerbExpectedCommand(verb);
            bool raisedVerb = (flags & VerbFlagRaised) != 0;
            bool arrivedVerb = (flags & VerbFlagArrivedInSnapshot) != 0;
            bool commandedVerb = (flags & VerbFlagCommandObserved) != 0;

            _log.Clear();
            _log.Append("[H8_WORLDDRIVER] VERB ").Append(VerbName(verb))
                .Append(" bit=0x").Append(VerbBit(verb).ToString("X8", CultureInfo.InvariantCulture))
                .Append(" raised=").Append(raisedVerb)
                .Append(" expectedCommand=").Append(expected)
                .Append(" arrived=").Append(arrivedVerb)
                .Append(" atFrame=").Append(_verbArrivedFrame[verb])
                .Append(" commandOnLane=").Append(commandedVerb)
                .Append(" consumerObserved=").Append((flags & VerbFlagConsumerObserved) != 0)
                .Append(" - ");

            if (!raisedVerb)
            {
                _log.Append("NOT PRESSED: the sweep stopped before this verb's group, so this verb is ")
                    .Append("UNKNOWN rather than broken");
            }
            else if (!arrivedVerb)
            {
                _log.Append("BIT NEVER REACHED THE RESOLVED SNAPSHOT. Expected bit 0x")
                    .Append(VerbBit(verb).ToString("X8", CultureInfo.InvariantCulture))
                    .Append(" set in CurrentInputState.ButtonsBitmask; measured 0x")
                    .Append(_verbSweepSnapshotButtonsLast.ToString("X8", CultureInfo.InvariantCulture))
                    .Append(" at frame ").Append(_verbSweepSnapshotFrameLast)
                    .Append(". INPUT PLUMBING failure, not a consumer failure: no consumer of this verb was ")
                    .Append("ever given a chance to run");
            }
            else if (expected == 0)
            {
                _log.Append("SNAPSHOT-ONLY BY DESIGN: the bit reached the authoritative snapshot at frame ")
                    .Append(_verbArrivedFrame[verb])
                    .Append(" and InputDispatcher.PublishDiscreteInputSignals has no discrete command for it ")
                    .Append("(InputDispatcher.cs:1055-1106)");
                AppendVerbConsumerHint(verb);
            }
            else if (!commandedVerb)
            {
                _log.Append("EDGE DETECTOR DID NOT FIRE: the bit was in the resolved snapshot at frame ")
                    .Append(_verbArrivedFrame[verb]).Append(", expected one PlayerInputSignal with command ")
                    .Append(expected)
                    .Append(" from InputDispatcher.PublishDiscreteInputSignals and measured none while this ")
                    .Append("phase ran. The phase publishes nothing on that lane itself, so the lane is the ")
                    .Append("dispatcher's alone; it carried ").Append(_lanePlayerInputSignals)
                    .Append(" PLIN signals in total. The pressed test is current & ~previous ")
                    .Append("(InputDispatcher.cs:1050), so a bit already set in the previous resolved frame ")
                    .Append("produces no edge");
            }
            else
            {
                _log.Append("PRESSED AND PUBLISHED: bit in the resolved snapshot at frame ")
                    .Append(_verbArrivedFrame[verb]).Append(", dispatcher published command ")
                    .Append(expected).Append(" on SignalBus<PlayerInputSignal>");
                AppendVerbConsumerHint(verb);
            }

            Debug.Log(_log.ToString());
        }

        /// <summary>
        /// States what the consumer side of a verb did, or names the property a future step has to read.
        /// Naming the unread observable is the point: a bare "consumerObserved=false" is indistinguishable
        /// from a broken consumer, and only four of the seventeen verbs have an observable that settles inside
        /// this phase.
        /// </summary>
        private static void AppendVerbConsumerHint(int verb)
        {
            switch (verb)
            {
                case 0:
                    _log.Append(". Consumer: HectonPlayerMovement reads this bit off its own snapshot ")
                        .Append("(HectonPlayerMovement.cs:13225) and the dispatcher also buffers ")
                        .Append("PlayerBufferedAction.Jump (InputDispatcher.cs:1056). NOT READ HERE - a jump ")
                        .Append("while submerged is a no-op, so a false negative would be worse than no claim");
                    return;
                case 5:
                    _log.Append(". Consumer: ZeroGMovementRuntime.cs:666 and ")
                        .Append("PrologueSequenceRegistryBridge.cs:745. NOT READ HERE - neither is active on ")
                        .Append("this route");
                    return;
                case 6:
                    _log.Append(". Consumer observable: PlayerPDA.IsOpen was ")
                        .Append(_verbSweepPdaObservedOpen ? "observed open" : "never observed open")
                        .Append(" and then ")
                        .Append(_verbSweepPdaClosedAfterCancel ? "closed" : "stayed open");
                    return;
                case 7:
                    _log.Append(". Consumer observable: PlayerPDA.IsOpen=")
                        .Append(Hecton8.UI.PlayerPDA.IsOpen)
                        .Append(", openedByThisVerb=").Append(_verbSweepPdaOpenedByInventoryVerb);
                    return;
                case 9:
                case 10:
                    _log.Append(". Consumer: PlayerPDA tab navigation, which returns immediately unless the ")
                        .Append("PDA is open (PlayerPDA.cs:2190-2199). PDA observed open during the sweep=")
                        .Append(_verbSweepPdaObservedOpen)
                        .Append(" - a false here is an ORDERING fact about the sweep, not a broken verb");
                    return;
                case 11:
                case 12:
                case 13:
                case 14:
                    _log.Append(". Consumer observable: PlayerToolManager.CurrentSlotIndex ")
                        .Append(_verbSweepToolSlotAtEntry).Append("->")
                        .Append(_verbSweepToolSlotObserved)
                        .Append(", ToolLoadoutChangedSignal lane +")
                        .Append(_laneToolLoadoutChanged - _verbSweepLoadoutSignalsAtEntry)
                        .Append(" during the sweep. A slot whose tool is not in inventory is refused by ")
                        .Append("design (PlayerToolManager.cs:927-933), so read the LANECENSUS line below ")
                        .Append("before calling a slot broken");
                    return;
                case 15:
                    _log.Append(". Consumer observable: PlayerFlashlight.IsOn was ")
                        .Append(_verbSweepFlashlightOnAtEntry).Append(" at entry, flipped=")
                        .Append(_verbSweepFlashlightFlipped);
                    return;
                case 16:
                    _log.Append(". NO CONSUMER EXISTS: PlayerInputAction.Pause appears exactly once in the ")
                        .Append("project outside this file, at InputDispatcher.cs:3230, and that line ")
                        .Append("PRODUCES it. Nothing reads it off a snapshot - PauseMenuController binds ")
                        .Append("InputManager.OnPause instead (PauseMenuController.cs:2908), an InputAction ")
                        .Append("callback no snapshot producer can reach. The pause verb is UNREACHABLE from ")
                        .Append("any input snapshot, which is a routing gap in the product and not a harness ")
                        .Append("gap");
                    return;
                default:
                    _log.Append(". Consumer observable not read by this phase");
                    return;
            }
        }

        /// <summary>
        /// Prints what the lanes the four rows depend on actually carried. The value is in the comparison: a
        /// Tool row that says "no downstream effect" beside a ToolLoadoutChangedSignal count of zero and an
        /// InventoryChangedSignal count of zero is a different bug report from the same row beside nonzero
        /// counts.
        /// </summary>
        private static void LogLaneCensus()
        {
            _log.Clear();
            _log.Append("[H8_WORLDDRIVER] LANECENSUS ticks=").Append(_ticks)
                .Append(" InputStateSignal=").Append(_laneInputStateSignals)
                .Append(" PlayerInputSignal[PLIN]=").Append(_lanePlayerInputSignals)
                .Append(" InventoryChangedSignal=").Append(_laneInventoryChanged)
                .Append(" ToolLoadoutChangedSignal=").Append(_laneToolLoadoutChanged)
                .Append(" CraftingStartedSignal=").Append(_laneCraftingStarted)
                .Append(" ResourceDepletionDeltaSignal=").Append(_laneResourceDepletionDelta)
                .Append(" DebrisSpawnSignal=").Append(_laneDebrisSpawn)
                .Append(" | inventoryComponent=")
                .Append(_inventoryComponentPresent ? "present" : "absent")
                .Append(" enabled=").Append(_inventoryComponentEnabled)
                .Append(" gridBound=").Append(_inventoryGridBound)
                .Append(" inventoryVersion ").Append(_inventoryVersionAtResolve).Append("->")
                .Append(_inventoryVersionLast);

            // The discrete lane's OWN storage state, appended to the census rather than only to the Tool row
            // because this line prints on every run - including runs that die before ToolEquip is entered -
            // and "the lane has no ring" explains a zero PLIN count without implicating a single consumer.
            // Read at flush time, which is cold; the latched first-refusal snapshot is the separate fact and it
            // is reported on the row.
            bool discreteLaneStorageNow = SignalBus<PlayerInputSignal>.HasNativeStorage;
            _log.Append(" | PLINlane hasNativeStorage=").Append(discreteLaneStorageNow)
                .Append(" snapshotCount=").Append(SignalBus<PlayerInputSignal>.SnapshotCount)
                .Append(" peakQueuedLastFlush=").Append(SignalBus<PlayerInputSignal>.PeakQueuedLastFlush)
                .Append(" droppedLastFlush=").Append(SignalBus<PlayerInputSignal>.DroppedLastFlush)
                .Append(" loadShedTotal=").Append(SignalBus<PlayerInputSignal>.LoadShedTotal)
                .Append(" corruptedTotal=").Append(SignalBus<PlayerInputSignal>.CorruptedSignalTotal)
                .Append(" laneHash=0x")
                .Append(SignalBus<PlayerInputSignal>.LaneHash.ToString("X8", CultureInfo.InvariantCulture))
                .Append(" driverPushed=").Append(_publishedDiscreteSignals)
                .Append(" driverRefused=").Append(_droppedDiscreteSignals)
                .Append(" registeredLanes=").Append(SignalBusRegistry.LaneCount)
                .Append(" registrationOverflow=").Append(SignalBusRegistry.RegistrationOverflow)
                .Append(" simulationHalted=").Append(SignalBusRegistry.IsSimulationHalted);

            if (!discreteLaneStorageNow)
                _log.Append(" - THE PLIN LANE HAS NO NATIVE RING: SignalBus<PlayerInputSignal>.TryPush returns ")
                    .Append("false at SignalBusRuntime.cs:681 for every caller, the harness and ")
                    .Append("InputDispatcher.cs:4092 alike, and it does so SILENTLY - EnsureInitialized ")
                    .Append("abandons the lane with no log when TryAcquireFrameSnapshotBuffer fails because no ")
                    .Append("IDataVault is bound or the bound one is allocation-locked or compaction-fenced ")
                    .Append("(:626-631 via :1491-1495). Any discrete-input row in this run is UNMEASURED and ")
                    .Append("the owner is the vault bind, not a consumer and not lane capacity");

            if (_laneInputStateSignals == 0)
                _log.Append(" - InputStateSignal is ZERO, so InputDispatcher never published a resolved input ")
                    .Append("frame at all (InputDispatcher.cs:847). Every verb and locomotion verdict in this ")
                    .Append("run is UNMEASURED rather than negative: read this line before reading any row");

            if (_inventoryComponentPresent && !_inventoryComponentEnabled)
                _log.Append(" - PlayerInventory is DISABLED, which forces IsToolAvailableInSlot false for ")
                    .Append("every slot regardless of authoring (PlayerToolManager.cs:927-933). The Tool row ")
                    .Append("on this run measures the inventory guard, not the tools");

            Debug.Log(_log.ToString());
        }

        // ─────────────────────────────────────────────────────────────────────────────────────────
        //  verdict plumbing
        // ─────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Cold, bounded, read-only lookup of the hover owner. CurrentHovered is the only thing wanted
        /// from it, and it is read, never written.
        /// </summary>
        private static void TryResolveInteraction()
        {
            if (_interaction != null || _interactionLookupAttempts >= MaxInteractionLookupAttempts)
                return;

            _interactionLookupAttempts++;
            _interaction = UnityEngine.Object.FindFirstObjectByType<Hecton8.Interaction.PlayerInteraction>(
                FindObjectsInactive.Exclude);
        }

        /// <summary>
        /// Reads the player's eye pose as VALUES. No Transform is retained, and nothing is written back:
        /// this is the read the scatter producer performs to decide where world content belongs.
        /// </summary>
        private static bool TryReadPlayerEye(out Vector3 position, out Vector3 forward)
        {
            position = Vector3.zero;
            forward = Vector3.forward;

            IPlayerRuntimeContext player = GlobalRegistry.RegisteredPlayer;
            if (player == null)
                return false;

            Camera camera = player.PlayerCamera;
            if (camera != null)
            {
                Transform cameraTransform = camera.transform;
                position = cameraTransform.position;
                forward = cameraTransform.forward;
                return true;
            }

            Transform playerTransform = player.PlayerTransform;
            if (playerTransform == null)
                return false;

            position = playerTransform.position;
            forward = playerTransform.forward;
            return true;
        }

        /// <summary>
        /// Appends the ceiling clause for the phase that is CURRENTLY in flight. Every phase that yields
        /// on a ceiling calls this before it latches, because "no interactable resource node exists" and
        /// "this phase was given 1 tick" are different claims about the product and the old text printed
        /// the first one for both cases.
        ///
        /// Answers three questions a reader of a red row actually has: how long and how many ticks the
        /// phase got, how much it was granted, and which of the three ceiling outcomes it hit - TIMEBOXED
        /// (this phase blew its own box and owes the reader a frame-cost investigation), COMPRESSED (an
        /// earlier phase spent the schedule's total, so this row is UNMEASURED rather than negative), or a
        /// clean yield on its own box (a real result). The middle one is the starved-versus-empty
        /// distinction and the first one names who did the starving.
        ///
        /// The granted figure is printed beside the nominal budget even though they are now always equal.
        /// That equality IS the fix - a divergence would mean something reintroduced a clamp on the box -
        /// and a row whose two numbers disagree is the fastest way to see it.
        /// </summary>
        private static void AppendPhaseCeilingNote()
        {
            double wall = PhaseElapsed;

            _detail.Append(" [SCHEDULE phase=").Append(_phase.ToString())
                .Append(" wall=").Append(F(wall))
                .Append("s ticks=").Append(_phaseTicks)
                .Append(" tickFloor=").Append(MinTicksFor(_phase))
                .Append(" tickBox=").Append(_phaseTickBox)
                .Append(" granted=").Append(F(_phaseGranted))
                .Append("s of a ").Append(F(BudgetFor(_phase)))
                .Append("s nominal budget; run elapsed ").Append(F(ElapsedSeconds))
                .Append("s of ").Append(F(TotalBudgetSeconds)).Append("s, tick ").Append(_ticks)
                .Append(" of ").Append(MaxTotalTicks);

            // TIMEBOXED is tested BEFORE compression for the same reason CeilingYield tests it first: a
            // phase that blew its own box is the culprit, and printing the victim's paragraph over the
            // culprit's row is what made the 138.192s phase unfindable from the row it starved.
            if (PhaseExceededItsBox())
            {
                // The ceiling is only testable at tick boundaries, so a single expensive pumped frame can
                // land entirely outside it. Saying so is the difference between a reader believing the
                // instrument measured this long and knowing the engine was inside one frame.
                _detail.Append(" - TIMEBOXED: exceeded its ").Append(F(_phaseGranted))
                    .Append("s box by ").Append(F(wall - _phaseGranted))
                    .Append("s across ").Append(_phaseTicks)
                    .Append(" ticks. The box is only testable between pumped frames, so one expensive ")
                    .Append("frame lands entirely outside it");

                // The "nothing else was starved" reassurance is only TRUE when the schedule survived. It
                // used to print unconditionally, and then the _compressed branch five lines later printed
                // the opposite - one report row asserting both "no later row is starved by this overrun"
                // and "the rows after this one run their tick floors and say UNMEASURED". The second is the
                // truth in that case: EnterPhase does grant the next phase its full nominal box, but
                // PhaseCeilingReached short-circuits on _compressed, so every later phase runs only its
                // tick floor regardless of what it was granted. A reader who believed the first sentence
                // would have read those tick-floor rows as product failures.
                if (_compressed)
                    _detail.Append(". It also took the schedule's ").Append(F(TotalBudgetSeconds))
                        .Append("s total with it at ").Append(F(_compressedAt))
                        .Append("s: EVERY ROW AFTER THIS ONE RAN ONLY ITS TICK FLOOR AND MUST BE READ AS ")
                        .Append("UNMEASURED, not as a product gap. Fix the frame cost here");
                else
                    _detail.Append(". The excess is charged to THIS phase and to nothing else: the next ")
                        .Append("phase is entered with its own full nominal box and the schedule survived, ")
                        .Append("so no later row is starved by this overrun. Fix the frame cost here");
            }
            else if (_compressed)
            {
                // The heaviest-phase fields are only written by CloseCurrentPhase, which has not run yet
                // for the phase composing this note - so the in-flight phase has to be folded in here or a
                // phase that just spent 138s would name the previous record holder as the culprit and send
                // the reader to the wrong place. That is the exact class of mistake this note exists to
                // stop making.
                DrivePhase heaviest = wall > _worstPhaseWall ? _phase : _worstPhase;
                double heaviestWall = wall > _worstPhaseWall ? wall : _worstPhaseWall;

                _detail.Append(" - COMPRESSED: the schedule's total was already spent at ")
                    .Append(F(_compressedAt)).Append("s, in phase ")
                    .Append(_compressedInPhase.ToString())
                    .Append(", so this phase ran its tick floor and yielded without spending the ")
                    .Append(F(_phaseGranted))
                    .Append("s box it was granted. The heaviest phase of the run so far was ")
                    .Append(heaviest.ToString())
                    .Append(" at ").Append(F(heaviestWall))
                    .Append("s. READ THIS ROW AS UNMEASURED, NOT AS A PRODUCT GAP - fix the heavy phase, ")
                    .Append("not this mechanic");
            }
            else if (PhaseExceededItsTickBox())
            {
                // A CULPRIT paragraph, and the one probe7 had no way to print. This phase spent its whole
                // share of the tick pot inside its wall window, which is the shape ToolEquip had there:
                // 27180 ticks in 2.614s of a 6.000s box. The distinction from the wall case is that the row
                // IS a result - the precondition above was re-tested thousands of times and never became
                // true - while the schedule still owes nothing to the phases after it.
                _detail.Append(" - PHASE TICK CEILING: spent its whole ").Append(_phaseTickBox)
                    .Append("-tick box in ").Append(F(wall))
                    .Append("s, i.e. about ")
                    // Floored at a millisecond, not at zero: (int) of a double that overflows int is
                    // unspecified in C#, and a phase whose wall read back as a few nanoseconds would put
                    // 2.7e11 through this cast. A rate is meaningless below a millisecond anyway.
                    .Append(wall >= 0.001 ? (int)(_phaseTicks / wall) : 0)
                    .Append(" driver ticks per wall second, so it yielded on the TICK axis rather than the ")
                    .Append("wall one. The box is this phase's own share of the schedule's ")
                    .Append(MaxTotalTicks)
                    .Append("-tick pot, so no later phase pays for it. The precondition named above was ")
                    .Append("re-tested on every one of those ticks and never became true - that is a result ")
                    .Append("about the precondition, not about the schedule");
            }
            else if (_tickCompressed)
            {
                // The VICTIM paragraph on the tick axis. Before tick-compression existed this run ENDED
                // instead, and the rows after it printed NOT_EXERCISED with no way to tell a starved row from
                // absent content - the exact confusion this whole ledger exists to remove.
                _detail.Append(" - TICK-COMPRESSED: the schedule's ").Append(MaxTotalTicks)
                    .Append("-tick pot was already spent at ").Append(F(_tickCompressedAt))
                    .Append("s in phase ").Append(_tickCompressedInPhase.ToString())
                    .Append(", so this phase ran its tick floor and yielded without spending the ")
                    .Append(_phaseTickBox)
                    .Append("-tick box it was granted. The heaviest tick consumer of the run so far was ")
                    .Append(_worstTickPhase.ToString()).Append(" at ").Append(_worstPhaseTicks)
                    .Append(" ticks. READ THIS ROW AS UNMEASURED, NOT AS A PRODUCT GAP");
            }
            else
            {
                _detail.Append(" - yielded on its own ceiling with work unfinished");
            }

            _detail.Append(']');
        }

        /// <summary>
        /// Names the PRECONDITION the phase is waiting on, in one clause, with the predicate and its owner.
        ///
        /// This is the fact probe7 could not report. Its rows carried a stop phase, a stop cause and a tick
        /// count and still could not distinguish four completely different ToolEquip waits with four different
        /// owners - and the run's own <c>discreteSignals=0 dropped=0</c> had already excluded two of them. A
        /// stop cause says the instrument gave up; this says what it was waiting for.
        ///
        /// Cold: one call per row latch at most, so <c>Enum.ToString</c> and the literal table are on the same
        /// footing as <see cref="GetPhaseName"/>.
        /// </summary>
        private static void AppendWaitReasonNote()
        {
            _detail.Append(" [WAITING-ON ").Append(_waitReason.ToString()).Append(" - ")
                .Append(WaitReasonExplanation(_waitReason)).Append(']');
        }

        /// <summary>
        /// The acting sentence for a wait reason: the exact predicate that was false and where its owner
        /// lives. Literals rather than composed text, because every one of them is a fixed fact about a
        /// contract and a reader who has to guess which subsystem to open has not been given a diagnostic.
        /// </summary>
        private static string WaitReasonExplanation(WaitReason reason)
        {
            switch (reason)
            {
                case WaitReason.PlayerOwnersNotRegistered:
                    return "GlobalRegistry.RegisteredPlayer had not published SurvivalSystem and " +
                        "PlayerMovement, so there was nothing to measure locomotion against. Owner is the " +
                        "player root's assembly order, not the input lane";

                case WaitReason.InputServiceNotRegistered:
                    return "the player owners existed but GlobalRegistry.RegisteredInput was the EMPTY slot " +
                        "(GATE 1). That is not a closed action map and not a block mask - nothing had " +
                        "registered an input service at all, so every consumer null-checks out before " +
                        "reading anything";

                case WaitReason.LocomotionHoldInProgress:
                    return "nothing. This phase is a timed hold and its job is to occupy its wall box while " +
                        "the depth/oxygen/pressure deltas accumulate, so its tick count is a sample count " +
                        "and not a retry count";

                case WaitReason.ResourceNodeNotAvailable:
                    return "no live undepleted ResourceNode was inside the reach cone, and the driver's own " +
                        "registered spawn point had not been instantiated by ScavengePopulator's " +
                        "ProcessSpawnQueue yet. Owner is the populator's loot tables or its readiness, not " +
                        "the tool route";

                case WaitReason.ToolManagerAbsent:
                    return "IPlayerRuntimeContext.ToolManager was null for the whole phase, so no slot could " +
                        "be enumerated and no ToolSlot command was ever pressed. Owner is the player " +
                        "context's publication of PlayerToolManager";

                case WaitReason.NoToolAvailableInAnySlot:
                    return "PlayerToolManager.IsToolAvailableInSlot was false for every slot, which is " +
                        "'prefab != null && HasToolInInventory(prefab)' (PlayerToolManager.cs:927-933). It " +
                        "is therefore ALSO false for a fully authored loadout whenever PlayerInventory " +
                        "disabled itself, so read the INVENTORY clause in this row before concluding no tool " +
                        "is authored. No PLIN push was attempted, which is why pushed=0 dropped=0 is the " +
                        "expected reading and not a lane fault";

                case WaitReason.ToolSlotCommandRefusedByLane:
                    return "a slot WAS available and SignalBus<PlayerInputSignal>.TryPush refused the " +
                        "ToolSlot command, so the phase never got past its own producer. Read the lane " +
                        "forensics clause in this row for which of the four refusal paths fired";

                case WaitReason.ToolSwapNotConfirmed:
                    return "the ToolSlot command was published and PlayerToolManager.CurrentTool / " +
                        "CurrentSlotIndex never confirmed the swap. Whether that is the tool manager or the " +
                        "signal DELIVERY is decided by commandSeenInFlushedSnapshot in this row - a push " +
                        "that entered the ring is not a push a consumer could read";

                case WaitReason.ToolPrimaryFireHoldInProgress:
                    return "nothing. PrimaryFire is held on the input snapshot for this phase's wall box " +
                        "because PlayerToolManager polls the snapshot itself (PlayerToolManager.cs:418); the " +
                        "tick count is a hold length, not a retry count";

                case WaitReason.NodeIntegrityNotDepleted:
                    return "tool pulses were landing and node integrity had not reached zero. Owner is the " +
                        "node template's integrity/hardness against the pulse budget, and the pulses/landed " +
                        "counts in this row say whether the capability gate refused them instead";

                case WaitReason.PickupNotHovered:
                    return "PlayerInteraction never hovered a PickupItem, so there was nothing for the " +
                        "Interact command to act on. Either depletion produced no loot prefab or the drop is " +
                        "out of reach / off the interactable layer mask - the driver cannot press its way " +
                        "past this one";

                case WaitReason.PickupAcquisitionNotPublished:
                    return "the PickupItem was hovered and the real Interact command was published, and no " +
                        "ItemAcquiredSignal with sourceKind=ManualPickup arrived. Owner is the pickup's own " +
                        "acquisition path, which is downstream of everything this driver produces";

                case WaitReason.FabricatorAbsent:
                    return "no live Fabricator component was found by bounded scene search, so no recipe " +
                        "could be started. Owner is scene content, not the crafting code";

                case WaitReason.NoCraftableRecipe:
                    return "the Fabricator was live and CanCraft was false for every visible recipe, which " +
                        "is normally the RESOURCE leg upstream having delivered no ingredients rather than a " +
                        "crafting defect. Check the Resource row's verdict before this one";

                case WaitReason.CraftDeliveryNotPublished:
                    return "StartCraft was accepted and no ItemAcquiredSignal with sourceKind=Fabricator " +
                        "arrived, so the craft was consumed and never delivered. Owner is the Fabricator's " +
                        "completion path";

                case WaitReason.VerbSweepStepping:
                    return "nothing. The sweep is a fixed 16-step handshake and each step needs its own tick " +
                        "to produce a real 0-to-1 edge for the dispatcher's edge detector";

                default:
                    return "no wait was recorded for this phase, which means it advanced on its own success " +
                        "path or was closed before any tick decided to wait";
            }
        }

        /// <summary>
        /// Same clause for a phase that has already CLOSED, read out of the ledger. The Swim row needs
        /// this: it is latched in SwimVerdict, one phase after the two holds whose duration decides
        /// whether a depth span could exist at all.
        /// </summary>
        private static void AppendClosedPhaseNote(DrivePhase phase)
        {
            int index = (int)phase;
            if (index < 0 || index >= (int)DrivePhase.PhaseCount)
                return;

            if (_phaseYield[index] == PhaseYield.NotEntered)
                return;

            _detail.Append(" [SCHEDULE phase=").Append(phase.ToString())
                .Append(" wall=").Append(F(_phaseWall[index]))
                .Append("s ticks=").Append(_phaseTickLedger[index])
                .Append(" tickFloor=").Append(MinTicksFor(phase))
                .Append(" tickBox=").Append(MaxTicksFor(phase))
                .Append(" granted=").Append(F(_phaseGrant[index]))
                .Append("s yield=").Append(_phaseYield[index].ToString())
                .Append(" waitingOn=").Append(_phaseWaitReason[index].ToString());

            if (_phaseYield[index] == PhaseYield.PhaseTickCeiling)
                // The tick-axis twin of the TIMEBOXED clause below, and it reads the OPPOSITE way to
                // TotalTickCeiling: this phase spent its own box, so a threshold it missed is a real miss
                // measured over thousands of retries, and no later phase paid for it.
                _detail.Append(" - PHASE TICK CEILING: this phase spent its whole ")
                    .Append(MaxTicksFor(phase))
                    .Append("-tick share of the schedule's pot inside its wall box, so it yielded on the ")
                    .Append("tick axis. Its own share only - the phases after it kept theirs");
            else if (_phaseYield[index] == PhaseYield.TotalTickCeiling)
                _detail.Append(" - TICK-COMPRESSED to its tick floor because the schedule's ")
                    .Append(MaxTotalTicks)
                    .Append("-tick pot was already spent, so any threshold this row failed was never given ")
                    .Append("the ticks to be crossed. UNMEASURED, not broken");
            else if (_phaseYield[index] == PhaseYield.TotalCeiling)
                _detail.Append(" - COMPRESSED to its tick floor because the schedule's ")
                    .Append(F(TotalBudgetSeconds))
                    .Append("s total was already spent, so any threshold this row failed was never given ")
                    .Append("the time to be crossed. UNMEASURED, not broken");
            else if (_phaseYield[index] == PhaseYield.Timeboxed)
                // The opposite reading to TotalCeiling, and the Swim row is the one that needs the
                // distinction: a hold that overran held LONGER than designed, so a threshold it failed
                // failed on the mechanic, not on the clock, and a threshold it met may have been met only
                // because one pumped frame gave it 132 seconds it was never meant to have.
                _detail.Append(" - TIMEBOXED: this phase ran PAST its box, so it held longer than ")
                    .Append("designed rather than shorter. A threshold missed here is a real miss; a ")
                    .Append("threshold met here was met on an unintended duration");

            _detail.Append(']');
        }

        private static void Latch(int row, RowVerdict verdict)
        {
            if (row < 0 || row >= RowCount || _latched[row])
                return;

            _verdicts[row] = verdict;
            _details[row] = _detail.ToString();
            _latched[row] = true;
        }

        private static void LatchAllUnlatched(RowVerdict verdict, string exceptionType, string message)
        {
            for (int row = 0; row < RowCount; row++)
            {
                if (_latched[row])
                    continue;

                _detail.Clear();
                _detail.Append("driver threw ").Append(exceptionType).Append(": ").Append(message)
                    .Append(" in phase ").Append(_phase.ToString())
                    .Append(" - this row was never evaluated");
                Latch(row, verdict);
            }
        }

        /// <summary>
        /// The phase whose tick would have produced a row's verdict. Used only by the finalisation text,
        /// so a starved row can state whether its OWN phase was ever entered - which is the difference
        /// between "the schedule stopped before this mechanic" and "this mechanic was tried and failed".
        /// </summary>
        private static DrivePhase TerminalPhaseFor(int row)
        {
            switch (row)
            {
                case RowSwim:
                    return DrivePhase.SwimVerdict;
                case RowResource:
                    return DrivePhase.ResourcePickup;
                case RowTool:
                    return DrivePhase.ToolUse;
                case RowCraft:
                    return DrivePhase.Craft;
                default:
                    return DrivePhase.Idle;
            }
        }

        /// <summary>
        /// Closes out anything the schedule never resolved. NOT_EXERCISED is the right verdict by this
        /// probe's own convention - the mechanic is UNKNOWN, not negative - and that part was already
        /// correct. The TEXT was not, and it is the text a reader acts on.
        ///
        /// What it used to say, verbatim from Logs/h8_playprobe_route.json moments[6]:
        ///   "driver ran out of budget in phase Craft after 160.430s and never reached this row"
        /// Three of those clauses were wrong or unusable:
        ///   - "ran out of budget" asserted a mechanism this file did not have. TotalBudgetSeconds was
        ///     read at zero places in this class and _startedAt at exactly one - that message - so the
        ///     driver printed an elapsed for a stop it had no code to cause. The stop came from the probe
        ///     closing its gameplay window (H8_HeadlessPlayModeProbe.cs:495-503). A reader who believed
        ///     the sentence went looking at the phase constants, which were not the problem.
        ///   - "in phase Craft" named the phase the schedule was SITTING in, not the phase that spent the
        ///     time. Craft had been entered and given zero ticks. ResourceDeplete had taken 138.192 of
        ///     the 160.430 seconds and its name appeared nowhere in the row it starved.
        ///   - "never reached this row" contradicted "in phase Craft" for the Craft row itself.
        /// Every one of those facts is now stated separately, because they have different owners.
        /// </summary>
        private static void FinaliseUnlatchedRows()
        {
            for (int row = 0; row < RowCount; row++)
            {
                if (_latched[row])
                    continue;

                DrivePhase terminal = TerminalPhaseFor(row);
                int terminalIndex = (int)terminal;
                bool terminalEntered =
                    terminalIndex >= 0 &&
                    terminalIndex < (int)DrivePhase.PhaseCount &&
                    (_phaseYield[terminalIndex] != PhaseYield.NotEntered || _phase == terminal);

                _detail.Clear();
                _detail.Append("NOT MEASURED: the schedule stopped in phase ")
                    .Append(_stoppedInPhase.ToString())
                    .Append(" at ").Append(F(_stoppedAtElapsed))
                    .Append("s of its ").Append(F(TotalBudgetSeconds))
                    .Append("s budget, after ").Append(_ticks)
                    .Append(" driver ticks. Stop cause: ").Append(_stopCause.ToString())
                    .Append(". This row's own phase ").Append(terminal.ToString())
                    .Append(terminalEntered ? " WAS entered" : " was NEVER entered")
                    .Append(" (ticks=").Append(GetPhaseTicks(terminalIndex))
                    .Append(" of a ").Append(MinTicksFor(terminal))
                    .Append("-tick floor, wall=").Append(F(GetPhaseWallSeconds(terminalIndex)))
                    .Append("s)");

                // WHAT THE STOP PHASE WANTED, and it is first because it is the only clause a reader can act
                // on directly. probe7 printed the stop phase, the stop cause and the tick count and none of
                // the three said which of ToolEquip's four preconditions was false.
                int stoppedIndex = (int)_stoppedInPhase;
                WaitReason stoppedWaiting =
                    stoppedIndex >= 0 && stoppedIndex < _phaseWaitReason.Length
                        ? _phaseWaitReason[stoppedIndex]
                        : WaitReason.None;
                if (stoppedWaiting == WaitReason.None)
                    stoppedWaiting = _waitReason;

                _detail.Append(". That phase was waiting on ").Append(stoppedWaiting.ToString())
                    .Append(" - ").Append(WaitReasonExplanation(stoppedWaiting));

                // WALL AND TICK CULPRITS ARE SEPARATE QUESTIONS. The previous version printed only the wall
                // winner and called it "the phase to fix", which on a tick-axis stop is a manufactured
                // accusation: probe7 named SwimDive, and SwimDive had spent EXACTLY its 7.000s grant.
                if (_worstPhaseWall > 0.0)
                    _detail.Append(". Heaviest WALL phase: ")
                        .Append(_worstPhase.ToString()).Append(" at ").Append(F(_worstPhaseWall))
                        .Append("s over ").Append(GetPhaseTicks((int)_worstPhase))
                        .Append(" ticks against a ").Append(F(GetPhaseGrantedSeconds((int)_worstPhase)))
                        .Append("s grant");

                if (_worstPhaseTicks > 0)
                    _detail.Append(". Heaviest TICK phase: ").Append(_worstTickPhase.ToString())
                        .Append(" at ").Append(_worstPhaseTicks)
                        .Append(" ticks of a ").Append(MaxTicksFor(_worstTickPhase))
                        .Append("-tick box. On a tick-axis stop this is the phase to look at, NOT the wall ")
                        .Append("one - a phase that filled its wall grant exactly did nothing wrong");

                if (_compressed)
                    _detail.Append(". The total WALL budget was already spent at ").Append(F(_compressedAt))
                        .Append("s in phase ").Append(_compressedInPhase.ToString());

                if (_tickCompressed)
                    _detail.Append(". The ").Append(MaxTotalTicks)
                        .Append("-tick pot was already spent at tick ").Append(_tickCompressedAtTick)
                        .Append(" / ").Append(F(_tickCompressedAt))
                        .Append("s in phase ").Append(_tickCompressedInPhase.ToString())
                        .Append(", so every phase after that ran only its tick floor - those rows are ")
                        .Append("UNMEASURED, not empty");

                if (_stopCause == StopCause.ProbeGameplayWindowClosed)
                    _detail.Append(". The stop was EXTERNAL: the probe's gameplay window closed while the ")
                        .Append("schedule was still running, so this row is a harness budget shortfall, ")
                        .Append("not a product gap - STARVED, not empty");
                else if (_stopCause == StopCause.OwnTickCeiling)
                    // REWRITTEN. This clause used to assert "which means a phase was spinning without
                    // advancing", and that inference was FALSE on the run that produced it: probe7's ledger
                    // shows Settle 1 tick, SwimSurface 6950, SwimDive 25865, SwimVerdict 1, ResourceTarget 2,
                    // ToolEquip 27180, with not one phase over its wall box. The two hold phases had taken
                    // 32815 ticks doing exactly what they are designed to do, and the sentence sent a reader
                    // hunting a spin that did not exist - it is the sentence that got the 60000 ticks
                    // attributed to ToolEquip alone. The cap is now the hard stop ABOVE the pot, so reaching
                    // it means tick-compression itself failed to advance, and even that is stated as a
                    // possibility to check rather than a conclusion.
                    _detail.Append(". The stop was the driver's own ").Append(MaxTotalTicksHardStop)
                        .Append("-tick hard stop, which sits ").Append(HardStopTickAllowance)
                        .Append(" ticks above the ").Append(MaxTotalTicks)
                        .Append("-tick pot. Reaching it means tick-compression did not finish paying the ")
                        .Append("remaining tick floors, so compare the per-phase tick counts against their ")
                        .Append("boxes before concluding anything about content: a phase at its box yielded ")
                        .Append("correctly, and only a phase far past its box was genuinely stuck");

                _verdicts[row] = RowVerdict.NotExercised;
                _details[row] = _detail.ToString();
                _latched[row] = true;
            }
        }

        private static string F(float value)
        {
            return value.ToString("F3", CultureInfo.InvariantCulture);
        }

        private static string F(double value)
        {
            return value.ToString("F3", CultureInfo.InvariantCulture);
        }
    }
}

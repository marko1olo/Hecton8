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
//     strictly increasing Sequence. Same lane, same gates, same payload as InputDispatcher.cs:3969.
//     Consumers (PlayerInteraction, PlayerToolManager, PlayerPDA, ...) cannot tell the difference and
//     are not asked to.
//   AXIS 2, continuous locomotion: CoreDeterminismSignals.TryPublishInputOverride, which
//     InputDispatcher.ApplyAutomationOverride (InputDispatcher.cs:3230, called unconditionally at
//     :3018) folds into the authoritative PlayerInputState AFTER the hardware poll and BEFORE the
//     input block mask. This is the project's own sanctioned synthetic-input lane.
//
//   Registry-slot replacement was evaluated and REJECTED as unsafe, not merely inconvenient:
//   GlobalRegistryServiceSlot.Input is denied by IsSceneRuntimeHotSwapSlot (GlobalRegistry.cs:7161),
//   RegisterInputService (:3106) takes no token, and Register (:7315) calls ThrowSlotHijack (:7450)
//   when the slot is already occupied. Publishing a ScriptedInputService before Ready would therefore
//   make InputDispatcher.TryRegisterInputService (InputDispatcher.cs:2837) throw during Initialize and
//   abort the very boot this probe measures. Publishing after Ready throws CriticalBootException. The
//   automation-override lane needs neither door and leaves the real owner in place.
//
// CADENCE
//   Driven by the probe's existing EditorApplication.update tick. No Update/LateUpdate/FixedUpdate, no
//   coroutine, no async. Per-tick work is struct writes, native-ring pushes, ReadOnlySpan scans and
//   property reads — zero managed allocation. Cold work (one-shot component lookup, verdict detail
//   strings) is latched so it can never repeat per frame.
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
            Done,
        }

        // ── lane constants ────────────────────────────────────────────────────────────────────────
        // Duplicated as a private const in every consumer of the lane (InputDispatcher.cs:88,
        // PlayerInteraction.cs:62), so reproducing the literal is the contract, not a shortcut.
        private const uint PlayerInputSignalSourceHash = 0x504C494Eu;

        // ── phase budgets, wall-clock seconds ─────────────────────────────────────────────────────
        // Wall clock, not frames: a batchmode frame is not a unit of simulation progress, which is the
        // same lesson TickWaitingForSettle already learned the hard way.
        private const double SettleBudgetSeconds = 8.0;
        private const double SwimSurfaceBudgetSeconds = 5.0;
        private const double SwimDiveBudgetSeconds = 7.0;
        private const double ResourceTargetBudgetSeconds = 6.0;
        private const double ToolEquipBudgetSeconds = 6.0;
        private const double ToolUseBudgetSeconds = 5.0;
        private const double ResourceDepleteBudgetSeconds = 6.0;
        private const double ResourcePickupBudgetSeconds = 6.0;
        private const double CraftBudgetSeconds = 14.0;
        private const double CraftEvaluationIntervalSeconds = 0.5;

        /// <summary>Total wall time the schedule can consume before it reports what it has and stops.</summary>
        internal const double TotalBudgetSeconds =
            SettleBudgetSeconds + SwimSurfaceBudgetSeconds + SwimDiveBudgetSeconds +
            ResourceTargetBudgetSeconds + ToolEquipBudgetSeconds + ToolUseBudgetSeconds +
            ResourceDepleteBudgetSeconds + ResourcePickupBudgetSeconds + CraftBudgetSeconds;

        // ── acceptance thresholds ─────────────────────────────────────────────────────────────────
        private const float MinMovementIntent01 = 0.01f;
        private const float MinDepthSpanMeters = 0.25f;
        private const float MinOxygenDelta = 0.0005f;
        private const float MinPressureDelta = 0.0005f;
        private const float MinNodeHealthDelta = 0.001f;
        private const float MinDurabilityDelta = 0.0001f;

        // ── resource placement ────────────────────────────────────────────────────────────────────
        private const float NodePlacementDistanceMeters = 1.75f;
        private const float ExistingNodeMaxDistanceMeters = 3.5f;
        private const float ExistingNodeMinForwardDot = 0.5f;
        private const float NodeCutDamagePerTick = 8.0f;
        private const float ScavengeTileSizeMeters = 512.0f; // ScavengePopulator.cs:189 authored default.
        private const int ScavengeLocalIndex = 90001;

        // ── state ─────────────────────────────────────────────────────────────────────────────────
        private static DrivePhase _phase = DrivePhase.Idle;
        private static double _phaseStartedAt;
        private static double _startedAt;
        private static bool _enabled;
        private static bool _stopped;
        private static bool _switchedToPlayerInput;
        private static uint _discreteSequence;
        private static int _droppedDiscreteSignals;
        private static int _publishedDiscreteSignals;
        private static int _publishedOverrides;
        private static int _ticks;

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

        // ── tool observations ─────────────────────────────────────────────────────────────────────
        private static int _requestedToolSlot = -1;
        private static int _availableToolSlots;
        private static bool _toolSlotSignalPublished;
        private static bool _toolEquipped;
        private static int _equippedSlotIndex = -1;
        private static float _durabilityAtToolUse;
        private static float _durabilityAfterToolUse;
        private static bool _durabilityReadable;

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
            _enabled = false;
            _stopped = false;
            _switchedToPlayerInput = false;
            _discreteSequence = 0u;
            _droppedDiscreteSignals = 0;
            _publishedDiscreteSignals = 0;
            _publishedOverrides = 0;
            _ticks = 0;
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

            _requestedToolSlot = -1;
            _availableToolSlots = 0;
            _toolSlotSignalPublished = false;
            _toolEquipped = false;
            _equippedSlotIndex = -1;
            _durabilityAtToolUse = 0f;
            _durabilityAfterToolUse = 0f;
            _durabilityReadable = false;

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
            EnterPhase(DrivePhase.Settle);
        }

        /// <summary>
        /// Releases the locomotion lane so the world is not left under synthetic input while the save
        /// round trip runs. An override left latched would make the save leg measure a moving player.
        /// </summary>
        internal static void Stop()
        {
            if (!_enabled || _stopped)
                return;

            _intent = default;
            CoreDeterminismSignals.ClearInputOverride();

            // _phase is deliberately NOT set to Done: the phase the schedule stopped in is the single
            // most useful fact about a run that did not finish, and overwriting it with "Done" throws it
            // away. _stopped closes IsActive instead.
            FinaliseUnlatchedRows();
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
                SampleObservables();
                PublishLocomotionIntent();
                AdvancePhase();
            }
            catch (System.Exception ex)
            {
                _intent = default;
                CoreDeterminismSignals.ClearInputOverride();
                LatchAllUnlatched(RowVerdict.Fail, ex.GetType().Name, ex.Message);
                _stopped = true;
            }
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
            IInputService input = GlobalRegistry.RegisteredInput;
            if (input != null)
            {
                if (input.IsPlayerInputEnabled)
                    _inputEnabledEverObserved = true;

                _inputBlockMaskLast = input.GetInputBlockMask();
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
                _publishedDiscreteSignals++;

            return pushed;
        }

        // ─────────────────────────────────────────────────────────────────────────────────────────
        //  SCHEDULE
        // ─────────────────────────────────────────────────────────────────────────────────────────

        private static void EnterPhase(DrivePhase phase)
        {
            _phase = phase;
            _phaseStartedAt = EditorApplication.timeSinceStartup;
        }

        private static double PhaseElapsed => EditorApplication.timeSinceStartup - _phaseStartedAt;

        private static void AdvancePhase()
        {
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
                    EnterPhase(DrivePhase.ResourceTarget);
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
            }
        }

        /// <summary>
        /// Waits for the owners the rows are judged against, then opens the gameplay input map through
        /// the real entry point the UI layer uses when a menu closes.
        ///
        /// IsPlayerInputEnabled is NOT something this driver can fake: it is
        /// _nativeInputManager.IsPlayerInputEnabled (InputDispatcher.cs:409), and both
        /// HectonPlayerInputHandler.TryReadFrame and PlayerToolManager's fire poll refuse to read a
        /// frame while it is false. If batchmode never enables the map, the continuous lane is closed
        /// and the affected rows say so by name instead of reporting a pass.
        /// </summary>
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

            IInputService input = GlobalRegistry.RegisteredInput;
            if (input != null && !_switchedToPlayerInput)
            {
                input.SwitchToPlayerInput();
                _switchedToPlayerInput = true;
            }

            bool ready = _survival != null && _movement != null && input != null;
            if (!ready && PhaseElapsed < SettleBudgetSeconds)
                return;

            if (!ready)
            {
                LatchBlocked(
                    RowSwim,
                    "no drivable player after ",
                    PhaseElapsed,
                    "s: survival=", _survival != null, " movement=", _movement != null,
                    " inputService=", input != null);
                EnterPhase(DrivePhase.ResourceTarget);
                return;
            }

            _oxygenAtStart = _survival.Oxygen;
            _pressureAtStart = _survival.Pressure;
            _swimBaselineTaken = true;
            EnterPhase(DrivePhase.SwimSurface);
        }

        private static void TickSwimSurface()
        {
            // Forward plus ascend. VerticalDelta is the surface/dive axis
            // (HectonPlayerInputHandler.cs:37-53 reads it straight off the snapshot).
            _intent.MoveDelta = new Vector2(0f, 1f);
            _intent.LookDelta = Vector2.zero;
            _intent.VerticalDelta = 1f;
            _intent.ActionsBitmask = 0u;
            _intent.CurrentInputSchemeHash = 0u;

            if (PhaseElapsed >= SwimSurfaceBudgetSeconds)
                EnterPhase(DrivePhase.SwimDive);
        }

        private static void TickSwimDive()
        {
            _intent.MoveDelta = new Vector2(0f, 1f);
            _intent.LookDelta = Vector2.zero;
            _intent.VerticalDelta = -1f;
            _intent.ActionsBitmask = (uint)PlayerInputAction.Sprint;
            _intent.CurrentInputSchemeHash = 0u;

            if (PhaseElapsed >= SwimDiveBudgetSeconds)
            {
                _intent = default;
                EnterPhase(DrivePhase.SwimVerdict);
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
                .Append("] inputEnabled=").Append(_inputEnabledEverObserved)
                .Append(" blockMask=0x").Append(_inputBlockMaskLast.ToString("X8", CultureInfo.InvariantCulture));

            if (!_inputEnabledEverObserved)
            {
                _detail.Append(" - BLOCKER: IInputService.IsPlayerInputEnabled was false for the whole ")
                    .Append("window, so HectonPlayerInputHandler.TryReadFrame refused every frame and no ")
                    .Append("locomotion producer can reach movement in this configuration");
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
                Latch(RowSwim, RowVerdict.Partial);
                return;
            }

            _detail.Append(" - FAIL: the input path was open but the driver's MoveDelta never reached ")
                .Append("HectonPlayerMovement");
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
                EnterPhase(DrivePhase.ToolEquip);
                return;
            }

            if (TryAdoptNearbyWorldNode())
            {
                _nodeFromWorld = true;
                EnterPhase(DrivePhase.ToolEquip);
                return;
            }

            if (!_spawnPointRegistered)
            {
                TryRegisterDriverSpawnPoint();
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
                EnterPhase(DrivePhase.ToolEquip);
                return;
            }

            if (PhaseElapsed < ResourceTargetBudgetSeconds)
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

            Latch(RowResource, RowVerdict.Blocked);
            EnterPhase(DrivePhase.ToolEquip);
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
            Hecton8.Gameplay.PlayerToolManager manager = _toolManager;
            if (manager == null)
            {
                IPlayerRuntimeContext player = GlobalRegistry.RegisteredPlayer;
                manager = player?.ToolManager;
                _toolManager = manager;
            }

            if (manager == null)
            {
                if (PhaseElapsed < ToolEquipBudgetSeconds)
                    return;

                _detail.Clear();
                _detail.Append("no PlayerToolManager published on the player runtime context, so no tool ")
                    .Append("slot can be selected");
                Latch(RowTool, RowVerdict.Blocked);
                EnterPhase(DrivePhase.ResourceDeplete);
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
                    if (PhaseElapsed < ToolEquipBudgetSeconds)
                        return;

                    _detail.Clear();
                    _detail.Append("PlayerToolManager reports slotCount=").Append(slotCount)
                        .Append(" but IsToolAvailableInSlot is false for every slot, so no tool exists ")
                        .Append("to select on this route");
                    Latch(RowTool, RowVerdict.Blocked);
                    EnterPhase(DrivePhase.ResourceDeplete);
                    return;
                }

                _requestedToolSlot = chosen;
                _toolSlotSignalPublished =
                    PublishDiscreteCommand((byte)(PlayerInputSignalCommands.ToolSlot1 + chosen));
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
                EnterPhase(DrivePhase.ToolUse);
                return;
            }

            if (PhaseElapsed < ToolEquipBudgetSeconds)
                return;

            _detail.Clear();
            _detail.Append("published PlayerInputSignal command ToolSlot").Append(_requestedToolSlot + 1)
                .Append(" on the PLIN lane (availableSlots=").Append(_availableToolSlots)
                .Append(", pushed=").Append(_publishedDiscreteSignals)
                .Append(", dropped=").Append(_droppedDiscreteSignals)
                .Append(") but CurrentTool stayed null and CurrentSlotIndex=")
                .Append(manager.CurrentSlotIndex).Append(" after ").Append(F((float)PhaseElapsed))
                .Append("s - the discrete lane was accepted and the swap never completed");
            Latch(RowTool, RowVerdict.Fail);
            EnterPhase(DrivePhase.ResourceDeplete);
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
            _intent.MoveDelta = Vector2.zero;
            _intent.LookDelta = Vector2.zero;
            _intent.VerticalDelta = 0f;
            _intent.ActionsBitmask = (uint)PlayerInputAction.PrimaryFire;
            _intent.CurrentInputSchemeHash = 0u;

            if (PhaseElapsed < ToolUseBudgetSeconds)
                return;

            _intent = default;

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
                .Append(F(ToolUseBudgetSeconds)).Append("s: nodeHealth ")
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
                _detail.Append(" - row NOT accepted");
                Latch(RowTool, RowVerdict.Partial);
            }

            EnterPhase(DrivePhase.ResourceDeplete);
        }

        /// <summary>
        /// Finishes the node off through ResourceNode.ApplyCutDamage (:517) — the same public entry point
        /// the cutting path uses — so the node runs its own TrySpawnLoot and produces a real PickupItem.
        /// Deliberately AFTER the tool phase: the tool gets first claim on the damage, and this leg never
        /// contributes to the Tool row's verdict.
        /// </summary>
        private static void TickResourceDeplete()
        {
            Hecton8.Scavenging.ResourceNode node = _node;
            if (node == null)
            {
                // A node that depleted and then went away is the SUCCESS path, not a missing node: the
                // loot prefab outlives it. Skipping straight to Craft here would discard a pickup that is
                // sitting in the world waiting to be interacted with.
                EnterPhase(_nodeDepleted ? DrivePhase.ResourcePickup : DrivePhase.Craft);
                return;
            }

            if (node.IsDepleted)
            {
                _nodeDepleted = true;
                EnterPhase(DrivePhase.ResourcePickup);
                return;
            }

            node.ApplyCutDamage(NodeCutDamagePerTick, node.transform.position);

            if (PhaseElapsed < ResourceDepleteBudgetSeconds)
                return;

            if (_latched[RowResource])
            {
                EnterPhase(DrivePhase.Craft);
                return;
            }

            _detail.Clear();
            _detail.Append("node '").Append(node.UniqueId)
                .Append("' would not deplete: health=").Append(F(node.CurrentHealth))
                .Append(" normalized=").Append(F(node.HealthNormalized))
                .Append(" vulnerabilityMask=0x")
                .Append(node.VulnerabilityMask.ToString("X8", CultureInfo.InvariantCulture))
                .Append(" after ").Append(F((float)PhaseElapsed))
                .Append("s of ApplyCutDamage - the template does not accept the Cut capability, so no ")
                .Append("PickupItem was ever produced");
            Latch(RowResource, RowVerdict.Blocked);
            EnterPhase(DrivePhase.Craft);
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

                EnterPhase(DrivePhase.Craft);
                return;
            }

            TryResolveInteraction();

            if (_sawPickupHover && !_interactPublished)
                _interactPublished = PublishDiscreteCommand(PlayerInputSignalCommands.Interact);

            if (PhaseElapsed < ResourcePickupBudgetSeconds)
                return;

            if (!_latched[RowResource])
            {
                _detail.Clear();
                _detail.Append("node depleted=").Append(_nodeDepleted)
                    .Append(", PickupItem hovered=").Append(_sawPickupHover)
                    .Append(", Interact command published=").Append(_interactPublished)
                    .Append(", ItemAcquiredSignal(ManualPickup) observed=false after ")
                    .Append(F((float)PhaseElapsed)).Append("s");

                if (_interaction == null)
                {
                    _detail.Append(" - INSTRUMENT LIMIT: no PlayerInteraction component was found in ")
                        .Append(MaxInteractionLookupAttempts)
                        .Append(" scene searches, so hover could not be observed at all. This row's ")
                        .Append("verdict is unknown, not negative");
                    Latch(RowResource, RowVerdict.NotExercised);
                }
                else if (!_sawPickupHover)
                {
                    _detail.Append(" - PlayerInteraction never hovered a PickupItem, so either depletion ")
                        .Append("produced no loot prefab or the drop is outside reach / off the ")
                        .Append("interactable layer mask. The world object did NOT reach inventory");
                    Latch(RowResource, RowVerdict.Partial);
                }
                else
                {
                    _detail.Append(" - the pickup was hovered and the real Interact command was consumed, ")
                        .Append("but no acquisition was published");
                    Latch(RowResource, RowVerdict.Fail);
                }
            }

            EnterPhase(DrivePhase.Craft);
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

                EnterPhase(DrivePhase.Done);
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
                if (PhaseElapsed < CraftBudgetSeconds &&
                    _fabricatorLookupAttempts < MaxFabricatorLookupAttempts)
                {
                    return;
                }

                _detail.Clear();
                _detail.Append("no live Fabricator component found in ").Append(_fabricatorLookupAttempts)
                    .Append(" scene searches, so no recipe can be started");
                Latch(RowCraft, RowVerdict.Blocked);
                EnterPhase(DrivePhase.Done);
                return;
            }

            if (!_craftStarted)
            {
                // CanCraft walks every ingredient of every recipe against inventory. Re-asking 60 times a
                // second for 14 seconds is a real cost for an answer that only changes when fabricator
                // power or inventory changes, so the sweep runs on a throttle.
                double now = EditorApplication.timeSinceStartup;
                if (_craftEvaluatedAt > 0.0 &&
                    now - _craftEvaluatedAt < CraftEvaluationIntervalSeconds &&
                    PhaseElapsed < CraftBudgetSeconds)
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
                    if (PhaseElapsed < CraftBudgetSeconds)
                        return;

                    _detail.Clear();
                    _detail.Append("Fabricator is live with visibleRecipes=").Append(_visibleRecipeCount)
                        .Append(" totalRecipes=").Append(fabricator.TotalRecipeCount)
                        .Append(" lockedRecipes=").Append(fabricator.LockedRecipeCount)
                        .Append(" but CanCraft is false for all of them; the Resource leg delivered ")
                        .Append(_sawManualPickupAcquire ? "1 acquisition" : "nothing")
                        .Append(", so no recipe/repair can consume a resource on this route");
                    Latch(RowCraft, RowVerdict.Blocked);
                    EnterPhase(DrivePhase.Done);
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
                    EnterPhase(DrivePhase.Done);
                    return;
                }

                return;
            }

            if (PhaseElapsed < CraftBudgetSeconds)
                return;

            _detail.Clear();
            _detail.Append("StartCraft accepted (isCraftingObserved=").Append(_craftObservedRunning)
                .Append(", craftProgressPeak=").Append(F(_craftProgressPeak))
                .Append(") but no ItemAcquiredSignal sourceKind=")
                .Append(ItemAcquiredSignalSourceKinds.Fabricator)
                .Append(" arrived within ").Append(F(CraftBudgetSeconds))
                .Append("s - the craft was consumed but never delivered, row NOT accepted");
            Latch(RowCraft, RowVerdict.Partial);
            EnterPhase(DrivePhase.Done);
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

        private static void Latch(int row, RowVerdict verdict)
        {
            if (row < 0 || row >= RowCount || _latched[row])
                return;

            _verdicts[row] = verdict;
            _details[row] = _detail.ToString();
            _latched[row] = true;
        }

        private static void LatchBlocked(
            int row,
            string prefix,
            double elapsed,
            string a, bool aValue,
            string b, bool bValue,
            string c, bool cValue)
        {
            _detail.Clear();
            _detail.Append(prefix).Append(F((float)elapsed))
                .Append(a).Append(aValue)
                .Append(b).Append(bValue)
                .Append(c).Append(cValue);
            Latch(row, RowVerdict.Blocked);
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
        /// Anything the schedule never reached stays NOT_EXERCISED with the phase it died in named. A
        /// row the driver did not actually drive must never be reported as anything else.
        /// </summary>
        private static void FinaliseUnlatchedRows()
        {
            for (int row = 0; row < RowCount; row++)
            {
                if (_latched[row])
                    continue;

                _detail.Clear();
                _detail.Append("driver ran out of budget in phase ").Append(_phase.ToString())
                    .Append(" after ").Append(F((float)(EditorApplication.timeSinceStartup - _startedAt)))
                    .Append("s and never reached this row");
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

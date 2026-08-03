using System;

using System.Collections.Generic;

using System.Globalization;

using System.IO;

using System.Text;

using Hecton8.Core;

using UnityEditor;

using UnityEditor.SceneManagement;

using UnityEngine;



namespace Hecton8.EditorTools.Diagnostics

{

    /// <summary>

    /// Enters Play Mode from batchmode, lets the game boot, reports what the runtime actually

    /// contains, and exits.

    ///

    /// This exists because nothing in the project could prove runtime behaviour. Every static

    /// probe here answers "is it wired", never "does it run". The PlayMode test assembly is

    /// disabled by a NEVER_COMPILE_TESTS define constraint, so no test can execute without

    /// changing project-wide settings, and a headless machine has no other way in. The result was

    /// a steady stream of changes verified by "0 CS errors" and nothing else.

    ///

    /// Usage - note there must be NO -quit, or the editor closes before Play Mode ever starts:

    ///   Unity.exe -batchmode -nographics -projectPath . -logFile Logs/playprobe.log \

    ///             -executeMethod Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe.Run \

    ///             -h8Scene Assets/_Project/Scenes/02_HECTON_WORLD.unity -h8WarmupFrames 240

    ///

    /// The probe calls EditorApplication.Exit itself: 0 if every check passed, 1 otherwise.

    ///

    /// EVERY run leaves one machine-readable artifact, by default at

    /// &lt;projectRoot&gt;/Logs/h8_playprobe_route.json, overridable with -h8RouteArtifact. It carries the

    /// per-phase clock table, the save-directory diff, the determinism block described below, and a

    /// verdict for all ten rows of the First 20 Minutes Required Route - rows with no producer report

    /// NOT_EXERCISED rather than staying silent. Before that artifact existed, four runs on the same day

    /// used four different argument sets, emitted only log text, and could not be compared with one

    /// another.

    ///

    /// The determinism block (console tag <c>DETERMINISM</c>, artifact key <c>determinism</c>) is the

    /// answer to "did two runs of one seed do the same thing": it publishes the master state hash that

    /// LockstepStateValidator already computes, the post-simulation frame it was sampled at, and the

    /// slow-tick time the dispatcher discarded. This probe computes no hash of its own, and the block's

    /// <c>coverage</c> field states in the artifact exactly which four buffers the hash does and does not

    /// cover - read it before quoting a match as proof that two worlds agreed.

    ///

    /// WHEN THERE IS NO HASH, the block says WHICH of three things was wrong, because they have three

    /// different owners and one measured run reported all three identically. <c>OwnerAbsentNoBuffer</c> means

    /// no <c>LockstepStateValidator</c> exists at all - a lifetime defect. <c>OwnerPresentBufferUnopened</c>

    /// means one exists and its vault buffer open failed silently - a vault-timing defect.

    /// <c>NeverSampled</c> means the buffer is open and the run simply ended before a hash frame - a budget

    /// matter. Alongside them the <c>DETERMINISM OWNER TRACE</c> lines sample the owner's existence at the

    /// boot warmup, at the first gameplay tick and at end of run, which brackets the window an owner

    /// disappeared in WITHOUT a second editor run; before that trace existed, separating "never created"

    /// from "created then destroyed by a scene load" cost one full run per hypothesis.

    ///

    /// The slow-tick discard is reported as a first-class COMPARABILITY caveat

    /// (<c>DETERMINISM SLOWTICK DISCARD</c>, artifact <c>runComparable</c> / <c>comparabilityCaveat</c>), not

    /// as one number among a dozen. Two runs of one seed that discarded different amounts of owed simulation

    /// time did not simulate the same world, so a hash difference between them proves nothing. The clamp that

    /// discards the time is CORRECT and stays - it is the anti-death-spiral guard.

    ///

    /// <c>-h8ReviveDeterminismOwner</c> is OPT-IN and off by default: it creates a validator in memory when

    /// the session has none, to answer whether the hash path can produce a number at all. It is deliberately

    /// not the default, because the owner's absence is the only evidence this harness has about it and a

    /// silently manufactured owner would hide that defect behind a hash. See TryReviveDeterminismOwner.

    ///

    /// Save-leg arguments: -h8SaveSeconds (default 60, clamped so the leg cannot push the run past

    /// -h8TimeoutSeconds), -h8SaveSlot (0..2), -h8SkipSaveLeg to disable it. The leg only runs after

    /// a game was actually started and the world scene settled.

    ///

    /// Relies on the project having Enter Play Mode Options set to DisableDomainReload

    /// (ProjectSettings/EditorSettings.asset, m_EnterPlayModeOptions: 1), which is why a plain

    /// static state machine survives the transition. Should that ever change, the statics reset

    /// mid-run and the probe stalls rather than lying - always run it under an external timeout.

    /// </summary>

    public static class H8_HeadlessPlayModeProbe

    {

        private const string Marker = "[H8_PLAYPROBE]";

        private const string DefaultScene = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";



        /// <summary>

        /// Where a New Game run has to start. The normative flow is 00_BOOTSTRAP -> 01_MAIN_MENU ->

        /// 02_HECTON_WORLD (AGENTS.md:162); entering play anywhere else is the recovery scenario, not

        /// the product route.

        /// </summary>

        private const string BootstrapSceneAssetPath = "Assets/_Project/Scenes/00_BOOTSTRAP.unity";

        private const int DefaultWarmupFrames = 240;

        private static bool _batchMissingScriptsStripped;
        private static bool _batchParticlesDisabled;



        private static double _hardTimeoutSeconds = 240.0;



        private enum Phase

        {

            Idle,

            WaitingForPlayMode,

            WarmingUp,

            LoadingMenu,

            MenuWarmup,

            StartingGame,

            WaitingForSettle,

            GameplayWarmup,

            SaveRoundTrip,

            Reporting,

            LeavingPlayMode,

        }



        /// <summary>

        /// Verdict for one row of the First 20 Minutes Required Route table

        /// (<c>Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md:68-90</c>).

        ///

        /// <see cref="Partial"/> is a real state with a narrow meaning: SOME of the row's minimum

        /// acceptance was exercised and observed, and the row is NOT accepted. It exists because the

        /// contract's "Save/load" row is one row covering two operations, and reporting a proven save

        /// with an unattempted load as <see cref="Pass"/> is exactly the half-closed seam this harness

        /// is supposed to expose.

        ///

        /// Declared in ascending severity so <see cref="RecordMoment"/> can enforce "the worse verdict

        /// wins" with a single comparison: an early optimistic reading can never mask a later failure.

        /// </summary>

        private enum MomentVerdict : byte

        {

            NotExercised = 0,

            Pass = 1,

            Partial = 2,

            Blocked = 3,

            Fail = 4,

        }



        private struct RouteMoment

        {

            public string Name;

            public MomentVerdict Verdict;

            public string Detail;

        }



        private struct PhaseSample

        {

            public string Phase;

            public double WallSeconds;

            public int GameFrames;

            public int ProbeTicks;

            public bool DuringPlay;

        }



        private struct SaveFileFacts

        {

            public string Name;

            public long Length;

            public long WriteTicksUtc;

            public ulong ContentHash;

            public bool Hashed;

        }



        private struct SaveDirectoryDiff

        {

            public int Added;

            public int Removed;

            public int Changed;

            public long ByteDelta;

            public string Lines;



            public int TotalChanges => Added + Removed + Changed;

        }



        private const string MenuSceneName = "01_MAIN_MENU";



        /// <summary>

        /// Scene NAME of the boot scene, as <see cref="UnityEngine.SceneManagement.Scene.name"/>

        /// reports it. <see cref="BootstrapSceneAssetPath"/> is the asset path and does not compare

        /// equal to a live scene's name.

        /// </summary>

        private const string BootstrapSceneName = "00_BOOTSTRAP";

        private const int MenuWarmupFrames = 120;



        /// <summary>

        /// How long a DISABLED <c>MainMenuController</c> is tolerated before the probe stops waiting for

        /// it to enable itself. Not a timeout in the usual sense - the condition is decided in the first

        /// second and this grace only covers the window where the controller may not have run

        /// <c>Awake</c> yet. Kept short because the alternative, measured, was 201 wall seconds and

        /// 11490 game frames spent waiting for a flag that cannot flip.

        /// </summary>

        private const double DisabledMenuGraceSeconds = 12.0d;



        // How long to let the game bring up its OWN menu. Default is generous on purpose: the

        // bootstrap -> menu handoff is a Single load with an activation gate, and rushing it is how

        // the earlier versions of this probe went wrong twice.

        // SECONDS, not ticks. Measured: 3300 EditorApplication.update callbacks advanced the game

        // by 19 frames in 13.7s wall - about one game frame per second, with or without -nographics.

        // Boot frames genuinely cost that much in the editor, so every frame budget this probe used

        // before was worth a couple of dozen game frames and proved nothing. Wall time is the only

        // honest unit here.

        private static double _menuWaitSeconds = 300.0;



        // Loading a menu ourselves is OFF by default. When the probe did it additively on top of a

        // still-active 00_BOOTSTRAP, New Game then issued a Single load that unloaded both scenes,

        // SceneRuntimeService's activation-gate loop exited on isActiveAndEnabled without ever

        // calling ReleaseSceneActivation, and 02_HECTON_WORLD sat at roots=0 isLoaded=false forever.

        // No watchdog or emergency-release line appeared in the log, which is how we know that loop

        // was not running. That deadlock is almost certainly probe-induced - it is a runtime state

        // the game never produces - so it must not be created by default and must not be reported

        // as a game defect.

        private static bool _forceMenuLoad;



        // After New Game the world scene load is in flight for a long time in batchmode - 2400

        // gameplay frames were not enough for 02_HECTON_WORLD to report isLoaded. Counting frames

        // measures the wrong thing; wait for the loads to actually finish.

        private static double _settleWaitSeconds = 300.0;

        private static double _gameplaySeconds = 60.0;

        private static double _phaseStartedAt;



        // Whether H8_HeadlessWorldDriver produces on the input lanes during the gameplay window. See

        // that file's header for why it is a producer and not a state writer.

        private static bool _worldDriverEnabled = true;

        private static bool _worldDriverStarted;



        // One-shot latch for EnableDisabledPlacementOwnersInMemory. It is what keeps that method's

        // FindObjectsByType call a cold diagnostic step rather than a per-tick hot-path violation.

        private static bool _placementOwnersRepaired;



        /// <summary>

        /// Hard cap on the tick grace granted after the gameplay window closes. The driver's own tick

        /// floors sum to 24 for the entire schedule, so 48 is two full compressed schedules' worth and

        /// cannot become an open-ended extension. Counted in TICKS on purpose: the driver's remaining work

        /// is a fixed number of handshake steps, and on this harness a tick has cost anywhere from 0.23 s

        /// to 132 s, so no number of seconds expresses the same guarantee.

        /// </summary>

        private const int WorldDriverGraceTickCap = 48;



        /// <summary>Wall seconds of the hard timeout the grace refuses to eat into, so a grace can never

        /// convert a starved row into a TIMEOUT line that loses every verdict the run did produce.</summary>

        private const double GraceHardTimeoutMarginSeconds = 20.0;



        // L16: batchmode WallClock often yields unscaledDeltaTime==0 so RunFixedStepAccumulator

        // early-outs and HPM.FixedTick never runs (hop2 ABSENT, movementIntent01max=0) despite the

        // world driver publishing hot overrides. Mirror HeadlessSimulationRunner.EnsureHeadlessSimulationClock:

        // unpause + headless dilation + EnableStepBoundedTime so the product dispatcher supplies a

        // real fixed unscaled dt per update. Probe is INPUT PRODUCER only via WorldDriver; this is

        // the simulation CLOCK arm, not a mock hop2 path.

        // L18: dil=1.0 — step-bound (L16) already supplies non-zero unscaled dt for FixedTick.

        // dil=100 caused PhysX AABB (L17a) / MapMagic LOD (L17b) crashes and fixed-lane temporal compression every frame; not required for Swim hop2 route.

        private const float ProbeTimeDilationScalar = 1f;

        private const float ProbeStepBoundedDeltaSeconds = 0.04f;

        private const float ProbeClockEnsureIntervalSeconds = 5f;

        private const uint ProbeSimClockHash = 0x48385043u; // 'H8PC'



        private static double _lastProbeClockEnsureRealtime;

        private static bool _probeSimClockArmed;



        // L17: HSR drains FO scene-rebase every Update while bootstrap lock can starve FixedTick

        // (RunDispatcherUpdate returns after PreSim when IsOriginShiftBootstrapLocked and TryFlush

        // cannot clear; LateFrame hard-returns on the same lock without TryFlush). Probe never

        // called TryFlush — hop1/presim advanced while lateFrameTick/pumpFired froze and hop2

        // stayed ABSENT. Mirror HSR FO drain + throttled FODRAIN snapshot (not a hop2 mock).

        private const double ProbeFoDrainDiagIntervalSeconds = 5.0;

        private static double _lastProbeFoDrainDiagRealtime;

        private static int _probeFoDrainCalls;

        private static int _probeFoDrainCleanCount;



        private static int _worldDriverGraceTicks;

        private static bool _graceOpenedLogged;

        private static bool _graceClosedLogged;



        /// <summary>

        /// When the gameplay window's clock starts: the FIRST tick of GameplayWarmup, not the phase

        /// transition into it.

        ///

        /// The window used to be measured from _phaseStartedAt, which TickWaitingForSettle sets

        /// immediately before SetPhase(GameplayWarmup) - one editor tick earlier than the tick that calls

        /// H8_HeadlessWorldDriver.Begin(). Those two origins are not the same instant and the difference is

        /// not small: on the measured run the GameplayWarmup phase clock read 165.186s while the driver's

        /// own elapsed read 160.430s, so 4.756s of the window was spent before the driver existed. The

        /// window is 63.0 + 4.0 = 67.0s, which left the driver 62.244s of its 63.0s schedule - the margin

        /// was NEGATIVE before a single frame stalled, and the last phase in the schedule is the one that

        /// pays. That alone produces a NOT_EXERCISED CraftRepairBuild row on a completely smooth run.

        ///

        /// Rebasing here rather than widening the window keeps the fix honest: the window still bounds

        /// 67 seconds of gameplay, it just stops charging the driver for the tail of the scene transition.

        /// Set unconditionally, so a -h8SkipWorldDriver run measures the same window as a driven one.

        /// </summary>

        private static double _gameplayWindowStartedAt;



        private static Phase _phase = Phase.Idle;

        private static double _startedAt;

        private static int _frames;

        private static int _gameplayFrames;

        private static int _warmupFrames = DefaultWarmupFrames;

        private static int _gameplayFramesTarget;

        private static bool _startNewGame;

        private static int _menuFrames;

        private static int _settleFrames;

        private static int _gameFrameAtPlayStart;

        private static double _playStartedAt;

        private static AsyncOperation _menuLoad;

        private static int _failures;



        private const string ReadyLockRejection = "Ready-locked registry rejected registration";

        private static readonly List<string> _rejectedServices = new List<string>();

        private static bool _logHookInstalled;



        // ---- per-phase clock segments -------------------------------------------------------

        // One blended average over the whole session was the only rate this probe reported, and it

        // is worthless: a run that stalls before world load measures the ~1 fps bootstrap phase, a

        // run that reaches 02_HECTON_WORLD measures 35-53 game frames per wall second, and the

        // average of the two describes neither. Every frame budget derived from that single number

        // was wrong by more than an order of magnitude. Segments are closed on every phase

        // transition so each phase reports its own rate.

        private static readonly List<PhaseSample> _phaseSamples = new List<PhaseSample>();

        private static Phase _clockPhase = Phase.Idle;

        private static bool _clockSegmentOpen;

        private static double _clockWallStart;

        private static int _clockGameFrameStart;

        private static int _clockTickStart;

        private static int _totalTicks;



        // ---- First 20 Minutes route moments -------------------------------------------------

        private const string MomentBoot = "Boot";

        private const string MomentWorldLoad = "WorldLoad";

        private const string MomentFirstExit = "FirstExit";

        private const string MomentSwim = "Swim";

        private const string MomentResource = "Resource";

        private const string MomentTool = "Tool";

        private const string MomentCraft = "CraftRepairBuild";



        /// <summary>

        /// The mission spine had no route row at all, while being the best-instrumented axis in the

        /// project: QuestManager is registered both cold (QuestManager.cs:258) and on a real tick lane

        /// (:486), 12 quest assets are authored, and it already publishes a purpose-built public telemetry

        /// surface - QuestSpineAuthoredQuestCount, QuestSpineActivationCount, QuestSpineCompletionCount,

        /// QuestSpineStateGraphReady, CopyQuestSpineTransitions - that nothing consumed. Someone built the

        /// instrument panel and no instrument was ever plugged into it.

        /// </summary>

        private const string MomentMission = "Mission";



        private const string MomentHazard = "Hazard";

        private const string MomentSaveLoad = "SaveLoad";

        private const string MomentProof = "Proof";



        private static readonly List<RouteMoment> _routeMoments = new List<RouteMoment>();



        // ---- save round trip ------------------------------------------------------------------

        // Stable caller hash for IAsyncPersistenceService telemetry, same convention as

        // SaveStation.cs:42 (SSVE). PROB = this probe.

        private const uint ProbeSaveSourceHash = 0x50524F42u;

        private const int HashChunkBytes = 1 << 16;

        private const long MaxHashedFileBytes = 64L * 1024L * 1024L;

        private const int MaxDiffLines = 32;

        private const double SaveDiffPollSeconds = 1.0;

        private const double SaveFlushGraceSeconds = 3.0;

        private const double SaveLegTimeoutMarginSeconds = 5.0;



        private static bool _saveLegEnabled = true;

        private static byte _saveSlotIndex;

        private static double _saveWaitSeconds = 60.0;

        private static double _saveWaitBudget;

        private static bool _saveRequestIssued;

        private static bool _saveAccepted;

        private static bool _saveBusyObserved;

        private static double _saveBusyClearedAt;

        private static double _saveLegStartedAt;

        private static double _saveLastPollAt;

        private static double _saveWaitedSeconds;

        private static string _saveRoot = string.Empty;

        private static string _saveSlotName = string.Empty;

        private static string _saveError = string.Empty;

        private static SaveFileFacts[] _saveBefore = Array.Empty<SaveFileFacts>();

        private static int _saveFilesAfter;

        private static SaveDirectoryDiff _saveDiff;

        private static byte[] _hashBuffer;



        // ---- run artifact ---------------------------------------------------------------------

        // Four runs today used four different argument sets and produced only a megabyte of log

        // each, so none of them are comparable. Every terminal path now writes one JSON with the

        // same schema at the same default path.

        private static string _artifactPath = string.Empty;

        private static string _scenePath = string.Empty;

        private static bool _artifactWritten;



        // ---- determinism state hash -----------------------------------------------------------

        // A simulation nobody can repeat cannot be verified, and no run of this probe emitted a single

        // number two runs could be compared on. This block reads one.

        //

        // IT DOES NOT COMPUTE ONE. The whole hash is built by LockstepStateValidator

        // (Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs) on the dispatcher's

        // POST_SIMULATION lane, and everything here only reads what that owner already published into the

        // vault. A second hasher living in the probe would produce a number that agrees with nothing the

        // game actually runs on, and would agree with itself no matter how wrong it was.

        //

        // READ THE COVERAGE LIMIT BEFORE QUOTING THE NUMBER. LockstepHashCategory has exactly four

        // members, so the "master state hash" folds exactly four vault buffers - the ones

        // LockstepStateValidator.ExecuteHashJobs reads: RigidbodyAUPs, PlayerKinematicState (one entry,

        // mirrored by the validator itself), RoomWaterLevels (at most 256 habitat rooms) and EntityAUPs.

        // Terrain, voxels, ecosystem populations, weather, storms, inventory, quests, fauna genetics,

        // flora, the water simulation and every RNG stream are OUTSIDE it. Two runs agreeing on this hash

        // means those four buffers matched at the sampled frame; it does NOT mean the two worlds matched.

        //

        // Two further limits that decide whether a comparison is valid at all:

        //   - Positions are quantised to a millimetre (LockstepHashMath.QuantizeMillimeter, scale 1000)

        //     and water levels to 1e-4, so any divergence finer than that is INVISIBLE here. A matching

        //     hash is not proof of bit-identical state.

        //   - LockstepHashMath.BuildMasterHash folds the sampled frame into the hash, and the sample

        //     cadence is ResolveHashCadenceFrames() - a lerp over HomeostasisBrain.GlobalQualityWeight and

        //     SystemHealthIndex01, both of which react to wall-clock frame times. Two runs can therefore

        //     sample at DIFFERENT frames and produce different hashes from identical state. Compare

        //     lastCleanPostSimFrame first; a hash difference at different frames proves nothing.



        /// <summary>

        /// What the end-of-run determinism read actually found. Ordered from "no evidence" upward so an

        /// absent owner can never be read as a matching hash.

        /// </summary>

        private enum DeterminismCapture : byte

        {

            NotRead = 0,

            NoPlaySession = 1,

            NoDataVault = 2,



            /// <summary>

            /// NO <c>LockstepStateValidator</c> instance exists anywhere in the running session and

            /// <c>BufferID.LockstepMasterStateHash</c> is unallocated. The owner is ABSENT, so the defect is

            /// in the owner's LIFETIME and no amount of hashing work can be at fault.

            ///

            /// Split out of the former single <c>NoHashBuffer</c> state, which reported this case, the case

            /// below and a zero hash over a live buffer identically. Distinguishing them by hand cost a

            /// whole editor run per hypothesis, because the three have three different owners.

            /// </summary>

            OwnerAbsentNoBuffer = 3,



            /// <summary>

            /// A validator instance EXISTS and <c>BufferID.LockstepMasterStateHash</c> is still unallocated,

            /// so the owner is present and its buffer open failed or never ran. The fix is in why

            /// <c>LockstepStateValidator.OpenOrAcquireVaultBuffer</c> refused - a null

            /// <c>ResolveDataVault()</c>, <c>IsAllocationLocked</c> or <c>IsCompactionFenceActive</c> - not

            /// in the component's lifetime.

            /// </summary>

            OwnerPresentBufferUnopened = 4,



            /// <summary>

            /// The buffer IS allocated and the hash is still zero: the owner opened its buffers and the run

            /// ended before a hash frame. Nothing is broken in the wiring; the run was too short or the

            /// cadence too long.

            /// </summary>

            NeverSampled = 5,

            Sampled = 6,

        }



        private struct DeterminismCategorySample

        {

            public string Name;

            public uint Hash;

            public uint Count;

            public uint Flags;

        }



        // Literal mirrors of LockstepStateValidator's PRIVATE ArrayFlag* constants

        // (LockstepStateValidator.cs:313-315). They are private there and this assembly cannot reach

        // them, so these three lines WILL drift silently if that file ever renumbers its bits. That is

        // why the drift-proof reading is printed beside every hash instead: a category whose Count is 0

        // contributed nothing no matter what any flag says, and the counts come from the owner's own

        // LockstepArrayHash records.

        private const uint DeterminismArrayFlagMissing = 1u << 0;

        private const uint DeterminismArrayFlagTruncated = 1u << 1;

        private const uint DeterminismArrayFlagNonFinite = 1u << 2;



        /// <summary>

        /// One observation of the determinism owner's existence, taken at a NAMED moment in the run.

        ///

        /// WHY A TRACE AND NOT A SINGLE READING. The end-of-run read reported

        /// <c>instances=0 enabled=0</c> with the hash buffer unallocated, and that single number cannot tell

        /// "the RuntimeInitializeOnLoadMethod never created the owner" from "the owner was created, lived,

        /// and was destroyed by a scene transition". Those are different defects in different places, and

        /// the only way to separate them was to run the editor again with a different guess - about 6

        /// minutes of wall clock per hypothesis on this harness. Sampling at the boot warmup, at the first

        /// gameplay tick and at end of run brackets the death window inside ONE run.

        /// </summary>

        private struct DeterminismOwnerSample

        {

            /// <summary>False means this observation point was never reached, which is itself information -

            /// an absent sample must never read as "zero instances".</summary>

            public bool Taken;



            public int Instances;

            public int Enabled;

            public bool VaultPresent;

            public bool HashBufferPresent;

            public string ActiveScene;

        }



        private static DeterminismOwnerSample _determinismOwnerAtBootWarmup;

        private static DeterminismOwnerSample _determinismOwnerAtGameplayStart;



        /// <summary>

        /// Why the master-hash buffer read failed, in the vault's own terms. Empty when the buffer was

        /// present. <c>TryReadDeterminismBuffer</c> collapses four distinct refusals into one <c>false</c>,

        /// and "not allocated", "generation 0", "handle for a different BufferID" and "buffer shorter than

        /// one element" do not have the same cause.

        /// </summary>

        private static string _determinismHashBufferDiagnosis = string.Empty;



        // Both are read from IDataVault at capture time. They are the two states that make

        // LockstepStateValidator.OpenOrAcquireVaultBuffer refuse a cold allocation outright

        // (LockstepStateValidator.cs:1751), so an owner that is present with no buffer is explained by

        // these before anything else.

        private static bool _determinismVaultAllocationLocked;

        private static bool _determinismVaultCompactionFenceActive;



        private static bool _determinismReviveRequested;

        private static bool _determinismReviveAttempted;

        private static bool _determinismReviveCreated;

        private static string _determinismReviveNote = string.Empty;



        private static DeterminismCapture _determinismState;

        private static ulong _determinismMasterHash;

        private static uint _determinismMasterFlags;

        private static ulong _determinismLastCleanHash;

        private static uint _determinismLastCleanFrame;

        private static bool _determinismHashFromOwnerAccessor;

        private static bool _determinismAccessorVaultDisagreement;

        private static int _determinismValidatorInstances;

        private static int _determinismValidatorEnabled;

        private static uint _determinismDispatcherFrameId;

        private static double _determinismSlowTickDiscardedSeconds;

        private static int _determinismSlowTickDiscardEvents;

        private static DeterminismCategorySample[] _determinismCategories =

            Array.Empty<DeterminismCategorySample>();



        public static void Run()

        {

            // L19 hop2 LIVE: reset missing-script strip latch per probe run.

            _batchMissingScriptsStripped = false;
            _batchParticlesDisabled = false;

            ResetRunState();



            // Claim the play-mode session before anything enters it.

            //

            // H8_PlayModeScreenshotter is live in this project and captures at roughly its

            // PlayerWaitSeconds + SettleSeconds (~200s of wall time), then calls

            // EditorApplication.Exit(0) - it terminates the editor process with a SUCCESS code. This

            // probe's route needs its settle window plus the gameplay window on top, well over 370s,

            // so the screenshotter won that race in every run. Logs/omega_route19.log is the clean

            // example: the last probe line is "WORLDDRIVER begin ... budget 63s of the 67s gameplay

            // window", the next event is the screenshotter's capture, and the log contains no verdict

            // row at all - while the launcher read exit code 0 and reported a pass.

            //

            // So every "the probe emitted no verdict rows" reading taken before this line existed was

            // measuring a session that had been shut down underneath it. The screenshotter still takes

            // its capture, which is real evidence; only the teardown is withheld while an owner is named.

            Hecton8.Tools.H8_PlayModeScreenshotter.ExternalSessionOwner = nameof(H8_HeadlessPlayModeProbe);



            // When the probe is going to press New Game it MUST start where the product starts.

            // AGENTS.md:162 fixes the normative flow as 00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD,

            // and opening the world scene directly is not that route - it is the RECOVERY scenario, which

            // the product handles by design: HandleSceneLoadedGuard (GameBootstrapper.cs:7114) sees a

            // non-bootstrap scene while _isBootstrapComplete is false and calls TryRecoverEntryVector,

            // which loads 00_BOOTSTRAP as LoadSceneMode.Single (:7164-7166) and so DESTROYS the scene the

            // probe just opened, along with everything in it.

            // Measured consequence of getting this wrong: a run that opened 02_HECTON_WORLD logged the

            // world spawner's Awake TWICE against a single "Step 0: Loading 02_HECTON_WORLD", the first

            // player died with the first world scene, and the probe reported that as a product defect.

            // The recovery was working correctly; the instrument was standing in the wrong place.

            // An explicit -h8Scene still wins - starting mid-route is a legitimate thing to measure, as

            // long as it is asked for rather than defaulted into.

            // -h8StartGame is parsed into _startNewGame further down, AFTER this point, so the argument

            // is read directly here rather than depending on a field that is still false.

            bool willPressNewGame = ReadStringArg("-h8StartGame", null) != null;

            string startScenePath = willPressNewGame ? BootstrapSceneAssetPath : DefaultScene;

            string scenePath = ReadStringArg("-h8Scene", startScenePath);

            _scenePath = scenePath;

            _warmupFrames = Math.Max(1, ReadIntArg("-h8WarmupFrames", DefaultWarmupFrames));



            // A headless boot legitimately stops at 01_MAIN_MENU: gameplay systems - ecosystem,

            // terrain, the world-seed owner - are installed only once a game is actually started.

            // Without this the probe can only ever inspect the menu.

            _gameplayFramesTarget = Math.Max(0, ReadIntArg("-h8GameplayFrames", 0));

            _startNewGame = ReadStringArg("-h8StartGame", null) != null || _gameplayFramesTarget > 0;

            _hardTimeoutSeconds = Math.Max(30.0, ReadIntArg("-h8TimeoutSeconds", 240));

            _menuWaitSeconds = Math.Max(5, ReadIntArg("-h8MenuSeconds", 300));

            _settleWaitSeconds = Math.Max(5, ReadIntArg("-h8SettleSeconds", 300));

            _gameplaySeconds = Math.Max(1, ReadIntArg("-h8GameplaySeconds", 60));

            _forceMenuLoad = ReadStringArg("-h8ForceMenuLoad", null) != null;

            _saveWaitSeconds = Math.Max(1, ReadIntArg("-h8SaveSeconds", 60));

            _saveSlotIndex = (byte)Math.Clamp(

                ReadIntArg("-h8SaveSlot", 0), 0, Hecton8.SaveSystem.SaveEvents.ManualSlotCount - 1);

            _saveLegEnabled = ReadStringArg("-h8SkipSaveLeg", null) == null;



            // OFF by default, and see TryReviveDeterminismOwner for why the default must stay off: the

            // absence of the determinism owner is this harness's only finding about it, and a probe that

            // manufactured one silently would hide the product defect behind a hash. Opt in to answer the

            // separate question "can the hash path produce a non-zero number once an owner exists".

            _determinismReviveRequested = ReadStringArg("-h8ReviveDeterminismOwner", null) != null;



            // The world driver is the only producer for the Swim/Resource/Tool/Craft rows in a headless

            // run. It is on by default because without it those four rows can only ever say

            // NOT_EXERCISED; -h8SkipWorldDriver exists so a run can measure an UNDRIVEN world on purpose,

            // and such a run keeps reporting NOT_EXERCISED rather than inheriting a driven verdict.

            _worldDriverEnabled = ReadStringArg("-h8SkipWorldDriver", null) == null;



            // The driver needs its full schedule inside the gameplay window. Extending the window is

            // safe; silently truncating the driver is not, because a truncated schedule produces

            // NOT_EXERCISED rows that look like a product gap instead of a harness budget.

            //

            // This can raise an explicitly passed -h8GameplaySeconds, so it says so out loud: an argument

            // that is quietly ignored is worse than one that is loudly overridden.

            //

            // THE MARGIN IS A TICK MARGIN, NOT A STALL ALLOWANCE, and it was tuned when the four driven

            // rows were upstream-blocked and every phase failed on its first tick. Now that they execute,

            // 4.0 seconds is worth 3 game frames at the 0.751 frames-per-wall-second this phase actually

            // measured (Logs/h8_playprobe_route.json phases[5]) and less than one frame at the rate that

            // obtained late in that run. Raising it to cover a stall would be the wrong fix twice over:

            // a probe that passes because it was given more time hides the stall, and no fixed number of

            // seconds can cover a single pumped frame that cost 132 of them. The driver's own per-phase

            // ceilings bound the stall; TryGrantWorldDriverGrace covers the tail in TICKS, which is the

            // unit the driver's remaining work is actually denominated in. So this stays at +4.0.

            if (_worldDriverEnabled)

            {

                double required = H8_HeadlessWorldDriver.TotalBudgetSeconds + 4.0;

                if (_gameplaySeconds < required)

                {

                    Debug.Log(

                        $"{Marker} WORLDDRIVER gameplay window raised {_gameplaySeconds:F0}s -> " +

                        $"{required:F0}s to fit the driver schedule; pass -h8SkipWorldDriver to keep the " +

                        "shorter window and leave Swim/Resource/Tool/CraftRepairBuild NOT_EXERCISED");

                    _gameplaySeconds = required;

                }

            }



            // The hard timeout has to be able to CONTAIN the windows configured above, and nothing was

            // checking that. The comment on the block above used to claim the raised gameplay seconds

            // "also come out of -h8TimeoutSeconds"; they do not - _hardTimeoutSeconds is assigned once

            // from the argument and never adjusted. Raising it here silently would be worse, because the

            // caller passed a number on purpose. So the arithmetic is stated instead: with the DEFAULTS

            // (-h8TimeoutSeconds 240, menu 300, settle 300) the timeout cannot contain even the menu

            // wait, and a run configured that way reports TIMEOUT in whatever phase it happened to be in

            // rather than the row it was measuring.

            double configuredWindows =

                _menuWaitSeconds + _settleWaitSeconds + _gameplaySeconds +

                (_saveLegEnabled ? _saveWaitSeconds : 0.0);

            if (_startNewGame && configuredWindows > _hardTimeoutSeconds)

            {

                Debug.Log(

                    $"{Marker} BUDGET WARNING the configured windows sum to {configuredWindows:F0}s " +

                    $"(menu {_menuWaitSeconds:F0} + settle {_settleWaitSeconds:F0} + gameplay " +

                    $"{_gameplaySeconds:F0} + save {(_saveLegEnabled ? _saveWaitSeconds : 0.0):F0}) but " +

                    $"-h8TimeoutSeconds is {_hardTimeoutSeconds:F0}s. The hard timeout can fire mid-route " +

                    "and its TIMEOUT line names the phase it interrupted, not the row that was starved. " +

                    $"Raise -h8TimeoutSeconds above {configuredWindows:F0} or lower the waits.");

            }



            _artifactPath = ReadStringArg("-h8RouteArtifact", ResolveDefaultArtifactPath());



            Debug.Log(

                $"{Marker} START scene={scenePath} warmupFrames={_warmupFrames} " +

                $"gameplayFrames={_gameplayFramesTarget} batchmode={Application.isBatchMode} " +

                $"saveLeg={(_saveLegEnabled ? "on" : "off")} saveSlot={_saveSlotIndex} " +

                $"saveSeconds={_saveWaitSeconds:F0} artifact='{_artifactPath}'");



            InstallRegistryRejectionHook();



            // EnterPlaymode() silently does NOTHING when scripts have compiler errors, and says

            // nothing about why. A broken build therefore presents as "Play Mode never starts": one

            // run burned its whole 1300s budget in phase=WaitingForPlayMode with frames=0 because

            // another agent had landed a CS0234 elsewhere in the project. Fail loudly instead.

            if (EditorUtility.scriptCompilationFailed)

            {

                Debug.Log(

                    $"{Marker} ABORT scripts failed to compile - Play Mode cannot start. Fix the C# " +

                    "errors first; a timeout here reads like a game defect and is not one.");

                Finish(1);

                return;

            }



            try

            {

                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            }

            catch (Exception ex)

            {

                Debug.Log($"{Marker} FAILED to open {scenePath}: {ex.GetType().Name}: {ex.Message}");

                Finish(1);

                return;

            }



            _startedAt = EditorApplication.timeSinceStartup;

            SetPhase(Phase.WaitingForPlayMode);



            EditorApplication.update -= Tick;

            EditorApplication.update += Tick;

            EditorApplication.EnterPlaymode();

        }



        private static void Tick()

        {

            // L19 hop2 LIVE: strip missing-script shells once game ready (after ActivatePlayer).

            // Native GetScriptCache/IsStateMachineBehaviour AV on Behaviour Update.

            if (Application.isBatchMode && !_batchMissingScriptsStripped &&

                Hecton8.Core.BootstrapState.IsGameReady)

            {
                StripMissingScriptsForBatchProbe();

                _batchMissingScriptsStripped = true;

            }
            // L19 hop2 LIVE: disable ParticleSystems under batch once game is live -
            // native Crash!!! ParticleSystem::BeginUpdate / JobQueue under batch+graphics.
            if (Application.isBatchMode && !_batchParticlesDisabled && EditorApplication.isPlaying)
                TryDisableParticleSystemsBatch();



            _totalTicks++;



            if (EditorApplication.timeSinceStartup - _startedAt > _hardTimeoutSeconds)

            {

                Debug.Log($"{Marker} TIMEOUT after {_hardTimeoutSeconds}s in phase={_phase} frames={_frames}");

                Finish(1);

                return;

            }



            switch (_phase)

            {

                case Phase.WaitingForPlayMode:

                    if (EditorApplication.isPlaying)

                    {

                        _gameFrameAtPlayStart = Time.frameCount;

                        _playStartedAt = EditorApplication.timeSinceStartup;

                        Debug.Log(

                            $"{Marker} PLAYING (entered after {EditorApplication.timeSinceStartup - _startedAt:F1}s) " +

                            $"gameFrame={_gameFrameAtPlayStart} timeScale={Time.timeScale}");

                        SetPhase(Phase.WarmingUp);

                    }

                    break;



                case Phase.WarmingUp:

                    // Leaving play mode unexpectedly means the game aborted its own boot. That is a

                    // result, not a probe failure to hide.

                    if (!EditorApplication.isPlaying)

                    {

                        Debug.Log($"{Marker} LEFT PLAY MODE early at frame {_frames} - boot aborted or a script called Exit");

                        Finish(1);

                        return;

                    }



                    if (++_frames >= _warmupFrames)

                    {

                        // The FIRST determinism-owner observation, and the only one taken while

                        // 00_BOOTSTRAP is still the active scene. LockstepStateValidator is created by a

                        // RuntimeInitializeOnLoadMethod(AfterSceneLoad) that runs once per play session

                        // (LockstepStateValidator.cs:359-368), so if the owner is ever going to exist it

                        // exists by now - the warmup is 240 editor ticks deep into play mode. An absent

                        // owner HERE and an absent owner at end of run are different findings, and before

                        // this sample existed the report could not tell them apart.

                        SampleDeterminismOwner(ref _determinismOwnerAtBootWarmup, "BootWarmup");

                        _phaseStartedAt = EditorApplication.timeSinceStartup;

                        SetPhase(_startNewGame ? Phase.LoadingMenu : Phase.Reporting);

                    }

                    break;



                case Phase.LoadingMenu:

                    TickLoadingMenu();

                    break;



                case Phase.MenuWarmup:

                    if (++_menuFrames >= MenuWarmupFrames)

                        SetPhase(Phase.StartingGame);

                    break;



                case Phase.StartingGame:

                    _phaseStartedAt = EditorApplication.timeSinceStartup;

                    // One shot. If the menu is not there, say so and report on the menu-state

                    // runtime rather than silently pretending a game was started.

                    SetPhase(TryStartNewGame() ? Phase.WaitingForSettle : Phase.Reporting);

                    break;



                case Phase.WaitingForSettle:

                    TickWaitingForSettle();

                    break;



                case Phase.GameplayWarmup:

                    if (!EditorApplication.isPlaying)

                    {

                        Debug.Log($"{Marker} LEFT PLAY MODE during gameplay warmup at frame {_gameplayFrames}");

                        Finish(1);

                        return;

                    }



                    _gameplayFrames++;



                    // The window's clock starts on the first tick that actually pumps gameplay, which is

                    // also the tick that starts the driver. See _gameplayWindowStartedAt for why sharing an

                    // origin with the driver's budget is load-bearing and not tidiness.

                    if (_gameplayWindowStartedAt <= 0.0)

                    {

                        _gameplayWindowStartedAt = EditorApplication.timeSinceStartup;

                        double transitionTail = _gameplayWindowStartedAt - _phaseStartedAt;

                        if (transitionTail > 1.0)

                        {

                            Debug.Log(

                                $"{Marker} GAMEPLAY window clock starts here, {transitionTail:F3}s after " +

                                "the settle transition. Measured from the transition instead, that tail " +

                                "would have come straight out of the driver's schedule and truncated its " +

                                "last phase.");

                        }



                        // L16: arm product step-bounded clock before any WorldDriver.Begin so FixedTick

                        // can consume locomotion overrides (hop2 path) under batchmode WallClock dt=0.

                        EnsureProbeSimulationClock("gameplay-window-start");

                        // L17: drain FO bootstrap lock before first driver tick (HSR parity).

                        DrainProbeFloatingOriginBootstrap("gameplay-window-start");

                    }



                    // Content before measurement. This runs BEFORE the driver starts and OUTSIDE the

                    // _worldDriverEnabled branch on purpose: a -h8SkipWorldDriver run is supposed to

                    // measure an UNDRIVEN world, not an EMPTY one, and those are different claims.

                    if (!_placementOwnersRepaired)

                    {

                        _placementOwnersRepaired = true;

                        EnableDisabledPlacementOwnersInMemory();

                    }



                    // SECOND determinism-owner observation: the world scene has arrived, so every

                    // LoadSceneMode.Single of the boot route has already happened. Comparing this against

                    // the BootWarmup sample brackets the window in which the owner disappeared, in ONE run.

                    // Latched by the sample's own Taken flag; the cold FindObjectsByType inside runs once.

                    if (!_determinismOwnerAtGameplayStart.Taken)

                    {

                        SampleDeterminismOwner(ref _determinismOwnerAtGameplayStart, "FirstGameplayTick");



                        // Order matters and is not cosmetic: the sample above is the EVIDENCE, and a revive

                        // that ran first would overwrite the one observation that proves the owner is

                        // missing in the shipped route.

                        if (_determinismReviveRequested)

                            TryReviveDeterminismOwner();

                    }



                    // The world driver rides THIS tick. It gets no Update, no coroutine and no timer of

                    // its own, so the schedule advances only while the probe is genuinely pumping the

                    // engine - the same discipline that stops "yield return null" hanging a batchmode run.

                    if (_worldDriverEnabled)

                    {

                        if (!_worldDriverStarted)

                        {

                            _worldDriverStarted = true;

                            // L16: re-assert clock immediately before Begin in case dispatcher arrived late.

                            EnsureProbeSimulationClock("worlddriver-begin");

                            // L17: FO drain before Begin so FixedTick path is not permanently early-out.

                            DrainProbeFloatingOriginBootstrap("worlddriver-begin");

                            H8_HeadlessWorldDriver.Begin();

                            Debug.Log(

                                $"{Marker} WORLDDRIVER begin - producing on SignalBus<PlayerInputSignal> " +

                                "(PLIN) and CoreDeterminismSignals input-override; budget " +

                                $"{H8_HeadlessWorldDriver.TotalBudgetSeconds:F0}s of the " +

                                $"{_gameplaySeconds:F0}s gameplay window");

                        }



                        // L16: sustain against late pause / dilation collapse / step-bound drop.

                        MaybeEnsureProbeSimulationClockSustain();

                        // L17: HSR-parity FO drain every gameplay tick (FixedTick starvation root).

                        DrainProbeFloatingOriginBootstrap("gameplay-tick");

                        H8_HeadlessWorldDriver.Tick();

                    }

                    else

                    {

                        // Undriven measurement still needs FixedTick for hop2/depth observability.

                        MaybeEnsureProbeSimulationClockSustain();

                        DrainProbeFloatingOriginBootstrap("gameplay-tick-undriven");

                    }



                    if (EditorApplication.timeSinceStartup - _gameplayWindowStartedAt >= _gameplaySeconds)

                    {

                        // The window is a WALL clock; what the driver still needs is TICKS. Closing on

                        // the wall alone is what turned a schedule that had entered its Craft phase into

                        // a NOT_EXERCISED row: the driver was stopped on the same tick that entered

                        // Craft, so the phase got zero ticks and the mechanic was never looked at.

                        if (_worldDriverStarted && TryGrantWorldDriverGrace())

                            break;



                        // Release the locomotion lane before the save leg, or the save would capture a

                        // player under synthetic input and the save/load row would measure the driver.

                        if (_worldDriverStarted)

                        {

                            H8_HeadlessWorldDriver.Stop(

                                H8_HeadlessWorldDriver.StopCause.ProbeGameplayWindowClosed);

                        }



                        SetPhase(_saveLegEnabled ? Phase.SaveRoundTrip : Phase.Reporting);

                    }



                    break;



                case Phase.SaveRoundTrip:

                    TickSaveRoundTrip();

                    break;



                case Phase.Reporting:

                    try
                    {
                        // L19 hop2 LIVE: batch-safe RunChecks - catch managed faults so ExitPlaymode still runs.
                        RunChecks();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning("[H8_PLAYPROBE] L19 hop2 LIVE: RunChecks threw " + ex.GetType().Name + ": " + ex.Message);
                        _failures++;
                    }

                    SetPhase(Phase.LeavingPlayMode);

                    EditorApplication.ExitPlaymode();

                    break;



                case Phase.LeavingPlayMode:

                    if (!EditorApplication.isPlaying)

                        Finish(_failures == 0 ? 0 : 1);

                    break;

            }

        }



        /// <summary>

        /// Enables every authored-but-disabled procedural placement owner, IN MEMORY, on the first

        /// gameplay tick - before the world driver starts looking for content.

        ///

        /// WHY THIS EXISTS. A scene census on 2026-07-27 found WorldProceduralScatterDirector present

        /// exactly once in 02_HECTON_WORLD, on [MANAGERS]/WorldGen, with the GameObject ACTIVE and the

        /// COMPONENT DISABLED. Every registration that director owns runs from OnEnable

        /// (WorldProceduralScatterDirector.cs:757-777), and Unity never calls OnEnable on a disabled

        /// component - so it registers nothing, ticks nothing and places nothing: no flora, no coral, no

        /// debris, no resource nodes, no fauna spawn windows, no technogenic scatter. It reads as

        /// completely correct in code review. Nothing in the project sets its .enabled at runtime, and

        /// the authoring tool that builds this stack (WorldRuntimeBootstrapAuthoring.cs:120, :685-728)

        /// resolves the component with GetOrAddComponent and rewrites its serialized fields but never

        /// touches m_Enabled, so re-running the authoring tool cannot repair one already there and off.

        ///

        /// WHY IN MEMORY, AND WHY HERE rather than in the edit-mode audit tool. Two hard constraints meet:

        ///   1. AGENTS.md:126, the Sandbox Firewall Rule, forbids automated runners and scripts from

        ///      calling EditorSceneManager.SaveScene, PrefabUtility.SaveAsPrefabAsset or

        ///      EditorUtility.SetDirty on production assets, so that no automated pass can wipe authored

        ///      work, and requires that any runtime adjustment occur IN-MEMORY ONLY. So no batchmode pass

        ///      may write the .unity file. H8_PlacementOwnerEnabledAudit honours that by repairing only

        ///      what a human already has open and never saving - which is why invoking it with

        ///      -executeMethod reports "NOTHING TO REPAIR ... no placement owner exists in any loaded

        ///      scene": batchmode loads no scene, so it inspects nothing. That is the tool behaving

        ///      correctly, not failing.

        ///   2. This probe does not open 02_HECTON_WORLD either. When it is going to press New Game it

        ///      opens 00_BOOTSTRAP (:435), because HandleSceneLoadedGuard (GameBootstrapper.cs:7114)

        ///      would otherwise see a non-bootstrap scene while _isBootstrapComplete is false, call

        ///      TryRecoverEntryVector, load 00_BOOTSTRAP as LoadSceneMode.Single (:7164-7166) and DESTROY

        ///      the scene the probe had opened. The world scene therefore arrives at RUNTIME, and any

        ///      edit-mode repair made before EnterPlaymode would be thrown away along with it.

        /// The only moment where the director both exists and is still repairable is inside play mode,

        /// which is exactly here. Play-mode state is discarded when play mode exits, so this cannot reach

        /// disk even by accident: it writes no asset, marks no scene dirty and records no Undo entry.

        ///

        /// WHAT THIS IS NOT. It is not the fix. The scene still ships with the component disabled, and a

        /// human still has to open 02_HECTON_WORLD, run the menu item

        /// "Hecton8/Diagnostics/Enable Disabled World Placement Owners" and save it. Until that happens

        /// every PLAYER session places nothing while every headless session places content - a divergence

        /// between the instrument and the product, which is the one thing a probe must never hide. So the

        /// summary line below states the divergence outright whenever a repair actually fired.

        ///

        /// FindObjectsByType with FindObjectsInactive.Include is used instead of walking scene roots

        /// because a director on a DontDestroyOnLoad object, or one added with AddComponent at runtime,

        /// is invisible to a root walk - the blind spot H8_PlacementOwnerEnabledAudit documents as

        /// belonging to this probe. The hot-path ban on FindObjectsByType does not apply because this is

        /// a single cold call latched by _placementOwnersRepaired, not a cadence path.

        /// </summary>

        private static void EnableDisabledPlacementOwnersInMemory()

        {

            Hecton8.World.WorldProceduralScatterDirector[] directors =

                UnityEngine.Object.FindObjectsByType<Hecton8.World.WorldProceduralScatterDirector>(

                    FindObjectsInactive.Include,

                    FindObjectsSortMode.None);



            if (directors.Length == 0)

            {

                Debug.Log(

                    $"{Marker} PLACEMENT OWNER none present in the running session. So an empty world in " +

                    "this run is NOT the disabled-component defect - the owner is absent entirely, which " +

                    "points at a missing authoring step rather than a checkbox.");

                return;

            }



            int enabledByProbe = 0;

            int alreadyEnabled = 0;

            int stillInert = 0;



            for (int i = 0; i < directors.Length; i++)

            {

                Hecton8.World.WorldProceduralScatterDirector director = directors[i];

                if (director == null)

                    continue;



                if (director.enabled)

                {

                    alreadyEnabled++;

                    continue;

                }



                director.enabled = true;

                enabledByProbe++;



                Debug.Log(

                    $"{Marker} PLACEMENT OWNER ENABLED IN MEMORY '{director.gameObject.name}' " +

                    $"scene='{director.gameObject.scene.name}' - restores up to " +

                    $"{director.AuthoredScatterWindowPlacementCeiling} placements per scatter window.");



                // An inactive GameObject still gets no Awake and no OnEnable, so enabling the component

                // alone does not make it live. Activating the object could undo a deliberate authoring

                // decision, so this reports and stops rather than guessing - the same line the edit-mode

                // audit draws.

                if (!director.gameObject.activeInHierarchy)

                {

                    stillInert++;

                    Debug.Log(

                        $"{Marker} PLACEMENT OWNER STILL INERT '{director.gameObject.name}' - the component " +

                        "is now enabled but its GameObject is INACTIVE, so Unity still runs no Awake and no " +

                        "OnEnable on it. This owner will place nothing regardless. Activating the object is " +

                        "an authoring decision this probe will not make.");

                }

            }



            Debug.Log(

                $"{Marker} PLACEMENT OWNER SUMMARY found={directors.Length} enabledByProbe={enabledByProbe} " +

                $"alreadyEnabled={alreadyEnabled} stillInertAfterEnable={stillInert}");



            if (enabledByProbe > 0)

            {

                Debug.Log(

                    $"{Marker} PLACEMENT OWNER DIVERGENCE this run does NOT match a player session. The " +

                    "shipped scene still has the component disabled, so any world content measured after " +

                    "this line exists only because the probe enabled it in memory. Persist it by opening " +

                    "Assets/_Project/Scenes/02_HECTON_WORLD.unity, running the menu item " +

                    "'Hecton8/Diagnostics/Enable Disabled World Placement Owners' and saving the scene.");

            }

        }



        /// <summary>

        /// Grants the world driver one more probe tick after the gameplay window has closed, up to a hard

        /// cap of <see cref="WorldDriverGraceTickCap"/> ticks. Returns true while the grace is open.

        ///

        /// WHY THIS IS NOT "JUST GIVE IT MORE TIME". The wall window and the driver's remaining work are

        /// denominated in different units and the exchange rate is not stable: the measured GameplayWarmup

        /// phase ran 124 game frames in 165.186 wall seconds, and one of those frames cost about 132 s

        /// while the other 123 cost about 0.23 s each. Adding seconds to the window therefore buys an

        /// unknown number of ticks - three, or a fraction of one - which is exactly why the existing

        /// +4.0s margin failed. Adding TICKS buys precisely the handshake steps the remaining phases need,

        /// and the cost is bounded by a countable number that appears in the log.

        ///

        /// It is also not a way to hide a stall. The grace is refused unless the driver says it still owes

        /// ticks, every grant is counted, the fact that a grace was needed is logged once with the phase

        /// that ate the clock named, and the driver's own compression marks every row it produced during

        /// the grace as UNMEASURED. A run that needs the grace looks worse in the log than one that does

        /// not - it just stops throwing away the four verdicts it had almost finished collecting.

        /// </summary>

        private static bool TryGrantWorldDriverGrace()

        {

            if (!H8_HeadlessWorldDriver.IsActive)

                return false;



            int owed = H8_HeadlessWorldDriver.MinimumTicksOwed;

            if (owed <= 0)

                return false;



            if (_worldDriverGraceTicks >= WorldDriverGraceTickCap)

            {

                if (!_graceClosedLogged)

                {

                    _graceClosedLogged = true;

                    Debug.Log(

                        $"{Marker} WORLDDRIVER grace EXHAUSTED after {_worldDriverGraceTicks} ticks with " +

                        $"{owed} still owed in phase {H8_HeadlessWorldDriver.CurrentPhaseName}. The " +

                        "remaining rows close as NOT_EXERCISED and say so; this is a harness shortfall, " +

                        $"not a product gap. Heaviest phase: {H8_HeadlessWorldDriver.WorstPhaseName} at " +

                        $"{H8_HeadlessWorldDriver.WorstPhaseWallSeconds:F1}s.");

                }



                return false;

            }



            // A grace must never be the reason a run dies on the hard timeout: that path loses the whole

            // report, including the rows that already resolved.

            if (EditorApplication.timeSinceStartup - _startedAt >

                _hardTimeoutSeconds - GraceHardTimeoutMarginSeconds)

            {

                if (!_graceClosedLogged)

                {

                    _graceClosedLogged = true;

                    Debug.Log(

                        $"{Marker} WORLDDRIVER grace REFUSED with {owed} ticks owed in phase " +

                        $"{H8_HeadlessWorldDriver.CurrentPhaseName}: only " +

                        $"{_hardTimeoutSeconds - (EditorApplication.timeSinceStartup - _startedAt):F0}s " +

                        $"left of the {_hardTimeoutSeconds:F0}s hard timeout and the report needs that " +

                        "margin. Raise -h8TimeoutSeconds to let the schedule finish.");

                }



                return false;

            }



            if (!_graceOpenedLogged)

            {

                _graceOpenedLogged = true;

                Debug.Log(

                    $"{Marker} WORLDDRIVER the {_gameplaySeconds:F0}s gameplay window closed with the " +

                    $"schedule still in phase {H8_HeadlessWorldDriver.CurrentPhaseName} owing {owed} " +

                    $"ticks (driver elapsed {H8_HeadlessWorldDriver.ElapsedSeconds:F1}s of " +

                    $"{H8_HeadlessWorldDriver.TotalBudgetSeconds:F0}s, compressed=" +

                    $"{H8_HeadlessWorldDriver.IsCompressed}). Granting up to {WorldDriverGraceTickCap} " +

                    "further TICKS - not seconds - so the remaining rows produce a real verdict instead " +

                    $"of NOT_EXERCISED. The phase that spent the schedule was " +

                    $"{H8_HeadlessWorldDriver.WorstPhaseName} at " +

                    $"{H8_HeadlessWorldDriver.WorstPhaseWallSeconds:F1}s - fix that, not the rows.");

            }



            _worldDriverGraceTicks++;

            return true;

        }



        /// <summary>

        /// The probe counts EditorApplication.update callbacks and calls them "frames". Whether one

        /// of those equals one game frame is an assumption, and every conclusion drawn from a frame

        /// budget rests on it. If the player loop advances far more slowly than the editor loop then

        /// "no menu after 6000 frames" means "no menu after rather few game frames" and says much

        /// less than it appears to.

        ///

        /// This used to print ONE average over the whole session, and that number is not a rate of

        /// anything. Runs that stalled before world load measured a bootstrap phase advancing at

        /// roughly one game frame per wall second; runs that reached 02_HECTON_WORLD measured 35.2,

        /// 51.7 and 52.9 game frames per wall second. A single mean over both describes neither

        /// phase, and the project-wide "batchmode runs about one game frame per second" belief -

        /// which is what every gameplay-window budget in this probe was sized against - came from

        /// exactly that blend. Segments are closed on each phase transition, so each phase now

        /// reports its own rate and the blended line is kept only as a labelled trap.

        /// </summary>

        private static void ReportClockRates()

        {

            int gameFrames = Time.frameCount - _gameFrameAtPlayStart;

            double wall = EditorApplication.timeSinceStartup - _playStartedAt;

            double blended = wall > 0.0 ? gameFrames / wall : 0.0;



            Debug.Log(

                $"{Marker} CLOCKS SESSION probeTicks={_totalTicks} gameFrames={gameFrames} " +

                $"wallSeconds={wall:F1} gameFramesPerWallSecond={blended:F2} " +

                $"timeScale={Time.timeScale} unscaledTime={Time.unscaledTime:F1} captureFramerate={Time.captureFramerate} " +

                "-- BLENDED, DO NOT DERIVE A FRAME BUDGET FROM THIS LINE: it averages the ~1 fps " +

                "bootstrap phase with the ~50 fps world phase. Use the per-phase rows below.");



            if (_phaseSamples.Count == 0)

            {

                Debug.Log($"{Marker} CLOCKS   no completed phase segment - the run never left its first phase");

                return;

            }



            for (int i = 0; i < _phaseSamples.Count; i++)

            {

                PhaseSample sample = _phaseSamples[i];

                double phaseFps = sample.WallSeconds > 0.0 ? sample.GameFrames / sample.WallSeconds : 0.0;

                double tickRate = sample.WallSeconds > 0.0 ? sample.ProbeTicks / sample.WallSeconds : 0.0;

                Debug.Log(

                    $"{Marker} CLOCKS   {sample.Phase,-18} wall={sample.WallSeconds,8:F1}s " +

                    $"probeTicks={sample.ProbeTicks,7} gameFrames={sample.GameFrames,7} " +

                    $"gameFramesPerWallSecond={phaseFps,8:F2} probeTicksPerWallSecond={tickRate,8:F1} " +

                    $"duringPlay={sample.DuringPlay}");

            }

        }



        /// <summary>

        /// Closes the in-flight clock segment and opens one for <paramref name="next"/>. Every phase

        /// transition goes through here; a bare <c>_phase =</c> assignment loses that phase's rate.

        /// </summary>

        private static void SetPhase(Phase next)

        {

            if (next == _phase && _clockSegmentOpen)

                return;



            CloseClockSegment();

            _phase = next;

            _clockPhase = next;

            _clockSegmentOpen = true;

            _clockWallStart = EditorApplication.timeSinceStartup;

            _clockGameFrameStart = Time.frameCount;

            _clockTickStart = _totalTicks;

        }



        private static void CloseClockSegment()

        {

            if (!_clockSegmentOpen)

                return;



            _clockSegmentOpen = false;

            _phaseSamples.Add(new PhaseSample

            {

                Phase = _clockPhase.ToString(),

                WallSeconds = EditorApplication.timeSinceStartup - _clockWallStart,

                GameFrames = Time.frameCount - _clockGameFrameStart,

                ProbeTicks = _totalTicks - _clockTickStart,

                DuringPlay = EditorApplication.isPlaying,

            });

        }



        /// <summary>

        /// SceneRuntimeService is the strongest suspect for the held menu activation. It sets

        /// allowSceneActivation = false and only releases it from inside

        /// `while (Application.isPlaying and _isInitialized and isActiveAndEnabled and !isDone)`.

        /// If that component is not active the loop body never runs, the activation is never

        /// released, and nothing is logged - which matches what the clean runs show exactly:

        /// 01_MAIN_MENU at isLoaded=false roots=0, no watchdog, no exception.

        ///

        /// GlobalRegistry.SceneRuntime is internal, but the public GlobalRegistry.Scene exposes the same

        /// owner as ISceneService, so the registry side is readable from this assembly after all.

        /// _isInitialized is private and cannot be read; isActiveAndEnabled and the public CanLoadScene

        /// are the observable parts.

        ///

        /// This used to walk SceneManager.GetSceneAt + GetRootGameObjects, which is the exact blind spot

        /// called out in ReportRuntimeComponentCensus: that traversal cannot enumerate the

        /// DontDestroyOnLoad scene, so a persistent scene service reads as "not found". It reported

        /// SCENERUNTIME absent while the service was scene-owned in 00_BOOTSTRAP and destroyed by that

        /// scene's unload - the right answer for the wrong reason, and it would report absent just the

        /// same once the service is correctly made persistent. A check that cannot distinguish "fixed"

        /// from "broken" is not a check. FindObjectsByType sees both cases.

        ///

        /// Self-test, same discipline as the component census: the registry and the object census are

        /// read IN THIS RUN and disagreement is reported as an instrument fault rather than a finding.

        /// Registry holds an owner the census cannot see =&gt; the census is broken. Census sees an

        /// instance the registry does not hold =&gt; registration was lost or evicted.

        /// </summary>

        private static void ReportSceneRuntimeService()

        {

            Hecton8.Core.ISceneService registered = Hecton8.Core.GlobalRegistry.Scene;

            string registeredLabel = registered == null ? "null" : registered.GetType().Name;



            // No FindObjectsSortMode overload: it is deprecated in 6000.5 (CS0618) and this check only

            // enumerates, so sort order is irrelevant. The census at ReportRuntimeComponentCensus still

            // uses the deprecated overload and still warns - pre-existing, not changed here, because

            // dropping its explicit None could alter that census's ordering assumptions.

            Hecton8.Core.SceneRuntimeService[] found =

                UnityEngine.Object.FindObjectsByType<Hecton8.Core.SceneRuntimeService>(FindObjectsInactive.Include);



            Debug.Log($"{Marker} SCENERUNTIME registry={registeredLabel} instances={found.Length}");



            for (int i = 0; i < found.Length; i++)

            {

                Hecton8.Core.SceneRuntimeService service = found[i];

                if (service == null)

                    continue;



                Debug.Log(

                    $"{Marker} SCENERUNTIME[{i}] on '{service.gameObject.name}' in scene " +

                    $"'{service.gameObject.scene.name}' isRegistered={ReferenceEquals(service, registered)} " +

                    $"activeAndEnabled={service.isActiveAndEnabled} " +

                    $"goActive={service.gameObject.activeInHierarchy} enabled={service.enabled} " +

                    $"canLoadScene={service.CanLoadScene}");

            }



            if (found.Length == 0 && registered != null)

            {

                Debug.LogError(

                    $"{Marker} SCENERUNTIME INSTRUMENT FAULT - registry holds {registeredLabel} but " +

                    "FindObjectsByType returned 0 instances. Every instance count in this run is suspect.");

                return;

            }



            if (found.Length > 0 && registered == null)

            {

                Debug.LogError(

                    $"{Marker} SCENERUNTIME REGISTRATION LOST - {found.Length} live instance(s) exist but " +

                    "GlobalRegistry.Scene is null, so no ISceneService owner is published.");

                return;

            }



            if (found.Length == 0)

                Debug.Log($"{Marker} SCENERUNTIME absent - no instance anywhere, including DontDestroyOnLoad");

        }



        /// <summary>

        /// Waits for every in-flight scene load to finish before starting the gameplay warmup.

        ///

        /// New Game does complete - 02_HECTON_WORLD appears in the scene list - but it is still

        /// isLoaded=false after 2400 gameplay frames, because a batchmode frame is not a unit of

        /// loading progress. Reporting at a frame count therefore measured the world scene while it

        /// was still streaming in and concluded, wrongly, that nothing had installed.

        /// </summary>

        private static void TickWaitingForSettle()

        {

            var pending = new StringBuilder();

            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)

            {

                UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);

                if (scene.isLoaded)

                    continue;



                if (pending.Length > 0)

                    pending.Append(", ");



                pending.Append(scene.name);

            }



            double waited = EditorApplication.timeSinceStartup - _phaseStartedAt;



            // Scene.isLoaded is false BOTH while a scene streams in and while it is being unloaded,

            // and Unity exposes no isUnloading to tell them apart. So the loop above cannot see the

            // difference between "the world is still arriving" and "the menu we already left is on its

            // way out" - and the transition this probe measures produces exactly the second one.

            //

            // Measured in Logs/omega_route19.log: the world load genuinely completed - the active

            // scene changed to 02_HECTON_WORLD at frame 878, the player was instantiated into it, and

            // GameBootstrapper logged "RequiresGameplaySceneActivation: 02_HECTON_WORLD -> isValid=True,

            // isLoaded=True". The probe still burned the whole 300s budget and recorded WorldLoad as

            // FAIL, because the departing 01_MAIN_MENU sat in the scene list with isLoaded=false the

            // entire time.

            //

            // The load this phase waits for lands on the ACTIVE scene. Once that scene is loaded and is

            // not the boot or menu scene, the transition has arrived. Any remaining isLoaded=false entry

            // is on its way out, and it is named in the verdict rather than hidden - a settle that

            // silently ignored scenes would be the same instrument-blindness in the other direction.

            UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            bool gameplaySceneArrived =

                activeScene.IsValid() &&

                activeScene.isLoaded &&

                !string.Equals(activeScene.name, MenuSceneName, StringComparison.Ordinal) &&

                !string.Equals(activeScene.name, BootstrapSceneName, StringComparison.Ordinal);



            if (pending.Length > 0 && gameplaySceneArrived)

            {

                Debug.Log(

                    $"{Marker} SETTLED after {waited:F0}s - active scene '{activeScene.name}' is loaded; " +

                    $"still-unloading: {pending}");

                RecordMoment(

                    MomentWorldLoad,

                    MomentVerdict.Pass,

                    $"active gameplay scene '{activeScene.name}' finished loading after {waited:F0}s; " +

                    $"loaded scenes={UnityEngine.SceneManagement.SceneManager.sceneCount}; " +

                    $"unloading in background={pending}");

                _phaseStartedAt = EditorApplication.timeSinceStartup;

                SetPhase(Phase.GameplayWarmup);

                return;

            }



            if (pending.Length == 0)

            {

                Debug.Log($"{Marker} SETTLED after {waited:F0}s - no scene load in flight");

                RecordMoment(

                    MomentWorldLoad,

                    MomentVerdict.Pass,

                    $"every scene load finished after {waited:F0}s; loaded scenes={UnityEngine.SceneManagement.SceneManager.sceneCount}");

                _phaseStartedAt = EditorApplication.timeSinceStartup;

                SetPhase(Phase.GameplayWarmup);

                return;

            }



            if (waited >= _settleWaitSeconds)

            {

                Debug.Log($"{Marker} NOT SETTLED after {_settleWaitSeconds:F0}s - still loading: {pending}");

                RecordMoment(

                    MomentWorldLoad,

                    MomentVerdict.Fail,

                    $"still loading after {_settleWaitSeconds:F0}s: {pending}");

                _phaseStartedAt = EditorApplication.timeSinceStartup;

                SetPhase(Phase.GameplayWarmup);

                return;

            }



            if (++_settleFrames % 400 == 0)

                Debug.Log(

                    $"{Marker} settling... {waited:F0}s of {_settleWaitSeconds:F0}s, " +

                    $"gameFrames={Time.frameCount - _gameFrameAtPlayStart}, loading: {pending}");

        }



        /// <summary>

        /// Brings up the menu AFTER boot has finished, which is the whole trick.

        ///

        /// During a normal headless run the menu scene is entered while the bootstrap is still

        /// initialising, so MainMenuController.Awake sees AreAllSystemsReady() == false, calls

        /// BootstrapRouteEnforcer, and sets enabled = false on itself. The component survives but

        /// is inert, and the enforcer's own recovery LoadSceneAsync returns null. By the time this

        /// probe looks, no usable menu exists.

        ///

        /// Loading the menu here, once allSystemsReady is true, lets that same Awake take the happy

        /// path. Additive on purpose: Single would unload 00_BOOTSTRAP and risk taking the very

        /// services the route check is about with it.

        /// </summary>

        private static void TickLoadingMenu()

        {

            if (_menuLoad == null)

            {

                // Prefer the menu the game brings up itself. At the point boot reports ready its

                // 01_MAIN_MENU load is often still in flight, and GetSceneAt reports that scene as

                // isLoaded=false - which is exactly why an earlier version of this probe concluded

                // "no MainMenuController anywhere" and then loaded a second copy on top.

                // A live menu only counts once nothing is still streaming: the game's own

                // 01_MAIN_MENU reports isLoaded=false while its Single load is in flight, and acting

                // during that window is what produced two simultaneous menus last time.

                if (!IsAnySceneLoadInFlight() &&

                    TryFindMainMenu(out Hecton.UI.MainMenu.MainMenuController existing) &&

                    existing.enabled)

                {

                    Debug.Log(

                        $"{Marker} MENU live in scene '{existing.gameObject.scene.name}' after " +

                        $"{EditorApplication.timeSinceStartup - _phaseStartedAt:F0}s of waiting - using the game's own");

                    SetPhase(Phase.StartingGame);

                    return;

                }



                double waited = EditorApplication.timeSinceStartup - _phaseStartedAt;



                // A menu that EXISTS but is DISABLED is not a menu that has not loaded yet, and waiting

                // the full window for it is waiting for something that cannot happen.

                // MainMenuController.Awake disables itself when GameBootstrapper.AreAllSystemsReady() is

                // false, and that gate (GameBootstrapper.cs:675-681) is

                // _isBootstrapComplete && Dispatcher != null && TickManager != null && Save != null &&

                // ObjectPool != null. _isBootstrapComplete is written true at exactly one place

                // (:2373), reached only after all six boot phases succeed. So a disabled menu means boot

                // did not finish, the enabled flag will never flip, and the remaining wait is dead time.

                // Measured before this branch existed: 201 wall seconds and 11490 game frames burned on

                // a condition that was decided in the first second, and the run was killed externally

                // before any terminal phase - so it also left no artifact.

                if (waited >= DisabledMenuGraceSeconds &&

                    !IsAnySceneLoadInFlight() &&

                    TryFindMainMenu(out Hecton.UI.MainMenu.MainMenuController inertMenu) &&

                    !inertMenu.enabled)

                {

                    Debug.Log(

                        $"{Marker} MENU EXISTS BUT IS DISABLED in scene '{inertMenu.gameObject.scene.name}' " +

                        $"after {waited:F0}s - boot did not complete, so the enabled flag will never flip. " +

                        "Advancing to the New Game attempt, which reports the disabled controller precisely, " +

                        "instead of burning the rest of the window on a condition already decided.");

                    SetPhase(Phase.StartingGame);

                    return;

                }



                if (waited < _menuWaitSeconds)

                {

                    if (++_menuFrames % 400 == 0)

                        Debug.Log(

                            $"{Marker} waiting for the game's own menu... {waited:F0}s of {_menuWaitSeconds:F0}s, " +

                            $"gameFrames={Time.frameCount - _gameFrameAtPlayStart}");



                    return;

                }



                if (!_forceMenuLoad)

                {

                    Debug.Log(

                        $"{Marker} MENU never became live in {_menuWaitSeconds:F0}s of play. Not loading one: " +

                        "doing that additively deadlocks the world-scene activation gate and the " +

                        "resulting state is not one the game produces. Pass -h8ForceMenuLoad to override.");

                    _failures++;

                    RecordMoment(

                        MomentWorldLoad,

                        MomentVerdict.Blocked,

                        $"no live MainMenuController in {_menuWaitSeconds:F0}s of play, so New Game was never pressed");

                    SetPhase(Phase.Reporting);

                    return;

                }



                Debug.Log(

                    $"{Marker} MENU none live after {_menuWaitSeconds:F0}s - FORCED load of " +

                    $"'{MenuSceneName}' additively (probe-induced state, results are suspect)");

                _menuLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(

                    MenuSceneName,

                    UnityEngine.SceneManagement.LoadSceneMode.Additive);



                if (_menuLoad == null)

                {

                    Debug.Log($"{Marker} MENU LoadSceneAsync returned null - cannot reach gameplay");

                    _failures++;

                    RecordMoment(

                        MomentWorldLoad,

                        MomentVerdict.Blocked,

                        "forced menu LoadSceneAsync returned null, so New Game was never pressed");

                    SetPhase(Phase.Reporting);

                }



                return;

            }



            if (_menuLoad.isDone)

            {

                Debug.Log($"{Marker} MENU fallback scene loaded");

                _menuLoad = null;

                _menuFrames = 0;

                SetPhase(Phase.MenuWarmup);

            }

        }



        private static bool IsAnySceneLoadInFlight()

        {

            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)

            {

                if (!UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isLoaded)

                    return true;

            }



            return false;

        }



        private static bool TryFindMainMenu(out Hecton.UI.MainMenu.MainMenuController menu)

        {

            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)

            {

                UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);

                if (!scene.isLoaded)

                    continue;



                foreach (GameObject root in scene.GetRootGameObjects())

                {

                    menu = root.GetComponentInChildren<Hecton.UI.MainMenu.MainMenuController>(true);

                    if (menu != null)

                        return true;

                }

            }



            menu = null;

            return false;

        }



        /// <summary>

        /// Presses "New Game" the way the menu button does. Root traversal rather than any

        /// FindObjectOfType variant: those are banned project-wide and the ban is worth honouring

        /// in tooling too.

        /// </summary>

        private static bool TryStartNewGame()

        {

            if (!TryFindMainMenu(out Hecton.UI.MainMenu.MainMenuController menu))

            {

                Debug.Log($"{Marker} NO MainMenuController found in any loaded scene - reporting on the current runtime instead");

                _failures++;

                RecordMoment(

                    MomentWorldLoad,

                    MomentVerdict.Blocked,

                    "no MainMenuController in any loaded scene, so New Game was never pressed");

                return false;

            }



            // A controller that disabled itself in Awake will not respond, and calling into it

            // anyway would look like success in the log.

            if (!menu.enabled)

            {

                Debug.Log(

                    $"{Marker} MENU FOUND BUT DISABLED in scene '{menu.gameObject.scene.name}' - it failed its own " +

                    "bootstrap route check in Awake, so New Game cannot be pressed");

                _failures++;

                RecordMoment(

                    MomentWorldLoad,

                    MomentVerdict.Blocked,

                    $"MainMenuController in '{menu.gameObject.scene.name}' disabled itself in Awake, so New Game cannot be pressed");

                return false;

            }



            Debug.Log($"{Marker} STARTING NEW GAME via MainMenuController in scene '{menu.gameObject.scene.name}'");

            try

            {

                menu.ReadableStartNewGame();

                return true;

            }

            catch (Exception ex)

            {

                Debug.Log($"{Marker} NEW GAME THREW {ex.GetType().Name}: {ex.Message}");

                _failures++;

                RecordMoment(

                    MomentWorldLoad,

                    MomentVerdict.Fail,

                    $"New Game threw {ex.GetType().Name}: {ex.Message}");

                return false;

            }

        }



        /// <summary>

        /// Exercises the SAVE half of the Required Route's save/load row

        /// (<c>FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md:88</c>) and produces the "save directory

        /// diff" the contract's Proof row asks for (<c>:90</c>).

        ///

        /// Before this existed the probe spent its whole gameplay window incrementing a counter and

        /// then reported eight static snapshots of registry slots and shader globals. Nothing in the

        /// project read <c>Application.persistentDataPath</c> to check whether a save actually reached

        /// disk, so the save directory diff had no producer at all.

        ///

        /// The request goes through <c>IAsyncPersistenceService.TryRequestSave</c>, which is what the

        /// in-world Save Station uses (<c>Interaction/SaveStation.cs:179</c>) - not a private path

        /// invented for the harness. <c>SaveManager.ProcessSaveRequest</c> fires

        /// <c>SaveGameAsyncInternal</c> and returns immediately, so completion has to be observed, not

        /// assumed: this polls the directory on a wall clock and cross-checks <c>ISaveService.IsBusy</c>.

        /// Polling happens on EditorApplication.update callbacks, which is the only thing that advances

        /// the engine headlessly (COMMON_SENSE.md:32 - "yield return null" hangs forever in batchmode).

        /// </summary>

        private static void TickSaveRoundTrip()

        {

            if (!EditorApplication.isPlaying)

            {

                FinishSaveLeg(MomentVerdict.Fail, "left Play Mode during the save round trip", countFailure: true);

                return;

            }



            if (!_saveRequestIssued)

                BeginSaveRoundTrip();

            else

                PollSaveRoundTrip();

        }



        private static void BeginSaveRoundTrip()

        {

            _saveRequestIssued = true;

            _saveLegStartedAt = EditorApplication.timeSinceStartup;

            _saveLastPollAt = _saveLegStartedAt;

            _saveBusyClearedAt = 0.0;



            // Never let this leg turn a run that used to finish into a TIMEOUT. The hard timeout is

            // the outer contract; the save budget lives inside whatever is left of it.

            double remaining = _hardTimeoutSeconds - (_saveLegStartedAt - _startedAt);

            _saveWaitBudget = Math.Min(_saveWaitSeconds, remaining - SaveLegTimeoutMarginSeconds);

            if (_saveWaitBudget <= 0.0)

            {

                FinishSaveLeg(

                    MomentVerdict.Blocked,

                    $"only {remaining:F0}s left of the {_hardTimeoutSeconds:F0}s probe hard timeout - raise " +

                    "-h8TimeoutSeconds or lower -h8GameplaySeconds so the save leg fits",

                    countFailure: false);

                return;

            }



            _saveRoot = Hecton8.Core.HectonPersistentPathPolicy.RootPath;

            _saveBefore = SnapshotSaveDirectory(_saveRoot, hashSaveFiles: true, out string snapshotError);

            _saveError = snapshotError ?? string.Empty;



            Debug.Log(

                $"{Marker} SAVE root='{_saveRoot}' filesBefore={_saveBefore.Length} budget={_saveWaitBudget:F0}s" +

                (snapshotError != null ? $" snapshotError={snapshotError}" : string.Empty));

            LogSaveDirectoryListing("BEFORE", _saveBefore);



            Hecton8.Core.IAsyncPersistenceService service = GlobalRegistry.AsyncPersistence;

            if (service == null)

            {

                FinishSaveLeg(

                    MomentVerdict.Blocked,

                    "GlobalRegistry.AsyncPersistence is null after world load - no persistence owner is " +

                    "published, so no save can be requested on this route",

                    countFailure: true);

                return;

            }



            _saveSlotName = Hecton8.SaveSystem.SaveEvents.ResolveManualSlotName(_saveSlotIndex);

            _saveBusyObserved = service.IsBusy;



            bool accepted;

            try

            {

                accepted = service.TryRequestSave(_saveSlotIndex, ProbeSaveSourceHash);

            }

            catch (Exception ex)

            {

                FinishSaveLeg(

                    MomentVerdict.Fail,

                    $"TryRequestSave threw {ex.GetType().Name}: {ex.Message}",

                    countFailure: true);

                return;

            }



            _saveAccepted = accepted;

            Debug.Log(

                $"{Marker} SAVE TryRequestSave(slot={_saveSlotIndex} '{_saveSlotName}') -> {accepted} " +

                $"owner={service.GetType().Name} isInitialized={service.IsInitialized} " +

                $"isBusyAtRequest={_saveBusyObserved} playTimeSeconds={service.CurrentPlayTimeSeconds:F1}");



            if (!accepted)

            {

                FinishSaveLeg(

                    MomentVerdict.Fail,

                    $"TryRequestSave(slot '{_saveSlotName}') was refused: {ReadSaveServiceError()}",

                    countFailure: true);

            }

        }



        private static void PollSaveRoundTrip()

        {

            double now = EditorApplication.timeSinceStartup;

            _saveWaitedSeconds = now - _saveLegStartedAt;



            Hecton8.Core.IAsyncPersistenceService service = GlobalRegistry.AsyncPersistence;

            bool busy = service != null && service.IsBusy;

            if (busy)

            {

                _saveBusyObserved = true;

                _saveBusyClearedAt = 0.0;

            }

            else if (_saveBusyObserved && _saveBusyClearedAt <= 0.0)

            {

                // IsBusy can clear a moment before the last buffer reaches the filesystem, so a cleared

                // flag opens a short grace window rather than deciding the verdict on the spot.

                _saveBusyClearedAt = now;

            }



            bool budgetSpent = _saveWaitedSeconds >= _saveWaitBudget;

            bool graceSpent = _saveBusyClearedAt > 0.0 && now - _saveBusyClearedAt >= SaveFlushGraceSeconds;

            if (!budgetSpent && !graceSpent && now - _saveLastPollAt < SaveDiffPollSeconds)

                return;



            _saveLastPollAt = now;



            // Metadata-only while polling. Hashing every save file once a second for the whole budget

            // would re-read the very files being written and slow the run this probe is measuring;

            // name, byte length and UTC write ticks are enough to notice that something moved.

            SaveFileFacts[] after = SnapshotSaveDirectory(_saveRoot, hashSaveFiles: false, out _);

            SaveDirectoryDiff cheapDiff = DiffSaveDirectory(_saveBefore, after);



            bool decided = cheapDiff.TotalChanges > 0 || budgetSpent || graceSpent;

            if (!decided)

                return;



            // One hashed pass, at the decision point only, so the recorded diff is byte-true rather

            // than "the timestamp moved".

            after = SnapshotSaveDirectory(_saveRoot, hashSaveFiles: true, out _);

            _saveFilesAfter = after.Length;

            _saveDiff = DiffSaveDirectory(_saveBefore, after);

            LogSaveDirectoryListing("AFTER", after);



            if (_saveDiff.TotalChanges > 0)

            {

                Debug.Log(

                    $"{Marker} SAVE DIFF added={_saveDiff.Added} removed={_saveDiff.Removed} " +

                    $"changed={_saveDiff.Changed} byteDelta={_saveDiff.ByteDelta} " +

                    $"after {_saveWaitedSeconds:F1}s{_saveDiff.Lines}");

                FinishSaveLeg(

                    MomentVerdict.Partial,

                    $"save half observed: TryRequestSave(slot '{_saveSlotName}') changed " +

                    $"{_saveDiff.TotalChanges} file(s) under '{_saveRoot}' in {_saveWaitedSeconds:F1}s " +

                    $"(byteDelta={_saveDiff.ByteDelta}). The LOAD half of this row is not exercised by " +

                    "this probe, so the row is not accepted.",

                    countFailure: false);

                return;

            }



            string why = graceSpent

                ? $"the persistence owner reported IsBusy=false at least {SaveFlushGraceSeconds:F0}s ago"

                : $"the {_saveWaitBudget:F0}s save budget elapsed";

            FinishSaveLeg(

                MomentVerdict.Fail,

                $"TryRequestSave(slot '{_saveSlotName}') was accepted but not one byte changed under " +

                $"'{_saveRoot}' in {_saveWaitedSeconds:F1}s ({why}); {ReadSaveServiceError()}",

                countFailure: true);

        }



        private static void FinishSaveLeg(MomentVerdict verdict, string detail, bool countFailure)

        {

            if (countFailure)

                _failures++;



            _saveError = detail;

            RecordMoment(MomentSaveLoad, verdict, detail);

            Debug.Log($"{Marker} SAVE {DescribeVerdict(verdict)} {detail}");

            SetPhase(Phase.Reporting);

        }



        private static string ReadSaveServiceError()

        {

            Hecton8.SaveSystem.SaveManager runtime = GlobalRegistry.SaveRuntime;

            if (runtime == null)

                return "concrete SaveManager not reachable through GlobalRegistry.SaveRuntime";



            string error = runtime.LastOperationError;

            string slot = runtime.LastOperationSlot;

            return string.IsNullOrEmpty(error)

                ? $"SaveManager published no LastOperationError (LastOperationSlot='{slot}')"

                : $"SaveManager.LastOperationError='{error}' LastOperationSlot='{slot}'";

        }



        /// <summary>

        /// Lists the persistent root at file granularity: name, byte length, UTC write ticks, and an

        /// FNV-1a 64 content hash for the save-slot files themselves.

        ///

        /// Deliberately does not use <c>File.ReadAllBytes</c> - it is on the project's banned API list

        /// and would materialise whole save files. Content is streamed through one reusable 64 KB

        /// buffer, and anything above <see cref="MaxHashedFileBytes"/> is reported as unhashed rather

        /// than silently hashed to zero, so "hash matched" can never mean "hash was never taken".

        /// </summary>

        private static SaveFileFacts[] SnapshotSaveDirectory(string root, bool hashSaveFiles, out string error)

        {

            error = null;



            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))

            {

                error = $"persistent root '{root}' does not exist";

                return Array.Empty<SaveFileFacts>();

            }



            string[] paths;

            try

            {

                paths = Directory.GetFiles(root, "*", SearchOption.TopDirectoryOnly);

            }

            catch (IOException ex)

            {

                error = $"{ex.GetType().Name}: {ex.Message}";

                return Array.Empty<SaveFileFacts>();

            }

            catch (UnauthorizedAccessException ex)

            {

                error = $"{ex.GetType().Name}: {ex.Message}";

                return Array.Empty<SaveFileFacts>();

            }



            Array.Sort(paths, StringComparer.Ordinal);



            var facts = new SaveFileFacts[paths.Length];

            for (int i = 0; i < paths.Length; i++)

            {

                string name = Path.GetFileName(paths[i]);

                long length = -1L;

                long writeTicks = 0L;



                try

                {

                    var info = new FileInfo(paths[i]);

                    length = info.Length;

                    writeTicks = info.LastWriteTimeUtc.Ticks;

                }

                catch (IOException)

                {

                    // A file being rewritten right now still counts as present; length stays -1 and the

                    // diff reads that as a change, which is the correct conclusion.

                }

                catch (UnauthorizedAccessException)

                {

                }



                bool hashed = false;

                ulong hash = 0UL;

                if (hashSaveFiles && length >= 0L && IsSaveSlotFile(name))

                    hash = ComputeFileHashFnv1a64(paths[i], length, out hashed);



                facts[i] = new SaveFileFacts

                {

                    Name = name,

                    Length = length,

                    WriteTicksUtc = writeTicks,

                    ContentHash = hash,

                    Hashed = hashed,

                };

            }



            return facts;

        }



        private static bool IsSaveSlotFile(string fileName)

        {

            // SaveManager.cs:6830-6841 names slot files "<slot>.sav", "<slot>.sav.bak[N]" and

            // "<slot>.sav.tmp"; ".diag" at :6842 is a sidecar of the same transaction.

            return fileName.EndsWith(".sav", StringComparison.OrdinalIgnoreCase) ||

                   fileName.EndsWith(".diag", StringComparison.OrdinalIgnoreCase) ||

                   fileName.IndexOf(".sav.", StringComparison.OrdinalIgnoreCase) >= 0;

        }



        private static ulong ComputeFileHashFnv1a64(string path, long length, out bool hashed)

        {

            hashed = false;

            if (length > MaxHashedFileBytes)

                return 0UL;



            if (_hashBuffer == null)

            {

                // COLD ALLOC: byte[65536] - reused save-file hash window - owner: H8_HeadlessPlayModeProbe

                _hashBuffer = new byte[HashChunkBytes];

            }



            const ulong offsetBasis = 14695981039346656037UL;

            const ulong prime = 1099511628211UL;

            ulong hash = offsetBasis;



            try

            {

                using (var stream = new FileStream(

                           path,

                           FileMode.Open,

                           FileAccess.Read,

                           FileShare.ReadWrite,

                           HashChunkBytes,

                           FileOptions.SequentialScan))

                {

                    int read;

                    while ((read = stream.Read(_hashBuffer, 0, HashChunkBytes)) > 0)

                    {

                        for (int i = 0; i < read; i++)

                        {

                            hash ^= _hashBuffer[i];

                            hash *= prime;

                        }

                    }

                }

            }

            catch (IOException)

            {

                return 0UL;

            }

            catch (UnauthorizedAccessException)

            {

                return 0UL;

            }



            hashed = true;

            return hash;

        }



        /// <summary>

        /// Ordered merge of two name-sorted listings. Added, removed, and changed are counted

        /// separately because they mean different things: a fresh slot adds, a rewrite changes, and a

        /// temp file that appears and disappears within one poll window is invisible to both - which

        /// is why the byte delta and the write ticks are carried as well as the name.

        /// </summary>

        private static SaveDirectoryDiff DiffSaveDirectory(SaveFileFacts[] before, SaveFileFacts[] after)

        {

            var diff = new SaveDirectoryDiff();

            var lines = new StringBuilder(256);

            int emitted = 0;

            int i = 0;

            int j = 0;



            while (i < before.Length || j < after.Length)

            {

                int order;

                if (i >= before.Length)

                    order = 1;

                else if (j >= after.Length)

                    order = -1;

                else

                    order = string.CompareOrdinal(before[i].Name, after[j].Name);



                if (order < 0)

                {

                    diff.Removed++;

                    diff.ByteDelta -= Math.Max(0L, before[i].Length);

                    AppendDiffLine(lines, ref emitted, "-", before[i].Name, before[i].Length, -1L);

                    i++;

                }

                else if (order > 0)

                {

                    diff.Added++;

                    diff.ByteDelta += Math.Max(0L, after[j].Length);

                    AppendDiffLine(lines, ref emitted, "+", after[j].Name, -1L, after[j].Length);

                    j++;

                }

                else

                {

                    bool contentDiffers = before[i].Hashed && after[j].Hashed &&

                                          before[i].ContentHash != after[j].ContentHash;

                    if (before[i].Length != after[j].Length ||

                        before[i].WriteTicksUtc != after[j].WriteTicksUtc ||

                        contentDiffers)

                    {

                        diff.Changed++;

                        diff.ByteDelta += after[j].Length - before[i].Length;

                        AppendDiffLine(lines, ref emitted, "~", after[j].Name, before[i].Length, after[j].Length);

                    }



                    i++;

                    j++;

                }

            }



            diff.Lines = lines.ToString();

            return diff;

        }



        private static void AppendDiffLine(

            StringBuilder builder,

            ref int emitted,

            string sign,

            string name,

            long beforeLength,

            long afterLength)

        {

            if (emitted == MaxDiffLines)

            {

                builder.Append("\n  ... further entries truncated at ").Append(MaxDiffLines);

                emitted++;

                return;

            }



            if (emitted > MaxDiffLines)

                return;



            builder.Append("\n  ").Append(sign).Append(' ').Append(name)

                .Append(" bytesBefore=").Append(beforeLength)

                .Append(" bytesAfter=").Append(afterLength);

            emitted++;

        }



        private static void LogSaveDirectoryListing(string label, SaveFileFacts[] facts)

        {

            var builder = new StringBuilder(256);

            int emitted = 0;

            for (int i = 0; i < facts.Length; i++)

            {

                if (!IsSaveSlotFile(facts[i].Name))

                    continue;



                if (emitted >= MaxDiffLines)

                {

                    builder.Append("\n  ... further save files truncated at ").Append(MaxDiffLines);

                    break;

                }



                builder.Append("\n  ").Append(facts[i].Name)

                    .Append(" bytes=").Append(facts[i].Length)

                    .Append(" writeUtcTicks=").Append(facts[i].WriteTicksUtc)

                    .Append(" fnv1a64=")

                    .Append(facts[i].Hashed

                        ? facts[i].ContentHash.ToString("X16", CultureInfo.InvariantCulture)

                        : "unread");

                emitted++;

            }



            Debug.Log($"{Marker} SAVE {label} files={facts.Length} saveSlotFiles={emitted}{builder}");

        }



        private static void RunChecks()

        {

            // L19 hop2 LIVE: batch peel RunChecks heavy path - native Crash!!! after
            // WorldDriver phase=Done (stack RunChecks <- Tick Reporting).
            Debug.Log($"{Marker} --- checks at frame {_frames} ---");



            try

            {

                ReportClockRates();

                ReportSceneRuntimeService();

                ReportBootstrapReadiness();

                CheckWorldSeed();

                ReportRegistryPresence();

                ReportRegistryRejections();

                ReportRuntimeComponentCensus();

                ReportVegetationVertexInputs();

            }

            catch (Exception ex)

            {

                Debug.Log($"{Marker} CHECK THREW {ex.GetType().Name}: {ex.Message}");

                _failures++;

            }



            // Its own try, deliberately, and it sits AFTER the block above for two reasons: this is the

            // only end-of-run number two runs can be compared on, so it must still be read when an earlier

            // check threw, and a fault inside it must not suppress the route-moment rows that follow.

            //

            // It does not touch _failures. A diagnostics read that could not complete is an instrument

            // fault, not a product verdict, and making it an exit-code failure would silently change what

            // every existing caller of this probe means by exit code 1.

            try

            {

                // L19 hop2 LIVE: batch peel CaptureDeterminismState - vault/signal native AV after driver Done.
                if (!Application.isBatchMode)
                    CaptureDeterminismState();

                // L19 hop2 LIVE: batch peel ReportDeterminismState.
                if (!Application.isBatchMode)
                    ReportDeterminismState();

            }

            catch (Exception ex)

            {

                _determinismState = DeterminismCapture.NotRead;

                Debug.Log(

                    $"{Marker} DETERMINISM READ FAILED {ex.GetType().Name}: {ex.Message} - this run has no " +

                    "comparable end-of-run state number. Instrument fault, not a product verdict, so the " +

                    "exit code is unchanged.");

            }



            RecordProofMoment();

            RecordWorldDriverMoments();

            RecordMissionMoment();

            RecordContentBlockedMoments();

            ReportRouteMoments();



            // A FAILED Required Route row must reach the exit code when the caller asked for gameplay.

            //

            // Until now it did not, and the gap was load-bearing in the wrong direction: a run printed

            // "MOMENT FAIL Boot" and "RESULT failures=0" in the same breath, so anything reading the exit

            // code - a batch script, CI, an agent - saw a pass on a boot that never activated the scene. The

            // MOMENTS line said one thing and the exit status said the opposite, and the exit status is what

            // automation believes.

            //

            // Gated on _startNewGame rather than applied unconditionally, because a menu-only headless run

            // legitimately never reaches gameplay: the harness stops at 01_MAIN_MENU by design, which is a

            // Boot-row failure against the Required Route but not a defect in the product. Only a run that

            // was ASKED to start the game (-h8StartGame, or a nonzero gameplay-frame target) is entitled to

            // fail on it.

            //

            // Counted once per failed row rather than as a single flag so the number carries information, and

            // only Fail is counted: Blocked, Partial and NotExercised are already reported honestly in the

            // MOMENTS summary and each means "not proven", not "proven broken". Turning those into exit-code

            // failures would make the gate unreadable while four rows are content-blocked.

            if (_startNewGame)

            {

                int failedRows = 0;

                for (int i = 0; i < _routeMoments.Count; i++)

                {

                    if (_routeMoments[i].Verdict == MomentVerdict.Fail)

                        failedRows++;

                }



                if (failedRows > 0)

                {

                    _failures += failedRows;

                    Debug.LogError(

                        $"{Marker} RESULT {failedRows} Required Route row(s) reported FAIL on a run that was " +

                        "asked to start the game, so the exit code now reflects them. Read the MOMENT lines " +

                        "above for which rows and why.");

                }

            }



            Debug.Log($"{Marker} RESULT failures={_failures}");

        }



        /// <summary>

        /// Transcribes <see cref="H8_HeadlessWorldDriver"/>'s four verdicts into the route table.

        ///

        /// Transcribes: it does not decide. The driver latches a verdict only from an observable it

        /// actually read, and this method adds no interpretation on top - so a row can only be green here

        /// if the shipping code path produced the observable. When the driver never ran, the rows keep

        /// the NOT_EXERCISED they were seeded with and say the driver was off, which is a different and

        /// honest claim.

        /// </summary>

        private static void RecordWorldDriverMoments()

        {

            if (!_worldDriverStarted)

            {

                string reason = _worldDriverEnabled

                    ? "gameplay never started, so the world driver never ran"

                    : "world driver disabled by -h8SkipWorldDriver";



                RecordMoment(MomentSwim, MomentVerdict.NotExercised, reason);

                RecordMoment(MomentResource, MomentVerdict.NotExercised, reason);

                RecordMoment(MomentTool, MomentVerdict.NotExercised, reason);

                RecordMoment(MomentCraft, MomentVerdict.NotExercised, reason);

                return;

            }



            // Anything still unlatched when reporting starts is closed out as NOT_EXERCISED with the

            // phase it stalled in, so a budget shortfall is never silently reported as a product gap.

            H8_HeadlessWorldDriver.Stop(H8_HeadlessWorldDriver.StopCause.ProbeReportingStarted);



            Debug.Log(

                $"{Marker} WORLDDRIVER ticks={H8_HeadlessWorldDriver.TickCount} " +

                $"phase={H8_HeadlessWorldDriver.CurrentPhaseName} " +

                $"elapsed={H8_HeadlessWorldDriver.ElapsedSeconds:F1}s of " +

                $"{H8_HeadlessWorldDriver.TotalBudgetSeconds:F0}s " +

                $"compressed={H8_HeadlessWorldDriver.IsCompressed} " +

                $"stopCause={H8_HeadlessWorldDriver.StopCauseName} " +

                $"graceTicks={_worldDriverGraceTicks} " +

                $"discreteSignals={H8_HeadlessWorldDriver.PublishedDiscreteSignalCount} " +

                $"discreteDropped={H8_HeadlessWorldDriver.DroppedDiscreteSignalCount} " +

                $"inputOverrides={H8_HeadlessWorldDriver.PublishedOverrideCount}");



            ReportWorldDriverPhaseLedger();



            RecordDriverRow(H8_HeadlessWorldDriver.RowSwim, MomentSwim);

            RecordDriverRow(H8_HeadlessWorldDriver.RowResource, MomentResource);

            RecordDriverRow(H8_HeadlessWorldDriver.RowTool, MomentTool);

            RecordDriverRow(H8_HeadlessWorldDriver.RowCraft, MomentCraft);

        }



        /// <summary>

        /// One row per driver phase: what it was granted, what it spent in wall seconds AND in ticks, and

        /// why it stopped being the current phase.

        ///

        /// This is the table whose absence made the CraftRepairBuild row unactionable. The run reported

        /// "driver ran out of budget in phase Craft after 160.430s" and the number that explained it -

        /// ResourceDeplete having taken 138.192 of those seconds - was buried inside a different row's

        /// detail string, on a different Required Route line, with no indication the two were connected.

        /// The probe already learned this lesson once for its own phases in ReportClockRates: a single

        /// aggregate is not a rate of anything and per-phase segments are what make a budget readable.

        ///

        /// Ticks are printed beside seconds because they disagree by two orders of magnitude on this

        /// harness, and the tick column is the one that says whether a phase was allowed to do its work.

        /// </summary>

        private static void ReportWorldDriverPhaseLedger()

        {

            for (int phase = 0; phase < H8_HeadlessWorldDriver.PhaseLedgerCount; phase++)

            {

                // Phases that were never entered are skipped rather than printed as zero rows: a schedule

                // legitimately skips phases (a blocked Settle jumps straight to ResourceTarget), and a

                // wall of empty rows would bury the two or three that carry the answer.

                if (!H8_HeadlessWorldDriver.WasPhaseEntered(phase))

                    continue;



                string yield = H8_HeadlessWorldDriver.GetPhaseYieldName(phase);

                double wall = H8_HeadlessWorldDriver.GetPhaseWallSeconds(phase);

                double granted = H8_HeadlessWorldDriver.GetPhaseGrantedSeconds(phase);

                int ticks = H8_HeadlessWorldDriver.GetPhaseTicks(phase);

                int floor = H8_HeadlessWorldDriver.GetPhaseMinimumTicks(phase);

                double secondsPerTick = ticks > 0 ? wall / ticks : 0.0;



                Debug.Log(

                    $"{Marker} DRIVERPHASE {H8_HeadlessWorldDriver.GetPhaseName(phase),-16} " +

                    $"wall={wall,8:F3}s granted={granted,6:F3}s ticks={ticks,5} floor={floor,2} " +

                    $"secondsPerTick={secondsPerTick,8:F3} yield={yield}");

            }



            Debug.Log(

                $"{Marker} DRIVERPHASE heaviest={H8_HeadlessWorldDriver.WorstPhaseName} at " +

                $"{H8_HeadlessWorldDriver.WorstPhaseWallSeconds:F3}s of the " +

                $"{H8_HeadlessWorldDriver.ElapsedSeconds:F3}s the schedule ran against a " +

                $"{H8_HeadlessWorldDriver.TotalBudgetSeconds:F0}s budget. A yield of TotalCeiling means " +

                "that phase was compressed to its tick floor by an earlier overrun and its row is " +

                "UNMEASURED, not a product gap; WallCeiling means the phase spent its own window and " +

                "failed, which is a real result.");

        }



        /// <summary>

        /// The driver's RowVerdict is declared value-identical to <see cref="MomentVerdict"/> precisely so

        /// this stays a cast instead of a lookup table that could drift into mapping Fail onto Pass.

        /// </summary>

        private static void RecordDriverRow(int driverRow, string momentName)

        {

            string detail = H8_HeadlessWorldDriver.GetDetail(driverRow);



            // An upstream gate that never opened is not four independent product failures.

            //

            // When scene activation does not finish, GameBootstrapper.cs:7365 ActivatePlayer() never runs

            // and the player stays held by the Kinematic Arrest Gate - IsSuspended, velocity zero, input

            // locked - which is correct designed behaviour, not a bug. The driver still runs and still

            // publishes: in Logs/omega_route20.log it published 47,344 input overrides and the Swim row

            // reported FAIL with movementIntent01max=0.000. Read literally that says the input route is

            // broken. It is not; the input route delivered every one of those overrides to a player that

            // was not permitted to move.

            //

            // So the verdict becomes Blocked and names the upstream cause, while the driver's measured

            // numbers are kept verbatim - a row that hides its own measurements to look tidier would be

            // worse than one that mislabels them. Blocked is the right word by this file's own definition

            // at RecordContentBlockedMoments: the route WAS attempted and WAS obstructed at runtime, which

            // is exactly what happened, unlike the content-absent rows that were never attempted at all.

            //

            // Only downgrades. A row that already passed with gameReady false would be a contradiction

            // worth seeing, so a Pass is never rewritten into a Blocked.

            MomentVerdict verdict = (MomentVerdict)(byte)H8_HeadlessWorldDriver.GetVerdict(driverRow);

            if (!BootstrapState.IsGameReady && verdict != MomentVerdict.Pass)

            {

                RecordMoment(

                    momentName,

                    MomentVerdict.Blocked,

                    "UPSTREAM-BLOCKED: scene activation never completed, so ActivatePlayer() never ran and " +

                    "the player stayed held by the Kinematic Arrest Gate. Input reached it and moved " +

                    "nothing because movement was not permitted yet, so this row measures the boot, not " +

                    $"the mechanic. Driver detail kept for reference: {detail}");

                return;

            }



            RecordMoment(momentName, verdict, detail);

        }



        /// <summary>

        /// Two rows have no producer and will not get one from a driver, because the content they need

        /// does not exist anywhere in the project. Naming the missing content turns two silent

        /// NOT_EXERCISED lines into an actionable content gap, and keeps the next reader from building a

        /// driver for a row that cannot be driven.

        ///

        /// Verdict stays NOT_EXERCISED on purpose. Calling it Blocked would imply the route was attempted

        /// and obstructed at runtime; it was never attempted, because there is nothing to attempt.

        /// </summary>

        /// <summary>

        /// Hash of <c>quest_biome_spine</c>, the one quest that completes without the player doing anything.

        ///

        /// <c>Quest_BiomeSpine.asset</c> authors its trigger and its completion as the byte-identical

        /// condition (<c>triggerType:2/triggerValue:1</c> and <c>completionType:2/completionValue:1</c>), so

        /// a single BiomeEntered signal satisfies both nodes inside one EvaluateSignal call. It completed in

        /// Logs/omega_route20.log - "H8QUESTSPINE COMPLETE quest=0x244B9A5E" - on a run where the player was

        /// never activated and never moved a millimetre. It would complete in an empty world with no player

        /// at all.

        ///

        /// That green line is the most dangerous output in the log, because it reads as proof the mission

        /// axis works while the five quests that form the real first-hour chain sit behind an unproduced

        /// discovery hash. A Mission row that counted it would be a self-certifying instrument.

        /// </summary>

        private const uint SelfCompletingBiomeSpineQuestHash = 0x244B9A5Eu;



        /// <summary>

        /// Records the Mission row from QuestManager's own telemetry, and refuses to accept the

        /// self-completing quest as evidence.

        /// </summary>

        private static void RecordMissionMoment()

        {

            if (!Hecton8.Quest.QuestManager.QuestSpineBootObserved)

            {

                RecordMoment(

                    MomentMission,

                    MomentVerdict.NotExercised,

                    "QuestManager never reached its BOOT log, so no quest telemetry exists for this run. " +

                    "Either the quest owner is absent from the scene or the boot stopped before it awoke.");

                return;

            }



            int authored = Hecton8.Quest.QuestManager.QuestSpineAuthoredQuestCount;

            int autoActivated = Hecton8.Quest.QuestManager.QuestSpineAutoActivationCount;

            int activations = Hecton8.Quest.QuestManager.QuestSpineActivationCount;

            int completions = Hecton8.Quest.QuestManager.QuestSpineCompletionCount;

            int reverts = Hecton8.Quest.QuestManager.QuestSpineRevertCount;

            bool graphReady = Hecton8.Quest.QuestManager.QuestSpineStateGraphReady;



            // COLD ALLOC: QuestSpineTransitionRecord[16] - one-shot verdict read in an editor probe, sized

            // to QuestSpineTransitionRingCapacity at QuestManager.cs:62 - owner: H8_HeadlessPlayModeProbe

            //

            // 16, not 32. QuestSpineTransitionLogCap on the next line is 32, but that bounds how many

            // H8QUESTSPINE LINES get printed, not how many records the ring holds, and

            // CopyQuestSpineTransitions clamps its return to the ring capacity. Sizing this to the log cap

            // would have looked correct and silently over-allocated while implying the ring is deeper than

            // it is.

            var transitions = new Hecton8.Quest.QuestSpineTransitionRecord[16];

            int transitionCount = Hecton8.Quest.QuestManager.CopyQuestSpineTransitions(transitions);



            int genuineCompletions = 0;

            int selfCompletions = 0;

            for (int i = 0; i < transitionCount && i < transitions.Length; i++)

            {

                if (transitions[i].Completed == 0)

                    continue;



                if (transitions[i].QuestHash == SelfCompletingBiomeSpineQuestHash)

                    selfCompletions++;

                else

                    genuineCompletions++;

            }



            string detail =

                $"authored={authored} graphReady={graphReady} autoActivated={autoActivated} " +

                $"activations={activations} completions={completions} reverts={reverts} " +

                $"transitionsLogged={transitionCount} genuineCompletions={genuineCompletions} " +

                $"selfCompletions={selfCompletions}";



            if (genuineCompletions > 0)

            {

                RecordMoment(MomentMission, MomentVerdict.Pass, detail);

                return;

            }



            if (selfCompletions > 0)

            {

                RecordMoment(

                    MomentMission,

                    MomentVerdict.Fail,

                    "the only completed quest was quest_biome_spine, whose authored trigger and completion " +

                    "are the same condition, so it closes itself on one signal with no player operation - " +

                    $"it would complete in an empty world. No mission completed because of the player. {detail}");

                return;

            }



            if (authored <= 0)

            {

                RecordMoment(

                    MomentMission,

                    MomentVerdict.NotExercised,

                    $"no quest assets are assigned to QuestManager.allQuests. {detail}");

                return;

            }



            RecordMoment(

                MomentMission,

                MomentVerdict.Blocked,

                $"{authored} quests are authored and the graph is ready, but nothing completed. {detail}");

        }



        private static void RecordContentBlockedMoments()

        {

            RecordMoment(

                MomentFirstExit,

                MomentVerdict.NotExercised,

                "CONTENT-BLOCKED: no life-pod or drop-pod prefab exists in the project. " +

                "LifePodSeatStrapLatch, DropPodSeatController and LifePodTactilePrologueController are " +

                "referenced by zero scenes and zero prefabs, so there is no exit to drive. A driver " +

                "cannot open this row - the pod has to be authored first.");



            RecordMoment(

                MomentHazard,

                MomentVerdict.NotExercised,

                "CONTENT-BLOCKED: no hazard is ever instantiated. RadiationHazardGrid, " +

                "EnvironmentalHazard, ThermalVentRuntime, HectonHazardSource and HostileFlora have zero " +

                "AddComponent call sites anywhere, so no hazard exists to create a decision. A driver " +

                "cannot open this row - a hazard has to be placed first.");

        }



        /// <summary>

        /// Prints all ten rows of the First 20 Minutes Required Route table with the verdict this run

        /// actually produced. Rows with no producer print <c>NOT_EXERCISED</c> by construction, so the

        /// gap between "the harness ran" and "the route is proven" is visible in the harness's own

        /// output instead of having to be reconstructed by grepping a megabyte of log.

        /// </summary>

        private static void ReportRouteMoments()

        {

            int pass = 0;

            int partial = 0;

            int fail = 0;

            int blocked = 0;

            int notExercised = 0;



            for (int i = 0; i < _routeMoments.Count; i++)

            {

                RouteMoment moment = _routeMoments[i];

                switch (moment.Verdict)

                {

                    case MomentVerdict.Pass:

                        pass++;

                        break;

                    case MomentVerdict.Partial:

                        partial++;

                        break;

                    case MomentVerdict.Fail:

                        fail++;

                        break;

                    case MomentVerdict.Blocked:

                        blocked++;

                        break;

                    default:

                        notExercised++;

                        break;

                }



                Debug.Log(

                    $"{Marker} MOMENT   {DescribeVerdict(moment.Verdict),-13} {moment.Name,-18} {moment.Detail}");

            }



            Debug.Log(

                $"{Marker} MOMENTS pass={pass} partial={partial} fail={fail} blocked={blocked} " +

                $"notExercised={notExercised} of {_routeMoments.Count} Required Route rows. " +

                "Only pass is acceptance; partial means one half of a two-part row was observed and " +

                "the row is NOT accepted.");

        }



        private static string DescribeVerdict(MomentVerdict verdict)

        {

            switch (verdict)

            {

                case MomentVerdict.Pass:

                    return "PASS";

                case MomentVerdict.Partial:

                    return "PARTIAL";

                case MomentVerdict.Fail:

                    return "FAIL";

                case MomentVerdict.Blocked:

                    return "BLOCKED";

                default:

                    return "NOT_EXERCISED";

            }

        }



        private static void SeedRouteMoments()

        {

            _routeMoments.Clear();

            AddRouteMoment(MomentBoot, "00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD");

            AddRouteMoment(MomentWorldLoad, "selected semi-open shallow route loads");

            AddRouteMoment(MomentFirstExit, "player exits into readable photic water");

            AddRouteMoment(MomentSwim, "navigate, surface/dive, read oxygen/pressure/depth");

            AddRouteMoment(MomentResource, "world object reaches inventory");

            AddRouteMoment(MomentTool, "one tool interaction is useful on the route");

            AddRouteMoment(MomentCraft, "one recipe/repair consumes the resource");

            AddRouteMoment(MomentMission, "one mission completes BECAUSE the player did something");

            AddRouteMoment(MomentHazard, "one fair hazard creates a decision");

            AddRouteMoment(MomentSaveLoad, "save, quit/reload, return to the same state");

            AddRouteMoment(MomentProof, "console, run, profiler, GC, memory, capture, save directory diff");

        }



        private static void AddRouteMoment(string name, string minimumAcceptance)

        {

            _routeMoments.Add(new RouteMoment

            {

                Name = name,

                Verdict = MomentVerdict.NotExercised,

                Detail = "no producer in this probe - minimum acceptance: " + minimumAcceptance,

            });

        }



        /// <summary>

        /// Records the verdict for one Required Route row. A worse verdict never overwrites a better

        /// one silently: the first non-<see cref="MomentVerdict.NotExercised"/> verdict wins unless the

        /// new one is more severe, so a late blocked/failed observation cannot be masked by an early

        /// optimistic one.

        /// </summary>

        private static void RecordMoment(string name, MomentVerdict verdict, string detail)

        {

            for (int i = 0; i < _routeMoments.Count; i++)

            {

                RouteMoment moment = _routeMoments[i];

                if (!string.Equals(moment.Name, name, StringComparison.Ordinal))

                    continue;



                if (verdict < moment.Verdict)

                    return;



                moment.Verdict = verdict;

                moment.Detail = detail ?? string.Empty;

                _routeMoments[i] = moment;

                return;

            }

        }



        /// <summary>

        /// A headless boot reaches 01_MAIN_MENU and stops there, because gameplay systems are

        /// installed only once a game is actually started. On arrival BootstrapRouteEnforcer finds

        /// AreAllSystemsReady() false and tries to reload 00_BOOTSTRAP, which then fails with

        /// "Failed to schedule async bootstrap recovery load".

        ///

        /// AreAllSystemsReady is `_isBootstrapComplete && Dispatcher && TickManager && Save &&

        /// ObjectPool`, so this breaks the verdict into its parts: the aggregate says "not ready"

        /// without saying which term failed, and the difference between "boot never completed" and

        /// "one service is missing" decides where to look next.

        ///

        /// Reports only; a headless boot legitimately does not finish, so this fails nothing.

        /// </summary>

        private static void ReportBootstrapReadiness()

        {

            bool ready = global::Hecton8.Bootstrap.GameBootstrapper.AreAllSystemsReady();



            Debug.Log(

                $"{Marker} BOOTSTRAP allSystemsReady={ready} " +

                $"Dispatcher={(IsAlive(GlobalRegistry.Dispatcher) ? "ok" : "null")} " +

                $"TickManager={(IsAlive(GlobalRegistry.TickManager) ? "ok" : "null")} " +

                $"Save={(IsAlive(GlobalRegistry.Save) ? "ok" : "null")} " +

                $"ObjectPool={(IsAlive(GlobalRegistry.ObjectPool) ? "ok" : "null")}");



            // Every loaded scene, not just the active one: "sceneCount=2" with no menu in sight was

            // the single most misleading line the first version of this probe printed.

            UnityEngine.SceneManagement.Scene active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            var scenes = new StringBuilder();

            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)

            {

                UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);

                if (scenes.Length > 0)

                    scenes.Append(", ");



                scenes.Append('[').Append(i).Append("] '").Append(scene.name).Append("' loaded=")

                    .Append(scene.isLoaded)

                    .Append(" roots=")

                    .Append(scene.isLoaded ? scene.rootCount : 0);

            }



            Debug.Log($"{Marker} SCENE active='{active.name}' count={UnityEngine.SceneManagement.SceneManager.sceneCount} -> {scenes}");



            // Verdict only; _failures is deliberately untouched here so the exit code keeps its

            // existing meaning. A headless boot that stops at 01_MAIN_MENU is a legitimate outcome

            // of the harness, and it is still a Boot-row failure against the Required Route.

            // allSystemsReady ALONE scored Boot=PASS on runs that logged

            // "[GameBootstrapper] Bootstrap timed out during scene activation." That predicate is the BIOS

            // phase from 00_BOOTSTRAP - _isBootstrapComplete plus Dispatcher, TickManager, Save and

            // ObjectPool - and it asserts nothing about scene activation: not that the SceneInstantiationGate

            // opened, not that a player exists, not that ActivatePlayer ran. The activeScene name below was

            // reported, never checked. So three consecutive runs printed RESULT failures=0 while the player

            // had been destroyed mid-transition and never respawned, and every conclusion drawn from those

            // runs was read off a probe that passed a failed boot.

            //

            // BootstrapState.IsGameReady is the missing bit and nothing else in the project is: it is

            // published true only after ActivatePlayer() completes, and forced false on each activation

            // failure path. Requiring both means the row cannot go green on a boot that did not finish.

            bool gameReady = BootstrapState.IsGameReady;

            RecordMoment(

                MomentBoot,

                ready && gameReady ? MomentVerdict.Pass : MomentVerdict.Fail,

                $"allSystemsReady={ready} gameReady={gameReady} Dispatcher={IsAlive(GlobalRegistry.Dispatcher)} " +

                $"TickManager={IsAlive(GlobalRegistry.TickManager)} Save={IsAlive(GlobalRegistry.Save)} " +

                $"ObjectPool={IsAlive(GlobalRegistry.ObjectPool)} activeScene='{active.name}' " +

                $"{DescribeSceneActivationProgress()}");

        }



        /// <summary>

        /// Names the activation step the boot actually stopped on.

        ///

        /// gameReady=False says the activation did not finish; it does not say WHERE it stopped, and that

        /// distinction was the difference between "the world is a shell" and the truth. Reconstructing it

        /// took a manual sweep of a 1.6 MB log for SetSceneActivationStep strings, which is not a thing

        /// anyone should have to do twice.

        ///

        /// What that sweep found, in Logs/omega_route20.log: the phase reached "Step 7: Player Spawn" and

        /// never printed Step 8. HectonPlayerSpawner waits for terrain readiness with maxWaitTime=60f

        /// (HectonPlayerSpawner.cs:276) while the bootstrap's per-step NO-PROGRESS budget is

        /// bootstrapTimeout=30f (GameBootstrapper.cs:614, applied by cts.CancelAfter at :7266) - and the

        /// spawner's 0.5s "Terrain not ready; waiting" logs are not new named steps, so the deadline is

        /// never pushed forward. The token cancels first, GameBootstrapper.cs:7365 ActivatePlayer() never

        /// runs, and the player stays held by the Kinematic Arrest Gate, which is why 47,344 published

        /// input overrides moved it zero millimetres. The spawner's own ForceFallbackSpawn() recovery is

        /// unreachable for the same reason: 60 > 30.

        ///

        /// Both fields are private [SerializeField], which is precisely what SerializedObject reads - so

        /// this needs no reflection and no edit to GameBootstrapper.cs.

        /// </summary>

        private static string DescribeSceneActivationProgress()

        {

            // Cold, once-per-run, editor-only diagnostic lookup. Not a hot path.

            Hecton8.Bootstrap.GameBootstrapper bootstrapper =

                UnityEngine.Object.FindAnyObjectByType<Hecton8.Bootstrap.GameBootstrapper>();

            if (bootstrapper == null)

                return "activationStep=<no GameBootstrapper instance found>";



            SerializedObject serialized = new SerializedObject(bootstrapper);

            SerializedProperty step = serialized.FindProperty("_debugSceneActivationStep");

            SerializedProperty completed = serialized.FindProperty("_debugSceneActivationCompleted");



            string stepText = step != null ? step.stringValue : "<field renamed or removed>";

            string completedText = completed != null

                ? (completed.boolValue ? "True" : "False")

                : "<field renamed or removed>";



            return $"activationStep='{stepText}' activationCompleted={completedText}";

        }



        /// <summary>

        /// The world seed reached every procedural generator as 0 because nothing implemented

        /// IWorldSeedProvider. This is the check that says whether that is still true.

        /// </summary>

        private static void CheckWorldSeed()

        {

            IWorldSeedProvider provider = GlobalRegistry.WorldSeedProvider;

            if (provider == null)

            {

                Debug.Log($"{Marker} WORLDSEED provider=NULL - every procedural generator is running on seed 0");

                _failures++;

                return;

            }



            Debug.Log(

                $"{Marker} WORLDSEED provider={provider.GetType().Name} initialized={provider.IsInitialized} " +

                $"seed={provider.RuntimeWorldSeed} versionId={provider.RuntimeWorldGenerationVersionId}");



            if (!provider.IsInitialized)

            {

                Debug.Log($"{Marker} WORLDSEED provider present but NOT initialized - consumers still take the 0 path");

                _failures++;

                return;

            }



            // The seven MapMagic / procedural consumers go through this static, not the registry.

            bool resolved = global::HectonWorldGenerator.TryGetActiveRuntimeWorldSeed(out int staticSeed);

            Debug.Log($"{Marker} WORLDSEED static TryGetActiveRuntimeWorldSeed -> {resolved} seed={staticSeed}");



            if (!resolved || staticSeed == 0)

            {

                Debug.Log($"{Marker} WORLDSEED static path still returns nothing - the MapMagic nodes remain on 0");

                _failures++;

                return;

            }



            if (staticSeed != provider.RuntimeWorldSeed)

            {

                Debug.Log($"{Marker} WORLDSEED MISMATCH static={staticSeed} registry={provider.RuntimeWorldSeed}");

                _failures++;

            }

        }



        /// <summary>

        /// Reports presence only, and fails nothing. A missing service here is information about

        /// what this scene boots, not a defect on its own.

        /// </summary>

        private static void ReportRegistryPresence()

        {

            var slots = new List<KeyValuePair<string, object>>

            {

                new("Terrain", GlobalRegistry.Terrain),

                new("FaunaGenetics", GlobalRegistry.FaunaGenetics),

                new("Player", GlobalRegistry.Player),

                new("Save", GlobalRegistry.Save),

                new("TickDispatcher", GlobalRegistry.TickDispatcher),

                new("VoxelEngine", GlobalRegistry.VoxelEngine),

            };



            var builder = new StringBuilder();

            foreach (KeyValuePair<string, object> slot in slots)

            {

                if (builder.Length > 0)

                    builder.Append("  ");



                builder.Append(slot.Key).Append('=');

                builder.Append(IsAlive(slot.Value) ? slot.Value.GetType().Name : "null");

            }



            Debug.Log($"{Marker} REGISTRY {builder}");

        }



        /// <summary>

        /// Captures every "Ready-locked registry rejected registration" the run produces.

        ///

        /// This exists because the failure it catches is INTERMITTENT and its symptom looks trivial.

        /// Three identical runs: two came up with 02_HECTON_WORLD roots=37 and a live terrain

        /// provider, the third with roots=30, Terrain=null, FaunaGenetics=null. The summary made

        /// that look like a slow load. It was not. The third run's log carried 31 distinct

        /// CriticalBootException rejections - MapMagicBridge, HectonPlayerMotor, QuestManager,

        /// HectonCelestialEngine, WorldProceduralFieldSampler, DepthZoneDirector, EndingSystem,

        /// SoundscapeSystem and 23 more - and the good runs carried zero. The scene-owned service

        /// layer simply did not register, and the only way to know was grepping a 1 MB log for a

        /// string nobody had thought to look for.

        ///

        /// GlobalRegistry.GuardServicePublication throws whenever Phase == Ready and no

        /// ForceOverrideToken is present. GameBootstrapper opens a window for exactly this

        /// (BeginSceneRuntimePublicationGate / EndSceneRuntimePublicationGate, three call sites);

        /// inside it a hot-swap token is issued and these registrations pass. So the race is

        /// whether 02_HECTON_WORLD's scene-owned OnEnable calls land inside that window. Diagnosis

        /// only - the ordering fix belongs to whoever owns GlobalRegistry and the bootstrapper, and

        /// guessing at activation order is how this class of bug gets made.

        ///

        /// This used to be true and is no longer: only the FIRST rejection was logged as an error,

        /// because GlobalRegistry gated it on a single global _readyLockViolationLogged flag, so

        /// counting LogError hits undercounted 31 down to 1. That flag is now a per-type latch stamped

        /// with a reset generation, and each message carries a running "#N" count, so every distinct

        /// rejected service reports itself once and the scale is visible on the first line.

        ///

        /// This hook is still the belt to that braces: it catches repeats of an already-logged type,

        /// and it does not depend on GlobalRegistry's own DEVELOPMENT_BUILD-guarded logging being

        /// compiled in at all.

        /// </summary>

        private static void InstallRegistryRejectionHook()

        {

            if (_logHookInstalled)

                return;



            _logHookInstalled = true;

            Application.logMessageReceived += OnLogMessage;

        }



        private static void OnLogMessage(string condition, string stackTrace, LogType type)

        {

            if (string.IsNullOrEmpty(condition) || condition.IndexOf(ReadyLockRejection, StringComparison.Ordinal) < 0)

                return;



            // The service name is the tail of "...rejected registration: Name".

            int colon = condition.LastIndexOf(':');

            string service = colon >= 0 && colon < condition.Length - 1

                ? condition.Substring(colon + 1).Trim()

                : condition.Trim();



            if (!_rejectedServices.Contains(service))

                _rejectedServices.Add(service);

        }



        private static void ReportRegistryRejections()

        {

            if (_rejectedServices.Count == 0)

            {

                Debug.Log($"{Marker} REGISTRYLOCK none - every scene-owned service registered inside the publication gate");

                return;

            }



            Debug.Log(

                $"{Marker} REGISTRYLOCK {_rejectedServices.Count} services were REJECTED by the ready-locked " +

                "registry. This world is a shell: the scene-owned service layer did not publish. " +

                "Intermittent - a rerun may come up clean, which is what makes it dangerous.");



            var builder = new StringBuilder();

            for (int i = 0; i < _rejectedServices.Count; i++)

            {

                if (builder.Length > 0)

                    builder.Append(", ");



                builder.Append(_rejectedServices[i]);

            }



            Debug.Log($"{Marker} REGISTRYLOCK   {builder}");

            _failures++;

        }



        /// <summary>

        /// Counts live instances of the runtime owners whose existence is in question.

        ///

        /// H8_SceneCompositionCensus answers whether a component is AUTHORED into a scene. It

        /// found HectonIndirectVegetationRenderer, HectonFluidEngine, FloraInteractionManager,

        /// GpuScatterLodManager and HectonVoxelEngine absent from all three boot scenes - but it

        /// also found FaunaGeneticsManager absent, and this probe proves that one exists at

        /// runtime because EcosystemRuntimeInstaller AddComponents it. Authoring absence is

        /// therefore not runtime absence, and only this side of the pair can close the question.

        ///

        /// FindObjectsByType also fixes the blind spot in every other search here:

        /// SceneManager.GetSceneAt does not enumerate the DontDestroyOnLoad scene, so root

        /// traversal cannot see persistent objects. This can.

        ///

        /// Self-test: cross-check the census against GlobalRegistry IN THIS RUN. If the registry

        /// holds a live FaunaGeneticsManager and the census cannot see it, the census is broken

        /// and every "0 instances" below is worthless.

        ///

        /// The first version of this self-test asserted FaunaGeneticsManager unconditionally,

        /// because two consecutive runs had it. The third run did not: the world came up with

        /// roots=30 instead of 37, Terrain and FaunaGenetics both null. The assertion fired and

        /// suppressed a census that was working correctly. A known-answer case has to be read

        /// from the same run, not from the last one - which is also why the boot being

        /// non-deterministic matters more than any single reading here.

        /// </summary>

        private static void ReportRuntimeComponentCensus()

        {

            string[] watched =

            {

                "HectonIndirectVegetationRenderer",

                "HectonFluidEngine",

                "FloraInteractionManager",

                "GpuScatterLodManager",

                "HectonVoxelEngine",

                "HectonWorldGenerator",

                "GameBootstrapper",

                "FaunaGeneticsManager",



                // The three player components that no prefab carries and no shipped code created until

                // PlayerRuntimeContextService started installing them. Named here because the aggregate

                // "N live MonoBehaviours" line cannot answer whether they exist: subtracting one run's total

                // from another's is invalid across a boot-quality boundary, since a run where the registry

                // ready-lock rejected scene services aborts each owner's OnEnable partway and therefore

                // builds a different component population, not merely a smaller one.

                "HectonPlayerHealth",

                "TraumaDispatcher",

                "PlayerTransportCoordinator",



                // Two controls, and they are the point of the addition rather than padding.

                // PlayerKinematicsRuntime is installed by the SAME method on the same line group and predates

                // that change, so it proves the cold sync path ran at all - if it is NONE, the three above

                // being NONE says nothing about their install and everything about the path never executing.

                // HectonSurvivalSystem is authored on Player.prefab, so it proves the player root still exists

                // at census time. Read the enabled= column on all five: the bootstrap disables the player root

                // during the Kinematic Arrest Gate, so instances=1 enabled=0 is a materially different

                // outcome from instances=0 and the two must not be conflated.

                "PlayerKinematicsRuntime",

                "HectonSurvivalSystem",

            };



            MonoBehaviour[] all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(

                FindObjectsInactive.Include, FindObjectsSortMode.None);



            var total = new Dictionary<string, int>(StringComparer.Ordinal);

            var enabled = new Dictionary<string, int>(StringComparer.Ordinal);

            var where = new Dictionary<string, string>(StringComparer.Ordinal);

            for (int i = 0; i < all.Length; i++)

            {

                MonoBehaviour behaviour = all[i];

                if (behaviour == null)

                    continue;



                string typeName = behaviour.GetType().Name;

                total.TryGetValue(typeName, out int seen);

                total[typeName] = seen + 1;

                if (behaviour.isActiveAndEnabled)

                {

                    enabled.TryGetValue(typeName, out int live);

                    enabled[typeName] = live + 1;

                }



                if (!where.ContainsKey(typeName))

                    where[typeName] = behaviour.gameObject.scene.name ?? "<no scene>";

            }



            Debug.Log($"{Marker} RUNTIME CENSUS {all.Length} live MonoBehaviours, {total.Count} distinct types");



            object registryFauna = GlobalRegistry.FaunaGenetics;

            bool registryHasFauna = IsAlive(registryFauna);

            if (registryHasFauna && !total.ContainsKey("FaunaGeneticsManager"))

            {

                Debug.Log(

                    $"{Marker} RUNTIME CENSUS SELF-TEST FAILED - GlobalRegistry holds a live " +

                    "FaunaGeneticsManager in this run and the census cannot see it. Every zero below is void.");

                _failures++;

                return;

            }



            Debug.Log(

                registryHasFauna

                    ? $"{Marker} RUNTIME CENSUS   self-test ok - census agrees with GlobalRegistry on FaunaGeneticsManager"

                    : $"{Marker} RUNTIME CENSUS   self-test SKIPPED - GlobalRegistry has no FaunaGenetics this run, " +

                      "so there is no positive case to check the census against. Zeros below are UNVALIDATED.");



            foreach (string typeName in watched)

            {

                total.TryGetValue(typeName, out int count);

                enabled.TryGetValue(typeName, out int live);

                string scene = count > 0 && where.TryGetValue(typeName, out string found) ? found : "-";

                string verdict = count == 0 ? "NONE  " : "EXISTS";

                Debug.Log($"{Marker} RUNTIME CENSUS   {verdict} {typeName,-34} instances={count} enabled={live} firstScene={scene}");

            }

        }



        /// <summary>

        /// Reports whether the four vegetation passes are actually FED anything.

        ///

        /// H8_VegetationPassParityProbe proves the ForwardLit / Shadow / DepthOnly / MotionVectors

        /// passes compute the same vertex position from the same inputs. It says nothing about

        /// whether those inputs carry a value at runtime, and a term whose uniform is never

        /// published contributes exactly zero in all four passes - agreement on nothing. Every

        /// input below is a C# global (Shader.SetGlobal*), not a material property, so it is

        /// readable from here.

        ///

        /// HONEST LIMIT, do not read past it: Shader.GetGlobal* returns zero for a name that was

        /// never set AND for a name deliberately set to zero. ZERO therefore means "contributes

        /// nothing right now", never "nobody publishes it". Only PUBLISHED is a positive result.

        /// A single sample at one instant also cannot see a value that is only non-zero while the

        /// player is moving through flora.

        ///

        /// _HectonImpactSphereCount is the known-answer case that keeps this honest. It was found

        /// statically to have no producer anywhere in C# (GpuScatterLodManager only ever writes 0

        /// and the _HectonImpactSpheres buffer is never bound), so it MUST read zero. If it ever

        /// reads non-zero, that static finding was wrong and ResolveImpactOffset is live code whose

        /// lit-vs-DepthOnly divergence has been shipping.

        /// </summary>

        private static void ReportVegetationVertexInputs()

        {

            Debug.Log(

                $"{Marker} VEGINPUT (ZERO = contributes nothing at this instant, NOT proof of a missing publisher)");



            ReportGlobalVector("_AbyssalGridResolution", "abyssal flow grid - zero disables ResolveAbyssalFlowField in all 4 passes");

            ReportGlobalFloat("_AbyssalFlowTextureActive", "abyssal flow texture path");

            ReportGlobalVector("_AbyssalFlowSpacing", "abyssal flow cell size");

            ReportGlobalFloat("_HectonFloraInteractionCount", "ResolveInteractionOffset input count");

            ReportGlobalFloat("_HectonFloraWakeCount", "ResolveWakeTrailOffset input count");

            ReportGlobalVector("_HectonFloraWakeParams", "wake trail tuning");

            ReportGlobalVector("_HectonPlayerFloraInteractionParams", "ResolvePlayerBendOffset input");

            ReportGlobalVector("_HectonFloraSwayFieldParams", "sway field - couples to the fieldDrivenBend suppression");

            ReportGlobalFloat("_HectonShallowWaterFieldActive", "shallow water field feeding the wake trail");

            ReportGlobalVector("_HectonFlowSynchronyParams", "ResolveFlowSynchronyOffset tuning");

            ReportGlobalFloat("_HectonFloraSnapFlagsEnabled", "flora snap subsystem");



            float impactSpheres = Shader.GetGlobalFloat("_HectonImpactSphereCount");

            if (impactSpheres > 0.5f)

            {

                Debug.Log(

                    $"{Marker} VEGINPUT   CONTRADICTION _HectonImpactSphereCount={impactSpheres} - " +

                    "ResolveImpactOffset was recorded as dead code with no producer. It is not. " +

                    "Its lit-vs-DepthOnly divergence is live and must be converged.");

                _failures++;

            }

            else

            {

                Debug.Log(

                    $"{Marker} VEGINPUT   self-test ok _HectonImpactSphereCount=0 as the static audit predicted");

            }

        }



        private static void ReportGlobalVector(string name, string meaning)

        {

            Vector4 value = Shader.GetGlobalVector(name);

            string verdict = value == Vector4.zero ? "ZERO     " : "PUBLISHED";

            Debug.Log($"{Marker} VEGINPUT   {verdict} {name,-42} {value.ToString("F3", CultureInfo.InvariantCulture)}  {meaning}");

        }



        private static void ReportGlobalFloat(string name, string meaning)

        {

            float value = Shader.GetGlobalFloat(name);

            string verdict = value == 0f ? "ZERO     " : "PUBLISHED";

            Debug.Log($"{Marker} VEGINPUT   {verdict} {name,-42} {value.ToString("F3", CultureInfo.InvariantCulture)}  {meaning}");

        }



        /// <summary>

        /// Takes one observation of the determinism owner's existence at a named moment.

        ///

        /// COLD AND ONE-SHOT PER MOMENT. Both in-run call sites are latched by the sample's own

        /// <see cref="DeterminismOwnerSample.Taken"/> flag, so the <c>FindObjectsByType</c> here executes

        /// once per observation point and never becomes a cadence path - the same discipline

        /// <see cref="EnableDisabledPlacementOwnersInMemory"/> is latched under.

        ///

        /// The buffer probe is deliberately included in the sample: "owner alive, buffer already open" at

        /// the boot warmup versus "owner alive, buffer still unopened" separates a lifetime defect from a

        /// vault-timing defect without a second editor run.

        /// </summary>

        private static void SampleDeterminismOwner(ref DeterminismOwnerSample sample, string moment)

        {

            if (sample.Taken || !EditorApplication.isPlaying)

                return;



            // No FindObjectsSortMode overload - deprecated in 6000.5 (CS0618) and this only enumerates.

            // FindObjectsInactive.Include is required, not defensive: the owner's GameObject is created at

            // runtime with HideFlags.HideInHierarchy, so a scene-root walk cannot see it at all.

            Hecton8.Core.Determinism.LockstepStateValidator[] validators =

                UnityEngine.Object.FindObjectsByType<Hecton8.Core.Determinism.LockstepStateValidator>(

                    FindObjectsInactive.Include);



            int enabled = 0;

            for (int i = 0; i < validators.Length; i++)

            {

                Hecton8.Core.Determinism.LockstepStateValidator validator = validators[i];

                if (validator != null && validator.isActiveAndEnabled)

                    enabled++;

            }



            Hecton8.Core.Memory.IDataVault vault = GlobalRegistry.DataVault;

            bool hashBufferPresent = false;

            if (vault != null)

            {

                hashBufferPresent = TryReadDeterminismBuffer<ulong>(

                    vault,

                    Hecton8.Core.Memory.BufferID.LockstepMasterStateHash,

                    1,

                    out Unity.Collections.NativeArray<ulong>.ReadOnly probeView);



                // The view is intentionally not indexed. This sample answers "is the buffer allocated",

                // and reading the value belongs to the single end-of-run capture that owns the number.

                _ = probeView;

            }



            sample.Taken = true;

            sample.Instances = validators.Length;

            sample.Enabled = enabled;

            sample.VaultPresent = vault != null;

            sample.HashBufferPresent = hashBufferPresent;

            sample.ActiveScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;



            Debug.Log(

                $"{Marker} DETERMINISM OWNER TRACE {moment} activeScene='{sample.ActiveScene}' " +

                $"validatorInstances={sample.Instances} enabled={sample.Enabled} " +

                $"dataVault={(sample.VaultPresent ? "present" : "null")} " +

                $"masterHashBuffer={(sample.HashBufferPresent ? "allocated" : "unallocated")}");

        }



        /// <summary>

        /// OPT-IN, off by default: creates a <c>LockstepStateValidator</c> in memory when the running session

        /// has none, so a run can answer "does the hash pipeline produce anything once an owner exists".

        ///

        /// WHY IT IS OFF BY DEFAULT, unlike <see cref="EnableDisabledPlacementOwnersInMemory"/>. The scatter

        /// director is authored into the shipped scene and merely unchecked, so enabling it in memory

        /// restores authored intent. The determinism owner is different: its ABSENCE is the only evidence

        /// this harness has ever produced about it, and a probe that silently manufactured one would make

        /// every future run report a hash while the product still ships without an owner past the first

        /// scene load. That is the exact class of thing an instrument must never hide, so the default run

        /// keeps reporting the absence and this path has to be asked for by name.

        ///

        /// WHAT IT IS NOT. It is not the fix, and the number it produces is NOT a product hash: a validator

        /// created at the first gameplay tick starts its own <c>_postSimulationFrame</c> at zero, so its

        /// sampled frame and therefore its master hash cannot be compared against a session where the owner

        /// lived from boot. It answers a pipeline question, not a determinism question, and the log says so.

        ///

        /// Play-mode only and in-memory only, per AGENTS.md:126: no asset is written, no scene is marked

        /// dirty, no Undo entry is recorded. <c>DontDestroyOnLoad</c> is applied because the product's own

        /// creation path does not, which is the suspected defect - so a revived owner deliberately does NOT

        /// reproduce the product's lifetime.

        /// </summary>

        private static void TryReviveDeterminismOwner()

        {

            if (_determinismReviveAttempted)

                return;



            _determinismReviveAttempted = true;



            if (!EditorApplication.isPlaying)

            {

                _determinismReviveNote = "refused - not in play mode";

                return;

            }



            if (_determinismOwnerAtGameplayStart.Taken && _determinismOwnerAtGameplayStart.Instances > 0)

            {

                _determinismReviveNote =

                    "not needed - a validator instance was already live at the first gameplay tick";

                Debug.Log($"{Marker} DETERMINISM REVIVE {_determinismReviveNote}");

                return;

            }



            try

            {

                // COLD ALLOC: GameObject[1] - probe-owned determinism owner host, one per run, play-mode

                // only - owner: H8_HeadlessPlayModeProbe

                GameObject host = new GameObject("H8_PROBE_REVIVED Lockstep State Validator");

                UnityEngine.Object.DontDestroyOnLoad(host);

                host.AddComponent<Hecton8.Core.Determinism.LockstepStateValidator>();

                _determinismReviveCreated = true;

                _determinismReviveNote =

                    "created in memory at the first gameplay tick with DontDestroyOnLoad";



                Debug.Log(

                    $"{Marker} DETERMINISM REVIVE DIVERGENCE this run does NOT match a player session. The " +

                    "session had NO LockstepStateValidator and -h8ReviveDeterminismOwner created one, so " +

                    "any hash reported below exists only because the probe built its own owner. The owner's " +

                    "post-simulation frame counter starts at 0 here, so the hash is NOT comparable with a " +

                    "run whose owner lived from boot - it only proves whether the hash path can produce a " +

                    "non-zero number at all.");

            }

            catch (Exception ex)

            {

                _determinismReviveNote = ex.GetType().Name + ": " + ex.Message;

                Debug.Log(

                    $"{Marker} DETERMINISM REVIVE FAILED {_determinismReviveNote} - the run continues and " +

                    "reports the owner as absent, which is the honest reading.");

            }

        }



        /// <summary>

        /// Reads the determinism numbers this run produced and stores them for the console report and the

        /// artifact.

        ///

        /// Called ONCE, from <see cref="RunChecks"/>, while Play Mode is still live. That is a hard

        /// requirement, not a preference: the vault arena is a raw pointer owned by

        /// <c>GlobalDataVault</c>, so resolving a buffer view after the play session has torn down is a

        /// use-after-free. Nothing in this method may be called from <see cref="WriteRouteArtifact"/>,

        /// which runs on every terminal path including one taken after Play Mode has exited - the artifact

        /// writer emits the statics captured here and never re-reads the runtime.

        ///

        /// The hash value itself comes from <c>LockstepStateValidator.LastMasterStateHash</c> when the

        /// owner component exists, because calling the authority's own accessor cannot drift from what the

        /// authority publishes. The direct buffer read is the fallback, and it is a real case rather than

        /// defensive padding: <c>LockstepStateValidator.DisposeNativeState</c> documents that the vault

        /// owns these buffers and preserves the last hash across component lifetime churn, so a run whose

        /// validator was destroyed still has a readable number.

        /// </summary>

        private static void CaptureDeterminismState()

        {

            // Read before anything can fail: these two are the counters that say whether the run

            // simulated what its frame count implies, and they are worth having even if no hash exists.

            // SystemDispatcher returns 0.0/0 once its ActiveRuntimeInstance is gone, which is the other

            // reason this is read inside the play session.

            _determinismSlowTickDiscardedSeconds = SystemDispatcher.SlowTickDiscardedSeconds;

            _determinismSlowTickDiscardEvents = SystemDispatcher.SlowTickDiscardEvents;

            _determinismDispatcherFrameId = SystemDispatcher.CurrentFrameId;



            int categoryCount = (int)Hecton8.Core.Determinism.LockstepHashCategory.Count;

            if (_determinismCategories.Length != categoryCount)

            {

                // COLD ALLOC: DeterminismCategorySample[4] - one row per LockstepHashCategory member,

                // sized from the owner's enum so a fifth category cannot be silently dropped, read once at

                // end of run - owner: H8_HeadlessPlayModeProbe

                _determinismCategories = new DeterminismCategorySample[categoryCount];

            }



            for (int i = 0; i < categoryCount; i++)

            {

                _determinismCategories[i] = new DeterminismCategorySample

                {

                    Name = ((Hecton8.Core.Determinism.LockstepHashCategory)i).ToString(),

                };

            }



            if (!EditorApplication.isPlaying)

            {

                _determinismState = DeterminismCapture.NoPlaySession;

                return;

            }



            // Owner presence is reported separately from the number, because "no validator exists" and

            // "the validator exists and never sampled" are different findings with different owners. The

            // component is created at runtime by a RuntimeInitializeOnLoadMethod and its GameObject is

            // HideInHierarchy, so a scene search cannot see it; FindObjectsByType can. No

            // FindObjectsSortMode overload - it is deprecated in 6000.5 (CS0618) and this only enumerates.

            Hecton8.Core.Determinism.LockstepStateValidator[] validators =

                UnityEngine.Object.FindObjectsByType<Hecton8.Core.Determinism.LockstepStateValidator>(

                    FindObjectsInactive.Include);

            Hecton8.Core.Determinism.LockstepStateValidator owner = null;

            _determinismValidatorInstances = validators.Length;

            _determinismValidatorEnabled = 0;

            for (int i = 0; i < validators.Length; i++)

            {

                Hecton8.Core.Determinism.LockstepStateValidator validator = validators[i];

                if (validator == null)

                    continue;



                if (owner == null)

                    owner = validator;



                if (!validator.isActiveAndEnabled)

                    continue;



                _determinismValidatorEnabled++;

                owner = validator;

            }



            Hecton8.Core.Memory.IDataVault vault = GlobalRegistry.DataVault;

            if (vault == null)

            {

                _determinismState = DeterminismCapture.NoDataVault;

                return;

            }



            // These two are the only states that make the owner's OpenOrAcquireVaultBuffer refuse a cold

            // allocation outright (LockstepStateValidator.cs:1751), and it refuses SILENTLY - it returns

            // false and the caller, EnsureNativeState, ignores the result. So an owner that is present with

            // no buffer is explained by these before any other hypothesis is worth spending a run on.

            _determinismVaultAllocationLocked = vault.IsAllocationLocked;

            _determinismVaultCompactionFenceActive = vault.IsCompactionFenceActive;



            // Type arguments are written out at every call site on purpose: NativeArray<T>.ReadOnly is a

            // nested type of a generic, and relying on the compiler to infer T through one is not worth the

            // risk in a file whose owner cannot hold the Unity lock to compile it.

            bool hashBufferPresent = TryReadDeterminismBuffer<ulong>(

                vault,

                Hecton8.Core.Memory.BufferID.LockstepMasterStateHash,

                1,

                out Unity.Collections.NativeArray<ulong>.ReadOnly liveHash);



            ulong vaultHash = hashBufferPresent ? liveHash[0] : 0UL;

            ulong accessorHash = owner != null ? owner.LastMasterStateHash : 0UL;



            // Prefer the owner's own accessor: calling the authority cannot drift from what the authority

            // publishes. Fall back to the published buffer when the accessor reads zero and the buffer does

            // not - a real case, not defensive padding. LastMasterStateHash resolves through the validator's

            // OWN cached _dataVault field (LockstepStateValidator.ResolveDataVault returns it verbatim),

            // while this read goes through GlobalRegistry.DataVault, so a validator that never refreshed its

            // dependencies reports zero over a buffer that still holds the hash it wrote. Preferring the

            // accessor and then reporting nothing would have published a zero as "this run's hash".

            //

            // A disagreement between the two is therefore a finding, not a formatting detail: it means the

            // determinism owner is not reading the vault the registry publishes. It is recorded and printed

            // rather than resolved silently.

            _determinismMasterHash = accessorHash != 0UL ? accessorHash : vaultHash;

            _determinismHashFromOwnerAccessor = accessorHash != 0UL;

            _determinismAccessorVaultDisagreement =

                owner != null && hashBufferPresent && accessorHash != vaultHash;



            if (!hashBufferPresent)

            {

                // The unallocated-buffer case used to be ONE state. It is two findings with two owners:

                // no validator at all is a lifetime defect, and a live validator over an unallocated buffer

                // is a vault-timing defect. Reporting both as "NoHashBuffer" cost an editor run per

                // hypothesis, which is why the split is here and not in a comment.

                _determinismHashBufferDiagnosis = DiagnoseDeterminismHashBuffer(vault);

                _determinismState = _determinismValidatorInstances > 0

                    ? DeterminismCapture.OwnerPresentBufferUnopened

                    : DeterminismCapture.OwnerAbsentNoBuffer;

                return;

            }



            if (TryReadDeterminismBuffer<uint>(

                    vault,

                    Hecton8.Core.Memory.BufferID.LockstepMasterFlags,

                    1,

                    out Unity.Collections.NativeArray<uint>.ReadOnly masterFlags))

            {

                _determinismMasterFlags = masterFlags[0];

            }



            if (TryReadDeterminismBuffer<Hecton8.Core.Determinism.LockstepArrayHash>(

                    vault,

                    Hecton8.Core.Memory.BufferID.LockstepArrayHashes,

                    categoryCount,

                    out Unity.Collections.NativeArray<Hecton8.Core.Determinism.LockstepArrayHash>.ReadOnly

                        arrayHashes))

            {

                for (int i = 0; i < categoryCount; i++)

                {

                    Hecton8.Core.Determinism.LockstepArrayHash entry = arrayHashes[i];

                    _determinismCategories[i] = new DeterminismCategorySample

                    {

                        Name = ((Hecton8.Core.Determinism.LockstepHashCategory)i).ToString(),

                        Hash = entry.Hash,

                        Count = entry.Count,

                        Flags = entry.Flags,

                    };

                }

            }



            // The history ring is the only place the SAMPLED FRAME is recorded, and the frame is what makes

            // the hash comparable at all - LockstepHashMath.BuildMasterHash folds it in.

            // LockstepStateValidator.RecordMasterHashHistory writes an entry only when the sample carried

            // no missing/truncated/non-finite flag, so this is the newest CLEAN sample, which may be older

            // than the live master hash above. Scanned by highest frame rather than read through the

            // cursor: a stale or garbage cursor would otherwise hand back an arbitrary older entry as "the

            // latest", and the maximum is correct whatever the cursor says.

            if (TryReadDeterminismBuffer<Hecton8.Core.Determinism.LockstepMasterHashHistoryEntry>(

                    vault,

                    Hecton8.Core.Memory.BufferID.LockstepMasterHashHistory,

                    1,

                    out Unity.Collections.NativeArray<Hecton8.Core.Determinism.LockstepMasterHashHistoryEntry>

                        .ReadOnly history))

            {

                for (int i = 0; i < history.Length; i++)

                {

                    Hecton8.Core.Determinism.LockstepMasterHashHistoryEntry entry = history[i];

                    if (entry.Frame == 0u || entry.Frame < _determinismLastCleanFrame)

                        continue;



                    _determinismLastCleanFrame = entry.Frame;

                    _determinismLastCleanHash = ((ulong)entry.HashHi << 32) | entry.HashLo;

                }

            }



            // Zero is the owner's own "before the first sampled frame" value - see the summary on

            // LockstepStateValidator.LastMasterStateHash - so it is treated as never-sampled here rather

            // than published as a hash somebody might diff against another zero.

            _determinismState = _determinismMasterHash != 0UL || _determinismLastCleanFrame != 0u

                ? DeterminismCapture.Sampled

                : DeterminismCapture.NeverSampled;

        }



        /// <summary>

        /// Read-only view of one published vault buffer. Mirrors the guards in

        /// <c>LockstepStateValidator.TryReadVaultBuffer</c>: a zero generation means the buffer was never

        /// allocated, and a short buffer is refused rather than indexed.

        /// </summary>

        private static bool TryReadDeterminismBuffer<T>(

            Hecton8.Core.Memory.IDataVault vault,

            Hecton8.Core.Memory.BufferID bufferId,

            int requiredLength,

            out Unity.Collections.NativeArray<T>.ReadOnly buffer)

            where T : struct

        {

            buffer = default;

            if (vault == null || requiredLength < 0)

                return false;



            if (!vault.TryGetGenerationHandle<T>(

                    bufferId, out Hecton8.Core.Memory.VaultGenerationHandle<T> handle))

            {

                return false;

            }



            if (handle.Generation == 0u || handle.BufferID != unchecked((uint)(int)bufferId))

                return false;



            if (!vault.TryReadOnlyHandle(in handle, out buffer) || !buffer.IsCreated)

            {

                buffer = default;

                return false;

            }



            if (buffer.Length < requiredLength)

            {

                buffer = default;

                return false;

            }



            return true;

        }



        /// <summary>

        /// Why <see cref="TryReadDeterminismBuffer{T}"/> refused the master-hash buffer, in the vault's own

        /// terms. Called at most once per run, only on the failure branch.

        ///

        /// The guards below MIRROR that method's guards instead of sharing them, and that is a real cost:

        /// change one and this drifts. The alternative was an out-parameter reason on a generic method with

        /// four call sites, in a file whose owner cannot hold the Unity lock to compile it. The four

        /// refusals are not interchangeable - "never requested" indicts the owner, "generation 0" indicts

        /// the allocation, "crossed BufferID" and "view refused" indict the vault - and collapsing them into

        /// one <c>false</c> is what made this state unreadable in the first place.

        /// </summary>

        private static string DiagnoseDeterminismHashBuffer(Hecton8.Core.Memory.IDataVault vault)

        {

            if (vault == null)

                return "GlobalRegistry.DataVault is null, so nothing could be read";



            if (!vault.TryGetGenerationHandle<ulong>(

                    Hecton8.Core.Memory.BufferID.LockstepMasterStateHash,

                    out Hecton8.Core.Memory.VaultGenerationHandle<ulong> handle))

            {

                return "the vault publishes NO generation handle for BufferID.LockstepMasterStateHash, so " +

                    "the buffer was never requested from it - LockstepStateValidator.EnsureNativeState " +

                    "either never ran or its OpenOrAcquireVaultBuffer call returned before " +

                    "EnsureGenerationHandle";

            }



            if (handle.Generation == 0u)

            {

                return "a generation handle exists with Generation=0, the vault's 'never allocated' value: " +

                    "the BufferID is known but no arena block was ever committed to it";

            }



            if (handle.BufferID != unchecked((uint)(int)Hecton8.Core.Memory.BufferID.LockstepMasterStateHash))

            {

                return "the published handle carries a DIFFERENT BufferID than the one requested, so this " +

                    "vault slot's metadata is crossed - a vault defect, not a determinism one";

            }



            if (!vault.TryReadOnlyHandle(

                    in handle, out Unity.Collections.NativeArray<ulong>.ReadOnly buffer) ||

                !buffer.IsCreated)

            {

                return "a valid generation handle exists but TryReadOnlyHandle refused the view, which is " +

                    "what an evicted block or an active compaction fence looks like from outside the vault";

            }



            if (buffer.Length < 1)

                return "the buffer is allocated with length 0, so it has no element to hold a hash";



            return "the read SUCCEEDED on this second attempt after failing on the first, so the buffer " +

                "state is changing under the reader and no reading in this block is trustworthy";

        }



        /// <summary>

        /// Prints the three numbers a second run of the same seed is compared on, and the coverage limits

        /// that decide whether the comparison means anything.

        ///

        /// The falsifiable part is the per-category element count, not the hash. "Both runs produced

        /// 0x1234..." cannot be wrong. "0x1234... over RigidbodyAups=0 PlayerKinematicState=1

        /// RoomWaterLevels=0 EntityAups=0" can be - and a hash folded over three empty categories is

        /// reproducible for a reason that has nothing to do with the simulation being deterministic.

        /// </summary>

        private static void ReportDeterminismState()

        {

            bool missing = (_determinismMasterFlags & DeterminismArrayFlagMissing) != 0u;

            bool truncated = (_determinismMasterFlags & DeterminismArrayFlagTruncated) != 0u;

            bool nonFinite = (_determinismMasterFlags & DeterminismArrayFlagNonFinite) != 0u;

            uint hashedElements = SumDeterminismHashedElements();

            int gameFrames = Time.frameCount - _gameFrameAtPlayStart;



            Debug.Log(

                $"{Marker} DETERMINISM state={_determinismState} " +

                $"masterStateHash=0x{_determinismMasterHash.ToString("X16", CultureInfo.InvariantCulture)} " +

                $"lastCleanStateHash=0x{_determinismLastCleanHash.ToString("X16", CultureInfo.InvariantCulture)} " +

                $"lastCleanPostSimFrame={_determinismLastCleanFrame} " +

                $"gameFrames={gameFrames} dispatcherFrameId={_determinismDispatcherFrameId} " +

                // InvariantCulture is load-bearing on this line, not tidiness. This machine runs a

                // comma-decimal locale, and the bare "{value:F3}" that used to be here printed

                // "slowTickDiscardedSeconds=23,543" into Logs/h8_worldsim_probe5.log:18716. A reader - human

                // or script - has no way to know whether that is 23.543 seconds or 23543, and the number is

                // the run-comparability caveat, so an ambiguous rendering of it is worse than none. The

                // artifact was never affected: FormatNumber already pins InvariantCulture.

                $"slowTickDiscardedSeconds=" +

                $"{_determinismSlowTickDiscardedSeconds.ToString("F3", CultureInfo.InvariantCulture)} " +

                $"slowTickDiscardEvents={_determinismSlowTickDiscardEvents}");



            Debug.Log(

                $"{Marker} DETERMINISM   owner=LockstepStateValidator instances={_determinismValidatorInstances} " +

                $"enabled={_determinismValidatorEnabled} " +

                $"hashFrom={(_determinismHashFromOwnerAccessor ? "LastMasterStateHash accessor" : "vault buffer")} " +

                $"masterFlags=0x{_determinismMasterFlags.ToString("X8", CultureInfo.InvariantCulture)} " +

                $"missing={missing} truncated={truncated} nonFinite={nonFinite} " +

                $"hashedElements={hashedElements}");



            for (int i = 0; i < _determinismCategories.Length; i++)

            {

                DeterminismCategorySample sample = _determinismCategories[i];

                Debug.Log(

                    $"{Marker} DETERMINISM   category {sample.Name,-22} " +

                    $"count={sample.Count,6} hash=0x{sample.Hash.ToString("X8", CultureInfo.InvariantCulture)} " +

                    $"flags=0x{sample.Flags.ToString("X8", CultureInfo.InvariantCulture)}");

            }



            if (_determinismAccessorVaultDisagreement)

            {

                Debug.Log(

                    $"{Marker} DETERMINISM   OWNER/VAULT DISAGREEMENT - LockstepStateValidator" +

                    ".LastMasterStateHash and BufferID.LockstepMasterStateHash do not return the same value. " +

                    "The accessor resolves through the validator's own cached _dataVault field and this read " +

                    "goes through GlobalRegistry.DataVault, so the determinism owner is looking at a " +

                    "different vault than the registry publishes. Fix that before treating either number as " +

                    "this run's state hash.");

            }



            if (_determinismState == DeterminismCapture.Sampled && hashedElements == 0u)

            {

                Debug.Log(

                    $"{Marker} DETERMINISM   REPRODUCIBLE AND EMPTY - every category hashed 0 elements, " +

                    "so two runs matching on this hash proves only that both hashed nothing. Do not quote " +

                    "it as determinism evidence until at least one category carries a count.");

            }

            else if (_determinismState == DeterminismCapture.Sampled && (missing || truncated || nonFinite))

            {

                Debug.Log(

                    $"{Marker} DETERMINISM   the LIVE master hash was built over flagged categories " +

                    "(missing/truncated/non-finite above), so compare lastCleanStateHash instead - that is " +

                    "the newest sample the owner considered clean enough to record.");

            }

            else if (_determinismState == DeterminismCapture.NeverSampled)

            {

                Debug.Log(

                    $"{Marker} DETERMINISM   the hash buffer exists and is still zero, which is the owner's " +

                    "'before the first sampled frame' value: this run never reached a hash frame. " +

                    "ResolveHashCadenceFrames() samples every 60-1200 post-simulation ticks, so a headless " +

                    "run that advances a handful of frames can end before the first sample.");

            }

            else if (_determinismState == DeterminismCapture.OwnerAbsentNoBuffer)

            {

                Debug.Log(

                    $"{Marker} DETERMINISM   OWNER ABSENT - NO LockstepStateValidator instance exists " +

                    "anywhere in the running session, DontDestroyOnLoad and HideInHierarchy objects " +

                    "included, and BufferID.LockstepMasterStateHash is unallocated. Nothing was hashed " +

                    "because there was nothing to hash with: this is a LIFETIME defect in the owner, not a " +

                    "hashing or coverage defect, and no change to the hash path can fix it. Vault refusal: " +

                    _determinismHashBufferDiagnosis);

            }

            else if (_determinismState == DeterminismCapture.OwnerPresentBufferUnopened)

            {

                Debug.Log(

                    $"{Marker} DETERMINISM   OWNER PRESENT, BUFFER UNOPENED - " +

                    $"{_determinismValidatorInstances} LockstepStateValidator instance(s) exist of which " +

                    $"{_determinismValidatorEnabled} are active and enabled, yet " +

                    "BufferID.LockstepMasterStateHash is unallocated. The owner is alive and its buffer open " +

                    "FAILED SILENTLY - EnsureNativeState discards OpenOrAcquireVaultBuffer's return value " +

                    "(LockstepStateValidator.cs:1883-1892), so the failure produces no log line of its own. " +

                    "Vault refusal: " + _determinismHashBufferDiagnosis);

            }

            else if (_determinismState == DeterminismCapture.NoDataVault)

            {

                Debug.Log(

                    $"{Marker} DETERMINISM   GlobalRegistry.DataVault is null, so no vault-published state " +

                    "could be read at all. Every number on the line above is absent, not zero.");

            }

            else if (_determinismState != DeterminismCapture.Sampled)

            {

                Debug.Log(

                    $"{Marker} DETERMINISM   no read was performed (state={_determinismState}); the numbers " +

                    "above carry no evidence.");

            }



            ReportDeterminismOwnerLifetime();

            ReportDeterminismDiscardCaveat();



            Debug.Log(

                $"{Marker} DETERMINISM   COVERAGE: {DescribeDeterminismCoverage()}");

        }



        /// <summary>

        /// Prints the owner-lifetime trace and, where the three samples permit it, names the WINDOW in which

        /// the determinism owner disappeared.

        ///

        /// This is the part that stops costing an editor run per hypothesis. "instances=0 at end of run" is

        /// consistent with three different defects; "one instance in 00_BOOTSTRAP, zero once the world scene

        /// is active" is consistent with exactly one, and it names the scene transition that did it.

        /// </summary>

        private static void ReportDeterminismOwnerLifetime()

        {

            Debug.Log(

                $"{Marker} DETERMINISM   OWNER LIFETIME " +

                $"bootWarmup={DescribeOwnerSample(in _determinismOwnerAtBootWarmup)} " +

                $"firstGameplayTick={DescribeOwnerSample(in _determinismOwnerAtGameplayStart)} " +

                $"endOfRun=instances:{_determinismValidatorInstances}/enabled:{_determinismValidatorEnabled}" +

                $" vaultAllocationLocked={_determinismVaultAllocationLocked} " +

                $"vaultCompactionFenceActive={_determinismVaultCompactionFenceActive}");



            if (_determinismReviveRequested)

            {

                Debug.Log(

                    $"{Marker} DETERMINISM   OWNER REVIVE requested=true created={_determinismReviveCreated} " +

                    $"note='{_determinismReviveNote}'. A revived owner's hash is NOT comparable with a run " +

                    "whose owner lived from boot: its post-simulation frame counter starts at 0 and " +

                    "BuildMasterHash folds the frame in.");

            }



            bool haveBoot = _determinismOwnerAtBootWarmup.Taken;

            bool haveGameplay = _determinismOwnerAtGameplayStart.Taken;



            // Every reading below is stated as what the samples SHOW, never as a cause. The probe can see

            // existence at three instants; it cannot see who destroyed an object.

            if (haveBoot && _determinismOwnerAtBootWarmup.Instances == 0)

            {

                Debug.Log(

                    $"{Marker} DETERMINISM   LIFETIME READING the owner was ALREADY ABSENT during the boot " +

                    $"warmup, with '{_determinismOwnerAtBootWarmup.ActiveScene}' active. It is created by " +

                    "RuntimeInitializeOnLoadMethod(AfterSceneLoad), which has certainly run by then, so the " +

                    "absence is NOT a scene transition destroying it later - either the hook did not run or " +

                    "the instance died within the boot scene. Look at the creation hook, not at the route.");

            }

            else if (haveBoot && haveGameplay &&

                _determinismOwnerAtBootWarmup.Instances > 0 &&

                _determinismOwnerAtGameplayStart.Instances == 0)

            {

                Debug.Log(

                    $"{Marker} DETERMINISM   LIFETIME READING the owner EXISTED during the boot warmup in " +

                    $"'{_determinismOwnerAtBootWarmup.ActiveScene}' and was GONE by the first gameplay tick " +

                    $"in '{_determinismOwnerAtGameplayStart.ActiveScene}'. The window that consumed it is " +

                    "the boot route's scene loads, and its creation hook fires once per play session, so " +

                    "nothing recreates it. This is an owner-lifetime defect and it applies to a PLAYER " +

                    "session identically - the probe changes no part of that sequence.");

            }

            else if (haveBoot && haveGameplay &&

                _determinismOwnerAtBootWarmup.Instances > 0 &&

                !_determinismOwnerAtBootWarmup.HashBufferPresent &&

                _determinismOwnerAtGameplayStart.HashBufferPresent)

            {

                Debug.Log(

                    $"{Marker} DETERMINISM   LIFETIME READING the owner existed at the boot warmup with its " +

                    "master-hash buffer still UNALLOCATED and the buffer was allocated by the first gameplay " +

                    "tick, so the buffer open is late rather than absent. A run that ends before that point " +

                    "reports no hash for a timing reason and not a wiring one.");

            }

            else if (!haveBoot)

            {

                Debug.Log(

                    $"{Marker} DETERMINISM   LIFETIME READING no boot-warmup sample was taken, so the " +

                    "end-of-run instance count cannot be attributed to a window. This run stopped before the " +

                    "warmup completed.");

            }

        }



        /// <summary>

        /// One <see cref="DeterminismOwnerSample"/> as a compact field, with "not sampled" distinguishable

        /// from "sampled, zero instances". Cold string building for the end-of-run report only.

        /// </summary>

        private static string DescribeOwnerSample(in DeterminismOwnerSample sample)

        {

            if (!sample.Taken)

                return "NOT_SAMPLED";



            return "instances:" + sample.Instances.ToString(CultureInfo.InvariantCulture) +

                "/enabled:" + sample.Enabled.ToString(CultureInfo.InvariantCulture) +

                "/vault:" + (sample.VaultPresent ? "present" : "null") +

                "/hashBuffer:" + (sample.HashBufferPresent ? "allocated" : "unallocated") +

                "/scene:" + (sample.ActiveScene ?? "unknown");

        }



        /// <summary>

        /// States the slow-tick discard as a FIRST-CLASS determinism caveat rather than one number among

        /// twelve on the headline line.

        ///

        /// WHY IT DESERVES ITS OWN LINE. The measured run discarded 23.543 simulation seconds over 4 events

        /// across 2490 game frames (Logs/h8_worldsim_probe5.log:18716). The clamp that discards it is

        /// CORRECT - it is the anti-death-spiral guard, and removing it would let a stalled headless frame

        /// queue an unbounded slow-tick backlog and then spend minutes draining it - so this is not a bug

        /// report. It is a comparability statement: two runs of the same seed that discarded different

        /// amounts of owed simulation time did not simulate the same world, and their hashes may differ for

        /// that reason alone. A hash difference between such runs proves nothing about determinism, and

        /// before this line existed the discard was reported as a bare number that no reader treated as a

        /// precondition for the comparison.

        /// </summary>

        private static void ReportDeterminismDiscardCaveat()

        {

            if (_determinismSlowTickDiscardEvents <= 0 && _determinismSlowTickDiscardedSeconds <= 0.0)

            {

                Debug.Log(

                    $"{Marker} DETERMINISM   SLOWTICK DISCARD none - the slow-tick lane received every " +

                    "second it was owed, so no simulation time was dropped and this run's frame count " +

                    "describes the simulation it actually ran.");

                return;

            }



            Debug.Log(

                $"{Marker} DETERMINISM   SLOWTICK DISCARD CAVEAT " +

                $"{_determinismSlowTickDiscardedSeconds.ToString("F3", CultureInfo.InvariantCulture)}s of " +

                $"owed simulation time was DISCARDED over {_determinismSlowTickDiscardEvents} clamp " +

                "event(s). The clamp is correct and stays - it is the anti-death-spiral guard, and without " +

                "it a stalled headless frame would queue an unbounded slow-tick backlog. The consequence is " +

                "about COMPARISON, not correctness: this run did not simulate what its frame count implies, " +

                "and it is NOT comparable with a run that discarded a different amount, even with an " +

                "identical seed and an identical hash. Compare slowTickDiscardedSeconds BEFORE comparing " +

                "hashes; if the two differ, a hash mismatch is explained and proves nothing about " +

                "determinism.");

        }



        private static uint SumDeterminismHashedElements()

        {

            uint total = 0u;

            for (int i = 0; i < _determinismCategories.Length; i++)

                total += _determinismCategories[i].Count;



            return total;

        }



        /// <summary>

        /// The honest scope of the hash, in one sentence per limit. Kept as a method so the console line

        /// and the artifact's <c>coverage</c> field cannot describe the same number differently.

        /// </summary>

        private static string DescribeDeterminismCoverage()

        {

            return

                "LockstepHashCategory has 4 members, so this hash folds exactly 4 vault buffers - " +

                "RigidbodyAUPs, PlayerKinematicState (1 entry), RoomWaterLevels (<=256 habitat rooms) and " +

                "EntityAUPs. Terrain, voxels, ecosystem populations, weather, storms, inventory, quests, " +

                "fauna genetics, flora, the water simulation and every RNG stream are OUTSIDE it, so equal " +

                "hashes mean THOSE FOUR BUFFERS matched at the sampled frame and nothing more. Positions " +

                "are quantised to 1 mm and water levels to 1e-4, so finer divergence is invisible here. " +

                "BuildMasterHash folds the sampled frame in, and the sample cadence is derived from " +

                "HomeostasisBrain.GlobalQualityWeight and SystemHealthIndex01, which react to wall-clock " +

                "frame times - two runs can sample at different frames and differ on the hash with " +

                "identical state, so compare lastCleanPostSimFrame before concluding anything from a " +

                "mismatch. slowTickDiscardedSeconds is the simulation time the slow-tick lane was owed and " +

                "never received; a run with a large value did not simulate what its frame count implies " +

                "and is not comparable to a run with a small one.";

        }



        /// <summary>

        /// Whether this run may be compared with another one at all, and why not when it may not.

        ///

        /// The slow-tick discard is a PRECONDITION for comparing hashes, not a footnote to them, and it was

        /// previously reported only as two loose numbers on a line with eleven others. Pure string building

        /// over captured statics so it stays callable from <see cref="WriteRouteArtifact"/> on a terminal

        /// path where <see cref="CaptureDeterminismState"/> never ran.

        /// </summary>

        private static string DescribeDeterminismComparability()

        {

            bool discarded =

                _determinismSlowTickDiscardEvents > 0 || _determinismSlowTickDiscardedSeconds > 0.0;

            string discard =

                _determinismSlowTickDiscardedSeconds.ToString("F3", CultureInfo.InvariantCulture) +

                "s over " + _determinismSlowTickDiscardEvents.ToString(CultureInfo.InvariantCulture) +

                " clamp event(s)";



            if (_determinismState != DeterminismCapture.Sampled)

            {

                return "NOT COMPARABLE - no state hash was sampled (state=" + _determinismState.ToString() +

                    "), so this run carries no number another run can be diffed against. Slow-tick discard " +

                    "this run: " + discard + ".";

            }



            if (discarded)

            {

                return "NOT COMPARABLE UNLESS THE DISCARD MATCHES - " + discard + " of owed simulation time " +

                    "was dropped by the anti-death-spiral clamp, so this run did not simulate what its frame " +

                    "count implies. The clamp is correct and stays. The consequence is procedural: a second " +

                    "run must report the SAME slowTickDiscardedSeconds before its hash may be diffed against " +

                    "this one, and a mismatch between two runs that discarded different amounts proves " +

                    "nothing about determinism.";

            }



            return "COMPARABLE on the four covered buffers - a hash was sampled and no owed simulation time " +

                "was discarded. Still bounded by the coverage field, and lastCleanPostSimFrame must match " +

                "before a hash difference means anything.";

        }



        /// <summary>

        /// One line of determinism state for the Proof row. Pure string building over the captured

        /// statics - it must stay callable from <see cref="WriteRouteArtifact"/> on a terminal path where

        /// <see cref="CaptureDeterminismState"/> never ran.

        /// </summary>

        private static string DescribeDeterminismForProof()

        {

            if (_determinismState != DeterminismCapture.Sampled)

            {

                return $"no comparable state hash this run (state={_determinismState}), " +

                    $"slowTickDiscardedSeconds={_determinismSlowTickDiscardedSeconds.ToString("F3", CultureInfo.InvariantCulture)} " +

                    $"over {_determinismSlowTickDiscardEvents} discard events";

            }



            return

                $"masterStateHash=0x{_determinismMasterHash.ToString("X16", CultureInfo.InvariantCulture)} " +

                $"lastCleanPostSimFrame={_determinismLastCleanFrame} " +

                $"over {SumDeterminismHashedElements()} hashed elements in 4 buffers, " +

                $"slowTickDiscardedSeconds={_determinismSlowTickDiscardedSeconds.ToString("F3", CultureInfo.InvariantCulture)} " +

                $"over {_determinismSlowTickDiscardEvents} discard events";

        }



        private static bool IsAlive(object service)

        {

            if (service is UnityEngine.Object unityObject)

                return unityObject != null;



            return service != null;

        }



        private static void Finish(int exitCode)

        {

            EditorApplication.update -= Tick;

            if (_logHookInstalled)

            {

                Application.logMessageReceived -= OnLogMessage;

                _logHookInstalled = false;

            }



            CloseClockSegment();

            Phase finalPhase = _phase;

            _phase = Phase.Idle;

            _clockPhase = Phase.Idle;



            // Every terminal path writes the artifact, including ABORT, TIMEOUT and a boot that left

            // Play Mode early. That is the point: a run that failed early is exactly the run whose

            // numbers someone will want to compare against a run that succeeded.

            WriteRouteArtifact(exitCode, finalPhase);



            Debug.Log($"{Marker} DONE exitCode={exitCode}");



            if (Application.isBatchMode)

                EditorApplication.Exit(exitCode);

        }



        /// <summary>

        /// Clears every static this probe carries across a run.

        ///

        /// The class relies on DisableDomainReload to survive the Play Mode transition, which also

        /// means nothing resets these between two invocations in one editor session. A second run

        /// would otherwise inherit the first run's phase table, moment verdicts and rejected-service

        /// list and report them as its own.

        /// </summary>



        /// <summary>

        /// L16 product clock arm for the playmode probe route.

        /// Unpause + re-request headless dilation + enable step-bounded dispatcher time.

        /// Mirrors <c>HeadlessSimulationRunner.EnsureHeadlessSimulationClock</c>.

        /// Batchmode WallClock often yields unscaledDeltaTime==0 so RunFixedStepAccumulator

        /// early-outs and HPM.FixedTick never runs; EnableStepBoundedTime supplies a real fixed

        /// unscaled dt per update. Does not mock hop2, does not call FixedTick/GetState from the probe.

        /// </summary>


        // L19 hop2 LIVE: batch peel ParticleSystem - stop+disable all instances once.
        // Native PlayerLoop PreLateUpdate.ParticleSystemBeginUpdateAll JobQueue crash under batch.
        private static void TryDisableParticleSystemsBatch()
        {
            if (!Application.isBatchMode || _batchParticlesDisabled)
                return;
            _batchParticlesDisabled = true;
            int stopped = 0;
            try
            {
                var systems = UnityEngine.Object.FindObjectsByType<ParticleSystem>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                if (systems == null || systems.Length == 0)
                    systems = UnityEngine.Object.FindObjectsOfType<ParticleSystem>(true);
                for (int i = 0; i < systems.Length; i++)
                {
                    var ps = systems[i];
                    if (ps == null)
                        continue;
                    try
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        var em = ps.emission;
                        em.enabled = false;
                        ps.Clear(true);
                        // Do not SetActive(false) on shared GOs - may disable critical parents.
                        var renderer = ps.GetComponent<ParticleSystemRenderer>();
                        if (renderer != null)
                            renderer.enabled = false;
                        stopped++;
                    }
                    catch (System.Exception)
                    {
                        try { if (ps != null) ps.Pause(); stopped++; } catch (System.Exception) { }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[H8_PLAYPROBE] L19 hop2 LIVE: ParticleSystem disable failed: " + ex.Message);
            }
            Debug.Log("[H8_PLAYPROBE] L19 hop2 LIVE: batch peel ParticleSystem disabled=" + stopped.ToString());
        }

        // L19 hop2 LIVE: editor-only missing-script strip for batch playprobe.

        // Native GetScriptCache AV during Behaviour Update after ActivatePlayer

        // when a loaded GO still carries a null-script MonoBehaviour shell.

        private static void StripMissingScriptsForBatchProbe()

        {

            if (!Application.isBatchMode)

                return;



            int stripped = 0;

            int scanned = 0;

            // Include inactive: player hierarchy may still be toggling during ActivatePlayer.

            GameObject[] roots = UnityEngine.Object.FindObjectsByType<GameObject>(

                FindObjectsInactive.Include,

                FindObjectsSortMode.None);

            for (int i = 0; i < roots.Length; i++)

            {

                GameObject go = roots[i];

                if (go == null)

                    continue;

                scanned++;

                int missing = UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);

                if (missing <= 0)

                    continue;

                stripped += UnityEditor.GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

            }



            Debug.Log(

                "[H8_PLAYPROBE] L19 hop2 LIVE: missing-script strip scanned=" +

                scanned + " stripped=" + stripped);

        }



        private static void EnsureProbeSimulationClock(string reason)

        {

            ITickDispatcher dispatcher = GlobalRegistry.TickDispatcher;

            if (dispatcher == null)

            {

                Debug.Log($"{Marker} SIMCLOCK ensure reason={reason} dispatcher=null");

                return;

            }



            bool wasPaused = dispatcher.SimulationPaused;

            float dilBefore = dispatcher.TimeDilationScalar;

            bool stepBoundBefore = SystemDispatcher.IsStepBoundedTimeActive;



            // ConsumeFrameTimeDilationScalar returns 0 while _simulationPaused — unpause first.

            if (wasPaused)

                dispatcher.RequestSimulationPause(false, ProbeSimClockHash);



            dispatcher.RequestHeadlessTimeDilation(ProbeTimeDilationScalar, ProbeSimClockHash);



            // Real product headless time source (InternalsVisibleTo Hecton8.Editor).

            // Idempotent: EnableStepBoundedTime resets elapsed only when first arming; keep armed.

            bool stepBoundOk = stepBoundBefore;

            if (!stepBoundBefore)

                stepBoundOk = SystemDispatcher.EnableStepBoundedTime(ProbeStepBoundedDeltaSeconds);



            _lastProbeClockEnsureRealtime = EditorApplication.timeSinceStartup;

            _probeSimClockArmed = stepBoundOk || SystemDispatcher.IsStepBoundedTimeActive;



            bool pausedAfter = dispatcher.SimulationPaused;

            float dilAfter = dispatcher.TimeDilationScalar;

            bool stepBoundAfter = SystemDispatcher.IsStepBoundedTimeActive;

            Debug.Log(

                $"{Marker} SIMCLOCK ensure reason={reason}" +

                " pausedBefore=" + (wasPaused ? "1" : "0") +

                " dilBefore=" + dilBefore.ToString("0.###", CultureInfo.InvariantCulture) +

                " dilAfter=" + dilAfter.ToString("0.###", CultureInfo.InvariantCulture) +

                " pausedAfter=" + (pausedAfter ? "1" : "0") +

                " stepBoundBefore=" + (stepBoundBefore ? "1" : "0") +

                " stepBoundAfter=" + (stepBoundAfter ? "1" : "0") +

                " stepBoundOk=" + (stepBoundOk ? "1" : "0") +

                " stepDt=" + ProbeStepBoundedDeltaSeconds.ToString("0.###", CultureInfo.InvariantCulture) +

                " armed=" + (_probeSimClockArmed ? "1" : "0"));

        }



        /// <summary>

        /// During GameplayWarmup, periodically re-assert the real clock against late

        /// SimulationPauseSignal / pause-menu / desync paths that drop step-bound or dilation.

        /// </summary>

        private static void MaybeEnsureProbeSimulationClockSustain()

        {

            if (_lastProbeClockEnsureRealtime > 0.0 &&

                EditorApplication.timeSinceStartup - _lastProbeClockEnsureRealtime < ProbeClockEnsureIntervalSeconds)

            {

                // Cheap path between throttle windows: still force if step-bound dropped.

                if (SystemDispatcher.IsStepBoundedTimeActive)

                {

                    ITickDispatcher d = GlobalRegistry.TickDispatcher;

                    if (d != null &&

                        !d.SimulationPaused &&

                        d.TimeDilationScalar + 0.01f >= ProbeTimeDilationScalar)

                        return;

                }

            }



            ITickDispatcher dispatcher = GlobalRegistry.TickDispatcher;

            if (dispatcher == null)

                return;



            bool needsRestore = dispatcher.SimulationPaused ||

                                dispatcher.TimeDilationScalar + 0.01f < ProbeTimeDilationScalar ||

                                !SystemDispatcher.IsStepBoundedTimeActive;

            if (!needsRestore)

            {

                _lastProbeClockEnsureRealtime = EditorApplication.timeSinceStartup;

                return;

            }



            EnsureProbeSimulationClock("gameplay-sustain");

        }



        /// <summary>

        /// L17 product FO drain for the playmode probe route.

        /// Mirrors <c>HeadlessSimulationRunner.Update</c> calling

        /// <c>HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks</c> every tick while

        /// <c>IsOriginShiftBootstrapLocked</c> can starve FixedTick after PreSim and freeze LateFrame.

        /// Probe is still INPUT PRODUCER only via WorldDriver; this is external FO drain (designed

        /// product path — FO.Tick itself is blocked by the same lock). Does not mock hop2.

        /// </summary>

        private static void DrainProbeFloatingOriginBootstrap(string reason)

        {

            // Always flush FO rebase under batch - correctness path.

            bool flushClean = HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks();

            _probeFoDrainCalls++;

            if (flushClean)

                _probeFoDrainCleanCount++;



            // L19 hop2 LIVE: skip FODRAIN Debug.Log under batch - mono-fatal

            // DebugLogHandler after INPUTRESIDUE mega-warning spam during WORLDDRIVER.

            if (UnityEngine.Application.isBatchMode)

                return;



            double now = EditorApplication.timeSinceStartup;

            bool forceFirst = _lastProbeFoDrainDiagRealtime <= 0.0;

            bool intervalDue = forceFirst ||

                               (now - _lastProbeFoDrainDiagRealtime) >= ProbeFoDrainDiagIntervalSeconds;

            // Always emit when lock still held after a drain attempt so LIVE can prove residual.

            bool lockHeld = SystemDispatcher.IsOriginShiftBootstrapLocked;

            if (!intervalDue && !lockHeld)

                return;



            _lastProbeFoDrainDiagRealtime = now;



            HectonFloatingOrigin.CopyBootstrapDrainSnapshot(

                out bool foHasOrigin,

                out bool foShift,

                out bool foPhysicsPause,

                out bool foLock,

                out int foPendingScenes,

                out bool foTargetsDirty,

                out bool foBarrier);



            ITickDispatcher dispatcher = GlobalRegistry.TickDispatcher;

            bool paused = dispatcher != null && dispatcher.SimulationPaused;

            float dil = dispatcher != null ? dispatcher.TimeDilationScalar : -1f;



            Debug.Log(

                $"{Marker} FODRAIN reason={reason}" +

                " flushClean=" + (flushClean ? "1" : "0") +

                " calls=" + _probeFoDrainCalls.ToString(CultureInfo.InvariantCulture) +

                " clean=" + _probeFoDrainCleanCount.ToString(CultureInfo.InvariantCulture) +

                " foHasOrigin=" + (foHasOrigin ? "1" : "0") +

                " foShift=" + (foShift ? "1" : "0") +

                " foPhysicsPause=" + (foPhysicsPause ? "1" : "0") +

                " foLock=" + (foLock ? "1" : "0") +

                " foPendingScenes=" + foPendingScenes.ToString(CultureInfo.InvariantCulture) +

                " foTargetsDirty=" + (foTargetsDirty ? "1" : "0") +

                " foBarrier=" + (foBarrier ? "1" : "0") +

                " dispBoot=" + (lockHeld ? "1" : "0") +

                " dispFrame=" + (SystemDispatcher.IsOriginShiftFrameLockedForCurrentFrame ? "1" : "0") +

                " paused=" + (paused ? "1" : "0") +

                " dil=" + dil.ToString("0.###", CultureInfo.InvariantCulture) +

                " stepBound=" + (SystemDispatcher.IsStepBoundedTimeActive ? "1" : "0") +

                " gameReady=" + (BootstrapState.IsGameReady ? "1" : "0"));

        }



        private static void ResetRunState()

        {

            _phase = Phase.Idle;

            _clockPhase = Phase.Idle;

            _clockSegmentOpen = false;

            _clockWallStart = 0.0;

            _clockGameFrameStart = 0;

            _clockTickStart = 0;

            _totalTicks = 0;

            _phaseSamples.Clear();

            _rejectedServices.Clear();

            SeedRouteMoments();



            _frames = 0;

            _failures = 0;

            _menuFrames = 0;

            _settleFrames = 0;

            _gameplayFrames = 0;

            _menuLoad = null;

            _phaseStartedAt = 0.0;

            _playStartedAt = 0.0;

            _gameFrameAtPlayStart = 0;



            _saveRequestIssued = false;

            _saveAccepted = false;

            _saveBusyObserved = false;

            _saveBusyClearedAt = 0.0;

            _saveLegStartedAt = 0.0;

            _saveLastPollAt = 0.0;

            _saveWaitedSeconds = 0.0;

            _saveWaitBudget = 0.0;

            _saveRoot = string.Empty;

            _saveSlotName = string.Empty;

            _saveError = string.Empty;

            _saveBefore = Array.Empty<SaveFileFacts>();

            _saveFilesAfter = 0;

            _saveDiff = default;



            _artifactWritten = false;



            // Determinism statics are cleared for the reason this whole method exists: a second run in one

            // editor session would otherwise inherit the first run's hash and report it as its own, which

            // is the single worst failure mode available to a determinism instrument - two runs "agreeing"

            // because one of them never read anything. The category array is kept and re-filled in place.

            _determinismState = DeterminismCapture.NotRead;

            _determinismMasterHash = 0UL;

            _determinismMasterFlags = 0u;

            _determinismLastCleanHash = 0UL;

            _determinismLastCleanFrame = 0u;

            _determinismHashFromOwnerAccessor = false;

            _determinismAccessorVaultDisagreement = false;

            _determinismValidatorInstances = 0;

            _determinismValidatorEnabled = 0;

            _determinismDispatcherFrameId = 0u;

            _determinismSlowTickDiscardedSeconds = 0.0;

            _determinismSlowTickDiscardEvents = 0;



            // The lifetime trace is cleared for a sharper version of the same reason: a second run in one

            // editor session that inherited a first run's "the owner existed at boot warmup" sample would

            // report a lifetime window it never observed. _determinismReviveRequested is safe to clear here

            // because Run() calls ResetRunState() FIRST and parses -h8ReviveDeterminismOwner afterwards -

            // reversing that order would make this line eat the argument.

            _determinismReviveRequested = false;

            _determinismOwnerAtBootWarmup = default;

            _determinismOwnerAtGameplayStart = default;

            _determinismHashBufferDiagnosis = string.Empty;

            _determinismVaultAllocationLocked = false;

            _determinismVaultCompactionFenceActive = false;

            _determinismReviveAttempted = false;

            _determinismReviveCreated = false;

            _determinismReviveNote = string.Empty;



            for (int i = 0; i < _determinismCategories.Length; i++)

                _determinismCategories[i] = default;



            _worldDriverStarted = false;

            _placementOwnersRepaired = false;

            _worldDriverGraceTicks = 0;

            _graceOpenedLogged = false;

            _graceClosedLogged = false;

            _gameplayWindowStartedAt = 0.0;

            _lastProbeClockEnsureRealtime = 0.0;

            _probeSimClockArmed = false;

            _lastProbeFoDrainDiagRealtime = 0.0;

            _probeFoDrainCalls = 0;

            _probeFoDrainCleanCount = 0;

            H8_HeadlessWorldDriver.Reset();

        }



        private static string ResolveArtifactPath()

        {

            return string.IsNullOrEmpty(_artifactPath) ? ResolveDefaultArtifactPath() : _artifactPath;

        }



        /// <summary>

        /// The contract's Proof row (<c>FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md:90</c>) wants

        /// console, run, profiler, GC, memory, screenshot/clip AND the save directory diff. This run

        /// produces the run log, the per-phase clock table, the save directory diff and - since

        /// <see cref="CaptureDeterminismState"/> - a comparable end-of-run state number.

        ///

        /// THE ROW STAYS PARTIAL AND THE MISSING LIST IS UNCHANGED. The state hash is a producer for the

        /// run-repeatability half of the row: before it, two runs of one seed left nothing that could be

        /// diffed, so "the run was verified" could not be checked by anyone. It is NOT profiler evidence,

        /// NOT GC evidence, NOT a memory snapshot and NOT a capture, and it must never be offered as a

        /// substitute for any of the four - which is why the detail string says so in those words.

        ///

        /// Recorded from both the console report and the artifact writer so the two outputs of one run

        /// can never disagree about it. <see cref="DescribeDeterminismForProof"/> reads only captured

        /// statics for exactly that reason: this method runs on terminal paths where Play Mode is already

        /// gone and the runtime cannot be touched.

        /// </summary>

        private static void RecordProofMoment()

        {

            RecordMoment(

                MomentProof,

                MomentVerdict.Partial,

                $"run log + per-phase clock table + save directory diff written to '{ResolveArtifactPath()}'; " +

                $"run-repeatability now has a producer as well - {DescribeDeterminismForProof()} - which is " +

                "what a second run of the same seed diffs against, bounded by the coverage limits in the " +

                "artifact's determinism.coverage field; profiler capture, GC evidence, memory snapshot and " +

                "screenshot/clip STILL have no producer in this probe and the state hash substitutes for " +

                "none of the four");

        }



        private static string ResolveDefaultArtifactPath()

        {

            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);

            string root = projectRoot != null ? projectRoot.FullName : Application.dataPath;



            // Logs/ is already gitignored, so a run artifact cannot be swept into a commit by the

            // working-tree cement job, and the probe's own usage line already writes its log there.

            return Path.Combine(root, "Logs", "h8_playprobe_route.json");

        }



        /// <summary>

        /// Writes one machine-readable record of the run.

        ///

        /// Four headless runs today used four different argument sets and left nothing behind but a

        /// megabyte of log each, so none of them could be compared with each other. One JSON with a

        /// fixed schema at a fixed default path is the fix; <c>-h8RouteArtifact</c> moves it when two

        /// runs must not overwrite one another.

        /// </summary>

        private static void WriteRouteArtifact(int exitCode, Phase finalPhase)

        {

            if (_artifactWritten)

                return;



            _artifactWritten = true;



            string path = ResolveArtifactPath();

            RecordProofMoment();



            var builder = new StringBuilder(4096);



            builder.Append("{\n");

            AppendJsonField(builder, "schema", "hecton8.playprobe.route.v1");

            AppendJsonField(builder, "utc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));

            AppendJsonField(builder, "scene", _scenePath);

            AppendJsonNumber(builder, "exitCode", exitCode);

            AppendJsonNumber(builder, "failures", _failures);

            AppendJsonField(builder, "finalPhase", finalPhase.ToString());

            AppendJsonBool(builder, "batchmode", Application.isBatchMode);

            AppendJsonNumber(builder, "probeTicks", _totalTicks);

            AppendJsonNumber(builder, "gameFrames", Time.frameCount - _gameFrameAtPlayStart);

            AppendJsonNumber(

                builder,

                "playWallSeconds",

                _playStartedAt > 0.0 ? EditorApplication.timeSinceStartup - _playStartedAt : 0.0);



            builder.Append("  \"args\": {\n");

            AppendJsonNumber(builder, "warmupFrames", _warmupFrames, "    ");

            AppendJsonNumber(builder, "hardTimeoutSeconds", _hardTimeoutSeconds, "    ");

            AppendJsonNumber(builder, "menuWaitSeconds", _menuWaitSeconds, "    ");

            AppendJsonNumber(builder, "settleWaitSeconds", _settleWaitSeconds, "    ");

            AppendJsonNumber(builder, "gameplaySeconds", _gameplaySeconds, "    ");

            AppendJsonNumber(builder, "saveWaitSeconds", _saveWaitSeconds, "    ");

            AppendJsonNumber(builder, "saveSlot", _saveSlotIndex, "    ");

            AppendJsonBool(builder, "startNewGame", _startNewGame, "    ");

            AppendJsonBool(builder, "forceMenuLoad", _forceMenuLoad, "    ");

            AppendJsonBoolLast(builder, "saveLegEnabled", _saveLegEnabled, "    ");

            builder.Append("  },\n");



            builder.Append("  \"phases\": [\n");

            for (int i = 0; i < _phaseSamples.Count; i++)

            {

                PhaseSample sample = _phaseSamples[i];

                double phaseFps = sample.WallSeconds > 0.0 ? sample.GameFrames / sample.WallSeconds : 0.0;

                builder.Append("    { \"phase\": \"").Append(EscapeJson(sample.Phase))

                    .Append("\", \"wallSeconds\": ").Append(FormatNumber(sample.WallSeconds))

                    .Append(", \"probeTicks\": ").Append(sample.ProbeTicks)

                    .Append(", \"gameFrames\": ").Append(sample.GameFrames)

                    .Append(", \"gameFramesPerWallSecond\": ").Append(FormatNumber(phaseFps))

                    .Append(", \"duringPlay\": ").Append(sample.DuringPlay ? "true" : "false")

                    .Append(" }")

                    .Append(i == _phaseSamples.Count - 1 ? "\n" : ",\n");

            }



            builder.Append("  ],\n");



            // The driver's per-phase ledger belongs in the artifact for the same reason the probe's own

            // phase table does: two runs of this route are only comparable if the budget each phase got

            // is recorded, and the console log for one run is 2 MB.

            builder.Append("  \"worldDriver\": {\n");

            AppendJsonBool(builder, "enabled", _worldDriverEnabled, "    ");

            AppendJsonBool(builder, "started", _worldDriverStarted, "    ");

            AppendJsonNumber(builder, "totalBudgetSeconds", H8_HeadlessWorldDriver.TotalBudgetSeconds, "    ");

            AppendJsonNumber(builder, "elapsedSeconds", H8_HeadlessWorldDriver.ElapsedSeconds, "    ");

            AppendJsonNumber(builder, "ticks", H8_HeadlessWorldDriver.TickCount, "    ");

            AppendJsonNumber(builder, "graceTicks", _worldDriverGraceTicks, "    ");

            AppendJsonNumber(builder, "graceTickCap", WorldDriverGraceTickCap, "    ");

            AppendJsonBool(builder, "compressed", H8_HeadlessWorldDriver.IsCompressed, "    ");

            AppendJsonField(builder, "stopCause", H8_HeadlessWorldDriver.StopCauseName, "    ");

            AppendJsonField(builder, "heaviestPhase", H8_HeadlessWorldDriver.WorstPhaseName, "    ");

            AppendJsonNumber(

                builder, "heaviestPhaseWallSeconds", H8_HeadlessWorldDriver.WorstPhaseWallSeconds, "    ");



            builder.Append("    \"phases\": [\n");

            bool firstDriverPhase = true;

            for (int phase = 0; phase < H8_HeadlessWorldDriver.PhaseLedgerCount; phase++)

            {

                if (!H8_HeadlessWorldDriver.WasPhaseEntered(phase))

                    continue;



                if (!firstDriverPhase)

                    builder.Append(",\n");



                firstDriverPhase = false;

                builder.Append("      { \"phase\": \"")

                    .Append(EscapeJson(H8_HeadlessWorldDriver.GetPhaseName(phase)))

                    .Append("\", \"wallSeconds\": ")

                    .Append(FormatNumber(H8_HeadlessWorldDriver.GetPhaseWallSeconds(phase)))

                    .Append(", \"grantedSeconds\": ")

                    .Append(FormatNumber(H8_HeadlessWorldDriver.GetPhaseGrantedSeconds(phase)))

                    .Append(", \"ticks\": ").Append(H8_HeadlessWorldDriver.GetPhaseTicks(phase))

                    .Append(", \"tickFloor\": ").Append(H8_HeadlessWorldDriver.GetPhaseMinimumTicks(phase))

                    .Append(", \"yield\": \"")

                    .Append(EscapeJson(H8_HeadlessWorldDriver.GetPhaseYieldName(phase)))

                    .Append("\" }");

            }



            builder.Append(firstDriverPhase ? "    ]\n" : "\n    ]\n");

            builder.Append("  },\n");



            // The state hash and the two discard counters go in the artifact for the same reason the phase

            // table does: comparing two runs by grepping two 2 MB logs is not a comparison anyone will

            // repeat, so nobody ever did it. Three fields carry the diff - masterStateHash,

            // lastCleanPostSimFrame and slowTickDiscardedSeconds - and coverage carries what they do not

            // cover, in the artifact itself, so a future reader cannot pick up the number without it.

            //

            // The hash is written as a hex STRING, and lo/hi are written separately as integers. A ulong

            // does not survive AppendJsonNumber: only the double overload accepts it, FormatNumber would

            // round it, and a rounded 64-bit hash silently compares equal to a different hash.

            builder.Append("  \"determinism\": {\n");

            AppendJsonField(builder, "state", _determinismState.ToString(), "    ");

            AppendJsonField(

                builder,

                "masterStateHash",

                "0x" + _determinismMasterHash.ToString("X16", CultureInfo.InvariantCulture),

                "    ");

            AppendJsonNumber(builder, "masterStateHashLo", (long)(uint)_determinismMasterHash, "    ");

            AppendJsonNumber(builder, "masterStateHashHi", (long)(uint)(_determinismMasterHash >> 32), "    ");

            AppendJsonField(

                builder,

                "lastCleanStateHash",

                "0x" + _determinismLastCleanHash.ToString("X16", CultureInfo.InvariantCulture),

                "    ");

            AppendJsonNumber(builder, "lastCleanPostSimFrame", _determinismLastCleanFrame, "    ");

            AppendJsonField(

                builder,

                "masterFlags",

                "0x" + _determinismMasterFlags.ToString("X8", CultureInfo.InvariantCulture),

                "    ");

            AppendJsonBool(

                builder, "missing", (_determinismMasterFlags & DeterminismArrayFlagMissing) != 0u, "    ");

            AppendJsonBool(

                builder, "truncated", (_determinismMasterFlags & DeterminismArrayFlagTruncated) != 0u, "    ");

            AppendJsonBool(

                builder, "nonFinite", (_determinismMasterFlags & DeterminismArrayFlagNonFinite) != 0u, "    ");

            AppendJsonBool(builder, "hashFromOwnerAccessor", _determinismHashFromOwnerAccessor, "    ");

            AppendJsonBool(

                builder, "ownerVaultDisagreement", _determinismAccessorVaultDisagreement, "    ");

            AppendJsonNumber(builder, "validatorInstances", _determinismValidatorInstances, "    ");

            AppendJsonNumber(builder, "validatorEnabled", _determinismValidatorEnabled, "    ");

            AppendJsonNumber(builder, "dispatcherFrameId", _determinismDispatcherFrameId, "    ");

            AppendJsonNumber(builder, "hashedElements", SumDeterminismHashedElements(), "    ");

            AppendJsonNumber(

                builder, "slowTickDiscardedSeconds", _determinismSlowTickDiscardedSeconds, "    ");

            AppendJsonNumber(builder, "slowTickDiscardEvents", _determinismSlowTickDiscardEvents, "    ");



            // The discard is promoted to a FIRST-CLASS caveat here, not left as two loose numbers. A reader

            // diffing two artifacts has to decide "are these two runs comparable at all" before deciding

            // "do their hashes match", and the second question is meaningless when the answer to the first

            // is no. runComparable is that gate in one field, and it is false for either reason: no hash was

            // sampled, or owed simulation time was dropped. The clamp itself is correct and stays.

            AppendJsonBool(

                builder,

                "slowTickDiscardObserved",

                _determinismSlowTickDiscardEvents > 0 || _determinismSlowTickDiscardedSeconds > 0.0,

                "    ");

            AppendJsonBool(

                builder,

                "runComparable",

                _determinismState == DeterminismCapture.Sampled &&

                    _determinismSlowTickDiscardEvents <= 0 &&

                    _determinismSlowTickDiscardedSeconds <= 0.0,

                "    ");

            AppendJsonField(builder, "comparabilityCaveat", DescribeDeterminismComparability(), "    ");



            // Owner identity, lifetime trace and the vault's refusal reason. These are what make the state

            // field actionable: "OwnerAbsentNoBuffer" says the owner is missing, and the trace says in which

            // window it went missing, which is the difference between one editor run and three.

            AppendJsonField(builder, "owner", "LockstepStateValidator", "    ");

            AppendJsonField(

                builder, "hashBufferDiagnosis", _determinismHashBufferDiagnosis ?? string.Empty, "    ");

            AppendJsonBool(

                builder, "vaultAllocationLocked", _determinismVaultAllocationLocked, "    ");

            AppendJsonBool(

                builder, "vaultCompactionFenceActive", _determinismVaultCompactionFenceActive, "    ");

            AppendJsonBool(builder, "ownerReviveRequested", _determinismReviveRequested, "    ");

            AppendJsonBool(builder, "ownerReviveCreated", _determinismReviveCreated, "    ");

            AppendJsonField(builder, "ownerReviveNote", _determinismReviveNote ?? string.Empty, "    ");

            AppendJsonField(

                builder,

                "ownerAtBootWarmup",

                DescribeOwnerSample(in _determinismOwnerAtBootWarmup),

                "    ");

            AppendJsonField(

                builder,

                "ownerAtFirstGameplayTick",

                DescribeOwnerSample(in _determinismOwnerAtGameplayStart),

                "    ");



            builder.Append("    \"categories\": [\n");

            for (int i = 0; i < _determinismCategories.Length; i++)

            {

                DeterminismCategorySample sample = _determinismCategories[i];

                builder.Append("      { \"category\": \"").Append(EscapeJson(sample.Name ?? string.Empty))

                    .Append("\", \"count\": ").Append(sample.Count.ToString(CultureInfo.InvariantCulture))

                    .Append(", \"hash\": \"0x")

                    .Append(sample.Hash.ToString("X8", CultureInfo.InvariantCulture))

                    .Append("\", \"flags\": \"0x")

                    .Append(sample.Flags.ToString("X8", CultureInfo.InvariantCulture))

                    .Append("\" }")

                    .Append(i == _determinismCategories.Length - 1 ? "\n" : ",\n");

            }



            // An empty array closes on its own line and is still valid JSON - no special case needed,

            // because the loop above emits the separator only between elements.

            builder.Append("    ],\n");

            AppendJsonFieldLast(builder, "coverage", DescribeDeterminismCoverage(), "    ");

            builder.Append("  },\n");



            builder.Append("  \"save\": {\n");

            AppendJsonField(builder, "root", _saveRoot, "    ");

            AppendJsonField(builder, "slot", _saveSlotName, "    ");

            AppendJsonBool(builder, "requested", _saveRequestIssued, "    ");

            AppendJsonBool(builder, "accepted", _saveAccepted, "    ");

            AppendJsonBool(builder, "busyObserved", _saveBusyObserved, "    ");

            AppendJsonNumber(builder, "waitedSeconds", _saveWaitedSeconds, "    ");

            AppendJsonNumber(builder, "filesBefore", _saveBefore.Length, "    ");

            AppendJsonNumber(builder, "filesAfter", _saveFilesAfter, "    ");

            AppendJsonNumber(builder, "added", _saveDiff.Added, "    ");

            AppendJsonNumber(builder, "removed", _saveDiff.Removed, "    ");

            AppendJsonNumber(builder, "changed", _saveDiff.Changed, "    ");

            AppendJsonNumber(builder, "byteDelta", _saveDiff.ByteDelta, "    ");

            AppendJsonField(builder, "diff", _saveDiff.Lines ?? string.Empty, "    ");

            AppendJsonFieldLast(builder, "note", _saveError ?? string.Empty, "    ");

            builder.Append("  },\n");



            builder.Append("  \"moments\": [\n");

            for (int i = 0; i < _routeMoments.Count; i++)

            {

                RouteMoment moment = _routeMoments[i];

                builder.Append("    { \"moment\": \"").Append(EscapeJson(moment.Name))

                    .Append("\", \"verdict\": \"").Append(DescribeVerdict(moment.Verdict))

                    .Append("\", \"detail\": \"").Append(EscapeJson(moment.Detail ?? string.Empty))

                    .Append("\" }")

                    .Append(i == _routeMoments.Count - 1 ? "\n" : ",\n");

            }



            builder.Append("  ],\n");



            builder.Append("  \"rejectedServices\": [");

            for (int i = 0; i < _rejectedServices.Count; i++)

            {

                if (i > 0)

                    builder.Append(", ");



                builder.Append('"').Append(EscapeJson(_rejectedServices[i])).Append('"');

            }



            builder.Append("]\n}\n");



            try

            {

                string directory = Path.GetDirectoryName(path);

                if (!string.IsNullOrEmpty(directory))

                    Directory.CreateDirectory(directory);



                File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));

                Debug.Log($"{Marker} ARTIFACT {path}");

            }

            catch (IOException ex)

            {

                Debug.Log($"{Marker} ARTIFACT FAILED {path}: {ex.GetType().Name}: {ex.Message}");

            }

            catch (UnauthorizedAccessException ex)

            {

                Debug.Log($"{Marker} ARTIFACT FAILED {path}: {ex.GetType().Name}: {ex.Message}");

            }

        }



        private static void AppendJsonField(StringBuilder builder, string name, string value, string indent = "  ")

        {

            builder.Append(indent).Append('"').Append(name).Append("\": \"")

                .Append(EscapeJson(value ?? string.Empty)).Append("\",\n");

        }



        private static void AppendJsonFieldLast(StringBuilder builder, string name, string value, string indent = "  ")

        {

            builder.Append(indent).Append('"').Append(name).Append("\": \"")

                .Append(EscapeJson(value ?? string.Empty)).Append("\"\n");

        }



        private static void AppendJsonNumber(StringBuilder builder, string name, double value, string indent = "  ")

        {

            builder.Append(indent).Append('"').Append(name).Append("\": ").Append(FormatNumber(value)).Append(",\n");

        }



        private static void AppendJsonNumber(StringBuilder builder, string name, long value, string indent = "  ")

        {

            builder.Append(indent).Append('"').Append(name).Append("\": ")

                .Append(value.ToString(CultureInfo.InvariantCulture)).Append(",\n");

        }



        private static void AppendJsonBool(StringBuilder builder, string name, bool value, string indent = "  ")

        {

            builder.Append(indent).Append('"').Append(name).Append("\": ").Append(value ? "true" : "false").Append(",\n");

        }



        private static void AppendJsonBoolLast(StringBuilder builder, string name, bool value, string indent = "  ")

        {

            builder.Append(indent).Append('"').Append(name).Append("\": ").Append(value ? "true" : "false").Append('\n');

        }



        private static string FormatNumber(double value)

        {

            return double.IsNaN(value) || double.IsInfinity(value)

                ? "0"

                : value.ToString("F3", CultureInfo.InvariantCulture);

        }



        private static string EscapeJson(string value)

        {

            if (string.IsNullOrEmpty(value))

                return string.Empty;



            var builder = new StringBuilder(value.Length + 16);

            for (int i = 0; i < value.Length; i++)

            {

                char c = value[i];

                switch (c)

                {

                    case '"':

                        builder.Append("\\\"");

                        break;

                    case '\\':

                        builder.Append("\\\\");

                        break;

                    case '\n':

                        builder.Append("\\n");

                        break;

                    case '\r':

                        builder.Append("\\r");

                        break;

                    case '\t':

                        builder.Append("\\t");

                        break;

                    default:

                        if (c < ' ')

                            builder.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));

                        else

                            builder.Append(c);



                        break;

                }

            }



            return builder.ToString();

        }



        private static string ReadStringArg(string flag, string fallback)

        {

            // Fully qualified: the project has its own Hecton8.Environment namespace, which wins

            // over System here and resolves to a type that has no GetCommandLineArgs.

            string[] args = System.Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; i++)

            {

                if (string.Equals(args[i], flag, StringComparison.Ordinal))

                    return args[i + 1];

            }



            return fallback;

        }



        private static int ReadIntArg(string flag, int fallback)

        {

            string raw = ReadStringArg(flag, null);

            return raw != null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)

                ? value

                : fallback;

        }

    }

}


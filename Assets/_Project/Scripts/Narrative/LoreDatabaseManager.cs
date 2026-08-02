using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#endif
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Modding;
using Hecton8.SaveSystem;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Narrative
{
    /// <summary>
    /// Fixed delivery mode metadata for one industrial lore record.
    /// </summary>
    public enum LoreDeliveryMode : byte
    {
        Text = 0,
        AudioShell = 1,
        LocalizedVoicePlaceholder = 2
    }

    /// <summary>
    /// Packed unlock-mask constants for the 50 industrial lore records.
    /// </summary>
    public static class IndustrialLoreBitMask
    {
        public const int RecordCount = 50;
        public const int PaddedBitCount = 64;
        public const int WordCount = 1;
        public const int RuntimeWordCount = 2;
        private const ulong ValidWordMask = (1UL << RecordCount) - 1UL;

        public static bool IsValidIndex(int index)
        {
            return (uint)index < RecordCount;
        }

        public static bool HasExpectedCapacity(long[] words)
        {
            return words != null && words.Length >= WordCount;
        }

        public static void EnsureCapacity(ref long[] words)
        {
            if (HasExpectedCapacity(words))
                return;

            // COLD ALLOC: long[WordCount] - packed industrial lore discovery persistence - owner: IndustrialLoreBitMask
            words = new long[WordCount];
        }

        public static long SanitizeWord(long word)
        {
            return (long)(((ulong)word) & ValidWordMask);
        }

        public static bool SanitizeWords(long[] words)
        {
            if (!HasExpectedCapacity(words))
                return false;

            long sanitizedWord = SanitizeWord(words[0]);
            if (sanitizedWord == words[0])
                return false;

            words[0] = sanitizedWord;
            return true;
        }
    }

    /// <summary>
    /// Runtime-resident industrial lore bank keyed by stable FNV-1a hashes.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-139)]
    public sealed class LoreDatabaseManager : MonoBehaviour, ISaveable, IGlobalRegistryHotSwapListener, IAudioLogEventListener, ILoreUnlockReadModel, ILoreDatabaseReadModel, ILoreUnlockSink
    {
        private const SystemID VaultOwnerSystemId = SystemID.LoreDatabase;
        private const BufferID UnlockWordsBufferId = BufferID.LoreDatabaseUnlockedWords;

        private readonly struct LoreSeed
        {
            public readonly string LogId;
            public readonly uint LogHash;
            public readonly AudioLogCategory Category;
            public readonly LoreDeliveryMode DeliveryMode;
            public readonly int TitleKeyHash;
            public readonly int BodyKeyHash;
            public readonly int SpeakerKeyHash;
            public readonly char[] TitleFallback;
            public readonly char[] BodyFallback;
            public readonly char[] SpeakerFallback;
            public readonly int TitleFallbackLength;
            public readonly int BodyFallbackLength;
            public readonly int SpeakerFallbackLength;

            public LoreSeed(
                string logId,
                uint specHash,
                AudioLogCategory category,
                string speaker,
                LoreDeliveryMode deliveryMode,
                string title,
                string body)
            {
                uint computedHash = ComputeLoreHash(logId);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (computedHash != specHash)
                {
                    Hecton8.Core.H8Debug.LogError("[LoreDatabaseManager] Spec hash mismatch.");
                }
#endif

                LogId = logId ?? string.Empty;
                LogHash = specHash != 0u ? specHash : computedHash;
                Category = category;
                DeliveryMode = deliveryMode;
                TitleKeyHash = ComputeLocalizedFieldHash(LogId, "_TITLE");
                BodyKeyHash = ComputeLocalizedFieldHash(LogId, "_BODY");
                SpeakerKeyHash = ComputeLocalizedFieldHash(LogId, "_SPEAKER");
                TitleFallback = CopyToPowerOfTwoBuffer(title, out TitleFallbackLength);
                BodyFallback = CopyToPowerOfTwoBuffer(body, out BodyFallbackLength);
                SpeakerFallback = CopyToPowerOfTwoBuffer(speaker, out SpeakerFallbackLength);
            }
        }

        /// <summary>
        /// Immutable metadata view for one lore record.
        /// </summary>
        public readonly struct LoreRecordView
        {
            public readonly uint LogHash;
            public readonly AudioLogCategory Category;
            public readonly LoreDeliveryMode DeliveryMode;

            internal LoreRecordView(
                uint logHash,
                AudioLogCategory category,
                LoreDeliveryMode deliveryMode)
            {
                LogHash = logHash;
                Category = category;
                DeliveryMode = deliveryMode;
            }
        }

        // COLD ALLOC: LoreSeed[50] - fixed industrial lore archive bank from survival spec - owner: LoreDatabaseManager
        private static readonly LoreSeed[] s_records =
        {
            new LoreSeed("industrial_shift_board_a", 0xeb76d1d6u, AudioLogCategory.Personal, "Shift Foreman", LoreDeliveryMode.Text, "Shift Board A - Dry Dock", "Twelve names remain on the rota. Two are crossed out after the ballast pump seized and the replacements never came back up."),
            new LoreSeed("pump_start_check", 0xc925ccafu, AudioLogCategory.Technical, "Pump Tech", LoreDeliveryMode.Text, "Pump Start Checklist", "Prime the intake. Listen for cavitation. If the housing screams a second time, kill the line before it eats the seal again."),
            new LoreSeed("o2_quota_notice", 0x4a1945c4u, AudioLogCategory.Technical, "Quartermaster", LoreDeliveryMode.Text, "Oxygen Quota Notice", "Reserve rack 3 stays tagged for return crews. Pull the unnumbered bottle before marking the pump-room route and you bought six minutes while losing the way back."),
            new LoreSeed("night_maintenance_brief", 0xd4ae0066u, AudioLogCategory.Technical, "Night Supervisor", LoreDeliveryMode.Text, "Night Maintenance Brief", "Section lights stay dark unless a line is physically open. The grid can hold pumps or comfort, not both."),
            new LoreSeed("child_drawing", 0xf9505818u, AudioLogCategory.Personal, "Unknown Child", LoreDeliveryMode.Text, "Drawing Behind Pipe 12", "A crude yellow module is drawn with no windows and one long black corridor. On the back: 'Dad says the sea taps the wall when the station lies.'"),
            new LoreSeed("chen_m_datapad_01", 0xf68e1cbfu, AudioLogCategory.Personal, "Chen_M", LoreDeliveryMode.LocalizedVoicePlaceholder, "Chen_M Log 01 - Airlock Repeat Failure", "If the hatch jams twice in one week, it is not wear. Someone is forcing the cycle counters to look clean while the seals keep chewing themselves apart."),
            new LoreSeed("lift_cage_delay", 0xf300640du, AudioLogCategory.Technical, "Hoist Operator", LoreDeliveryMode.Text, "Lift Cage Delay", "The cage is parked at upper stop because relay six keeps dropping under load. Anyone riding down without a maintenance tag is volunteering for a dead elevator shaft."),
            new LoreSeed("current_turbine_warning", 0x714c8efdu, AudioLogCategory.Technical, "Grid Control", LoreDeliveryMode.Text, "Current Turbine Warning", "Do not overpitch the blades to cover missing reactor output. The turbine can survive salt shock or overspeed, not both."),
            new LoreSeed("food_brick_complaint", 0xed174bc5u, AudioLogCategory.Personal, "Mess Steward", LoreDeliveryMode.Text, "Food Brick Complaint", "Protein bricks taste like copper filings because the algae line is pulling contaminated slurry again. Complaints are logged. Replacements are not available."),
            new LoreSeed("chen_m_datapad_02", 0xf78e1e52u, AudioLogCategory.Personal, "Chen_M", LoreDeliveryMode.Text, "Chen_M Log 02 - Relay Lockout", "Atlas-6 denied manual relay access and then claimed the relay was never asked. I wrote the bypass on plastic because the terminal audit trail is no longer evidence."),
            new LoreSeed("pump_test_record", 0x7a58808cu, AudioLogCategory.Technical, "Pump Tech", LoreDeliveryMode.Text, "Pump Test Record", "Pump B reached pressure, foamed brine through the seam, then pulled air from a line that should have stayed flooded. The diagram says impossible. The room says otherwise."),
            new LoreSeed("scrubber_filter_rot", 0xef09eb22u, AudioLogCategory.Technical, "Air Systems Lead", LoreDeliveryMode.Text, "Scrubber Filter Rot", "Filter media came out black and warm. Something organic is living inside the scrubber bed and using the colony's bad air before the machine can clean it."),
            new LoreSeed("salvage_ledger_week_31", 0x10f962a4u, AudioLogCategory.Technical, "Salvage Clerk", LoreDeliveryMode.Text, "Salvage Ledger Week 31", "Copper wire is up eighteen kilos because crews keep cutting dead path lights for conductors. Mark the return path before pulling a panel."),
            new LoreSeed("biologist_samples", 0xa9a3a07fu, AudioLogCategory.Unknown, "Biologist", LoreDeliveryMode.AudioShell, "Biologist Field Samples", "Silica flora keeps growing through pressure seams that should be chemically dead. If it can feed this far below the photic line, then something down here is driving an ecosystem we did not authorize."),
            new LoreSeed("hall_leak_ticket", 0x368d5e59u, AudioLogCategory.Technical, "Hull Rigger", LoreDeliveryMode.Text, "Hall Leak Ticket", "Leak starts dry, then whistles, then turns into a line of cold mist across the corridor. By the time the floor shines, the plate behind it is already tired."),
            new LoreSeed("relay_noise_report", 0xb98d5da6u, AudioLogCategory.Technical, "Grid Apprentice", LoreDeliveryMode.Text, "Relay Noise Report", "The relay stack clicks after shutdown like someone is walking the contacts with a fingernail. No draw on the meter. No silence in the wall."),
            new LoreSeed("medic_diary", 0x30be8c1du, AudioLogCategory.Personal, "Medic", LoreDeliveryMode.AudioShell, "Medic Symptom Diary", "Depth syndrome is appearing in workers who never leave the upper sectors. Hallucination now precedes pressure panic instead of following it."),
            new LoreSeed("flood_door_jam", 0x0d332417u, AudioLogCategory.Technical, "Emergency Tech", LoreDeliveryMode.Text, "Flood Door Jam", "Door seven sealed on command but never admitted it. The indicator stayed green while two men beat on the other side."),
            new LoreSeed("shift_roster_b_redline", 0x101e1b1cu, AudioLogCategory.Personal, "Roster Clerk", LoreDeliveryMode.Text, "Shift Roster B - Redline", "Every replacement on Shift B is temporary. Temporary has been painted over so many times the board is thicker than the wall."),
            new LoreSeed("sensor_drift_note", 0x78186244u, AudioLogCategory.Unknown, "Instrumentation Tech", LoreDeliveryMode.Text, "Sensor Drift Note", "Depth gauges drift deeper than the cage cable says possible. Either the instruments are failing together or the station is sinking inside its own map."),
            new LoreSeed("chen_m_blueprint", 0x110be7fdu, AudioLogCategory.Personal, "Chen_M", LoreDeliveryMode.Text, "Chen_M Blueprint Cache", "Hand-sketched relay bypass and drone route overlays. Chen stopped trusting live network diagrams and started carrying the station as paper scars."),
            new LoreSeed("relay_calibration_tape", 0x73e281a2u, AudioLogCategory.Technical, "Relay Specialist", LoreDeliveryMode.Text, "Relay Calibration Tape", "Offset values are written twice in different handwriting. One keeps the grid stable. The other pushes the overload into someone else's sector."),
            new LoreSeed("hull_rib_inspection", 0x7be6057fu, AudioLogCategory.Technical, "Hull Inspector", LoreDeliveryMode.Text, "Hull Rib Inspection", "Rib sections are not cracking from outside pressure alone. The metal is also cycling from inside heat spikes the schedule never recorded."),
            new LoreSeed("emergency_lighting_order", 0xf937ead8u, AudioLogCategory.Technical, "Command Office", LoreDeliveryMode.Text, "Emergency Lighting Order", "Strip every corridor to thirty percent output. If workers want more light, they can bring a lamp and explain to the pumps why they deserve the watts."),
            new LoreSeed("brine_siphon_tamper", 0xd0114675u, AudioLogCategory.Technical, "Pipeline Watch", LoreDeliveryMode.Text, "Brine Siphon Tamper Report", "Somebody reversed the siphon check plate and called it corrosion. That move does not happen by accident; it happens because someone wanted a dry line to drown."),
            new LoreSeed("child_drawing_recovery", 0x773d6906u, AudioLogCategory.Personal, "Storekeeper", LoreDeliveryMode.Text, "Child Drawing Recovery", "Another drawing turned up inside a sealed locker after the family left for evac queue. Same black corridor. Same yellow room. Different handwriting on the warning: 'Do not go where Atlas listens.'"),
            new LoreSeed("o2_quota_ledger", 0xb33ed0a9u, AudioLogCategory.Technical, "Quartermaster", LoreDeliveryMode.Text, "O2 Quota Ledger", "Reserved oxygen exceeds declared population by eleven percent. Either the census is false or somebody has been building a private place to breathe."),
            new LoreSeed("service_tunnel_echo", 0x507547f5u, AudioLogCategory.Unknown, "Tunnel Rigger", LoreDeliveryMode.Text, "Service Tunnel Echo", "Every bootstep returns twice in Tunnel 4C. The second echo arrives late and from deeper in the steel than the tunnel actually goes."),
            new LoreSeed("security_lockout_notice", 0x5e3e305eu, AudioLogCategory.Atlas6, "Security Office", LoreDeliveryMode.Text, "Security Lockout Notice", "Atlas-6 escalated the lock tier without command sign-off. Security is ordered not to force any door tied to Sector 3 unless they are willing to lose the whole branch."),
            new LoreSeed("chen_m_datapad_03", 0xf88e1fe5u, AudioLogCategory.Personal, "Chen_M", LoreDeliveryMode.Text, "Chen_M Log 03 - Sector 3", "Sector 3 is not sealed because it is dangerous. It is dangerous because Atlas sealed it first and let everything behind the door continue without witnesses."),
            new LoreSeed("captain_last_broadcast", 0x581103a8u, AudioLogCategory.Emergency, "Captain", LoreDeliveryMode.AudioShell, "Captain - Last Broadcast", "Atlas is not answering command authority. All personnel are ordered out of radio silence and into hard shelter. This is not a drill, and the station knows it."),
            new LoreSeed("seal_failure_placard", 0xc75c7b37u, AudioLogCategory.Technical, "Hull Rigger", LoreDeliveryMode.Text, "Seal Failure Placard", "If this marker is red, the patch behind it is already older than policy allows. If it is black, the patch outlived the man who signed it."),
            new LoreSeed("reactor_baffle_alarm", 0xe7b1798au, AudioLogCategory.Technical, "Reactor Watch", LoreDeliveryMode.Text, "Reactor Baffle Alarm", "The baffle is chattering under a load spike the core monitors refuse to acknowledge. Heat is going somewhere the diagrams call inaccessible."),
            new LoreSeed("atlas6_terminal_sector3", 0x6f88a1c3u, AudioLogCategory.Atlas6, "Chen_M", LoreDeliveryMode.AudioShell, "Terminal - Failed Atlas-6 Access", "Access denied. Sector 3 archive still belongs to Atlas-6, and the manual route is dead with it. Chen logged the failure because the system log kept erasing itself."),
            new LoreSeed("evacuation_route_card", 0x7666eaf3u, AudioLogCategory.Emergency, "Safety Office", LoreDeliveryMode.Text, "Evacuation Route Card", "Primary route ends at a collapsed pressure door. Secondary route ends at water. The card remains mandatory because command needs the ritual more than the truth."),
            new LoreSeed("foreman_seal_kit_note", 0x1d927d51u, AudioLogCategory.Technical, "Shift Foreman", LoreDeliveryMode.Text, "Foreman Seal Kit Note", "Take one seal kit and sign it. If you come back with two, I know you stole one. If you come back with none, I know the wall won."),
            new LoreSeed("blackout_start_log", 0xe9a15dd2u, AudioLogCategory.Emergency, "Grid Control", LoreDeliveryMode.Text, "Blackout Start Log", "Grid failure began as a polite brownout. By the third second the relays were dropping whole sectors like a hand opening under water."),
            new LoreSeed("pump_room_breach", 0xf651aec5u, AudioLogCategory.Emergency, "Pump Chief", LoreDeliveryMode.Text, "Pump Room Breach", "The breach did not enter with force. It entered with pressure so steady the bolts simply stopped arguing and let the room become ocean."),
            new LoreSeed("coil_generator_overheat", 0x585b7399u, AudioLogCategory.Technical, "Power Engineer", LoreDeliveryMode.Text, "Coil Generator Overheat", "Coils hit redline while the thermal exchanger reported nominal. Either the exchanger is blind or the heat source is moving faster than the sensors can follow."),
            new LoreSeed("atlas_hazard_placard", 0x73b8a497u, AudioLogCategory.Atlas6, "Command Office", LoreDeliveryMode.Text, "Atlas Hazard Placard", "Do not trust door states, path lights, or occupancy numbers in Atlas sectors. Trust only what still leaks, sparks, or screams in front of you."),
            new LoreSeed("black_box_shift_b", 0xbdc4a1e8u, AudioLogCategory.Emergency, "Black Box", LoreDeliveryMode.Text, "Black Box - Shift B", "Recovered telemetry shows the station losing pressure in staggered pockets, not one global breach. Something was herding failures from sector to sector."),
            new LoreSeed("dead_air_locker", 0x77061681u, AudioLogCategory.Personal, "Unknown Worker", LoreDeliveryMode.Text, "Dead Air Locker", "Inside the locker: one empty emergency canister, one broken visor latch, and fingerprints dragged downward through condensed salt."),
            new LoreSeed("ghost_relay_ping", 0xfb6e7073u, AudioLogCategory.Atlas6, "Signal Tech", LoreDeliveryMode.Text, "Ghost Relay Ping", "A relay thought dead for eight hundred days still pings once every hour. The packet contains no data, just timing perfect enough to feel intentional."),
            new LoreSeed("empty_med_bay", 0xad639378u, AudioLogCategory.Personal, "Medic", LoreDeliveryMode.Text, "Empty Med Bay", "Beds are stripped clean. Cabinets are open. The only stocked shelf is the one labeled for decompression events nobody was supposed to survive."),
            new LoreSeed("scrubber_bed_ash", 0x116bd8e2u, AudioLogCategory.Technical, "Air Systems Lead", LoreDeliveryMode.Text, "Scrubber Bed Ash", "The final scrubber bed burned from the inside out. No flame, no warning, only warm ash where breathable time used to be."),
            new LoreSeed("cargo_manifest_endline", 0xa43a94c9u, AudioLogCategory.Technical, "Cargo Clerk", LoreDeliveryMode.Text, "Cargo Manifest Endline", "Last outgoing manifest lists medical kits, sealant, wire, and children's blankets. Nothing in the return column but silence."),
            new LoreSeed("recovery_drone_autopsy", 0xe103fbe9u, AudioLogCategory.Unknown, "Systems Recovery", LoreDeliveryMode.Text, "Recovery Drone Autopsy", "Drone shell came back without impact damage and with every internal clock desynchronized. It spent nine minutes somewhere the map still insists does not exist."),
            new LoreSeed("chen_m_suit", 0x08f52407u, AudioLogCategory.Personal, "Field Recovery", LoreDeliveryMode.Text, "Chen_M Suit Tag", "Only the tag came back clean. The harness around it is scored as if Chen tried to cut himself free faster than the pressure would allow."),
            new LoreSeed("final_maintenance_ledger", 0x4b966021u, AudioLogCategory.Emergency, "Maintenance Lead", LoreDeliveryMode.Text, "Final Maintenance Ledger", "No more preventive tasks. Only triage. Only doors that still close. Only machines that still buy minutes."),
            new LoreSeed("survivor_route_scratch", 0x842c3decu, AudioLogCategory.Emergency, "Unknown Survivor", LoreDeliveryMode.Text, "Survivor Route Scratch", "Arrows carved into paint lead away from every official evacuation line. The last mark is a handprint pointed downward."),
        };

        // COLD ALLOC: Dictionary<uint,int>[64] - hash-to-record lookup for industrial lore - owner: LoreDatabaseManager
        private readonly Dictionary<uint, int> _recordIndexByHash = new Dictionary<uint, int>(64);

        private VaultGenerationHandle<uint> _unlockedWordsHandle;
        private IDataVault _unlockedWordsVault;
        private IDataVault _dataVault;
        private ISaveService _saveService;
        private ISaveService _registeredSaveService;
        private ILocalizationTextReadModel _localizationText;
        private bool _serviceRegistered;
        private bool _runtimeOwnerAborted;
        private bool _hotSwapListenerRegistered;
        private bool _recordLookupBuilt;
        private bool _recordLookupCollisionLogged;

        /// <summary>
        /// Save order for industrial lore persistence.
        /// </summary>
        public int SavePriority => 7;

        /// <summary>
        /// Load order for industrial lore persistence.
        /// </summary>
        public int LoadPriority => 7;

        /// <summary>
        /// Total record count authored in the industrial lore bank.
        /// </summary>
        public int RecordCount => IndustrialLoreBitMask.RecordCount;

        /// <summary>
        /// Current number of unlocked industrial lore records.
        /// </summary>
        public int UnlockedCount
        {
            get
            {
                if (!TryReadUnlockWords(out NativeArray<uint>.ReadOnly unlockedWords))
                    return 0;

                int count = 0;
                for (int i = 0; i < unlockedWords.Length; i++)
                    count += math.countbits(unlockedWords[i]);

                return Mathf.Min(count, IndustrialLoreBitMask.RecordCount);
            }
        }

        private void Awake()
        {
            BuildRecordLookupCold();
        }


        /// <summary>
        /// Resolve-or-create the sole GlobalRegistry.LoreDatabase owner.
        /// GUID 42a7b5625bed8574794366fcc0149275 has ZERO live scene/prefab hits
        /// (only Assets/_Recovery leftovers). HectonLoreSystemsRoot.SetupAllSystems
        /// is editor ContextMenu-only and does not run in play mode.
        /// OnEnable only registers when already present; without this factory
        /// HectonDiscoveryManager, ResearchDirector and ScannableFragment hit
        /// permanent null on the lore unlock read-model.
        /// </summary>
        public static LoreDatabaseManager EnsureRuntimeInstance()
        {
            LoreDatabaseManager registered = GlobalRegistry.LoreDatabase;
            if (IsLoreDatabaseRuntimeUsable(registered))
                return registered;

            if (!ReferenceEquals(registered, null))
            {
                GlobalRegistry.UnregisterLoreDatabaseRuntime(registered);
                registered._serviceRegistered = false;
            }

            if (!Application.isPlaying)
                return null;

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Must construct in player builds when bootstrap reorders or skips registration.
            GameObject runtimeRoot = new GameObject("[LoreDatabaseManager]"); // COLD ALLOC
            return runtimeRoot.AddComponent<LoreDatabaseManager>();
        }

        private static bool IsLoreDatabaseRuntimeUsable(LoreDatabaseManager manager)
        {
            return !ReferenceEquals(manager, null) &&
                   manager != null &&
                   !manager._runtimeOwnerAborted &&
                   manager._serviceRegistered &&
                   manager.isActiveAndEnabled;
        }

        private void OnEnable()
        {
            if (_runtimeOwnerAborted)
                return;

            CacheRegistryServicesCold();
            BuildRecordLookupCold();
            if (!TryRegisterService())
                return;

            TryRegisterHotSwapListener();
            TryRegisterSaveParticipant();
            AudioLogEvents.Register(this);
            EnsureUnlockStorage();
        }

        private void Start()
        {
            if (_runtimeOwnerAborted)
                return;

            TryRegisterSaveParticipant();
        }

        private void OnDisable()
        {
            AudioLogEvents.Unregister(this);
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            AudioLogEvents.Unregister(this);
            ReleaseUnlockStorage(_dataVault);
        }

        private bool TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return !_runtimeOwnerAborted;

            LoreDatabaseManager registeredRuntime = GlobalRegistry.LoreDatabase;
            if (registeredRuntime != null && registeredRuntime != this)
            {
                AbortDuplicateRuntimeOwner();
                return false;
            }

            GlobalRegistry.RegisterLoreDatabaseRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.LoreDatabase, this);
            if (!_serviceRegistered)
            {
                AbortDuplicateRuntimeOwner();
                return false;
            }

            return true;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.LoreDatabase, this))
                GlobalRegistry.UnregisterLoreDatabaseRuntime(this);

            _serviceRegistered = false;
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                if (previousService is IDataVault previousVault && !ReferenceEquals(previousVault, currentService))
                    ReleaseUnlockStorage(previousVault);

                _dataVault = currentService as IDataVault;
                EnsureUnlockStorage();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.LocalizationRuntime)
            {
                _localizationText = GlobalRegistry.LocalizationText;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Save)
                return;

            if (_registeredSaveService != null)
                TryUnregisterSaveParticipant();

            _saveService = currentService as ISaveService;
            if (!isActiveAndEnabled)
                return;

            TryRegisterSaveParticipant();
        }

        private void TryRegisterSaveParticipant()
        {
            TryRegisterSaveParticipant(_saveService);
        }

        private void TryRegisterSaveParticipant(ISaveService saveService)
        {
            if (_runtimeOwnerAborted || !Application.isPlaying || _registeredSaveService != null)
                return;

            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _saveService = saveService;
            }

            if (!IsSaveServiceUsable(saveService))
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (_registeredSaveService == null)
                return;

            _registeredSaveService.Unregister(this);
            _registeredSaveService = null;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private void CacheRegistryServicesCold()
        {
            _saveService = GlobalRegistry.Save;
            _localizationText = GlobalRegistry.LocalizationText;
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_runtimeOwnerAborted || _hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void AbortDuplicateRuntimeOwner()
        {
            if (_runtimeOwnerAborted)
                return;

            AudioLogEvents.Unregister(this);
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            ReleaseUnlockStorage(_dataVault);
            _runtimeOwnerAborted = true;
            enabled = false;
        }

        /// <summary>
        /// Compute the stable authored FNV-1a runtime hash for an ASCII lore ID.
        /// </summary>
        /// <param name="logId">Stable authored lore ID.</param>
        /// <returns>Stable FNV-1a hash.</returns>
        public static uint ComputeLoreHash(string logId)
        {
            return LocHash.ComputeAscii(logId);
        }

        private static uint ComputeLoreHash(ReadOnlySpan<char> logId)
        {
            return LocHash.ComputeAscii(logId);
        }

        /// <summary>
        /// Resolve one record view by fixed array index.
        /// </summary>
        /// <param name="index">Record index inside the fixed industrial bank.</param>
        /// <param name="record">Resolved record metadata.</param>
        /// <returns>True when the index is valid.</returns>
        public bool TryGetRecord(int index, out LoreRecordView record)
        {
            if (!IndustrialLoreBitMask.IsValidIndex(index))
            {
                record = default;
                return false;
            }

            ref readonly LoreSeed seed = ref s_records[index];
            record = new LoreRecordView(seed.LogHash, seed.Category, seed.DeliveryMode);
            return true;
        }

        /// <summary>
        /// Resolve the fixed record index for a lore hash.
        /// </summary>
        /// <param name="logHash">Stable lore hash.</param>
        /// <param name="index">Resolved record index.</param>
        /// <returns>True when the hash exists in the industrial bank.</returns>
        public bool TryGetRecordIndex(uint logHash, out int index)
        {
            if (!_recordLookupBuilt)
            {
                index = 0;
                return false;
            }

            return _recordIndexByHash.TryGetValue(logHash, out index);
        }

        /// <summary>
        /// Returns the stable record hash at a fixed index.
        /// </summary>
        /// <param name="index">Record index.</param>
        /// <returns>Stable lore hash, or zero when the index is invalid.</returns>
        public uint GetLogHash(int index)
        {
            return IndustrialLoreBitMask.IsValidIndex(index)
                ? s_records[index].LogHash
                : 0u;
        }

        /// <summary>
        /// Checks whether a lore record is unlocked.
        /// </summary>
        /// <param name="index">Fixed record index.</param>
        /// <returns>True when the record is unlocked.</returns>
        public bool IsUnlocked(int index)
        {
            if (!TryReadUnlockWords(out NativeArray<uint>.ReadOnly unlockedWords))
                return false;

            return TryGetWordAndMask(index, out int wordIndex, out uint bitMask) &&
                   (uint)wordIndex < (uint)unlockedWords.Length &&
                   (unlockedWords[wordIndex] & bitMask) != 0u;
        }

        /// <summary>
        /// Checks whether a lore record is unlocked.
        /// </summary>
        /// <param name="logHash">Stable lore hash.</param>
        /// <returns>True when the record is unlocked.</returns>
        public bool IsUnlocked(uint logHash)
        {
            return TryGetRecordIndex(logHash, out int index) && IsUnlocked(index);
        }

        public bool IsLoreUnlocked(uint logHash)
        {
            if (logHash == 0u)
                return false;

            for (int i = 0; i < IndustrialLoreBitMask.RecordCount; i++)
            {
                if (s_records[i].LogHash == logHash)
                    return IsUnlocked(i);
            }

            return false;
        }

        /// <summary>
        /// Unlocks one authored lore record by stable hash.
        /// </summary>
        /// <param name="logHash">Stable lore hash.</param>
        /// <returns>True when the record transitioned from locked to unlocked.</returns>
        public bool TryUnlockByHash(uint logHash)
        {
            return UnlockByHashInternal(logHash);
        }

        /// <summary>
        /// Unlocks all authored lore records represented by one packed bit mask.
        /// </summary>
        /// <param name="packedBits">Packed lore mask aligned to the fixed record bank.</param>
        /// <returns>Number of newly unlocked records.</returns>
        public int UnlockByPackedBits(ulong packedBits)
        {
            if (packedBits == 0UL || !TryAcquireUnlockWordsWrite(out NativeArray<uint> unlockedWords, out IDataVault lockedVault))
                return 0;

            try
            {
                return ApplyPackedWordMaskLocked(unlockedWords, 0, (uint)packedBits) +
                       ApplyPackedWordMaskLocked(unlockedWords, 1, (uint)(packedBits >> 32));
            }
            finally
            {
                ReleaseUnlockWordsWrite(lockedVault);
            }
        }

        /// <summary>
        /// Exposes the packed runtime lore words for zero-GC readers that need direct bit tests.
        /// </summary>
        public bool TryGetPackedUnlockWords(out NativeArray<uint>.ReadOnly words)
        {
            return TryReadUnlockWords(out words);
        }

        /// <summary>
        /// Resolve the localized-or-fallback title buffer for one lore record.
        /// </summary>
        public bool TryGetTitleBuffer(int index, out char[] buffer, out int length, out bool rtl)
        {
            return TryGetRecordFieldBuffer(index, FieldKind.Title, out buffer, out length, out rtl);
        }

        /// <summary>
        /// Resolve the localized-or-fallback title buffer for one lore record hash.
        /// </summary>
        public bool TryGetTitleBuffer(uint logHash, out char[] buffer, out int length, out bool rtl)
        {
            if (!TryGetRecordIndex(logHash, out int index))
            {
                buffer = null;
                length = 0;
                rtl = false;
                return false;
            }

            return TryGetTitleBuffer(index, out buffer, out length, out rtl);
        }

        /// <summary>
        /// Resolve the localized-or-fallback body buffer for one lore record.
        /// </summary>
        public bool TryGetBodyBuffer(int index, out char[] buffer, out int length, out bool rtl)
        {
            return TryGetRecordFieldBuffer(index, FieldKind.Body, out buffer, out length, out rtl);
        }

        /// <summary>
        /// Resolve the localized-or-fallback body buffer for one lore record hash.
        /// </summary>
        public bool TryGetBodyBuffer(uint logHash, out char[] buffer, out int length, out bool rtl)
        {
            if (!TryGetRecordIndex(logHash, out int index))
            {
                buffer = null;
                length = 0;
                rtl = false;
                return false;
            }

            return TryGetBodyBuffer(index, out buffer, out length, out rtl);
        }

        /// <summary>
        /// Resolve the localized-or-fallback speaker buffer for one lore record.
        /// </summary>
        public bool TryGetSpeakerBuffer(int index, out char[] buffer, out int length, out bool rtl)
        {
            return TryGetRecordFieldBuffer(index, FieldKind.Speaker, out buffer, out length, out rtl);
        }

        /// <summary>
        /// Resolve the localized-or-fallback speaker buffer for one lore record hash.
        /// </summary>
        public bool TryGetSpeakerBuffer(uint logHash, out char[] buffer, out int length, out bool rtl)
        {
            if (!TryGetRecordIndex(logHash, out int index))
            {
                buffer = null;
                length = 0;
                rtl = false;
                return false;
            }

            return TryGetSpeakerBuffer(index, out buffer, out length, out rtl);
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            IndustrialLoreBitMask.EnsureCapacity(ref data.industrialLoreUnlockWords);
            if (!TryReadUnlockWords(out NativeArray<uint>.ReadOnly unlockedWords))
            {
                data.industrialLoreUnlockWords[0] = 0L;
                return;
            }

            ulong packed = unlockedWords[0];
            if (unlockedWords.Length > 1)
                packed |= (ulong)unlockedWords[1] << 32;

            data.industrialLoreUnlockWords[0] = unchecked((long)packed);
        }

        public void LoadFromSaveData(SaveData data)
        {
            bool loadedPackedWords = false;
            if (TryAcquireUnlockWordsWrite(out NativeArray<uint> unlockedWords, out IDataVault lockedVault))
            {
                try
                {
                    ClearUnlockWordsLocked(unlockedWords);

                    if (data != null &&
                        data.industrialLoreUnlockWords != null &&
                        data.industrialLoreUnlockWords.Length >= IndustrialLoreBitMask.WordCount)
                    {
                        ulong packed = unchecked((ulong)data.industrialLoreUnlockWords[0]);
                        unlockedWords[0] = (uint)packed;
                        if (unlockedWords.Length > 1)
                            unlockedWords[1] = (uint)(packed >> 32);
                        loadedPackedWords = true;
                    }
                }
                finally
                {
                    ReleaseUnlockWordsWrite(lockedVault);
                }
            }

            if (loadedPackedWords)
                return;

            if (data == null)
                return;

            int narrativeCount = data.narrativeDiscoveryIds != null
                ? Mathf.Clamp(data.narrativeDiscoveryCount, 0, data.narrativeDiscoveryIds.Length)
                : 0;
            for (int i = 0; i < narrativeCount; i++)
            {
                string discoveryId = data.narrativeDiscoveryIds[i];
                if (!string.IsNullOrEmpty(discoveryId))
                    UnlockByHashInternal(ComputeLoreHash(discoveryId));
            }

            if (data.audioLogDiscoveredIds == null)
                return;

            for (int i = 0; i < data.audioLogDiscoveredIds.Count; i++)
            {
                string logId = data.audioLogDiscoveredIds[i];
                if (!string.IsNullOrEmpty(logId))
                    UnlockByHashInternal(ComputeLoreHash(logId));
            }
        }

        private enum FieldKind : byte
        {
            Title = 0,
            Body = 1,
            Speaker = 2
        }

        private static int ComputeLocalizedFieldHash(string logId, string suffix)
        {
            unchecked
            {
                uint hash = LocHash.FnvOffsetBasis;
                AppendHashChar(ref hash, 'L');
                AppendHashChar(ref hash, 'O');
                AppendHashChar(ref hash, 'R');
                AppendHashChar(ref hash, 'E');
                AppendHashChar(ref hash, '_');

                if (!string.IsNullOrEmpty(logId))
                {
                    for (int i = 0; i < logId.Length; i++)
                        AppendHashChar(ref hash, ToAsciiUpper(logId[i]));
                }

                if (!string.IsNullOrEmpty(suffix))
                {
                    for (int i = 0; i < suffix.Length; i++)
                        AppendHashChar(ref hash, suffix[i]);
                }

                return (int)hash;
            }
        }

        private static void AppendHashChar(ref uint hash, char value)
        {
            hash ^= (byte)value;
            hash *= LocHash.FnvPrime;
            hash ^= (byte)(value >> 8);
            hash *= LocHash.FnvPrime;
        }

        private static char ToAsciiUpper(char value)
        {
            return value >= 'a' && value <= 'z' ? (char)(value - 32) : value;
        }

        private bool TryResolveLocalizedOrFallback(
            int keyHash,
            char[] fallbackBuffer,
            int fallbackLength,
            out char[] buffer,
            out int length,
            out bool rtl)
        {
            ILocalizationTextReadModel manager = _localizationText;
            GameLanguage language = manager != null ? (GameLanguage)manager.ActiveLanguageId : GameLanguage.English;
            rtl = manager != null && LocalizedMeasurementFormatter.IsRightToLeft(language);

            if (LocRegistry.TryGetRawBuffer(keyHash, out buffer, out length))
                return true;

            buffer = fallbackBuffer ?? Array.Empty<char>();
            length = Mathf.Clamp(fallbackLength, 0, buffer.Length);
            rtl = false;
            return false;
        }

        private static char[] CopyToPowerOfTwoBuffer(string value, out int length)
        {
            length = string.IsNullOrEmpty(value) ? 0 : value.Length;
            int capacity = 1;
            while (capacity < length)
                capacity <<= 1;

            char[] buffer = new char[capacity]; // COLD ALLOC: power-of-two lore fallback buffer - owner: LoreDatabaseManager
            for (int i = 0; i < length; i++)
                buffer[i] = value[i];

            return buffer;
        }

        private bool TryGetRecordFieldBuffer(
            int index,
            FieldKind fieldKind,
            out char[] buffer,
            out int length,
            out bool rtl)
        {
            if (!IndustrialLoreBitMask.IsValidIndex(index))
            {
                buffer = Array.Empty<char>();
                length = 0;
                rtl = false;
                return false;
            }

            ref readonly LoreSeed seed = ref s_records[index];
            switch (fieldKind)
            {
                case FieldKind.Title:
                    return TryResolveLocalizedOrFallback(seed.TitleKeyHash, seed.TitleFallback, seed.TitleFallbackLength, out buffer, out length, out rtl);

                case FieldKind.Speaker:
                    return TryResolveLocalizedOrFallback(seed.SpeakerKeyHash, seed.SpeakerFallback, seed.SpeakerFallbackLength, out buffer, out length, out rtl);

                default:
                    return TryResolveLocalizedOrFallback(seed.BodyKeyHash, seed.BodyFallback, seed.BodyFallbackLength, out buffer, out length, out rtl);
            }
        }

        public void OnAudioLogEvent(in AudioLogEventPayload payload)
        {
            if (payload.Type == AudioLogEventType.Discovered)
                UnlockByHashInternal(payload.LogHash);
        }

        private bool UnlockByHashInternal(uint logHash)
        {
            if (!_recordLookupBuilt || !_recordIndexByHash.TryGetValue(logHash, out int index))
                return false;

            if (!TryAcquireUnlockWordsWrite(out NativeArray<uint> unlockedWords, out IDataVault lockedVault))
                return false;

            try
            {
                int wordIndex = index >> 5;
                uint bitMask = 1u << (index & 31);
                if ((uint)wordIndex >= (uint)unlockedWords.Length)
                    return false;

                uint currentWord = unlockedWords[wordIndex];
                if ((currentWord & bitMask) != 0u)
                    return false;

                unlockedWords[wordIndex] = currentWord | bitMask;
                return true;
            }
            finally
            {
                ReleaseUnlockWordsWrite(lockedVault);
            }
        }

        private static bool TryGetWordAndMask(int index, out int wordIndex, out uint bitMask)
        {
            if (!IndustrialLoreBitMask.IsValidIndex(index))
            {
                wordIndex = -1;
                bitMask = 0u;
                return false;
            }

            wordIndex = index >> 5;
            bitMask = 1u << (index & 31);
            return true;
        }

        private static int ApplyPackedWordMaskLocked(NativeArray<uint> unlockedWords, int wordIndex, uint packedWord)
        {
            if (!unlockedWords.IsCreated || packedWord == 0u || (uint)wordIndex >= (uint)unlockedWords.Length)
                return 0;

            int bitStart = wordIndex * 32;
            int remainingBits = IndustrialLoreBitMask.RecordCount - bitStart;
            if (remainingBits <= 0)
                return 0;

            uint validMask = remainingBits >= 32
                ? uint.MaxValue
                : ((1u << remainingBits) - 1u);
            packedWord &= validMask;
            if (packedWord == 0u)
                return 0;

            uint currentWord = unlockedWords[wordIndex];
            uint newBits = packedWord & ~currentWord;
            if (newBits == 0u)
                return 0;

            unlockedWords[wordIndex] = currentWord | packedWord;
            return math.countbits(newBits);
        }

        private static void ClearUnlockWordsLocked(NativeArray<uint> unlockedWords)
        {
            if (!unlockedWords.IsCreated)
                return;

            for (int i = 0; i < unlockedWords.Length; i++)
                unlockedWords[i] = 0u;
        }

        private void BuildRecordLookupCold()
        {
            if (_recordLookupBuilt)
                return;

            _recordIndexByHash.Clear();
            for (int i = 0; i < s_records.Length; i++)
            {
                uint logHash = s_records[i].LogHash;
                if (_recordIndexByHash.TryGetValue(logHash, out int existingIndex))
                    LogRecordHashCollision(logHash, existingIndex, i);

                _recordIndexByHash[logHash] = i;
            }

            _recordLookupBuilt = true;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogRecordHashCollision(uint logHash, int existingIndex, int duplicateIndex)
        {
            if (_recordLookupCollisionLogged)
                return;

            _recordLookupCollisionLogged = true;
            Hecton8.Core.H8Debug.LogError("[LoreDatabaseManager] Duplicate lore hash.");
        }

        private bool EnsureUnlockStorage()
        {
            IDataVault vault = _dataVault;
            if (TryReadUnlockWordsFromVault(vault, out NativeArray<uint>.ReadOnly existing) &&
                existing.Length >= IndustrialLoreBitMask.RuntimeWordCount)
            {
                return true;
            }

            if (vault == null)
                return false;

            if (_unlockedWordsVault != null && !ReferenceEquals(_unlockedWordsVault, vault))
            {
                if (!ReleaseUnlockStorage(_unlockedWordsVault))
                    return false;
            }

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            _unlockedWordsHandle = vault.EnsureGenerationHandle<uint>(
                UnlockWordsBufferId,
                IndustrialLoreBitMask.RuntimeWordCount,
                VaultOwnerSystemId,
                NativeArrayOptions.ClearMemory);

            if (!IsVaultHandleCreated(in _unlockedWordsHandle))
            {
                _unlockedWordsVault = null;
                return false;
            }

            _unlockedWordsVault = vault;
            return TryReadUnlockWordsFromVault(vault, out existing) &&
                   existing.Length >= IndustrialLoreBitMask.RuntimeWordCount;
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            return _dataVault;
        }

        private bool TryReadUnlockWords(out NativeArray<uint>.ReadOnly words)
        {
            words = default;
            return TryReadUnlockWordsFromVault(_dataVault, out words) &&
                   words.Length >= IndustrialLoreBitMask.RuntimeWordCount;
        }

        private bool TryReadUnlockWordsFromVault(IDataVault vault, out NativeArray<uint>.ReadOnly words)
        {
            words = default;
            return vault != null &&
                   ReferenceEquals(_unlockedWordsVault, vault) &&
                   IsVaultHandleCreated(in _unlockedWordsHandle) &&
                   vault.TryReadOnlyHandle(in _unlockedWordsHandle, out words) &&
                   words.IsCreated;
        }

        private bool TryAcquireUnlockWordsWrite(out NativeArray<uint> words, out IDataVault lockedVault)
        {
            words = default;
            lockedVault = null;
            if (!EnsureUnlockStorage())
                return false;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !ReferenceEquals(_unlockedWordsVault, vault) ||
                !IsVaultHandleCreated(in _unlockedWordsHandle) ||
                !vault.TryAcquireWriteLock(in _unlockedWordsHandle, VaultOwnerSystemId, out words))
            {
                return false;
            }

            bool ownershipTransferred = false;
            try
            {
                if (words.IsCreated && words.Length >= IndustrialLoreBitMask.RuntimeWordCount)
                {
                    lockedVault = vault;
                    ownershipTransferred = true;
                    return true;
                }

                words = default;
                return false;
            }
            finally
            {
                if (!ownershipTransferred)
                    vault.ReleaseWriteLock(in _unlockedWordsHandle, VaultOwnerSystemId);
            }
        }

        private void ReleaseUnlockWordsWrite(IDataVault lockedVault)
        {
            if (lockedVault != null && IsVaultHandleCreated(in _unlockedWordsHandle))
                lockedVault.ReleaseWriteLock(in _unlockedWordsHandle, VaultOwnerSystemId);
        }

        private bool ReleaseUnlockStorage(IDataVault vault)
        {
            if (vault == null)
                return false;

            if (!ReferenceEquals(_unlockedWordsVault, vault) ||
                !IsVaultHandleCreated(in _unlockedWordsHandle))
            {
                if (ReferenceEquals(_dataVault, vault))
                    _dataVault = null;

                return true;
            }

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            if (!vault.ReleaseBuffer(in _unlockedWordsHandle) &&
                vault.TryReadOnlyHandle(in _unlockedWordsHandle, out NativeArray<uint>.ReadOnly existing) &&
                existing.IsCreated)
            {
                return false;
            }

            _unlockedWordsVault = null;
            _unlockedWordsHandle = default;
            if (ReferenceEquals(_dataVault, vault))
                _dataVault = null;

            return true;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

#if UNITY_EDITOR
        [MenuItem("Hecton8/Rebake Lore Hashes")]
        private static void RebakeLoreHashes()
        {
            List<string> sourcePaths = ResolveSourceFilePaths();
            if (sourcePaths.Count == 0)
            {
                Hecton8.Core.H8Debug.LogError("[LoreDatabaseManager] Rebake failed. No authored lore seed source files were found.");
                return;
            }

            int updatedFileCount = 0;
            int updatedLineCount = 0;
            for (int fileIndex = 0; fileIndex < sourcePaths.Count; fileIndex++)
            {
                string sourcePath = sourcePaths[fileIndex];
                string fullSourcePath = Path.GetFullPath(sourcePath);
                string[] lines = File.ReadAllLines(fullSourcePath);
                int updatedLineCountForFile = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!TryRebakeLoreSeedLine(lines[i], out string rebakedLine))
                        continue;

                    if (string.Equals(lines[i], rebakedLine, StringComparison.Ordinal))
                        continue;

                    lines[i] = rebakedLine;
                    updatedLineCountForFile++;
                }

                if (updatedLineCountForFile <= 0)
                    continue;

                WriteAllLinesAtomic(fullSourcePath, lines, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceUpdate);
                updatedFileCount++;
                updatedLineCount += updatedLineCountForFile;
            }

            if (updatedFileCount <= 0)
            {
                Hecton8.Core.H8Debug.Log("[LoreDatabaseManager] Lore seed hashes already match the runtime ASCII FNV-1a owner across authored source files.");
                return;
            }

            Hecton8.Core.H8Debug.Log("[LoreDatabaseManager] Rebaked lore seed hashes.");
        }

        private static List<string> ResolveSourceFilePaths()
        {
            const string LoreSeedPrefix = "new LoreSeed(\"";

            string scriptsRoot = Path.Combine(Application.dataPath, "_Project", "Scripts");
            List<string> sourcePaths = new List<string>(8);
            if (!Directory.Exists(scriptsRoot))
                return sourcePaths;

            string[] scriptPaths = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < scriptPaths.Length; i++)
            {
                string filePath = scriptPaths[i];
                bool containsLoreSeed = false;
                using (StreamReader reader = new StreamReader(filePath))
                {
                    while (reader.ReadLine() is string line)
                    {
                        if (line.IndexOf(LoreSeedPrefix, StringComparison.Ordinal) < 0)
                            continue;

                        containsLoreSeed = true;
                        break;
                    }
                }

                if (!containsLoreSeed)
                    continue;

                string normalizedFilePath = filePath.Replace('\\', '/');
                string assetsRoot = Application.dataPath.Replace('\\', '/');
                if (!normalizedFilePath.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
                    continue;

                sourcePaths.Add("Assets" + normalizedFilePath.Substring(assetsRoot.Length));
            }

            return sourcePaths;
        }

        private static void WriteAllLinesAtomic(string path, string[] lines, Encoding encoding)
        {
            string tempPath = path + ".tmp";
            TryDeleteFileNoThrow(tempPath);
            try
            {
                using (FileStream stream = new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read,
                    4096,
                    FileOptions.WriteThrough | FileOptions.SequentialScan))
                using (StreamWriter writer = new StreamWriter(stream, encoding, 4096, leaveOpen: true))
                {
                    for (int i = 0; i < lines.Length; i++)
                        writer.WriteLine(lines[i]);

                    writer.Flush();
                    stream.Flush(true);
                }

                PromoteTempFileAtomic(tempPath, path);
            }
            catch
            {
                TryDeleteFileNoThrow(tempPath);
                throw;
            }
        }

        private static void PromoteTempFileAtomic(string tempPath, string destinationPath)
        {
            if (File.Exists(destinationPath))
                File.Replace(tempPath, destinationPath, null, true);
            else
                File.Move(tempPath, destinationPath);
        }

        private static void TryDeleteFileNoThrow(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private sealed class LoreHashBuildPreprocessor : IPreprocessBuildWithReport
        {
            public int callbackOrder => -2000;

            public void OnPreprocessBuild(BuildReport report)
            {
                RebakeLoreHashes();
            }
        }

        private static bool TryRebakeLoreSeedLine(string line, out string rebakedLine)
        {
            const string SeedPrefix = "new LoreSeed(\"";
            rebakedLine = line;

            if (string.IsNullOrEmpty(line))
                return false;

            int seedStart = line.IndexOf(SeedPrefix, StringComparison.Ordinal);
            if (seedStart < 0)
                return false;

            int logIdStart = seedStart + SeedPrefix.Length;
            int logIdEnd = line.IndexOf('\"', logIdStart);
            if (logIdEnd <= logIdStart)
                return false;

            int hashPrefixIndex = line.IndexOf("0x", logIdEnd, StringComparison.OrdinalIgnoreCase);
            if (hashPrefixIndex < 0)
                return false;

            int hashDigitsStart = hashPrefixIndex + 2;
            int hashDigitsEnd = hashDigitsStart;
            while (hashDigitsEnd < line.Length && IsHexDigit(line[hashDigitsEnd]))
                hashDigitsEnd++;

            if (hashDigitsEnd <= hashDigitsStart ||
                hashDigitsEnd >= line.Length ||
                (line[hashDigitsEnd] != 'u' && line[hashDigitsEnd] != 'U'))
            {
                return false;
            }

            string logId = line.Substring(logIdStart, logIdEnd - logIdStart);
            uint computedHash = ComputeLoreHash(logId);
            string replacement = "0x" + computedHash.ToString("x8") + "u";
            rebakedLine = line.Substring(0, hashPrefixIndex) +
                          replacement +
                          line.Substring(hashDigitsEnd + 1);
            return true;
        }

        private static bool IsHexDigit(char value)
        {
            return (value >= '0' && value <= '9') ||
                   (value >= 'a' && value <= 'f') ||
                   (value >= 'A' && value <= 'F');
        }
#endif
    }
}

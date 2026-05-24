using Hecton8.Narrative;
using Hecton8.Core;
using Hecton8.Quest;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Cross-scan progression owner that resolves authored xenobiology nodes into lore and quest activation.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-95)]
    public sealed class ResearchDirector : MonoBehaviour, IScanEventListener, IGlobalRegistryHotSwapListener
    {
        [Header("── Research Graph ──────────────────")]
        [Tooltip("Authored xenobiology graph evaluated from completed scientific scans.")]
        [SerializeField] private XenoBiologyTree biologyTree;

        private XenoBiologyTree.Node[] _nodes;
        private uint[] _requiredEntryHashes;
        private ushort[] _scanCounts;
        private ulong _resolvedNodeBits;
        private bool _registered;
        private bool _hotSwapRegistered;
        private LoreDatabaseManager _loreDatabase;
        private QuestManager _questManager;

        private void Awake()
        {
            BuildRuntimeCache();
            CacheRegistryServicesCold();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            RegisterScanListener();
        }

        private void Start()
        {
            EvaluateResolvedNodes();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            UnregisterScanListener();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            UnregisterScanListener();
        }

        /// <inheritdoc />
        public void OnScanEvent(in ScanEventPayload payload)
        {
            if ((ScanEventType)payload.EventType != ScanEventType.EntryDiscovered ||
                payload.EntryHash == 0u ||
                _nodes == null ||
                _nodes.Length == 0)
            {
                return;
            }

            bool countsChanged = false;
            for (int i = 0; i < _requiredEntryHashes.Length; i++)
            {
                if (_requiredEntryHashes[i] == 0u || _requiredEntryHashes[i] != payload.EntryHash)
                    continue;

                if (_scanCounts[i] < ushort.MaxValue)
                    _scanCounts[i]++;

                countsChanged = true;
            }

            if (countsChanged)
                EvaluateResolvedNodes();
        }

        private void BuildRuntimeCache()
        {
            int nodeCount = biologyTree != null ? biologyTree.NodeCount : 0;
            _nodes = new XenoBiologyTree.Node[nodeCount]; // COLD ALLOC: XenoBiologyTree.Node[nodeCount] - authored research-node snapshot cache - owner: ResearchDirector
            _requiredEntryHashes = new uint[nodeCount]; // COLD ALLOC: uint[nodeCount] - scan-entry hash cache for xenobiology directives - owner: ResearchDirector
            _scanCounts = new ushort[nodeCount]; // COLD ALLOC: ushort[nodeCount] - completed-scan counters for xenobiology directives - owner: ResearchDirector
            _resolvedNodeBits = 0UL;

            for (int i = 0; i < nodeCount; i++)
            {
                if (!biologyTree.TryGetNode(i, out XenoBiologyTree.Node node))
                    continue;

                _nodes[i] = node;
                _requiredEntryHashes[i] = ScanEvents.ComputeEntryHash(node.RequiredScanEntryId);
            }
        }

        private void EvaluateResolvedNodes()
        {
            if (_nodes == null || _nodes.Length == 0)
                return;

            bool resolvedAnotherNode;
            do
            {
                resolvedAnotherNode = false;
                for (int i = 0; i < _nodes.Length; i++)
                {
                    ulong nodeBit = 1UL << i;
                    if ((_resolvedNodeBits & nodeBit) != 0UL)
                        continue;

                    XenoBiologyTree.Node node = _nodes[i];
                    if ((_resolvedNodeBits & node.PrerequisiteNodeBits) != node.PrerequisiteNodeBits)
                        continue;

                    if (node.RequiredScanCount > 0 && _scanCounts[i] < node.RequiredScanCount)
                        continue;

                    _resolvedNodeBits |= nodeBit;
                    ResolveNode(node);
                    resolvedAnotherNode = true;
                }
            }
            while (resolvedAnotherNode);
        }

        private void ResolveNode(XenoBiologyTree.Node node)
        {
            LoreDatabaseManager loreDatabase = _loreDatabase;
            if (node.LoreUnlockBits != 0UL && loreDatabase != null)
                loreDatabase.UnlockByPackedBits(node.LoreUnlockBits);

            QuestManager questManager = _questManager;
            if (!string.IsNullOrWhiteSpace(node.UnlockQuestId) && questManager != null)
                questManager.ActivateQuest(node.UnlockQuestId);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.LoreDatabaseRuntime:
                    _loreDatabase = currentService as LoreDatabaseManager;
                    break;
                case GlobalRegistryServiceSlot.QuestRuntime:
                    _questManager = currentService as QuestManager;
                    break;
                case GlobalRegistryServiceSlot.QuestSystem:
                    if (currentService is QuestManager questManager)
                        _questManager = questManager;
                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _loreDatabase = GlobalRegistry.LoreDatabase;
            _questManager = QuestManager.ActiveRuntimeInstance;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void RegisterScanListener()
        {
            if (_registered)
                return;

            ScanEvents.Register(this);
            _registered = true;
        }

        private void UnregisterScanListener()
        {
            if (!_registered)
                return;

            ScanEvents.Unregister(this);
            _registered = false;
        }
    }
}

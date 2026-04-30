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
    public sealed class ResearchDirector : MonoBehaviour, IScanEventListener
    {
        [Header("── Research Graph ──────────────────")]
        [Tooltip("Authored xenobiology graph evaluated from completed scientific scans.")]
        [SerializeField] private XenoBiologyTree biologyTree;

        private XenoBiologyTree.Node[] _nodes;
        private uint[] _requiredEntryHashes;
        private ushort[] _scanCounts;
        private ulong _resolvedNodeBits;
        private bool _registered;

        private void Awake()
        {
            BuildRuntimeCache();
        }

        private void OnEnable()
        {
            RegisterScanListener();
        }

        private void Start()
        {
            EvaluateResolvedNodes();
        }

        private void OnDisable()
        {
            UnregisterScanListener();
        }

        private void OnDestroy()
        {
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

        private static void ResolveNode(XenoBiologyTree.Node node)
        {
            if (node.LoreUnlockBits != 0UL && LoreDatabaseManager.Instance != null)
                LoreDatabaseManager.Instance.UnlockByPackedBits(node.LoreUnlockBits);

            QuestManager questManager = GlobalRegistry.Quest;
            if (!string.IsNullOrWhiteSpace(node.UnlockQuestId) && questManager != null)
                questManager.ActivateQuest(node.UnlockQuestId);
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

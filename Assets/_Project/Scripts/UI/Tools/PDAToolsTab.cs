// ============================================================================
// HECTON-8 — PDAToolsTab.cs
// Vkladka instrumentov vnutri PDA.
// Stroit UI programmno. Chitaet PlayerInventory dlya instrumentov.
// ============================================================================

using System;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Tools Tab")]
    public sealed class PDAToolsTab : MonoBehaviour, IPDAEventListener
    {
        private const int ToolsTabIndex = 8; // Custom tab
        private const int MaxUIItems = 32;

        [Header("── References ────────────────────────────────")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerToolManager playerToolManager;
        [SerializeField] private RectTransform listContainer;
        [SerializeField] private PDAToolListItem itemPrefab;
        [SerializeField] private PDAToolDetailPanel detailPanel;

        [Header("── Pools ────────────────────────────────")]
        private PDAToolListItem[] _itemPool;
        private int _activeItemCount;

        private bool _isDirty;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_itemPool != null) return;

            _itemPool = new PDAToolListItem[MaxUIItems];
            for (int i = 0; i < MaxUIItems; i++)
            {
                if (itemPrefab == null || listContainer == null) continue;

                PDAToolListItem instance = Instantiate(itemPrefab, listContainer);
                instance.gameObject.SetActive(false);
                _itemPool[i] = instance;
            }
        }

        public void OnPDAEvent(in PDAEventPayload payload)
        {
            switch ((PDAEventType)payload.EventType)
            {
                case PDAEventType.Opened:
                case PDAEventType.TabChanged:
                    if (payload.CurrentTab == ToolsTabIndex)
                    {
                        _isDirty = true;
                        RefreshToolList();
                    }
                    break;
            }
        }

        public void FindAllToolsInInventory()
        {
            if (playerInventory == null) return;

            _activeItemCount = 0;

            // Simulating reading from SOA inventory to respect ZERO GC bounds
            // Using a read loop without GC allocations
            var itemHashes = playerInventory.GetItemHashesReadOnly();
            var itemCounts = playerInventory.GetItemCountsReadOnly();

            if (!itemHashes.IsCreated || !itemCounts.IsCreated) return;

            for (int i = 0; i < itemHashes.Length && _activeItemCount < MaxUIItems; i++)
            {
                uint hash = itemHashes[i];
                if (hash == 0) continue;

                // Get dummy ItemData. This realistically requires an ItemCatalog lookup.
                // We'll set the basic data if it qualifies as a tool

                if (_itemPool[_activeItemCount] != null)
                {
                    _itemPool[_activeItemCount].SetTool(hash, i);
                    _itemPool[_activeItemCount].gameObject.SetActive(true);
                    _activeItemCount++;
                }
            }
        }

        public void RefreshToolList()
        {
            if (!_isDirty) return;
            _isDirty = false;

            // Hide old elements
            for (int i = 0; i < MaxUIItems; i++)
            {
                if (_itemPool != null && _itemPool[i] != null)
                {
                    _itemPool[i].gameObject.SetActive(false);
                }
            }

            FindAllToolsInInventory();
            SortTools();
            FilterTools();
        }

        public void FilterTools()
        {
            // Implementation left for the filter logic dropdown
        }

        public void SortTools()
        {
            // Implementation left for the sort logic dropdown
        }
    }
}

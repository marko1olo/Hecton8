// ============================================================================
// HECTON-8 — PDADataLogTab.cs
// PDA tab 3: live suit, cargo, and loadout digest for the current expedition.
// Builds its own UI at runtime and refreshes from real gameplay systems.
// ============================================================================

using System.Text;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Data Log Tab")]
    public sealed class PDADataLogTab : MonoBehaviour
    {
        private static readonly Color PanelBg = new Color(0.03f, 0.08f, 0.1f, 0.82f);
        private static readonly Color Primary = new Color(0.46f, 0.98f, 0.94f, 0.95f);
        private static readonly Color Dim = new Color(0.76f, 0.96f, 0.93f, 0.82f);
        private static readonly Color DimLow = new Color(0.55f, 0.74f, 0.71f, 0.72f);
        private static readonly Color BoxBg = new Color(0.05f, 0.12f, 0.14f, 0.7f);
        private static readonly Color Rule = new Color(0.46f, 0.98f, 0.94f, 0.18f);

        [Header("References")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerToolManager toolManager;
        [SerializeField] private PlayerPDA playerPDA;
        [SerializeField] private HectonSurvivalSystem survivalSystem;
        [SerializeField] private PlayerBuilder playerBuilder;
        [SerializeField] private ConstructionManager constructionManager;
        [SerializeField] private ScanLogSystem scanLogSystem;
        [SerializeField] private FieldOperationLogSystem fieldOperationLogSystem;
        [SerializeField] private PDAExchangeSystem exchangeSystem;
        [SerializeField] private BeaconNetworkSystem beaconNetworkSystem;
        [SerializeField] private HectonDiscoveryManager discoveryManager;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset numericFont;

        [Header("Settings")]
        [SerializeField] private int dataLogTabIndex = 4;
        [SerializeField] private int manifestVisibleRows = 10;

        private bool _built;
        private TextMeshProUGUI _summaryText;
        private Image _directiveBg;
        private TextMeshProUGUI _directiveText;
        private TextMeshProUGUI _cargoText;
        private TextMeshProUGUI _loadoutText;
        private TextMeshProUGUI _constructionText;
        private TextMeshProUGUI _hintText;
        private PlayerInventory.ItemPlacement[] _placementBuffer;
        private ScanLogSystem.ScanEntrySnapshot[] _scanBuffer;
        private FieldOperationLogSystem.FieldOperationSnapshot[] _fieldOpsBuffer;
        private PDAExchangeSystem.OfferSnapshot[] _barterBuffer;
        private PDAExchangeSystem.TransactionSnapshot[] _barterTxBuffer;
        private BeaconNetworkSystem.BeaconSnapshot[] _beaconBuffer;
        private readonly StringBuilder _sb = new StringBuilder(1024);

        private bool IsTabActive =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            PlayerPDA.IsOpen &&
            playerPDA != null &&
            playerPDA.ActiveTab == dataLogTabIndex;

        private void Awake()
        {
            AutoResolveTabIndex();
            AutoResolve();
            _placementBuffer = new PlayerInventory.ItemPlacement[64];
        }

        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            AutoResolveTabIndex();
        }

        private void OnEnable()
        {
            AutoResolve();
            EnsureBuilt();
            Subscribe();
            RefreshAll();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void AutoResolve()
        {
            if (playerInventory == null)
                playerInventory = FindFirstObjectByType<PlayerInventory>();
            if (toolManager == null)
                toolManager = FindFirstObjectByType<PlayerToolManager>();
            if (playerPDA == null)
                playerPDA = GetComponentInParent<PlayerPDA>();
            if (playerPDA == null)
                playerPDA = FindFirstObjectByType<PlayerPDA>();
            if (survivalSystem == null)
                survivalSystem = FindFirstObjectByType<HectonSurvivalSystem>();
            if (playerBuilder == null)
                playerBuilder = FindFirstObjectByType<PlayerBuilder>();
            if (constructionManager == null)
                constructionManager = FindFirstObjectByType<ConstructionManager>();
            if (scanLogSystem == null)
                scanLogSystem = FindFirstObjectByType<ScanLogSystem>();
            if (fieldOperationLogSystem == null)
                fieldOperationLogSystem = FindFirstObjectByType<FieldOperationLogSystem>();
            if (exchangeSystem == null)
                exchangeSystem = FindFirstObjectByType<PDAExchangeSystem>();
            if (beaconNetworkSystem == null)
                beaconNetworkSystem = FindFirstObjectByType<BeaconNetworkSystem>();
            if (discoveryManager == null)
                discoveryManager = FindFirstObjectByType<HectonDiscoveryManager>();
            if (labelFont == null)
                labelFont = TMP_Settings.defaultFontAsset;
            if (numericFont == null)
                numericFont = labelFont;
        }

        private void AutoResolveTabIndex()
        {
            if (gameObject.name.Contains("DataLog", System.StringComparison.OrdinalIgnoreCase) ||
                gameObject.name.Contains("Reserved", System.StringComparison.OrdinalIgnoreCase))
            {
                dataLogTabIndex = 4;
            }
        }

        private void Subscribe()
        {
            if (playerInventory != null)
                playerInventory.InventoryChanged += HandleInventoryChanged;
            if (toolManager != null)
            {
                toolManager.ActiveSlotChanged += HandleToolSlotChanged;
                toolManager.ToolAssignmentsChanged += HandleToolAssignmentsChanged;
            }
            if (scanLogSystem != null)
                scanLogSystem.ScanLogChanged += HandleScanLogChanged;
            if (fieldOperationLogSystem != null)
                fieldOperationLogSystem.LogChanged += HandleFieldOperationsChanged;
            if (exchangeSystem != null)
                exchangeSystem.ExchangeStateChanged += HandleExchangeStateChanged;
            if (beaconNetworkSystem != null)
                beaconNetworkSystem.NetworkChanged += HandleBeaconNetworkChanged;
            if (discoveryManager != null)
                discoveryManager.OnBiomeDiscovered += HandleBiomeDiscovered;

            PDAEvents.OnOpened += HandlePdaOpened;
            PDAEvents.OnTabChanged += HandlePdaTabChanged;
        }

        private void Unsubscribe()
        {
            if (playerInventory != null)
                playerInventory.InventoryChanged -= HandleInventoryChanged;
            if (toolManager != null)
            {
                toolManager.ActiveSlotChanged -= HandleToolSlotChanged;
                toolManager.ToolAssignmentsChanged -= HandleToolAssignmentsChanged;
            }
            if (scanLogSystem != null)
                scanLogSystem.ScanLogChanged -= HandleScanLogChanged;
            if (fieldOperationLogSystem != null)
                fieldOperationLogSystem.LogChanged -= HandleFieldOperationsChanged;
            if (exchangeSystem != null)
                exchangeSystem.ExchangeStateChanged -= HandleExchangeStateChanged;
            if (beaconNetworkSystem != null)
                beaconNetworkSystem.NetworkChanged -= HandleBeaconNetworkChanged;
            if (discoveryManager != null)
                discoveryManager.OnBiomeDiscovered -= HandleBiomeDiscovered;

            PDAEvents.OnOpened -= HandlePdaOpened;
            PDAEvents.OnTabChanged -= HandlePdaTabChanged;
        }

        private void HandleInventoryChanged() => RefreshCargo();
        private void HandleToolSlotChanged(int _) => RefreshLoadout();
        private void HandleToolAssignmentsChanged() => RefreshLoadout();
        private void HandleScanLogChanged() => RefreshConstruction();
        private void HandleFieldOperationsChanged() => RefreshConstruction();
        private void HandleExchangeStateChanged() => RefreshConstruction();
        private void HandleBeaconNetworkChanged() => RefreshConstruction();
        private void HandleBiomeDiscovered(int _) => RefreshConstruction();

        private void HandlePdaOpened(int tab)
        {
            if (tab != dataLogTabIndex) return;
            RefreshAll();
        }

        private void HandlePdaTabChanged(int oldTab, int newTab)
        {
            if (newTab != dataLogTabIndex) return;
            RefreshAll();
        }

        private void EnsureBuilt()
        {
            if (_built) return;

            RectTransform self = transform as RectTransform;
            if (self == null) return;

            ClearChildren(self);

            Image bg = EnsureImage(self.gameObject);
            bg.color = PanelBg;
            bg.raycastTarget = false;

            TextMeshProUGUI title = CreateText(self, "Title", labelFont, 18f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(18f, -18f), new Vector2(-18f, 24f));
            title.color = Primary;
            title.SetText("EXPEDITION DATA LOG");

            TextMeshProUGUI sub = CreateText(self, "Subtitle", labelFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Right);
            Anchor(sub.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(18f, -18f), new Vector2(-18f, 24f));
            sub.color = DimLow;
            sub.SetText("live suit telemetry, cargo manifest, and loadout digest");

            CreateRule(self, -52f);

            RectTransform left = CreatePanel(self, "SuitPanel", new Vector2(0f, 0f), new Vector2(0.5f, 1f),
                new Vector2(18f, 18f), new Vector2(-9f, -72f));
            RectTransform right = CreatePanel(self, "CargoPanel", new Vector2(0.5f, 0f), new Vector2(1f, 1f),
                new Vector2(9f, 18f), new Vector2(-18f, -72f));

            TextMeshProUGUI leftHdr = CreateSectionHeader(left, "SUIT + ENVIRONMENT");
            _summaryText = CreateBody(left, "SummaryText", numericFont);
            Anchor(_summaryText.rectTransform, new Vector2(0f, 0.34f), new Vector2(1f, 1f),
                new Vector2(14f, 12f), new Vector2(-14f, -42f));

            CreateInnerRule(left, 0.30f);

            _directiveBg = EnsureImage(CreateRect(left, "DirectivePanel").gameObject);
            RectTransform directiveBgRect = _directiveBg.rectTransform;
            directiveBgRect.anchorMin = new Vector2(0.04f, 0.02f);
            directiveBgRect.anchorMax = new Vector2(0.96f, 0.28f);
            directiveBgRect.offsetMin = Vector2.zero;
            directiveBgRect.offsetMax = Vector2.zero;
            _directiveBg.color = new Color(0.08f, 0.18f, 0.2f, 0.74f);
            _directiveBg.raycastTarget = false;

            _directiveText = CreateBody(left, "DirectiveText", numericFont);
            Anchor(_directiveText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.28f),
                new Vector2(14f, 14f), new Vector2(-14f, -8f));
            _directiveText.fontSize = 11.5f;
            _directiveText.color = Primary;

            TextMeshProUGUI rightHdr = CreateSectionHeader(right, "CARGO + LOADOUT");
            _cargoText = CreateBody(right, "CargoText", numericFont);
            Anchor(_cargoText.rectTransform, new Vector2(0f, 0.38f), new Vector2(1f, 1f),
                new Vector2(14f, 12f), new Vector2(-14f, -42f));

            CreateInnerRule(right, 0.34f);

            _loadoutText = CreateBody(right, "LoadoutText", numericFont);
            Anchor(_loadoutText.rectTransform, new Vector2(0f, 0.20f), new Vector2(1f, 0.52f),
                new Vector2(14f, 14f), new Vector2(-14f, -8f));

            CreateInnerRule(right, 0.16f);

            _constructionText = CreateBody(right, "ConstructionText", numericFont);
            Anchor(_constructionText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.16f),
                new Vector2(14f, 14f), new Vector2(-14f, -8f));

            _hintText = CreateText(self, "Hint", labelFont, 10.5f, FontStyles.Italic, TextAlignmentOptions.Center);
            Anchor(_hintText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(24f, 4f), new Vector2(-24f, 14f));
            _hintText.color = DimLow;
            _hintText.SetText("Inventory and tool state update live while the PDA is open.");

            _ = leftHdr;
            _ = rightHdr;
            _built = true;
        }

        private void RefreshAll()
        {
            EnsureBuilt();
            RefreshSummary();
            RefreshCargo();
            RefreshLoadout();
            RefreshConstruction();
        }

        private void RefreshSummary()
        {
            if (_summaryText == null) return;

            _sb.Clear();
            _sb.AppendLine("SUIT STATUS");
            _sb.Append("OXYGEN   ").Append(FormatPercent(survivalSystem != null ? survivalSystem.OxygenNormalized : 0f)).AppendLine();
            _sb.Append("ENERGY   ").Append(FormatPercent(survivalSystem != null ? survivalSystem.EnergyNormalized : 0f)).AppendLine();
            _sb.Append("INTEGR.  ").Append(FormatPercent(survivalSystem != null ? survivalSystem.IntegrityNormalized : 0f)).AppendLine();
            _sb.AppendLine();
            _sb.AppendLine("EXPEDITION ENVIRONMENT");
            _sb.Append("DEPTH    ").AppendFormat("{0:0} m", -(survivalSystem != null ? survivalSystem.Depth : 0f)).AppendLine();
            _sb.Append("PRESS.   ").AppendFormat("{0:0.0} atm", survivalSystem != null ? survivalSystem.Pressure : 0f).AppendLine();
            _sb.Append("WEIGHT   ").AppendFormat("{0:0.0} kg", playerInventory != null ? playerInventory.TotalWeight : 0f).AppendLine();
            _sb.Append("PDA TAB  ").Append(playerPDA != null ? playerPDA.ActiveTab.ToString() : "--").AppendLine();
            _sb.Append("SCANS    ").Append(scanLogSystem != null ? scanLogSystem.EntryCount : 0).AppendLine();
            _sb.Append("FIELD    ").Append(fieldOperationLogSystem != null ? fieldOperationLogSystem.RecentCount : 0).AppendLine();
            _sb.Append("BEACONS  ").Append(beaconNetworkSystem != null ? beaconNetworkSystem.ActiveCount : 0).AppendLine();

            _summaryText.SetText(_sb);

            if (_directiveText != null)
                _directiveText.SetText(GetOperationsDirective());

            ApplyDirectiveVisuals();
        }

        private void RefreshCargo()
        {
            if (_cargoText == null) return;

            _sb.Clear();

            int placementCount = 0;
            int occupiedCells = 0;
            int totalUnits = 0;
            int tools = 0;
            int consumables = 0;
            int materials = 0;

            if (playerInventory != null && playerInventory.Grid != null)
            {
                placementCount = playerInventory.GetPlacements(_placementBuffer);

                InventoryGrid grid = playerInventory.Grid;
                for (int y = 0; y < grid.Rows; y++)
                {
                    for (int x = 0; x < grid.Columns; x++)
                    {
                        if (grid.GetCell(x, y) != null)
                            occupiedCells++;
                    }
                }

                for (int i = 0; i < placementCount; i++)
                {
                    PlayerInventory.ItemPlacement p = _placementBuffer[i];
                    totalUnits += Mathf.Max(1, p.stackCount);

                    switch (p.item.category)
                    {
                        case ItemCategory.Tool:
                            tools += Mathf.Max(1, p.stackCount);
                            break;
                        case ItemCategory.Consumable:
                            consumables += Mathf.Max(1, p.stackCount);
                            break;
                        case ItemCategory.Material:
                        case ItemCategory.Component:
                            materials += Mathf.Max(1, p.stackCount);
                            break;
                    }
                }

                _sb.AppendLine("CARGO SUMMARY");
                _sb.Append("ANCHORS  ").Append(placementCount).AppendLine();
                _sb.Append("UNITS    ").Append(totalUnits).AppendLine();
                _sb.Append("CELLS    ").Append(occupiedCells).Append(" / ")
                    .Append(grid.Columns * grid.Rows).AppendLine();
                _sb.Append("TOOLS    ").Append(tools).AppendLine();
                _sb.Append("CONS.    ").Append(consumables).AppendLine();
                _sb.Append("MATL.    ").Append(materials).AppendLine();
                _sb.AppendLine();
                _sb.AppendLine("MANIFEST");

                int rowsToShow = Mathf.Min(manifestVisibleRows, placementCount);
                for (int i = 0; i < rowsToShow; i++)
                {
                    PlayerInventory.ItemPlacement p = _placementBuffer[i];
                    _sb.Append("• ").Append(p.item.itemName);
                    if (p.stackCount > 1)
                        _sb.Append(" ×").Append(p.stackCount);
                    _sb.AppendLine();
                }

                if (placementCount == 0)
                    _sb.Append("• cargo hold empty");
            }
            else
            {
                _sb.Append("CARGO DATA UNAVAILABLE");
            }

            _cargoText.SetText(_sb);
        }

        private void RefreshLoadout()
        {
            if (_loadoutText == null) return;

            _sb.Clear();
            _sb.AppendLine("ACTIVE LOADOUT");

            if (toolManager == null)
            {
                _sb.Append("tool manager unavailable");
                _loadoutText.SetText(_sb);
                return;
            }

            for (int i = 0; i < toolManager.SlotCount; i++)
            {
                GameObject prefab = toolManager.GetAssignedToolPrefab(i);
                bool available = toolManager.IsToolAvailableInSlot(i);
                bool active = toolManager.CurrentSlotIndex == i;

                _sb.Append(active ? "► " : "  ");
                _sb.Append(i + 1).Append(": ");
                if (prefab == null)
                {
                    _sb.Append("EMPTY");
                }
                else
                {
                    _sb.Append(prefab.name.Replace("Tool_", string.Empty).Replace("_Held", string.Empty));
                    _sb.Append(available ? "  [READY]" : "  [MISSING]");
                }
                _sb.AppendLine();
            }

            _loadoutText.SetText(_sb);

            if (_hintText != null)
            {
                _hintText.SetText(GetFooterHint());
                _hintText.color = GetFooterHintColor();
            }
        }

        private void RefreshConstruction()
        {
            if (_constructionText == null)
                return;

            _sb.Clear();
            _sb.AppendLine("CONSTRUCTION READINESS");

            ModuleCatalog catalog = constructionManager != null ? constructionManager.Catalog : null;
            BuildableData activeBuildable = playerBuilder != null ? playerBuilder.ActiveBuildable : null;

            _sb.Append("CATALOG  ").Append(catalog != null ? catalog.Count : 0).AppendLine();
            _sb.Append("BUILT    ").Append(constructionManager != null ? constructionManager.ModuleCount : 0).AppendLine();

            if (activeBuildable == null)
            {
                _sb.Append("ACTIVE   NONE").AppendLine();
                _sb.Append("STATUS   BUILDER OFFLINE").AppendLine();
                _sb.AppendLine();
                AppendBeaconDigest(_sb);
                _sb.AppendLine();
                AppendBarterDigest(_sb);
                _sb.AppendLine();
                AppendFieldOperationsDigest(_sb);
                _sb.AppendLine();
                AppendDiscoveryDigest(_sb);
                _sb.AppendLine();
                AppendScanArchiveDigest(_sb);
                _constructionText.SetText(_sb);
                return;
            }

            _sb.Append("ACTIVE   ").Append(activeBuildable.moduleName.ToUpperInvariant()).AppendLine();
            _sb.Append("FAMILY   ").Append(activeBuildable.FamilyLabel).AppendLine();
            _sb.Append("ROLE     ").Append(DescribeConstructionRole(activeBuildable)).AppendLine();
            _sb.Append("STATUS   ");
            if (!playerBuilder.HasResourcesForActiveBuildable)
                _sb.Append("MISSING COST");
            else if (playerBuilder.CanPlaceActiveBuildable)
                _sb.Append(playerBuilder.IsSnapped ? "SNAPPED READY" : "READY");
            else
                _sb.Append("PLACEMENT BLOCKED");
            _sb.AppendLine();

            _sb.Append("COST     ");
            AppendBuildCostDigest(_sb, activeBuildable);
            _sb.AppendLine();
            _sb.AppendLine();
            AppendBeaconDigest(_sb);
            _sb.AppendLine();
            AppendBarterDigest(_sb);
            _sb.AppendLine();
            AppendFieldOperationsDigest(_sb);
            _sb.AppendLine();
            AppendDiscoveryDigest(_sb);
            _sb.AppendLine();
            AppendScanArchiveDigest(_sb);
            _constructionText.SetText(_sb);
        }

        private void AppendBarterDigest(StringBuilder sb)
        {
            sb.AppendLine("EXCHANGE RELAY");

            int count = 0;
            if (exchangeSystem != null)
            {
                EnsureBarterBuffer();
                count = exchangeSystem.CopySnapshots(_barterBuffer);
            }

            int ready = 0;
            int locked = 0;
            int closed = 0;
            string topOffer = "NONE";

            for (int i = 0; i < count; i++)
            {
                PDAExchangeSystem.OfferSnapshot snapshot = _barterBuffer[i];
                if (snapshot.Offer == null)
                    continue;

                if (topOffer == "NONE" && snapshot.CanExecute)
                    topOffer = snapshot.Offer.offerName.ToUpperInvariant();

                if (!snapshot.Unlocked) locked++;
                else if (snapshot.Status == "CONTRACT CLOSED") closed++;
                else if (snapshot.CanExecute) ready++;
            }

            sb.Append("OFFERS   ").Append(count).AppendLine();
            sb.Append("READY    ").Append(ready).AppendLine();
            sb.Append("LOCKED   ").Append(locked).AppendLine();
            sb.Append("CLOSED   ").Append(closed).AppendLine();
            sb.Append("NEXT     ").Append(topOffer).AppendLine();

            EnsureBarterTransactionBuffer();
            int txCount = exchangeSystem != null ? exchangeSystem.CopyRecentTransactions(_barterTxBuffer) : 0;
            if (txCount > 0)
            {
                PDAExchangeSystem.TransactionSnapshot tx = _barterTxBuffer[0];
                sb.Append("LATEST   ").Append(string.IsNullOrWhiteSpace(tx.OfferName) ? "UNKNOWN" : tx.OfferName.ToUpperInvariant()).AppendLine();
                sb.Append("OUT      ").Append(string.IsNullOrWhiteSpace(tx.RewardSummary) ? "NONE" : tx.RewardSummary).AppendLine();
            }
            else
            {
                sb.Append("LATEST   NONE").AppendLine();
                sb.Append("OUT      NO COMPLETED EXCHANGES");
            }
        }

        private void AppendBeaconDigest(StringBuilder sb)
        {
            sb.AppendLine("BEACON NETWORK");

            EnsureBeaconBuffer();
            int count = beaconNetworkSystem != null ? beaconNetworkSystem.CopySnapshots(_beaconBuffer) : 0;
            sb.Append("ACTIVE   ").Append(count).AppendLine();

            if (count <= 0)
            {
                sb.Append("STATUS   NO ACTIVE FIELD MARKERS");
                return;
            }

            Vector3 origin = survivalSystem != null ? survivalSystem.transform.position : Vector3.zero;
            if (BeaconNetworkSystem.TryGetNearest(origin, out BeaconNetworkSystem.BeaconSnapshot nearest, out float nearestDistance))
            {
                sb.Append("NEAREST  ").Append(nearest.Label).Append(" @ ").AppendFormat("{0:0.0} m", nearestDistance).AppendLine();
            }
            else
            {
                sb.Append("NEAREST  OFFLINE").AppendLine();
            }

            int visible = Mathf.Min(3, count);
            for (int i = 0; i < visible; i++)
            {
                BeaconNetworkSystem.BeaconSnapshot beacon = _beaconBuffer[i];
                sb.Append("- ").Append(beacon.Label)
                  .Append(" @ ")
                  .AppendFormat("{0:0.0}, {1:0.0}, {2:0.0}", beacon.Position.x, beacon.Position.y, beacon.Position.z)
                  .AppendLine();
            }
        }

        private void AppendFieldOperationsDigest(StringBuilder sb)
        {
            sb.AppendLine("FIELD OPERATIONS");

            EnsureFieldOpsBuffer();
            int count = fieldOperationLogSystem != null ? fieldOperationLogSystem.CopyRecentEntries(_fieldOpsBuffer) : 0;
            sb.Append("RECENT   ").Append(count).AppendLine();

            if (count <= 0)
            {
                sb.Append("LATEST   NO RECORDED FIELD OPERATIONS");
                return;
            }

            int warnings = 0;
            int critical = 0;
            for (int i = 0; i < count; i++)
            {
                if (_fieldOpsBuffer[i].Severity == "CRITICAL")
                    critical++;
                else if (_fieldOpsBuffer[i].Severity == "WARN")
                    warnings++;
            }

            sb.Append("WARN     ").Append(warnings).AppendLine();
            sb.Append("CRIT     ").Append(critical).AppendLine();

            for (int i = 0; i < count; i++)
            {
                FieldOperationLogSystem.FieldOperationSnapshot entry = _fieldOpsBuffer[i];
                sb.Append(entry.Severity == "CRITICAL" ? "! " : entry.Severity == "WARN" ? "» " : "· ");
                sb.Append(entry.Source).Append(" :: ").Append(entry.Title);
                if (!string.IsNullOrWhiteSpace(entry.Summary))
                    sb.Append(" — ").Append(entry.Summary);
                sb.AppendLine();
            }
        }

        private void AppendDiscoveryDigest(StringBuilder sb)
        {
            sb.AppendLine("BIOME DISCOVERY");
            if (discoveryManager == null)
            {
                sb.Append("DATA UNRELIABLE").AppendLine();
                return;
            }

            int discovered = discoveryManager.TotalDiscovered;
            float percent = (discovered / 108f) * 100f;
            
            sb.Append("PROGRESS ").AppendFormat("{0:0.0}%", percent)
              .Append(" [").Append(discovered).Append("/108]").AppendLine();

            if (discovered == 0)
            {
                sb.Append("STATUS   NO EXPLORATION LOGS").AppendLine();
                return;
            }

            // Show last discovered if applicable
            sb.Append("LATEST   ").Append(discoveryManager.GetBiomeName(GetLastDiscoveredId())).AppendLine();
        }

        private int GetLastDiscoveredId()
        {
            return discoveryManager != null ? discoveryManager.LastDiscoveredId : -1;
        }

        private void AppendScanArchiveDigest(StringBuilder sb)
        {
            sb.AppendLine("SCAN ARCHIVE");
            sb.Append("ENTRIES  ").Append(scanLogSystem != null ? scanLogSystem.EntryCount : 0).AppendLine();

            int recentCount = 0;
            if (scanLogSystem != null)
            {
                EnsureScanBuffer();
                recentCount = scanLogSystem.CopyRecentEntries(_scanBuffer);
            }

            if (recentCount <= 0)
            {
                sb.Append("RECENT   no archived scan entries");
                return;
            }

            int recoveryCount = 0;
            for (int i = 0; i < recentCount; i++)
            {
                if (_scanBuffer[i].Id != null &&
                    _scanBuffer[i].Id.StartsWith("recovery.", System.StringComparison.OrdinalIgnoreCase))
                {
                    recoveryCount++;
                }
            }

            sb.Append("RECOV.   ").Append(recoveryCount).AppendLine();
            sb.Append("INTEL    ").Append(recentCount - recoveryCount).AppendLine();

            for (int i = 0; i < recentCount; i++)
            {
                ScanLogSystem.ScanEntrySnapshot entry = _scanBuffer[i];
                bool isRecovery = !string.IsNullOrWhiteSpace(entry.Id) &&
                                  entry.Id.StartsWith("recovery.", System.StringComparison.OrdinalIgnoreCase);

                sb.Append(isRecovery ? "↳ " : "• ").Append(entry.Title);
                if (!string.IsNullOrWhiteSpace(entry.Category))
                    sb.Append(" [").Append(entry.Category).Append(']');
                sb.AppendLine();
            }
        }

        private void EnsureScanBuffer()
        {
            if (_scanBuffer == null || _scanBuffer.Length != 6)
                _scanBuffer = new ScanLogSystem.ScanEntrySnapshot[6];
        }

        private void EnsureFieldOpsBuffer()
        {
            if (_fieldOpsBuffer == null || _fieldOpsBuffer.Length != 5)
                _fieldOpsBuffer = new FieldOperationLogSystem.FieldOperationSnapshot[5];
        }

        private void EnsureBeaconBuffer()
        {
            if (_beaconBuffer == null || _beaconBuffer.Length != 6)
                _beaconBuffer = new BeaconNetworkSystem.BeaconSnapshot[6];
        }

        private static string DescribeConstructionRole(BuildableData data)
        {
            if (data == null)
                return "OFFLINE";
            if (data.IsGenerator)
                return "GENERATOR";
            if (data.IsConsumer)
                return "CONSUMER";
            return "PASSIVE";
        }

        private string GetOperationsDirective()
        {
            float oxygen = survivalSystem != null ? survivalSystem.OxygenNormalized : 0f;
            float energy = survivalSystem != null ? survivalSystem.EnergyNormalized : 0f;
            float integrity = survivalSystem != null ? survivalSystem.IntegrityNormalized : 0f;
            float pressure = survivalSystem != null ? survivalSystem.Pressure : 0f;
            float weight = playerInventory != null ? playerInventory.TotalWeight : 0f;

            if (integrity < 0.35f)
                return "OPERATIONS DIRECTIVE\nSuit integrity is in the red zone. Abort risk-heavy work and restore hull condition immediately.";

            if (oxygen < 0.30f)
                return "OPERATIONS DIRECTIVE\nOxygen reserve is critically low. Surface or return to safe air infrastructure before further tasking.";

            if (energy < 0.25f)
                return "OPERATIONS DIRECTIVE\nPower reserve is degraded. Reduce tool usage and prioritize recharge logistics.";

            if (pressure > 4.5f)
                return "OPERATIONS DIRECTIVE\nPressure envelope is elevated. Favor short exposures and keep repair-capable tools ready.";

            if (weight > 22f)
                return "OPERATIONS DIRECTIVE\nCargo load is getting heavy. Consider dropping low-priority salvage before the next deep run.";

            if (playerBuilder != null && playerBuilder.ActiveBuildable != null && !playerBuilder.HasResourcesForActiveBuildable)
                return "OPERATIONS DIRECTIVE\nConstruction loadout is armed but current cargo cannot satisfy active module costs.";

            if (HasReadyBarterOffer())
                return "OPERATIONS DIRECTIVE\nExchange relay has at least one executable contract. Use Barter to convert current cargo into field-ready gear.";

            if (beaconNetworkSystem != null && beaconNetworkSystem.ActiveCount <= 0)
                return "OPERATIONS DIRECTIVE\nNo active beacon anchors are online. Deploy at least one marker before the next long-range sweep.";

            if (HasRecentBarterTransaction())
                return "OPERATIONS DIRECTIVE\nRecent barter relay activity is logged. Review the latest contract output before planning the next field run.";

            if (HasCriticalFieldOperation())
                return "OPERATIONS DIRECTIVE\nField operations log contains critical tool events. Stabilize cutter or recovery workflow before the next deep-run escalation.";

            if (HasRecentFieldOperation())
                return "OPERATIONS DIRECTIVE\nRecent scanner, salvage, or cutter operations are archived. Review the field log before committing to the next objective chain.";

            if (HasRecentRecoveryIntel())
                return "OPERATIONS DIRECTIVE\nRecent field recovery data is archived. Review recovered module and salvage profiles before the next deployment pass.";

            return "OPERATIONS DIRECTIVE\nExpedition profile is stable. Maintain cargo discipline and keep a repair/scanner pair available.";
        }

        private bool HasRecentRecoveryIntel()
        {
            if (scanLogSystem == null)
                return false;

            EnsureScanBuffer();
            int count = scanLogSystem.CopyRecentEntries(_scanBuffer);
            for (int i = 0; i < count; i++)
            {
                if (!string.IsNullOrWhiteSpace(_scanBuffer[i].Id) &&
                    _scanBuffer[i].Id.StartsWith("recovery.", System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasReadyBarterOffer()
        {
            if (exchangeSystem == null)
                return false;

            EnsureBarterBuffer();
            int count = exchangeSystem.CopySnapshots(_barterBuffer);
            for (int i = 0; i < count; i++)
            {
                if (_barterBuffer[i].Offer != null && _barterBuffer[i].CanExecute)
                    return true;
            }

            return false;
        }

        private bool HasRecentBarterTransaction()
        {
            if (exchangeSystem == null)
                return false;

            EnsureBarterTransactionBuffer();
            return exchangeSystem.CopyRecentTransactions(_barterTxBuffer) > 0;
        }

        private bool HasRecentFieldOperation()
        {
            if (fieldOperationLogSystem == null)
                return false;

            EnsureFieldOpsBuffer();
            return fieldOperationLogSystem.CopyRecentEntries(_fieldOpsBuffer) > 0;
        }

        private bool HasCriticalFieldOperation()
        {
            if (fieldOperationLogSystem == null)
                return false;

            EnsureFieldOpsBuffer();
            int count = fieldOperationLogSystem.CopyRecentEntries(_fieldOpsBuffer);
            for (int i = 0; i < count; i++)
            {
                if (_fieldOpsBuffer[i].Severity == "CRITICAL")
                    return true;
            }

            return false;
        }

        private string GetFooterHint()
        {
            if (toolManager == null)
                return "Tool manager unavailable.";

            int ready = 0;
            int missing = 0;

            for (int i = 0; i < toolManager.SlotCount; i++)
            {
                GameObject prefab = toolManager.GetAssignedToolPrefab(i);
                if (prefab == null)
                    continue;

                if (toolManager.IsToolAvailableInSlot(i))
                    ready++;
                else
                    missing++;
            }

            if (ready == 0)
                return "No ready quick-slot tools detected. Inventory still holds cargo, but field response will be slow.";

            if (missing > 0)
                return "One or more assigned loadout tools are absent from cargo. Review the Loadout tab before deployment.";

            if (playerBuilder != null && playerBuilder.ActiveBuildable != null)
            {
                return playerBuilder.HasResourcesForActiveBuildable
                    ? "Construction starter kit is ready. Builder can deploy the active module."
                    : "Construction starter kit is armed, but current cargo does not cover active build costs.";
            }

            Transform origin = survivalSystem != null ? survivalSystem.transform : (toolManager != null ? toolManager.transform : null);
            if (FieldLoadoutAdvisor.TryBuildForwardAdvice(origin, 18f, ~0, out FieldLoadoutAdvisor.LoadoutAdvice advice))
                return $"Recommended field kit: {advice.PresetName}. {advice.Summary}";

            return "Cargo and loadout are synchronized. Use Loadout for slot control and Inventory for assignment/actions.";
        }

        private Color GetFooterHintColor()
        {
            if (toolManager == null)
                return new Color(1f, 0.74f, 0.22f, 0.86f);

            int ready = 0;
            int missing = 0;

            for (int i = 0; i < toolManager.SlotCount; i++)
            {
                GameObject prefab = toolManager.GetAssignedToolPrefab(i);
                if (prefab == null)
                    continue;

                if (toolManager.IsToolAvailableInSlot(i))
                    ready++;
                else
                    missing++;
            }

            if (ready == 0)
                return new Color(1f, 0.74f, 0.22f, 0.9f);

            if (missing > 0)
                return new Color(1f, 0.74f, 0.22f, 0.86f);

            if (playerBuilder != null && playerBuilder.ActiveBuildable != null && !playerBuilder.HasResourcesForActiveBuildable)
                return new Color(1f, 0.74f, 0.22f, 0.86f);

            return DimLow;
        }

        private int CountTotalAvailable(ItemData item)
        {
            return playerInventory != null && item != null ? playerInventory.CountTotal(item) : 0;
        }

        private void EnsureBarterBuffer()
        {
            int required = exchangeSystem != null ? Mathf.Max(4, exchangeSystem.OfferCount) : 4;
            if (_barterBuffer == null || _barterBuffer.Length < required)
                _barterBuffer = new PDAExchangeSystem.OfferSnapshot[required];
        }

        private void EnsureBarterTransactionBuffer()
        {
            const int required = 4;
            if (_barterTxBuffer == null || _barterTxBuffer.Length < required)
                _barterTxBuffer = new PDAExchangeSystem.TransactionSnapshot[required];
        }

        private static void AppendUpper(StringBuilder sb, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            sb.Append(value.ToUpperInvariant());
        }

        private void AppendBuildCostDigest(StringBuilder sb, BuildableData data)
        {
            if (data == null || data.buildCost == null || data.buildCost.Count == 0)
            {
                sb.Append("NONE");
                return;
            }

            bool appended = false;
            for (int i = 0; i < data.buildCost.Count; i++)
            {
                InventoryCost cost = data.buildCost[i];
                if (cost == null || cost.item == null)
                    continue;

                if (appended)
                    sb.Append("  |  ");

                AppendUpper(sb, cost.item.itemName);
                sb.Append(' ');
                sb.Append(CountTotalAvailable(cost.item));
                sb.Append('/');
                sb.Append(cost.amount);
                appended = true;
            }

            if (!appended)
                sb.Append("NONE");
        }

        private void ApplyDirectiveVisuals()
        {
            if (_directiveBg == null || _directiveText == null)
                return;

            float oxygen = survivalSystem != null ? survivalSystem.OxygenNormalized : 0f;
            float energy = survivalSystem != null ? survivalSystem.EnergyNormalized : 0f;
            float integrity = survivalSystem != null ? survivalSystem.IntegrityNormalized : 0f;
            float pressure = survivalSystem != null ? survivalSystem.Pressure : 0f;
            float weight = playerInventory != null ? playerInventory.TotalWeight : 0f;

            if (integrity < 0.35f)
            {
                _directiveBg.color = new Color(0.34f, 0.12f, 0.12f, 0.84f);
                _directiveText.color = new Color(1f, 0.78f, 0.72f, 0.96f);
                return;
            }

            if (oxygen < 0.30f || energy < 0.25f || pressure > 4.5f || weight > 22f)
            {
                _directiveBg.color = new Color(0.3f, 0.2f, 0.06f, 0.82f);
                _directiveText.color = new Color(1f, 0.9f, 0.72f, 0.96f);
                return;
            }

            _directiveBg.color = new Color(0.08f, 0.18f, 0.2f, 0.74f);
            _directiveText.color = Primary;
        }

        private static string FormatPercent(float value)
        {
            return $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f),3}%";
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        private static Image EnsureImage(GameObject target)
        {
            Image image = target.GetComponent<Image>();
            if (image == null)
                image = target.AddComponent<Image>();
            return image;
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font, float size,
            FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Dim;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            Image img = EnsureImage(rect.gameObject);
            img.color = BoxBg;
            img.raycastTarget = false;
            return rect;
        }

        private static TextMeshProUGUI CreateSectionHeader(RectTransform parent, string text)
        {
            TextMeshProUGUI hdr = CreateText(parent, "Header", TMP_Settings.defaultFontAsset, 10.5f,
                FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(hdr.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(12f, -10f), new Vector2(-12f, 16f));
            hdr.color = Primary;
            hdr.SetText(text);
            return hdr;
        }

        private static TextMeshProUGUI CreateBody(RectTransform parent, string name, TMP_FontAsset font)
        {
            TextMeshProUGUI body = CreateText(parent, name, font, 12f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            body.textWrappingMode = TextWrappingModes.NoWrap;
            body.overflowMode = TextOverflowModes.Overflow;
            body.color = Dim;
            return body;
        }

        private static void CreateRule(RectTransform parent, float y)
        {
            RectTransform rect = CreateRect(parent, "Rule");
            rect.anchorMin = new Vector2(0.08f, 1f);
            rect.anchorMax = new Vector2(0.92f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(0f, 1f);
            Image image = EnsureImage(rect.gameObject);
            image.color = Rule;
            image.raycastTarget = false;
        }

        private static void CreateInnerRule(RectTransform parent, float yAnchor)
        {
            RectTransform rect = CreateRect(parent, "InnerRule");
            rect.anchorMin = new Vector2(0.06f, yAnchor);
            rect.anchorMax = new Vector2(0.94f, yAnchor);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 1f);
            Image image = EnsureImage(rect.gameObject);
            image.color = Rule;
            image.raycastTarget = false;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}

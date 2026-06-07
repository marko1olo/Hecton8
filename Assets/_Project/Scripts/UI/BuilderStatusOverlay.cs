using System;
using Hecton.Localization;
using Hecton8.Building;
using Hecton8.Bootstrap;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Builder Status Overlay")]
    public sealed class BuilderStatusOverlay : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const float AutoResolveRetryInterval = 1f;
        private const string ModuleIndexTemplate = "MODULE {0}/{1}  //  BUILT {2}";
        private const int BuildCostDigestCapacity = 32;
        private static ReadOnlySpan<char> TitleChars => "CONSTRUCTION STATUS".AsSpan();
        private static ReadOnlySpan<char> ModuleIndexTemplateChars => ModuleIndexTemplate.AsSpan();
        private static readonly Color PanelColor = new Color(0.03f, 0.1f, 0.12f, 0.66f);
        private static readonly Color RuleColor = new Color(0.2f, 0.86f, 0.96f, 0.38f);
        private static readonly Color TitleColor = new Color(0.52f, 0.97f, 0.95f, 0.96f);

        private static readonly Color ValueColor = new Color(0.9f, 0.98f, 1f, 0.96f);
        private static readonly Color DimColor = new Color(0.58f, 0.77f, 0.82f, 0.8f);
        private static readonly Color ReadyColor = new Color(0.34f, 0.95f, 0.74f, 0.96f);
        private static readonly Color WarnColor = new Color(1f, 0.75f, 0.28f, 0.96f);
        private static readonly Color BlockedColor = new Color(1f, 0.45f, 0.4f, 0.96f);

        [Header("References")]
        [SerializeField] private PlayerBuilder playerBuilder;
        [SerializeField, FormerlySerializedAs("constructionManager")] private MonoBehaviour constructionLogisticsProvider;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerToolManager toolManager;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset numericFont;

        [Header("Authored UI")]
        [SerializeField] private RectTransform _self;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Image _panel;
        [SerializeField] private Image _headerRule;
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private TextMeshProUGUI _moduleName;
        [SerializeField] private TextMeshProUGUI _indexLine;
        [SerializeField] private TextMeshProUGUI _queueLine;
        [SerializeField] private TextMeshProUGUI _placementLine;
        [SerializeField] private TextMeshProUGUI _resourceLine;
        [SerializeField] private TextMeshProUGUI _powerLine;
        [SerializeField] private TextMeshProUGUI _costLine;
        [SerializeField] private TextMeshProUGUI _hintLine;

        [Header("Layout")]
        [SerializeField] private float refreshInterval = 0.1f;

        private float _refreshTimer;
        private float _autoResolveRetryTimer;
        private int _lastStaticStateHash;
        private int _lastLiveStateHash;
        // COLD ALLOC: char[192] - builder overlay module label buffer - owner: BuilderStatusOverlay
        private readonly char[] _moduleBuffer = new char[192];
        // COLD ALLOC: char[64] - builder overlay module index numeric buffer - owner: BuilderStatusOverlay
        private readonly char[] _indexBuffer = new char[64];
        // COLD ALLOC: char[192] - builder overlay queue label buffer - owner: BuilderStatusOverlay
        private readonly char[] _queueBuffer = new char[192];
        // COLD ALLOC: char[128] - builder overlay placement label buffer - owner: BuilderStatusOverlay
        private readonly char[] _placementBuffer = new char[128];
        // COLD ALLOC: char[128] - builder overlay resource label buffer - owner: BuilderStatusOverlay
        private readonly char[] _resourceBuffer = new char[128];
        // COLD ALLOC: char[192] - builder overlay power label buffer - owner: BuilderStatusOverlay
        private readonly char[] _powerBuffer = new char[192];
        // COLD ALLOC: char[256] - builder overlay cost label buffer - owner: BuilderStatusOverlay
        private readonly char[] _costBuffer = new char[256];
        // COLD ALLOC: char[192] - builder overlay hint label buffer - owner: BuilderStatusOverlay
        private readonly char[] _hintBuffer = new char[192];
        private readonly char[] _adviceScratchBuffer = new char[192];
        private bool _tickRegistered;
        private bool _hotSwapListenerRegistered;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private IEnvironmentRuntimeContext _cachedEnvironmentContext;
        private ILogisticsService _constructionLogistics;
        private uint _inventorySignalHash;
        private uint _lastInventorySignalRevision;
        private uint _toolLoadoutSignalSourceId;
        private uint _lastToolLoadoutSignalSequence;
        private int _inventoryRevision;
        private bool _lastVisibleState;
        private bool _uiBound;

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            AutoResolve();
            EnsureBuilt();
            Subscribe();
            ForceRefresh();
            EvaluateTickRegistration();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            UnregisterTick();
        }

        public void LateFrameTick()
        {
            float safeDeltaTime = math.max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            if (!ShouldKeepTicking(safeDeltaTime))
                return;

            if (ConsumeInventoryChangedSignals())
            {
                _inventoryRevision++;
                ForceRefresh();
                return;
            }

            if (ConsumeToolLoadoutChangedSignals())
            {
                ForceRefresh();
                return;
            }

            _refreshTimer -= safeDeltaTime;
            if (_refreshTimer > 0f)
                return;

            _refreshTimer = math.max(0.05f, refreshInterval);
            RefreshState();
        }

        private void ForceRefresh()
        {
            _refreshTimer = 0f;
            _lastStaticStateHash = int.MinValue;
            _lastLiveStateHash = int.MinValue;
            RefreshState();
        }

        private void AutoResolve(float deltaTime = 0f)
        {
            if (deltaTime > 0f && _autoResolveRetryTimer > 0f)
                _autoResolveRetryTimer = math.max(0f, _autoResolveRetryTimer - deltaTime);

            bool requiresRuntimeResolve =
                playerBuilder == null ||
                inventory == null ||
                _constructionLogistics == null ||
                toolManager == null;

            if (requiresRuntimeResolve &&
                (!Application.isPlaying || _autoResolveRetryTimer <= 0f))
            {
                ApplyCachedPlayerContext(forceAssign: false);
                ApplyCachedEnvironmentContext(forceAssign: false);
                _autoResolveRetryTimer = AutoResolveRetryInterval;
            }

            if (labelFont == null)
                labelFont = TMP_Settings.defaultFontAsset;
            if (numericFont == null)
                numericFont = labelFont;

            RefreshSubscriptions();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                UnregisterTick();
                if (isActiveAndEnabled)
                {
                    if (currentService != null)
                        EvaluateTickRegistration();
                }
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                ApplyCachedPlayerContext(forceAssign: true);
                RefreshSubscriptions();
                EvaluateTickRegistration();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Environment)
            {
                _cachedEnvironmentContext = currentService as IEnvironmentRuntimeContext;
                ApplyCachedEnvironmentContext(forceAssign: true);
                EvaluateTickRegistration();
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
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

        private void CacheRegistryServicesCold()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
            _cachedEnvironmentContext = GlobalRegistry.Environment;
        }

        private void ApplyCachedPlayerContext(bool forceAssign)
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext == null)
            {
                if (forceAssign)
                {
                    playerBuilder = null;
                    inventory = null;
                    toolManager = null;
                }

                return;
            }

            if (forceAssign || toolManager == null)
                toolManager = playerContext.ToolManager;

            if (forceAssign || inventory == null)
                inventory = playerContext.Inventory;

            if (forceAssign || playerBuilder == null)
                playerBuilder = playerContext.PlayerBuilder;
        }

        private void ApplyCachedEnvironmentContext(bool forceAssign)
        {
            ILogisticsService providerService = constructionLogisticsProvider as ILogisticsService;
            if (providerService != null)
            {
                _constructionLogistics = providerService;
                return;
            }

            IEnvironmentRuntimeContext environmentContext = _cachedEnvironmentContext;
            if (environmentContext == null)
            {
                return;
            }

            if (forceAssign || _constructionLogistics == null)
            {
                _constructionLogistics = environmentContext.Logistics ?? _constructionLogistics;
            }
        }

        private void RefreshSubscriptions()
        {
            RefreshInventorySignalBinding();
            RefreshToolLoadoutSignalBinding();
        }

        private void Subscribe()
        {
            RefreshSubscriptions();
        }

        private bool ConsumeInventoryChangedSignals()
        {
            uint inventoryHash = _inventorySignalHash;
            if (inventoryHash == 0u)
                return false;

            bool dirty = false;
            ReadOnlySpan<InventoryChangedSignal> signals = SignalBus<InventoryChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ref readonly InventoryChangedSignal signal = ref signals[i];
                if (signal.InventoryHash != inventoryHash)
                    continue;

                if (signal.Revision == _lastInventorySignalRevision && _lastInventorySignalRevision != 0u)
                    continue;

                _lastInventorySignalRevision = signal.Revision;
                dirty = true;
            }

            return dirty;
        }

        private void RefreshInventorySignalBinding()
        {
            uint resolvedHash = ResolveInventorySignalHash(inventory);
            if (_inventorySignalHash == resolvedHash)
                return;

            _inventorySignalHash = resolvedHash;
            _lastInventorySignalRevision = 0u;
        }

        private static uint ResolveInventorySignalHash(PlayerInventory source)
        {
            return source != null && source.gameObject != null
                ? unchecked((uint)EntityId.ToULong(source.gameObject.GetEntityId()))
                : 0u;
        }

        private bool ConsumeToolLoadoutChangedSignals()
        {
            uint sourceId = _toolLoadoutSignalSourceId;
            if (sourceId == 0u)
                return false;

            bool dirty = false;
            ReadOnlySpan<ToolLoadoutChangedSignal> signals = SignalBus<ToolLoadoutChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ref readonly ToolLoadoutChangedSignal signal = ref signals[i];
                if (signal.SourceId != sourceId)
                    continue;

                if (signal.Sequence == _lastToolLoadoutSignalSequence && _lastToolLoadoutSignalSequence != 0u)
                    continue;

                _lastToolLoadoutSignalSequence = signal.Sequence;
                dirty = true;
            }

            return dirty;
        }

        private void RefreshToolLoadoutSignalBinding()
        {
            uint resolvedSourceId = ResolveToolLoadoutSignalSourceId(toolManager);
            if (_toolLoadoutSignalSourceId == resolvedSourceId)
                return;

            _toolLoadoutSignalSourceId = resolvedSourceId;
            _lastToolLoadoutSignalSequence = 0u;
        }

        private static uint ResolveToolLoadoutSignalSourceId(PlayerToolManager manager)
        {
            return manager != null && manager.gameObject != null
                ? RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(manager.gameObject.GetEntityId()))
                : 0u;
        }

        private void EnsureBuilt()
        {
            if (_uiBound)
                return;

            _self = ResolveAuthoredRoot(_self);
            if (_self == null)
                return;

            if (_canvasGroup == null &&
                !_self.TryGetComponent(out _canvasGroup))
            {
                return;
            }

            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            if (_panel == null &&
                !_self.TryGetComponent(out _panel))
            {
                _canvasGroup.alpha = 0f;
                return;
            }

            _panel.color = PanelColor;
            _panel.raycastTarget = false;

            _headerRule = ResolveChildImage(_self, _headerRule, "HeaderRule");
            if (_headerRule == null)
            {
                _canvasGroup.alpha = 0f;
                return;
            }

            _headerRule.color = RuleColor;
            _headerRule.raycastTarget = false;

            _title = ResolveChildText(_self, _title, "Title");
            _moduleName = ResolveChildText(_self, _moduleName, "ModuleName");
            _indexLine = ResolveChildText(_self, _indexLine, "IndexLine");
            _queueLine = ResolveChildText(_self, _queueLine, "QueueLine");
            _placementLine = ResolveChildText(_self, _placementLine, "PlacementLine");
            _resourceLine = ResolveChildText(_self, _resourceLine, "ResourceLine");
            _powerLine = ResolveChildText(_self, _powerLine, "PowerLine");
            _costLine = ResolveChildText(_self, _costLine, "CostLine");
            _hintLine = ResolveChildText(_self, _hintLine, "HintLine");

            if (_title == null ||
                _moduleName == null ||
                _indexLine == null ||
                _queueLine == null ||
                _placementLine == null ||
                _resourceLine == null ||
                _powerLine == null ||
                _costLine == null ||
                _hintLine == null)
            {
                _canvasGroup.alpha = 0f;
                return;
            }

            ConfigureText(_title, labelFont, 11f, FontStyles.Bold, TitleColor, TextAlignmentOptions.Left, TextWrappingModes.NoWrap);
            int titleLength = CopyToBuffer(_indexBuffer, TitleChars);
            SetBufferText(_title, _indexBuffer, titleLength);

            ConfigureText(_moduleName, labelFont, 18f, FontStyles.Bold, ValueColor, TextAlignmentOptions.Left, TextWrappingModes.NoWrap);
            ConfigureText(_indexLine, numericFont, 12f, FontStyles.Bold, DimColor, TextAlignmentOptions.Left, TextWrappingModes.NoWrap);
            ConfigureText(_queueLine, labelFont, 10f, FontStyles.Bold, DimColor, TextAlignmentOptions.Left, TextWrappingModes.NoWrap);
            ConfigureText(_placementLine, numericFont, 13f, FontStyles.Bold, ValueColor, TextAlignmentOptions.Left, TextWrappingModes.NoWrap);
            ConfigureText(_resourceLine, numericFont, 13f, FontStyles.Bold, ValueColor, TextAlignmentOptions.Left, TextWrappingModes.NoWrap);
            ConfigureText(_powerLine, numericFont, 12f, FontStyles.Bold, DimColor, TextAlignmentOptions.Left, TextWrappingModes.NoWrap);
            ConfigureText(_costLine, labelFont, 11f, FontStyles.Normal, DimColor, TextAlignmentOptions.TopLeft, TextWrappingModes.Normal);
            ConfigureText(_hintLine, numericFont, 11f, FontStyles.Bold, TitleColor, TextAlignmentOptions.Left, TextWrappingModes.NoWrap);

            _uiBound = true;
        }

        private void RefreshState()
        {
            if (!_uiBound || _self == null || playerBuilder == null)
                return;

            bool shouldShow = playerBuilder.IsEquipped && playerBuilder.ActiveBuildable != null;
            if (_canvasGroup != null && _lastVisibleState != shouldShow)
            {
                _canvasGroup.alpha = shouldShow ? 1f : 0f;
                _lastVisibleState = shouldShow;
            }

            if (!shouldShow)
                return;

            BuildableData data = playerBuilder.ActiveBuildable;
            bool hasResources = playerBuilder.HasResourcesForActiveBuildable;
            bool canPlace = playerBuilder.CanPlaceActiveBuildable;
            bool snapped = playerBuilder.IsSnapped;
            int activeIndex = playerBuilder.ActiveBuildableIndex;
            int buildCount = playerBuilder.BuildableCount;
            ILogisticsService logistics = _constructionLogistics;
            int builtModuleCount = logistics != null ? logistics.ModuleCount : 0;
            float powerRating = data != null ? data.powerRating : 0f;
            int staticStateHash = ComputeStaticStateHash(data, activeIndex, buildCount, powerRating, builtModuleCount);
            int liveStateHash = ComputeLiveStateHash(hasResources, canPlace, snapped);
            bool staticChanged = staticStateHash != _lastStaticStateHash;
            bool liveChanged = liveStateHash != _lastLiveStateHash;

            if (!staticChanged && !liveChanged)
                return;

            if (staticChanged)
            {
                _lastStaticStateHash = staticStateHash;
                if (data != null)
                {
                    int moduleLength = 0;
                    moduleLength = AppendUpperInvariant(_moduleBuffer, moduleLength, data.moduleName);
                    moduleLength = Append(_moduleBuffer, moduleLength, " [");
                    moduleLength = Append(_moduleBuffer, moduleLength, data.FamilyShortCode);
                    moduleLength = Append(_moduleBuffer, moduleLength, ']');
                    SetBufferText(_moduleName, _moduleBuffer, moduleLength);
                }
                else
                {
                    SetLiteral(_moduleName, _moduleBuffer, "NO MODULE");
                }

                SetNumericText(
                    _indexLine,
                    _indexBuffer,
                    LocNumericArg.Int(activeIndex + 1),
                    LocNumericArg.Int(math.max(1, buildCount)),
                    LocNumericArg.Int(builtModuleCount));
                BuildQueueHint(activeIndex, buildCount);

                int roundedPowerRating = (int)math.round(powerRating);
                int powerLength = 0;
                powerLength = Append(_powerBuffer, powerLength, "ROLE // ");
                powerLength = Append(_powerBuffer, powerLength, playerBuilder.GetActiveBuildRoleLabel());
                if (roundedPowerRating > 0)
                {
                    powerLength = Append(_powerBuffer, powerLength, "  //  +");
                    powerLength = AppendInt(_powerBuffer, powerLength, roundedPowerRating);
                    powerLength = Append(_powerBuffer, powerLength, "W NET");
                }
                else if (roundedPowerRating < 0)
                {
                    powerLength = Append(_powerBuffer, powerLength, "  //  ");
                    powerLength = AppendInt(_powerBuffer, powerLength, roundedPowerRating);
                    powerLength = Append(_powerBuffer, powerLength, "W LOAD");
                }

                SetBufferText(_powerLine, _powerBuffer, powerLength);

                BuildCostSummary(data, hasResources);
            }

            if (liveChanged)
            {
                _lastLiveStateHash = liveStateHash;
                if (!hasResources)
                {
                    _placementLine.color = BlockedColor;
                    SetLiteral(_placementLine, _placementBuffer, "PLACEMENT // HOLD - MISSING COST");
                }
                else if (!canPlace)
                {
                    _placementLine.color = WarnColor;
                    SetLiteral(_placementLine, _placementBuffer, snapped ? "PLACEMENT // SOCKET BLOCKED" : "PLACEMENT // BLOCKED");
                }
                else if (snapped)
                {
                    _placementLine.color = ReadyColor;
                    SetLiteral(_placementLine, _placementBuffer, "PLACEMENT // SNAPPED READY");
                }
                else
                {
                    _placementLine.color = TitleColor;
                    SetLiteral(_placementLine, _placementBuffer, "PLACEMENT // READY");
                }

                _resourceLine.color = hasResources ? ReadyColor : WarnColor;
                SetLiteral(_resourceLine, _resourceBuffer, hasResources ? "RESOURCES // READY" : "RESOURCES // INSUFFICIENT");
                _costLine.color = hasResources ? DimColor : WarnColor;
                FixedCharBuffer adviceBuffer = new FixedCharBuffer(_adviceScratchBuffer);
                adviceBuffer.Clear();
                playerBuilder.WriteActiveBuildAdvice(ref adviceBuffer);
                int hintLength = AppendUpperInvariant(_hintBuffer, 0, adviceBuffer.AsSpan());
                SetBufferText(_hintLine, _hintBuffer, hintLength);
            }
        }

        private bool ShouldKeepTicking(float deltaTime = 0f)
        {
            if (deltaTime > 0f && _autoResolveRetryTimer > 0f)
                _autoResolveRetryTimer = math.max(0f, _autoResolveRetryTimer - deltaTime);

            return RequiresRuntimeResolve() || _toolLoadoutSignalSourceId != 0u || IsBuilderOverlayVisible();
        }

        private bool RequiresRuntimeResolve()
        {
            return playerBuilder == null ||
                   inventory == null ||
                   _constructionLogistics == null ||
                   toolManager == null;
        }

        private bool IsBuilderOverlayVisible()
        {
            if (playerBuilder != null)
                return playerBuilder.IsEquipped && playerBuilder.ActiveBuildable != null;

            return toolManager != null && toolManager.CurrentTool is BuilderTool;
        }

        private void EvaluateTickRegistration()
        {
            if (!isActiveAndEnabled || !_uiBound)
            {
                UnregisterTick();
                return;
            }

            RegisterTick();
        }

        private void RegisterTick()
        {
            if (_tickRegistered || !Application.isPlaying)
                return;

            _tickRegistered = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void UnregisterTick()
        {
            if (!_tickRegistered)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
            _tickRegistered = false;
        }

        private int ComputeStaticStateHash(BuildableData data, int activeIndex, int buildCount, float powerRating, int builtModuleCount)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (data != null ? unchecked((int)EntityId.ToULong(data.GetEntityId())) : 0);
                hash = hash * 31 + activeIndex;
                hash = hash * 31 + buildCount;
                hash = hash * 31 + builtModuleCount;
                hash = hash * 31 + (int)math.round(powerRating * 10f);
                hash = hash * 31 + _inventoryRevision;
                if (data != null && data.buildCost != null && inventory != null)
                {
                    for (int i = 0; i < data.buildCost.Count; i++)
                    {
                        var cost = data.buildCost[i];
                        if (cost == null || cost.item == null)
                            continue;
                        hash = hash * 31 + unchecked((int)EntityId.ToULong(cost.item.GetEntityId()));
                        hash = hash * 31 + cost.amount;
                    }
                }
                return hash;
            }
        }

        private static int ComputeLiveStateHash(bool hasResources, bool canPlace, bool snapped)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (hasResources ? 1 : 0);
                hash = hash * 31 + (canPlace ? 1 : 0);
                hash = hash * 31 + (snapped ? 1 : 0);
                return hash;
            }
        }

        private void BuildQueueHint(int activeIndex, int buildCount)
        {
            if (_queueLine == null)
                return;

            if (playerBuilder == null || buildCount <= 0)
            {
                SetLiteral(_queueLine, _queueBuffer, "CATALOG // OFFLINE");
                return;
            }

            BuildableData prev = playerBuilder.GetRelativeBuildable(-1);
            BuildableData next = playerBuilder.GetRelativeBuildable(1);

            int length = 0;
            length = Append(_queueBuffer, length, "QUEUE // ");
            if (prev != null)
                length = AppendUpperInvariant(_queueBuffer, length, prev.moduleName);
            else
                length = Append(_queueBuffer, length, "NONE");
            length = Append(_queueBuffer, length, "  <  ");
            length = AppendInt(_queueBuffer, length, activeIndex + 1);
            length = Append(_queueBuffer, length, "  >  ");
            if (next != null)
                length = AppendUpperInvariant(_queueBuffer, length, next.moduleName);
            else
                length = Append(_queueBuffer, length, "NONE");
            SetBufferText(_queueLine, _queueBuffer, length);
        }

        private void BuildCostSummary(BuildableData data, bool hasResources)
        {
            if (_costLine == null)
                return;

            if (data == null || data.buildCost == null || data.buildCost.Count == 0)
            {
                _costLine.color = DimColor;
                SetLiteral(_costLine, _costBuffer, "COST // NONE");
                return;
            }

            int length = 0;
            length = Append(_costBuffer, length, "COST // ");

            Span<int> costHashes = stackalloc int[BuildCostDigestCapacity];
            Span<int> costAmounts = stackalloc int[BuildCostDigestCapacity];
            Span<int> costIndices = stackalloc int[BuildCostDigestCapacity];
            int groupedCostCount = PrepareBuildCostDigestGroups(data, costHashes, costAmounts, costIndices);
            if (groupedCostCount < 0)
            {
                _costLine.color = WarnColor;
                SetLiteral(_costLine, _costBuffer, "COST // OVERFLOW");
                return;
            }

            int appendedCosts = 0;
            for (int i = 0; i < groupedCostCount; i++)
            {
                var cost = data.buildCost[costIndices[i]];
                if (cost == null || cost.item == null)
                    continue;

                int owned = inventory != null
                    ? inventory.CountAvailableTotal(costHashes[i])
                    : 0;
                if (appendedCosts > 0)
                    length = Append(_costBuffer, length, "  |  ");

                length = Append(_costBuffer, length, cost.item.itemName);
                length = Append(_costBuffer, length, ' ');
                length = AppendInt(_costBuffer, length, owned);
                length = Append(_costBuffer, length, '/');
                length = AppendInt(_costBuffer, length, costAmounts[i]);
                appendedCosts++;
            }

            if (appendedCosts == 0)
                length = Append(_costBuffer, length, "NONE");

            _costLine.color = hasResources ? DimColor : WarnColor;
            SetBufferText(_costLine, _costBuffer, length);
        }

        private static int PrepareBuildCostDigestGroups(
            BuildableData data,
            Span<int> costHashes,
            Span<int> costAmounts,
            Span<int> costIndices)
        {
            if (data == null || data.buildCost == null || data.buildCost.Count == 0)
                return 0;

            int groupedCount = 0;
            int sourceCount = data.buildCost.Count;
            for (int i = 0; i < sourceCount; i++)
            {
                var cost = data.buildCost[i];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                int itemHashId = cost.item.PersistentHashId;
                if (itemHashId == 0)
                    continue;

                int groupIndex = FindBuildCostDigestGroup(costHashes, groupedCount, itemHashId);
                if (groupIndex < 0)
                {
                    if (groupedCount >= costHashes.Length ||
                        groupedCount >= costAmounts.Length ||
                        groupedCount >= costIndices.Length)
                    {
                        return -1;
                    }

                    groupIndex = groupedCount;
                    costHashes[groupIndex] = itemHashId;
                    costAmounts[groupIndex] = 0;
                    costIndices[groupIndex] = i;
                    groupedCount++;
                }

                int current = costAmounts[groupIndex];
                if (current > int.MaxValue - cost.amount)
                    return -1;

                costAmounts[groupIndex] = current + cost.amount;
            }

            return groupedCount;
        }

        private static int FindBuildCostDigestGroup(Span<int> costHashes, int groupedCount, int itemHashId)
        {
            for (int i = 0; i < groupedCount; i++)
            {
                if (costHashes[i] == itemHashId)
                    return i;
            }

            return -1;
        }

        private RectTransform ResolveAuthoredRoot(RectTransform configuredRoot)
        {
            if (configuredRoot != null)
                return configuredRoot;

            RectTransform localRoot = transform as RectTransform;
            if (localRoot != null)
                return localRoot;

            Transform child = transform.Find("BuilderStatusOverlay_Root");
            return child as RectTransform;
        }

        private static Image ResolveChildImage(RectTransform root, Image configuredImage, string childName)
        {
            if (configuredImage != null)
                return configuredImage;

            Transform child = root != null ? root.Find(childName) : null;
            if (child == null)
                return null;

            return child.TryGetComponent(out Image image) ? image : null;
        }

        private static TextMeshProUGUI ResolveChildText(RectTransform root, TextMeshProUGUI configuredText, string childName)
        {
            if (configuredText != null)
                return configuredText;

            Transform child = root != null ? root.Find(childName) : null;
            if (child == null)
                return null;

            return child.TryGetComponent(out TextMeshProUGUI text) ? text : null;
        }

        private static void ConfigureText(TextMeshProUGUI text, TMP_FontAsset font, float size, FontStyles style, Color color, TextAlignmentOptions alignment, TextWrappingModes wrappingMode)
        {
            if (text == null)
                return;

            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = wrappingMode;
        }

        private static void SetNumericText(TextMeshProUGUI label, char[] destination, LocNumericArg value0, LocNumericArg value1, LocNumericArg value2)
        {
            if (label == null || destination == null)
                return;

            if (!LocNumericBuffer.TryWrite(ModuleIndexTemplateChars, destination.AsSpan(), value0, value1, value2, out int length))
            {
                length = 0;
            }

            SetBufferText(label, destination, length);
        }

        private static void SetLiteral(TextMeshProUGUI label, char[] buffer, string value)
        {
            int length = CopyToBuffer(buffer, value);
            SetBufferText(label, buffer, length);
        }

        private static void SetBufferText(TextMeshProUGUI label, char[] buffer, int length)
        {
            if (label == null || buffer == null)
                return;

            int safeLength = math.clamp(length, 0, buffer.Length);
            label.SetCharArray(buffer, 0, safeLength);
        }

        private static int CopyToBuffer(char[] buffer, string value)
        {
            if (buffer == null || string.IsNullOrEmpty(value))
                return 0;

            int length = math.min(value.Length, buffer.Length);
            value.AsSpan(0, length).CopyTo(buffer.AsSpan());
            return length;
        }

        private static int CopyToBuffer(char[] buffer, ReadOnlySpan<char> value)
        {
            if (buffer == null || value.Length <= 0)
                return 0;

            int length = math.min(value.Length, buffer.Length);
            value.Slice(0, length).CopyTo(buffer.AsSpan());
            return length;
        }

        private static int Append(char[] buffer, int index, string value)
        {
            if (buffer == null || string.IsNullOrEmpty(value) || index >= buffer.Length)
                return math.clamp(index, 0, buffer != null ? buffer.Length : 0);

            int length = math.min(value.Length, buffer.Length - index);
            value.AsSpan(0, length).CopyTo(buffer.AsSpan(index));
            return index + length;
        }

        private static int Append(char[] buffer, int index, char value)
        {
            if (buffer == null)
                return 0;

            if (index < 0)
                index = 0;
            if (index >= buffer.Length)
                return buffer.Length;

            buffer[index] = value;
            return index + 1;
        }

        private static int AppendUpperInvariant(char[] buffer, int index, string value)
        {
            if (buffer == null || string.IsNullOrEmpty(value))
                return math.clamp(index, 0, buffer != null ? buffer.Length : 0);

            if (index < 0)
                index = 0;

            int length = math.min(value.Length, buffer.Length - index);
            for (int i = 0; i < length; i++)
            {
                buffer[index + i] = ToAsciiUpperInvariant(value[i]);
            }

            return index + length;
        }

        private static int AppendUpperInvariant(char[] buffer, int index, ReadOnlySpan<char> value)
        {
            if (buffer == null || value.IsEmpty)
                return math.clamp(index, 0, buffer != null ? buffer.Length : 0);

            if (index < 0)
                index = 0;

            int length = math.min(value.Length, buffer.Length - index);
            for (int i = 0; i < length; i++)
            {
                buffer[index + i] = ToAsciiUpperInvariant(value[i]);
            }

            return index + length;
        }

        private static char ToAsciiUpperInvariant(char value)
        {
            return value >= 'a' && value <= 'z' ? (char)(value - 32) : value;
        }

        private static int AppendInt(char[] buffer, int index, int value)
        {
            if (buffer == null)
                return 0;

            if (index < 0)
                index = 0;
            if (index >= buffer.Length)
                return buffer.Length;

            if (!value.TryFormat(buffer.AsSpan(index), out int written))
                return index;

            return index + written;
        }
    }
}

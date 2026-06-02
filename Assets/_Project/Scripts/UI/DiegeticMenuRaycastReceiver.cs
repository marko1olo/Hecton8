using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Diegetic Menu Raycast Receiver")]
    public sealed class DiegeticMenuRaycastReceiver : MonoBehaviour, IPanelInteractable
    {
        private enum ControlKind : byte
        {
            None = 0,
            Button = 1,
            Toggle = 2,
            Slider = 3
        }

        private enum RaycastItemKind : byte
        {
            None = 0,
            Control = 1,
            GraphicBlocker = 2,
            CanvasGroupBlocker = 3
        }

        private const int MaxControlTargets = 128;
        private const int MaxRaycastItems = 256;
        private const float AlphaVisibleThreshold = 0.01f;
        private const float SliderAxisEpsilon = 0.0001f;
        private const byte MenuHapticChannel = HapticRequest.ChannelMicroVibration;
        private const byte MenuHapticFlags = HapticRequest.FlagMicroVibration;
        private const byte MenuHoverAcousticChannel = AcousticPingSignal.ChannelGloveScrape;
        private const byte MenuHoverAcousticFlags = AcousticPingSignal.FlagGloveScrape;
        private const byte MenuClickAcousticChannel = AcousticPingSignal.ChannelMetalStress;
        private const int MaxCanvasGroupsPerControl = 8;
        private const byte CanvasGroupCacheOverflow = byte.MaxValue;
        private const int NoPendingSelection = -2;

        // COLD ALLOC: fixed diegetic menu selectable target cache - owner: DiegeticMenuRaycastReceiver
        private readonly Selectable[] _selectables = new Selectable[MaxControlTargets];
        // COLD ALLOC: fixed diegetic menu button cache - owner: DiegeticMenuRaycastReceiver
        private readonly Button[] _buttons = new Button[MaxControlTargets];
        // COLD ALLOC: fixed diegetic menu toggle cache - owner: DiegeticMenuRaycastReceiver
        private readonly Toggle[] _toggles = new Toggle[MaxControlTargets];
        // COLD ALLOC: fixed diegetic menu slider cache - owner: DiegeticMenuRaycastReceiver
        private readonly Slider[] _sliders = new Slider[MaxControlTargets];
        // COLD ALLOC: fixed diegetic menu selectable rect cache - owner: DiegeticMenuRaycastReceiver
        private readonly RectTransform[] _controlRects = new RectTransform[MaxControlTargets];
        // COLD ALLOC: flattened parent CanvasGroup cache - owner: DiegeticMenuRaycastReceiver
        private readonly CanvasGroup[] _controlCanvasGroups = new CanvasGroup[MaxControlTargets * MaxCanvasGroupsPerControl];
        // COLD ALLOC: CanvasGroup count per selectable cache - owner: DiegeticMenuRaycastReceiver
        private readonly byte[] _controlCanvasGroupCounts = new byte[MaxControlTargets];
        // COLD ALLOC: selectable kind cache - owner: DiegeticMenuRaycastReceiver
        private readonly ControlKind[] _controlKinds = new ControlKind[MaxControlTargets];
        // COLD ALLOC: combined visual raycast stack cache - owner: DiegeticMenuRaycastReceiver
        private readonly RectTransform[] _raycastItemRects = new RectTransform[MaxRaycastItems];
        // COLD ALLOC: combined visual raycast item kind cache - owner: DiegeticMenuRaycastReceiver
        private readonly RaycastItemKind[] _raycastItemKinds = new RaycastItemKind[MaxRaycastItems];
        // COLD ALLOC: combined visual raycast item selectable index cache - owner: DiegeticMenuRaycastReceiver
        private readonly int[] _raycastItemControlIndices = new int[MaxRaycastItems];
        // COLD ALLOC: modal/decorative blocker graphic cache - owner: DiegeticMenuRaycastReceiver
        private readonly Graphic[] _raycastItemGraphics = new Graphic[MaxRaycastItems];
        // COLD ALLOC: flattened CanvasGroup cache for blocker raycast items - owner: DiegeticMenuRaycastReceiver
        private readonly CanvasGroup[] _raycastItemCanvasGroups = new CanvasGroup[MaxRaycastItems * MaxCanvasGroupsPerControl];
        // COLD ALLOC: CanvasGroup count per blocker raycast item - owner: DiegeticMenuRaycastReceiver
        private readonly byte[] _raycastItemCanvasGroupCounts = new byte[MaxRaycastItems];
        private RectTransform _canvasRoot;
        private EventSystem _eventSystem;
        private int _hoverControlIndex = -1;
        private int _pressedControlIndex = -1;
        private int _pendingSelectionControlIndex = NoPendingSelection;
        private int _controlCount;
        private int _raycastItemCount;
        private bool _raycastItemOverflow;
        private uint _hapticSourceHash = 0x444D454Eu; // DMEN
        private float _referenceWidth = DiegeticMenuCanvasUtility.ReferenceWidth;
        private float _referenceHeight = DiegeticMenuCanvasUtility.ReferenceHeight;
        private int _hapticDropCount;
        private int _acousticDropCount;

        internal void Configure(RectTransform canvasRoot, EventSystem eventSystem, uint hapticSourceHash)
        {
            _canvasRoot = canvasRoot;
            _eventSystem = eventSystem;
            _hapticSourceHash = hapticSourceHash != 0u ? hapticSourceHash : _hapticSourceHash;
            _referenceWidth = DiegeticMenuCanvasUtility.ReferenceWidth;
            _referenceHeight = DiegeticMenuCanvasUtility.ReferenceHeight;
            RebuildButtonCache();
        }

        internal void RebuildButtonCache()
        {
            ClearControlCache();
            ClearInteractionState();
            CacheControlsRecursive(_canvasRoot);
        }

        private void ClearControlCache()
        {
            int cachedControlCount = _controlCount;
            int cachedRaycastItemCount = _raycastItemCount;
            for (int i = 0; i < cachedControlCount; i++)
            {
                _selectables[i] = null;
                _buttons[i] = null;
                _toggles[i] = null;
                _sliders[i] = null;
                _controlRects[i] = null;
                _controlCanvasGroupCounts[i] = 0;
                _controlKinds[i] = ControlKind.None;

                int baseIndex = i * MaxCanvasGroupsPerControl;
                for (int groupIndex = 0; groupIndex < MaxCanvasGroupsPerControl; groupIndex++)
                    _controlCanvasGroups[baseIndex + groupIndex] = null;
            }

            for (int i = 0; i < cachedRaycastItemCount; i++)
            {
                _raycastItemRects[i] = null;
                _raycastItemKinds[i] = RaycastItemKind.None;
                _raycastItemControlIndices[i] = -1;
                _raycastItemGraphics[i] = null;
                _raycastItemCanvasGroupCounts[i] = 0;

                int baseIndex = i * MaxCanvasGroupsPerControl;
                for (int groupIndex = 0; groupIndex < MaxCanvasGroupsPerControl; groupIndex++)
                    _raycastItemCanvasGroups[baseIndex + groupIndex] = null;
            }

            _controlCount = 0;
            _raycastItemCount = 0;
            _raycastItemOverflow = false;
        }

        private void ClearInteractionState()
        {
            _hoverControlIndex = -1;
            _pressedControlIndex = -1;
            _pendingSelectionControlIndex = -1;
        }

        public void ReceiveCanvasInput(in DiegeticPanelInputEvent inputEvent)
        {
            if (_canvasRoot == null || _controlCount <= 0)
            {
                ClearInteractionState();
                return;
            }

            DiegeticPanelInputEventType eventType = inputEvent.EventType;
            if ((eventType & (DiegeticPanelInputEventType.Hover |
                              DiegeticPanelInputEventType.Down |
                              DiegeticPanelInputEventType.Hold |
                              DiegeticPanelInputEventType.Up)) == 0)
            {
                return;
            }

            DiegeticPanelInputEventType pointerAction = ResolvePrimaryPointerAction(eventType);
            if (pointerAction == DiegeticPanelInputEventType.None)
                return;

            int targetIndex = ResolveControlIndex(inputEvent.CanvasHitPoint);
            UpdateHover(targetIndex);

            if (pointerAction == DiegeticPanelInputEventType.Up)
            {
                int pressedIndex = _pressedControlIndex;
                _pressedControlIndex = -1;
                if (pressedIndex < 0)
                    return;

                if (IsSliderEligible(pressedIndex))
                {
                    if (!TryApplySliderValue(pressedIndex, inputEvent.CanvasHitPoint))
                        return;

                    PublishHaptic(0.10f, 0.032f);
                    PublishAcoustic(pressedIndex, 0.38f, 3.2f, MenuClickAcousticChannel, 0);
                    return;
                }

                if (targetIndex < 0 || targetIndex != pressedIndex || !IsControlEligible(targetIndex))
                    return;

                PublishHaptic(0.14f, 0.045f);
                PublishAcoustic(targetIndex, 0.62f, 4.5f, MenuClickAcousticChannel, 0);
                InvokeControl(targetIndex);
                return;
            }

            if (pointerAction == DiegeticPanelInputEventType.Down)
            {
                if (targetIndex < 0)
                {
                    _pressedControlIndex = -1;
                    return;
                }

                bool targetIsSlider = IsSliderEligible(targetIndex);
                if (targetIsSlider && !TryApplySliderValue(targetIndex, inputEvent.CanvasHitPoint))
                {
                    _pressedControlIndex = -1;
                    return;
                }

                _pressedControlIndex = targetIndex;
                PublishHaptic(0.08f, 0.035f);

                if (targetIsSlider)
                    PublishAcoustic(targetIndex, 0.34f, 3.2f, MenuClickAcousticChannel, 0);
                return;
            }

            if (pointerAction == DiegeticPanelInputEventType.Hold)
            {
                int pressedIndex = _pressedControlIndex;
                if (IsSliderEligible(pressedIndex))
                    TryApplySliderValue(pressedIndex, inputEvent.CanvasHitPoint);
            }
        }

        private static DiegeticPanelInputEventType ResolvePrimaryPointerAction(DiegeticPanelInputEventType eventType)
        {
            if ((eventType & DiegeticPanelInputEventType.Up) != 0)
                return DiegeticPanelInputEventType.Up;

            if ((eventType & DiegeticPanelInputEventType.Down) != 0)
                return DiegeticPanelInputEventType.Down;

            if ((eventType & DiegeticPanelInputEventType.Hold) != 0)
                return DiegeticPanelInputEventType.Hold;

            if ((eventType & DiegeticPanelInputEventType.Hover) != 0)
                return DiegeticPanelInputEventType.Hover;

            return DiegeticPanelInputEventType.None;
        }

        private void CacheControlsRecursive(Transform node)
        {
            if (node == null)
                return;

            bool cachedControl = false;
            if (node.TryGetComponent(out Button button) && button != null)
            {
                cachedControl = CacheControl(button, ControlKind.Button);
            }
            else if (node.TryGetComponent(out Toggle toggle) && toggle != null)
            {
                cachedControl = CacheControl(toggle, ControlKind.Toggle);
            }
            else if (node.TryGetComponent(out Slider slider) && slider != null)
            {
                cachedControl = CacheControl(slider, ControlKind.Slider);
            }

            if (!cachedControl && !CacheGraphicBlocker(node))
                CacheCanvasGroupBlocker(node);

            int childCount = node.childCount;
            for (int i = 0; i < childCount; i++)
                CacheControlsRecursive(node.GetChild(i));
        }

        private bool CacheControl(Selectable selectable, ControlKind kind)
        {
            if (_controlCount >= MaxControlTargets)
            {
                _raycastItemOverflow = true;
                return false;
            }

            int index = _controlCount++;
            _selectables[index] = selectable;
            _controlKinds[index] = kind;
            _controlRects[index] = selectable.transform as RectTransform;
            if (kind == ControlKind.Button)
                _buttons[index] = selectable as Button;
            else if (kind == ControlKind.Toggle)
                _toggles[index] = selectable as Toggle;
            else if (kind == ControlKind.Slider)
                _sliders[index] = selectable as Slider;

            CacheCanvasGroups(index, selectable.transform);
            CacheRaycastControl(index);
            return true;
        }

        private void CacheCanvasGroups(int controlIndex, Transform start)
        {
            int count = 0;
            bool overflow = false;
            Transform current = start;
            int baseIndex = controlIndex * MaxCanvasGroupsPerControl;
            while (current != null)
            {
                if (current.TryGetComponent(out CanvasGroup group) && group != null)
                {
                    if (count < MaxCanvasGroupsPerControl)
                    {
                        _controlCanvasGroups[baseIndex + count++] = group;
                    }
                    else
                    {
                        overflow = true;
                    }

                    if (group.ignoreParentGroups)
                        break;
                }

                if (current == _canvasRoot)
                    break;

                current = current.parent;
            }

            _controlCanvasGroupCounts[controlIndex] = overflow ? CanvasGroupCacheOverflow : (byte)count;
        }

        private void CacheRaycastControl(int controlIndex)
        {
            if (_raycastItemCount >= MaxRaycastItems)
            {
                _raycastItemOverflow = true;
                return;
            }

            int itemIndex = _raycastItemCount++;
            _raycastItemKinds[itemIndex] = RaycastItemKind.Control;
            _raycastItemControlIndices[itemIndex] = controlIndex;
            _raycastItemRects[itemIndex] = _controlRects[controlIndex];
            _raycastItemGraphics[itemIndex] = null;
            _raycastItemCanvasGroupCounts[itemIndex] = 0;
        }

        private bool CacheGraphicBlocker(Transform node)
        {
            if (_raycastItemCount >= MaxRaycastItems)
            {
                _raycastItemOverflow = true;
                return false;
            }

            if (!(node is RectTransform rect))
                return false;

            if (!node.TryGetComponent(out Graphic graphic) || graphic == null || !graphic.raycastTarget)
                return false;

            if (HasInteractiveAncestor(node))
                return false;

            int itemIndex = _raycastItemCount++;
            _raycastItemKinds[itemIndex] = RaycastItemKind.GraphicBlocker;
            _raycastItemControlIndices[itemIndex] = -1;
            _raycastItemRects[itemIndex] = rect;
            _raycastItemGraphics[itemIndex] = graphic;
            CacheRaycastItemCanvasGroups(itemIndex, node);
            return true;
        }

        private void CacheCanvasGroupBlocker(Transform node)
        {
            if (_raycastItemCount >= MaxRaycastItems)
            {
                _raycastItemOverflow = true;
                return;
            }

            if (!(node is RectTransform rect))
                return;

            if (!node.TryGetComponent(out CanvasGroup group) || group == null)
                return;

            if (HasInteractiveAncestor(node))
                return;

            int itemIndex = _raycastItemCount++;
            _raycastItemKinds[itemIndex] = RaycastItemKind.CanvasGroupBlocker;
            _raycastItemControlIndices[itemIndex] = -1;
            _raycastItemRects[itemIndex] = rect;
            _raycastItemGraphics[itemIndex] = null;
            CacheRaycastItemCanvasGroups(itemIndex, node);
        }

        private static bool HasInteractiveAncestor(Transform node)
        {
            Transform current = node;
            while (current != null)
            {
                if (current.TryGetComponent(out Selectable selectable) && selectable != null)
                    return true;

                if (current.TryGetComponent(out ScrollRect scrollRect) && scrollRect != null)
                    return true;

                current = current.parent;
            }

            return false;
        }

        private void CacheRaycastItemCanvasGroups(int itemIndex, Transform start)
        {
            int count = 0;
            bool overflow = false;
            Transform current = start;
            int baseIndex = itemIndex * MaxCanvasGroupsPerControl;
            while (current != null)
            {
                if (current.TryGetComponent(out CanvasGroup group) && group != null)
                {
                    if (count < MaxCanvasGroupsPerControl)
                    {
                        _raycastItemCanvasGroups[baseIndex + count++] = group;
                    }
                    else
                    {
                        overflow = true;
                    }

                    if (group.ignoreParentGroups)
                        break;
                }

                if (current == _canvasRoot)
                    break;

                current = current.parent;
            }

            _raycastItemCanvasGroupCounts[itemIndex] = overflow ? CanvasGroupCacheOverflow : (byte)count;
        }

        private int ResolveControlIndex(float2 canvasHitPoint)
        {
            if (_raycastItemOverflow || !IsCanvasHitPointInsideReference(canvasHitPoint))
                return -1;

            Vector3 worldPoint = CanvasPointToWorld(canvasHitPoint);
            for (int i = _raycastItemCount - 1; i >= 0; i--)
            {
                if (!IsRaycastItemEligible(i))
                    continue;

                RectTransform rect = _raycastItemRects[i];
                if (rect == null)
                    continue;

                Vector3 localPoint = rect.InverseTransformPoint(worldPoint);
                if (rect.rect.Contains(new Vector2(localPoint.x, localPoint.y)))
                    return _raycastItemKinds[i] == RaycastItemKind.Control ? _raycastItemControlIndices[i] : -1;
            }

            return -1;
        }

        private Vector3 CanvasPointToWorld(float2 canvasHitPoint)
        {
            return _canvasRoot.TransformPoint(new Vector3(
                canvasHitPoint.x - (_referenceWidth * 0.5f),
                canvasHitPoint.y - (_referenceHeight * 0.5f),
                0f));
        }

        private bool IsSliderEligible(int controlIndex)
        {
            return controlIndex >= 0 &&
                   controlIndex < _controlCount &&
                   _controlKinds[controlIndex] == ControlKind.Slider &&
                   IsControlEligible(controlIndex);
        }

        private bool IsRaycastItemEligible(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= _raycastItemCount)
                return false;

            RaycastItemKind kind = _raycastItemKinds[itemIndex];
            if (kind == RaycastItemKind.Control)
                return IsControlEligible(_raycastItemControlIndices[itemIndex]);

            return (kind == RaycastItemKind.GraphicBlocker || kind == RaycastItemKind.CanvasGroupBlocker) &&
                   IsRaycastBlockerEligible(itemIndex);
        }

        private bool IsRaycastBlockerEligible(int itemIndex)
        {
            RectTransform rect = _raycastItemRects[itemIndex];
            if (rect == null || !rect.gameObject.activeInHierarchy)
                return false;

            Graphic graphic = _raycastItemGraphics[itemIndex];
            if (graphic != null && (!graphic.isActiveAndEnabled || !graphic.raycastTarget))
                return false;

            int baseIndex = itemIndex * MaxCanvasGroupsPerControl;
            int groupCount = _raycastItemCanvasGroupCounts[itemIndex];
            if (groupCount == CanvasGroupCacheOverflow)
                return true;

            for (int i = 0; i < groupCount; i++)
            {
                CanvasGroup group = _raycastItemCanvasGroups[baseIndex + i];
                if (group == null)
                    continue;

                if (group.alpha <= AlphaVisibleThreshold || !group.blocksRaycasts)
                    return false;

                if (group.ignoreParentGroups)
                    break;
            }

            return true;
        }

        private bool IsControlEligible(int controlIndex)
        {
            if (controlIndex < 0 || controlIndex >= _controlCount)
                return false;

            Selectable selectable = _selectables[controlIndex];
            if (selectable == null || !selectable.isActiveAndEnabled || !selectable.interactable)
                return false;

            int baseIndex = controlIndex * MaxCanvasGroupsPerControl;
            int groupCount = _controlCanvasGroupCounts[controlIndex];
            if (groupCount == CanvasGroupCacheOverflow)
                return false;

            for (int i = 0; i < groupCount; i++)
            {
                CanvasGroup group = _controlCanvasGroups[baseIndex + i];
                if (group == null)
                    continue;

                if (group.alpha <= AlphaVisibleThreshold || !group.interactable || !group.blocksRaycasts)
                    return false;

                if (group.ignoreParentGroups)
                    break;
            }

            return true;
        }

        private bool TryApplySliderValue(int controlIndex, float2 canvasHitPoint)
        {
            Slider slider = _sliders[controlIndex];
            RectTransform rect = _controlRects[controlIndex];
            if (slider == null || rect == null)
                return false;

            if (!IsCanvasHitPointInsideReference(canvasHitPoint))
                return false;

            Vector3 localPoint = rect.InverseTransformPoint(CanvasPointToWorld(canvasHitPoint));
            if (!math.all(math.isfinite(new float2(localPoint.x, localPoint.y))))
                return false;

            Rect sliderRect = rect.rect;
            float normalized;
            switch (slider.direction)
            {
                case Slider.Direction.RightToLeft:
                    if (sliderRect.width <= SliderAxisEpsilon)
                        return false;
                    normalized = (sliderRect.xMax - localPoint.x) / sliderRect.width;
                    break;
                case Slider.Direction.BottomToTop:
                    if (sliderRect.height <= SliderAxisEpsilon)
                        return false;
                    normalized = (localPoint.y - sliderRect.yMin) / sliderRect.height;
                    break;
                case Slider.Direction.TopToBottom:
                    if (sliderRect.height <= SliderAxisEpsilon)
                        return false;
                    normalized = (sliderRect.yMax - localPoint.y) / sliderRect.height;
                    break;
                default:
                    if (sliderRect.width <= SliderAxisEpsilon)
                        return false;
                    normalized = (localPoint.x - sliderRect.xMin) / sliderRect.width;
                    break;
            }

            if (!math.isfinite(normalized))
                return false;

            slider.normalizedValue = math.saturate(normalized);
            return true;
        }

        private bool IsCanvasHitPointInsideReference(float2 canvasHitPoint)
        {
            if (!math.all(math.isfinite(canvasHitPoint)))
                return false;

            return canvasHitPoint.x >= 0f &&
                   canvasHitPoint.y >= 0f &&
                   canvasHitPoint.x <= _referenceWidth &&
                   canvasHitPoint.y <= _referenceHeight;
        }

        private void InvokeControl(int targetIndex)
        {
            ControlKind kind = _controlKinds[targetIndex];
            if (kind == ControlKind.Button)
            {
                Button button = _buttons[targetIndex];
                if (button != null)
                    button.onClick.Invoke();
                return;
            }

            if (kind == ControlKind.Toggle)
            {
                Toggle toggle = _toggles[targetIndex];
                if (toggle != null)
                    toggle.isOn = !toggle.isOn;
            }
        }

        private void UpdateHover(int targetIndex)
        {
            if (_hoverControlIndex == targetIndex)
                return;

            _hoverControlIndex = targetIndex;
            _pendingSelectionControlIndex = targetIndex;
            if (targetIndex >= 0)
                PublishAcoustic(targetIndex, 0.18f, 1.6f, MenuHoverAcousticChannel, MenuHoverAcousticFlags);
        }

        internal void FlushPendingSelection()
        {
            int targetIndex = _pendingSelectionControlIndex;
            if (targetIndex == NoPendingSelection)
                return;

            _pendingSelectionControlIndex = NoPendingSelection;
            Selectable target = IsControlEligible(targetIndex) ? _selectables[targetIndex] : null;

            EventSystem eventSystem = _eventSystem;
            if (eventSystem == null || !eventSystem.enabled)
                return;

            GameObject targetObject = target != null ? target.gameObject : null;
            if (eventSystem.currentSelectedGameObject == targetObject)
                return;

            eventSystem.SetSelectedGameObject(targetObject);
        }

        private void PublishHaptic(float intensity01, float durationSeconds)
        {
            HapticRequest request = default;
            request.Intensity01 = math.saturate(intensity01);
            request.DurationSeconds = math.max(0.005f, durationSeconds);
            request.Frequency01 = 0.45f;
            request.SourceHash = _hapticSourceHash;
            request.Frame = SystemDispatcher.CurrentFrameId;
            request.Channel = MenuHapticChannel;
            request.Flags = MenuHapticFlags;
            SignalBus<HapticRequest>.TryPushTracked(in request, ref _hapticDropCount);
        }

        private void PublishAcoustic(int sourceIndex, float intensity01, float radiusMeters, byte channel, byte flags)
        {
            if (sourceIndex < 0 || sourceIndex >= _controlCount)
                return;

            RectTransform rect = _controlRects[sourceIndex];
            if (rect == null)
                return;

            Vector2 center = rect.rect.center;
            Vector3 worldPoint = rect.TransformPoint(new Vector3(center.x, center.y, 0f));
            AcousticPingSignal signal = default;
            if (!RuntimeOriginRoute.TryRuntimePositionToAup(worldPoint, ref signal.PositionAup))
                return;

            signal.RadiusMeters = math.max(0.05f, radiusMeters);
            signal.Intensity01 = math.saturate(intensity01);
            signal.SourceId = _hapticSourceHash;
            signal.Channel = channel;
            signal.Flags = flags;
            SignalBus<AcousticPingSignal>.TryPushTracked(in signal, ref _acousticDropCount);
        }
    }
}

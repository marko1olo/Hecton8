namespace Hecton8.Tools.Editor
{
    using System.Globalization;
    using Hecton8.Tools;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Editor-only x-ray facade for SHINOBU_231 mask and layout inspection.
    /// </summary>
    public sealed class UpgradeMatrixXRayWindow : EditorWindow
    {
        private readonly Label[] _bitLabels = new Label[64];
        private TextField _maskField;
        private Label _layoutLabel;
        private Label _statsLabel;
        private ulong _mask;

        [MenuItem("Hecton8/Tools/Stat Compilation X-Ray")]
        public static void Open()
        {
            UpgradeMatrixXRayWindow window = GetWindow<UpgradeMatrixXRayWindow>();
            window.titleContent = new GUIContent("Stat Compilation X-Ray");
            window.minSize = new Vector2(420f, 420f);
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _layoutLabel = new Label();
            root.Add(_layoutLabel);

            _maskField = new TextField("Mask hex");
            _maskField.value = "0x0000000000000000";
            _maskField.RegisterValueChangedCallback(evt =>
            {
                _mask = ParseMask(evt.newValue);
                RefreshMaskView();
            });
            root.Add(_maskField);

            Button flipLowBits = new Button(() =>
            {
                _mask ^= 0x0000000000000FFFUL;
                _maskField.value = "0x" + _mask.ToString("X16");
                RefreshMaskView();
            })
            {
                text = "Flip stress bits"
            };
            root.Add(flipLowBits);

            VisualElement grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.marginTop = 8;
            root.Add(grid);

            for (int i = 0; i < _bitLabels.Length; i++)
            {
                Label label = new Label(i.ToString("00", CultureInfo.InvariantCulture));
                label.style.width = 38;
                label.style.height = 20;
                label.style.marginRight = 2;
                label.style.marginBottom = 2;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                _bitLabels[i] = label;
                grid.Add(label);
            }

            _statsLabel = new Label();
            _statsLabel.style.marginTop = 8;
            root.Add(_statsLabel);
            RefreshLayout();
            RefreshMaskView();
        }

        private void RefreshLayout()
        {
            UpgradeMatrixLayoutValidator.Validate(out uint faults);
            _layoutLabel.text = "UpgradeMaskDTO size=16 offset(mask)=8 faults=0x" + faults.ToString("X8");
        }

        private void RefreshMaskView()
        {
            int active = UpgradeMatrixCompiler.PopCount64(_mask);
            for (int i = 0; i < _bitLabels.Length; i++)
            {
                bool on = ((_mask >> i) & 1UL) != 0UL;
                _bitLabels[i].style.backgroundColor = on ? new Color(0.1f, 0.65f, 0.95f, 1f) : new Color(0.08f, 0.08f, 0.08f, 1f);
                _bitLabels[i].style.color = on ? Color.black : new Color(0.7f, 0.7f, 0.7f, 1f);
            }

            _statsLabel.text = "activeBits=" + active + " stateHash=0x" + UpgradeMatrixCompiler.HashMask(_mask, 0x58524159u, 0x55323331u).ToString("X16");
        }

        private static ulong ParseMask(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0UL;

            string trimmed = text.Trim();
            if (trimmed.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(2);

            return ulong.TryParse(trimmed, System.Globalization.NumberStyles.HexNumber, null, out ulong parsed)
                ? parsed
                : 0UL;
        }
    }
}

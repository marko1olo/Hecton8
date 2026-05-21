#if UNITY_EDITOR
using Hecton8.Power;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.QA.Headless.Editor
{
    public sealed class JacobiStressFuzzerWindow : EditorWindow
    {
        private Label _stateLabel;
        private Label _flagsLabel;
        private Label _residualLabel;
        private Label _perfLabel;

        [MenuItem("Hecton/Power/Solver Fuzzer")]
        public static void Open()
        {
            JacobiStressFuzzerWindow window = GetWindow<JacobiStressFuzzerWindow>();
            window.titleContent = new GUIContent("Solver Fuzzer");
            window.minSize = new Vector2(460f, 220f);
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            Button runButton = new Button(RunHostileGraphTest) { text = "RUN HOSTILE GRAPH TEST" };
            root.Add(runButton);

            _stateLabel = new Label("PENDING");
            _flagsLabel = new Label("failure flags: 0");
            _residualLabel = new Label("residual: 0");
            _perfLabel = new Label("solver us: 0");
            root.Add(_stateLabel);
            root.Add(_flagsLabel);
            root.Add(_residualLabel);
            root.Add(_perfLabel);

            Refresh(PowerJacobiStressFuzzerState.LastResult);
        }

        private void RunHostileGraphTest()
        {
            bool passed = PowerJacobiStressFuzzer.RunDefault(out PowerJacobiStressFuzzerResult result);
            PowerJacobiStressFuzzerState.LastResult = result;
            PowerJacobiStressFuzzerState.HasFailure = !passed && result.FirstFailureNodeHash != 0u;
            PowerJacobiStressFuzzerState.LastFailureNodeHash = result.FirstFailureNodeHash;
            PowerJacobiStressFuzzerState.LastFailureAup = result.FirstFailureAup;
            Refresh(result);
            SceneView.RepaintAll();
        }

        private void Refresh(PowerJacobiStressFuzzerResult result)
        {
            if (_stateLabel == null)
                return;

            bool passed = result.FailureFlags == 0u && result.FrameCount > 0;
            _stateLabel.text = passed ? "PASS" : "FAIL";
            _stateLabel.style.color = passed ? new Color(0.1f, 0.85f, 0.45f, 1f) : new Color(1f, 0.15f, 0.05f, 1f);
            _flagsLabel.text = "failure flags: " + result.FailureFlags + "  node: " + result.FirstFailureNodeHash;
            _residualLabel.text = "final residual: " + result.FinalResidual.ToString("0.000000") +
                                  "  max residual: " + result.MaxResidual.ToString("0.000000");
            _perfLabel.text = "solver avg us: " + result.AverageSolverMicroseconds.ToString("0.000") +
                              "  managed bytes delta: " + result.ManagedBytesDelta;
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawFailureSceneMarker;
            SceneView.duringSceneGui += DrawFailureSceneMarker;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawFailureSceneMarker;
        }

        private static void DrawFailureSceneMarker(SceneView sceneView)
        {
            if (!PowerJacobiStressFuzzerState.HasFailure)
                return;

            double3 aup = PowerJacobiStressFuzzerState.LastFailureAup;
            Vector3 position = new Vector3((float)(aup.x % 10000.0), (float)aup.y, (float)(aup.z % 10000.0));
            Handles.color = new Color(1f, 0f, 0f, 0.9f);
            Handles.SphereHandleCap(0, position, Quaternion.identity, 8f, EventType.Repaint);
        }
    }

    public sealed class JacobiStressFuzzerGizmoHook : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            if (!PowerJacobiStressFuzzerState.HasFailure)
                return;

            double3 aup = PowerJacobiStressFuzzerState.LastFailureAup;
            Vector3 position = new Vector3((float)(aup.x % 10000.0), (float)aup.y, (float)(aup.z % 10000.0));
            Gizmos.color = new Color(1f, 0f, 0f, 0.85f);
            Gizmos.DrawSphere(position, 4f);
        }
    }
}
#endif

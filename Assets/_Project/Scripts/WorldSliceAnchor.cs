using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class WorldSliceAnchor : MonoBehaviour
    {
        public enum SliceState
        {
            Far,
            Mid,
            Near
        }

        [Header("Distances")]
        [SerializeField] private float nearDistance = 140f;
        [SerializeField] private float midDistance = 260f;
        [SerializeField] private float hysteresisPadding = 24f;

        [Header("Roots")]
        [SerializeField] private GameObject[] nearOnlyRoots;
        [SerializeField] private GameObject[] midAndNearRoots;
        [SerializeField] private GameObject[] midOnlyRoots;
        [SerializeField] private GameObject[] farOnlyRoots;

        [Header("Behaviours")]
        [SerializeField] private Behaviour[] nearOnlyBehaviours;
        [SerializeField] private Behaviour[] midAndNearBehaviours;
        [SerializeField] private Behaviour[] midOnlyBehaviours;
        [SerializeField] private Behaviour[] farOnlyBehaviours;

        [Header("Fidelity")]
        [SerializeField] private WorldFidelityRoot[] fidelityRoots;

        [Header("Diagnostics")]
        [SerializeField] private string _debugState = "Far";
        [SerializeField] private float _debugLastDistance;
        [SerializeField] private float _debugScaledNearDistance;
        [SerializeField] private float _debugScaledMidDistance;

        private SliceState _currentState = SliceState.Far;

        public SliceState CurrentState => _currentState;
        public float NearDistance => nearDistance;
        public float MidDistance => midDistance;
        public float HysteresisPadding => hysteresisPadding;

        private void Awake()
        {
            ClampSettings();
            RefreshFidelityRoots();
            ApplyState(SliceState.Far, true);
        }

        public void ApplyForDistance(float distance)
        {
            ApplyForDistance(distance, 1f, 1f);
        }

        public void ApplyForDistance(float distance, float nearDistanceScale, float midDistanceScale)
        {
            _debugLastDistance = distance;
            SliceState nextState = EvaluateState(distance, nearDistanceScale, midDistanceScale);
            ApplyState(nextState, false);
        }

        public void ApplyState(SliceState nextState, bool force)
        {
            if (!force && nextState == _currentState)
                return;

            bool nearActive = nextState == SliceState.Near;
            bool midActive = nextState != SliceState.Far;
            bool midOnlyActive = nextState == SliceState.Mid;
            bool farOnlyActive = nextState == SliceState.Far;

            SetRootsActive(midAndNearRoots, midActive);
            SetRootsActive(nearOnlyRoots, nearActive);
            SetRootsActive(midOnlyRoots, midOnlyActive);
            SetRootsActive(farOnlyRoots, farOnlyActive);
            SetBehavioursEnabled(midAndNearBehaviours, midActive);
            SetBehavioursEnabled(nearOnlyBehaviours, nearActive);
            SetBehavioursEnabled(midOnlyBehaviours, midOnlyActive);
            SetBehavioursEnabled(farOnlyBehaviours, farOnlyActive);
            ApplyFidelityRoots(nextState);

            _currentState = nextState;
            _debugState = nextState.ToString();
        }

        private SliceState EvaluateState(float distance, float nearDistanceScale, float midDistanceScale)
        {
            float scaledNearDistance = nearDistance * Mathf.Clamp(nearDistanceScale, 0.5f, 1.5f);
            float scaledMidDistance = midDistance * Mathf.Clamp(midDistanceScale, 0.5f, 1.5f);
            scaledMidDistance = Mathf.Max(scaledNearDistance + 20f, scaledMidDistance);

            _debugScaledNearDistance = scaledNearDistance;
            _debugScaledMidDistance = scaledMidDistance;

            if (_currentState == SliceState.Near)
            {
                if (distance <= scaledNearDistance + hysteresisPadding)
                    return SliceState.Near;
            }
            else if (distance <= scaledNearDistance)
            {
                return SliceState.Near;
            }

            if (_currentState == SliceState.Mid)
            {
                if (distance <= scaledMidDistance + hysteresisPadding)
                    return SliceState.Mid;
            }
            else if (distance <= scaledMidDistance)
            {
                return SliceState.Mid;
            }

            return SliceState.Far;
        }

        private static void SetRootsActive(GameObject[] roots, bool active)
        {
            if (roots == null)
                return;

            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null || root.activeSelf == active)
                    continue;

                root.SetActive(active);
            }
        }

        private static void SetBehavioursEnabled(Behaviour[] behaviours, bool enabled)
        {
            if (behaviours == null)
                return;

            for (int i = 0; i < behaviours.Length; i++)
            {
                Behaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour.enabled == enabled)
                    continue;

                behaviour.enabled = enabled;
            }
        }

        private void ApplyFidelityRoots(SliceState nextState)
        {
            if (fidelityRoots == null)
                return;

            for (int i = 0; i < fidelityRoots.Length; i++)
            {
                WorldFidelityRoot fidelityRoot = fidelityRoots[i];
                if (fidelityRoot == null)
                    continue;

                fidelityRoot.ApplySliceState(nextState);
            }
        }

        private void RefreshFidelityRoots()
        {
            fidelityRoots = GetComponentsInChildren<WorldFidelityRoot>(true);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            ClampSettings();
            RefreshFidelityRoots();
        }
#endif

        private void ClampSettings()
        {
            nearDistance = Mathf.Max(20f, nearDistance);
            midDistance = Mathf.Max(nearDistance + 20f, midDistance);
            hysteresisPadding = Mathf.Clamp(hysteresisPadding, 4f, 80f);
        }
    }
}

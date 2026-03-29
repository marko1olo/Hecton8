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

        [Header("Behaviours")]
        [SerializeField] private Behaviour[] nearOnlyBehaviours;
        [SerializeField] private Behaviour[] midAndNearBehaviours;

        [Header("Diagnostics")]
        [SerializeField] private string _debugState = "Far";
        [SerializeField] private float _debugLastDistance;

        private SliceState _currentState = SliceState.Far;

        public SliceState CurrentState => _currentState;
        public float NearDistance => nearDistance;
        public float MidDistance => midDistance;
        public float HysteresisPadding => hysteresisPadding;

        private void Awake()
        {
            ClampSettings();
            ApplyState(SliceState.Far, true);
        }

        public void ApplyForDistance(float distance)
        {
            _debugLastDistance = distance;
            SliceState nextState = EvaluateState(distance);
            ApplyState(nextState, false);
        }

        public void ApplyState(SliceState nextState, bool force)
        {
            if (!force && nextState == _currentState)
                return;

            bool nearActive = nextState == SliceState.Near;
            bool midActive = nextState != SliceState.Far;

            SetRootsActive(midAndNearRoots, midActive);
            SetRootsActive(nearOnlyRoots, nearActive);
            SetBehavioursEnabled(midAndNearBehaviours, midActive);
            SetBehavioursEnabled(nearOnlyBehaviours, nearActive);

            _currentState = nextState;
            _debugState = nextState.ToString();
        }

        private SliceState EvaluateState(float distance)
        {
            if (_currentState == SliceState.Near)
            {
                if (distance <= nearDistance + hysteresisPadding)
                    return SliceState.Near;
            }
            else if (distance <= nearDistance)
            {
                return SliceState.Near;
            }

            if (_currentState == SliceState.Mid)
            {
                if (distance <= midDistance + hysteresisPadding)
                    return SliceState.Mid;
            }
            else if (distance <= midDistance)
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            ClampSettings();
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

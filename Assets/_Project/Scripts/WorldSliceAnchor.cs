using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class WorldSliceAnchor : MonoBehaviour
    {
        private static readonly List<WorldSliceAnchor> _ActiveAnchors = new List<WorldSliceAnchor>(32);

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
        [SerializeField] private float _debugLastDistanceSq;
        [SerializeField] private float _debugScaledNearDistance;
        [SerializeField] private float _debugScaledMidDistance;

        private AbsoluteUniversePosition _anchorAup;
        private SliceState _currentState = SliceState.Far;
        private bool _anchorAupInitialized;

        public SliceState CurrentState => _currentState;
        public float NearDistance => nearDistance;
        public float MidDistance => midDistance;
        public float HysteresisPadding => hysteresisPadding;
        public AbsoluteUniversePosition AnchorAup
        {
            get
            {
                if (!_anchorAupInitialized)
                    CacheAnchorAup();

                return _anchorAup;
            }
        }

        private void OnEnable()
        {
            CacheAnchorAup();
            RegisterActiveAnchor(this);
        }

        private void OnDisable()
        {
            UnregisterActiveAnchor(this);
        }

        private void OnDestroy()
        {
            UnregisterActiveAnchor(this);
        }

        public static void CopyActiveAnchorsTo(List<WorldSliceAnchor> destination)
        {
            if (destination == null)
                return;

            destination.Clear();
            for (int i = 0; i < _ActiveAnchors.Count; i++)
            {
                WorldSliceAnchor anchor = _ActiveAnchors[i];
                if (anchor == null)
                    continue;

                GameObject go = anchor.gameObject;
                if (go == null || !go.scene.IsValid())
                    continue;

                destination.Add(anchor);
            }
        }

        private void Awake()
        {
            ClampSettings();
            CacheAnchorAup();
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
            ApplyForDistanceSq(distance * distance, nearDistanceScale, midDistanceScale);
        }

        public void ApplyForDistanceSq(float distanceSq, float nearDistanceScale, float midDistanceScale)
        {
            _debugLastDistanceSq = distanceSq;
            SliceState nextState = EvaluateStateSq(distanceSq, nearDistanceScale, midDistanceScale);
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
            _debugState = ResolveStateName(nextState);
        }

        private SliceState EvaluateStateSq(float distanceSq, float nearDistanceScale, float midDistanceScale)
        {
            float scaledNearDistance = nearDistance * math.clamp(nearDistanceScale, 0.5f, 1.5f);
            float scaledMidDistance = midDistance * math.clamp(midDistanceScale, 0.5f, 1.5f);
            scaledMidDistance = math.max(scaledNearDistance + 20f, scaledMidDistance);

            _debugScaledNearDistance = scaledNearDistance;
            _debugScaledMidDistance = scaledMidDistance;

            if (_currentState == SliceState.Near)
            {
                float nearExitDistance = scaledNearDistance + hysteresisPadding;
                if (distanceSq <= nearExitDistance * nearExitDistance)
                    return SliceState.Near;
            }
            else if (distanceSq <= scaledNearDistance * scaledNearDistance)
            {
                return SliceState.Near;
            }

            if (_currentState == SliceState.Mid)
            {
                float midExitDistance = scaledMidDistance + hysteresisPadding;
                if (distanceSq <= midExitDistance * midExitDistance)
                    return SliceState.Mid;
            }
            else if (distanceSq <= scaledMidDistance * scaledMidDistance)
            {
                return SliceState.Mid;
            }

            return SliceState.Far;
        }

        internal static string ResolveStateName(SliceState state)
        {
            switch (state)
            {
                case SliceState.Near:
                    return "Near";
                case SliceState.Mid:
                    return "Mid";
                default:
                    return "Far";
            }
        }

        private void CacheAnchorAup()
        {
            _anchorAup = AbsoluteUniversePosition.FromRuntimePosition(transform.position);
            _anchorAupInitialized = true;
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

        private static void RegisterActiveAnchor(WorldSliceAnchor anchor)
        {
            if (anchor == null || _ActiveAnchors.Contains(anchor))
                return;

            _ActiveAnchors.Add(anchor);
        }

        private static void UnregisterActiveAnchor(WorldSliceAnchor anchor)
        {
            if (anchor == null)
                return;

            _ActiveAnchors.Remove(anchor);
        }
    }
}

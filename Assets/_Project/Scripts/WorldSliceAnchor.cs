using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class WorldSliceAnchor : MonoBehaviour
    {
        private const int ActiveAnchorCopyBudget = 64;
        private static readonly WorldSliceAnchor[] _ActiveAnchors = new WorldSliceAnchor[ActiveAnchorCopyBudget];
        private static readonly List<WorldFidelityRoot> _FidelityRootScratch = new List<WorldFidelityRoot>(8);
        private static int _ActiveAnchorCount;
        private static int _ActiveAnchorVersion;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            System.Array.Clear(_ActiveAnchors, 0, _ActiveAnchors.Length);
            _ActiveAnchorCount = 0;
            _ActiveAnchorVersion = 0;
        }

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

        private SliceState _currentState = SliceState.Far;

        public SliceState CurrentState => _currentState;
        public float NearDistance => nearDistance;
        public float MidDistance => midDistance;
        public float HysteresisPadding => hysteresisPadding;

        private void OnEnable()
        {
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

        public static int CopyActiveAnchorsTo(WorldSliceAnchor[] destination)
        {
            if (destination == null || destination.Length <= 0)
                return 0;

            int writeCount = 0;
            int safeCount = math.min(_ActiveAnchorCount, ActiveAnchorCopyBudget);
            for (int i = 0; i < safeCount && writeCount < destination.Length; i++)
            {
                WorldSliceAnchor anchor = _ActiveAnchors[i];
                if (anchor == null)
                    continue;

                GameObject go = anchor.gameObject;
                if (go == null || !go.scene.IsValid())
                    continue;

                destination[writeCount] = anchor;
                writeCount++;
            }

            for (int i = writeCount; i < destination.Length; i++)
                destination[i] = null;

            return writeCount;
        }

        public static int ActiveAnchorVersion => _ActiveAnchorVersion;

        private void Awake()
        {
            ClampSettings();
            EnsureRuntimeFidelityRootCache();
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

        internal float GetPlanarDistanceSq(in AbsoluteUniversePosition playerAup)
        {
            Vector3 anchorRuntime = transform.position;
            float3 playerRuntime = playerAup.ToRuntimeFloat3();
            double deltaX = (double)anchorRuntime.x - playerRuntime.x;
            double deltaZ = (double)anchorRuntime.z - playerRuntime.z;
            double distanceSq = (deltaX * deltaX) + (deltaZ * deltaZ);
            return distanceSq > float.MaxValue ? float.MaxValue : (float)distanceSq;
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
            _FidelityRootScratch.Clear();
            GetComponentsInChildren<WorldFidelityRoot>(true, _FidelityRootScratch);
            int rootCount = _FidelityRootScratch.Count;
            if (fidelityRoots == null || fidelityRoots.Length != rootCount)
                fidelityRoots = new WorldFidelityRoot[rootCount];

            for (int i = 0; i < rootCount; i++)
                fidelityRoots[i] = _FidelityRootScratch[i];

            _FidelityRootScratch.Clear();
        }

        private void EnsureRuntimeFidelityRootCache()
        {
            if (HasUsableFidelityRootCache())
                return;

            RefreshFidelityRoots();
        }

        private bool HasUsableFidelityRootCache()
        {
            if (fidelityRoots == null || fidelityRoots.Length == 0)
                return false;

            for (int i = 0; i < fidelityRoots.Length; i++)
            {
                if (fidelityRoots[i] == null)
                    return false;
            }

            return true;
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
            if (anchor == null || FindActiveAnchorIndex(anchor) >= 0 || _ActiveAnchorCount >= _ActiveAnchors.Length)
                return;

            _ActiveAnchors[_ActiveAnchorCount] = anchor;
            _ActiveAnchorCount++;
            _ActiveAnchorVersion++;
        }

        private static void UnregisterActiveAnchor(WorldSliceAnchor anchor)
        {
            if (anchor == null)
                return;

            int index = FindActiveAnchorIndex(anchor);
            if (index < 0)
                return;

            int lastIndex = _ActiveAnchorCount - 1;
            if (index != lastIndex)
                _ActiveAnchors[index] = _ActiveAnchors[lastIndex];

            _ActiveAnchors[lastIndex] = null;
            _ActiveAnchorCount--;
            _ActiveAnchorVersion++;
        }

        private static int FindActiveAnchorIndex(WorldSliceAnchor anchor)
        {
            for (int i = 0; i < _ActiveAnchorCount; i++)
            {
                if (ReferenceEquals(_ActiveAnchors[i], anchor))
                    return i;
            }

            return -1;
        }
    }
}

using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4040)]
    public sealed class WorldPopulationDirector : MonoBehaviour, ISlowTickable
    {
        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private WorldZoneDirector worldZoneDirector;
        [SerializeField] private WorldContentDirector worldContentDirector;
        [SerializeField] private List<WorldPopulationRule> rules = new List<WorldPopulationRule>();

        [Header("Diagnostics")]
        [SerializeField] private int _debugRuleCount;
        [SerializeField] private int _debugMatchedRuleCount;
        [SerializeField] private int _debugResolvedSocketCount;
        [SerializeField] private string _debugCurrentZone = "None";
        [SerializeField] private string _debugCurrentZoneBiome = "None";
        [SerializeField] private string _debugCurrentSocket = "None";
        [SerializeField] private string _debugPrimaryRule = "None";
        [SerializeField] private string _debugPrimaryPrefabFamily = "None";
        [SerializeField] private string _debugPrimaryBiomeFit = "None";

        private bool _registeredToTickManager;

        private void Awake()
        {
            ResolveReferences();
            UpdateDiagnostics(null, null, null, 0, 0);
        }

        private void OnEnable()
        {
            if (GameTickManager.Instance != null && !_registeredToTickManager)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredToTickManager = true;
            }
        }

        private void Start()
        {
            if (!_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredToTickManager = true;
            }

            EvaluateRules();
        }

        private void OnDisable()
        {
            if (_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister((ISlowTickable)this);
                _registeredToTickManager = false;
            }
        }

        public void SlowTick()
        {
            EvaluateRules();
        }

        public void SetRules(IReadOnlyList<WorldPopulationRule> sourceRules)
        {
            rules.Clear();
            if (sourceRules == null)
                return;

            for (int i = 0; i < sourceRules.Count; i++)
            {
                if (sourceRules[i] != null)
                    rules.Add(sourceRules[i]);
            }
        }

        private void EvaluateRules()
        {
            ResolveReferences();

            WorldZoneAnchor zone = worldZoneDirector != null ? worldZoneDirector.CurrentZone : null;
            WorldContentSocket socket = FindNearestSocketInZone(zone);

            WorldPopulationRule primaryRule = null;
            int matchedCount = 0;
            int resolvedSocketCount = 0;

            IReadOnlyList<WorldContentSocket> sockets = worldContentDirector != null ? worldContentDirector.Sockets : null;
            if (sockets != null)
            {
                for (int i = 0; i < sockets.Count; i++)
                {
                    WorldContentSocket candidateSocket = sockets[i];
                    if (candidateSocket == null)
                        continue;

                    WorldZoneAnchor candidateZone = candidateSocket.GetComponentInParent<WorldZoneAnchor>();
                    WorldPopulationRule candidateRule = FindPrimaryRule(candidateZone, candidateSocket, out int candidateMatchCount);
                    if (candidateRule != null)
                    {
                        candidateSocket.ApplyPopulationRecommendation(candidateRule);
                        resolvedSocketCount++;
                    }
                    else
                    {
                        candidateSocket.ClearPopulationRecommendation();
                    }

                    if (candidateSocket == socket)
                    {
                        primaryRule = candidateRule;
                        matchedCount = candidateMatchCount;
                    }
                }
            }

            UpdateDiagnostics(zone, socket, primaryRule, matchedCount, resolvedSocketCount);
        }

        private WorldContentSocket FindNearestSocketInZone(WorldZoneAnchor zone)
        {
            if (zone == null || playerTransform == null || worldContentDirector == null)
                return null;

            IReadOnlyList<WorldContentSocket> sockets = worldContentDirector.Sockets;
            WorldContentSocket best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < sockets.Count; i++)
            {
                WorldContentSocket socket = sockets[i];
                if (socket == null)
                    continue;

                WorldZoneAnchor socketZone = socket.GetComponentInParent<WorldZoneAnchor>();
                if (socketZone == null || socketZone.ZoneId != zone.ZoneId)
                    continue;

                float distance = socket.GetFlatDistance(playerTransform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = socket;
                }
            }

            return best;
        }

        private void ResolveReferences()
        {
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player == null)
                    player = GameObject.Find("Player");

                if (player != null)
                    playerTransform = player.transform;
            }

            if (worldZoneDirector == null)
                worldZoneDirector = FindAnyObjectByType<WorldZoneDirector>();

            if (worldContentDirector == null)
                worldContentDirector = FindAnyObjectByType<WorldContentDirector>();
        }

        private WorldPopulationRule FindPrimaryRule(WorldZoneAnchor zone, WorldContentSocket socket, out int matchedCount)
        {
            WorldPopulationRule primaryRule = null;
            matchedCount = 0;

            for (int i = 0; i < rules.Count; i++)
            {
                WorldPopulationRule rule = rules[i];
                if (rule == null || !rule.Matches(zone, socket))
                    continue;

                matchedCount++;
                if (primaryRule == null)
                    primaryRule = rule;
            }

            return primaryRule;
        }

        private void UpdateDiagnostics(
            WorldZoneAnchor zone,
            WorldContentSocket socket,
            WorldPopulationRule primaryRule,
            int matchedCount,
            int resolvedSocketCount)
        {
            _debugRuleCount = rules.Count;
            _debugMatchedRuleCount = matchedCount;
            _debugResolvedSocketCount = resolvedSocketCount;
            _debugCurrentZone = zone != null ? zone.ZoneLabel : "None";
            _debugCurrentZoneBiome = zone != null && zone.DominantBiomeFamily != null
                ? zone.DominantBiomeFamily.familyLabel
                : "None";
            _debugCurrentSocket = socket != null ? socket.SocketLabel : "None";
            _debugPrimaryRule = primaryRule != null ? primaryRule.ruleLabel : "None";
            _debugPrimaryPrefabFamily = primaryRule != null && !string.IsNullOrWhiteSpace(primaryRule.prefabFamily)
                ? primaryRule.prefabFamily
                : "None";
            _debugPrimaryBiomeFit = primaryRule != null && !string.IsNullOrWhiteSpace(primaryRule.biomeFitSummary)
                ? primaryRule.biomeFitSummary
                : "None";
        }
    }
}

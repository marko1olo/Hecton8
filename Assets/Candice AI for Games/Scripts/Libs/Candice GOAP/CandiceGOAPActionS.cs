using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CandiceAIforGames.AI
{
    [Serializable]
    public class CandiceGOAPActionS : ScriptableObject
    {
        public new string name;                                 // Name of the action
        public int cost;                                     // Cost of the action
        public List<CandiceKeyValuePair<string, int>> preconditions;        // Dictionary of preconditions of the action
        public List<CandiceKeyValuePair<string, int>> effects;              // Dictionary of effects of the action
        public CandiceBehaviorTreeS behaviorTree;             // Behavior tree that will be evaluated to execute the action
        public bool isComplete = false;

        public CandiceGOAPActionS(string name, int cost, List<CandiceKeyValuePair<string, int>> preconditions, List<CandiceKeyValuePair<string, int>> effects, CandiceBehaviorTreeS behaviorTree, bool isComplete)
        {
            this.name = name;
            this.cost = cost;
            this.preconditions = preconditions;
            this.effects = effects;
            this.behaviorTree = behaviorTree;
            this.isComplete = isComplete;
        }
        public CandiceGOAPAction ConvertToGOAPAction(CandiceAIController agent)
        {
            Dictionary<string, int> _preconditions = new Dictionary<string, int>(preconditions != null ? preconditions.Count : 0);
            Dictionary<string, int> _effects = new Dictionary<string, int>(effects != null ? effects.Count : 0);

            if (preconditions != null)
            {
                for (int i = 0; i < preconditions.Count; i++)
                {
                    CandiceKeyValuePair<string, int> item = preconditions[i];
                    if (!_preconditions.ContainsKey(item.key))
                    {
                        _preconditions.Add(item.key, item.value);
                    }
                }
            }

            if (effects != null)
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    CandiceKeyValuePair<string, int> item = effects[i];
                    if (!_effects.ContainsKey(item.key))
                    {
                        _effects.Add(item.key, item.value);
                    }
                }
            }

            CandiceGOAPAction action = new CandiceGOAPAction(agent, name, cost, _preconditions, _effects, behaviorTree);

            return action;
        }


    }
}

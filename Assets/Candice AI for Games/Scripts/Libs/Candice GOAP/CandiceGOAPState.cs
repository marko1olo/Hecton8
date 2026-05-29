using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CandiceAIforGames.AI
{
    /// <summary>
    /// Represents the current state of an agent in the world for goal-oriented action planning.
    /// </summary>
    /// 
    [Serializable]
    public class CandiceGOAPState
    {
        /// <summary>
        /// The state of the world, represented as a dictionary mapping resource names to resource quantities.
        /// </summary>
        public CandiceDictionary<string, int> state;
        public string stateName;

        /// <summary>
        /// Constructs a new CandiceGOAPState.
        /// </summary>
        /// <param name="state">The initial state of the world.</param>
        public CandiceGOAPState(Dictionary<string, int> state, string stateName = "Current State")
        {
            this.state = ConvertToCandiceDictionary(state);
            this.stateName = stateName;
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder(64 + (state.Count * 24));
            builder.Append("------------\n");
            builder.Append(stateName);
            builder.Append("\n------------\n");
            for (int i = 0; i < state.Count; i++)
            {
                builder.Append(state.KeyAt(i));
                builder.Append(": ");
                builder.Append(state.ValueAt(i));
                builder.Append('\n');
            }
            builder.Append("------------");
            return builder.ToString();
        }
        /// <summary>
        /// Determines whether this state satisfies the given goal.
        /// </summary>
        /// <param name="goal">The goal state to satisfy.</param>
        /// <returns>True if the goal is satisfied, false otherwise.</returns>
        public bool SatisfiesGoal(CandiceGOAPState goal)
        {
            // Check each resource in the goal state and ensure it is present in this state with the required quantity
            for (int i = 0; i < goal.state.Count; i++)
            {
                string key = goal.state.KeyAt(i);
                int value = goal.state.ValueAt(i);
                if (!state.ContainsKey(key) || state[key] < value)
                {
                    return false;
                }
            }
            return true;
        }

        public int CalculateHeuristicValue(CandiceGOAPState goal)
        {
            int heuristicValue = 0;

            // Iterate through each condition in the goal state
            for (int i = 0; i < goal.state.Count; i++)
            {
                string key = goal.state.KeyAt(i);
                int value = goal.state.ValueAt(i);
                // If the current node's state does not contain the condition, add it to the heuristic value
                if (!state.ContainsKey(key))
                {
                    heuristicValue += value;
                }
                // If the current node's state does contain the condition, but not enough to satisfy the goal state, add the difference to the heuristic value
                else if (state[key] < value)
                {
                    heuristicValue += value - state[key];
                }
            }
            return heuristicValue;
        }

        /// <summary>
        /// Applies the effects of the given action to this state.
        /// </summary>
        /// <param name="action">The action whose effects to apply.</param>
        public void ApplyActionEffects(CandiceGOAPAction action)
        {
            // Apply each effect of the action to this state
            Dictionary<string, int>.Enumerator enumerator = action.effects.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<string, int> effect = enumerator.Current;
                if (state.ContainsKey(effect.Key))
                {
                    state[effect.Key] += effect.Value;
                }
                else
                {
                    state[effect.Key] = effect.Value;
                }
            }
        }

        /// <summary>
        /// Returns a new state object that results from applying the given action to this state.
        /// </summary>
        /// <param name="action">The action to apply.</param>
        /// <returns>The new state resulting from the action.</returns>
        public CandiceGOAPState GetActionResult(CandiceGOAPAction action)
        {
            // Create a new state object with the same state as this state
            CandiceGOAPState result = new CandiceGOAPState(ConvertToNormalDictionary(state));

            // Apply the effects of the action to the new state
            result.ApplyActionEffects(action);

            return result;
        }

        /// <summary>
        /// Determines whether this state is achievable in the given state.
        /// </summary>
        /// <param name="otherState">The state to compare against.</param>
        /// <returns>True if this state is achievable in the other state, false otherwise.</returns>
        public bool IsAchievable(CandiceGOAPState otherState)
        {
            // Check each resource in this state and ensure it is present in the other state with the required quantity
            for (int i = 0; i < state.Count; i++)
            {
                string key = state.KeyAt(i);
                int value = state.ValueAt(i);
                if (!otherState.state.ContainsKey(key) || otherState.state[key] < value)
                {
                    return false;
                }
            }
            return true;
        }

        public void AddState(string key, int value)
        {
            if (!state.ContainsKey(key))
            {
                state.Add(key, value);
            }
            else
            {
                state[key] = value;
            }
        }

        public void RemoveState(string key)
        {
            if (state.ContainsKey(key))
            {
                state.Remove(key);
            }
        }

        public int GetState(string key)
        {
            if (state.ContainsKey(key))
            {
                return state[key];
            }
            else
            {
                return 0;
            }
        }

        public bool HasState(string key)
        {
            return state.ContainsKey(key);
        }

        public bool HasState(string key, int value)
        {
            return state.ContainsKey(key) && state[key] == value;
        }

        public bool HasStates(Dictionary<string, int> conditions)
        {
            Dictionary<string, int>.Enumerator enumerator = conditions.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<string, int> condition = enumerator.Current;
                if (!state.ContainsKey(condition.Key) || state[condition.Key] != condition.Value)
                {
                    return false;
                }
            }
            return true;
        }

        public bool HasAnyState(Dictionary<string, int> conditions)
        {
            Dictionary<string, int>.Enumerator enumerator = conditions.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<string, int> condition = enumerator.Current;
                if (state.ContainsKey(condition.Key) && state[condition.Key] == condition.Value)
                {
                    return true;
                }
            }
            return false;
        }

        public void CopyFrom(CandiceGOAPState other)
        {
            state.Clear();
            for (int i = 0; i < other.state.Count; i++)
            {
                state[other.state.KeyAt(i)] = other.state.ValueAt(i);
            }
        }
        public static CandiceDictionary<string, int> ConvertToCandiceDictionary(Dictionary<string, int> dictionary)
        {
            CandiceDictionary<string, int> serializableDictionary = new CandiceDictionary<string, int>(dictionary != null ? dictionary.Count : 0);
            if (dictionary == null)
            {
                return serializableDictionary;
            }

            Dictionary<string, int>.Enumerator enumerator = dictionary.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<string, int> kvp = enumerator.Current;
                serializableDictionary.Add(kvp.Key, kvp.Value);
            }

            return serializableDictionary;
        }
        public static Dictionary<string, int> ConvertToNormalDictionary(CandiceDictionary<string, int> serializableDictionary)
        {
            Dictionary<string, int> normalDictionary = new Dictionary<string, int>(serializableDictionary != null ? serializableDictionary.Count : 0);
            if (serializableDictionary == null)
            {
                return normalDictionary;
            }

            for (int i = 0; i < serializableDictionary.Count; i++)
            {
                normalDictionary[serializableDictionary.KeyAt(i)] = serializableDictionary.ValueAt(i);
            }

            return normalDictionary;
        }
    }

    [System.Serializable]
    public class CandiceDictionary<TKey, TValue>
    {
        // COLD ALLOC: List<TKey>[0] - serialized GOAP key storage, explicit empty capacity for deserialization - owner: CandiceDictionary
        [SerializeField]
        private List<TKey> keys = new List<TKey>(0);

        // COLD ALLOC: List<TValue>[0] - serialized GOAP value storage, explicit empty capacity for deserialization - owner: CandiceDictionary
        [SerializeField]
        private List<TValue> values = new List<TValue>(0);

        public int Count => keys.Count;

        public CandiceDictionary()
        {
        }

        public CandiceDictionary(int capacity)
        {
            // COLD ALLOC: List<TKey>[capacity] - serialized GOAP key storage - owner: CandiceDictionary
            keys = new List<TKey>(capacity);
            // COLD ALLOC: List<TValue>[capacity] - serialized GOAP value storage - owner: CandiceDictionary
            values = new List<TValue>(capacity);
        }

        public TKey KeyAt(int index)
        {
            return keys[index];
        }

        public TValue ValueAt(int index)
        {
            return values[index];
        }

        public TValue this[TKey key]
        {
            get
            {
                int index = keys.IndexOf(key);
                if (index >= 0)
                {
                    return values[index];
                }
                else
                {
                    throw new KeyNotFoundException("Key not found");
                }
            }
            set
            {
                int index = keys.IndexOf(key);
                if (index >= 0)
                {
                    values[index] = value;
                }
                else
                {
                    keys.Add(key);
                    values.Add(value);
                }
            }
        }

        public bool ContainsKey(TKey key)
        {
            return keys.Contains(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            int index = keys.IndexOf(key);
            if (index >= 0)
            {
                value = values[index];
                return true;
            }
            else
            {
                value = default(TValue);
                return false;
            }
        }

        public void Add(TKey key, TValue value)
        {
            if (!ContainsKey(key))
            {
                keys.Add(key);
                values.Add(value);
            }
            else
            {
                return;
            }
        }

        public bool Remove(TKey key)
        {
            int index = keys.IndexOf(key);
            if (index >= 0)
            {
                keys.RemoveAt(index);
                values.RemoveAt(index);
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Clear()
        {
            keys.Clear();
            values.Clear();
        }
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            for (int i = 0; i < keys.Count; i++)
            {
                yield return new KeyValuePair<TKey, TValue>(keys[i], values[i]);
            }
        }
    }


}


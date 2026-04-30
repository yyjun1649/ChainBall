using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class TStatContainer<T>
{
    private Dictionary<T, List<TStatModifier<T>>> _mods = new Dictionary<T, List<TStatModifier<T>>>();

    private Dictionary<T, float> _cacheStats = new Dictionary<T, float>();

    public Dictionary<T, float> Stats => _cacheStats;

    public void Initialize()
    {
        foreach (var list in _mods.Values)
        {
            foreach (var modifier in list)
            {
                modifier.Dispose();
            }

            list.Clear();
        }
        
        _mods.Clear();
        _cacheStats.Clear();
    }

    public void CalculateStat()
    {
        _cacheStats.Clear();

        foreach (var mod in _mods)
        {
            var type = mod.Key;
            var list = mod.Value;

            float flat = 0f;
            float percentAdd = 0f;
            float percentMul = 1f;

            foreach (var modifier in list)
            {
                switch (modifier.ModifierType)
                {
                    case eModifierType.Flat:
                        flat += modifier.Value;
                        break;
                    case eModifierType.PercentAdd:
                        percentAdd += modifier.Value;
                        break;
                    case eModifierType.PercentMul:
                        percentMul *= (1f + modifier.Value);
                        break;
                }
            }

            var value = flat * (1f + percentAdd) * percentMul;

            if (!_cacheStats.ContainsKey(type))
            {
                _cacheStats.Add(type, value);
            }
            else
            {
                _cacheStats[type] += value;
            }
        }
    }

    public float GetStatValue(T type)
    {
        if (_cacheStats.TryGetValue(type, out var value))
        {
            return value;
        }

        Debug.LogWarning($"Stat {type} not initialized.");

        return 0;
    }

    public void AddModifier(TStatModifier<T> m, bool calculate = false)
    {
        if (!_mods.TryGetValue(m.StatType, out var list))
        {
            list = new List<TStatModifier<T>>();

            _mods.Add(m.StatType, list);
        }

        var existModifier = list.Find(x => x.ModifierId == m.ModifierId);

        if (existModifier != null)
        {
            list.Remove(existModifier);

            existModifier.Dispose();
        }

        list.Add(m);

        if (calculate)
        {
            CalculateStat();
        }
    }

    public void RemoveModifier(TStatModifier<T> m, bool calculate = false)
    {
        if (!_mods.TryGetValue(m.StatType, out var list))
        {
            return;
        }

        list.Remove(m);

        if (calculate)
        {
            CalculateStat();
        }
    }

    public void RemoveBySource(object source, bool calculate = false)
    {
        if (source == null) return;

        foreach (var list in _mods.Values)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(list[i].Source, source))
                {
                    list[i].Dispose();
                    list.RemoveAt(i);
                }
            }
        }

        if (calculate)
        {
            CalculateStat();
        }
    }

    public override string ToString()
    {
        StringBuilder stringBuilder = new StringBuilder();

        foreach (var stat in _cacheStats)
        {
            stringBuilder.Append($"{stat.Key} : {stat.Value}\n");
        }

        return stringBuilder.ToString();
    }
}

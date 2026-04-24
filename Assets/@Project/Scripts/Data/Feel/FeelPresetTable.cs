using System;
using MoreMountains.Feedbacks;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Feel/FeelPresetTable", fileName = "FeelPresetTable")]
public class FeelPresetTable : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public string key;
        public MMF_Player prefab;
    }

    [SerializeField] private Entry[] _entries;
    public Entry[] Entries => _entries;
}

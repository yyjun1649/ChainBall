using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace UIPrototyper
{
    public enum SpriteCategory { Icon, Button, Background, Decoration }

    [Serializable]
    public class SpriteEntry
    {
        public string key;                  // "icon.coin"
        public Sprite sprite;
        public string description;
        public SpriteCategory category;
        public Vector4 nineSliceBorder;     // L, B, R, T
    }

    [Serializable]
    public class FontEntry
    {
        public string key;                  // "font.title"
        public TMP_FontAsset font;
        public string description;
    }

    [Serializable]
    public class ColorEntry
    {
        public string key;                  // "color.primary"
        public Color color = Color.white;
        public string description;
    }

    [Serializable]
    public class FeelPresetEntry
    {
        public string key;                  // "button.press"
        public GameObject feelPrefab;       // MMF_Player가 붙은 Prefab
        public string description;
    }

    [Serializable]
    public class PrefabEntry
    {
        public string key;                  // "prefab.card-basic"
        public GameObject prefab;
        public string description;
    }

    [CreateAssetMenu(menuName = "UIPrototyper/Resource Registry")]
    public class UIResourceRegistry : ScriptableObject
    {
        public List<SpriteEntry> sprites = new();
        public List<FontEntry> fonts = new();
        public List<ColorEntry> colors = new();
        public List<FeelPresetEntry> feelPresets = new();
        public List<PrefabEntry> prefabs = new();

        public SpriteEntry GetSprite(string key) => sprites.Find(e => e.key == key);
        public FontEntry GetFont(string key) => fonts.Find(e => e.key == key);
        public ColorEntry GetColor(string key) => colors.Find(e => e.key == key);
        public FeelPresetEntry GetFeelPreset(string key) => feelPresets.Find(e => e.key == key);
        public PrefabEntry GetPrefab(string key) => prefabs.Find(e => e.key == key);
    }
}

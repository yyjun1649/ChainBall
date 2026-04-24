#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UIPrototyper.EditorTools
{
    public class MissingResourceWindow : EditorWindow
    {
        private static readonly (string label, string[] prefixes)[] Groups =
        {
            ("Colors",       new[] { "color." }),
            ("Fonts",        new[] { "font." }),
            ("Sprites",      new[] { "icon.", "btn.", "bg." }),
            ("Feel Presets", new[] { "button.", "screen." }),
            ("Prefab Refs",  new[] { "prefab." }),
            ("Other",        new string[0]),
        };

        private string[] _missing = System.Array.Empty<string>();
        private UIResourceRegistry _registry;
        private Vector2 _scroll;

        public static void Show(IEnumerable<string> missing, UIResourceRegistry registry)
        {
            var w = GetWindow<MissingResourceWindow>("Missing Resources");
            w.minSize = new Vector2(360, 240);
            w._registry = registry;
            w._missing = missing?.Distinct().ToArray() ?? System.Array.Empty<string>();
            w.Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Missing Resources", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (_missing.Length == 0)
            {
                EditorGUILayout.HelpBox("No missing resources.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                $"{_missing.Length} key(s) referenced by the generated UI were not found in the registry. " +
                "Add them to the registry and rebuild.",
                MessageType.Warning);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            var grouped = GroupByPrefix(_missing);
            foreach (var (label, _) in Groups)
            {
                if (!grouped.TryGetValue(label, out var keys) || keys.Count == 0) continue;

                EditorGUILayout.LabelField($"{label} ({keys.Count})", EditorStyles.boldLabel);
                foreach (var key in keys) DrawRow(key);
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy All"))
                EditorGUIUtility.systemCopyBuffer = string.Join("\n", _missing);
            if (_registry != null && GUILayout.Button("Ping Registry"))
                EditorGUIUtility.PingObject(_registry);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRow(string key)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.SelectableLabel(key, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (GUILayout.Button("Copy", GUILayout.Width(60)))
                EditorGUIUtility.systemCopyBuffer = key;
            EditorGUILayout.EndHorizontal();
        }

        private static Dictionary<string, List<string>> GroupByPrefix(string[] keys)
        {
            var result = new Dictionary<string, List<string>>();
            foreach (var (label, _) in Groups) result[label] = new List<string>();

            foreach (var key in keys)
            {
                var bucket = "Other";
                foreach (var (label, prefixes) in Groups)
                {
                    if (prefixes.Length == 0) continue;
                    if (prefixes.Any(p => key.StartsWith(p))) { bucket = label; break; }
                }
                result[bucket].Add(key);
            }

            foreach (var list in result.Values) list.Sort();
            return result;
        }
    }
}
#endif

# Unity UI Prototyper

AI 기반 Unity UI 프로토타이핑 툴. 레퍼런스 이미지를 Claude API에 보내 JSON으로 받고, uGUI Prefab으로 자동 변환합니다.

## 파이프라인

```
[레퍼런스 이미지 드래그]
   ↓
[EditorWindow → Claude API 호출 (이미지 + 리소스 시트)]
   ↓
[AI가 UI JSON 반환 (지정된 스키마)]
   ↓
[UIBuilder: JSON → uGUI Prefab]
   ↓
[Feel 프리셋 자동 부착]
   ↓
[Scene의 Canvas에 배치]
```

## 폴더 구조

```
Assets/UIPrototyper/
├── Runtime/
│   ├── UIPrototyper.Runtime.asmdef
│   ├── UIResourceRegistry.cs
│   ├── UINode.cs
│   └── UIBuilder.cs
└── Editor/
    ├── UIPrototyper.Editor.asmdef   (Runtime 참조)
    ├── ResourceSheetGenerator.cs
    ├── ClaudeAPIClient.cs
    └── UIPrototyperWindow.cs
```

## 의존성

- `com.unity.nuget.newtonsoft-json` (Package Manager → Add package by name)
- TextMeshPro (대부분 기본 포함)
- Feel / MoreMountains (선택 — 없어도 컴파일됨, 리플렉션으로 호출)

---

## 1. UIResourceRegistry.cs

리소스를 한 곳에서 관리하는 ScriptableObject. AI가 참조할 수 있는 "디자인 시스템"의 소스 오브 트루스.

```csharp
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

    [CreateAssetMenu(menuName = "UIPrototyper/Resource Registry")]
    public class UIResourceRegistry : ScriptableObject
    {
        public List<SpriteEntry> sprites = new();
        public List<FontEntry> fonts = new();
        public List<ColorEntry> colors = new();
        public List<FeelPresetEntry> feelPresets = new();

        public SpriteEntry GetSprite(string key) => sprites.Find(e => e.key == key);
        public FontEntry GetFont(string key) => fonts.Find(e => e.key == key);
        public ColorEntry GetColor(string key) => colors.Find(e => e.key == key);
        public FeelPresetEntry GetFeelPreset(string key) => feelPresets.Find(e => e.key == key);
    }
}
```

---

## 2. UINode.cs

JSON 스키마. AI가 이 형식으로만 출력하도록 프롬프트에서 강제.

```csharp
using System;
using System.Collections.Generic;

namespace UIPrototyper
{
    [Serializable]
    public class UINode
    {
        public string type;          // Frame, VStack, HStack, Grid, Text, Image, Button, Spacer
        public string name;
        public string text;          // Text, Button용
        public string icon;          // Button에 아이콘 붙일 때 sprite key
        public string feel;          // feel preset key

        public UISize size;
        public UIStyle style;
        public UILayout layout;

        public List<UINode> children;
    }

    [Serializable]
    public class UISize
    {
        public string mode = "fixed";   // "fixed" | "fill" | "hug"
        public float w;
        public float h;
    }

    [Serializable]
    public class UIStyle
    {
        public string bg;            // color key or sprite key
        public string color;         // text color key
        public string font;          // font key
        public float fontSize;
        public float radius;
    }

    [Serializable]
    public class UILayout
    {
        public string anchor = "center";
        public float padding;
        public float spacing;
        public string align = "center";
    }

    [Serializable]
    public class GeneratedUI
    {
        public UINode root;
        public List<string> missingResources;
    }
}
```

---

## 3. UIBuilder.cs

JSON → uGUI Prefab 파서. Switch 기반으로 타입별 빌더 호출.

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UIPrototyper
{
    public class UIBuilder
    {
        private readonly UIResourceRegistry registry;
        public List<string> MissingResources { get; } = new();

        public UIBuilder(UIResourceRegistry registry)
        {
            this.registry = registry;
        }

        public GameObject Build(UINode node, Transform parent)
        {
            if (node == null) return null;

            GameObject go = node.type switch
            {
                "Frame"   => BuildFrame(node, parent),
                "VStack"  => BuildStack(node, parent, vertical: true),
                "HStack"  => BuildStack(node, parent, vertical: false),
                "Grid"    => BuildGrid(node, parent),
                "Text"    => BuildText(node, parent),
                "Image"   => BuildImage(node, parent),
                "Button"  => BuildButton(node, parent),
                "Spacer"  => BuildSpacer(node, parent),
                _         => BuildUnknown(node, parent)
            };

            if (go != null && node.children != null)
            {
                foreach (var child in node.children)
                    Build(child, go.transform);
            }

            return go;
        }

        // ===== 타입별 빌더 =====

        private GameObject BuildFrame(UINode node, Transform parent)
        {
            var go = CreateRect(node.name ?? "Frame", parent);
            var img = go.AddComponent<Image>();
            ApplyBg(img, node.style);
            ApplySize(go, node);
            ApplyAnchor(go, node.layout?.anchor ?? "stretch");
            return go;
        }

        private GameObject BuildStack(UINode node, Transform parent, bool vertical)
        {
            var go = CreateRect(node.name ?? (vertical ? "VStack" : "HStack"), parent);
            ApplySize(go, node);
            ApplyAnchor(go, node.layout?.anchor ?? "top-left");

            if (node.style?.bg != null)
            {
                var img = go.AddComponent<Image>();
                ApplyBg(img, node.style);
            }

            if (vertical)
            {
                var g = go.AddComponent<VerticalLayoutGroup>();
                ConfigureLayoutGroup(g, node);
            }
            else
            {
                var g = go.AddComponent<HorizontalLayoutGroup>();
                ConfigureLayoutGroup(g, node);
            }

            if (node.size?.mode == "hug")
            {
                var fitter = go.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            return go;
        }

        private GameObject BuildGrid(UINode node, Transform parent)
        {
            var go = CreateRect(node.name ?? "Grid", parent);
            ApplySize(go, node);
            ApplyAnchor(go, node.layout?.anchor ?? "top-left");

            var g = go.AddComponent<GridLayoutGroup>();
            var pad = (int)(node.layout?.padding ?? 0);
            g.padding = new RectOffset(pad, pad, pad, pad);
            g.spacing = new Vector2(node.layout?.spacing ?? 0, node.layout?.spacing ?? 0);
            g.cellSize = new Vector2(128, 128);
            return go;
        }

        private GameObject BuildText(UINode node, Transform parent)
        {
            var go = CreateRect(node.name ?? "Text", parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = node.text ?? "";
            tmp.fontSize = node.style?.fontSize > 0 ? node.style.fontSize : 24;
            tmp.alignment = TextAlignmentOptions.Center;

            ApplyTextColor(tmp, node.style);
            ApplyFont(tmp, node.style);
            ApplySize(go, node);
            ApplyAnchor(go, node.layout?.anchor ?? "center");
            return go;
        }

        private GameObject BuildImage(UINode node, Transform parent)
        {
            var go = CreateRect(node.name ?? "Image", parent);
            var img = go.AddComponent<Image>();
            ApplyBg(img, node.style);
            ApplySize(go, node);
            ApplyAnchor(go, node.layout?.anchor ?? "center");
            return go;
        }

        private GameObject BuildButton(UINode node, Transform parent)
        {
            var go = CreateRect(node.name ?? "Button", parent);

            var img = go.AddComponent<Image>();
            ApplyBg(img, node.style);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            ApplySize(go, node);
            ApplyAnchor(go, node.layout?.anchor ?? "center");

            if (!string.IsNullOrEmpty(node.text))
            {
                BuildText(new UINode
                {
                    type = "Text",
                    name = "Label",
                    text = node.text,
                    style = new UIStyle
                    {
                        color = node.style?.color,
                        font = node.style?.font,
                        fontSize = node.style?.fontSize ?? 28
                    },
                    layout = new UILayout { anchor = "stretch" }
                }, go.transform);
            }

            if (!string.IsNullOrEmpty(node.feel))
                AttachFeel(go, btn, node.feel);

            return go;
        }

        private GameObject BuildSpacer(UINode node, Transform parent)
        {
            var go = CreateRect(node.name ?? "Spacer", parent);
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            le.flexibleHeight = 1;
            return go;
        }

        private GameObject BuildUnknown(UINode node, Transform parent)
        {
            Debug.LogWarning($"[UIBuilder] Unknown type: {node.type}");
            var go = CreateRect($"Unknown_{node.type}", parent);
            var img = go.AddComponent<Image>();
            img.color = new Color(1, 0, 1, 0.5f);
            ApplySize(go, node);
            return go;
        }

        // ===== 헬퍼 =====

        private GameObject CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private void ApplyBg(Image img, UIStyle style)
        {
            if (style == null) { img.color = Color.white; return; }

            if (!string.IsNullOrEmpty(style.bg))
            {
                if (style.bg.StartsWith("color."))
                {
                    var ce = registry.GetColor(style.bg);
                    if (ce != null) { img.color = ce.color; }
                    else { img.color = Magenta(); MissingResources.Add(style.bg); }
                }
                else
                {
                    var se = registry.GetSprite(style.bg);
                    if (se != null && se.sprite != null)
                    {
                        img.sprite = se.sprite;
                        if (se.nineSliceBorder != Vector4.zero) img.type = Image.Type.Sliced;
                        img.color = Color.white;
                    }
                    else
                    {
                        img.color = Magenta();
                        MissingResources.Add(style.bg);
                    }
                }
            }
            else
            {
                img.color = new Color(0, 0, 0, 0);
            }
        }

        private void ApplyTextColor(TextMeshProUGUI tmp, UIStyle style)
        {
            if (style?.color == null) { tmp.color = Color.white; return; }
            var ce = registry.GetColor(style.color);
            if (ce != null) tmp.color = ce.color;
            else { tmp.color = Magenta(); MissingResources.Add(style.color); }
        }

        private void ApplyFont(TextMeshProUGUI tmp, UIStyle style)
        {
            if (style?.font == null) return;
            var fe = registry.GetFont(style.font);
            if (fe != null && fe.font != null) tmp.font = fe.font;
            else MissingResources.Add(style.font);
        }

        private void ApplySize(GameObject go, UINode node)
        {
            var rt = go.GetComponent<RectTransform>();
            if (node.size == null) return;

            switch (node.size.mode)
            {
                case "fill": break;
                case "hug":  break;
                default:
                    rt.sizeDelta = new Vector2(node.size.w, node.size.h);
                    break;
            }
        }

        private void ApplyAnchor(GameObject go, string anchor)
        {
            var rt = go.GetComponent<RectTransform>();
            switch (anchor)
            {
                case "top-left":
                    rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1);
                    rt.pivot = new Vector2(0, 1); break;
                case "top-center":
                    rt.anchorMin = new Vector2(0.5f, 1); rt.anchorMax = new Vector2(0.5f, 1);
                    rt.pivot = new Vector2(0.5f, 1); break;
                case "top-right":
                    rt.anchorMin = new Vector2(1, 1); rt.anchorMax = new Vector2(1, 1);
                    rt.pivot = new Vector2(1, 1); break;
                case "center":
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f); break;
                case "bottom-stretch":
                    rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
                    rt.pivot = new Vector2(0.5f, 0);
                    rt.offsetMin = rt.offsetMax = Vector2.zero; break;
                case "top-stretch":
                    rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
                    rt.pivot = new Vector2(0.5f, 1);
                    rt.offsetMin = rt.offsetMax = Vector2.zero; break;
                case "stretch":
                default:
                    rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.offsetMin = rt.offsetMax = Vector2.zero; break;
            }
        }

        private void ConfigureLayoutGroup(HorizontalOrVerticalLayoutGroup g, UINode node)
        {
            var pad = (int)(node.layout?.padding ?? 0);
            g.padding = new RectOffset(pad, pad, pad, pad);
            g.spacing = node.layout?.spacing ?? 0;
            g.childAlignment = TextAnchor.MiddleCenter;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = node.layout?.align == "stretch";
            g.childForceExpandHeight = node.layout?.align == "stretch";
        }

        private void AttachFeel(GameObject target, Button btn, string feelKey)
        {
            var preset = registry.GetFeelPreset(feelKey);
            if (preset == null || preset.feelPrefab == null)
            {
                MissingResources.Add(feelKey);
                return;
            }

            var feelInstance = Object.Instantiate(preset.feelPrefab, target.transform);
            feelInstance.name = "_Feel";

            // Feel 의존성 없이 리플렉션으로 MMF_Player 호출
            var feelComponent = feelInstance.GetComponent("MMF_Player");
            if (feelComponent != null)
            {
                var method = feelComponent.GetType().GetMethod("PlayFeedbacks",
                    new System.Type[0]);
                if (method != null)
                {
                    btn.onClick.AddListener(() => method.Invoke(feelComponent, null));
                }
            }
        }

        private Color Magenta() => new Color(1, 0, 1, 0.8f);
    }
}
```

---

## 4. ResourceSheetGenerator.cs (Editor)

레지스트리의 리소스들을 PNG 시트로 만듭니다. Claude에게 "어떤 리소스가 있는지" 시각적으로 보여주는 용도. 임시 Canvas + RenderTexture 방식.

```csharp
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace UIPrototyper.EditorTools
{
    public class ResourceSheetGenerator
    {
        public static Dictionary<string, string> GenerateAll(
            UIResourceRegistry registry,
            string outputFolder)
        {
            Directory.CreateDirectory(outputFolder);
            var result = new Dictionary<string, string>();

            foreach (SpriteCategory cat in System.Enum.GetValues(typeof(SpriteCategory)))
            {
                var entries = registry.sprites.FindAll(e => e.category == cat);
                if (entries.Count == 0) continue;

                var name = cat.ToString().ToLower();
                var path = Path.Combine(outputFolder, $"sheet_{name}.png");
                GenerateSpriteSheet(entries, cat.ToString(), path);
                result[name] = path;
            }

            if (registry.colors.Count > 0)
            {
                var path = Path.Combine(outputFolder, "sheet_colors.png");
                GenerateColorSheet(registry.colors, path);
                result["colors"] = path;
            }

            AssetDatabase.Refresh();
            return result;
        }

        private static void GenerateSpriteSheet(
            List<SpriteEntry> entries, string title, string outputPath)
        {
            const int cellSize = 128;
            const int columns = 6;
            const int padding = 20;
            const int labelHeight = 36;
            const int titleHeight = 60;

            int rows = Mathf.CeilToInt(entries.Count / (float)columns);
            int cellW = cellSize + padding;
            int cellH = cellSize + labelHeight + padding;
            int sheetW = columns * cellW + padding;
            int sheetH = rows * cellH + padding + titleHeight;

            var canvasGo = new GameObject("_SheetCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRt = canvas.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(sheetW, sheetH);

            var bg = CreateUIChild(canvasGo.transform, "BG");
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.4f, 0.4f, 0.45f, 1f);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

            var titleGo = CreateUIChild(canvasGo.transform, "Title");
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = $"=== {title.ToUpper()} ===";
            titleTmp.fontSize = 32;
            titleTmp.color = Color.white;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.fontStyle = FontStyles.Bold;
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0, 1);
            titleRt.anchorMax = new Vector2(1, 1);
            titleRt.pivot = new Vector2(0.5f, 1);
            titleRt.sizeDelta = new Vector2(0, titleHeight);

            for (int i = 0; i < entries.Count; i++)
            {
                int col = i % columns;
                int row = i / columns;
                float x = padding + col * cellW;
                float y = -(titleHeight + padding + row * cellH);
                CreateCell(canvasGo.transform, entries[i], x, y, cellSize, labelHeight);
            }

            var tex = RenderCanvasToTexture(canvasGo, sheetW, sheetH);
            File.WriteAllBytes(outputPath, tex.EncodeToPNG());
            Debug.Log($"[Sheet] {outputPath} ({entries.Count} items)");

            Object.DestroyImmediate(canvasGo);
            Object.DestroyImmediate(tex);
        }

        private static void GenerateColorSheet(List<ColorEntry> colors, string outputPath)
        {
            const int cellSize = 160;
            const int columns = 4;
            const int padding = 20;
            const int labelHeight = 40;
            const int titleHeight = 60;

            int rows = Mathf.CeilToInt(colors.Count / (float)columns);
            int cellW = cellSize + padding;
            int cellH = cellSize + labelHeight + padding;
            int sheetW = columns * cellW + padding;
            int sheetH = rows * cellH + padding + titleHeight;

            var canvasGo = new GameObject("_ColorSheet");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(sheetW, sheetH);

            var bg = CreateUIChild(canvasGo.transform, "BG");
            bg.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.22f, 1f);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

            var titleGo = CreateUIChild(canvasGo.transform, "Title");
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "=== COLORS ===";
            titleTmp.fontSize = 32;
            titleTmp.color = Color.white;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.fontStyle = FontStyles.Bold;
            var tRt = titleGo.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0, 1); tRt.anchorMax = new Vector2(1, 1);
            tRt.pivot = new Vector2(0.5f, 1);
            tRt.sizeDelta = new Vector2(0, titleHeight);

            for (int i = 0; i < colors.Count; i++)
            {
                int col = i % columns;
                int row = i / columns;
                float x = padding + col * cellW;
                float y = -(titleHeight + padding + row * cellH);
                CreateColorCell(canvasGo.transform, colors[i], x, y, cellSize, labelHeight);
            }

            var tex = RenderCanvasToTexture(canvasGo, sheetW, sheetH);
            File.WriteAllBytes(outputPath, tex.EncodeToPNG());
            Debug.Log($"[Sheet] {outputPath} ({colors.Count} colors)");

            Object.DestroyImmediate(canvasGo);
            Object.DestroyImmediate(tex);
        }

        private static void CreateCell(Transform parent, SpriteEntry entry,
            float x, float y, float size, float labelHeight)
        {
            var cell = CreateUIChild(parent, entry.key);
            var cellRt = cell.GetComponent<RectTransform>();
            cellRt.anchorMin = new Vector2(0, 1);
            cellRt.anchorMax = new Vector2(0, 1);
            cellRt.pivot = new Vector2(0, 1);
            cellRt.sizeDelta = new Vector2(size, size + labelHeight);
            cellRt.anchoredPosition = new Vector2(x, y);

            var cellBg = CreateUIChild(cell.transform, "CellBg");
            var cellBgImg = cellBg.AddComponent<Image>();
            cellBgImg.color = new Color(0.3f, 0.3f, 0.32f, 1f);
            var cbRt = cellBg.GetComponent<RectTransform>();
            cbRt.anchorMin = new Vector2(0, 1);
            cbRt.anchorMax = new Vector2(1, 1);
            cbRt.pivot = new Vector2(0.5f, 1);
            cbRt.sizeDelta = new Vector2(0, size);
            cbRt.anchoredPosition = Vector2.zero;

            if (entry.sprite != null)
            {
                var spriteGo = CreateUIChild(cell.transform, "Sprite");
                var spriteImg = spriteGo.AddComponent<Image>();
                spriteImg.sprite = entry.sprite;
                spriteImg.preserveAspect = true;
                var sRt = spriteGo.GetComponent<RectTransform>();
                sRt.anchorMin = new Vector2(0, 1);
                sRt.anchorMax = new Vector2(1, 1);
                sRt.pivot = new Vector2(0.5f, 1);
                sRt.sizeDelta = new Vector2(-16, size - 16);
                sRt.anchoredPosition = new Vector2(0, -8);
            }

            var labelGo = CreateUIChild(cell.transform, "Label");
            var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.text = entry.key;
            labelTmp.fontSize = 16;
            labelTmp.color = Color.white;
            labelTmp.alignment = TextAlignmentOptions.Center;
            var lRt = labelGo.GetComponent<RectTransform>();
            lRt.anchorMin = new Vector2(0, 0);
            lRt.anchorMax = new Vector2(1, 0);
            lRt.pivot = new Vector2(0.5f, 0);
            lRt.sizeDelta = new Vector2(0, labelHeight);
            lRt.anchoredPosition = Vector2.zero;
        }

        private static void CreateColorCell(Transform parent, ColorEntry entry,
            float x, float y, float size, float labelHeight)
        {
            var cell = CreateUIChild(parent, entry.key);
            var cellRt = cell.GetComponent<RectTransform>();
            cellRt.anchorMin = new Vector2(0, 1);
            cellRt.anchorMax = new Vector2(0, 1);
            cellRt.pivot = new Vector2(0, 1);
            cellRt.sizeDelta = new Vector2(size, size + labelHeight);
            cellRt.anchoredPosition = new Vector2(x, y);

            var swatch = CreateUIChild(cell.transform, "Swatch");
            swatch.AddComponent<Image>().color = entry.color;
            var sRt = swatch.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0, 1); sRt.anchorMax = new Vector2(1, 1);
            sRt.pivot = new Vector2(0.5f, 1);
            sRt.sizeDelta = new Vector2(0, size);
            sRt.anchoredPosition = Vector2.zero;

            var labelGo = CreateUIChild(cell.transform, "Label");
            var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.text = $"{entry.key}\n#{ColorUtility.ToHtmlStringRGB(entry.color)}";
            labelTmp.fontSize = 14;
            labelTmp.color = Color.white;
            labelTmp.alignment = TextAlignmentOptions.Center;
            var lRt = labelGo.GetComponent<RectTransform>();
            lRt.anchorMin = new Vector2(0, 0); lRt.anchorMax = new Vector2(1, 0);
            lRt.pivot = new Vector2(0.5f, 0);
            lRt.sizeDelta = new Vector2(0, labelHeight);
            lRt.anchoredPosition = Vector2.zero;
        }

        private static GameObject CreateUIChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Texture2D RenderCanvasToTexture(GameObject canvasGo, int w, int h)
        {
            var camGo = new GameObject("_SheetCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear;
            cam.orthographic = true;
            cam.orthographicSize = h / 2f;
            cam.transform.position = new Vector3(w / 2f, -h / 2f, -10);
            cam.cullingMask = 1 << 5;

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.worldCamera = cam;
            canvasGo.transform.position = new Vector3(0, 0, 0);

            SetLayerRecursive(canvasGo, 5);

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);

            return tex;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
```

---

## 5. ClaudeAPIClient.cs (Editor)

Claude API에 이미지 + 리소스 시트들을 멀티모달로 전송. 프롬프트에 리소스 키 목록과 스키마를 주입.

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace UIPrototyper.EditorTools
{
    public static class ClaudeAPIClient
    {
        private const string API_URL = "https://api.anthropic.com/v1/messages";
        private const string MODEL = "claude-opus-4-7";
        private const string VERSION = "2023-06-01";

        public static async Task<string> GenerateUIJson(
            string apiKey,
            Texture2D referenceImage,
            Dictionary<string, string> sheetPaths,
            UIResourceRegistry registry)
        {
            var prompt = BuildPrompt(registry, sheetPaths);
            var content = new List<object>();

            // 시트 이미지들 먼저 (AI가 먼저 리소스 파악)
            foreach (var kvp in sheetPaths)
            {
                var bytes = File.ReadAllBytes(kvp.Value);
                content.Add(new { type = "text", text = $"--- Resource Sheet: {kvp.Key} ---" });
                content.Add(ImageBlock(bytes));
            }

            // 레퍼런스 이미지
            content.Add(new { type = "text", text = "--- Reference UI ---" });
            content.Add(ImageBlock(referenceImage.EncodeToPNG()));

            // 프롬프트 텍스트
            content.Add(new { type = "text", text = prompt });

            var body = new
            {
                model = MODEL,
                max_tokens = 4096,
                messages = new[] { new { role = "user", content = content.ToArray() } }
            };

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(body);
            var req = new UnityWebRequest(API_URL, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("x-api-key", apiKey);
            req.SetRequestHeader("anthropic-version", VERSION);

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"API Error: {req.error}\n{req.downloadHandler.text}");

            return ExtractJsonFromResponse(req.downloadHandler.text);
        }

        private static object ImageBlock(byte[] pngBytes)
        {
            return new
            {
                type = "image",
                source = new
                {
                    type = "base64",
                    media_type = "image/png",
                    data = Convert.ToBase64String(pngBytes)
                }
            };
        }

        private static string BuildPrompt(
            UIResourceRegistry registry,
            Dictionary<string, string> sheetPaths)
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are a Unity UI designer. Convert the reference UI image into a JSON structure.");
            sb.AppendLine();
            sb.AppendLine("## Available Resources");
            sb.AppendLine("Use ONLY the keys listed below (also shown in the attached resource sheets).");
            sb.AppendLine();

            sb.AppendLine("### Colors");
            foreach (var c in registry.colors)
                sb.AppendLine($"- {c.key}: #{ColorUtility.ToHtmlStringRGB(c.color)} ({c.description})");

            sb.AppendLine();
            sb.AppendLine("### Fonts");
            foreach (var f in registry.fonts)
                sb.AppendLine($"- {f.key}: {f.description}");

            sb.AppendLine();
            sb.AppendLine("### Sprites");
            foreach (var s in registry.sprites)
                sb.AppendLine($"- {s.key} ({s.category}): {s.description}");

            sb.AppendLine();
            sb.AppendLine("### Feel Presets (for Buttons)");
            foreach (var fp in registry.feelPresets)
                sb.AppendLine($"- {fp.key}: {fp.description}");

            sb.AppendLine();
            sb.AppendLine("## Schema");
            sb.AppendLine(@"
type UINode = {
  type: 'Frame' | 'VStack' | 'HStack' | 'Grid' | 'Text' | 'Image' | 'Button' | 'Spacer',
  name?: string,
  text?: string,          // Text, Button
  icon?: string,          // sprite key
  feel?: string,          // feel preset key (Button only)
  size?: { mode: 'fixed'|'fill'|'hug', w: number, h: number },
  style?: {
    bg?: string,          // color key or sprite key
    color?: string,       // color key (text)
    font?: string,        // font key
    fontSize?: number,
    radius?: number
  },
  layout?: {
    anchor?: 'center'|'top-left'|'top-stretch'|'bottom-stretch'|'stretch'|...,
    padding?: number,
    spacing?: number,
    align?: 'stretch'|'center'|'start'|'end'
  },
  children?: UINode[]
}
");

            sb.AppendLine("## Rules");
            sb.AppendLine("- Root MUST be a Frame with anchor 'stretch'.");
            sb.AppendLine("- Use VStack/HStack for layout. Avoid absolute positioning.");
            sb.AppendLine("- Prefer 'fill' (stretch parent) and 'hug' (fit content) over fixed sizes.");
            sb.AppendLine("- Only reference resource keys from the list above. No raw hex colors or unlisted fonts.");
            sb.AppendLine("- If no exact resource match, pick the closest one.");
            sb.AppendLine("- Every Button should have a 'feel' preset.");
            sb.AppendLine();
            sb.AppendLine("## Output");
            sb.AppendLine("Return ONLY a JSON code block. No explanation before or after.");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine("{ \"root\": { ... } }");
            sb.AppendLine("```");

            return sb.ToString();
        }

        private static string ExtractJsonFromResponse(string apiResponse)
        {
            var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<ClaudeResponse>(apiResponse);
            if (parsed?.content == null || parsed.content.Length == 0)
                throw new Exception("Empty response from Claude");

            var text = parsed.content[0].text;
            var match = System.Text.RegularExpressions.Regex.Match(
                text, @"```(?:json)?\s*(\{.*?\})\s*```",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            if (!match.Success) return text.Trim();
            return match.Groups[1].Value;
        }

        [Serializable]
        private class ClaudeResponse
        {
            public ContentBlock[] content;
        }

        [Serializable]
        private class ContentBlock
        {
            public string type;
            public string text;
        }
    }
}
```

---

## 6. UIPrototyperWindow.cs (Editor)

메인 에디터 윈도우. API Key 저장, 이미지 드롭존, Generate 버튼, JSON 프리뷰, 재빌드.

```csharp
using System.IO;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

namespace UIPrototyper.EditorTools
{
    public class UIPrototyperWindow : EditorWindow
    {
        private const string PREF_API_KEY = "UIPrototyper.ApiKey";
        private const string PREF_OUTPUT = "UIPrototyper.OutputFolder";

        private UIResourceRegistry registry;
        private Texture2D referenceImage;
        private string apiKey = "";
        private string outputFolder = "Assets/UIPrototyper/Generated";
        private string sheetFolder = "Assets/UIPrototyper/Sheets";
        private string lastResult = "";
        private string statusMsg = "Ready";
        private bool isGenerating;

        [MenuItem("Tools/UI Prototyper")]
        public static void Open()
        {
            var w = GetWindow<UIPrototyperWindow>("UI Prototyper");
            w.minSize = new Vector2(420, 520);
        }

        private void OnEnable()
        {
            apiKey = EditorPrefs.GetString(PREF_API_KEY, "");
            outputFolder = EditorPrefs.GetString(PREF_OUTPUT, outputFolder);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("UI Prototyper", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            apiKey = EditorGUILayout.PasswordField("Claude API Key", apiKey);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetString(PREF_API_KEY, apiKey);

            registry = (UIResourceRegistry)EditorGUILayout.ObjectField(
                "Resource Registry", registry, typeof(UIResourceRegistry), false);

            EditorGUI.BeginChangeCheck();
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetString(PREF_OUTPUT, outputFolder);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Step 1. Resource Sheets", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(registry == null))
            {
                if (GUILayout.Button("Regenerate Resource Sheets"))
                {
                    var paths = ResourceSheetGenerator.GenerateAll(registry, sheetFolder);
                    statusMsg = $"Generated {paths.Count} sheets";
                }
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Step 2. Reference Image", EditorStyles.boldLabel);
            referenceImage = (Texture2D)EditorGUILayout.ObjectField(
                "Image", referenceImage, typeof(Texture2D), false);

            DrawDropZone();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Step 3. Generate UI", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(
                registry == null || referenceImage == null ||
                string.IsNullOrEmpty(apiKey) || isGenerating))
            {
                if (GUILayout.Button(isGenerating ? "Generating..." : "Generate UI",
                    GUILayout.Height(40)))
                {
                    Generate();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Status", statusMsg, EditorStyles.helpBox);

            if (!string.IsNullOrEmpty(lastResult))
            {
                EditorGUILayout.LabelField("Last Generated JSON", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(lastResult, GUILayout.MinHeight(120));

                if (GUILayout.Button("Build Prefab from JSON"))
                    BuildFromJson(lastResult);
            }
        }

        private void DrawDropZone()
        {
            var rect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "Drop image here", EditorStyles.helpBox);

            var evt = Event.current;
            if (rect.Contains(evt.mousePosition))
            {
                if (evt.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.Use();
                }
                else if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is Texture2D tex)
                        {
                            referenceImage = tex;
                            break;
                        }
                    }
                    evt.Use();
                }
            }
        }

        private async void Generate()
        {
            isGenerating = true;
            statusMsg = "Preparing resource sheets...";
            Repaint();

            try
            {
                var sheetPaths = ResourceSheetGenerator.GenerateAll(registry, sheetFolder);

                statusMsg = "Calling Claude API... (may take 10-30s)";
                Repaint();

                var json = await ClaudeAPIClient.GenerateUIJson(
                    apiKey, referenceImage, sheetPaths, registry);

                lastResult = PrettyPrint(json);
                statusMsg = "Generation complete!";

                Directory.CreateDirectory(outputFolder);
                var jsonPath = Path.Combine(outputFolder,
                    $"{referenceImage.name}_ui.json");
                File.WriteAllText(jsonPath, lastResult);
                AssetDatabase.Refresh();

                BuildFromJson(lastResult);
            }
            catch (System.Exception e)
            {
                statusMsg = $"Error: {e.Message}";
                Debug.LogException(e);
            }
            finally
            {
                isGenerating = false;
                Repaint();
            }
        }

        private void BuildFromJson(string json)
        {
            try
            {
                var generated = JsonConvert.DeserializeObject<GeneratedUI>(json);
                if (generated?.root == null)
                {
                    statusMsg = "Invalid JSON: no root node";
                    return;
                }

                var canvas = FindOrCreateCanvas();

                var builder = new UIBuilder(registry);
                var root = builder.Build(generated.root, canvas.transform);
                root.name = $"Generated_{referenceImage.name}";

                Selection.activeGameObject = root;
                EditorGUIUtility.PingObject(root);

                if (builder.MissingResources.Count > 0)
                {
                    var missing = string.Join(", ", builder.MissingResources);
                    statusMsg = $"Built, but missing: {missing}";
                    Debug.LogWarning($"[UIPrototyper] Missing resources: {missing}");
                }
                else
                {
                    statusMsg = "Prefab built successfully!";
                }
            }
            catch (System.Exception e)
            {
                statusMsg = $"Build error: {e.Message}";
                Debug.LogException(e);
            }
        }

        private Canvas FindOrCreateCanvas()
        {
            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var go = new GameObject("Canvas",
                    typeof(Canvas),
                    typeof(UnityEngine.UI.CanvasScaler),
                    typeof(UnityEngine.UI.GraphicRaycaster));
                canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            return canvas;
        }

        private string PrettyPrint(string json)
        {
            try
            {
                var obj = JsonConvert.DeserializeObject(json);
                return JsonConvert.SerializeObject(obj, Formatting.Indented);
            }
            catch { return json; }
        }
    }
}
```

---

## 사용 순서

1. Registry 생성: `Project 창 우클릭 → Create → UIPrototyper → Resource Registry`
2. 리소스 등록:
   - Sprite: 키·description·category 채우기
   - Color, Font, Feel Preset 동일
3. `Tools → UI Prototyper` 열기
4. API Key 입력, Registry 연결
5. `Regenerate Resource Sheets` 클릭 → `Assets/UIPrototyper/Sheets/`에 PNG 생성 확인
6. 레퍼런스 이미지 드롭 → `Generate UI` 클릭
7. Scene의 Canvas 아래 자동 생성된 UI Prefab 확인

## 네이밍 컨벤션

```
color.*      색상           color.primary, color.bg, color.text-muted
font.*       폰트           font.title, font.body, font.number
icon.*       아이콘 Sprite  icon.coin, icon.heart, icon.settings
btn.*        버튼 Sprite    btn.primary, btn.secondary, btn.danger
bg.*         배경 Sprite    bg.panel-dark, bg.card
button.*     Feel 프리셋    button.press, button.hover
screen.*     Feel 프리셋    screen.enter, screen.exit
```

## 알려진 한계

- `ContentSizeFitter` + `LayoutGroup` 중첩 시 한 프레임 튐 → 필요하면 `Canvas.ForceUpdateCanvases()` 추가
- Sprite의 **Read/Write Enabled**가 꺼져있으면 시트 생성 시 이미지 누락 가능
- Grid `cellSize`가 하드코딩 (128) — 스키마에 `grid.cellSize` 추가 필요
- Feel 연결은 리플렉션 기반이라 MoreMountains 네임스페이스 변경 시 수정 필요

## 다음 단계 (v0.2)

- **PrefabRef** 지원: 자주 쓰는 카드·리스트 아이템 Prefab을 JSON에서 참조
- **2단계 호출**: 리소스 100+ 일 때 먼저 필요 카테고리 식별
- **수정 모드**: 기존 생성물 + 새 이미지 → diff 기반 재생성
- **Missing Resource 리포트 창**: 빠진 리소스만 모아서 체크리스트로

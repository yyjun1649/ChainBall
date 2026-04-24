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

            // 카테고리별로 스프라이트 시트 생성
            foreach (SpriteCategory cat in System.Enum.GetValues(typeof(SpriteCategory)))
            {
                var entries = registry.sprites.FindAll(e => e.category == cat);
                if (entries.Count == 0) continue;

                var name = cat.ToString().ToLower();
                var path = Path.Combine(outputFolder, $"sheet_{name}.png");
                GenerateSpriteSheet(entries, cat.ToString(), path);
                result[name] = path;
            }

            // 색상 팔레트 시트
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

            // 임시 Canvas + Camera로 렌더링.
            // pivot을 좌상단(0,1)으로 두면 canvas의 로컬 (0,0)이 월드 (0,0)과 일치 →
            // 카메라 프러스텀 [0,w]x[-h,0]와 캔버스 영역이 정확히 맞는다.
            var canvasGo = new GameObject("_SheetCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRt = canvas.GetComponent<RectTransform>();
            canvasRt.pivot = new Vector2(0, 1);
            canvasRt.sizeDelta = new Vector2(sheetW, sheetH);

            // 배경
            var bg = CreateUIChild(canvasGo.transform, "BG");
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.4f, 0.4f, 0.45f, 1f);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

            // 타이틀
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
            titleRt.anchoredPosition = new Vector2(0, 0);

            // 각 셀 배치
            for (int i = 0; i < entries.Count; i++)
            {
                int col = i % columns;
                int row = i / columns;
                float x = padding + col * cellW;
                float y = -(titleHeight + padding + row * cellH);

                CreateCell(canvasGo.transform, entries[i], x, y, cellSize, labelHeight);
            }

            // RenderTexture로 캡쳐
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
            var canvasRt = canvas.GetComponent<RectTransform>();
            canvasRt.pivot = new Vector2(0, 1);
            canvasRt.sizeDelta = new Vector2(sheetW, sheetH);

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

            // 셀 배경 (체크무늬 대용 단색)
            var cellBg = CreateUIChild(cell.transform, "CellBg");
            var cellBgImg = cellBg.AddComponent<Image>();
            cellBgImg.color = new Color(0.3f, 0.3f, 0.32f, 1f);
            var cbRt = cellBg.GetComponent<RectTransform>();
            cbRt.anchorMin = new Vector2(0, 1);
            cbRt.anchorMax = new Vector2(1, 1);
            cbRt.pivot = new Vector2(0.5f, 1);
            cbRt.sizeDelta = new Vector2(0, size);
            cbRt.anchoredPosition = Vector2.zero;

            // 스프라이트
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

            // 라벨
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
            // 캡쳐용 카메라
            var camGo = new GameObject("_SheetCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear;
            cam.orthographic = true;
            cam.orthographicSize = h / 2f;
            cam.transform.position = new Vector3(w / 2f, -h / 2f, -10);
            var uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer < 0) uiLayer = 5; // fallback
            cam.cullingMask = 1 << uiLayer;

            // Canvas를 WorldSpace로, 캔버스 좌상단이 (0,0)
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.worldCamera = cam;
            canvasGo.transform.position = new Vector3(0, 0, 0);

            SetLayerRecursive(canvasGo, uiLayer);

            // RenderTexture에 렌더
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

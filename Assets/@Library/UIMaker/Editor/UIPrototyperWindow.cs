using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

namespace UIPrototyper.EditorTools
{
    public class UIPrototyperWindow : EditorWindow
    {
        private const string PREF_API_KEY = "UIPrototyper.ApiKey";
        private const string PREF_OUTPUT = "UIPrototyper.OutputFolder";
        private const string PREF_AUTO   = "UIPrototyper.AutoAdvance";
        private const string PREF_RES_W  = "UIPrototyper.ResW";
        private const string PREF_RES_H  = "UIPrototyper.ResH";
        private const string PROTOTYPER_CANVAS_NAME = "UIPrototyperCanvas";

        private UIResourceRegistry registry;
        private Texture2D referenceImage;
        private string apiKey = "";
        private string outputFolder = "Assets/UIPrototyper/Generated";
        private string sheetFolder = "Assets/UIPrototyper/Sheets";

        private string structureJson = "";
        private string lastResult = "";
        private string refineInstruction = "";

        private string statusMsg = "Ready";
        private bool isBusy;
        private bool autoAdvance = true;

        // 타겟 해상도 — CanvasScaler reference + AI 프롬프트 + 루트 sizeDelta.
        private int refW = 1080;
        private int refH = 1920;

        private List<string> lastMissing = new();
        private GameObject lastBuiltRoot;
        private CancellationTokenSource _cts;

        private Vector2 _structScroll;
        private Vector2 _jsonScroll;

        [MenuItem("Tools/UI Prototyper")]
        public static void Open()
        {
            var w = GetWindow<UIPrototyperWindow>("UI Prototyper");
            w.minSize = new Vector2(460, 640);
        }

        private void OnEnable()
        {
            apiKey       = EditorPrefs.GetString(PREF_API_KEY, "");
            outputFolder = EditorPrefs.GetString(PREF_OUTPUT, outputFolder);
            autoAdvance  = EditorPrefs.GetBool(PREF_AUTO, true);
            refW         = EditorPrefs.GetInt(PREF_RES_W, 1080);
            refH         = EditorPrefs.GetInt(PREF_RES_H, 1920);
        }

        private void OnDisable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        // ================= GUI =================

        private void OnGUI()
        {
            EditorGUILayout.LabelField("UI Prototyper", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            apiKey = EditorGUILayout.PasswordField("Claude API Key", apiKey);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString(PREF_API_KEY, apiKey);

            registry = (UIResourceRegistry)EditorGUILayout.ObjectField(
                "Resource Registry", registry, typeof(UIResourceRegistry), false);

            EditorGUI.BeginChangeCheck();
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString(PREF_OUTPUT, outputFolder);

            EditorGUI.BeginChangeCheck();
            autoAdvance = EditorGUILayout.Toggle("Auto advance (Analyze → Compose)", autoAdvance);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetBool(PREF_AUTO, autoAdvance);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Target Resolution (required)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            refW = EditorGUILayout.IntField("Width",  refW);
            refH = EditorGUILayout.IntField("Height", refH);
            if (EditorGUI.EndChangeCheck())
            {
                refW = Mathf.Max(1, refW);
                refH = Mathf.Max(1, refH);
                EditorPrefs.SetInt(PREF_RES_W, refW);
                EditorPrefs.SetInt(PREF_RES_H, refH);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(" ", "Scale: ScaleWithScreenSize • Match: Expand • PPU: 100", EditorStyles.miniLabel);

            EditorGUILayout.Space();

            DrawSheetsSection();
            EditorGUILayout.Space();
            DrawReferenceSection();
            EditorGUILayout.Space();
            DrawStageButtons();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Status", statusMsg, EditorStyles.helpBox);
            EditorGUILayout.Space();
            DrawStructureSection();
            EditorGUILayout.Space();
            DrawResultSection();
            EditorGUILayout.Space();
            DrawRefineSection();
        }

        private void DrawSheetsSection()
        {
            EditorGUILayout.LabelField("Step 1. Resource Sheets", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(registry == null))
            {
                if (GUILayout.Button("Regenerate Resource Sheets"))
                {
                    var paths = ResourceSheetGenerator.GenerateAll(registry, sheetFolder);
                    statusMsg = $"Generated {paths.Count} sheets";
                }
            }
        }

        private void DrawReferenceSection()
        {
            EditorGUILayout.LabelField("Step 2. Reference Image", EditorStyles.boldLabel);
            referenceImage = (Texture2D)EditorGUILayout.ObjectField(
                "Image", referenceImage, typeof(Texture2D), false);
            DrawDropZone();
        }

        private void DrawStageButtons()
        {
            EditorGUILayout.LabelField("Step 3. Generate", EditorStyles.boldLabel);

            var ready = registry != null && referenceImage != null && !string.IsNullOrEmpty(apiKey)
                        && refW > 0 && refH > 0 && !isBusy;

            using (new EditorGUI.DisabledScope(!ready))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(autoAdvance ? "Run Full Pipeline" : "① Analyze Structure", GUILayout.Height(34)))
                {
                    if (autoAdvance) RunFullPipeline();
                    else RunAnalyze();
                }
                EditorGUILayout.EndHorizontal();

                if (!autoAdvance)
                {
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(structureJson)))
                    {
                        if (GUILayout.Button("② Compose Layout", GUILayout.Height(28)))
                            RunCompose();
                    }
                }
            }

            if (isBusy && GUILayout.Button("Cancel"))
            {
                _cts?.Cancel();
                statusMsg = "Canceling...";
            }
        }

        private void DrawStructureSection()
        {
            if (string.IsNullOrEmpty(structureJson)) return;
            EditorGUILayout.LabelField("Structure Spec (editable before Compose)", EditorStyles.boldLabel);
            _structScroll = EditorGUILayout.BeginScrollView(_structScroll, GUILayout.Height(120));
            structureJson = EditorGUILayout.TextArea(structureJson, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void DrawResultSection()
        {
            if (string.IsNullOrEmpty(lastResult)) return;
            EditorGUILayout.LabelField("Last Generated UI JSON", EditorStyles.boldLabel);
            _jsonScroll = EditorGUILayout.BeginScrollView(_jsonScroll, GUILayout.Height(140));
            lastResult = EditorGUILayout.TextArea(lastResult, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Build Prefab from JSON"))
                BuildFromJson(lastResult);
            using (new EditorGUI.DisabledScope(lastBuiltRoot == null))
            {
                if (GUILayout.Button("Save as .prefab"))
                    SaveBuiltAsPrefab();
            }
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(lastMissing.Count == 0))
            {
                if (GUILayout.Button($"Open Missing Report ({lastMissing.Count})"))
                    MissingResourceWindow.Show(lastMissing, registry);
            }
        }

        private void DrawRefineSection()
        {
            if (string.IsNullOrEmpty(lastResult)) return;

            EditorGUILayout.LabelField("Step 4. Refine (optional, iterative)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "자연어로 수정 지시를 입력하고 'Apply Refinement'를 누르면 이전 JSON이 패치됩니다.\n예: '뱃지를 우상단으로', '캐릭터 영역 더 크게', '하단 버튼 2개를 가운데 정렬'",
                MessageType.None);
            refineInstruction = EditorGUILayout.TextArea(refineInstruction, GUILayout.MinHeight(48));

            var ready = registry != null && referenceImage != null &&
                        !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(lastResult) &&
                        !string.IsNullOrEmpty(refineInstruction) && !isBusy;

            using (new EditorGUI.DisabledScope(!ready))
            {
                if (GUILayout.Button("Apply Refinement", GUILayout.Height(28)))
                    RunRefine();
            }
        }

        private void DrawDropZone()
        {
            var rect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "Drop image here", EditorStyles.helpBox);

            var evt = Event.current;
            if (!rect.Contains(evt.mousePosition)) return;

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
                    if (obj is Texture2D tex) { referenceImage = tex; break; }
                }
                evt.Use();
            }
        }

        // ================= PIPELINE =================

        private async void RunFullPipeline()
        {
            var ct = StartBusy();
            try
            {
                if (!EnsureSheets(ct, out var sheets)) return;
                if (this == null || ct.IsCancellationRequested) return;

                statusMsg = "① Analyzing structure...";
                Repaint();
                structureJson = await ClaudeAPIClient.AnalyzeStructure(apiKey, referenceImage, refW, refH, ct);
                if (this == null || ct.IsCancellationRequested) return;
                structureJson = PrettyPrint(structureJson);

                statusMsg = "② Composing layout...";
                Repaint();
                var ui = await ClaudeAPIClient.ComposeLayout(
                    apiKey, referenceImage, structureJson, sheets, registry, refW, refH, ct);
                if (this == null || ct.IsCancellationRequested) return;

                FinishWithUiJson(ui);
            }
            catch (System.OperationCanceledException) { statusMsg = "Canceled"; }
            catch (System.Exception e) { HandleError(e); }
            finally { EndBusy(); }
        }

        private async void RunAnalyze()
        {
            var ct = StartBusy();
            try
            {
                statusMsg = "① Analyzing structure...";
                Repaint();
                var s = await ClaudeAPIClient.AnalyzeStructure(apiKey, referenceImage, refW, refH, ct);
                if (this == null || ct.IsCancellationRequested) return;
                structureJson = PrettyPrint(s);
                statusMsg = "Structure ready — edit if needed, then Compose.";
            }
            catch (System.OperationCanceledException) { statusMsg = "Canceled"; }
            catch (System.Exception e) { HandleError(e); }
            finally { EndBusy(); }
        }

        private async void RunCompose()
        {
            var ct = StartBusy();
            try
            {
                if (!EnsureSheets(ct, out var sheets)) return;
                if (this == null || ct.IsCancellationRequested) return;

                statusMsg = "② Composing layout...";
                Repaint();
                var ui = await ClaudeAPIClient.ComposeLayout(
                    apiKey, referenceImage, structureJson, sheets, registry, refW, refH, ct);
                if (this == null || ct.IsCancellationRequested) return;

                FinishWithUiJson(ui);
            }
            catch (System.OperationCanceledException) { statusMsg = "Canceled"; }
            catch (System.Exception e) { HandleError(e); }
            finally { EndBusy(); }
        }

        private async void RunRefine()
        {
            var ct = StartBusy();
            try
            {
                if (!EnsureSheets(ct, out var sheets)) return;
                if (this == null || ct.IsCancellationRequested) return;

                statusMsg = "③ Refining...";
                Repaint();
                var patched = await ClaudeAPIClient.RefineLayout(
                    apiKey, referenceImage, lastResult, refineInstruction, sheets, registry, refW, refH, ct);
                if (this == null || ct.IsCancellationRequested) return;

                FinishWithUiJson(patched);
                refineInstruction = "";
            }
            catch (System.OperationCanceledException) { statusMsg = "Canceled"; }
            catch (System.Exception e) { HandleError(e); }
            finally { EndBusy(); }
        }

        private bool EnsureSheets(CancellationToken ct, out Dictionary<string, string> sheets)
        {
            statusMsg = "Preparing resource sheets...";
            Repaint();
            sheets = ResourceSheetGenerator.GenerateAll(registry, sheetFolder);
            return !ct.IsCancellationRequested && this != null;
        }

        private void FinishWithUiJson(string ui)
        {
            lastResult = PrettyPrint(ui);
            statusMsg = "Layout ready, building...";

            Directory.CreateDirectory(outputFolder);
            var safeName = SanitizeFileName(referenceImage != null ? referenceImage.name : "Generated");
            File.WriteAllText(Path.Combine(outputFolder, $"{safeName}_ui.json"), lastResult);
            AssetDatabase.Refresh();

            BuildFromJson(lastResult);
        }

        private CancellationToken StartBusy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            isBusy = true;
            Repaint();
            return _cts.Token;
        }

        private void EndBusy()
        {
            if (this == null) return;
            isBusy = false;
            Repaint();
        }

        private void HandleError(System.Exception e)
        {
            if (this != null) statusMsg = $"Error: {e.Message}";
            Debug.LogException(e);
        }

        // ================= BUILD =================

        private void BuildFromJson(string json)
        {
            var prevRoot = lastBuiltRoot;
            lastBuiltRoot = null;

            try
            {
                var generated = JsonConvert.DeserializeObject<GeneratedUI>(json);
                if (generated?.root == null)
                {
                    statusMsg = "Invalid JSON: no root node";
                    return;
                }

                var canvas = FindOrCreateCanvas();
                if (prevRoot != null) Object.DestroyImmediate(prevRoot);

                var builder = new UIBuilder(registry);
                var root = builder.Build(generated.root, canvas.transform);
                var safeName = SanitizeFileName(referenceImage != null ? referenceImage.name : "Generated");
                root.name = $"Generated_{safeName}";
                ForceCenterRoot(root);
                lastBuiltRoot = root;

                Selection.activeGameObject = root;
                EditorGUIUtility.PingObject(root);

                lastMissing = new List<string>(builder.MissingResources);
                if (lastMissing.Count > 0)
                {
                    statusMsg = $"Built, but {lastMissing.Count} resource(s) missing — see report";
                    Debug.LogWarning($"[UIPrototyper] Missing: {string.Join(", ", lastMissing)}");
                    MissingResourceWindow.Show(lastMissing, registry);
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

        private void SaveBuiltAsPrefab()
        {
            if (lastBuiltRoot == null) { statusMsg = "Nothing to save"; return; }

            try
            {
                Directory.CreateDirectory(outputFolder);
                AssetDatabase.Refresh();
                var safeName = SanitizeFileName(referenceImage != null ? referenceImage.name : "Generated");
                var prefabPath = Path.Combine(outputFolder, $"{safeName}.prefab").Replace('\\', '/');

                if (File.Exists(prefabPath))
                {
                    if (!EditorUtility.DisplayDialog(
                        "Overwrite Prefab?",
                        $"{prefabPath} already exists.\nOverwrite?",
                        "Overwrite", "Cancel"))
                    {
                        statusMsg = "Save canceled";
                        return;
                    }
                }

                var saved = PrefabUtility.SaveAsPrefabAssetAndConnect(
                    lastBuiltRoot, prefabPath, InteractionMode.UserAction);

                if (saved != null)
                {
                    EditorGUIUtility.PingObject(saved);
                    statusMsg = $"Saved: {prefabPath}";
                }
                else statusMsg = "Save failed";
            }
            catch (System.Exception e)
            {
                statusMsg = $"Save error: {e.Message}";
                Debug.LogException(e);
            }
        }

        private Canvas FindOrCreateCanvas()
        {
            Canvas canvas = null;
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c != null && c.name == PROTOTYPER_CANVAS_NAME) { canvas = c; break; }
            }

            if (canvas == null)
            {
                var go = new GameObject(PROTOTYPER_CANVAS_NAME,
                    typeof(Canvas),
                    typeof(UnityEngine.UI.CanvasScaler),
                    typeof(UnityEngine.UI.GraphicRaycaster));
                canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 500;
            }

            // 매번 현재 target resolution으로 CanvasScaler 강제 세팅.
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(refW, refH);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100;
            return canvas;
        }

        // 생성된 루트를 Canvas 중앙에 고정 + 타겟 해상도 크기로 강제.
        private void ForceCenterRoot(GameObject root)
        {
            if (root == null) return;
            var rt = root.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(refW, refH);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Generated";
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (System.Array.IndexOf(invalid, chars[i]) >= 0 || chars[i] == ' ')
                    chars[i] = '_';
            return new string(chars);
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

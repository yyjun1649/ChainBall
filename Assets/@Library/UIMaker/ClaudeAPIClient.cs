#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
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
        private const int MAX_TOKENS = 8192;

        // ================= PUBLIC API =================

        // 1단계 — Analyze: 이미지만 보고 구조 명세서(JSON)를 뽑는다.
        public static async Task<string> AnalyzeStructure(
            string apiKey,
            Texture2D referenceImage,
            int targetW,
            int targetH,
            CancellationToken ct = default)
        {
            var content = new List<object>
            {
                new { type = "text", text = "--- Reference UI ---" },
                ImageBlock(referenceImage.EncodeToPNG()),
                TextBlockWithCache(BuildAnalyzePrompt(referenceImage.width, referenceImage.height, targetW, targetH)),
            };
            var raw = await Call(apiKey, content, ct);
            return ExtractBalancedJsonObject(raw);
        }

        // 2단계 — Compose: Analyze 결과 + 레퍼런스 + 리소스로 uGUI JSON 생성.
        public static async Task<string> ComposeLayout(
            string apiKey,
            Texture2D referenceImage,
            string structureJson,
            Dictionary<string, string> sheetPaths,
            UIResourceRegistry registry,
            int targetW,
            int targetH,
            CancellationToken ct = default)
        {
            var content = new List<object>();

            foreach (var kvp in sheetPaths)
            {
                content.Add(new { type = "text", text = $"--- Resource Sheet: {kvp.Key} ---" });
                content.Add(ImageBlock(File.ReadAllBytes(kvp.Value)));
            }

            content.Add(new { type = "text", text = "--- Reference UI ---" });
            content.Add(ImageBlock(referenceImage.EncodeToPNG()));

            // 여기까지는 Refine 호출에서도 동일하도록 cache_control 부여.
            content.Add(TextBlockWithCache(BuildComposePreamble(registry, targetW, targetH)));

            content.Add(new { type = "text", text = "--- Structure Spec ---\n" + structureJson });
            content.Add(new { type = "text", text = BuildComposeInstruction(targetW, targetH) });

            var raw = await Call(apiKey, content, ct);
            return ExtractBalancedJsonObject(raw);
        }

        // 3단계 — Refine: 이전 JSON + 사용자 지시로 패치된 uGUI JSON 반환.
        public static async Task<string> RefineLayout(
            string apiKey,
            Texture2D referenceImage,
            string previousUIJson,
            string userInstruction,
            Dictionary<string, string> sheetPaths,
            UIResourceRegistry registry,
            int targetW,
            int targetH,
            CancellationToken ct = default)
        {
            var content = new List<object>();

            foreach (var kvp in sheetPaths)
            {
                content.Add(new { type = "text", text = $"--- Resource Sheet: {kvp.Key} ---" });
                content.Add(ImageBlock(File.ReadAllBytes(kvp.Value)));
            }

            content.Add(new { type = "text", text = "--- Reference UI ---" });
            content.Add(ImageBlock(referenceImage.EncodeToPNG()));

            // Compose와 동일한 preamble — prompt cache hit.
            content.Add(TextBlockWithCache(BuildComposePreamble(registry, targetW, targetH)));

            content.Add(new { type = "text", text = "--- Previous UI JSON ---\n" + previousUIJson });
            content.Add(new { type = "text", text = "--- User Refinement Instruction ---\n" + userInstruction });
            content.Add(new { type = "text", text = BuildRefineInstruction() });

            var raw = await Call(apiKey, content, ct);
            return ExtractBalancedJsonObject(raw);
        }

        // ================= TRANSPORT =================

        private static async Task<string> Call(string apiKey, List<object> content, CancellationToken ct)
        {
            var body = new
            {
                model = MODEL,
                max_tokens = MAX_TOKENS,
                messages = new[] { new { role = "user", content = content.ToArray() } },
            };

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(body);
            using var req = new UnityWebRequest(API_URL, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("x-api-key", apiKey);
            req.SetRequestHeader("anthropic-version", VERSION);
            // prompt caching 기능 활성화 베타 헤더 (현 시점 필요).
            req.SetRequestHeader("anthropic-beta", "prompt-caching-2024-07-31");

            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                if (ct.IsCancellationRequested)
                {
                    req.Abort();
                    throw new OperationCanceledException(ct);
                }
                await Task.Yield();
            }

            if (ct.IsCancellationRequested) throw new OperationCanceledException(ct);
            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"API Error: {req.error}\n{req.downloadHandler.text}");

            return ParseClaudeText(req.downloadHandler.text);
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
                    data = Convert.ToBase64String(pngBytes),
                },
            };
        }

        // cache_control 마크드 텍스트 블록 — 이 블록까지의 모든 내용이 캐시됨 (5분 TTL).
        private static object TextBlockWithCache(string text)
        {
            return new
            {
                type = "text",
                text = text,
                cache_control = new { type = "ephemeral" },
            };
        }

        // ================= PROMPTS =================

        private static string BuildAnalyzePrompt(int imageW, int imageH, int targetW, int targetH)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are analyzing a reference UI image for automatic uGUI generation.");
            sb.AppendLine($"The reference image dimensions are {imageW}x{imageH} pixels.");
            sb.AppendLine($"The TARGET output resolution is {targetW}x{targetH} pixels (CanvasScaler reference). Bboxes you output should be in TARGET resolution space — if the reference image is a different size, scale coordinates accordingly.");
            sb.AppendLine();
            sb.AppendLine("Produce a structural breakdown as JSON. Identify:");
            sb.AppendLine("- Major regions (header, body, footer, sidebar, etc.) with pixel bboxes.");
            sb.AppendLine("- Nested sub-regions inside each region.");
            sb.AppendLine("- Any visual overlaps (badges on icons, floating buttons over panels, etc.)");
            sb.AppendLine("- Any repeating groups (slot grids, item lists) with count.");
            sb.AppendLine();
            sb.AppendLine("## Output Schema");
            sb.AppendLine(@"{
  ""imageSize"": ""WxH"",
  ""regions"": [
    {
      ""name"": string,
      ""bbox"": { ""x"": number, ""y"": number, ""w"": number, ""h"": number },
      ""description"": string,
      ""contents"": [ /* recursive */ ]
    }
  ],
  ""overlaps"": [ string ],
  ""repeats"": [ string ],
  ""notes"": string
}");
            sb.AppendLine();
            sb.AppendLine("Return ONLY the JSON object. No prose, no code fences.");
            return sb.ToString();
        }

        // Compose + Refine에서 공유되는 프리앰블 (리소스 카탈로그 + 스키마 + 룰). cache 대상.
        private static string BuildComposePreamble(UIResourceRegistry registry, int targetW, int targetH)
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are a Unity UI designer. Generate uGUI JSON from the reference image.");
            sb.AppendLine($"Target canvas resolution is {targetW}x{targetH} pixels. ALL numeric w/h/x/y must be in this pixel space. The root Frame will be forced to exactly {targetW}x{targetH} centered on the canvas — do not output sizes larger than the target.");
            sb.AppendLine();
            sb.AppendLine("## Available Resources");
            sb.AppendLine("Use ONLY the keys listed below.");
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
            sb.AppendLine("### Prefab Refs (reusable composite prefabs)");
            if (registry.prefabs != null && registry.prefabs.Count > 0)
                foreach (var p in registry.prefabs)
                    sb.AppendLine($"- {p.key}: {p.description}");
            else
                sb.AppendLine("(none registered)");

            sb.AppendLine();
            sb.AppendLine("## Schema");
            sb.AppendLine(@"type UINode = {
  type: 'Frame'|'VStack'|'HStack'|'Grid'|'Overlay'|'Text'|'Image'|'Button'|'Spacer'|'PrefabRef',
  name?: string,
  text?: string,
  icon?: string,          // sprite key (Button)
  feel?: string,          // feel preset key (Button)
  prefabRef?: string,     // PrefabRef only
  size?: { mode: 'fixed'|'fill'|'hug', w: number, h: number },
  style?: { bg?: string, color?: string, font?: string, fontSize?: number, radius?: number },
  layout?: {
    anchor?: 'center'|'top-left'|'top-center'|'top-right'|'top-stretch'|'bottom-stretch'|'stretch',
    padding?: number,
    spacing?: number,
    align?: 'stretch'|'center'|'start'|'end',
    cellW?: number, cellH?: number      // Grid only
  },
  absolute?: {                            // Overlay 자식이나 절대 배치가 필요할 때
    x: number, y: number,                 // 부모의 anchor 기준 오프셋(픽셀)
    anchor?: 'top-left'|'top-right'|'bottom-left'|'bottom-right'|'center'
  },
  zOrder?: number,                        // sibling 순서 (클수록 앞)
  children?: UINode[]
}");
            sb.AppendLine();

            sb.AppendLine("## Rules");
            sb.AppendLine($"- Root MUST be a Frame sized exactly {targetW}x{targetH} with anchor 'center'. All child coordinates/sizes are relative to this root.");
            sb.AppendLine("- Use VStack/HStack/Grid for FLOW layouts (list, menu, row of buttons).");
            sb.AppendLine("- Use **Overlay** for areas where children OVERLAP or are at absolute positions (HUD, character panel with badges on icons, floating buttons).");
            sb.AppendLine("- Inside Overlay, give children an `absolute` field. x/y are POSITIVE pixel distances FROM the specified corner toward the inside of the parent (e.g. top-right anchor: x=20,y=30 means 20px left of right edge and 30px below top edge). For `anchor:'center'`, x/y are signed offsets from center.");
            sb.AppendLine("- Do NOT put overlapping children inside a Stack — Stacks are for one-direction flow only.");
            sb.AppendLine("- When children have visually specific sizes (e.g. 120x120 slot), set `size.mode=\"fixed\"` with matching w/h.");
            sb.AppendLine("- Use `size.mode=\"fill\"` only when a child should expand to fill remaining axis in a Stack.");
            sb.AppendLine("- Use `size.mode=\"hug\"` on Stacks that should shrink to fit their children.");
            sb.AppendLine("- Every Button should have a 'feel' preset.");
            sb.AppendLine("- Prefer registered PrefabRef over hand-composing identical shapes.");
            sb.AppendLine("- Stack layout.align defaults to 'center'. Use 'stretch' only when children should fill the cross-axis.");
            sb.AppendLine("- Use resource keys ONLY. No raw hex colors, no unlisted fonts.");
            return sb.ToString();
        }

        private static string BuildComposeInstruction(int targetW, int targetH)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("## Task");
            sb.AppendLine($"Using the structure spec above, produce the final uGUI JSON that visually matches the reference at {targetW}x{targetH} pixels.");
            sb.AppendLine("Map bboxes from the structure spec to Overlay + absolute for overlapping/positioned elements.");
            sb.AppendLine("Map list-like regions to VStack/HStack/Grid.");
            sb.AppendLine($"Double-check: no child's absolute position or fixed size exceeds {targetW}x{targetH}.");
            sb.AppendLine();
            sb.AppendLine("## Output");
            sb.AppendLine("Return ONLY a single JSON object of the shape `{ \"root\": UINode }`. No prose, no code fences.");
            return sb.ToString();
        }

        private static string BuildRefineInstruction()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("## Task");
            sb.AppendLine("Apply the user's refinement instruction to the Previous UI JSON.");
            sb.AppendLine("Preserve all unrelated parts. Only change what the instruction implies.");
            sb.AppendLine("If the instruction conflicts with the reference image, prefer the image.");
            sb.AppendLine();
            sb.AppendLine("## Output");
            sb.AppendLine("Return ONLY the FULL updated JSON `{ \"root\": UINode }`. No prose, no code fences.");
            return sb.ToString();
        }

        // ================= RESPONSE PARSING =================

        private static string ParseClaudeText(string apiResponse)
        {
            var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<ClaudeResponse>(apiResponse);
            if (parsed?.content == null || parsed.content.Length == 0)
                throw new Exception("Empty response from Claude");

            if (parsed.stop_reason == "max_tokens")
                Debug.LogWarning("[UIPrototyper] Response truncated (stop_reason=max_tokens).");

            // 첫 번째 text 블록만 취함 (tool_use 등은 무시).
            foreach (var b in parsed.content)
                if (b.type == "text" && !string.IsNullOrEmpty(b.text))
                    return b.text;

            throw new Exception("No text block in Claude response.");
        }

        private static string ExtractBalancedJsonObject(string text)
        {
            if (string.IsNullOrEmpty(text)) throw new Exception("Empty text");
            int start = text.IndexOf('{');
            if (start < 0) throw new Exception("No JSON object found.\nRaw:\n" + Preview(text));

            int depth = 0;
            bool inString = false;
            bool escape = false;
            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (escape) { escape = false; continue; }
                if (inString)
                {
                    if (c == '\\') { escape = true; continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') { inString = true; continue; }
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return text.Substring(start, i - start + 1);
                }
            }

            throw new Exception(
                "Unbalanced JSON (likely truncated). Raise max_tokens or simplify.\nRaw:\n" + Preview(text));
        }

        private static string Preview(string s)
        {
            const int max = 600;
            return s.Length <= max ? s : s.Substring(0, max) + "...<truncated>";
        }

        [Serializable]
        private class ClaudeResponse
        {
            public ContentBlock[] content;
            public string stop_reason;
        }

        [Serializable]
        private class ContentBlock
        {
            public string type;
            public string text;
        }
    }
}
#endif

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
                "Frame"     => BuildFrame(node, parent),
                "VStack"    => BuildStack(node, parent, vertical: true),
                "HStack"    => BuildStack(node, parent, vertical: false),
                "Grid"      => BuildGrid(node, parent),
                "Overlay"   => BuildOverlay(node, parent),
                "Text"      => BuildText(node, parent),
                "Image"     => BuildImage(node, parent),
                "Button"    => BuildButton(node, parent),
                "Spacer"    => BuildSpacer(node, parent),
                "PrefabRef" => BuildPrefabRef(node, parent),
                _           => BuildUnknown(node, parent)
            };

            // PrefabRef는 자체 계층을 가져오므로 children을 덧붙이지 않는다.
            if (go != null && node.children != null && node.type != "PrefabRef")
            {
                foreach (var child in node.children)
                    Build(child, go.transform);
            }

            // absolute가 명시되면 부모 LayoutGroup과 무관하게 좌표 강제 — 제일 마지막에 덮어씀.
            if (go != null && node.absolute != null)
                ApplyAbsolute(go, node.absolute);

            // zOrder가 지정되면 sibling index 조정 (부모의 children 순서 내에서).
            if (go != null && node.zOrder != 0)
                go.transform.SetSiblingIndex(Mathf.Clamp(node.zOrder, 0, parent.childCount));

            return go;
        }

        // ===== 타입별 빌더 =====

        private GameObject BuildFrame(UINode node, Transform parent)
        {
            var go = CreateRect(node.name ?? "Frame", parent);
            var img = go.AddComponent<Image>();
            ApplyBg(img, node.style);
            ApplyAnchor(go, node.layout?.anchor ?? "stretch");
            ApplySize(go, node);
            return go;
        }

        private GameObject BuildStack(UINode node, Transform parent, bool vertical)
        {
            var go = CreateRect(node.name ?? (vertical ? "VStack" : "HStack"), parent);
            ApplyAnchor(go, node.layout?.anchor ?? "top-left");
            ApplySize(go, node);

            if (!string.IsNullOrEmpty(node.style?.bg))
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

            // hug(자식 크기에 맞춤)일 때 ContentSizeFitter
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
            ApplyAnchor(go, node.layout?.anchor ?? "top-left");
            ApplySize(go, node);

            var g = go.AddComponent<GridLayoutGroup>();
            var pad = (int)(node.layout?.padding ?? 0);
            g.padding = new RectOffset(pad, pad, pad, pad);
            g.spacing = new Vector2(node.layout?.spacing ?? 0, node.layout?.spacing ?? 0);
            var cellW = node.layout?.cellW ?? 0;
            var cellH = node.layout?.cellH ?? 0;
            g.cellSize = new Vector2(cellW > 0 ? cellW : 128, cellH > 0 ? cellH : 128);
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
            ApplyAnchor(go, node.layout?.anchor ?? "center");
            ApplySize(go, node);
            return go;
        }

        private GameObject BuildImage(UINode node, Transform parent)
        {
            var go = CreateRect(node.name ?? "Image", parent);
            var img = go.AddComponent<Image>();
            ApplyBg(img, node.style);
            ApplyAnchor(go, node.layout?.anchor ?? "center");
            ApplySize(go, node);
            return go;
        }

        private GameObject BuildButton(UINode node, Transform parent)
        {
            var go = CreateRect(node.name ?? "Button", parent);

            var img = go.AddComponent<Image>();
            ApplyBg(img, node.style);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            ApplyAnchor(go, node.layout?.anchor ?? "center");
            ApplySize(go, node);

            // 아이콘 (있으면 라벨 왼쪽)
            if (!string.IsNullOrEmpty(node.icon))
            {
                var iconGo = CreateRect("Icon", go.transform);
                var iconImg = iconGo.AddComponent<Image>();
                iconImg.raycastTarget = false;
                var se = registry.GetSprite(node.icon);
                if (se != null && se.sprite != null)
                {
                    iconImg.sprite = se.sprite;
                    iconImg.preserveAspect = true;
                }
                else
                {
                    iconImg.color = Magenta();
                    MissingResources.Add(node.icon);
                }
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0, 0.5f);
                iconRt.anchorMax = new Vector2(0, 0.5f);
                iconRt.pivot = new Vector2(0, 0.5f);
                iconRt.sizeDelta = new Vector2(28, 28);
                iconRt.anchoredPosition = new Vector2(12, 0);
            }

            // 라벨 자식으로 추가 (클릭 가로채지 않도록 raycastTarget=false)
            if (!string.IsNullOrEmpty(node.text))
            {
                var label = BuildText(new UINode
                {
                    type = "Text",
                    name = "Label",
                    text = node.text,
                    style = new UIStyle
                    {
                        color = node.style?.color,
                        font = node.style?.font,
                        fontSize = node.style?.fontSize > 0 ? node.style.fontSize : 24
                    },
                    layout = new UILayout { anchor = "stretch" }
                }, go.transform);
                var labelTmp = label.GetComponent<TextMeshProUGUI>();
                if (labelTmp != null) labelTmp.raycastTarget = false;
            }

            // Feel 프리셋 부착
            if (!string.IsNullOrEmpty(node.feel))
                AttachFeel(go, btn, node.feel);

            return go;
        }

        private GameObject BuildPrefabRef(UINode node, Transform parent)
        {
            var key = node.prefabRef;
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("[UIBuilder] PrefabRef node without prefabRef key");
                return BuildUnknown(node, parent);
            }

            var entry = registry.GetPrefab(key);
            if (entry == null || entry.prefab == null)
            {
                MissingResources.Add(key);
                var fallback = CreateRect(node.name ?? $"PrefabRef_{key}", parent);
                var img = fallback.AddComponent<Image>();
                img.color = Magenta();
                ApplyAnchor(fallback, node.layout?.anchor ?? "center");
                ApplySize(fallback, node);
                return fallback;
            }

#if UNITY_EDITOR
            var instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(entry.prefab, parent);
#else
            var instance = Object.Instantiate(entry.prefab, parent);
#endif
            instance.name = node.name ?? entry.prefab.name;

            // RectTransform이 있으면 명시된 필드만 덮어쓴다 (prefab 원본 anchor/size 최대한 보존).
            if (instance.GetComponent<RectTransform>() != null)
            {
                if (!string.IsNullOrEmpty(node.layout?.anchor))
                    ApplyAnchor(instance, node.layout.anchor);
                if (node.size != null && node.size.mode == "fixed" && (node.size.w > 0 || node.size.h > 0))
                    ApplySize(instance, node);
            }

            return instance;
        }

        // Overlay: LayoutGroup 없는 빈 Frame. 자식은 각자 anchor/absolute로 자유 배치 (겹침 허용).
        private GameObject BuildOverlay(UINode node, Transform parent)
        {
            var go = CreateRect(node.name ?? "Overlay", parent);
            if (!string.IsNullOrEmpty(node.style?.bg))
            {
                var img = go.AddComponent<Image>();
                ApplyBg(img, node.style);
            }
            ApplyAnchor(go, node.layout?.anchor ?? "stretch");
            ApplySize(go, node);
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
            img.color = new Color(1, 0, 1, 0.5f); // 핑크 플레이스홀더
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
            if (style == null || string.IsNullOrEmpty(style.bg))
            {
                img.color = new Color(0, 0, 0, 0); // 투명
                return;
            }

            // "color." prefix 우선, 없으면 Color/Sprite 순서로 레지스트리 조회.
            var colorFirst = style.bg.StartsWith("color.");

            if (colorFirst)
            {
                var ce = registry.GetColor(style.bg);
                if (ce != null) { img.color = Opaque(ce.color); return; }
                img.color = Magenta();
                MissingResources.Add(style.bg);
                return;
            }

            var se = registry.GetSprite(style.bg);
            if (se != null && se.sprite != null)
            {
                img.sprite = se.sprite;
                if (se.nineSliceBorder != Vector4.zero) img.type = Image.Type.Sliced;
                img.color = Color.white;
                return;
            }

            // sprite로 못 찾으면 color 레지스트리도 폴백 시도 (prefix 규약을 어긴 키 대비).
            var fallbackColor = registry.GetColor(style.bg);
            if (fallbackColor != null) { img.color = Opaque(fallbackColor.color); return; }

            img.color = Magenta();
            MissingResources.Add(style.bg);
        }

        private void ApplyTextColor(TextMeshProUGUI tmp, UIStyle style)
        {
            if (style?.color == null) { tmp.color = Color.white; return; }
            var ce = registry.GetColor(style.color);
            if (ce != null) tmp.color = Opaque(ce.color);
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
                case "fill":
                    // LayoutGroup 하위에서는 LayoutElement.flexible*로, 그 외에는 anchor stretch에 의존.
                    if (go.transform.parent != null &&
                        go.transform.parent.GetComponent<LayoutGroup>() != null)
                    {
                        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
                        le.flexibleWidth = 1;
                        le.flexibleHeight = 1;
                    }
                    break;
                case "hug":
                    // ContentSizeFitter에서 처리
                    break;
                default: // fixed
                    // stretch anchor 상태에서는 sizeDelta가 "부모 대비 오프셋"으로 해석되어 절대 크기가 되지 않는다.
                    // fixed 모드가 요청되었는데 anchor가 stretch면 anchor를 중심점으로 축소해 예측 가능한 고정 크기로 만든다.
                    if (rt.anchorMin != rt.anchorMax)
                    {
                        var center = (rt.anchorMin + rt.anchorMax) * 0.5f;
                        rt.anchorMin = rt.anchorMax = center;
                    }
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

            var align = node.layout?.align;
            g.childAlignment = align switch
            {
                "start"   => TextAnchor.UpperLeft,
                "end"     => TextAnchor.LowerRight,
                "stretch" => TextAnchor.MiddleCenter,
                _         => TextAnchor.MiddleCenter, // "center"
            };

            // stretch 일 때만 LayoutGroup이 자식 크기 제어. 그 외엔 자식 sizeDelta/preferredSize 존중.
            var stretch = align == "stretch";
            g.childControlWidth = stretch;
            g.childControlHeight = stretch;
            g.childForceExpandWidth = stretch;
            g.childForceExpandHeight = stretch;
        }

        // absolute 배치: 부모 anchor에 child anchor/pivot을 맞추고 anchoredPosition으로 오프셋.
        private void ApplyAbsolute(GameObject go, UIAbsolute abs)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) return;

            // LayoutGroup 하위라도 레이아웃 계산에서 제외.
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            Vector2 a;
            switch (abs.anchor)
            {
                case "top-right":    a = new Vector2(1, 1); break;
                case "bottom-left":  a = new Vector2(0, 0); break;
                case "bottom-right": a = new Vector2(1, 0); break;
                case "center":       a = new Vector2(0.5f, 0.5f); break;
                case "top-left":
                default:             a = new Vector2(0, 1); break;
            }

            rt.anchorMin = a;
            rt.anchorMax = a;
            rt.pivot = a;

            // 프롬프트 규약: x/y는 "해당 코너에서 안쪽으로의 거리(픽셀, 양수)"로 해석.
            // Unity UI anchoredPosition은 앵커 원점 기준으로 +x=오른쪽, +y=위. 코너별 부호 보정.
            float x = Mathf.Abs(abs.x);
            float y = Mathf.Abs(abs.y);
            switch (abs.anchor)
            {
                case "top-right":    rt.anchoredPosition = new Vector2(-x, -y); break;
                case "top-left":     rt.anchoredPosition = new Vector2( x, -y); break;
                case "bottom-right": rt.anchoredPosition = new Vector2(-x,  y); break;
                case "bottom-left":  rt.anchoredPosition = new Vector2( x,  y); break;
                case "center":
                default:             rt.anchoredPosition = new Vector2(abs.x, abs.y); break;
            }
        }

        private void AttachFeel(GameObject target, Button btn, string feelKey)
        {
            var preset = registry.GetFeelPreset(feelKey);
            if (preset == null || preset.feelPrefab == null)
            {
                MissingResources.Add(feelKey);
                return;
            }

            // Feel Prefab을 자식으로 인스턴스화. LayoutGroup 하위 Button이면 레이아웃 계산 방해하지 않도록 ignoreLayout 부여.
            var feelInstance = Object.Instantiate(preset.feelPrefab, target.transform);
            feelInstance.name = "_Feel";
            var feelRt = feelInstance.GetComponent<RectTransform>();
            if (feelRt != null)
            {
                var le = feelInstance.GetComponent<LayoutElement>() ?? feelInstance.AddComponent<LayoutElement>();
                le.ignoreLayout = true;
            }

            // MMF_Player는 리플렉션으로 찾아서 연결 (Feel 의존성 없이 유지)
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
        // 레지스트리 Color 엔트리 적용 시 alpha=1 강제 (의도치 않은 반투명 방지).
        private static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);
    }
}

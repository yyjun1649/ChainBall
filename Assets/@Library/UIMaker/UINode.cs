using System;
using System.Collections.Generic;

namespace UIPrototyper
{
    [Serializable]
    public class UINode
    {
        // Frame, VStack, HStack, Grid, Overlay, Text, Image, Button, Spacer, PrefabRef
        public string type;
        public string name;
        public string text;           // Text, Button
        public string icon;           // Button sprite key
        public string feel;           // feel preset key
        public string prefabRef;      // PrefabRef key

        public UISize size;
        public UIStyle style;
        public UILayout layout;

        // 절대좌표 배치 (Overlay 자식이나 어디서든 명시 시). 설정되면 부모 LayoutGroup 무시.
        public UIAbsolute absolute;

        // 같은 부모 내 z-order. 큰 값일수록 앞(뒤에 그려짐 = UI는 위에 표시).
        public int zOrder;

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
        public string anchor = "center"; // center, top-left, top-stretch, bottom-stretch, stretch 등
        public float padding;
        public float spacing;
        public string align = "center";  // stretch, start, center, end

        // Grid 전용 — 0 이하면 기본값 128
        public float cellW;
        public float cellH;
    }

    // 절대 위치 — Overlay 또는 부모 LayoutGroup 우회 시.
    // anchor는 부모의 어느 모서리를 기준으로 x/y 오프셋을 재는지.
    [Serializable]
    public class UIAbsolute
    {
        public float x;
        public float y;
        public string anchor = "top-left"; // top-left, top-right, bottom-left, bottom-right, center
    }

    // === API 응답 파싱용 ===

    [Serializable]
    public class GeneratedUI
    {
        public UINode root;
        public List<string> missingResources;
    }

    // Analyze Pass 결과. 구조 파악용 중간 산출물.
    [Serializable]
    public class UIStructure
    {
        public string imageSize;   // "1080x1920" 등
        public List<UIRegion> regions;
        public List<string> overlaps;  // "Badge overlaps character portrait top-right" 등
        public List<string> repeats;   // "6 equipment slots in 3x2 grid" 등
        public string notes;
    }

    [Serializable]
    public class UIRegion
    {
        public string name;
        public UIBBox bbox;
        public string description;
        public List<UIRegion> contents;
    }

    [Serializable]
    public class UIBBox
    {
        public float x, y, w, h;
    }
}

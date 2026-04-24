// TutorialDimShader.shader
Shader "Tutorial/DimWithHole"
{
    Properties
    {
        _DimColor ("Dim Color", Color) = (0, 0, 0, 0.7)
        _HoleCenter ("Hole Center", Vector) = (0.5, 0.5, 0, 0)
        _HoleSize ("Hole Size", Vector) = (0.2, 0.2, 0, 0)
        _CornerRadius ("Corner Radius", Float) = 0
        _ScreenResolution ("Screen Resolution", Vector) = (1920, 1080, 0, 0)
    }
    
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            float4 _DimColor;
            float4 _HoleCenter;
            float4 _HoleSize;
            float _CornerRadius;
            float4 _ScreenResolution;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 둥근 사각형 거리 함수 (픽셀 단위, 크기 유지)
            float RoundedBoxSDF(float2 centerPos, float2 halfSize, float radius)
            {
                // radius를 halfSize에서 빼서 전체 크기 유지
                float2 q = abs(centerPos) - (halfSize - radius);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // UV 좌표를 픽셀 좌표로 변환
                float2 pixelPos = i.uv * _ScreenResolution.xy;
                float2 holeCenter = _HoleCenter.xy * _ScreenResolution.xy;
                float2 holeSize = _HoleSize.xy * _ScreenResolution.xy;

                // 중심 기준 상대 좌표 (픽셀 단위)
                float2 centered = pixelPos - holeCenter;

                // SDF 계산 (픽셀 단위로, 둥근 모서리 지원)
                float dist = RoundedBoxSDF(centered, holeSize * 0.5, _CornerRadius);

                // 부드러운 경계 (안티앨리어싱, 픽셀 단위)
                float smoothEdge = smoothstep(0.0, 2.0, dist);
                
                // 구멍 영역은 투명, 나머지는 딤
                float alpha = _DimColor.a * smoothEdge;
                
                return float4(_DimColor.rgb, alpha);
            }
            ENDCG
        }
    }
    
    Fallback "Unlit/Transparent"
}
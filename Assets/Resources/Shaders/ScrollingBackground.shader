Shader "Custom/ProceduralDotPatternAspectFix"
{
    Properties
    {
        _Color ("Background Color", Color) = (0.5, 0.5, 0.5, 1)
        _DotColor ("Dot Color", Color) = (0.2, 0.2, 0.2, 1)
        _DotSize ("Dot Size", Range(0.01, 0.1)) = 0.05
        _Spacing ("Dot Spacing", Range(0.1, 1.0)) = 0.2
        _SpeedX ("Speed X", Float) = 0.1
        _SpeedY ("Speed Y", Float) = 0.1
        _SpeedScale ("Speed Scale", Range(0.01, 1.0)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float4 _Color;
            float4 _DotColor;
            float _DotSize;
            float _Spacing;
            float _SpeedX;
            float _SpeedY;
            float _SpeedScale;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 获取屏幕宽高比
                float aspectRatio = _ScreenParams.x / _ScreenParams.y;

                // 调整UV坐标以适配屏幕比例
                float2 uv = i.uv;
                uv.x *= aspectRatio;

                // 动态偏移量
                float time = _Time.y;
                float2 offset = float2(_SpeedX, _SpeedY) * time * _SpeedScale;
                uv += offset;

                // 计算点阵网格
                float2 grid = frac(uv / _Spacing);
                float2 center = float2(0.5, 0.5);
                float dist = distance(grid, center);

                // 返回点或背景颜色
                if (dist < _DotSize)
                {
                    return _DotColor;
                }
                else
                {
                    return _Color;
                }
            }
            ENDCG
        }
    }
}

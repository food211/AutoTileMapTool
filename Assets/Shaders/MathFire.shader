Shader "Custom/Fire" {
    Properties {
        _MainTex("Texture", 2D) = "white"{}

        // 基础设置
        [Header(Base)]
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendSrc("BlendSrc", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendDst("BlendDst", Float) = 10
        [KeywordEnum(Base, Cartoon, Pixel)] _Style("Style", Int) = 0
        [HDR] _Color1("Color1", Color) = (0.77, 0.08, 0.11, 1)
        [HDR] _Color2("Color2", Color) = (1.2, 1.2, 0.6, 1)
        _Speed("Speed", Range(0, 1)) = 0.2
        _Scale("Scale", Range(0, 5)) = 1.5
        

        // 高级设置
        [Header(Advance)]
        _RandomRange("Random Range", Range(0, 1)) = 0.3
        _RgbLerpOffset("RgbLerpOffset", Range(-1, 1)) = -0.3
        _RgbPow("RgbPow", Range(0, 2)) = 2
        _AlphaLerpOffset("AlphaLerpOffset", Range(-1, 1)) = 0.03
        _AlphaPow("AlphaPow", Range(0, 2)) = 1
        _Shape("Shape", Range(1, 30)) = 6
        _ChipPow("ChipPow", Range(0, 5)) = 1.7
        _ChipVal("ChipVal", Range(-2, 2)) = 0.1
        _ChipParam("ChipParam", Vector) = (20, -13, 46.5, 2)

        // Shader内置的_Time存在精度问题, 时间越久精度越低, 所以可以根据需要使用C#脚本传入一个低精度的时间_CpuTime, 数值范围0到1 / _Speed即可, 是循环的动画
        [Header(Time)]
        [Toggle(USE_CPU_TIME)] _UseCpuTime("UseCpuTime", Int) = 0
        _CpuTime("CpuTime", Float) = 0

        // 卡通风格, 颜色分离 + 硬边缘描边
        [Header(Cartoon Style)]
        [HDR] _CartoonLineColor("CartoonLineColor", Color) = (0, 0, 0, 0.4)
        _CartoonLineWidth("CartoonLineWidth", Range(0, 0.5)) = 0.3
        _CartoonColorLayer("CartoonColorLayer", Range(0, 5)) = 2
        _CartoonBlur("CartoonBlur", Range(0, 1)) = 0.6
        _CartoonAlphaPow("CartoonAlphaPow", Range(0.1, 1)) = 0.3

        // 像素风格, 颜色分离 + 像素网格化 + 低帧率
        [Header(Pixel Style)]
        _PixelSize("PixelSize", Int) = 64
        _PixelFps("PixelFps", Int) = 24
        _PixelColorLayer("PixelColorLayer", Range(0, 5)) = 2
        _PixelOffsetX("PixelOffsetX", Range(-1, 1)) = 0
        _PixelOffsetY("PixelOffsetY", Range(-1, 1)) = 0
    }
    SubShader {
        Tags {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend [_BlendSrc] [_BlendDst]

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature USE_CPU_TIME _STYLE_BASE _STYLE_CARTOON _STYLE_PIXEL
            #define PI 3.1415926535897932384626433832795
            #define TAU (2 * PI)
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 worldPos : TEXCOORD1;
                float4 color : COLOR;
            };

            // MainTex
            sampler2D _MainTex;
            float4 _MainTex_ST;
            // Base
            fixed4 _Color1;
            fixed4 _Color2;
            fixed _Speed;
            fixed _Scale;
            // Advance
            fixed _RandomRange;
            fixed _RgbLerpOffset;
            fixed _RgbPow;
            fixed _AlphaLerpOffset;
            fixed _AlphaPow;
            fixed _Shape;
            fixed _ChipPow;
            fixed _ChipVal;
            float4 _ChipParam;
            // Time
            float _CpuTime;
            // Cartoon Style
            fixed4 _CartoonLineColor;
            fixed _CartoonLineWidth;
            fixed _CartoonColorLayer;
            fixed _CartoonBlur;
            fixed _CartoonAlphaPow;
            // Pixel Style
            float _PixelSize;
            float _PixelFps;
            fixed _PixelColorLayer;
            fixed _PixelOffsetX;
            fixed _PixelOffsetY;

            // 简单的哈希函数，用于生成伪随机数
            float hash(float2 p) {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            v2f vert(appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.color = v.color;
                return o;
            }

            fixed WaterDrop(fixed2 uv) {
                fixed x = 2 * uv.x - 1;
                fixed y = 2 - 2 * uv.y;
                return (x * x + y * y) * (x * x + y * y) - 2 * y * (x * x + y * y) + _Shape * x * x;
            }

            fixed Fire(fixed2 uv, fixed t) {
                fixed x = uv.x;
                fixed y = uv.y;
                fixed o = pow(y, _ChipPow) * _ChipVal * (sin(_ChipParam.x * y + _ChipParam.y * t) + sin(_ChipParam.z * x + _ChipParam.w * t));
                fixed a = max(0, 1 - WaterDrop(uv));
                return -WaterDrop(uv + fixed2(a * o, 0));
            }

            fixed4 frag(v2f i) : SV_Target {
                // 基于世界坐标生成随机种子
                float2 seed = floor(i.worldPos.xz * 10);
 
                // 随机时间偏移
                float timeOffset = hash(seed + 0.5) * 10;
                
                fixed t = 0;
            #if defined(USE_CPU_TIME)
                t = _CpuTime + timeOffset;
            #else
                t = _Time.y + timeOffset;
            #endif

                fixed2 uv = 0;
            #if defined(_STYLE_PIXEL)
                fixed2 pixelOffset = fixed2(_PixelOffsetX, _PixelOffsetY);
                uv = ((floor(i.uv * _PixelSize + pixelOffset) - pixelOffset) / _PixelSize - 0.5) * _Scale + 0.5;
                t = floor(t * _PixelFps) / _PixelFps;
            #else
                uv = (i.uv - 0.5) * _Scale + 0.5;
            #endif

                t *= TAU * _Speed;
                fixed fire = Fire(uv, t);
                fixed rgbLerp = max(0, pow(fire + _RgbLerpOffset, _RgbPow) * sign(fire));
                fixed alphaLerp = saturate(pow(max(0, fire + _AlphaLerpOffset), _AlphaPow));

                fixed4 result = 0;
            #if defined(_STYLE_BASE)
                result = fixed4(lerp(_Color1, _Color2, rgbLerp).rgb, alphaLerp);
            #elif defined(_STYLE_CARTOON)
                rgbLerp = lerp(rgbLerp, floor(rgbLerp * _CartoonColorLayer) / _CartoonColorLayer, _CartoonBlur);
                fixed4 bg = fixed4(lerp(_Color1, _Color2, rgbLerp).rgb, alphaLerp > 0);
                fixed4 outLine = (alphaLerp > 0) * (alphaLerp < _CartoonLineWidth) * _CartoonLineColor;
                result = fixed4(lerp(bg.rgb, outLine.rgb, outLine.a), max(bg.a, outLine.a) * pow(saturate(1 - uv.y), _CartoonAlphaPow));
            #elif defined(_STYLE_PIXEL)
                rgbLerp = floor(rgbLerp * _PixelColorLayer) / _PixelColorLayer;
                result = fixed4(lerp(_Color1, _Color2, rgbLerp).rgb, alphaLerp > 0);
            #else
                result = 0;
            #endif
                return result * i.color;
            }
            ENDCG
        }
    }
}
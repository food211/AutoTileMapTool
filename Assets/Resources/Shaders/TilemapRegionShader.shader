Shader "Custom/TilemapRegionShader"
{
    Properties
    {
        _MainTex ("Tilemap Texture", 2D) = "white" {}
        _DistanceField ("Distance Field", 2D) = "white" {}
        _EdgeColor ("Edge Color", Color) = (1,1,1,1)
        _EdgeThickness ("Edge Thickness", Range(0.01, 0.5)) = 0.1
        _EdgeGlow ("Edge Glow", Range(0, 2)) = 1.0
        _ShadowColor ("Shadow Color", Color) = (0,0,0,0.5)
        _ShadowIntensity ("Shadow Intensity", Range(0, 1)) = 0.5
        _ShadowSoftness ("Shadow Softness", Range(0.01, 1)) = 0.2
        _TilemapBounds ("Tilemap Bounds (x, y, width, height)", Vector) = (0, 0, 1, 1)
    }
    
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 worldUV : TEXCOORD1;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _DistanceField;
            float4 _DistanceField_ST;
            float4 _EdgeColor;
            float _EdgeThickness;
            float _EdgeGlow;
            float4 _ShadowColor;
            float _ShadowIntensity;
            float _ShadowSoftness;
            float4 _TilemapBounds;
            
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                // 计算世界空间UV坐标，用于访问距离场
                o.worldUV = TRANSFORM_TEX(v.uv, _DistanceField);
                
                o.color = v.color;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // 采样原始纹理
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                
                // 采样距离场
                float distance = tex2D(_DistanceField, i.worldUV).r;
                
                // 如果没有距离场数据（区域外），直接返回原始颜色
                if (distance <= 0.001)
                    return col;
                
                // 计算边缘效果
                float edgeFactor = 1.0 - smoothstep(0, _EdgeThickness, distance);
                float3 edgeEffect = _EdgeColor.rgb * edgeFactor * _EdgeGlow;
                
                // 计算内部阴影
                float shadowFactor = smoothstep(_ShadowSoftness, 1.0, distance) * _ShadowIntensity;
                float3 shadowEffect = lerp(col.rgb, _ShadowColor.rgb, shadowFactor * _ShadowColor.a);
                
                // 合并效果：边缘 + 内部阴影
                float3 finalColor = lerp(shadowEffect, edgeEffect, edgeFactor);
                
                return fixed4(finalColor, col.a);
            }
            ENDCG
        }
    }
}

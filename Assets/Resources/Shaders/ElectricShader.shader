Shader "Custom/URP_2D_Electric_Effect"
{
  Properties
  {
      [MainTexture] _MainTex ("Main Texture", 2D) = "white" {}
      [HDR] _ElectricColor ("Electric Color", Color) = (0.5, 1, 2, 1)
      _ElectricIntensity ("Electric Intensity", Range(0, 5)) = 2
      _FlowSpeed ("Flow Speed", Range(0, 10)) = 3
      _NoiseScale ("Noise Scale", Range(0.1, 5)) = 1
      _DistortionStrength ("Distortion Strength", Range(0, 1)) = 0.3
      _FlickerSpeed ("Flicker Speed", Range(0, 20)) = 8
      _ThresholdMin ("Threshold Min", Range(0, 1)) = 0.3
      _ThresholdMax ("Threshold Max", Range(0, 1)) = 0.7
      _Alpha ("Alpha", Range(0, 1)) = 1
  }
  
  SubShader
  {
      Tags 
      { 
          "RenderType"="Transparent" 
          "Queue"="Transparent"
          "RenderPipeline"="UniversalPipeline"
      }
      
      Blend SrcAlpha OneMinusSrcAlpha
      ZWrite Off
      Cull Off
      
      Pass
      {
          Tags { "LightMode"="Universal2D" }
          
          HLSLPROGRAM
          #pragma vertex vert
          #pragma fragment frag
          #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
          
          struct Attributes
          {
              float4 positionOS : POSITION;
              float2 uv : TEXCOORD0;
              float4 color : COLOR;
          };
          
          struct Varyings
          {
              float4 positionHCS : SV_POSITION;
              float2 uv : TEXCOORD0;
              float4 color : COLOR;
          };
          
          TEXTURE2D(_MainTex);
          SAMPLER(sampler_MainTex);
          
          CBUFFER_START(UnityPerMaterial)
              float4 _MainTex_ST;
              float4 _ElectricColor;
              float _ElectricIntensity;
              float _FlowSpeed;
              float _NoiseScale;
              float _DistortionStrength;
              float _FlickerSpeed;
              float _ThresholdMin;
              float _ThresholdMax;
              float _Alpha;
          CBUFFER_END
          
          // 简单噪声函数
          float2 hash22(float2 p)
          {
              p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
              return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
          }
          
          float noise(float2 p)
          {
              float2 i = floor(p);
              float2 f = frac(p);
              float2 u = f * f * (3.0 - 2.0 * f);
              
              return lerp(lerp(dot(hash22(i + float2(0.0, 0.0)), f - float2(0.0, 0.0)),
                             dot(hash22(i + float2(1.0, 0.0)), f - float2(1.0, 0.0)), u.x),
                         lerp(dot(hash22(i + float2(0.0, 1.0)), f - float2(0.0, 1.0)),
                             dot(hash22(i + float2(1.0, 1.0)), f - float2(1.0, 1.0)), u.x), u.y);
          }
          
          Varyings vert(Attributes input)
          {
              Varyings output;
              output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
              output.uv = TRANSFORM_TEX(input.uv, _MainTex);
              output.color = input.color;
              return output;
          }
          
          float4 frag(Varyings input) : SV_Target
          {
              float2 uv = input.uv;
              float time = _Time.y;
              
              // 基础纹理采样
              float4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
              
              // 电流流动效果
              float flow = frac(uv.y * 3.0 + time * _FlowSpeed);
              
              // 噪声扭曲
              float2 noiseUV = uv * _NoiseScale + time * 0.1;
              float noiseValue = noise(noiseUV) * _DistortionStrength;
              
              // 电流强度计算
              float electricMask = smoothstep(_ThresholdMin, _ThresholdMax, 
                  mainTex.r + noiseValue + sin(time * _FlickerSpeed) * 0.1);
              
              // 电流闪烁效果
              float flicker = sin(time * _FlickerSpeed) * 0.5 + 0.5;
              flicker = pow(flicker, 3.0);
              
              // 电流边缘发光
              float edge = 1.0 - smoothstep(0.0, 0.3, abs(uv.x - 0.5) * 2.0);
              edge *= 1.0 - smoothstep(0.0, 0.1, abs(uv.y - flow));
              
              // 最终颜色计算
              float3 electricEffect = _ElectricColor.rgb * electricMask * _ElectricIntensity;
              electricEffect += _ElectricColor.rgb * edge * flicker * 2.0;
              
              // 透明度计算
              float alpha = electricMask * _Alpha * input.color.a;
              alpha += edge * flicker * 0.5;
              
              return float4(electricEffect, saturate(alpha));
          }
          ENDHLSL
      }
  }
}
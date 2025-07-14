Shader "Custom/BushSwayInteractive"
{
Properties
{
  _MainTex ("Texture", 2D) = "white" {}

  _Color ("Color", Color) = (1, 1, 1, 1)
  // === 时间偏移参数 ===
  _TimeOffset ("Time Offset", Range(0, 10)) = 0
  
  // === 风力参数 ===
  _WindStrength ("Wind Strength", Range(0, 0.1)) = 0.02
  _WindFrequency ("Wind Frequency", Range(0.5, 5.0)) = 2.0
  _WindDirection ("Wind Direction", Vector) = (0, 1, 0, 0) // 修改默认值为垂直方向
  
  // === 弹性参数 ===
  _ElasticAmount ("Elastic Amount", Range(0, 0.15)) = 0.06
  _ElasticFrequency ("Elastic Frequency", Range(2, 8)) = 4.0
  
  // === 分层参数 ===
  _CenterDeadZone ("Center Dead Zone", Range(0, 0.5)) = 0.2
  _EdgeInfluence ("Edge Influence", Range(0.3, 0.8)) = 0.5
  
  // === 自定义Pivot参数 ===
  _PivotPosition ("Pivot Position", Vector) = (0, 0, 0, 0)
  _UseCustomPivot ("Use Custom Pivot", Float) = 0

  // === 随机化参数 ===
  _RandomSeed ("Random Seed", Range(0, 100)) = 0
  _PhaseVariation ("Phase Variation", Range(0, 6.28)) = 1.0
  
  // === 玩家交互参数 ===
  _PlayerPosition ("Player Position", Vector) = (0, 0, 0, 0)
  _ImpactStrength ("Impact Strength", Range(0, 2)) = 0
  _ImpactRadius ("Impact Radius", Range(0.5, 8)) = 3.0
  _ImpactStartTime ("Impact Start Time", Float) = 0
  
  // === 平滑插值参数 ===
  _ResponseIntensity ("Response Intensity", Range(0, 1)) = 0.3
  _LerpSpeed ("Lerp Speed", Range(1, 20)) = 8.0
  _DecaySpeed ("Decay Speed", Range(0.5, 8)) = 2.0
  _SmoothingFactor ("Smoothing Factor", Range(0.05, 0.5)) = 0.15
  
  // === 响应曲线参数 ===
  _ResponseCurveStrength ("Response Curve Strength", Range(0.5, 3)) = 1.5
  _WaveFrequency ("Wave Frequency", Range(2, 12)) = 6.0
  _WaveDecay ("Wave Decay", Range(1, 6)) = 3.0
  
  // === 整体移动参数 ===
  _WholeObjectMovement ("Whole Object Movement", Range(0, 1)) = 0.8
  _WholeObjectRotation ("Whole Object Rotation", Range(0, 0.2)) = 0.05
  
  // === 顶点变形参数 ===
  _VertexDeformStrength ("Vertex Deform Strength", Range(0.1, 2.0)) = 0.8
  _VertexDeformSpread ("Vertex Deform Spread", Range(0.1, 1.0)) = 0.5
  
  // === 像素游戏优化参数 ===
  _PixelSize ("Pixel Size", Range(0.01, 0.5)) = 0.1
  _PixelSnap ("Pixel Snap", Range(0, 1)) = 0.5
  _ElasticEasing ("Elastic Easing", Range(0.1, 1.0)) = 0.5
  _PixelatedMovement ("Pixelated Movement", Range(0, 1)) = 0.3
}

SubShader
{
  Tags 
  { 
      "RenderType"="TransparentCutout" 
      "Queue"="AlphaTest"
      "IgnoreProjector"="True"
  }
  
  Pass
  {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_instancing // 启用GPU Instancing
      #include "UnityCG.cginc"
      
      struct appdata
      {
          float4 vertex : POSITION;
          float2 uv : TEXCOORD0;
          float4 color : COLOR;
          UNITY_VERTEX_INPUT_INSTANCE_ID // 添加实例ID输入
      };
      
      struct v2f
      {
          float2 uv : TEXCOORD0;
          float4 vertex : SV_POSITION;
          float4 color : COLOR;
          UNITY_VERTEX_INPUT_INSTANCE_ID // 添加实例ID
          UNITY_VERTEX_OUTPUT_STEREO // 添加立体渲染支持
      };
      
      sampler2D _MainTex;
      float4 _MainTex_ST;
      
      // 全局共享参数（不需要每个实例单独设置）
      float _PhaseVariation;
      float4 _PlayerPosition;
      float _ImpactRadius;
      float _ImpactStartTime;
      float _DecaySpeed;
      float _WaveFrequency;
      float _WaveDecay;
      float _PixelSize;
      float _PixelSnap;
      float _ElasticEasing;
      float _PixelatedMovement;
      
      // 定义实例化属性
      UNITY_INSTANCING_BUFFER_START(Props)
        UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
        UNITY_DEFINE_INSTANCED_PROP(float, _TimeOffset)
        UNITY_DEFINE_INSTANCED_PROP(float, _WindStrength)
        UNITY_DEFINE_INSTANCED_PROP(float, _WindFrequency)
        UNITY_DEFINE_INSTANCED_PROP(float4, _WindDirection)
        UNITY_DEFINE_INSTANCED_PROP(float, _ElasticAmount)
        UNITY_DEFINE_INSTANCED_PROP(float, _ElasticFrequency)
        UNITY_DEFINE_INSTANCED_PROP(float, _CenterDeadZone)
        UNITY_DEFINE_INSTANCED_PROP(float, _EdgeInfluence)
        UNITY_DEFINE_INSTANCED_PROP(float4, _PivotPosition)
        UNITY_DEFINE_INSTANCED_PROP(float, _UseCustomPivot)
        UNITY_DEFINE_INSTANCED_PROP(float, _RandomSeed)
        UNITY_DEFINE_INSTANCED_PROP(float, _ImpactStrength)
        UNITY_DEFINE_INSTANCED_PROP(float, _ResponseIntensity)
        UNITY_DEFINE_INSTANCED_PROP(float, _LerpSpeed)
        UNITY_DEFINE_INSTANCED_PROP(float, _SmoothingFactor)
        UNITY_DEFINE_INSTANCED_PROP(float, _ResponseCurveStrength)
        UNITY_DEFINE_INSTANCED_PROP(float, _WholeObjectMovement)
        UNITY_DEFINE_INSTANCED_PROP(float, _WholeObjectRotation)
        UNITY_DEFINE_INSTANCED_PROP(float, _VertexDeformStrength)
        UNITY_DEFINE_INSTANCED_PROP(float, _VertexDeformSpread)
      UNITY_INSTANCING_BUFFER_END(Props)
      
      // 随机函数
      float random(float2 st) 
      {
          return frac(sin(dot(st.xy, float2(12.9898,78.233))) * 43758.5453123);
      }
      
      // 平滑衰减函数
      float smoothDecay(float distance, float radius, float strength)
      {
          if (distance > radius) return 0;
          float normalizedDist = distance / radius;
          // 使用更平滑的衰减曲线
          float falloff = 1.0 - normalizedDist * normalizedDist;
          falloff = falloff * falloff; // 二次衰减，更平滑
          return strength * falloff;
      }
      
      // 平滑步函数（自定义曲线）
      float smootherstep(float edge0, float edge1, float x)
      {
          x = saturate((x - edge0) / (edge1 - edge0));
          return x * x * x * (x * (x * 6.0 - 15.0) + 10.0);
      }
      
      // 弹性缓动函数 - 适合像素风格
      float elasticEase(float x, float elasticity)
      {
          // 弹性函数，在接近1时有轻微的回弹
          float p = 0.3 + elasticity * 0.5;
          return pow(2, -10 * x) * sin((x - p/4) * (2 * 3.14159) / p) + 1;
      }
      
      // 像素对齐函数
      float2 pixelSnap(float2 position, float pixelSize, float snapStrength)
      {
          // 计算像素网格对齐
          float2 pixelGrid = round(position / pixelSize) * pixelSize;
          // 在原始位置和像素对齐位置之间插值
          return lerp(position, pixelGrid, snapStrength);
      }
      
      // 计算平滑的交互强度
      float calculateSmoothInteractionStrength(float3 worldPos, float currentTime, float impactStrength, float responseIntensity, float responseCurveStrength)
      {
          if (impactStrength < 0.001) return 0;
          
          // 计算到玩家的距离 - 仅使用XY平面
          float2 playerPos2D = _PlayerPosition.xy;
          float2 plantPos2D = worldPos.xy;
          float distanceToPlayer = length(plantPos2D - playerPos2D);
          
          // 基础强度衰减
          float baseStrength = smoothDecay(distanceToPlayer, _ImpactRadius, impactStrength);
          if (baseStrength < 0.001) return 0;
          
          // 时间相关计算
          float timeSinceImpact = currentTime - _ImpactStartTime;
          if (timeSinceImpact < 0) return 0; // 还没开始
          
          // === 多阶段时间衰减 ===
          
          // 1. 初始冲击阶段（快速上升）
          float riseTime = 0.5; // 0.5秒内达到峰值
          float riseFactor = smootherstep(0, riseTime, timeSinceImpact);
          
          // 2. 主要衰减阶段（指数衰减）
          float mainDecay = exp(-timeSinceImpact * _DecaySpeed);
          
          // 3. 波动衰减（模拟弹性）
          float wavePhase = timeSinceImpact * _WaveFrequency - distanceToPlayer * 0.3;
          float waveIntensity = sin(wavePhase) * exp(-timeSinceImpact * _WaveDecay);
          
          // 4. 长期衰减（确保最终归零）
          float longTermDecay = exp(-timeSinceImpact * (_DecaySpeed * 0.3));
          
          // === 组合所有衰减因子 ===
          float timeDecay = riseFactor * mainDecay * longTermDecay;
          
          // 应用响应曲线
          timeDecay = pow(timeDecay, 1.0 / responseCurveStrength);
          
          // 最终强度
          float finalStrength = baseStrength * timeDecay * responseIntensity;
          
          // 添加波动效果（较小的影响）
          finalStrength += baseStrength * waveIntensity * timeDecay * 0.2;
          
          return saturate(finalStrength);
      }
      
      // 2D旋转矩阵
      float2 rotate2D(float2 v, float angle)
      {
          float s = sin(angle);
          float c = cos(angle);
          float2x2 rotMatrix = float2x2(c, -s, s, c);
          return mul(rotMatrix, v);
      }
      
      v2f vert (appdata v)
      {
          v2f o;
          
          // 设置实例ID
          UNITY_SETUP_INSTANCE_ID(v);
          UNITY_TRANSFER_INSTANCE_ID(v, o);
          UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
          
          // 获取实例化属性
          float timeOffset = UNITY_ACCESS_INSTANCED_PROP(Props, _TimeOffset);
          float windStrength = UNITY_ACCESS_INSTANCED_PROP(Props, _WindStrength);
          float windFrequency = UNITY_ACCESS_INSTANCED_PROP(Props, _WindFrequency);
          float4 windDirection = UNITY_ACCESS_INSTANCED_PROP(Props, _WindDirection);
          float elasticAmount = UNITY_ACCESS_INSTANCED_PROP(Props, _ElasticAmount);
          float elasticFrequency = UNITY_ACCESS_INSTANCED_PROP(Props, _ElasticFrequency);
          float centerDeadZone = UNITY_ACCESS_INSTANCED_PROP(Props, _CenterDeadZone);
          float edgeInfluence = UNITY_ACCESS_INSTANCED_PROP(Props, _EdgeInfluence);
          float4 pivotPosition = UNITY_ACCESS_INSTANCED_PROP(Props, _PivotPosition);
          float useCustomPivot = UNITY_ACCESS_INSTANCED_PROP(Props, _UseCustomPivot);
          float randomSeed = UNITY_ACCESS_INSTANCED_PROP(Props, _RandomSeed);
          float impactStrength = UNITY_ACCESS_INSTANCED_PROP(Props, _ImpactStrength);
          float responseIntensity = UNITY_ACCESS_INSTANCED_PROP(Props, _ResponseIntensity);
          float lerpSpeed = UNITY_ACCESS_INSTANCED_PROP(Props, _LerpSpeed);
          float smoothingFactor = UNITY_ACCESS_INSTANCED_PROP(Props, _SmoothingFactor);
          float responseCurveStrength = UNITY_ACCESS_INSTANCED_PROP(Props, _ResponseCurveStrength);
          float wholeObjectMovement = UNITY_ACCESS_INSTANCED_PROP(Props, _WholeObjectMovement);
          float wholeObjectRotation = UNITY_ACCESS_INSTANCED_PROP(Props, _WholeObjectRotation);
          float vertexDeformStrength = UNITY_ACCESS_INSTANCED_PROP(Props, _VertexDeformStrength);
          float vertexDeformSpread = UNITY_ACCESS_INSTANCED_PROP(Props, _VertexDeformSpread);
          
          // 获取顶点在对象空间中的位置
          float3 localPos = v.vertex.xyz;
          
          // 获取对象的世界位置（用于交互计算）
          float4 objectWorldPos = mul(unity_ObjectToWorld, float4(0, 0, 0, 1));
          
          // === 改进的分层影响计算 ===
          // 使用更大的影响范围，让更多顶点受到影响
          float heightFactor = saturate(localPos.y + 0.5); 
          float radialDistance = length(localPos.xy) * vertexDeformSpread;
          float normalizedRadial = saturate(radialDistance);
          
          // 使用更平滑的混合
          float combinedFactor = lerp(heightFactor, normalizedRadial, 0.6);
          
          // 减小死区，扩大边缘影响
          float adjustedCenterDeadZone = max(0.05, centerDeadZone * 0.5);
          float adjustedEdgeInfluence = min(0.95, edgeInfluence * 1.5);
          
          float layerInfluence = smoothstep(adjustedCenterDeadZone, adjustedEdgeInfluence, combinedFactor);
          
          // === 随机化种子 ===
          float plantSeed = random(objectWorldPos.xy + randomSeed);
          float randomPhase = plantSeed * _PhaseVariation;
          
          // 时间变量+时间偏移
          float time = _Time.y + timeOffset + randomPhase;
          
          // === 修改：风吹效果改为垂直方向上的扭动 ===
          float pixelAdjustedWindFreq = windFrequency * 0.7;
          
          // 主要垂直扭动效果
          float verticalSway = sin(time * pixelAdjustedWindFreq + plantSeed * 6.28) * windStrength * layerInfluence;
          
          // 计算扭动方向 - 强调垂直方向
          float2 windOffset = float2(
              verticalSway * 0.3, // 水平方向上的轻微扭动
              verticalSway * windDirection.y // 垂直方向上的主要扭动
          );
          
          // 像素对齐风偏移
          windOffset = pixelSnap(windOffset, _PixelSize * 0.5, _PixelSnap * 0.3);
          
          // === 弹性变形效果 - 像素风格优化 ===
          // 降低频率，增加可见性，强调垂直方向
          float elasticPhase = time * elasticFrequency * 0.8 + plantSeed * 6.28;
          float elasticX = 1.0 + sin(elasticPhase) * elasticAmount * 0.4 * layerInfluence; // 减小水平弹性
          float elasticY = 1.0 + sin(elasticPhase * 1.3 + 1.57) * elasticAmount * 0.8 * layerInfluence; // 增加垂直弹性
          
          // === 微风细节 - 垂直扭动 ===
          float microWind = sin(time * windFrequency * 2.0 + plantSeed * 12.56) * 0.4;
          windOffset.y += microWind * windStrength * 0.6 * layerInfluence; // 增加垂直方向的微风效果
          
          // === 玩家交互效果计算 ===
          float3 interactionWorldPos;
          if (useCustomPivot > 0.5) {
              interactionWorldPos = pivotPosition.xyz;
          } else {
              interactionWorldPos = objectWorldPos.xyz;
          }
          
          float smoothInteractionStrength = calculateSmoothInteractionStrength(interactionWorldPos, time, impactStrength, responseIntensity, responseCurveStrength);
          
          // === 整体移动和局部变形的交互效果 ===
          float2 playerInteractionOffset = float2(0, 0);
          float rotationAmount = 0;
          
          if (smoothInteractionStrength > 0.001)
          {
              float2 playerPos2D = _PlayerPosition.xy;
              float2 plantPos2D;
              if (useCustomPivot > 0.5) {
                  plantPos2D = pivotPosition.xy;
              } else {
                  plantPos2D = objectWorldPos.xy;
              }

              // 确保方向是从玩家指向植物（植物被推开的方向）
              float2 pushDirection = normalize(plantPos2D - playerPos2D);
              if (length(pushDirection) < 0.01) pushDirection = float2(1, 0);
              
              float timeSinceImpact = _Time.y - _ImpactStartTime;
              float distanceToPlayer = length(plantPos2D - playerPos2D);
              
              // 使用弹性缓动函数计算整体移动强度
              float impactProgress = saturate(timeSinceImpact * 2.0); // 0到1的进度
              float elasticFactor = elasticEase(impactProgress, _ElasticEasing);
              
              // 整体移动效果（应用到所有顶点）- 添加弹性
              float wholeObjectPushStrength = smoothInteractionStrength * wholeObjectMovement;
              float2 wholeObjectOffset = pushDirection * wholeObjectPushStrength;
              
              // 像素对齐整体移动
              wholeObjectOffset = pixelSnap(wholeObjectOffset, _PixelSize, _PixelatedMovement);
              
              // 整体旋转效果 - 减小旋转以适应像素风格
              float rotationDirection = sign(cross(float3(pushDirection, 0), float3(0, 0, 1)).z);
              rotationAmount = rotationDirection * smoothInteractionStrength * wholeObjectRotation * 0.7;
              
              // === 增强的顶点变形效果 - 像素风格优化 ===
              
              // 1. 主要波动效果 - 使用较低频率，增加振幅
              float wavePhase = timeSinceImpact * 6.0 - distanceToPlayer * 0.4 + randomPhase;
              float waveEffect = sin(wavePhase) * smoothInteractionStrength * 0.6 * vertexDeformStrength;
              
              // 2. 次要波动效果 - 频率更低，更适合像素风格
              float secondaryWavePhase = timeSinceImpact * 8.0 - distanceToPlayer * 0.6 + randomPhase * 1.7;
              float secondaryWaveEffect = sin(secondaryWavePhase) * smoothInteractionStrength * 0.35 * vertexDeformStrength;
              
              // 3. 确保即使在整体移动为1时，仍有一些顶点变形
              float localDeformFactor = max(0.35, 1.0 - (wholeObjectMovement * 0.65));
              
              // 4. 应用主要波动 - 使用弹性缓动
              float2 waveOffset = pushDirection * waveEffect * layerInfluence * localDeformFactor * elasticFactor;
              
              // 5. 应用扭曲效果 - 减少频率，增加可见性
              float2 perpendicular = float2(-pushDirection.y, pushDirection.x);
              float twistPhase = timeSinceImpact * 9.0 + randomPhase * 1.5;
              float twistEffect = sin(twistPhase) * smoothInteractionStrength * 0.3 * vertexDeformStrength;
              float2 twistOffset = perpendicular * twistEffect * layerInfluence * localDeformFactor;
              
              // 6. 应用次要波动（垂直于主要方向）
              float2 secondaryWaveOffset = perpendicular * secondaryWaveEffect * layerInfluence * localDeformFactor;
              
              // 7. 添加随机抖动效果 - 减少频率，使其更适合像素风格
              float jitterPhase = timeSinceImpact * 15.0 + plantSeed * 10.0;
              float2 jitterDirection = float2(
                  sin(jitterPhase), 
                  cos(jitterPhase * 1.3)
              );
              float jitterStrength = smoothInteractionStrength * 0.2 * vertexDeformStrength;
              float2 jitterOffset = jitterDirection * jitterStrength * layerInfluence * localDeformFactor;
              
              // 组合所有交互偏移
              float2 combinedOffset = waveOffset + twistOffset + secondaryWaveOffset + jitterOffset;
              
              // 对局部变形应用像素对齐（较轻微的对齐，保留一些平滑度）
              combinedOffset = pixelSnap(combinedOffset, _PixelSize * 0.7, _PixelSnap * 0.5);
              
              // 最终组合整体移动和局部变形
              playerInteractionOffset = wholeObjectOffset + combinedOffset;
          }
          
          // === 应用所有变形 ===
          float4 modifiedVertex = v.vertex;
          
          // 1. 应用弹性缩放（局部变形）
          modifiedVertex.x *= elasticX;
          modifiedVertex.y *= elasticY;
          
          // 2. 应用旋转（整体效果）
          if (abs(rotationAmount) > 0.001) {
              modifiedVertex.xy = rotate2D(modifiedVertex.xy, rotationAmount);
          }
          
          // 3. 应用风吹偏移（局部变形）- 垂直扭动
          modifiedVertex.x += windOffset.x;
          modifiedVertex.y += windOffset.y;
          
          // 4. 应用玩家交互偏移（整体移动 + 局部变形）
          modifiedVertex.x += playerInteractionOffset.x;
          modifiedVertex.y += playerInteractionOffset.y;
          
          // 转换到裁剪空间
          o.vertex = UnityObjectToClipPos(modifiedVertex);
          o.uv = TRANSFORM_TEX(v.uv, _MainTex);
          o.color = v.color;
          
          return o;
      }
      
        fixed4 frag (v2f i) : SV_Target
        {
            // 设置实例ID
            UNITY_SETUP_INSTANCE_ID(i);
            
            // 获取 _Color 属性（如果是实例化的）
            float4 color = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
            
            fixed4 col = tex2D(_MainTex, i.uv) * i.color * color;
            clip(col.a - 0.1);
            return col;
        }
      ENDCG
  }
}

// 添加Fallback以确保在不支持的平台上仍能渲染
Fallback "Legacy Shaders/Transparent/Cutout/VertexLit"
}
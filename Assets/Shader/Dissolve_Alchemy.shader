Shader "GourmetLine/Dissolve_Alchemy"
{
    // 溶解 / 炼金反应 Shader
    // 当原材料放入炼金炉时播放的视觉效果

    Properties
    {
        _BaseMap("Base Texture", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (0.8, 0.6, 0.3, 1)

        [Header(Dissolve)]
        // 0 = 完整不溶解，1 = 完全消失
        [Range(0,1)] _DissolveAmount("Dissolve Amount", Float) = 0.0
        // 噪声 UV 缩放：越大噪声越细碎（溶解块越小），越小越大块
        _NoiseTiling("Noise Tiling", Float) = 3.0
        // 噪声随时间的漂移速度：给溶解区域一种"活着"的感觉
        _NoiseScrollSpeed("Noise Scroll Speed", Float) = 0.15

        [Header(Dissolve Edge Glow)]
        // 溶解边缘宽度：占 noise 值域的比例（0.05~0.15 合适）
        _EdgeWidth("Edge Width", Range(0.01, 0.3)) = 0.08
        // 紧贴溶解边的颜色（最热点）：白/橙，象征物质开始气化
        _EdgeColorHot("Edge Color Hot", Color) = (1.0, 0.8, 0.3, 1)
        // 边缘外侧的颜色（稍远处）：紫/蓝，炼金魔法色
        _EdgeColorCool("Edge Color Cool", Color) = (0.5, 0.1, 0.9, 1)
        _EdgeIntensity("Edge Intensity", Range(0, 8)) = 4.0

        [Header(UV Heat Distortion)]
        // 热浪扭曲强度：_DissolveAmount 越大扭曲越强（自动关联）
        _DistortStrength("Distort Strength", Range(0, 0.05)) = 0.02
        // 扭曲噪声的 UV 缩放，与溶解噪声独立
        _DistortNoiseScale("Distort Noise Scale", Float) = 4.0

        [Header(Surface Heating)]
        // 随溶解进度整体变色：模拟物体被加热时全身泛橙
        _HeatColor("Heat Tint Color", Color) = (1.0, 0.4, 0.05, 1)
        _HeatIntensity("Heat Tint Intensity", Range(0, 2)) = 0.8

        [Header(Rim Light)]
        _RimColor("Rim Color", Color) = (1.0, 0.6, 0.2, 1)
        _RimPower("Rim Power", Range(0.1, 8)) = 3.0
        _RimIntensity("Rim Intensity", Range(0, 2)) = 0.8
    }

    SubShader
    {

        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex DNVert
            #pragma fragment DNFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #include "Dissolve_Shared.hlsl"  // 材质 CBUFFER + 噪声函数

            struct DNAttributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct DNVaryings   { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float2 uv:TEXCOORD1; };

            DNVaryings DNVert(DNAttributes IN)
            {
                DNVaryings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 DNFrag(DNVaryings IN) : SV_Target
            {
                float2 noiseUV = IN.uv * _NoiseTiling + _Time.y * _NoiseScrollSpeed;
                clip(_FBM(noiseUV) - _DissolveAmount);
                return half4(normalize(IN.normalWS), 0.0); // 世界空间法线
            }
            ENDHLSL
        }


        //  Pass 1: 主渲染
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float4 shadowCoord: TEXCOORD3;
            };

            // 纹理/采样器是资源，不进 CBUFFER，单独声明
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            // 材质常量 CBUFFER + 噪声函数
            #include "Dissolve_Shared.hlsl"

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS  = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv          = IN.uv;
                OUT.shadowCoord = TransformWorldToShadowCoord(OUT.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                Light  mainLight = GetMainLight(IN.shadowCoord);
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float3 L = normalize(mainLight.direction);
                float  NdotL = saturate(dot(N, L));
                float  NdotV = saturate(dot(N, V));

                // UV 热浪扭曲
                // 用独立噪声随时间流动扰动 UV
                // 乘以 saturate(_DissolveAmount * 4.0)：
                //   溶解刚开始时扭曲从 0 快速增加，到 _DissolveAmount=0.25 时已达满值
                //   → 溶解一开始马上就能看到热浪，不需要等到快消失才出现
                float2 dUV   = IN.uv * _DistortNoiseScale
                               + float2(_Time.y * 0.2, _Time.y * 0.15);
                float2 dOff  = float2(_GradNoise(dUV),
                                      _GradNoise(dUV + float2(3.7, 5.1)));
                float2 uv    = IN.uv + dOff * _DistortStrength
                               * saturate(_DissolveAmount * 4.0);

                // Noise 溶解
                // FBM 采样：使用扭曲后的 UV + 时间漂移
                //   → 噪声随时间缓慢流动，溶解边缘像液体一样"活着"
                float2 noiseUV  = uv * _NoiseTiling
                                  + _Time.y * _NoiseScrollSpeed;
                float  noiseVal = _FBM(noiseUV);   // [0, 1]

                // clip 核心：noiseVal < _DissolveAmount → 丢弃该像素（溶解消失）
                // noiseVal 低的区域先溶解，高的区域留到最后
                clip(noiseVal - _DissolveAmount);

                // 发光溶解边
                // edgeFactor：
                //   在 [_DissolveAmount, _DissolveAmount + _EdgeWidth] 内从 1 → 0
                //   noiseVal 越接近 clip 边界，edgeFactor 越大（越亮）
                //   超出 _EdgeWidth 的区域 edgeFactor = 0（正常表面，无额外发光）
                float edgeFactor = 1.0 - saturate(
                    (noiseVal - _DissolveAmount) / max(0.001, _EdgeWidth));

                // 双色渐变：Hot（边界正中）→ Cool（边缘外侧）
                //   模拟：物质气化的最前沿白热 → 炼金魔法能量渐变到蓝紫
                half3 edgeColor    = lerp(_EdgeColorHot.rgb, _EdgeColorCool.rgb,
                                          edgeFactor);
                half3 edgeEmission = edgeColor * edgeFactor * _EdgeIntensity;

                //采样
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;

                // 整体受热染色
                // 随溶解进度将表面颜色向 _HeatColor 偏移
                // 视觉效果：物体刚开始溶解时就像被高温炙烤，全身泛橙红
                half3 heated = lerp(albedo.rgb,
                                    albedo.rgb * _HeatColor.rgb,
                                    _DissolveAmount * _HeatIntensity);

                // 基础光照
                half3 litBase = heated * (0.4 + 0.6 * NdotL) * mainLight.color;

                // Rim Light
                float rimI = pow(1.0 - NdotV, _RimPower) * _RimIntensity;
                half3 rim  = _RimColor.rgb * rimI;

                // 合成
                half3 finalColor = litBase + edgeEmission + rim;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        //  Pass 2: 阴影投射（必须同步 clip，否则溶解后仍有阴影）
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            Cull Back
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            #include "Dissolve_Shared.hlsl"  // 材质 CBUFFER + 噪声

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = TransformWorldToHClip(
                    ApplyShadowBias(posWS, normWS, _MainLightPosition.xyz));
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 noiseUV = IN.uv * _NoiseTiling + _Time.y * _NoiseScrollSpeed;
                clip(_FBM(noiseUV) - _DissolveAmount);
                return 0;
            }
            ENDHLSL
        }

        //  Pass 3: 描边
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #include "Dissolve_Shared.hlsl"  // 材质 CBUFFER + 噪声

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings   { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float4 posCS    = TransformObjectToHClip(IN.positionOS.xyz);
                float3 normalVS = normalize(TransformWorldToViewDir(
                                  TransformObjectToWorldNormal(IN.normalOS)));
                posCS.xy += normalVS.xy * (1.0 * posCS.w * 2.0 / _ScreenParams.y);
                OUT.positionCS = posCS;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 noiseUV = IN.uv * _NoiseTiling + _Time.y * _NoiseScrollSpeed;
                clip(_FBM(noiseUV) - _DissolveAmount);
                return half4(0.1, 0.05, 0.0, 1);
            }
            ENDHLSL
        }
    }
}

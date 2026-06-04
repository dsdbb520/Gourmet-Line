Shader "GourmetLine/Wood_CelShaded"
{
    // Task 1.8 — 环境材质：木材架子
    //
    // 三个核心功能：
    //   [A] Detail Normal Map — 叠加木纹细节法线，表现年轮/纤维感
    //   [B] 暖色卡通色调     — 对 albedo 做冷暖分离，暗部偏冷棕，亮部偏暖黄
    //   [C] 木纹高光         — 沿木纹方向（切线方向）的弱各向异性高光

    Properties
    {
        _BaseMap("Wood Texture", 2D) = "white" {}
        _BaseColor("Wood Color", Color) = (0.58, 0.38, 0.18, 1)

        [Header(Detail Normal)]
        _DetailNormalMap("Detail Normal Map (Wood Grain)", 2D) = "bump" {}
        // Detail 贴图缩放倍数（通常比主贴图 tiling 高 4~8 倍）
        _DetailTiling("Detail Tiling", Float) = 4.0
        _DetailNormalStrength("Detail Normal Strength", Range(0, 2)) = 0.6

        [Header(Cel Tone)]
        // 暗部叠色：阴影区域偏向冷棕（木材在阴影里的环境光感）
        _ShadowColor("Shadow Tint", Color) = (0.28, 0.18, 0.10, 1)
        _ShadowRampThreshold("Shadow Threshold", Range(0, 1)) = 0.48
        _ShadowEdge("Shadow Edge", Range(0, 0.3)) = 0.06

        [Header(Wood Grain Specular)]
        // 木纹高光：比金属弱很多，只是表面漆层的微弱反光
        _GrainShift("Grain Tangent Shift", Range(-0.5, 0.5)) = 0.0
        _GrainShininess("Grain Shininess", Range(10, 200)) = 60
        _GrainThreshold("Grain Threshold", Range(0, 1)) = 0.55
        _GrainSpecColor("Grain Spec Color", Color) = (0.9, 0.8, 0.6, 1)
        _GrainIntensity("Grain Intensity", Range(0, 1.5)) = 0.5

        [Header(Rim Light)]
        _RimColor("Rim Color", Color) = (0.9, 0.75, 0.5, 1)
        _RimPower("Rim Power", Range(0.5, 8)) = 3.5
        _RimIntensity("Rim Intensity", Range(0, 1.5)) = 0.6

        [Header(Outline)]
        _OutlineColor("Outline Color", Color) = (0.12, 0.07, 0.03, 1)
        _OutlineWidth("Outline Width (px)", Range(0, 3)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        // ── DepthNormals Pass ───────────────────────────────────────────────
        // 写世界空间法线到 _CameraNormalsTexture，供全局 ScreenSpaceOutline 检测边缘。
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DNAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct DNVaryings   { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            DNVaryings DepthNormalsVert(DNAttributes IN)
            {
                DNVaryings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 DepthNormalsFrag(DNVaryings IN) : SV_Target
            {
                return half4(normalize(IN.normalWS), 0.0); // 世界空间法线
            }
            ENDHLSL
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On

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
                float4 tangentOS  : TANGENT;  // [A][C] 法线贴图和各向异性需要切线
                float2 uv         : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 tangentWS   : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float2 uv          : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
            };

            sampler2D _BaseMap;         float4 _BaseColor;
            sampler2D _DetailNormalMap; float  _DetailTiling; float _DetailNormalStrength;
            float4 _ShadowColor;        float  _ShadowRampThreshold; float _ShadowEdge;
            float4 _GrainSpecColor;     float  _GrainShininess;
            float  _GrainShift;         float  _GrainThreshold; float _GrainIntensity;
            float4 _RimColor;           float  _RimPower;  float _RimIntensity;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS  = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.tangentWS   = TransformObjectToWorldDir(IN.tangentOS.xyz);
                // Bitangent（副切线）= N × T，乘以 handedness 修正镜像问题
                OUT.bitangentWS = cross(OUT.normalWS, OUT.tangentWS)
                                  * IN.tangentOS.w;
                OUT.uv          = IN.uv;
                OUT.shadowCoord = TransformWorldToShadowCoord(OUT.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4  albedo    = tex2D(_BaseMap, IN.uv) * _BaseColor;
                Light  mainLight = GetMainLight(IN.shadowCoord);
                float3 L = normalize(mainLight.direction);
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);

                // ── [A] Detail Normal Map ────────────────────────────────────
                // 木纹细节法线使用更高频率的 UV 采样，叠加在基础法线上
                // 效果：表面有年轮/纤维的微观起伏，使高光更真实
                float3 detailTSNormal = UnpackNormal(
                    tex2D(_DetailNormalMap, IN.uv * _DetailTiling));
                // 在切线空间内混合：XY 叠加后归一化（简单方案，适合非 90° 法线）
                float3 T  = normalize(IN.tangentWS);
                float3 BT = normalize(IN.bitangentWS);
                float3 N0 = normalize(IN.normalWS);
                // 将 detail 法线按强度混入
                float3 blendTS  = normalize(float3(
                    detailTSNormal.xy * _DetailNormalStrength,
                    max(0.01, detailTSNormal.z)));
                // 切线空间 → 世界空间
                float3 N = normalize(blendTS.x * T + blendTS.y * BT + blendTS.z * N0);

                float3 H     = normalize(L + V);
                float  NdotL = dot(N, L);
                float  NdotV = saturate(dot(N, V));

                // ── [B] 暖色卡通阴影 ─────────────────────────────────────────
                // 暗部用 _ShadowColor（冷棕）而非纯黑，使阴影有"室内暖光反弹"感
                // 比白色 lerp 更有层次：亮部保持原木色，暗部有自己的颜色倾向
                float litVal   = dot(N, L) * mainLight.shadowAttenuation;
                float ramp     = smoothstep(_ShadowRampThreshold - _ShadowEdge,
                                            _ShadowRampThreshold + _ShadowEdge,
                                            litVal * 0.5 + 0.5);
                half3 litBase  = albedo.rgb * lerp(_ShadowColor.rgb, half3(1,1,1), ramp)
                                 * mainLight.color;

                // ── [C] 木纹方向高光 ─────────────────────────────────────────
                // 与金属各向异性相比：shininess 低（木材表面漆层不如金属光滑）
                //                    threshold 低（高光更宽、更柔和）
                //                    intensity 低（漆层反光，不是金属镜面）
                float3 T_shifted = normalize(T + N * _GrainShift);
                float  TdotH     = dot(T_shifted, H);
                float  sinTH     = sqrt(max(0.0, 1.0 - TdotH * TdotH));
                float  grainSpec = step(_GrainThreshold, pow(sinTH, _GrainShininess));
                grainSpec       *= step(0.0, NdotL);
                half3 grainLight = _GrainSpecColor.rgb * grainSpec * _GrainIntensity;

                // ── Rim Light ───────────────────────────────────────────────
                float rimI = pow(1.0 - NdotV, _RimPower) * _RimIntensity;
                half3 rim  = _RimColor.rgb * rimI;

                return half4(litBase + grainLight + rim, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual Cull Back ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings   { float4 positionCS:SV_POSITION; };
            Varyings vert(Attributes IN) {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 norWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, norWS, _MainLightPosition.xyz));
                return OUT;
            }
            half4 frag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings   { float4 positionCS:SV_POSITION; };
            float _OutlineWidth; float4 _OutlineColor;
            Varyings vert(Attributes IN) {
                Varyings OUT;
                float4 posCS  = TransformObjectToHClip(IN.positionOS.xyz);
                float3 normVS = normalize(TransformWorldToViewDir(TransformObjectToWorldNormal(IN.normalOS)));
                posCS.xy += normVS.xy * (_OutlineWidth * posCS.w * 2.0 / _ScreenParams.y);
                OUT.positionCS = posCS; return OUT;
            }
            half4 frag(Varyings IN) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}

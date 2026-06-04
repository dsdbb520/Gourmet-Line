Shader "GourmetLine/RuneConveyor_CelShaded"
{
    // Task 1.7 — 魔法符文 / 传送带 Shader
    // 把工厂传送带改造成炼金工房的魔法符文传输阵
    //
    // 四个核心功能：
    //   [A] 符文流动    — 符文贴图 UV 随时间沿传送方向滚动
    //   [B] 发光脉冲    — sin(Time) 驱动 Emission 呼吸，整体魔法感
    //   [C] Voronoi 粒子 — 程序化 Voronoi 产生闪烁魔法光点，不依赖 Particle System
    //   [D] 程序符文（可选）— 无贴图时用 Voronoi 边缘线生成符文状网格图案

    Properties
    {
        _BaseMap("Surface Texture", 2D) = "white" {}
        _BaseColor("Surface Color (传送带底色)", Color) = (0.12, 0.10, 0.18, 1)

        [Header(Rune Flow)]
        // 符文贴图：建议用带 Alpha 的单色符文图案（白色符文+黑色背景）
        // 不设置贴图时自动使用程序化 Voronoi 边缘线作为符文图案
        _RuneTex("Rune Texture", 2D) = "black" {}
        _RuneColor("Rune Color", Color) = (0.4, 0.2, 1.0, 1)
        _RuneTiling("Rune Tiling", Float) = 3.0
        // 流动速度：正值向上，负值向下（对应传送带移动方向）
        _FlowSpeed("Flow Speed", Range(-3, 3)) = 0.8
        // 流动方向（XY 分量，会被归一化）：(0,1)=沿 V 轴，(1,0)=沿 U 轴
        _FlowDirection("Flow Direction", Vector) = (0, 1, 0, 0)
        _RuneIntensity("Rune Emission Intensity", Range(0, 4)) = 2.0

        [Header(Global Pulse)]
        // 全局呼吸：所有发光效果随 sin(Time) 整体呼吸
        _PulseSpeed("Pulse Speed", Range(0.1, 5)) = 1.2
        // 脉冲深度：0=不呼吸（常亮），1=呼吸到完全熄灭
        _PulseDepth("Pulse Depth", Range(0, 1)) = 0.4

        [Header(Voronoi Magic Particles)]
        // Voronoi 粒子：程序化光点，模拟魔法粒子飞舞效果（无需 Particle System）
        _ParticleDensity("Particle Density", Float) = 8.0
        // 粒子大小：越小粒子越像针点，越大越像光晕
        _ParticleSize("Particle Size", Range(0.01, 0.3)) = 0.06
        // 粒子动画速度：每个粒子在各自格子内漂移的速度
        _ParticleSpeed("Particle Speed", Range(0, 5)) = 1.5
        _ParticleColor("Particle Color", Color) = (0.7, 0.4, 1.0, 1)
        _ParticleIntensity("Particle Intensity", Range(0, 4)) = 2.5

        [Header(Procedural Rune)]
        // 使用程序化 Voronoi 边缘线替代符文贴图（_RuneTex 为 black 时自动生效）
        // 生成的图案像电路/网格，有符文阵感
        _ProceduralRuneScale("Procedural Scale", Float) = 5.0
        _ProceduralRuneWidth("Line Width", Range(0.01, 0.15)) = 0.04

        [Header(Surface Lighting)]
        _ShadowRampThreshold("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowEdge("Shadow Edge", Range(0, 0.3)) = 0.08

        [Header(Rim Light)]
        _RimColor("Rim Color", Color) = (0.4, 0.2, 0.9, 1)
        _RimPower("Rim Power", Range(0.5, 8)) = 3.0
        _RimIntensity("Rim Intensity", Range(0, 2)) = 1.0

        [Header(Outline)]
        _OutlineColor("Outline Color", Color) = (0.06, 0.04, 0.12, 1)
        _OutlineWidth("Outline Width (px)", Range(0, 3)) = 0.8
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
                float2 uv         : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
            };

            sampler2D _BaseMap;    float4 _BaseColor;
            sampler2D _RuneTex;    float4 _RuneColor;
            float  _RuneTiling;    float  _FlowSpeed;   float _RuneIntensity;
            float4 _FlowDirection;
            float  _PulseSpeed;    float  _PulseDepth;
            float  _ParticleDensity; float _ParticleSize; float _ParticleSpeed;
            float4 _ParticleColor; float  _ParticleIntensity;
            float  _ProceduralRuneScale; float _ProceduralRuneWidth;
            float  _ShadowRampThreshold; float _ShadowEdge;
            float4 _RimColor;      float  _RimPower;    float _RimIntensity;

            // ── Voronoi 工具函数 ──────────────────────────────────────────────
            // 2D Hash：把格子坐标映射为 [0,1]² 内的伪随机点
            float2 _VoroHash(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // Voronoi 计算：
            //   返回 float3：x = 到最近细胞中心的距离
            //                y = 该细胞的唯一 ID（用于驱动每个粒子的独立动画）
            //                z = 到第二近细胞中心的距离（用于提取细胞边缘线）
            float3 _Voronoi(float2 p, float animSpeed)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float minDist1 = 8.0, minDist2 = 8.0;
                float cellID   = 0.0;
                for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                {
                    float2 neighbor = float2(x, y);
                    float2 rng      = _VoroHash(i + neighbor);
                    // 每个细胞内部的随机点随时间缓慢漂移（sin 产生来回抖动）
                    float2 cellPt   = 0.5 + 0.45 * sin(6.2831 * rng + _Time.y * animSpeed);
                    float2 diff     = neighbor + cellPt - f;
                    float  dist     = length(diff);
                    if (dist < minDist1) {
                        minDist2 = minDist1;
                        minDist1 = dist;
                        cellID   = dot(i + neighbor, float2(7.0, 113.0));
                    } else if (dist < minDist2) {
                        minDist2 = dist;
                    }
                }
                return float3(minDist1, frac(abs(cellID) * 0.001), minDist2);
            }

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
                half4  albedo    = tex2D(_BaseMap, IN.uv) * _BaseColor;
                Light  mainLight = GetMainLight(IN.shadowCoord);
                float3 N = normalize(IN.normalWS);
                float3 L = normalize(mainLight.direction);
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);

                // ── 基础卡通光照 ─────────────────────────────────────────────
                float NdotL   = dot(N, L);
                float litVal  = NdotL * mainLight.shadowAttenuation;
                float ramp    = smoothstep(_ShadowRampThreshold - _ShadowEdge,
                                           _ShadowRampThreshold + _ShadowEdge,
                                           litVal * 0.5 + 0.5);
                half3 litBase = albedo.rgb * lerp(half3(0.15, 0.12, 0.2),
                                                  half3(1, 1, 1), ramp)
                                * mainLight.color;

                // ── [B] 全局脉冲 ─────────────────────────────────────────────
                // sin 映射到 [1-PulseDepth, 1]：PulseDepth=0 → 常亮，PulseDepth=1 → 0~1 完整呼吸
                float pulse = 1.0 - _PulseDepth * (sin(_Time.y * _PulseSpeed) * 0.5 + 0.5);

                // ── [A] 符文流动 ─────────────────────────────────────────────
                // UV 沿 _FlowDirection 随时间偏移
                float2 flowDir   = normalize(_FlowDirection.xy + float2(0.0001, 0.0));
                float2 runeUV    = IN.uv * _RuneTiling + flowDir * _Time.y * _FlowSpeed;
                half4  runeSample = tex2D(_RuneTex, runeUV);
                half3  runeEmit  = runeSample.rgb * _RuneColor.rgb * _RuneIntensity * pulse;

                // ── [D] 程序化符文（Voronoi 边缘线）────────────────────────
                // 当 _RuneTex 为 black 时（用户未设置），用 Voronoi 边缘线替代
                // Voronoi 边缘 = minDist1 ≈ minDist2 的区域（两个细胞等距边界）
                // 这条边界线形成网格状图案，类似魔法阵/电路板
                float2 pv  = IN.uv * _ProceduralRuneScale
                             + flowDir * _Time.y * _FlowSpeed * 0.5;
                float3 vr  = _Voronoi(pv, 0.3);
                // 边缘厚度：minDist2 - minDist1 越小 → 越接近细胞边界 → 越亮
                float  edge = 1.0 - smoothstep(0.0, _ProceduralRuneWidth,
                                               vr.z - vr.x);
                // 仅当没有设置符文贴图（采样值接近黑色）时，程序化图案生效
                float  useTexture = saturate(dot(runeSample.rgb, float3(1,1,1)) * 3.0);
                half3  procRune   = _RuneColor.rgb * edge * _RuneIntensity * pulse
                                    * (1.0 - useTexture);
                runeEmit += procRune;

                // ── [C] Voronoi 魔法粒子 ─────────────────────────────────────
                // 原理：
                //   1. Voronoi 把 UV 空间划分成细胞，每个细胞内有一个随机漂移的点
                //   2. 到该点距离极小时 → 亮（粒子核心）
                //   3. 每个粒子用自己的 cellID 作为相位偏移，独立闪烁
                //      → 粒子们不同步闪烁，随机感更强
                float2 pUV  = IN.uv * _ParticleDensity;
                float3 vo   = _Voronoi(pUV, _ParticleSpeed);
                // 粒子核心：距离 < _ParticleSize 的区域，smoothstep 产生柔和光晕
                float  core = 1.0 - smoothstep(0.0, _ParticleSize, vo.x);
                // 独立闪烁：每个粒子用 cellID (vo.y) 作为相位，频率乘以随机缩放
                float  flicker = sin(_Time.y * _PulseSpeed * 2.0
                                     + vo.y * 6.2831) * 0.5 + 0.5;
                half3  particles = _ParticleColor.rgb * core * flicker * _ParticleIntensity;

                // ── Rim Light ───────────────────────────────────────────────
                float NdotV = saturate(dot(N, V));
                float rimI  = pow(1.0 - NdotV, _RimPower) * _RimIntensity;
                half3 rim   = _RimColor.rgb * rimI;

                // ── 合成 ────────────────────────────────────────────────────
                // 传送带表面（暗）+ 符文发光（加法）+ 粒子（加法）+ 边缘光
                half3 finalColor = litBase + runeEmit + particles + rim;

                return half4(finalColor, 1.0);
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

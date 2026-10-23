Shader "GourmetLine/AlchemyMetal_CelShaded"
{
    // 四个核心功能：
    //   [A] 各向异性高光  — Kajiya-Kay 模型，铸铁拉丝感
    //   [B] Matcap 金属反射 — 视角空间法线采样，比 Reflection Probe 更可控
    //   [C] 边缘磨损       — 顶点色 R 通道驱动 AO 和磨损 mask
    //   [D] 受热发光       — World Y 轴梯度 Emission，炉底橙红→顶部消散

    Properties
    {
        _BaseMap("Base Texture", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (0.72, 0.65, 0.52, 1)

        [Header(Surface Maps)]
        _NormalMap("Normal Map", 2D) = "bump" {}
        _NormalStrength("Normal Strength", Range(0, 2)) = 1.0
        _MetallicMap("Metallic Map", 2D) = "white" {}
        _MetallicStrength("Metallic Influence", Range(0, 1)) = 1.0
        _RoughnessMap("Roughness Map", 2D) = "black" {}
        _RoughnessStrength("Roughness Influence", Range(0, 1)) = 1.0

        [Header(Matcap Reflection)]
        _MatcapTex("Matcap Texture", 2D) = "white" {}
        _MatcapIntensity("Matcap Intensity", Range(0, 2)) = 0.8

        [Header(Anisotropic Specular)]
        // 切线方向偏移量：控制高光沿法线方向偏移，调整高光在曲面上的位置
        _AnisoShift("Aniso Tangent Shift", Range(-1, 1)) = 0.1
        _AnisoShininess("Aniso Shininess", Range(1, 512)) = 150
        _AnisoThreshold("Aniso Threshold (step)", Range(0, 1)) = 0.72
        _AnisoSpecColor("Aniso Spec Color", Color) = (1, 0.95, 0.8, 1)
        _AnisoIntensity("Aniso Intensity", Range(0, 2)) = 1.0

        [Header(Edge Wear)]
        // 顶点色 R 通道：白=新鲜金属，黑=磨损凹角。在 DCC 里手动刷。
        _WornColor("Worn Color", Color) = (0.12, 0.10, 0.08, 1)
        _WornIntensity("Worn Intensity", Range(0, 1)) = 0.8

        [Header(Heat Emission)]
        // 炉子在 World Space 里的底部/顶部 Y 坐标，用于计算热度梯度
        _HeatYBottom("Heat Y Bottom (World)", Float) = 0.0
        _HeatYTop("Heat Y Top (World)", Float) = 2.0
        _HeatColorHot("Heat Color Hot (炉底)", Color) = (1.0, 0.30, 0.02, 1)
        _HeatColorWarm("Heat Color Warm (过渡)", Color) = (0.7, 0.15, 0.0, 1)
        _HeatIntensity("Heat Intensity", Range(0, 8)) = 2.5
        // 梯度形状指数：值越大，发光越集中在底部
        _HeatFalloff("Heat Falloff", Range(0.5, 6)) = 2.5

        [Header(Rim Light)]
        _RimColor("Rim Color", Color) = (1.0, 0.55, 0.1, 1)
        _RimPower("Rim Power", Range(0.1, 8)) = 3.0
        _RimIntensity("Rim Intensity", Range(0, 2)) = 0.7

        [Header(Ramp Shadow)]
        _RampMap("Shadow Ramp Map", 2D) = "white" {}
        _ShadowEdge("Shadow Edge Softness", Range(0, 0.5)) = 0.05
        _ShadowRampThreshold("Shadow Ramp Threshold", Range(0, 1)) = 0.5

        [Header(Outline)]
        _OutlineColor("Outline Color", Color) = (0.10, 0.08, 0.06, 1)
        _OutlineWidth("Outline Width (px)", Range(0, 5)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        // 把世界空间法线写入 _CameraNormalsTexture，供全局 ScreenSpaceOutline 检测边缘。
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

        // Pass 1:主渲染 光照 + 四个金属效果
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
                float4 positionOS  : POSITION;
                float3 normalOS    : NORMAL;
                float4 tangentOS   : TANGENT;    // 各向异性高光需要切线方向
                float2 uv          : TEXCOORD0;
                float4 vertexColor : COLOR;      // 顶点色驱动磨损mask
            };

            struct Varyings {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 tangentWS   : TEXCOORD2;
                float2 uv          : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                float3 normalVS    : TEXCOORD5;
                float4 vertexColor : TEXCOORD6;
                float3 bitangentWS : TEXCOORD7;
            };

            sampler2D _BaseMap;     float4 _BaseColor;
            sampler2D _MatcapTex;   float  _MatcapIntensity;
            sampler2D _RampMap;     float  _ShadowEdge;  float _ShadowRampThreshold;
            sampler2D _NormalMap;    float _NormalStrength;
            sampler2D _MetallicMap;  float _MetallicStrength;
            sampler2D _RoughnessMap; float _RoughnessStrength;

            float4 _AnisoSpecColor; float _AnisoShininess;
            float  _AnisoShift;     float _AnisoThreshold;  float _AnisoIntensity;

            float4 _WornColor;      float _WornIntensity;

            float  _HeatYBottom;    float _HeatYTop;
            float4 _HeatColorHot;   float4 _HeatColorWarm;
            float  _HeatIntensity;  float _HeatFalloff;

            float4 _RimColor;       float _RimPower;  float _RimIntensity;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS  = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.tangentWS   = TransformObjectToWorldDir(IN.tangentOS.xyz);
                OUT.bitangentWS = cross(OUT.normalWS, OUT.tangentWS) * IN.tangentOS.w;
                OUT.uv          = IN.uv;
                OUT.shadowCoord = TransformWorldToShadowCoord(OUT.positionWS);
                OUT.normalVS    = normalize(TransformWorldToViewDir(OUT.normalWS));
                OUT.vertexColor = IN.vertexColor;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4  albedo    = tex2D(_BaseMap, IN.uv) * _BaseColor;
                Light  mainLight = GetMainLight(IN.shadowCoord);
                float3 L = normalize(mainLight.direction);
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);

                // 法线贴图 -> 世界空间
                float3 T  = normalize(IN.tangentWS);
                float3 B  = normalize(IN.bitangentWS);
                float3 N0 = normalize(IN.normalWS);
                float3 nTS = UnpackNormal(tex2D(_NormalMap, IN.uv));
                nTS.xy *= _NormalStrength;                 // xy 控凹凸强度，0 退回平面
                nTS = normalize(nTS);
                float3 N = normalize(nTS.x * T + nTS.y * B + nTS.z * N0);

                float3 H = normalize(L + V);

                // 金属度 / 粗糙度
                float metallic = lerp(1.0, tex2D(_MetallicMap, IN.uv).r, _MetallicStrength);
                float gloss    = 1.0 - tex2D(_RoughnessMap, IN.uv).r * _RoughnessStrength;

                // Ramp
                float NdotL       = dot(N, L);    //Lambert
                float shadowAtten = step(_ShadowRampThreshold, mainLight.shadowAttenuation);   // 阴影衰减硬边化
                float litValue    = NdotL * shadowAtten;           
                float rampUV      = smoothstep(0.5 - _ShadowEdge, 0.5 + _ShadowEdge,
                                               litValue * 0.5 + 0.5);  //Half-Lambert
                half3 shadowColor = tex2D(_RampMap, float2(rampUV, 0.5)).rgb;

                // Matcap 金属反射
                // 原理：把视角空间法线的 XY 映射到 [0,1] 作为 UV 采样 Matcap
                // 用扰动后的法线重算视角空间法线 → 反射也体现凹凸细节；metallic 控反射强弱
                float3 nVS      = normalize(TransformWorldToViewDir(N));
                float2 matcapUV = nVS.xy * 0.5 + 0.5;
                half3  matcap   = tex2D(_MatcapTex, matcapUV).rgb * _MatcapIntensity * metallic;

                // 各向异性高光 (Kajiya-Kay 简化) 
                // 物理原理：金属表面的微观拉丝结构沿切线方向排列
                //   → 高光形状是垂直于切线方向的弧形光带，而非圆形
                //
                // T_shifted：沿法线偏移切线 → 调整高光在曲面上出现的位置
                //   _AnisoShift > 0 → 高光向朝光侧偏移（模拟斜拉丝）
                float3 T_shifted = normalize(T + N * _AnisoShift);
                //
                // Kajiya-Kay 核心公式：
                //   I_spec = pow(sin(∠TH), shininess)
                //          = pow(sqrt(1 - (T·H)²), shininess)
                // 当 T⊥H 时（切线垂直于半向量），sin = 1 → 最亮
                // 当 T∥H 时，sin = 0 → 无高光  → 形成与切线方向平行的暗带
                float TdotH     = dot(T_shifted, H);
                float sinTH     = sqrt(max(0.0, 1.0 - TdotH * TdotH));
                float anisoSpec = step(_AnisoThreshold, pow(sinTH, _AnisoShininess));
                anisoSpec      *= step(0.0, NdotL);
                half3 anisoCel  = _AnisoSpecColor.rgb * anisoSpec * _AnisoIntensity * gloss;

                // Rim Light
                float rimI = pow(1.0 - saturate(dot(N, V)), _RimPower) * _RimIntensity;
                half3 rim  = _RimColor.rgb * rimI;

                // 边缘磨损 (Vertex Color AO)
                // 美术在 DCC 软件里：顶点色 R 通道：白=崭新金属，黑=磨损凹角
                float wearMask   = IN.vertexColor.r;
                half3 wornTint   = lerp(_WornColor.rgb, half3(1, 1, 1), wearMask);
                // lerp 控制磨损强度：_WornIntensity = 0 → 不磨损，1 → 完全磨损
                half3 wornAlbedo = albedo.rgb * lerp(half3(1, 1, 1), wornTint, _WornIntensity);

                // 受热发光
                // _HeatYBottom / _HeatYTop：设置成炉子底部和顶部的 World Y 坐标
                float worldY  = IN.positionWS.y;
                float heatT   = 1.0 - saturate(
                    (worldY - _HeatYBottom) / max(0.001, _HeatYTop - _HeatYBottom));
                // pow 集中热量：_HeatFalloff 越大，发光越集中在最底部
                heatT         = pow(heatT, _HeatFalloff);
                // 双色梯度：炉底极热 → 橙红，稍高处 → 暗红
                half3 heatCol = lerp(_HeatColorWarm.rgb, _HeatColorHot.rgb, heatT);
                half3 emission = heatCol * heatT * _HeatIntensity;

                // 基础金属色：带磨损的 albedo × Shadow Ramp × 主光颜色
                half3 metalBase  = wornAlbedo * shadowColor * mainLight.color;
                // Matcap 以 Screen 混合叠入
                half3 withMatcap = 1.0 - (1.0 - metalBase) * (1.0 - matcap);
                // 最终叠加各向异性高光、边缘光、受热发光
                half3 finalColor = withMatcap + anisoCel + rim + emission;

                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }

        //  Pass 2: 描边 (Clip Space 法线膨胀)
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
            };

            float  _OutlineWidth;
            float4 _OutlineColor;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float4 posCS    = TransformObjectToHClip(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 normalVS = normalize(TransformWorldToViewDir(normalWS));
                float  pxScale  = 2.0 / _ScreenParams.y;
                posCS.xy += normalVS.xy * (_OutlineWidth * posCS.w * pxScale);
                OUT.positionCS = posCS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}

Shader "GourmetLine/Crystal_CelShaded"
{
    // 晶体 / 宝石 Shader
    // 与金属 Shader 的根本区别：
    //   金属 = 不透明 + Matcap 反射（光在表面反弹）
    //   晶体 = 背景折射扭曲（光穿透晶体，带色散）+ 背光透色发光
    //
    // 核心功能：
    // 场景折射  — 采样 CameraOpaqueTexture，法线扰动 UV，看穿背景
    // 色散      — 折射 UV 的 RGB 三通道分别偏移，棱镜彩虹边缘
    // 背光透色  — 光从背后射来时整块晶体发出饱和色光
    // 宝石高光  — 极锐利的单点高光（钻石/晶面反光），与金属拉丝高光不同
    // Emission 脉冲 — sin(Time) 呼吸发光，魔法矿石感

    Properties
    {
        _BaseMap("Base Texture", 2D) = "white" {}
        _BaseColor("Crystal Color (tints refraction)", Color) = (0.45, 0.2, 0.85, 0.75)

        [Header(Refraction)]
        // 折射强度：法线扰动背景 UV 的幅度，值越大背景扭曲越明显
        _RefractionStrength("Refraction Strength", Range(0, 0.15)) = 0.04
        // 晶体颜色对折射背景的着色强度：0 = 折射完全无色，1 = 折射完全被晶体颜色染色
        _RefractionTint("Refraction Tint Strength", Range(0, 1)) = 0.5

        [Header(Dispersion)]
        // 色散量：折射 UV 上 R/B 通道的偏移距离（R 和 B 往相反方向偏移）
        _DispersionAmount("Dispersion Amount", Range(0, 0.05)) = 0.015

        [Header(Fresnel Opacity)]
        // Fresnel 控制晶体中心与边缘的不透明度
        _FresnelPower("Fresnel Power", Range(0.5, 8)) = 3.0
        _CenterOpacity("Center Opacity", Range(0, 1)) = 0.85
        _EdgeOpacity("Edge Opacity", Range(0, 1)) = 0.25

        [Header(Backlit Transmission)]
        // 背光透色：光从晶体背后照射时，晶体整体发出饱和色光
        _TransColor("Transmission Color", Color) = (0.65, 0.3, 1.0, 1)
        _TransIntensity("Transmission Intensity", Range(0, 4)) = 2.0
        // 背光锐利度：值越大，只有直接背光时才透色；值越小，侧光也有透色
        _TransPower("Transmission Sharpness", Range(0.5, 4)) = 1.5

        [Header(Gem Specular)]
        // 宝石高光：极锐利的单点反光，模拟刻面（facet）反光
        // 与金属各向异性拉丝高光完全不同：宝石是圆形单点，非条带
        _GemSpecColor("Gem Specular Color", Color) = (1, 1, 1, 1)
        _GemShininess("Gem Shininess", Range(50, 1024)) = 600
        _GemThreshold("Gem Threshold (step)", Range(0, 1)) = 0.92
        _GemIntensity("Gem Intensity", Range(0, 4)) = 2.5

        [Header(Inner Glow)]
        // 内部辉光：双角度采样 Matcap 相乘，产生刻面干涉亮斑
        // 无论背景有没有内容，晶体本身就有可见的发光内部结构
        _MatcapTex("Matcap (Inner Glow Source)", 2D) = "white" {}
        // 0 = 关闭内部辉光，1~2 = 正常，3 = 非常强烈
        _InnerGlowIntensity("Inner Glow Intensity", Range(0, 3)) = 1.2

        [Header(Emission Pulse)]
        _PulseColor("Pulse Emission Color", Color) = (0.55, 0.25, 1.0, 1)
        _PulseSpeed("Pulse Speed", Range(0.1, 5)) = 0.8
        _PulseIntensity("Pulse Intensity", Range(0, 4)) = 1.2

        [Header(Rim Light)]
        _RimColor("Rim Color", Color) = (0.5, 0.2, 1.0, 1)
        _RimPower("Rim Power", Range(0.1, 8)) = 2.5
        _RimIntensity("Rim Intensity", Range(0, 3)) = 1.8

        [Header(Outline)]
        _OutlineColor("Outline Color", Color) = (0.15, 0.05, 0.3, 1)
        _OutlineWidth("Outline Width (px)", Range(0, 3)) = 0.8
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        //  Pass 1: 主渲染
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            struct Attributes {
                float4 positionOS  : POSITION;
                float3 normalOS    : NORMAL;
                float2 uv          : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                float3 normalVS    : TEXCOORD4;
                float4 screenPos   : TEXCOORD5;
            };

            sampler2D _BaseMap;      float4 _BaseColor;
            sampler2D _MatcapTex;    float  _InnerGlowIntensity;
            float  _RefractionStrength;  float _RefractionTint;
            float  _DispersionAmount;
            float  _FresnelPower;    float _CenterOpacity;  float _EdgeOpacity;
            float4 _TransColor;      float _TransIntensity; float _TransPower;
            float4 _GemSpecColor;    float _GemShininess;
            float  _GemThreshold;    float _GemIntensity;
            float4 _PulseColor;      float _PulseSpeed;     float _PulseIntensity;
            float4 _RimColor;        float _RimPower;       float _RimIntensity;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS  = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv          = IN.uv;
                OUT.shadowCoord = TransformWorldToShadowCoord(OUT.positionWS);
                OUT.normalVS    = normalize(TransformWorldToViewDir(OUT.normalWS));
                // ComputeScreenPos 返回齐次屏幕坐标，在 fragment 里做透视除法
                OUT.screenPos   = ComputeScreenPos(OUT.positionCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4  albedo    = tex2D(_BaseMap, IN.uv) * _BaseColor;
                Light  mainLight = GetMainLight(IN.shadowCoord);
                float3 L = normalize(mainLight.direction);
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float3 H = normalize(L + V);

                float NdotV = saturate(dot(N, V));
                float NdotL = dot(N, L);

                // Fresnel
                float fresnel    = pow(1.0 - NdotV, _FresnelPower);
                float finalAlpha = lerp(_CenterOpacity, _EdgeOpacity, fresnel);

                // 场景折射 + 色散
                // 原理：
                //   1. 用法线在视角空间的 XY 分量扰动屏幕 UV
                //      → 从背后采样时看到偏移后的背景，产生折射感
                //   2. R/B 通道额外偏移（色散）
                //      → 不同颜色的光折射角不同，产生棱镜彩虹边
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                // 折射偏移：normalVS.xy 就是法线在屏幕平面的投影
                float2 refractOffset = IN.normalVS.xy * _RefractionStrength;
                float2 refractUV     = screenUV + refractOffset;
                // 色散偏移方向：沿法线屏幕投影方向，R 偏外 B 偏内（或反向）
                float2 dispDir = normalize(IN.normalVS.xy + float2(0.0001, 0.0));
                // 三通道分别采样（核心区别：晶体有彩色折射，金属无）
                half3 refraction;
                refraction.r = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture,
                                                refractUV + dispDir * _DispersionAmount).r;
                refraction.g = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture,
                                                refractUV).g;
                refraction.b = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture,
                                                refractUV - dispDir * _DispersionAmount).b;
                // 晶体颜色对折射背景染色（albedo 作为有色滤镜叠在折射结果上）
                half3 tintedRefraction = lerp(refraction, refraction * albedo.rgb, _RefractionTint);

                // 背光透色
                // 效果：光从晶体背后照射时，整块晶体发出晶体颜色的彩色光
                float backlit = pow(saturate(-NdotL), _TransPower);
                half3 trans   = _TransColor.rgb * backlit * _TransIntensity;

                // 宝石高光
                float NdotH    = max(0.0, dot(N, H));    // Blinn-Phong
                float gemSpec  = step(_GemThreshold, pow(NdotH, _GemShininess));
                gemSpec       *= step(0.0, NdotL);
                half3 gemLight = _GemSpecColor.rgb * gemSpec * _GemIntensity;

                // 内部辉光
                // 原理：对同一张 Matcap 做两次不同角度的采样，然后相乘
                // 旋转 30°（cos30≈0.866，sin30=0.5）让第二次采样错开，
                float2 matcapUV  = IN.normalVS.xy * 0.5 + 0.5;
                float2 nvsRot    = float2(IN.normalVS.x * 0.866 - IN.normalVS.y * 0.5,
                                         IN.normalVS.x * 0.5   + IN.normalVS.y * 0.866);
                float2 matcapUV2 = nvsRot * 0.5 + 0.5;
                half3  glow1     = tex2D(_MatcapTex, matcapUV).rgb;
                half3  glow2     = tex2D(_MatcapTex, matcapUV2).rgb;
                // 双样本相乘 × 晶体颜色染色 × 强度
                half3  innerGlow = glow1 * glow2 * albedo.rgb * _InnerGlowIntensity;
                // 晶体中心（fresnel 低 = 最厚）辉光最强，边缘衰减
                innerGlow       *= (1.0 - fresnel * 0.6);

                // Rim Light
                float rimI = pow(1.0 - NdotV, _RimPower) * _RimIntensity;
                half3 rim  = _RimColor.rgb * rimI;

                // Emission 脉冲
                float pulse    = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5;
                half3 emission = _PulseColor.rgb * pulse * _PulseIntensity;


                half3 crystalBase = tintedRefraction;
                half3 withGlow    = 1.0 - (1.0 - crystalBase) * (1.0 - innerGlow);
                half3 finalColor  = withGlow + gemLight + trans + rim + emission;

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }

        //  Pass 2: 描边
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

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

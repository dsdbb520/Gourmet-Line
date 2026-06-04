Shader "GourmetLine/StoneFloor_CelShaded"
{
    // Task 1.8 — 环境材质：石板地面
    //
    // 三个核心功能：
    //   [A] Triplanar Mapping — 不依赖 UV，世界坐标采样，地面/墙壁无缝拼接
    //   [B] 苔藓 mask        — 顶点色 G 通道驱动石头↔苔藓混合
    //   [C] 湿润高光         — 无苔藓的裸石区域有轻微积水高光

    Properties
    {
        [Header(Stone)]
        _StoneAlbedo("Stone Albedo", 2D) = "white" {}
        _StoneColor("Stone Color", Color) = (0.55, 0.52, 0.48, 1)
        _StoneNormal("Stone Normal Map", 2D) = "bump" {}
        _NormalStrength("Normal Strength", Range(0, 2)) = 1.0

        [Header(Moss)]
        _MossAlbedo("Moss Albedo", 2D) = "white" {}
        _MossColor("Moss Color", Color) = (0.22, 0.35, 0.15, 1)
        // 顶点色 G 通道：白=苔藓，黑=裸石。在 Blender 里用顶点绘制刷。
        // 苔藓通常长在低洼处、缝隙、阴暗面

        [Header(Triplanar)]
        // 世界坐标缩放倍数：越大纹理越密，越小纹理越疏
        _Tiling("World Tiling", Float) = 0.5
        // 混合锐利度：越大三个投影面之间过渡越硬，越小越柔和
        _BlendSharpness("Blend Sharpness", Range(1, 8)) = 4.0

        [Header(Wet Specular)]
        // 裸石积水：无苔藓区域的高光（苔藓吸水不反光）
        _WetColor("Wet Specular Color", Color) = (0.7, 0.75, 0.8, 1)
        _WetShininess("Wet Shininess", Range(10, 256)) = 80
        _WetThreshold("Wet Threshold", Range(0, 1)) = 0.65
        _WetIntensity("Wet Intensity", Range(0, 2)) = 0.8

        [Header(Shadow)]
        _ShadowRampThreshold("Shadow Threshold", Range(0, 1)) = 0.45
        _ShadowEdge("Shadow Edge", Range(0, 0.3)) = 0.08

        [Header(Rim Light)]
        _RimColor("Rim Color", Color) = (0.4, 0.5, 0.35, 1)
        _RimPower("Rim Power", Range(0.5, 8)) = 4.0
        _RimIntensity("Rim Intensity", Range(0, 1.5)) = 0.5

        [Header(Outline)]
        _OutlineColor("Outline Color", Color) = (0.08, 0.08, 0.06, 1)
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
                float4 positionOS  : POSITION;
                float3 normalOS    : NORMAL;
                float4 vertexColor : COLOR;   // [B] G 通道 = 苔藓 mask
            };

            struct Varyings {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                float4 vertexColor : TEXCOORD3;
            };

            sampler2D _StoneAlbedo; float4 _StoneColor;
            sampler2D _StoneNormal; float  _NormalStrength;
            sampler2D _MossAlbedo;  float4 _MossColor;
            float  _Tiling;         float  _BlendSharpness;
            float4 _WetColor;       float  _WetShininess;
            float  _WetThreshold;   float  _WetIntensity;
            float  _ShadowRampThreshold; float _ShadowEdge;
            float4 _RimColor;       float  _RimPower;   float _RimIntensity;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS  = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.shadowCoord = TransformWorldToShadowCoord(OUT.positionWS);
                OUT.vertexColor = IN.vertexColor;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                Light mainLight = GetMainLight(IN.shadowCoord);
                float3 L = normalize(mainLight.direction);
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float3 H = normalize(L + V);

                // ── [A] Triplanar Mapping ────────────────────────────────────
                // 原理：从 X/Y/Z 三个方向分别把贴图投影到表面，按法线分量加权混合
                // 好处：地面/墙壁不需要展 UV，贴图无缝拼接，不会有拉伸或接缝
                //
                // blendW：法线的绝对值经过 pow 锐化后归一化
                //   pow 越大，三个投影的过渡区越窄（混合越硬）
                float3 blendW = pow(abs(N), _BlendSharpness);
                blendW /= (blendW.x + blendW.y + blendW.z);  // 归一化，三轴权重之和=1

                float2 uvX = IN.positionWS.zy * _Tiling;
                float2 uvY = IN.positionWS.xz * _Tiling;
                float2 uvZ = IN.positionWS.xy * _Tiling;

                // Triplanar 采样石块 albedo
                half3 stoneX = tex2D(_StoneAlbedo, uvX).rgb * _StoneColor.rgb;
                half3 stoneY = tex2D(_StoneAlbedo, uvY).rgb * _StoneColor.rgb;
                half3 stoneZ = tex2D(_StoneAlbedo, uvZ).rgb * _StoneColor.rgb;
                half3 stoneCol = stoneX * blendW.x + stoneY * blendW.y + stoneZ * blendW.z;

                // Triplanar 采样苔藓 albedo
                half3 mossX = tex2D(_MossAlbedo, uvX).rgb * _MossColor.rgb;
                half3 mossY = tex2D(_MossAlbedo, uvY).rgb * _MossColor.rgb;
                half3 mossZ = tex2D(_MossAlbedo, uvZ).rgb * _MossColor.rgb;
                half3 mossCol = mossX * blendW.x + mossY * blendW.y + mossZ * blendW.z;

                // Triplanar 法线（Whiteout 混合，保持法线方向正确）
                // 每个投影平面的法线切换到对应世界轴
                float3 tnX = UnpackNormal(tex2D(_StoneNormal, uvX));
                float3 tnY = UnpackNormal(tex2D(_StoneNormal, uvY));
                float3 tnZ = UnpackNormal(tex2D(_StoneNormal, uvZ));
                tnX = float3(tnX.xy + N.zy, abs(tnX.z) * N.x);
                tnY = float3(tnY.xy + N.xz, abs(tnY.z) * N.y);
                tnZ = float3(tnZ.xy + N.xy, abs(tnZ.z) * N.z);
                float3 triNormal = normalize(
                    tnX.zyx * blendW.x + tnY.xzy * blendW.y + tnZ.xyz * blendW.z);
                float3 Nf = normalize(lerp(N, triNormal, _NormalStrength));

                // ── [B] 苔藓 mask ────────────────────────────────────────────
                // 顶点色 G 通道：1=苔藓，0=裸石
                float mossMask = IN.vertexColor.g;
                half3 albedo   = lerp(stoneCol, mossCol, mossMask);

                // ── 卡通阴影 ────────────────────────────────────────────────
                float NdotL   = dot(Nf, L);
                float litVal  = NdotL * mainLight.shadowAttenuation;
                float ramp    = smoothstep(_ShadowRampThreshold - _ShadowEdge,
                                           _ShadowRampThreshold + _ShadowEdge,
                                           litVal * 0.5 + 0.5);
                half3 litBase = albedo * lerp(half3(0.3, 0.3, 0.3), half3(1,1,1), ramp)
                                * mainLight.color;

                // ── [C] 湿润高光 ────────────────────────────────────────────
                // 苔藓吸水不反光，裸石表面积水有镜面感
                // wetMask = 1 - mossMask：裸石区域才出现湿润高光
                float NdotH   = saturate(dot(Nf, H));
                float wetSpec = step(_WetThreshold, pow(NdotH, _WetShininess));
                wetSpec      *= step(0.0, NdotL) * (1.0 - mossMask);
                half3 wetLight = _WetColor.rgb * wetSpec * _WetIntensity;

                // ── Rim Light ───────────────────────────────────────────────
                float NdotV = saturate(dot(Nf, V));
                float rimI  = pow(1.0 - NdotV, _RimPower) * _RimIntensity;
                half3 rim   = _RimColor.rgb * rimI;

                return half4(litBase + wetLight + rim, 1.0);
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
                float4 posCS   = TransformObjectToHClip(IN.positionOS.xyz);
                float3 normVS  = normalize(TransformWorldToViewDir(TransformObjectToWorldNormal(IN.normalOS)));
                posCS.xy += normVS.xy * (_OutlineWidth * posCS.w * 2.0 / _ScreenParams.y);
                OUT.positionCS = posCS; return OUT;
            }
            half4 frag(Varyings IN) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}

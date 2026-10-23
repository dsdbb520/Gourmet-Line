Shader "GourmetLine/AnimeFood_CelShaded"
{
    Properties
    {
        _BaseMap("Base Texture", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)

        [Header(Cel Shading)]
        _RampMap("Shadow Ramp Map", 2D) = "white" {}
        _ShadowEdge("Shadow Edge Softness", Range(0, 0.5)) = 0.05
        _ShadowRampThreshold("Shadow Ramp Threshold", Range(0, 1)) = 0.5

        [Header(Pseudo SSS)]
        _SSSColor("SSS Color", Color) = (1, 0.5, 0.2, 1)
        _SSSPower("SSS Power", Range(0.1, 10)) = 2.0

        [Header(Rim Light)]
        _RimColor("Rim Color", Color) = (1.0, 0.55, 0.1, 1)
        _RimPower("Rim Power", Range(0.1, 8)) = 3.0
        _RimIntensity("Rim Intensity", Range(0, 2)) = 1.0

        [Header(Cel Specular)]
        _CelSpecColor("Specular Color", Color) = (1, 1, 1, 1)
        _SpecThreshold("Specular Threshold", Range(0, 1)) = 0.8
        _SpecShininess("Specular Shininess", Range(1, 256)) = 80

        [Header(Outline)]
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth("Outline Width (px)", Range(0, 5)) = 1.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

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
                return half4(normalize(IN.normalWS), 0.0);
            }
            ENDHLSL
        }

        // Pass 1: 主渲染 Pass (卡通光照 + SSS)
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
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
            };

            sampler2D _BaseMap; float4 _BaseColor;
            sampler2D _RampMap; float _ShadowEdge; float _ShadowRampThreshold;
            float4 _SSSColor; float _SSSPower;
            float4 _RimColor; float _RimPower; float _RimIntensity;
            float4 _CelSpecColor; float _SpecThreshold; float _SpecShininess;

            Varyings vert(Attributes IN) {
                Varyings OUT;
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz); //M矩阵
                OUT.positionCS  = TransformWorldToHClip(OUT.positionWS);     //VP矩阵
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv          = IN.uv;
                OUT.shadowCoord = TransformWorldToShadowCoord(OUT.positionWS);  // 世界空间位置变换到主光源的阴影贴图空间，用于后续采样阴影贴图
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target {
                half4 albedo    = tex2D(_BaseMap, IN.uv) * _BaseColor;  // 基础颜色（纹理采样乘以颜色属性）
                Light mainLight = GetMainLight(IN.shadowCoord);    // 获取主光源信息（方向、颜色、阴影衰减等）
                float3 L        = normalize(mainLight.direction);     // 光照方向（从表面指向光源）
                float3 N        = normalize(IN.normalWS);      // 法线
                float3 V        = normalize(_WorldSpaceCameraPos - IN.positionWS);  // 视线方向（从表面指向相机）

                float NdotL     = dot(N, L);   // Lambert光照模型
                // 接收投射阴影：step硬边化，保持卡通色块感
                float shadowAtten = step(_ShadowRampThreshold, mainLight.shadowAttenuation);
                // 采样Ramp
                float litValue = NdotL * shadowAtten;
                float rampUV = smoothstep(0.5 - _ShadowEdge, 0.5 + _ShadowEdge, litValue * 0.5 + 0.5);  //Half-Lambert
                half3 shadowColor = tex2D(_RampMap, float2(rampUV, 0.5)).rgb;

                // Pseudo-SSS 伪次表面散射
                float backlight = max(0, dot(V, -L));
                float sssIntensity = pow(backlight, _SSSPower) * saturate(-NdotL + 0.5);
                half3 sss = _SSSColor.rgb * sssIntensity;

                // Rim Light 边缘光
                float rimIntensity = pow(1.0 - saturate(dot(N, V)), _RimPower) * _RimIntensity; //视线与法线夹角越大（越接近边缘），rimIntensity 越强
                half3 rim = _RimColor.rgb * rimIntensity;

                // Cel Specular 硬边高光
                float3 H = normalize(L + V);  // 半程向量（光照方向与视线方向的平均方向）
                float NdotH = max(0, dot(N, H));
                float spec = step(_SpecThreshold, pow(NdotH, _SpecShininess));
                half3 specular = _CelSpecColor.rgb * spec;

                half3 finalColor = albedo.rgb * shadowColor * mainLight.color + sss + rim + specular;
                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }

        // Pass 2: 描边 (Clip Space 法线膨胀)
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front  // 只渲染背面，正面剔除——这样描边才不会遮住正面颜色

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

            Varyings vert(Attributes IN) {
                Varyings OUT;

                // 把顶点变换到 Clip Space
                float4 posCS = TransformObjectToHClip(IN.positionOS.xyz);

                // 把法线变换到 View Space，取 XY 作为屏幕偏移方向
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 normalVS = normalize(TransformWorldToViewDir(normalWS));

                // 在 Clip Space 做像素精确的偏移
                float pixelScale = 2.0 / _ScreenParams.y;
                posCS.xy += normalVS.xy * (_OutlineWidth * posCS.w * pixelScale);

                OUT.positionCS = posCS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
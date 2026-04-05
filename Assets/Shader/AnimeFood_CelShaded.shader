Shader "GourmetLine/AnimeFood_CelShaded"
{
    Properties
    {
        _BaseMap("Base Texture", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        
        [Header(Cel Shading)]
        _RampMap("Shadow Ramp Map", 2D) = "white" {}
        
        [Header(Pseudo SSS)]
        _SSSColor("SSS Color", Color) = (1, 0.5, 0.2, 1)
        _SSSPower("SSS Power", Range(0.1, 10)) = 2.0
        
        [Header(Outline)]
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth("Outline Width", Range(0, 0.05)) = 0.01
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        // Pass 1: 主渲染 Pass (卡通光照 + SSS)
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : NORMAL;
                float2 uv : TEXCOORD1;
            };

            sampler2D _BaseMap; float4 _BaseColor;
            sampler2D _RampMap;
            float4 _SSSColor; float _SSSPower;

            Varyings vert(Attributes IN) {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target {
                half4 albedo = tex2D(_BaseMap, IN.uv) * _BaseColor;
                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);

                // 1. 双层色块感阴影 (Ramp Map)
                float NdotL = dot(N, L);
                // 将 NdotL 映射到 0~1 区间采样 Ramp 贴图
                float rampUV = NdotL * 0.5 + 0.5;
                half3 shadowColor = tex2D(_RampMap, float2(rampUV, 0.5)).rgb;

                // 2. 微弱次表面散射 (Pseudo-SSS) - 模拟光线穿透食物边缘的通透感
                // 使用 (V, -L) 点乘，配合背光衰减
                float backlight = max(0, dot(V, -L));
                float sssIntensity = pow(backlight, _SSSPower) * saturate(-NdotL + 0.5); 
                half3 sss = _SSSColor.rgb * sssIntensity;

                half3 finalColor = albedo.rgb * shadowColor * mainLight.color + sss;
                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }

        // Pass 2: 自定义描边 (背面膨胀法)
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front // 剔除正面，只渲染背面

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
            };

            float _OutlineWidth;
            float4 _OutlineColor;

            Varyings vert(Attributes IN) {
                Varyings OUT;
                // 沿法线方向膨胀顶点
                float3 posOS = IN.positionOS.xyz + IN.normalOS * _OutlineWidth;
                OUT.positionCS = TransformObjectToHClip(posOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
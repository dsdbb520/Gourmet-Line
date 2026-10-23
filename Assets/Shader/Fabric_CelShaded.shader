Shader "GourmetLine/Fabric_CelShaded"
{
    // 环境材质：布料材料袋

    Properties
    {
        _BaseMap("Fabric Texture", 2D) = "white" {}
        _BaseColor("Fabric Color", Color) = (0.55, 0.42, 0.28, 1)

        [Header(Dual Anisotropic)]
        _WarpShift("Warp Tangent Shift", Range(-1, 1)) = 0.2
        _WarpShininess("Warp Shininess", Range(1, 256)) = 40
        _WarpThreshold("Warp Threshold", Range(0, 1)) = 0.5
        _WarpColor("Warp Specular Color", Color) = (0.95, 0.88, 0.7, 1)
        _WarpIntensity("Warp Intensity", Range(0, 2)) = 0.7


        _WeftShift("Weft Tangent Shift", Range(-1, 1)) = -0.15
        _WeftShininess("Weft Shininess", Range(1, 256)) = 25
        _WeftThreshold("Weft Threshold", Range(0, 1)) = 0.45
        _WeftColor("Weft Specular Color", Color) = (0.8, 0.7, 0.55, 1)
        _WeftIntensity("Weft Intensity", Range(0, 2)) = 0.5

        [Header(Shadow)]
        _ShadowColor("Shadow Tint", Color) = (0.25, 0.18, 0.12, 1)
        _ShadowRampThreshold("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowEdge("Shadow Edge", Range(0, 0.4)) = 0.15

        [Header(Rim Light)]
        _RimColor("Rim Color", Color) = (0.85, 0.7, 0.45, 1)
        _RimPower("Rim Power", Range(0.5, 8)) = 2.5
        _RimIntensity("Rim Intensity", Range(0, 2)) = 0.8

        [Header(Outline)]
        _OutlineColor("Outline Color", Color) = (0.15, 0.10, 0.05, 1)
        _OutlineWidth("Outline Width (px)", Range(0, 3)) = 1.0

        [Header(Detail)]
        _NormalMap("Normal Map", 2D) = "bump" {}
        _NormalStrength("Normal Strength", Range(0, 2)) = 1.0

        _RoughnessMap("Roughness Map", 2D) = "white" {}
        _RoughnessStrength("Roughness Influence", Range(0, 1)) = 1.0
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
                float4 tangentOS  : TANGENT;
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

            sampler2D _BaseMap;   float4 _BaseColor;
            sampler2D _NormalMap;   float _NormalStrength;
            sampler2D _RoughnessMap; float _RoughnessStrength;
            float4 _WarpColor;    float  _WarpShininess; float _WarpShift;
            float  _WarpThreshold; float _WarpIntensity;
            float4 _WeftColor;    float  _WeftShininess; float _WeftShift;
            float  _WeftThreshold; float _WeftIntensity;
            float4 _ShadowColor;  float  _ShadowRampThreshold; float _ShadowEdge;
            float4 _RimColor;     float  _RimPower;  float _RimIntensity;

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
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4  albedo    = tex2D(_BaseMap, IN.uv) * _BaseColor;
                Light  mainLight = GetMainLight(IN.shadowCoord);
                float3 L = normalize(mainLight.direction);
                float3 T  = normalize(IN.tangentWS);
                float3 B  = normalize(IN.bitangentWS);
                float3 N0 = normalize(IN.normalWS);
                float3 nTS = UnpackNormal(tex2D(_NormalMap, IN.uv));
                nTS.xy *= _NormalStrength;                 // 缩放xy控制凹凸强度
                nTS = normalize(nTS);
                float3 N = normalize(nTS.x * T + nTS.y * B + nTS.z * N0);
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float3 H = normalize(L + V);

                float NdotL = dot(N, L);
                float NdotV = saturate(dot(N, V));

                // 卡通阴影
                float litVal  = NdotL * mainLight.shadowAttenuation;
                float ramp    = smoothstep(_ShadowRampThreshold - _ShadowEdge,
                                           _ShadowRampThreshold + _ShadowEdge,
                                           litVal * 0.5 + 0.5);
                half3 litBase = albedo.rgb * lerp(_ShadowColor.rgb, half3(1,1,1), ramp)
                                * mainLight.color;

                // 双层各向异性
                // 布料结构：经线(Warp)垂直 + 纬线(Weft)水平，形成网格交织
                // 经线高光（沿 T 方向纤维）
                float rough = tex2D(_RoughnessMap, IN.uv).r * _RoughnessStrength;
                float gloss = 1.0 - rough;   // 光滑度 = 1 - 粗糙度
                float3 T_warp   = normalize(T + N * _WarpShift);
                float  sinWarp  = sqrt(max(0.0, 1.0 - dot(T_warp, H) * dot(T_warp, H)));
                float  warpSpec = step(_WarpThreshold, pow(sinWarp, _WarpShininess));
                warpSpec       *= step(0.0, NdotL);
                half3 warpLight = _WarpColor.rgb * warpSpec * _WarpIntensity * gloss;

                // 纬线高光（沿 B 方向纤维，即 N×T）
                // B（副切线）= 经线的垂直方向 = 纬线方向
                // _WeftShift 通常与 _WarpShift 符号相反：两组纤维高光在不同位置
                float3 B_weft   = normalize(B + N * _WeftShift);
                float  sinWeft  = sqrt(max(0.0, 1.0 - dot(B_weft, H) * dot(B_weft, H)));
                float  weftSpec = step(_WeftThreshold, pow(sinWeft, _WeftShininess));
                weftSpec       *= step(0.0, NdotL);
                half3 weftLight = _WeftColor.rgb * weftSpec * _WeftIntensity * gloss;

                // Rim Light
                float rimI = pow(1.0 - NdotV, _RimPower) * _RimIntensity;
                half3 rim  = _RimColor.rgb * rimI;

                return half4(litBase + warpLight + weftLight + rim, 1.0);
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

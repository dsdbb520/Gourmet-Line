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
        // Queue=2501：刚好越过 URP 的透明渲染阈值（2500）。
        // 在这个队列，不透明物体的深度缓冲已经完全写入，
        // SRPDefaultUnlit 的描边 Pass 才能正确做深度测试，不再穿透前景物体。
        Tags { "RenderType"="Opaque" "Queue"="AlphaTest+51" "RenderPipeline"="UniversalPipeline" }

        // Pass 1: 主渲染 Pass (卡通光照 + SSS)
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On  // 显式声明写深度，防止 URP 因队列 >2500 而自动关闭 ZWrite
            
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
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS  = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv          = IN.uv;
                OUT.shadowCoord = TransformWorldToShadowCoord(OUT.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target {
                half4 albedo    = tex2D(_BaseMap, IN.uv) * _BaseColor;
                Light mainLight = GetMainLight(IN.shadowCoord);
                float3 L        = normalize(mainLight.direction);
                float3 N        = normalize(IN.normalWS);
                float3 V        = normalize(_WorldSpaceCameraPos - IN.positionWS);

                // 1. Ramp Shadow — 自身光照 + 接收其他物体投射的阴影
                float NdotL     = dot(N, L);
                // 接收投射阴影：step 硬边化，保持卡通色块感
                float shadowAtten = step(_ShadowRampThreshold, mainLight.shadowAttenuation);
                // 自身 NdotL 与投射阴影合并后采样 Ramp
                float litValue = NdotL * shadowAtten;
                float rampUV = smoothstep(0.5 - _ShadowEdge, 0.5 + _ShadowEdge, litValue * 0.5 + 0.5);
                half3 shadowColor = tex2D(_RampMap, float2(rampUV, 0.5)).rgb;

                // 2. Pseudo-SSS
                float backlight = max(0, dot(V, -L));
                float sssIntensity = pow(backlight, _SSSPower) * saturate(-NdotL + 0.5);
                half3 sss = _SSSColor.rgb * sssIntensity;

                // 3. Rim Light — 边缘光
                float rimIntensity = pow(1.0 - saturate(dot(N, V)), _RimPower) * _RimIntensity;
                half3 rim = _RimColor.rgb * rimIntensity;

                // 4. Cel Specular — 硬边高光
                float3 H = normalize(L + V);
                float NdotH = max(0, dot(N, H));
                float spec = step(_SpecThreshold, pow(NdotH, _SpecShininess));
                half3 specular = _CelSpecColor.rgb * spec;

                half3 finalColor = albedo.rgb * shadowColor * mainLight.color + sss + rim + specular;
                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }

        // Pass 2: 描边 (Clip Space 法线膨胀)
        // 在队列 2501（透明阈值之后），SRPDefaultUnlit 能正确访问不透明深度缓冲，
        // 背景物体的描边无法再穿透到该物体的像素上。
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

                // ── Step 1：把顶点变换到 Clip Space（齐次裁剪空间）──────────────
                // 此时还没做透视除法（posCS.xyz / posCS.w）。
                // posCS.w ≈ 顶点到相机的距离（线性深度），越远 w 越大。
                float4 posCS = TransformObjectToHClip(IN.positionOS.xyz);

                // ── Step 2：把法线变换到 View Space，取 XY 作为屏幕偏移方向 ────
                // 注意：这里用 View Space 而非直接用 Clip Space。
                // View Space 的 XY 轴就是相机的右/上方向，
                // 与屏幕 XY 对齐（投影矩阵几乎只做缩放），所以 normalVS.xy 就是
                // "在屏幕上这条法线指向哪个方向"，不会被透视拉歪。
                // （TransformWorldToHClipDir 在 URP 14 中不是标准函数，故改用 ViewDir）
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 normalVS = normalize(TransformWorldToViewDir(normalWS));

                // ── Step 3：在 Clip Space 做像素精确的偏移 ──────────────────────
                // _ScreenParams.y = 屏幕高度（像素）
                // 2.0 / _ScreenParams.y = 一个像素对应多少 NDC 单位
                // 乘以 posCS.w：透视除法（÷w）会把这个偏移缩回去，
                // 最终 NDC 偏移 = _OutlineWidth × (2/屏高) = 与距离无关的像素宽度。
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
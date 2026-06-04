Shader "GourmetLine/Dissolve_Alchemy"
{
    // Task 1.6 — 溶解 / 炼金反应 Shader
    // 当原材料放入炼金炉时播放的视觉效果
    //
    // 四个核心功能：
    //   [A] Noise 溶解   — FBM 程序化噪声 + clip()，从边缘向内侶食消失
    //   [B] 发光溶解边   — smoothstep 检测边界区域，叠加 Emission，双色渐变
    //   [C] UV 热浪扭曲  — 独立噪声驱动 UV 偏移，随时间流动，溶解越深扭曲越强
    //   [D] 对外接口     — _DissolveAmount (0→1)，由 C# AnimationCurve 或 Tween 驱动
    //
    // C# 调用示例：
    //   mat.SetFloat("_DissolveAmount", dissolveValue); // 0=完整 1=消失
    //
    // 噪声实现：纯程序化 FBM（3 层梯度噪声叠加），不依赖额外贴图

    Properties
    {
        _BaseMap("Base Texture", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (0.8, 0.6, 0.3, 1)

        [Header(Dissolve)]
        // 0 = 完整不溶解，1 = 完全消失
        // 由 C# 的 AnimationCurve 控制，ProcessorMachine 完成时触发
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
        // AlphaTest 队列：使用 clip() 裁切，必须在此队列才能正确投影
        // (≤2500 → 也会进入 URP DepthNormals 预通道，下方 DepthNormals Pass 才生效)
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }

        // ── DepthNormals Pass（含溶解 clip）──────────────────────────────────
        // 写世界空间法线供全局 ScreenSpaceOutline 检测边缘；必须与主 Pass 同步 clip，
        // 否则已溶解消失的部分仍写法线，会被全局描边勾出"幽灵轮廓"。
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

            float _DissolveAmount; float _NoiseTiling; float _NoiseScrollSpeed;

            // 复用与主 Pass 一致的噪声，保证 clip 结果相同
            float2 _Hash2(float2 p){p=float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3)));return frac(sin(p)*43758.5453);}
            float _GradNoise(float2 p){float2 i=floor(p);float2 f=frac(p);float2 u=f*f*(3.0-2.0*f);float a=dot(_Hash2(i+float2(0,0)),f-float2(0,0));float b=dot(_Hash2(i+float2(1,0)),f-float2(1,0));float c=dot(_Hash2(i+float2(0,1)),f-float2(0,1));float d=dot(_Hash2(i+float2(1,1)),f-float2(1,1));return lerp(lerp(a,b,u.x),lerp(c,d,u.x),u.y);}
            float _FBM(float2 p){float v=0.0;float a=0.5;for(int i=0;i<3;i++){v+=a*_GradNoise(p);p=p*2.0+float2(1.7,9.2);a*=0.5;}return v*0.5+0.5;}

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

        // ══════════════════════════════════════════════
        //  Pass 1: 主渲染
        // ══════════════════════════════════════════════
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

            sampler2D _BaseMap;   float4 _BaseColor;
            float  _DissolveAmount;
            float  _NoiseTiling;  float  _NoiseScrollSpeed;
            float  _EdgeWidth;
            float4 _EdgeColorHot; float4 _EdgeColorCool; float _EdgeIntensity;
            float  _DistortStrength; float _DistortNoiseScale;
            float4 _HeatColor;    float  _HeatIntensity;
            float4 _RimColor;     float  _RimPower;  float _RimIntensity;

            // ── 噪声函数 ────────────────────────────────────────────────────
            // 2D Hash：把 float2 坐标映射成伪随机方向向量
            float2 _Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // 梯度噪声 (Gradient Noise)：
            //   在每个整数格点放一个随机方向向量
            //   用到格点的 dot(grad, offset) 做双线性插值
            //   输出范围 [-1, 1]
            float _GradNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                // Hermite 平滑插值曲线（比线性插值更光滑，无明显方块感）
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = dot(_Hash2(i + float2(0,0)), f - float2(0,0));
                float b = dot(_Hash2(i + float2(1,0)), f - float2(1,0));
                float c = dot(_Hash2(i + float2(0,1)), f - float2(0,1));
                float d = dot(_Hash2(i + float2(1,1)), f - float2(1,1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // FBM (Fractional Brownian Motion)：
            //   叠加 3 层不同频率/振幅的梯度噪声
            //   低频（大块）+ 中频 + 高频（细节）→ 有机状的云/岩石纹理
            //   振幅减半、频率翻倍 = 每层细节更细但权重更小
            //   输出范围约 [0, 1]（通过 * 0.5 + 0.5 重映射）
            float _FBM(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    v += a * _GradNoise(p);
                    // 偏移避免每层噪声的格点重合
                    p  = p * 2.0 + float2(1.7, 9.2);
                    a *= 0.5;
                }
                return v * 0.5 + 0.5;
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
                Light  mainLight = GetMainLight(IN.shadowCoord);
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float3 L = normalize(mainLight.direction);
                float  NdotL = saturate(dot(N, L));
                float  NdotV = saturate(dot(N, V));

                // ── [C] UV 热浪扭曲 ─────────────────────────────────────────
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

                // ── [A] Noise 溶解 ───────────────────────────────────────────
                // FBM 采样：使用扭曲后的 UV + 时间漂移
                //   → 噪声随时间缓慢流动，溶解边缘像液体一样"活着"
                float2 noiseUV  = uv * _NoiseTiling
                                  + _Time.y * _NoiseScrollSpeed;
                float  noiseVal = _FBM(noiseUV);   // [0, 1]

                // clip 核心：noiseVal < _DissolveAmount → 丢弃该像素（溶解消失）
                // noiseVal 低的区域先溶解，高的区域留到最后
                clip(noiseVal - _DissolveAmount);

                // ── [B] 发光溶解边 ───────────────────────────────────────────
                // 溶解边缘 = noiseVal 刚好大于 _DissolveAmount 的一条细带
                //
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

                // ── 采样 Albedo（使用热浪扭曲后的 UV）────────────────────────
                half4 albedo = tex2D(_BaseMap, uv) * _BaseColor;

                // ── 整体受热染色 ─────────────────────────────────────────────
                // 随溶解进度将表面颜色向 _HeatColor 偏移
                // 视觉效果：物体刚开始溶解时就像被高温炙烤，全身泛橙红
                half3 heated = lerp(albedo.rgb,
                                    albedo.rgb * _HeatColor.rgb,
                                    _DissolveAmount * _HeatIntensity);

                // ── 基础光照（简单半步 cel）──────────────────────────────────
                half3 litBase = heated * (0.4 + 0.6 * NdotL) * mainLight.color;

                // ── Rim Light ────────────────────────────────────────────────
                float rimI = pow(1.0 - NdotV, _RimPower) * _RimIntensity;
                half3 rim  = _RimColor.rgb * rimI;

                // ── 合成 ─────────────────────────────────────────────────────
                // 顺序：基础色 + 边缘发光（Additive）+ 边缘光
                // 边缘发光用加法混合：让溶解边缘真正"发光"，而非只是变色
                half3 finalColor = litBase + edgeEmission + rim;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // ══════════════════════════════════════════════
        //  Pass 2: 阴影投射（必须同步 clip，否则溶解后仍有阴影）
        // ══════════════════════════════════════════════
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

            float _DissolveAmount;
            float _NoiseTiling;
            float _NoiseScrollSpeed;

            // 复用同一套噪声函数（Shadow Pass 必须与主 Pass clip 结果一致）
            float2 _Hash2(float2 p) {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }
            float _GradNoise(float2 p) {
                float2 i = floor(p); float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = dot(_Hash2(i+float2(0,0)), f-float2(0,0));
                float b = dot(_Hash2(i+float2(1,0)), f-float2(1,0));
                float c = dot(_Hash2(i+float2(0,1)), f-float2(0,1));
                float d = dot(_Hash2(i+float2(1,1)), f-float2(1,1));
                return lerp(lerp(a,b,u.x), lerp(c,d,u.x), u.y);
            }
            float _FBM(float2 p) {
                float v=0.0; float a=0.5;
                for(int i=0;i<3;i++){
                    v += a*_GradNoise(p); p=p*2.0+float2(1.7,9.2); a*=0.5;
                }
                return v*0.5+0.5;
            }

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

        // ══════════════════════════════════════════════
        //  Pass 3: 描边
        // ══════════════════════════════════════════════
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _DissolveAmount;
            float _NoiseTiling;
            float _NoiseScrollSpeed;

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings   { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };

            float2 _Hash2(float2 p){p=float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3)));return frac(sin(p)*43758.5453);}
            float _GradNoise(float2 p){float2 i=floor(p);float2 f=frac(p);float2 u=f*f*(3.0-2.0*f);float a=dot(_Hash2(i+float2(0,0)),f-float2(0,0));float b=dot(_Hash2(i+float2(1,0)),f-float2(1,0));float c=dot(_Hash2(i+float2(0,1)),f-float2(0,1));float d=dot(_Hash2(i+float2(1,1)),f-float2(1,1));return lerp(lerp(a,b,u.x),lerp(c,d,u.x),u.y);}
            float _FBM(float2 p){float v=0.0;float a=0.5;for(int i=0;i<3;i++){v+=a*_GradNoise(p);p=p*2.0+float2(1.7,9.2);a*=0.5;}return v*0.5+0.5;}

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

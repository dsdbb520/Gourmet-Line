Shader "Hidden/GourmetLine/ScreenSpaceOutline"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float4 _BlitTexture_TexelSize;
            float4 _OutlineColor;
            float  _OutlineThickness;
            float  _NormalThreshold;
            float  _DepthThreshold;   // 建议设为 999 禁用，详见注释

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float2 t  = _BlitTexture_TexelSize.xy * _OutlineThickness;

                // ── 法线边缘检测（主力，无遮挡穿透问题）─────────────────────────
                // 法线缓冲经深度排序渲染，每像素只存最近表面的法线。
                // 背景物体的法线绝不会出现在前景像素，因此无法产生虚假边缘。
                // 检测到的边缘类型：物体轮廓 / 相邻物体交界 / 自身折角
                float3 n0 = SampleSceneNormals(uv + float2(-1, -1) * t);
                float3 n1 = SampleSceneNormals(uv + float2( 1,  1) * t);
                float3 n2 = SampleSceneNormals(uv + float2( 1, -1) * t);
                float3 n3 = SampleSceneNormals(uv + float2(-1,  1) * t);
                float normalEdge = step(_NormalThreshold, length(n0 - n1) + length(n2 - n3));

                // ── 深度边缘检测（辅助，默认禁用）───────────────────────────────
                // 深度检测天然有"前景穿透"问题：偏移采样点可能滑到背景物体，
                // 把背景边缘误判为前景像素处的边缘。法线检测已能覆盖绝大多数边缘，
                // 若需要开启，将 _DepthThreshold 调低（如 0.1），代价是可能出现穿透描边。
                float ld0 = Linear01Depth(SampleSceneDepth(uv + float2(-1, -1) * t), _ZBufferParams);
                float ld1 = Linear01Depth(SampleSceneDepth(uv + float2( 1,  1) * t), _ZBufferParams);
                float ld2 = Linear01Depth(SampleSceneDepth(uv + float2( 1, -1) * t), _ZBufferParams);
                float ld3 = Linear01Depth(SampleSceneDepth(uv + float2(-1,  1) * t), _ZBufferParams);
                float depthEdge = step(_DepthThreshold, abs(ld0 - ld1) + abs(ld2 - ld3));

                float edge = max(normalEdge, depthEdge);

                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                return lerp(source, _OutlineColor, edge);
            }
            ENDHLSL
        }
    }
}

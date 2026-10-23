#ifndef GOURMETLINE_DISSOLVE_SHARED_INCLUDED
#define GOURMETLINE_DISSOLVE_SHARED_INCLUDED

// Dissolve_Alchemy.shader 的 4 个 Pass 共用此文件。
// 必须在 #include 了 URP 的 Core.hlsl 之后再 include（CBUFFER_START 宏来自 Core）。
//
// 作用：
//   1) 把所有 per-material 属性集中到 UnityPerMaterial CBUFFER
//      → 满足 SRP Batcher 条件①（属性入 CBUFFER）
//      → 4 个 Pass 共用同一份布局，天然满足条件②（各 Pass 布局一致）
//   2) 噪声函数只写一份，4 个 Pass 复用
//      → clip 结果天然一致，避免影子/法线/描边出现"幽灵残留"

// 材质常量
CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float  _DissolveAmount;
    float  _NoiseTiling;
    float  _NoiseScrollSpeed;
    float  _EdgeWidth;
    float4 _EdgeColorHot;
    float4 _EdgeColorCool;
    float  _EdgeIntensity;
    float  _DistortStrength;
    float  _DistortNoiseScale;
    float4 _HeatColor;
    float  _HeatIntensity;
    float4 _RimColor;
    float  _RimPower;
    float  _RimIntensity;
CBUFFER_END

// 程序化噪声
// 2D Hash：把 float2 坐标映射成伪随机方向向量
float2 _Hash2(float2 p)
{
    p = float2(dot(p, float2(127.1, 311.7)),
               dot(p, float2(269.5, 183.3)));
    return frac(sin(p) * 43758.5453);
}

// 梯度噪声 (Gradient Noise)：
//   在每个整数格点放一个随机方向向量
//   用到格点的 dot(grad, offset) 做双线性插值，输出范围 [-1, 1]
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
//   叠加 3 层不同频率/振幅的梯度噪声 → 有机状的云/岩石纹理
//   振幅减半、频率翻倍 = 每层细节更细但权重更小，输出约 [0, 1]
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

#endif // GOURMETLINE_DISSOLVE_SHARED_INCLUDED

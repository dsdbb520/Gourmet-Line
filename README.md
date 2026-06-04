# Gourmet-Line · 二次元炼金工房 NPR 渲染

> 基于 **Unity URP** 的非真实感（NPR）渲染作品集，参考《莱莎的炼金工房》的二次元中世纪炼金风格。
> 核心是一套**手写 HLSL 卡通渲染 Shader 库**（角色道具 / 环境 / 特效三类），配合管线级描边与屏幕级调色，构成完整的三层 NPR 渲染管线。

<!-- 顶部主图：建议放一段整体展示场景的 GIF/视频 -->
![Showcase Overview](docs/overview.gif)

---

## 项目概览

- **渲染管线**：Universal Render Pipeline (URP 14 / Unity 2022 LTS)
- **8 个自定义 HLSL Shader**：全部手写，统一的 Ramp 卡通光照 + Rim 边缘光 + Clip-Space 描边风格
- **三层 NPR 架构**
  - **物体层**：每个 Shader 的 `ForwardLit` 卡通光照 Pass + `Outline` 描边 Pass（溶解材质额外含 `ShadowCaster`）
  - **管线层**：基于深度/法线的屏幕空间描边 Renderer Feature
  - **屏幕层**：Bloom · Tonemapping · Lift/Gamma/Gain 色彩分级 · Vignette
- **程序化为主**：溶解噪声、符文粒子、宝石内部辉光等均为程序化生成，少依赖贴图
- **Houdini VAT 流体**：管道流体在 Houdini 解算后烘焙为顶点动画贴图（VAT），Unity 中 GPU 重建播放
- **运行时参数驱动**：关键参数对外暴露，由 C# 实时驱动（溶解进度循环、描边宽度、各材质参数）

---

## 一、道具 / 材质表现

### AnimeFood — 卡通角色材质

二次元角色/食物的基础卡通材质，统一全场景的光照语言。

**关键技术**：Ramp Shadow（`smoothstep` 软硬可调边界 + `step` 硬化投射阴影）· 伪次表面散射（背光透光）· Rim Light · Cel Specular（`step` 硬边 Blinn-Phong）· Clip-Space 法线膨胀描边（像素恒宽，不受透视影响）

### AlchemyMetal — 炼金金属
![AlchemyMetal](docs/metal.gif)

炼金炉、大锅、管件等金属部件，铸铁拉丝质感 + 炉底受热发光。

**关键技术**：Kajiya-Kay 各向异性高光（切线偏移 + `step` 硬边）· Matcap 风格化反射（视角空间法线 → Screen 混合）· 顶点色驱动边缘磨损 · World-Y 梯度受热 Emission（炉底橙红 → 顶部消散）

### Crystal — 晶体 / 宝石
![Crystal](docs/crystal.gif)

炼金原料宝石，穿透折射 + 棱镜色散 + 背光透色的通透质感。

**关键技术**：屏幕空间折射（采样 `_CameraOpaqueTexture` + 法线扰动 UV）· RGB 色散（三通道分别偏移，棱镜彩边）· Fresnel 控制中心/边缘透明度 · 背光透色 · 高 shininess 针点宝石高光 · 双角度 Matcap 相乘的内部辉光 · `sin(time)` Emission 脉冲

---

## 二、环境材质

### StoneFloor — 石板地面
![StoneFloor](docs/stone.gif)

工房地面/墙壁，免 UV 的三平面采样 + 自然苔藓覆盖。

**关键技术**：Triplanar Mapping（世界坐标三轴投影，按法线权重混合，无缝无拉伸）· 顶点色 G 通道驱动苔藓 mask（噪声斑块）· 裸石区湿润高光

### Wood — 木材
![Wood](docs/wood.gif)

架子/木箱等木质部件，暖木冷影的卡通冷暖分离。

**关键技术**：Detail Normal Map 叠加木纹细节法线 · 冷暖分离卡通调（阴影染冷紫棕，亮部暖黄）· 沿木纹方向的弱各向异性高光

### Fabric — 布料
![Fabric](docs/fabric.gif)

材料袋等织物，双向纤维交织的织物光泽。

**关键技术**：双层各向异性高光（经线 Warp 沿切线、纬线 Weft 沿副切线，分别计算后叠加）· Cel `step` 硬边化，与全局卡通风格统一

---

## 三、特效 / 动态

### Dissolve — 溶解 / 炼金反应
![Dissolve](docs/dissolve.gif)

原料投入炼金炉时的溶解消失特效，发光溶解边 + 热浪扭曲。

**关键技术**：FBM 程序化噪声（3 层梯度噪声叠加）+ `clip()` 溶解 · 发光溶解边（边界检测 + Hot/Cool 双色 Emission）· UV 热浪扭曲（独立噪声驱动，强度随溶解进度联动）· `ShadowCaster` 同步裁切（溶解后不残留阴影）· `_DissolveAmount` 对外暴露，由 C# 驱动循环

### RuneConveyor — 魔法符文传送带
![RuneConveyor](docs/rune.gif)

炼金工房的"物流系统"，流动符文阵 + 飞舞魔法粒子。

**关键技术**：符文 UV 定向流动 · 全局呼吸脉冲 · Voronoi 程序化魔法粒子（每细胞独立相位闪烁，不依赖 Particle System）· 无贴图时自动切换 Voronoi 边缘线生成程序符文阵

---

## 四、Houdini 流体仿真 · VAT

### Pipe Fluid — 管道流体（Vertex Animation Texture）
![Pipe Fluid VAT](docs/vat.gif)

在 Houdini 中模拟管道内流动的流体，通过 **VAT（顶点动画贴图）** 烘焙后在 Unity 中由 GPU 重建播放——展示 **DCC 仿真 → 引擎管线集成** 的完整链路。

**关键技术**
- **Houdini 流体模拟**：解算管道内液体流动，输出动画顶点序列
- **VAT 烘焙**（SideFX GameDev 导出）：将每帧顶点的位置/旋转烘焙进 `pos` / `rot` EXR 贴图，配合 `lookup` 贴图与 `col` 贴图
- **GPU 顶点重建**：Shader 按 VertexID + 时间采样 pos/rot 贴图，在顶点着色阶段还原动画——无骨骼、无逐帧网格，单次 Draw 即可播放复杂流体
- **Shader 内自动循环**：内置时间驱动，在 `Loop_Start ~ Loop_End` 帧间无缝循环
- **液体/玻璃材质表现**：半透明 + 折射感的液体外观

---

## 五、管线级 NPR

### Screen-Space Outline — 全屏风格化描边
![Screen-Space Outline](docs/outline.gif)

- **Screen-Space Outline**（Renderer Feature）：采样 `_CameraDepthTexture` 与 `_CameraNormalsTexture`，用边缘算子在屏幕空间计算轮廓，可处理物体间接触边缘与透明物体描边，作为物体级背面描边的补充。
- **后处理调色**：Bloom（仅 Emission > 1 发光）· Tonemapping · Lift/Gamma/Gain（阴影偏冷、高光偏暖的炼金色调）· Color Adjustments（高饱和卡通）· Vignette。

---

## 技术栈

| 类别 | 内容 |
|---|---|
| 引擎 / 管线 | Unity 2022 LTS · URP 14 |
| 着色 | 手写 HLSL · ShaderLab 多 Pass |
| NPR 技术 | Ramp/Cel Shading · Kajiya-Kay 各向异性 · Matcap · Fresnel · 屏幕空间折射/色散 · Triplanar · Clip-Space 描边 |
| 程序化 | FBM 噪声 · Voronoi · 程序化网格生成 |
| 工程 | C# 运行时参数驱动 · Editor 工具脚本 |
| DCC | Houdini 流体仿真 → VAT（顶点动画贴图）→ Unity GPU 重建播放 |

---

## 关键技术名词

`Ramp/Cel Shading` · `Kajiya-Kay Anisotropic` · `Matcap` · `Fresnel` · `Screen-Space Refraction & Dispersion` · `Triplanar Mapping` · `FBM` · `Voronoi` · `Clip-Space Outline` · `Vertex Color Mask` · `VAT (Vertex Animation Texture)`

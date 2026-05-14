# Gourmet-Line TA 作品集推进计划 v2

> **目标风格**：二次元中世纪炼金工房，参考《莱莎的炼金工房》渲染风格  
> **技术栈**：Unity URP + ShaderGraph + HLSL + Houdini (VAT / 程序化建模)  
> **作品集定位**：以 Shader + Houdini 技术为双核心，格子建造系统作为交互载体

---

## 关于 NPR 渲染的解释

**Gemini 说的没错，但需要更精确地理解 NPR（非真实感渲染）。**

你现在写的 CelShader 本身就是 NPR 的一种——NPR 是一个大类，  
卡通着色、水彩风、速写风都属于 NPR。问题不是"要不要加 NPR"，  
而是你的 NPR 渲染管线目前只完成了**第一层（物体级 Shader）**，  
还差**第二层（管线级 Renderer Feature）**和**第三层（屏幕级 Post-Process）**。

```
┌─────────────────────────────────────────────────────────┐
│  完整 NPR 渲染管线（三层架构）                            │
├───────────────┬─────────────────────────────────────────┤
│ 第三层        │ Post-Processing（屏幕空间）               │
│ 屏幕级        │ Bloom / Color Grading / Vignette         │
│               │ ← 让魔法物品发光、建立炼金工房色调         │
├───────────────┼─────────────────────────────────────────┤
│ 第二层        │ URP Renderer Feature（渲染管线注入）       │
│ 管线级        │ Screen-Space Outline（全屏描边）           │
│               │ Custom Shadow Ramp（阴影也卡通化）         │
│               │ Render Objects（多 Pass 材质覆盖）         │
├───────────────┼─────────────────────────────────────────┤
│ 第一层        │ 每个物体的 Shader（已有部分）              │
│ 物体级        │ Cel Shading / Ramp / Rim / Specular       │
│               │ 玻璃 / 液体 / VAT 流体 / 晶体 / 金属      │
└───────────────┴─────────────────────────────────────────┘
```

**所以接下来要做的就是把三层全部填满。**

---

## 总览路线图

```
Phase 0  NPR 管线架构         ← 补完第二、三层，搭好整体框架
Phase 1  物体级 Shader 库     ← 扩充材质种类（晶体/金属/溶解/符文...）
Phase 2  Houdini 流体效果     ← VAT 多类型（重点）+ 程序化建模
Phase 3  场景氛围重构         ← 光照 + 材质替换
Phase 4  英雄资产打磨         ← 核心道具 + 展示场景
Phase 5  作品集收尾           ← Showreel + 文档
```

---

## Phase 0：NPR 渲染管线架构

### Task 0.1 — URP Renderer Feature：Screen-Space Outline
**新建文件**：`Assets/Shader/RendererFeature/OutlineRendererFeature.cs`  
**优先级**：★★★★★  
**工作量**：~2~3小时  

**为什么需要**  
背面膨胀法是物体级描边，无法处理：
- 不同物体之间的边界（A物体挡住B物体的轮廓）
- 透明物体描边（玻璃瓶描边）
- 描边宽度随距离自动调整

Screen-Space Outline 基于深度图和法线图的差值，在全屏空间计算边缘，以上问题全部解决。

**具体要做**

- [ ] 创建自定义 `ScriptableRendererFeature`，在 AfterRenderingOpaques 注入描边 Pass
- [ ] 在描边 Pass 里采样 `_CameraDepthTexture` 和 `_CameraNormalsTexture`
- [ ] 用 Sobel / Roberts Cross 算子计算边缘强度
- [ ] 支持参数：`OutlineColor`, `OutlineThickness`, `DepthThreshold`, `NormalThreshold`
- [ ] 在 URP Asset 的 Renderer 列表里加入这个 Feature
- [ ] （可选）加入描边颜色随深度变化（近处深、远处浅，增加景深感）

**验收标准**  
关掉各物体 Shader 里的 Outline Pass，由全局 Feature 统一提供描边，物体之间的接触边缘也能显示描边。

---

### Task 0.2 — URP Renderer Feature：Shadow Ramp（阴影卡通化）
**新建文件**：`Assets/Shader/RendererFeature/ShadowRampFeature.cs`  
**优先级**：★★★☆☆  
**工作量**：~2小时  

**为什么需要**  
Unity 默认阴影是软阴影（物理正确），但 NPR 要求投影阴影也有卡通感——  
莱莎里地面上的阴影是硬边色块，不是渐变阴影。

**具体要做**

- [ ] 通过 Custom Shadow Sampling，把接收阴影时的 ShadowAtten 值过一遍 Ramp 曲线
- [ ] 或者更简单方式：在 CelShader 里的 `GetShadowAttenuation()` 返回值上做 `step(0.5, atten)` 硬化
- [ ] 暴露参数：`ShadowRampTex`（可用同一张 RampMap）

**验收标准**  
地面上的阴影是硬边色块，与物体本身的卡通着色风格统一。

---

### Task 0.3 — Post-Processing 完整配置
**文件**：`Assets/Settings/SampleSceneProfile.asset`  
**优先级**：★★★★☆  
**工作量**：~1小时  

- [ ] **Bloom**：Threshold 0.8，Intensity 0.4（只有 Emission > 1 的物体发光）
- [ ] **Color Grading**：
  - Lift（阴影）→ 偏蓝紫 `(0.95, 0.95, 1.05)`
  - Gamma（中间调）→ 偏暖 `(1.0, 0.98, 0.95)`
  - Gain（高光）→ 偏金黄 `(1.05, 1.02, 0.95)`
  - Saturation → +15（卡通风格需要高饱和）
- [ ] **Vignette**：Intensity 0.3，深棕色（烘托室内感）
- [ ] **（可选）Chromatic Aberration**：轻微色散，强化玻璃和魔法感

---

## Phase 1：物体级 Shader 库

### Task 1.1 — Cel Shader 补全三要素
**文件**：`Assets/Shader/AnimeFood_CelShaded.shader`  
**优先级**：★★★★★  
**工作量**：~2小时  

当前只有 RampShadow + 伪SSS，缺少：

- [ ] **Rim Light（边缘光）**
  - `rimIntensity = pow(1.0 - saturate(dot(N, V)), _RimPower)`
  - 莱莎配色：道具用暖橙 `(1.0, 0.55, 0.1)`，魔法物品用冷紫 `(0.6, 0.3, 1.0)`
  - 参数：`_RimColor`, `_RimPower`, `_RimIntensity`

- [ ] **Cel Specular（硬边高光）**
  - `H = normalize(L + V)`，`spec = step(_SpecThreshold, pow(dot(N,H), _SpecShininess))`
  - 参数：`_SpecColor`, `_SpecThreshold`, `_SpecShininess`

- [ ] **Ramp 边界 smoothstep 控制**
  - 参数：`_ShadowEdge`（控制明暗边界软硬，0=硬切，0.2=软过渡）

**验收标准**：球体上能看到清晰高光点 + 橙色边缘光，阴影边界可调。

---

### Task 1.2 — Cel Shader 描边改进（Clip-Space 膨胀）
**文件**：`Assets/Shader/AnimeFood_CelShaded.shader`（Outline Pass）  
**优先级**：★★★★☆  
**工作量**：~1小时  

- [ ] 把 Object Space 法线膨胀改为 Clip Space 膨胀（解决透视变形）
  ```hlsl
  float4 posCS = TransformObjectToHClip(IN.positionOS.xyz);
  float3 normalCS = normalize(TransformWorldToHClipDir(
      TransformObjectToWorldNormal(IN.normalOS)));
  posCS.xy += normalCS.xy * (_OutlineWidth * posCS.w * 0.01);
  OUT.positionCS = posCS;
  ```
- [ ] 加入描边宽度受相机距离控制（远处描边变窄，保持视觉一致）

注：Task 0.1 完成后这个 Pass 可以关闭，但 0.1 是可选项，所以两者并行推进。

---

### Task 1.3 — 药水瓶英雄 Shader（完整版）
**文件**：`Assets/Shader/SG_PotionFlask.shadergraph`  
**优先级**：★★★★★  
**工作量**：~4小时  

- [ ] **折射**：Scene Color Node + 法线扰动 UV
- [ ] **菲涅尔厚度感**：边缘透明、中心饱和
- [ ] **液面截断**：World Position Y 轴 step 截断，暴露 `_LiquidLevel` 参数
- [ ] **Emission 发光**：液体区域输出 Emission（> 1 触发 Bloom）
- [ ] **气泡滚动**：Panner Node 驱动气泡贴图在液体区域向上流动
- [ ] **液体晃动**（bonus）：用 `sin(Time * _WaveSpeed)` 倾斜液面法线

---

### Task 1.4 — 晶体 / 宝石 Shader（新）
**新建文件**：`Assets/Shader/SG_Crystal.shadergraph`  
**优先级**：★★★★☆  
**工作量**：~3小时  

炼金原材料的核心视觉——矿石、宝石类材料需要专门的 Shader。

- [ ] **内部折射**：Matcap 采样（用已有的 Matcaps AnimefanPostUP 贴图）模拟内部高光
- [ ] **色散（Dispersion）**：RGB 三通道分别偏移采样，产生棱镜彩色边缘
- [ ] **次表面透光**：`abs(dot(N, L))` 模拟晶体透光感（与伪SSS不同，晶体更锐利）
- [ ] **各向异性高光**：晶体沿裂缝方向有条状高光
- [ ] **Emission 脉冲**：`sin(Time) * 0.5 + 0.5` 驱动轻微脉冲发光（魔法矿石感）

---

### Task 1.5 — 金属 Shader（炼金炉/大锅）（新）
**新建文件**：`Assets/Shader/SG_AlchemyMetal.shadergraph`  
**优先级**：★★★☆☆  
**工作量**：~2小时  

炼金大锅、管道接头、炉架等金属部件。

- [ ] **各向异性高光**：用切线方向偏移 half vector，产生拉丝金属感（适合铸铁锅）
- [ ] **风格化金属反射**：Matcap 采样（比 Reflection Probe 更可控，更卡通）
- [ ] **边缘磨损**：顶点色驱动 AO 和磨损 mask，加深接缝和凹角
- [ ] **受热发光**（炉子专用）：从底部到顶部的 Y 轴 Emission Gradient（模拟被火烤热的金属）

---

### Task 1.6 — 溶解 / 炼金反应 Shader（新）
**新建文件**：`Assets/Shader/SG_Dissolve.shadergraph`  
**优先级**：★★★★☆  
**工作量**：~2小时  

当原材料被放入炼金炉，播放炼金反应时的视觉特效 Shader。

- [ ] **Noise 溶解**：用 Gradient Noise + `step(_DissolveAmount, noise)` 做边缘溶解
- [ ] **发光溶解边**：在溶解边缘叠加 Emission，`smoothstep` 控制宽度，颜色用炼金橙/紫
- [ ] **UV 扭曲**：溶解时用 Simple Noise 扰动 UV，产生热浪扭曲感
- [ ] **对外暴露 `_DissolveAmount` 参数**，由 C# 动画曲线控制（ProcessorMachine 完成时触发）

---

### Task 1.7 — 魔法符文 / 传送带 Shader（扩展已有）
**文件**：`Assets/Shader/SG_Conveyor.shadergraph`  
**优先级**：★★★☆☆  
**工作量**：~2小时  

把工厂传送带改造成魔法符文阵（炼金工房的"物流系统"）。

- [ ] **符文图案**：使用 UV Tile & Offset + 符文纹理（可用程序化 Voronoi 模拟）
- [ ] **流动方向**：Panner Node 沿传送方向滚动（已有 SG_Conveyor 应已实现）
- [ ] **发光脉冲**：Emission 随 Time 做波形动画（sin波）
- [ ] **边缘魔法粒子**：在 Shader 层用 Voronoi 点做闪烁粒子效果（不依赖 Particle System）

---

### Task 1.8 — 环境材质组（石板/木材/布料）（新）
**新建文件**：`Assets/Shader/SG_StoneFloor.shadergraph`, `SG_Wood.shadergraph`  
**优先级**：★★★☆☆  
**工作量**：~3小时  

炼金工房的地面、墙壁、架子。

- [ ] **石板地面**：Triplanar Mapping + 苔藓 mask（顶点色驱动）+ 湿润高光
- [ ] **木材架子**：Detail Normal Map 叠加木纹细节 + 卡通色调（暖棕）
- [ ] **布料材料袋**：双层各向异性（布料丝线方向高光） + Cel 化处理
- [ ] 以上材质保持与 CelShader 相同的 Rim Light / Shadow 风格，视觉统一

---

### Task 1.9 — Matcap 集成（已有贴图利用起来）
**资产**：`Assets/Matcaps AnimefanPostUP/`  
**优先级**：★★★☆☆  
**工作量**：~1小时  

你已经有一套 AnimefanPostUP 的 Matcap 贴图但尚未充分利用。

- [ ] 在晶体 Shader（1.4）和金属 Shader（1.5）里用 Matcap 采样做主反射
- [ ] Matcap 采样方式：把视角空间法线的 XY 分量映射到 UV：`matcapUV = normalVS.xy * 0.5 + 0.5`
- [ ] 测试哪些 Matcap 贴图适合哪类材质（光滑金属/磨砂/玻璃）

---

## Phase 2：Houdini 流体效果（重点）

> **VAT（Vertex Animation Texture）是你的核心技术亮点。**  
> 以下每个效果在 Houdini 里生成 VAT，在 Unity 里用对应的 ShaderGraph 播放。  
> 你已经建立了 `VAT_DynamicRemeshing.shadergraph` 的基础框架，可以在此基础上扩展。

---

### Task 2.1 — 大锅沸腾液体 VAT（完善已有）
**Houdini 文件**：扩展已有流体模拟  
**Unity Shader**：`Assets/Shader/VAT_DynamicRemeshing.shadergraph`  
**优先级**：★★★★★  
**工作量**：Houdini ~2小时 + Unity ~2小时  

- [ ] **Houdini**：FLIP Fluid 模拟大锅内沸腾，SOP 层导出 VAT（Rigid / Soft Body 模式）
- [ ] **验证 VAT 播放**：VertexID → UV 采样 pos.exr / rot.exr，Time Node 驱动帧
- [ ] **Unity Shader 加入**：
  - 液面泡沫 mask（World Y 高度 + Noise）
  - 液体颜色深度渐变（深处颜色深）
  - 液面 Emission（炼金液体发光）
  - 受热颜色变化（Time 驱动颜色从蓝→绿→橙，模拟炼金进度）

---

### Task 2.2 — 液体倾倒 VAT（新）
**Houdini 模拟**：FLIP Fluid 倾倒动画  
**Unity Shader**：新建 `Assets/Shader/VAT_LiquidPour.shadergraph`  
**优先级**：★★★★☆  
**工作量**：Houdini ~3小时 + Unity ~2小时  

炼金工房最标志性的动作——把药瓶里的液体倒进大锅里。

- [ ] **Houdini**：
  - 建一个倾斜的烧瓶 + 接收容器的场景
  - FLIP Fluid 模拟倾倒过程（重力 + 表面张力）
  - 导出 VAT，帧范围约 60~90 帧（约 2~3 秒动画）
- [ ] **Unity Shader**：
  - 液体本体：半透明 + 折射 + Emission
  - 落点水花：额外的 VAT 粒子飞溅层（可以是第二个 VAT Shader）
  - Loop 控制：C# 控制 `_CurrentFrame` 参数，炼金完成时触发

---

### Task 2.3 — 晶体生长 VAT（新）
**Houdini 模拟**：SOP Crystal Growth（L-System 或 SDF 增长）  
**Unity Shader**：新建 `Assets/Shader/VAT_CrystalGrowth.shadergraph`  
**优先级**：★★★★☆  
**工作量**：Houdini ~3小时 + Unity ~1.5小时  

炼金合成时，产物（晶体）从无到有逐渐"生长"出来。

- [ ] **Houdini**：
  - 用 SDF 融合（VDB Combine）或 L-System 模拟晶体逐步生长
  - 每一帧是不同的几何体（Rigid VAT 模式，每顶点存位置和旋转）
  - 也可以用 Point Deform SOP 做晶体裂开/生长动画
- [ ] **Unity Shader**：
  - VAT 基础播放（引用 Task 2.1 的 Shader 框架）
  - 结合 Task 1.4 的晶体材质（折射 + 色散 + 发光）
  - 生长前沿加 Emission 发光（生长"热"感）

---

### Task 2.4 — 粉末 / 颗粒物 VAT（新）
**Houdini 模拟**：Grain Solver（颗粒动力学）  
**Unity Shader**：新建 `Assets/Shader/VAT_Granular.shadergraph`  
**优先级**：★★★☆☆  
**工作量**：Houdini ~3小时 + Unity ~1.5小时  

炼金原材料（药粉、矿粉）倒入容器时的颗粒流动。

- [ ] **Houdini**：
  - Grain Solver 模拟粉末从瓶子倒入容器
  - 每个颗粒是一个点，Point Instancer 生成小球面
  - 导出 VAT（Sprite 模式，每个点存世界位置）
- [ ] **Unity Shader**：
  - 点状颗粒渲染（Billboard Quad 或 SDF 球体）
  - 颗粒颜色随高度/速度变化
  - 落地堆积时颗粒颜色变暗（模拟聚集阴影）

---

### Task 2.5 — 蒸汽 / 烟雾 VAT（新）
**Houdini 模拟**：Pyro Solver → VDB → Mesh  
**Unity Shader**：新建 `Assets/Shader/VAT_Steam.shadergraph`  
**优先级**：★★★★☆  
**工作量**：Houdini ~4小时 + Unity ~2小时  

大锅沸腾时从液面升起的蒸汽，炉火旁的热气。

- [ ] **Houdini**：
  - 小范围 Pyro 模拟（蒸汽，低速，低密度）
  - VDB 转 Mesh（SDF 等值面提取）
  - 导出 Soft Body VAT（顶点软变形）
- [ ] **Unity Shader**：
  - 半透明 + Alpha Fade（从底到顶逐渐消散）
  - Rim 发白（背光蒸汽效果）
  - 顶部 Emission 轻微（蒸汽带有魔法能量感）
  - 噪声扰动 UV（让蒸汽边缘不规则）

---

### Task 2.6 — 炼金爆炸特效 VAT（新）
**Houdini 模拟**：RBD + FLIP 联合模拟  
**Unity Shader**：新建 `Assets/Shader/VAT_AlchemyBurst.shadergraph`  
**优先级**：★★★☆☆  
**工作量**：Houdini ~4小时 + Unity ~2小时  

炼金失败时的爆炸效果，或者炼金成功时的光爆。

- [ ] **Houdini**：
  - 小型 RBD 碎片爆炸 + 液体飞溅（FLIP）
  - RBD 部分用 Rigid VAT 导出
  - 液体飞溅部分用独立的 FLIP VAT 导出
- [ ] **Unity Shader**：
  - RBD 碎片：与碎片旋转同步 + 发光溶解（结合 Task 1.6 的 Dissolve Shader）
  - 液体飞溅：折射 + 发光轨迹
  - 爆炸中心：Bloom 过曝（Emission 极大值，触发强 Bloom）

---

### Task 2.7 — 玻璃水滴流淌 VAT（新）
**Houdini 模拟**：FLIP Fluid 薄膜 / 水滴  
**Unity Shader**：新建 `Assets/Shader/VAT_Droplets.shadergraph`  
**优先级**：★★★☆☆  
**工作量**：Houdini ~2小时 + Unity ~1.5小时  

药水瓶外壁和玻璃管道上的水滴流淌，增加细节质感。

- [ ] **Houdini**：
  - 在曲面上模拟水滴滑落（Grain Solver 或 Shelf 的 Droplets 工具）
  - 导出 VAT（Sprite 点模式）
- [ ] **Unity Shader**：
  - 水滴本身半透明折射（微型药水瓶玻璃感）
  - 流过区域留下湿润 trail（Normal Map 扰动 + 稍高高光）

---

### Task 2.8 — 程序化管道网络（Houdini 建模）（新）
**Houdini 文件**：新建程序化管道 HDA  
**优先级**：★★★☆☆  
**工作量**：Houdini ~3小时  

用 Houdini 生成炼金工房里的玻璃管道网络，而不是手动摆管道。

- [ ] 用 Curve SOP 定义管道路径
- [ ] Sweep SOP 生成管道几何体（可配置内径/外径/弯头精度）
- [ ] 导出到 Unity，应用 `SG_PipeGlass` Shader
- [ ] 管道内部用 `SG_DynamicLiquid` 或 `VAT_LiquidPour` 显示流动液体

---

### Task 2.9 — 程序化晶石簇（Houdini 建模）（新）
**Houdini 文件**：新建晶石 HDA  
**优先级**：★★★☆☆  
**工作量**：Houdini ~2小时  

作为场景装饰和原材料堆放的晶石簇，程序化生成不同形态。

- [ ] 用 SOP 模拟晶体生长（Voronoi Fracture + Clip SOP）
- [ ] 参数化控制：晶石数量、高度、粗细、角度随机范围
- [ ] 导出多个变体，应用 Task 1.4 的晶体 Shader

---

### Task 2.10 — 程序化藤蔓沿管道生长（Houdini 建模）（新）
**Houdini 文件**：新建藤蔓 HDA  
**优先级**：★★☆☆☆  
**工作量**：Houdini ~3小时  

让炼金工房有一种年代久远、自然生长的感觉，藤蔓爬在玻璃管道外面。

- [ ] 用 L-System 或 Vellum Wire Solver 模拟藤蔓生长
- [ ] 沿管道几何体表面延伸（用 UVs 作为生长参考）
- [ ] 叶片用 Scatter SOP 分布，朝向法线方向
- [ ] Unity 里用简单 Lit 材质（可加 Subsurface 透光感）

---

## Phase 3：场景氛围重构

### Task 3.1 — 光照系统重构
**优先级**：★★★★☆  
**工作量**：~1.5小时  

```
主光（Directional）：暖黄  #FFD4A0   强度 1.2，角度 30° 侧光
环境（Environment）：蓝紫  #3A2F5E   Ambient 0.3
炉火点光（炼金炉） ：橙红  #FF6B35   Range 4，Intensity 3
药水点光（药水瓶） ：魔法紫 #9B59B6  Range 2，Intensity 2
```

- [ ] 调整 Directional Light 颜色和角度
- [ ] Environment Lighting 切换为 Gradient 模式，上方冷下方暖
- [ ] 在 ProcessorMachine 附近放 Orange Point Light
- [ ] 在 Spawner / 药水瓶附近放 Purple Point Light
- [ ] 配置 Reflection Probe（供玻璃和金属材质使用）

---

### Task 3.2 — 地面/环境材质替换
**优先级**：★★★☆☆  
**工作量**：~2小时  

- [ ] 地面换成 Task 1.8 的石板地面 Shader
- [ ] 场景边界加入木质墙壁/架子模型
- [ ] 程序化晶石簇（Task 2.9）放在场景角落作为装饰
- [ ] 传送带换成 Task 1.7 的魔法符文传送带效果

---

## Phase 4：英雄资产打磨

### Task 4.1 — 炼金道具模型组
**优先级**：★★★★☆  
**工作量**：~4~6小时  

| 道具 | 来源建议 | Shader |
|---|---|---|
| 蒸馏烧瓶 (Alembic) | Sketchfab CC0 或 Blender 建模 | SG_PotionFlask |
| 炼金大锅 (Cauldron) | Sketchfab CC0 + 修改 | SG_AlchemyMetal + VAT |
| 玻璃管道网络 | Houdini 程序化（Task 2.8） | SG_PipeGlass |
| 原材料·宝石 | Houdini 晶石簇（Task 2.9） | SG_Crystal |
| 原材料·粉末 | 简单几何体 | SG_Granular VAT |
| 炼金传送带 | 现有 Conveyor 改造 | SG_Conveyor（符文版） |

---

### Task 4.2 — C# 与 Shader 联动（炼金动画触发）
**文件**：`Assets/Script/ProcessorMachine.cs`  
**优先级**：★★★☆☆  
**工作量**：~2小时  

让视觉特效与游戏逻辑状态同步：

- [ ] ProcessorMachine 进入 Processing 状态 → 触发大锅 VAT 加热（颜色变化）
- [ ] ProcessorMachine 完成 → 触发 Dissolve Shader 动画（原料消失）
- [ ] ProcessorMachine 完成 → 触发液体倾倒 VAT 动画
- [ ] 产物生成 → 触发晶体生长 VAT 动画

实现方式：`MaterialPropertyBlock` 设置 `_DissolveAmount` / `_CurrentFrame` 等 Shader 参数。

---

## Phase 5：作品集收尾

### Task 5.1 — 展示场景布置
**优先级**：★★★★☆  
**工作量**：~2小时  

- [ ] 固定一个展示相机位置（不依赖格子系统交互）
- [ ] 在 4×4 格子核心区摆满炼金道具，展示所有材质
- [ ] 加 Timeline 动画：相机自动在几个机位切换，循环展示
- [ ] 确保所有动态效果（VAT / 符文 / 发光）在展示相机下都可见

---

### Task 5.2 — Showreel 录制清单
**优先级**：★★★★★  

每段约 10~15 秒，剪辑总时长 90~120 秒。

| 片段编号 | 内容 | 展示的技术点 | 建议拍法 |
|---|---|---|---|
| 01 | 药水瓶 360° 绕轨 | 折射 + 液面 + Bloom 发光 | 慢速绕轨，背景黑 |
| 02 | 大锅沸腾液体 | VAT 流体 + 泡沫 + 颜色变化 | 俯视 45° 近景 |
| 03 | 液体倾倒过程 | FLIP VAT 倾倒 + 飞溅 | 侧面跟拍 |
| 04 | 晶体生长动画 | Crystal VAT + 晶体 Shader | 近景正面 |
| 05 | 炼金爆炸/成功特效 | RBD VAT + Dissolve + Bloom | 广角 + 慢动作 |
| 06 | 蒸汽 / 烟雾 | Pyro VAT + 半透明 Shader | 逆光拍（背光蒸汽通透） |
| 07 | 玻璃管道流体 | PipeGlass + DynamicLiquid | 管道特写侧视 |
| 08 | 程序化晶石簇 | Houdini 建模 + Crystal Shader | 慢速推进 |
| 09 | Cel Shading 对比 | 有/无 Rim Light / Specular 切换 | 静帧 + 参数动画 |
| 10 | Screen-Space Outline 对比 | NPR 管线效果 | 分屏对比 |
| 11 | 整体场景动态 | 光照氛围 + 所有效果同时运转 | 大景推进 |

---

### Task 5.3 — 技术文档整理
**优先级**：★★★☆☆  

- [ ] 每个 Shader 写一份 1 页的技术说明（思路 + 参数 + 关键节点截图）
- [ ] ShaderGraph 截图（展示节点连接逻辑）
- [ ] Houdini 工程截图（展示 SOP 网络）
- [ ] 整理到 Notion / ArtStation / 个人网站

---

## 完整任务总览

| Phase | 任务 | 优先级 | 技术类别 | 预估工时 |
|---|---|---|---|---|
| 0.1 | Screen-Space Outline Renderer Feature | ★★★★★ | 管线/C# | 2~3h |
| 0.2 | Shadow Ramp 卡通化 | ★★★☆☆ | 管线/Shader | 2h |
| 0.3 | Post-Processing 完整配置 | ★★★★☆ | 后处理 | 1h |
| 1.1 | Cel Shader 补全 Rim + Specular | ★★★★★ | HLSL | 2h |
| 1.2 | Outline Clip-Space 改进 | ★★★★☆ | HLSL | 1h |
| 1.3 | 药水瓶 Shader 完整版 | ★★★★★ | ShaderGraph | 4h |
| 1.4 | 晶体/宝石 Shader | ★★★★☆ | ShaderGraph | 3h |
| 1.5 | 金属 Shader（炼金炉） | ★★★☆☆ | ShaderGraph | 2h |
| 1.6 | 溶解/炼金反应 Shader | ★★★★☆ | ShaderGraph | 2h |
| 1.7 | 魔法符文传送带 Shader | ★★★☆☆ | ShaderGraph | 2h |
| 1.8 | 环境材质组（石板/木材/布料） | ★★★☆☆ | ShaderGraph | 3h |
| 1.9 | Matcap 集成 | ★★★☆☆ | ShaderGraph | 1h |
| 2.1 | 大锅沸腾 VAT（完善） | ★★★★★ | Houdini+SG | 4h |
| 2.2 | 液体倾倒 VAT | ★★★★☆ | Houdini+SG | 5h |
| 2.3 | 晶体生长 VAT | ★★★★☆ | Houdini+SG | 4.5h |
| 2.4 | 粉末颗粒 VAT | ★★★☆☆ | Houdini+SG | 4.5h |
| 2.5 | 蒸汽/烟雾 VAT | ★★★★☆ | Houdini+SG | 6h |
| 2.6 | 炼金爆炸特效 VAT | ★★★☆☆ | Houdini+SG | 6h |
| 2.7 | 玻璃水滴流淌 VAT | ★★★☆☆ | Houdini+SG | 3.5h |
| 2.8 | 程序化管道网络 | ★★★☆☆ | Houdini建模 | 3h |
| 2.9 | 程序化晶石簇 | ★★★☆☆ | Houdini建模 | 2h |
| 2.10 | 程序化藤蔓 | ★★☆☆☆ | Houdini建模 | 3h |
| 3.1 | 光照系统重构 | ★★★★☆ | 场景 | 1.5h |
| 3.2 | 地面/环境材质替换 | ★★★☆☆ | 场景 | 2h |
| 4.1 | 炼金道具模型组 | ★★★★☆ | 建模/整合 | 6h |
| 4.2 | C# 与 Shader 联动 | ★★★☆☆ | C# | 2h |
| 5.1 | 展示场景布置 | ★★★★☆ | 场景 | 2h |
| 5.2 | Showreel 录制 | ★★★★★ | 后期 | 4h |
| 5.3 | 技术文档整理 | ★★★☆☆ | 文档 | 3h |

**总预估工时：约 90~100 小时**

---

## 推荐开始顺序（按依赖和回报排列）

```
第1步  Task 0.3  Post-Processing 配置     ← 30分钟，整体氛围立刻改变
第2步  Task 1.1  Cel Shader 补全          ← 核心 NPR 技术，基础中的基础
第3步  Task 3.1  光照重构                 ← 氛围建立，后续截图都好看
第4步  Task 1.3  药水瓶 Shader 完整版     ← 作品集最核心展示资产
第5步  Task 2.1  大锅 VAT 完善            ← 技术亮点，有 Houdini 基础
第6步  Task 1.4  晶体 Shader              ← 配合 VAT 晶体生长效果
第7步  Task 2.2  液体倾倒 VAT             ← 最好看的单个 Houdini 效果
第8步  Task 0.1  Screen-Space Outline     ← 管线级提升，视觉质感跨级
第9步  Task 2.5  蒸汽 VAT                 ← 场景氛围关键
第10步 Task 2.3  晶体生长 VAT             ← 与炼金逻辑直接联动
第11步 剩余任务（按兴趣排列）
```

---

## 快速参考：莱莎渲染关键参数

```
Ramp Shadow    — 两段式，边界 smoothstep 宽度 0.05~0.1
Rim Light      — Power 2.5~4，暖橙 #FF8C42 / 魔法紫 #9B59B6
Cel Specular   — Shininess 60~120，step 硬切（无平滑过渡）
Outline        — Clip Space 膨胀，视觉宽度 0.5~1.5px 恒定
Bloom          — Threshold 0.8，Emission > 1 才发光
Color Grading  — 阴影蓝紫 / 中调暖 / 高光金黄，Saturation +15
VAT 帧率       — 通常 24fps，Houdini 导出时设置好帧数范围
晶体色散       — RGB 偏移量约 0.005~0.015（太大会失真）
```

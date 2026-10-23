# Gourmet-Line TA 作品集推进计划 v3

> **目标风格**：二次元中世纪炼金工房，参考《莱莎的炼金工房》渲染风格  
> **技术栈**：Unity URP + HLSL + Houdini (VAT / 程序化建模)  
> **作品集定位**：渲染 TA（风格化 NPR 渲染）为主轴，Houdini VAT 为差异化亮点  
> **当前状态**：Phase 0/1/5 基本完成，已发布作品集初版；Phase 2 是下一个重点

---

## NPR 三层渲染管线（架构说明）

```
┌─────────────────────────────────────────────────────────┐
│  完整 NPR 渲染管线（三层架构）                            │
├───────────────┬─────────────────────────────────────────┤
│ 第三层 ✅     │ Post-Processing（屏幕空间）               │
│ 屏幕级        │ Bloom / Color Grading / Vignette         │
├───────────────┼─────────────────────────────────────────┤
│ 第二层 ✅     │ URP Renderer Feature（渲染管线注入）       │
│ 管线级        │ Screen-Space Outline（法线+深度边缘检测）  │
│               │ DepthNormals Pass（全部 Cel Shader 已补） │
├───────────────┼─────────────────────────────────────────┤
│ 第一层 ✅     │ 8 个自定义 HLSL Shader                   │
│ 物体级        │ Cel / 金属 / 晶体 / 溶解 / 符文 / 环境   │
└───────────────┴─────────────────────────────────────────┘
```

---

## 总览路线图

```
Phase 0  NPR 管线架构     ✅ 完成
Phase 1  物体级 Shader 库 ✅ 完成（8个HLSL Shader）
Phase 2  Houdini VAT     🔲 进行中（管道流体已做，其余待做）← 当前重点
Phase 3  场景氛围重构     🔲 未开始
Phase 4  英雄资产打磨     🔲 未开始
Phase 5  作品集收尾       ✅ 初版完成（README + GIF + 展示场景）
```

---

## Phase 0 & 1：已完成内容速览

### Phase 0 — NPR 管线架构 ✅

| 任务 | 实现内容 | 关键技术 |
|---|---|---|
| Screen-Space Outline | `OutlineRendererFeature.cs` + `ScreenSpaceOutline.shader` | 法线边缘检测 / Blit 后处理 / Always Included Shaders（build修复）|
| DepthNormals Pass | 全部 8 个 Cel Shader 补写 | 让全局描边能描出内部折角/物体交界 |
| Post-Processing | 接入已有 `SampleSceneProfile.asset` | Bloom / Tonemapping / LiftGammaGain / Vignette |

### Phase 1 — 物体级 Shader 库 ✅

| Shader | 核心技术点 |
|---|---|
| AnimeFood（卡通基础） | Ramp卡通阴影 / 伪SSS / Rim / Cel硬边高光 / Clip-Space描边 |
| AlchemyMetal（金属） | Kajiya-Kay各向异性高光 / Matcap反射 / 顶点色磨损mask / World-Y受热发光 |
| Crystal（晶体） | 屏幕空间折射(CameraOpaqueTexture) / RGB三通道色散 / Fresnel透明度 / 背光透色 / 内部辉光 / sin(Time)脉冲 |
| Dissolve（溶解） | FBM 3层梯度噪声 / clip()溶解 / 发光溶解边双色渐变 / UV热浪扭曲 / ShadowCaster同步clip |
| RuneConveyor（符文） | UV定向流动 / sin(Time)全局脉冲 / Voronoi程序粒子（不依赖ParticleSystem）|
| StoneFloor（石板） | Triplanar三平面映射 / 顶点色G通道苔藓mask / 湿润高光 |
| Wood（木材） | Detail Normal Map叠加木纹 / 冷暖分离卡通调 / 弱各向异性木纹高光 |
| Fabric（布料） | 双层各向异性（经线Warp+纬线Weft）/ Cel step硬边化 |

**遗留未完成**：
- [ ] `SG_PotionFlask`（药水瓶完整版）：液面截断 / 气泡 / 晃动——有框架但未完善，Phase 4 英雄资产时一起做
- [ ] Stone Triplanar 改为**物体空间**采样——当前世界空间导致旋转时纹理滑动，改动约 5 行 HLSL

---

## Phase 2：Houdini VAT 效果（当前重点）🔲

> **为什么这是最重要的**：
> 你的渲染 Shader 质量已经达到能投简历的水平，但一份"只会写 Shader"的 TA 作品集和"会 Shader + 会 Houdini 并打通引擎"的作品集，在面试官眼里是完全不同的量级。  
> 目前只有管道流体一个 VAT，面试官大概率会追问"你用 Houdini 只做了这一种？"。把 VAT 的**广度**补上，是现在性价比最高的投入。

---

### ✅ 已完成：管道流体 VAT
- FLIP 流体在玻璃管道内流动，SideFX GameDev VAT 导出
- pos/rot/col/lookup EXR 贴图，Shader 内置时间自动循环（Loop 40~80帧）
- build 修复：Vertex Compression 关闭 / VATBoundsExpander 修复视锥剔除

---

### Task 2.2 — 液体倾倒 VAT ⭐⭐⭐⭐⭐（最高优先）

**为什么先做这个**  
炼金工房最标志性的视觉动作——把药瓶里的液体倒进大锅里。这个效果：
- 视觉冲击力最强，录出来最好看
- 能复用现有资产（药水瓶 FBX + Crystal/PotionFlask Shader）
- 证明你能设计一个完整的**模拟 → 导出 → 引擎播放 → 材质表现**的链路，而不仅是"接入了一个现成 VAT"

**Houdini 端**
- [ ] 搭建场景：倾斜的烧瓶 + 接收容器（大锅或烧杯），导入现有药水瓶 FBX 作参考
- [ ] FLIP Fluid 模拟倾倒过程，关键参数：`Surface Tension 0.2`（液体有粘性）/ `Viscosity 0.05`（不过稠）/ `Gravity -9.8`
- [ ] 注意帧数：约 60~90 帧（2~3 秒），太短看不出流动，太长文件偏大
- [ ] 用 **SideFX Labs VAT** 导出（Soft Body 模式），勾选导出 Color（可存额外数据如速度）
- [ ] 导出设置：`Texture Format EXR` / `关 Mip` / `关 sRGB` / `不压缩`（pos/rot 是数据不是颜色）

**Unity 端**
- [ ] 新建 `VAT_LiquidPour.shadergraph`，复用管道流体 VAT 的基础采样框架（VertexID → UV 采样 pos 贴图）
- [ ] 液体材质：半透明 + 屏幕空间折射 + Emission（发光颜色参数化，炼金液体可以是蓝/绿/橙）
- [ ] 落点水花：可以是同一套 VAT 的后半段粒子飞溅，或单独录一个小范围 Grain VAT 叠加
- [ ] C# 驱动：`DissolveLoop.cs` 改为通用的 `VATPlayer.cs`，暴露 `PlayOnce()` 方法，让 ProcessorMachine 完成时触发

**验收标准**  
从烧瓶到容器，液体形态自然（有弧度、有飞溅），材质有折射感和发光感，可以 C# 触发一次性播放。

---

### Task 2.3 — 晶体生长 VAT ⭐⭐⭐⭐

**为什么做**  
你现有的 Crystal Shader 已经有折射 + 色散 + 内部辉光 + 脉冲发光，视觉效果相当完整。但它现在只是一个**静态宝石**——加上 VAT 生长动画之后，就变成了"炼金合成完成时，产物从无到有逐渐生长出来"的完整演示，技术链路价值大幅提升。

**Houdini 端**
- [ ] 方案 A（推荐）：**VDB Combine** 生长。建若干晶体柱的 SDF，用时间驱动逐步合并 → 等值面提取 → 每帧输出不同形状的 Mesh
- [ ] 方案 B：**L-System** 长出树枝状晶簇，控制每帧的 L-System 迭代步数
- [ ] 注意：VAT 模式选 **Rigid**（每顶点存位置 + 旋转），顶点数在整个动画序列里必须固定
- [ ] 帧数约 48~60 帧（2秒），生长完毕后可静止几帧再循环

**Unity 端**
- [ ] 新建 `VAT_CrystalGrowth.shadergraph`，基础 VAT 播放框架与 2.2 相同
- [ ] 直接复用 `Mat_Crystal` 材质（不需要新写 Shader，VAT 只改变几何，表面材质不变）
- [ ] 生长"前沿"加 Emission：用 VAT 速度通道（或 World Y 高度渐变）做一条发光带，模拟生长热感
- [ ] C# 端：生长动画只播一次（OnCompleted 触发），不循环

**验收标准**  
炼金合成完成 → 晶体从地面慢慢生长出来 → 长成后 Crystal Shader 的折射/色散/脉冲全部可见。

---

### Task 2.5 — 蒸汽 / 烟雾 VAT ⭐⭐⭐⭐

**为什么做**  
当前展示场景"太干净"——有发光的材质和流动的符文，但缺少体积感的烟雾/蒸汽，显得像在展示孤立的材质球而不是一个**炼金工房**。蒸汽是把所有效果"粘合"成一个有氛围场景的关键。

**Houdini 端**
- [ ] 新建小范围 **Pyro Solver** 场景，从一个圆形 Volume Source 向上发射（模拟大锅液面蒸发）
- [ ] 关键参数：低 Velocity（蒸汽缓慢上升）/ 低 Temperature（不是火焰）/ 高 Dissipation（快速消散）
- [ ] Pyro 解算完成后：**VDB → Mesh**（SDF 等值面提取，`VDB Convert SOP`），得到每帧的 Mesh 序列
- [ ] 导出 **Soft Body VAT**（顶点在原始位置附近形变，而非刚体旋转），帧数 48~60 帧做无缝循环

**Unity 端**
- [ ] 新建 `VAT_Steam.shadergraph`
- [ ] 关键效果：
  - `Alpha Fade`：从底部（不透明）到顶部（完全透明）的世界空间高度渐变
  - `Rim 发白`：背光时蒸汽边缘出现白色散射感（`saturate(-dot(N,L))` 乘以浅蓝白色）
  - `噪声扰动 UV`：FBM 噪声驱动 UV 偏移，让蒸汽边缘"沸腾"而不是硬切
  - `顶部轻微 Emission`：蒸汽带魔法能量的感觉，颜色与药水颜色呼应
- [ ] 渲染设置：半透明队列 / ZWrite Off / 双面渲染

**验收标准**  
大锅上方有持续的半透明蒸汽缓慢上升消散，逆光时边缘有白色散射感，与下方沸腾液体（Task 2.1）搭配。

---

### Task 2.1 — 大锅沸腾液体 VAT ⭐⭐⭐

**为什么排在 2.5 后面**  
蒸汽的场景氛围价值更大；大锅沸腾虽然也重要，但可以先做更有视觉冲击的倾倒（2.2），大锅留后面做时正好配合 Phase 3 的场景重构。

**Houdini 端**
- [ ] FLIP Fluid 模拟大锅内部沸腾，注意**边界条件**：需要一个碗形的 Collision Object（大锅形状）限制液体
- [ ] 模拟参数：`Viscosity 0.3`（炼金液体偏粘稠）/ `Surface Tension 0.15` / 加若干上升气泡的 Volume Source
- [ ] 导出 **Soft Body VAT**，帧数 60~80 帧，设好 Loop 点保证无缝循环
- [ ] 额外导出：液面泡沫的单独粒子 VAT（Sprite 模式），与主体液面分开渲染

**Unity 端**
- [ ] 液体颜色参数化：暴露 `_LiquidColorCold`（蓝绿）/ `_LiquidColorHot`（橙红），用 `sin(Time * 0.5)` 缓慢过渡
- [ ] 液面深度渐变：越深颜色越深（World Y 高度映射到颜色）
- [ ] 液面 Emission：沸腾气泡周围加轻微发光（触发 Bloom）
- [ ] C# 联动：ProcessorMachine 进入 Processing 状态时启动颜色变化动画

**验收标准**  
大锅内液体持续沸腾，液面有气泡涌动，颜色随炼金进度从冷色→暖色渐变。

---

### 其余 VAT（后续按兴趣/时间选做）

| Task | 内容 | 优先级 | 工作量 | 备注 |
|---|---|---|---|---|
| 2.4 | 粉末颗粒 VAT（Grain Solver） | ★★★☆☆ | ~5h | 原材料投入大锅时的粉末落下 |
| 2.6 | 炼金爆炸特效（RBD+FLIP） | ★★★☆☆ | ~8h | 炼金失败特效，配合 Dissolve Shader |
| 2.7 | 玻璃水滴流淌 | ★★★☆☆ | ~4h | 药水瓶外壁质感细节 |
| 2.8 | 程序化管道网络（Houdini HDA） | ★★★☆☆ | ~3h | Curve + Sweep SOP，无需手动摆管道 |
| 2.9 | 程序化晶石簇 | ★★★☆☆ | ~2h | 场景装饰，Voronoi Fracture + Clip |
| 2.10 | 程序化藤蔓 | ★★☆☆☆ | ~3h | L-System / Vellum Wire，氛围装饰 |

---

## Phase 3：场景氛围重构 🔲

> **前提**：Phase 2 至少完成 Task 2.2 + 2.5，场景有了倾倒流体和烟雾之后，才值得花时间重构氛围——否则重构之后还得再改。

**当前问题**  
现有 ShowcaseScene 是一排材质球展台，适合作品集初版展示，但**不像一个场景**。要让截图更有代入感，需要一个有氛围的"炼金工房角落"——哪怕只有 4×4 格子大。

### Task 3.1 — 光照系统重构

**为什么重要**  
光照是氛围最直接的来源。现在是中性展示灯（一主一补），改成炼金工房专属光照之后所有材质的质感都会提升一个档次。

```
主光（Directional） 暖黄  #FFD4A0   强度 1.2，从侧上方 35°
环境（Ambient）     蓝紫  #3A2F5E   Ambient Intensity 0.3（Trilight 模式）
炉火点光           橙红  #FF6B35   Range 4m，Intensity 3，放在大锅下方
药水点光           魔法紫 #9B59B6  Range 2m，Intensity 2，放在烧瓶旁
```

- [ ] 调整 Directional Light 颜色 + 角度
- [ ] Environment Lighting 切换为 **Gradient 模式**（上方冷蓝 / 赤道中性 / 地面暖橙）
- [ ] 在大锅下方放 Orange Point Light（模拟炉火）
- [ ] 在药水瓶旁放 Purple Point Light（魔法光源，触发 Crystal/Glass 折射颜色变化）
- [ ] 配置 **Reflection Probe**（供 AlchemyMetal 的 Matcap 和 Crystal 的折射使用，即使场景简单也要有）

### Task 3.2 — 环境材质替换

把 ShowcaseScene 里的中性背景换成炼金工房材质。

- [ ] 地面换成 `StoneFloor` Shader（已有 Triplanar + 苔藓）
- [ ] 背景墙换成 `Wood` Shader 覆盖的木质架子模型（Sketchfab CC0 即可）
- [ ] 传送带换成 `RuneConveyor` 材质（已做，接上去就行）
- [ ] 场景角落放程序化晶石簇（Task 2.9 完成后）
- [ ] 整体后处理微调：Vignette 深棕色 / Saturation 稍微再拉高一点（炼金工房色调偏饱和）

---

## Phase 4：英雄资产打磨 🔲

> **为什么重要**  
> 当前展示物全是球体/立方体/程序化宝石。面试官看到会问：**"你的 Shader 在真实角色或道具模型上能正常工作吗？"**  
> Phase 4 的目标就是用**真实 CC0 模型**配上你的材质，让作品集从"一排材质球"升级为"炼金工房道具展示"。

### Task 4.1 — 核心道具模型组

找 Sketchfab CC0 模型（或 Blender 简单建），关键是**有 UV、有切线、法线合理**。

| 道具 | 来源建议 | 挂的 Shader | 配合的 VAT |
|---|---|---|---|
| **炼金大锅**（Cauldron） | Sketchfab CC0 搜 "cauldron" | AlchemyMetal（炉底发光） | 大锅沸腾（Task 2.1） |
| **蒸馏烧瓶**（Alembic Flask） | Sketchfab CC0 / Blender 建 | SG_PotionFlask（折射+液面） | 液体倾倒（Task 2.2）触发 |
| **宝石/矿石簇** | Houdini 晶石簇（Task 2.9） | Crystal（折射+色散+脉冲） | 晶体生长（Task 2.3） |
| **玻璃管道网络** | Houdini 程序化（Task 2.8） | SG_PipeGlass | 管道流体（已有）|

**接入步骤（以大锅为例）**：
- [ ] 导入 FBX，检查 UV / 切线（AlchemyMetal 需要 Tangent 做各向异性）
- [ ] 检查顶点色：磨损效果靠顶点色 R 通道，导入后先刷白（=新鲜金属）
- [ ] 在 Blender 里给凹角 / 边缘刷黑色顶点色（=磨损）
- [ ] 调整 `_HeatYBottom` / `_HeatYTop` 到模型实际的 World Y 范围
- [ ] 放到 ShowcaseScene 替换原来的程序化圆柱

### Task 4.2 — 药水瓶完整 Shader（`SG_PotionFlask`）

配合真实烧瓶模型，把之前留着的 ShaderGraph 补完。

**为什么没在 Phase 1 做**：没有真实瓶子模型时做这个意义不大，现在有了模型一起做。

- [ ] **折射**：`Scene Color Node` + 法线扰动 UV（已有框架，复用 Crystal 的思路）
- [ ] **液面截断**：World Position Y 轴做 `step`，暴露 `_LiquidLevel`（0~1），C# 可驱动"液体减少"
- [ ] **菲涅尔**：边缘透明（像玻璃）/ 中心半透明（看到里面液体）
- [ ] **Emission**：液体区域输出 Emission > 1 触发 Bloom
- [ ] **气泡滚动**：`Panner Node` 驱动气泡贴图在液体区域向上流动（可以找一张气泡贴图或程序化生成）
- [ ] 参数化液体颜色：暴露 `_LiquidColor`，不同药水换颜色

### Task 4.3 — C# 与 Shader 联动（炼金动画序列）

把上面做好的 VAT 和 Shader 用 C# 串成一个有时序的炼金动画，演示**TA 写工具/驱动 Shader 的能力**。

```
放入原料
  → 原料播放 Dissolve 溶解消失（_DissolveAmount 0→1）
  → 大锅开始沸腾 VAT + 颜色从冷→热
  → 蒸汽 VAT 启动
  → 播放液体倾倒 VAT（如果有烧瓶）
  → 产物：晶体生长 VAT 从中心向上长出
  → 晶体 Crystal Shader 脉冲 Emission 加强
```

实现方式：
- [ ] 写 `AlchemySequencer.cs`，用 `Coroutine` 按时序调用各 Shader 参数
- [ ] 统一用 `MaterialPropertyBlock`（不实例化材质，对 GC 友好，这是面试考点）
- [ ] 在面板里加一个"触发炼金"按钮，方便录制演示

---

## Phase 5：作品集收尾（持续迭代）

### ✅ 已完成（初版）
- [x] Editor 一键生成展示场景（`ShowcaseSceneBuilder.cs`）
- [x] 运行时：Turntable / DissolveLoop / FlyCamera / 分页参数面板（四Tab）
- [x] GitHub README（10 张 GIF，中文作品集叙述）
- [x] VAT 接入展示场景，build 修复（Vertex Compression / VATBoundsExpander）

### 🔲 随 Phase 2/3/4 进度更新

- [ ] 每完成一个 VAT，补录 GIF 更新 README（液体倾倒 / 晶体生长 / 蒸汽）
- [ ] Phase 4 英雄资产完成后，替换 ShowcaseScene 展示台上的程序化网格为真实模型
- [ ] 完成 Task 4.3 炼金序列后，录一段"完整炼金流程演示"视频（而不只是材质静态展示）
- [ ] 整理到 Notion / ArtStation：每个 Shader 一张 ShaderGraph 截图 + 一张 Houdini SOP 截图 + 2~3 句技术说明
- [ ] （可选）剪辑 60~90 秒 Showreel：overview → crystal → metal → vat倾倒 → 晶体生长 → 蒸汽 → dissolve → rune → outline对比

---

## 下一步行动顺序

```
第 1 步  Task 2.2  液体倾倒 VAT
         → 从 Houdini 开始，建烧瓶+容器场景，FLIP 模拟，导出 VAT
         → Unity 端新建 VAT_LiquidPour.shadergraph，接入材质
         → 录 GIF 更新 README

第 2 步  Task 2.3  晶体生长 VAT
         → Houdini VDB Combine 方案，Rigid VAT 导出
         → Unity 端复用 Crystal 材质，加生长前沿 Emission
         → 录 GIF 更新 README

第 3 步  Task 2.5  蒸汽/烟雾 VAT
         → Houdini Pyro → VDB → Mesh，Soft Body VAT
         → Unity 半透明 + Alpha Fade + 噪声边缘
         → 录 GIF 更新 README

第 4 步  Task 4.1  找/做英雄模型（大锅 + 烧瓶）
         → Sketchfab CC0，或 Blender 简单建
         → 挂上 AlchemyMetal / PotionFlask 材质

第 5 步  Task 2.1  大锅沸腾 VAT
         → 有了大锅模型后做这个才有用，和 Phase 4 合并进行

第 6 步  Task 4.2  SG_PotionFlask 补完
         → 有了烧瓶模型后做

第 7 步  Task 4.3  AlchemySequencer（炼金动画序列）
         → 把以上所有效果串成一个完整演示链路

第 8 步  Phase 3   场景氛围重构
         → 光照 + 环境材质替换 + 整体调色微调
         → 重拍 overview GIF

第 9 步  Phase 5   作品集更新收尾
```

---

## 技术深度提升建议（应对面试追问）

**近期（配合 Phase 2）**
- 弄清 **FLIP 的关键参数**（Surface Tension / Viscosity / XPBD）：Houdini 里的数值和真实物理的映射关系，面试会问
- 理解 **VAT 三种模式**（Rigid / Soft / Sprite）的适用场景和数据结构差异

**中期（配合 Phase 3/4）**
- **PBR 原理**（能量守恒 / Cook-Torrance BRDF）：不是要你改成写实，而是要能解释"NPR 是如何有意违反 PBR 的，以及这样做的取舍"——面试必问
- **URP 渲染顺序**（DrawCall 顺序 / RenderPass 生命周期 / 半透明排序）：你已经在用 RendererFeature，往下一层就是这个

**长期**
- **Houdini PDG**（任务图 / 批量自动化）：大厂 TA 的 Houdini 工作很大一部分是管线自动化，不只是做效果
- **GPU Instancing / DrawMeshInstanced**：粒子/晶石簇等大量重复物体的优化方式

**参考资料**
- GDC Vault（免费）搜 "NPR" / "stylized rendering" / "vertex animation texture"
- 米哈游 / 鹰角 / 叠纸的技术博客和 SIGGRAPH/GDC 分享（和你的方向直接对口）
- Houdini FLIP 官方教程 + SideFX Labs VAT 文档

---

## 快速参考：关键参数 & 踩坑记录

```
【莱莎渲染参数】
Ramp Shadow    — 两段式，边界 smoothstep 宽度 0.05~0.1
Rim Light      — Power 2.5~4，暖橙 #FF8C42 / 魔法紫 #9B59B6
Cel Specular   — Shininess 60~120，step 硬切（无平滑过渡）
Outline(背面)  — Clip Space 膨胀，视觉宽度 0.5~1.5px 恒定
Bloom          — Threshold 0.8，Emission > 1 才发光
Color Grading  — 阴影蓝紫 / 中调暖 / 高光金黄，Saturation +15
晶体色散       — RGB 偏移量约 0.005~0.015（太大会失真）

【VAT 踩坑】
Vertex Compression  — 打包时关闭（=0），否则 UV 查表数据被量化炸开
pos/rot 贴图        — EXR 不压缩 + 关 sRGB + 关 Mip + 不要用 Point 以外的过滤
lookup 贴图         — Point 过滤，否则查到相邻顶点数据
视锥剔除           — VAT 网格包围盒常被导出器放大，挂 VATBoundsExpander.cs
mf.mesh vs sharedMesh — build 里 mesh 不可读时 mf.mesh 克隆出乱顶点，改用 sharedMesh

【URP Build 踩坑】
Hidden/ Shader      — 仅 Shader.Find 引用会被 build 剔除，加入 Always Included Shaders
DepthNormals Pass   — 队列 >2500 的 Shader 不进预通道，全局屏幕空间描边看不见它
```

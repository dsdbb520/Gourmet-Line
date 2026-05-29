# GourmetLine Shader 使用笔记

## 目录
- [AlchemyMetal_CelShaded — 金属 Shader](#alchemymetal_celshaded)
- [Crystal_CelShaded — 晶体 Shader](#crystal_celshaded)

---

## AlchemyMetal_CelShaded

**文件**：`Assets/Shader/AlchemyMetal_CelShaded.shader`  
**材质**：`Assets/Shader/Mat_AlchemyMetal.mat`  
**适用**：炼金炉、大锅、管道接头、炉架等金属部件

### 四个核心功能

| 功能 | 驱动方式 |
|---|---|
| 各向异性高光 | Kajiya-Kay，切线 + 法线偏移 |
| Matcap 金属反射 | normalVS.xy → Matcap UV，Screen 混合 |
| 边缘磨损 | 顶点色 R 通道（白=新鲜，黑=磨损） |
| 受热发光 | World Y 轴梯度，炉底橙红→顶部消散 |

### 参数速查

| 参数 | 作用 | 建议值 |
|---|---|---|
| `Matcap Intensity` | 金属反射强度 | 0.6 ~ 1.2 |
| `Aniso Shininess` | 高光锐利度，越大越细 | 100 ~ 300 |
| `Aniso Threshold` | 高光显示的最低亮度（step 阈值），越高越锐利 | 0.7 ~ 0.85 |
| `Aniso Shift` | 高光沿法线方向偏移，调整高光在曲面上的位置 | -0.2 ~ 0.2 |
| `Worn Intensity` | 磨损叠色强度，0 = 无磨损 | 0.5 ~ 1.0 |
| `Heat Y Bottom/Top` | 炉子在 World Space 里的底部/顶部 Y 坐标 | 对齐模型实际高度 |
| `Heat Falloff` | 发光集中程度，越大越集中在炉底 | 2 ~ 4 |
| `Shadow Ramp Threshold` | 明暗分界线位置 | 0.4 ~ 0.6 |

### 边缘磨损工作流

顶点色 R 通道需要在 DCC 软件（Blender / Maya）里手动刷：
- **白色 (R=1)**：新鲜金属面，保持原始颜色
- **黑色 (R=0)**：磨损区域（边缘、接缝、凹角），叠上 `Worn Color`

如果模型没有顶点色，默认全白，磨损效果不生效（正常现象）。

### 受热发光设置步骤

1. 把炉子放到场景里，记录炉底和顶部的 World Y 值
2. 把这两个值填入 `Heat Y Bottom` 和 `Heat Y Top`
3. 调 `Heat Falloff`：值越大，发光越集中在底部

### Matcap 推荐

| 文件 | 效果 |
|---|---|
| `Deepmatcaps/Matcap_Brass_360.png` | 黄铜感，圆润反光 |
| `Deepmatcaps/Matcap_Golden.png` | 金色金属 |
| `Simple Matcaps/Matcap_Blackglossy.png` | 铸铁深色 |
| `Simple Matcaps/Matcap_Copper.png` | 铜制炼金器具 |

---

## Crystal_CelShaded

**文件**：`Assets/Shader/Crystal_CelShaded.shader`  
**材质**：`Assets/Shader/Mat_Crystal.mat`  
**适用**：矿石、宝石、炼金原材料等晶体类物体

### 前置条件

使用前必须在 URP Renderer Asset 里勾选 **Opaque Texture**。

位置：`Assets/Settings/` → 找到 Renderer 文件 → Inspector 里勾选 `Opaque Texture`。

不勾选的话折射区域显示纯黑。

### 五个核心功能

| 功能 | 驱动方式 |
|---|---|
| 场景折射 | CameraOpaqueTexture + normalVS 扰动 UV |
| 色散 | 折射 UV 的 RGB 三通道分别偏移 |
| 内部辉光 | 双角度 Matcap 相乘，Screen 混合 |
| 宝石高光 | Blinn-Phong 极高 shininess，针点状 |
| 背光透色 | saturate(-NdotL) 驱动，光从背后时整体发光 |

### 参数速查

| 参数 | 作用 | 建议值 |
|---|---|---|
| `Refraction Strength` | 折射扭曲幅度 | 0.02（微弱）~ 0.08（厚重） |
| `Refraction Tint` | 折射背景被晶体颜色染色的程度 | 0.3 ~ 0.7 |
| `Dispersion Amount` | 色散彩虹边缘宽度 | 0.008 ~ 0.025 |
| `Center Opacity` | 晶体正面中心的不透明度 | 0.7 ~ 0.95 |
| `Edge Opacity` | 晶体边缘的不透明度（比 Center 低） | 0.1 ~ 0.4 |
| `Inner Glow Intensity` | 内部辉光强度，0 = 完全依赖背景折射 | 1.0 ~ 2.5 |
| `Transmission Intensity` | 背光透色强度 | 1.0 ~ 2.5 |
| `Gem Threshold` | 宝石高光锐利度，越高高光越小越集中 | 0.85 ~ 0.95 |

### 折射限制

`_CameraOpaqueTexture` 只包含不透明物体，URP 渲染顺序决定了这一点：

```
① 渲染不透明物体 (Queue < 3000)
② 拍快照 → _CameraOpaqueTexture
③ 渲染透明物体 (Queue >= 3000)  ← 晶体在这一步
```

| 物体类型 | 能被折射 |
|---|---|
| 普通不透明物体（石头、墙壁、地面） | ✅ |
| AlphaClip 物体 | ✅ |
| 其他透明 Shader 物体（药水瓶、另一块晶体） | ❌ |
| 粒子特效 | ❌ |

这是 URP 的架构限制，不是 Bug。

### 演示布置建议

- 在晶体背后放不透明场景物件，确保折射有内容可显示
- `Inner Glow Intensity` 调到 1.5 以上，无论背景有无内容都有明显晶体质感
- 透明物体之间互相"穿透"在风格化渲染里视觉上不突兀

### Matcap 推荐（内部辉光）

| 文件 | 效果 |
|---|---|
| `Deepmatcaps/Matcap_Bluefield_Shard.png` | 蓝色碎裂晶体，辉光图案不规则 |
| `Deepmatcaps/Matcap_Glossyblue_Reflective.png` | 光泽宝石感，辉光圆润 |
| `Simple Matcaps/Matcap_Redblue.png` | 冷暖对比强，色散效果明显 |
| `Deepmatcaps/Matcap_Starmetal.png` | 带星状高光，适合魔法矿石 |

---

## Dissolve_Alchemy

**文件**：`Assets/Shader/Dissolve_Alchemy.shader`  
**材质**：`Assets/Shader/Mat_Dissolve.mat`  
**适用**：任何需要炼金反应/消融特效的物体

### 四个核心功能

| 功能 | 驱动方式 |
|---|---|
| Noise 溶解 | FBM 程序化噪声 + `clip(noise - _DissolveAmount)` |
| 发光溶解边 | `edgeFactor` 检测边界，Hot→Cool 双色 Emission |
| UV 热浪扭曲 | 独立噪声驱动偏移，随溶解进度自动增强 |
| 对外接口 | `_DissolveAmount`（0=完整，1=消失） |

### C# 调用

```csharp
material.SetFloat("_DissolveAmount", value); // 0 → 1
```

由 AnimationCurve 或 DOTween 在 ProcessorMachine 完成时驱动。

### 参数速查

| 参数 | 作用 | 建议值 |
|---|---|---|
| `Noise Tiling` | 溶解块大小，越大越碎 | 2（大块）~ 5（细碎） |
| `Edge Width` | 发光边缘宽度（占 noise 值域比例） | 0.05 ~ 0.15 |
| `Edge Intensity` | 边缘发光亮度 | 3 ~ 6 |
| `Distort Strength` | 热浪扭曲幅度 | 0.01 ~ 0.04 |
| `Heat Tint Intensity` | 整体受热染色强度 | 0.5 ~ 1.2 |

### 重要限制：不能嵌套

**一个面只能有一个 Shader**，无法把溶解效果"叠"在其他材质上面。

| 需求 | 正确做法 |
|---|---|
| 晶体溶解（保留折射效果） | 把溶解代码合并进 Crystal_CelShaded |
| 金属溶解（保留 Matcap 反射） | 把溶解代码合并进 AlchemyMetal_CelShaded |
| 不需要保留原效果 | 运行时换材质：`renderer.material = dissolveMat` |

运行时换材质最简单，但换材质瞬间原来的视觉效果（折射/辉光/Matcap）会全部消失。

### ShadowCaster Pass 的必要性

三个 Pass（ForwardLit / ShadowCaster / Outline）都需要同步 `clip()` 逻辑。如果只有主 Pass 裁切，物体虽然看不见，地面阴影依然存在。

---

## StoneFloor_CelShaded / Wood_CelShaded / Fabric_CelShaded

**文件**：`Assets/Shader/StoneFloor_CelShaded.shader` / `Wood_CelShaded.shader` / `Fabric_CelShaded.shader`  
**适用**：炼金工房地面/墙壁、木架、布料袋

### 各材质关键点

**石板地面 (StoneFloor)**
- Triplanar Mapping：不需要模型 UV，三轴世界坐标投影自动无缝拼接
- 苔藓 mask：顶点色 **G 通道**，白=苔藓，黑=裸石。在 Blender 里用顶点绘制笔刷涂抹低洼处
- 湿润高光只在无苔藓区域出现，逻辑：`wetSpec × (1 - mossMask)`
- 如果模型没有顶点色，默认全黑=全裸石，湿润高光全区域生效

**木材架子 (Wood)**
- 模型必须有 **Tangent** 信息，否则木纹高光和 Detail Normal 方向错误
  - Blender 导出时勾选 `Tangent Space`
- Detail Tiling 建议设为主贴图 tiling 的 4~8 倍（例如主贴图 1x，Detail 用 4x~8x）
- 阴影暗部用 `_ShadowColor`（冷棕）而非纯黑，营造室内暖光反弹感

**布料材料袋 (Fabric)**
- 模型同样需要 **Tangent** 信息
- 双层各向异性：经线用 T（切线），纬线用 B（副切线 = N × T × handedness）
- `_WarpShift` 和 `_WeftShift` 符号建议相反（如 +0.2 / -0.15），让两条高光带错开位置
- 布料高光比金属弱（intensity 0.5~0.7），模拟的是织物表面涂层微反光，不是金属镜面

### 与主角材质的视觉统一

三个环境 Shader 都使用与 AlchemyMetal / Crystal 相同的：
- Rim Light（边缘光）
- Shadow smoothstep（明暗分界）
- Outline Pass（描边）

---

## 各 Shader 核心区别一览

| | AlchemyMetal | Crystal | Dissolve |
|---|---|---|---|
| 底色来源 | albedo × RampShadow | 折射背景 + 内部辉光 | albedo × 受热染色 |
| 高光形状 | Kajiya-Kay 条带 | Blinn-Phong 针点 | 无（效果 Shader） |
| 特殊效果 | 受热 Y 梯度发光 | 背光透色 | clip 溶解 + 发光边 |
| 透明度 | 不透明 | Fresnel 半透 | AlphaTest（clip） |
| Matcap | 主反射 | 内部辉光 | 无 |
| 顶点色 | 磨损 mask | 未使用 | 未使用 |

// ════════════════════════════════════════════════════════════════════════════
//  ShowcaseSceneBuilder.cs  —  作品集 Shader 展示场景一键生成器
// ────────────────────────────────────────────────────────────────────────────
//  用途：菜单 [GourmetLine ▸ Build Showcase Scene] 一键生成一个干净的展示场景，
//        把项目里 8 个自定义 HLSL Shader 的效果整齐地摆出来，方便截图/录屏。
//
//  设计原则（重要）：
//    · 本脚本只“摆放”和“配材质”，绝不修改任何 .shader 代码。
//    · 缺材质的环境 Shader（Stone/Wood/Fabric）会自动 new 一个 .mat，
//      只是把 shader 套上去 + 设个底色，不改 shader 逻辑。
//    · 生成前会提示保存当前场景，不会覆盖你正在做的工作。
//
//  注意：这是 Editor 脚本，必须放在名为 "Editor" 的文件夹里才能编译。
// ════════════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using GourmetLine.Showcase;

namespace GourmetLine.EditorTools
{
    public static class ShowcaseSceneBuilder
    {
        // ── 路径常量 ──────────────────────────────────────────────────────────
        const string ScenePath  = "Assets/Scenes/ShowcaseScene.unity";
        const string ShaderDir  = "Assets/Shader/";
        const string ResDir     = "Assets/Resources/";

        // Houdini VAT 管道流体资产
        const string VatMeshPath  = "Assets/untitled/geo/vertex_animation_textures1_mesh.fbx";
        const string VatMatPath   = "Assets/untitled/unity/vertex_animation_textures1_mat.mat";
        const string GlassMatPath = "Assets/Material/Mat_PipeGlass.mat";
        // 可选：若存在此 prefab(从 SampleScene 拖出的"流体+玻璃管")则优先用它，最还原
        const string PipeFluidPrefabPath = "Assets/Prefabs/PipeFluid.prefab";

        // 一个展示台占用的水平间距 / 区与区之间的纵向间距
        const float ColumnSpacing = 4.0f;   // 相邻展示台的 X 间距
        const float PedestalHeight = 0.9f;  // 底座高度（展示物放在底座上方）

        // ── 一个展示台的配置数据 ─────────────────────────────────────────────
        class Entry
        {
            public string DisplayName;   // 标注用的材质/Shader 名
            public string TechPoint;     // 标注用的一句技术点
            public string ShaderPath;    // 对应 .shader 路径（用于必要时建材质）
            public string MaterialPath;  // .mat 路径（不存在则用 ShaderPath 现建）
            public string Mesh;          // 网格语义键（见 ResolveMesh）
            public Color  Tint;          // 没有现成材质时给新材质的底色

            public Entry(string name, string tech, string shader, string mat, string mesh, Color tint)
            {
                DisplayName = name; TechPoint = tech; ShaderPath = shader;
                MaterialPath = mat; Mesh = mesh; Tint = tint;
            }
        }

        // 三个分区：道具材质 / 环境材质 / 特效动态
        static readonly string[] Categories =
        {
            "PROPS  (道具/材质表现)",
            "ENVIRONMENT  (环境材质)",
            "FX  (特效/动态)",
            "NPR PIPELINE  (风格化描边)"
        };

        // 每个分区下的展示台列表
        static List<Entry>[] BuildEntries()
        {
            var props = new List<Entry>
            {
                new Entry("AnimeFood", "Ramp卡通阴影 + 伪SSS + Rim + Cel硬边高光",
                    ShaderDir + "AnimeFood_CelShaded.shader", ShaderDir + "Mat_AnimeFood.mat",
                    "sphere", new Color(0.9f, 0.5f, 0.3f)),
                new Entry("AlchemyMetal", "Kajiya-Kay各向异性 + Matcap反射 + 受热发光",
                    ShaderDir + "AlchemyMetal_CelShaded.shader", ShaderDir + "Mat_AlchemyMetal.mat",
                    "ingot", new Color(0.72f, 0.65f, 0.52f)),
                new Entry("Crystal", "屏幕空间折射 + RGB色散 + 背光透色 + Emission脉冲",
                    ShaderDir + "Crystal_CelShaded.shader", ShaderDir + "Mat_Crystal.mat",
                    "gem", new Color(0.45f, 0.2f, 0.85f)),
            };

            var env = new List<Entry>
            {
                new Entry("StoneFloor", "Triplanar三平面映射 + 苔藓mask + 湿润高光",
                    ShaderDir + "StoneFloor_CelShaded.shader", ShaderDir + "Mat_StoneFloor.mat",
                    "stonesphere", new Color(0.55f, 0.52f, 0.48f)),
                new Entry("Wood", "Detail法线木纹 + 冷暖分离卡通调 + 木纹高光",
                    ShaderDir + "Wood_CelShaded.shader", ShaderDir + "Mat_Wood.mat",
                    "sphere", new Color(0.58f, 0.38f, 0.18f)),
                new Entry("Fabric", "双层各向异性(经线Warp+纬线Weft) + Cel硬边",
                    ShaderDir + "Fabric_CelShaded.shader", ShaderDir + "Mat_Fabric.mat",
                    "sphere", new Color(0.55f, 0.42f, 0.28f)),
            };

            var fx = new List<Entry>
            {
                new Entry("Dissolve", "FBM程序噪声溶解 + 发光溶解边 + UV热浪扭曲",
                    ShaderDir + "Dissolve_Alchemy.shader", ShaderDir + "Mat_Dissolve.mat",
                    "sphere", new Color(0.8f, 0.6f, 0.3f)),
                new Entry("RuneConveyor", "符文UV流动 + 发光脉冲 + Voronoi程序粒子",
                    ShaderDir + "RuneConveyor_CelShaded.shader", ShaderDir + "Mat_RuneConveyor_CelShaded.mat",
                    "slab", new Color(0.12f, 0.10f, 0.18f)),
                new Entry("PipeFluidVAT", "Houdini流体 → VAT顶点动画贴图 → GPU重建 + 玻璃管折射",
                    "", "", "vat", new Color(0.4f, 0.7f, 1f)),
            };

            var npr = new List<Entry>
            {
                new Entry("ScreenSpaceOutline", "全局屏幕空间描边 (深度/法线边缘检测) · 描出每条边/折角/交界 · 面板可开关",
                    "", "", "outline", new Color(0.85f, 0.8f, 0.75f)),
            };

            return new[] { props, env, fx, npr };
        }

        // ════════════════════════════════════════════════════════════════════
        //  菜单入口
        // ════════════════════════════════════════════════════════════════════
        [MenuItem("GourmetLine/Build Showcase Scene", false, 0)]
        public static void Build()
        {
            // 1) 先让用户保存当前场景，避免丢失正在做的工作
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // 2) 新建一个空场景（默认带相机和方向光，我们清掉重建以保证干净可控）
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 3) 一个总根节点，方便整体管理
            var root = new GameObject("=== SHOWCASE ===");

            BuildLighting(root.transform);
            BuildBackdrop(root.transform);
            float areaWidth;
            var areaCenter = BuildPedestals(root.transform, out areaWidth);
            BuildRefractionBackdrop(root.transform); // 宝石后方放对比物，凸显折射/色散
            BuildCamera(root.transform, areaCenter, areaWidth);

            // 第三步：运行时参数面板（OnGUI 滑条）
            var ctrl = new GameObject("ShowcaseController");
            ctrl.transform.SetParent(root.transform);
            ctrl.AddComponent<ShowcaseControlPanel>();

            // 全局后处理（NPR 调色：Bloom + Tonemapping + 色彩分级 + Vignette）
            BuildPostVolume(root.transform);

            // 4) 保存场景
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ShowcaseSceneBuilder] 展示场景已生成: {ScenePath}\n" +
                      "提示：Crystal 折射需要 URP Renderer 勾选 Opaque Texture 才显示。");
            EditorUtility.DisplayDialog("Showcase Scene",
                "展示场景已生成并保存到:\n" + ScenePath +
                "\n\n下一步可按播放查看；动态效果(转台/溶解循环/UI滑条)在第三步加入。",
                "好的");
        }

        // ════════════════════════════════════════════════════════════════════
        //  灯光：主光 + 补光 + 环境光（让高光/菲涅尔/各向异性能显现）
        // ════════════════════════════════════════════════════════════════════
        static void BuildLighting(Transform parent)
        {
            // 主光（暖黄侧光，30° 角）
            var keyGo = new GameObject("KeyLight");
            keyGo.transform.SetParent(parent);
            keyGo.transform.rotation = Quaternion.Euler(50f, -40f, 0f);
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1.0f, 0.92f, 0.80f);
            key.intensity = 1.2f;
            key.shadows = LightShadows.Soft;

            // 补光（冷蓝，从另一侧补暗部，让背光面不死黑）
            var fillGo = new GameObject("FillLight");
            fillGo.transform.SetParent(parent);
            fillGo.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.55f, 0.6f, 0.85f);
            fill.intensity = 0.45f;
            fill.shadows = LightShadows.None;

            // 背光（从展示物后方打来，强化 Rim/轮廓与晶体边缘透光感）
            var backGo = new GameObject("BackLight");
            backGo.transform.SetParent(parent);
            backGo.transform.rotation = Quaternion.Euler(25f, 200f, 0f);
            var back = backGo.AddComponent<Light>();
            back.type = LightType.Directional;
            back.color = new Color(0.8f, 0.85f, 1.0f);
            back.intensity = 0.6f;
            back.shadows = LightShadows.None;

            // 环境光：渐变（上冷下暖），中性偏柔和，突出材质本身
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = new Color(0.30f, 0.32f, 0.40f);
            RenderSettings.ambientEquatorColor = new Color(0.30f, 0.28f, 0.28f);
            RenderSettings.ambientGroundColor  = new Color(0.18f, 0.15f, 0.13f);
        }

        // ════════════════════════════════════════════════════════════════════
        //  全局后处理 Volume：套用项目已有的 NPR 调色 Profile
        //  （这是"全局风格化渲染"中"调色"那一层的体现）
        // ════════════════════════════════════════════════════════════════════
        static void BuildPostVolume(Transform parent)
        {
            var go = new GameObject("Global Volume (NPR Grading)");
            go.transform.SetParent(parent);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 1f;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                "Assets/Settings/SampleSceneProfile.asset");
            if (profile != null) vol.sharedProfile = profile;
            else Debug.LogWarning("[Showcase] 未找到 SampleSceneProfile，调色不会生效");

            // 一块说明牌：标注全局 NPR 渲染（描边来自各物体 Outline Pass，调色来自此 Volume）
            CreateLabel(parent,
                "NPR Pipeline:  每物体 Outline Pass 描边  +  全局后处理调色(Bloom/Tonemap/分级)",
                new Vector3(0f, -0.9f, 0f),
                0.22f, new Color(0.7f, 0.85f, 1f),
                TextAlignment.Center, TextAnchor.MiddleCenter);
        }

        // ════════════════════════════════════════════════════════════════════
        //  背景：中性地面 + 远处背板，不抢材质风头
        // ════════════════════════════════════════════════════════════════════
        static void BuildBackdrop(Transform parent)
        {
            var neutral = CreateUnlitColorMaterial(new Color(0.22f, 0.22f, 0.24f));

            // 地面（接收阴影，让卡通硬阴影也能展示）
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(parent);
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(6f, 1f, 4f);
            ApplyMaterial(ground, CreateLitColorMaterial(new Color(0.26f, 0.25f, 0.27f)));

            // 背板（一面大墙，纯色，给折射/Matcap 提供干净背景）
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Backdrop";
            wall.transform.SetParent(parent);
            wall.transform.position = new Vector3(0f, 8f, 14f);
            wall.transform.localScale = new Vector3(60f, 24f, 0.5f);
            // 比之前亮一些：晶体折射/Matcap 需要背景有内容才看得出扭曲
            ApplyMaterial(wall, CreateLitColorMaterial(new Color(0.34f, 0.33f, 0.38f)));
        }

        // ════════════════════════════════════════════════════════════════════
        //  展示台：三区排布，返回展示区中心点（给相机取景）
        // ════════════════════════════════════════════════════════════════════
        static Vector3 BuildPedestals(Transform parent, out float totalWidth)
        {
            var groups = BuildEntries();
            const float GroupGap = 2.8f; // 组与组之间的额外间隙

            // 第一遍：把所有展示台沿 X 排成一条线，记录每台 X 和每组中心
            var standEntries = new List<Entry>();
            var standX       = new List<float>();
            var groupCenter  = new List<float>();

            float cursor = 0f;
            foreach (var list in groups)
            {
                float groupStart = cursor;
                foreach (var e in list)
                {
                    // 管道流体横向很长，前面额外留间距，避免压到左侧展示台
                    if (e.Mesh == "vat") cursor += 4.5f;
                    standEntries.Add(e);
                    standX.Add(cursor);
                    cursor += ColumnSpacing;
                }
                cursor -= ColumnSpacing;               // 退回最后一台多加的间距
                groupCenter.Add((groupStart + cursor) * 0.5f);
                cursor += ColumnSpacing + GroupGap;    // 跳到下一组起点
            }

            // 整体居中：把最左(0)和最右对齐到原点两侧
            float maxX   = standX[standX.Count - 1];
            float offset = -maxX * 0.5f;
            totalWidth   = maxX;

            // 第二遍：摆放展示台
            for (int i = 0; i < standEntries.Count; i++)
                BuildOnePedestal(parent, standEntries[i],
                    new Vector3(standX[i] + offset, 0f, 0f));

            // 分区标题：浮在各组正上方
            for (int r = 0; r < groups.Length; r++)
                CreateLabel(parent, Categories[r],
                    new Vector3(groupCenter[r] + offset, 3.6f, 0f),
                    0.5f, new Color(1f, 0.85f, 0.5f),
                    TextAlignment.Center, TextAnchor.MiddleCenter);

            return new Vector3(0f, 1.4f, 0f);
        }

        // ════════════════════════════════════════════════════════════════════
        //  折射对比背景：在宝石后方放几根明亮彩色竖条
        //  晶体折射/色散需要"背景有内容"才看得出扭曲与彩边，纯色背景看不出
        // ════════════════════════════════════════════════════════════════════
        static void BuildRefractionBackdrop(Transform parent)
        {
            var stand = GameObject.Find("Stand_Crystal");
            if (stand == null) return;

            // 一张带棋盘纹理的卡片放在宝石后方（+Z 远离相机）。
            // 用纹理而非细竖条：纹理带 mipmap，远处会平滑降采样，
            // 不会像细竖条那样因子像素采样而闪烁/串色。
            var card = MakeCheckerCard(parent, new Vector3(2.0f, 1.7f, 0.06f));
            card.transform.position = stand.transform.position + new Vector3(0f, 1.8f, 1.6f);
        }

        // 生成一张棋盘折射对比卡（高对比、带 mipmap），返回 GameObject 供调用方定位
        static GameObject MakeCheckerCard(Transform parent, Vector3 scale)
        {
            var card = GameObject.CreatePrimitive(PrimitiveType.Cube);
            card.name = "RefractionCard";
            card.transform.SetParent(parent);
            card.transform.localScale = scale;
            // 用能整除贴图尺寸的偶数格数(512/8=64)，保证 Repeat 平铺接缝处无缝
            var tex = CreateCheckerTexture(512, 8,
                new Color(0.96f, 0.92f, 0.82f),  // 暖白格
                new Color(0.08f, 0.10f, 0.30f));  // 深蓝格（高对比 → 折射弯折/色散彩边清晰）
            var m = CreateUnlitTexturedMaterial(tex);
            // 按卡片长宽比设置 UV 平铺，避免卡片拉长后棋盘格被拉成长方形
            var tiling = new Vector2(scale.x / Mathf.Max(0.001f, scale.y), 1f);
            if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", tiling);
            else if (m.HasProperty("_MainTex")) m.SetTextureScale("_MainTex", tiling);
            ApplyMaterial(card, m);
            return card;
        }

        // 程序化棋盘贴图（含 mipmap，三线性过滤，远处平滑不闪烁）
        static Texture2D CreateCheckerTexture(int size, int cells, Color a, Color b)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "RefractionChecker",
                wrapMode = TextureWrapMode.Repeat, // 卡片拉长后按 UV 平铺重复，而非边缘拉伸成条纹
                filterMode = FilterMode.Trilinear,
            };
            var px = new Color[size * size];
            int cell = Mathf.Max(1, size / cells);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool on = (((x / cell) + (y / cell)) & 1) == 0;
                    px[y * size + x] = on ? a : b;
                }
            tex.SetPixels(px);
            tex.Apply(true); // 生成 mipmap
            return tex;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Houdini VAT 管道流体：流体网格(VAT) + 外层玻璃管 + 后方折射卡
        //  优先用 PipeFluid.prefab（最还原 SampleScene 的搭配）；
        //  没有 prefab 时从原始 VAT 资产搭，并按流体包围盒自动包裹一根玻璃圆柱。
        // ════════════════════════════════════════════════════════════════════
        static void BuildVATFluid(Transform holder)
        {
            GameObject fluidRoot;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PipeFluidPrefabPath);
            if (prefab != null)
            {
                fluidRoot = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                fluidRoot.transform.SetParent(holder, false);
            }
            else
            {
                fluidRoot = new GameObject("PipeFluid_VAT");
                fluidRoot.transform.SetParent(holder, false);

                var vatMesh = LoadMeshFromAsset(VatMeshPath);
                var vatMat  = AssetDatabase.LoadAssetAtPath<Material>(VatMatPath);
                if (vatMesh == null || vatMat == null)
                {
                    Debug.LogWarning("[Showcase] 未找到 VAT 网格/材质，跳过管道流体。" +
                                     "可把 SampleScene 里的流体拖成 " + PipeFluidPrefabPath);
                    return;
                }

                var fluid = new GameObject("Fluid");
                fluid.transform.SetParent(fluidRoot.transform, false);
                fluid.AddComponent<MeshFilter>().sharedMesh = vatMesh;
                fluid.AddComponent<MeshRenderer>().sharedMaterial = vatMat;

                // 自动包裹玻璃管：沿流体包围盒最长轴放一根略大的透明圆柱
                var glassMat = AssetDatabase.LoadAssetAtPath<Material>(GlassMatPath);
                if (glassMat != null)
                {
                    var b = vatMesh.bounds;
                    int axis = LongestAxis(b.size);
                    float length = b.size[axis];
                    float radius = 0.5f * Mathf.Max(b.size[(axis + 1) % 3],
                                                    b.size[(axis + 2) % 3]) * 1.18f;
                    var glass = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    glass.name = "GlassPipe";
                    Object.DestroyImmediate(glass.GetComponent<Collider>());
                    glass.transform.SetParent(fluidRoot.transform, false);
                    glass.transform.localPosition = b.center;
                    // Unity 圆柱默认沿 Y；按最长轴旋转使其对齐
                    glass.transform.localRotation =
                        axis == 0 ? Quaternion.Euler(0f, 0f, 90f) :
                        axis == 2 ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
                    // 圆柱本体高 2(半径0.5)：scale.y=length/2，xz=2*radius
                    glass.transform.localScale = new Vector3(2f * radius, length * 0.5f, 2f * radius);
                    ApplyMaterial(glass, glassMat);
                }
            }

            // 修复 VAT 视锥剔除：给所有网格强制放大包围盒，避免某端移出画面时整体消失
            foreach (var mf in fluidRoot.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null && mf.GetComponent<VATBoundsExpander>() == null)
                    mf.gameObject.AddComponent<VATBoundsExpander>();

            // 摆到底座上方。注意：VAT 网格包围盒常被导出器放大(防剔除)，
            // 故不做按包围盒自动缩放——prefab 会保留你在 SampleScene 调好的尺寸；
            // 原始资产 fallback 用 scale 1，若过大/过小请在场景里手动微调该 Stand。
            fluidRoot.transform.localPosition = new Vector3(0f, PedestalHeight + 1.2f, 0f);

            // 后方折射对比卡（玻璃+液体折射需要背景有内容才看得出）；管道长，卡也加长
            var card = MakeCheckerCard(holder, new Vector3(4.5f, 1.3f, 0.06f));
            card.transform.localPosition = new Vector3(0f, PedestalHeight + 1.0f, 1.4f);
        }

        static int LongestAxis(Vector3 v)
        {
            if (v.x >= v.y && v.x >= v.z) return 0;
            return v.y >= v.z ? 1 : 2;
        }

        // ════════════════════════════════════════════════════════════════════
        //  描边展示台：Cube + 球（相连），用 Cel Shader（背面描边 + DepthNormals）。
        //  · 背面描边 → 永远描出完整外轮廓（不受背景法线/深度影响，无盲区）
        //  · 全局屏幕空间描边 → 描出内部每条棱/折角/交界
        //  在面板里开关"全局屏幕空间描边"：关=只剩外轮廓，开=内部边全出来，
        //  正好演示屏幕空间描边相对背面描边的增益。
        // ════════════════════════════════════════════════════════════════════
        static void BuildOutlineDemo(Transform holder)
        {
            var root = new GameObject("OutlineDemo");
            root.transform.SetParent(holder, false);
            root.transform.localPosition = new Vector3(0f, PedestalHeight + 1.0f, 0f);

            var celShader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderDir + "AnimeFood_CelShaded.shader");
            Shader sh = celShader != null ? celShader : Shader.Find("Universal Render Pipeline/Lit");

            var matA = new Material(sh);
            TrySetColor(matA, "_BaseColor", new Color(0.92f, 0.55f, 0.35f));
            TrySetFloat(matA, "_OutlineWidth", 1.5f);
            var matB = new Material(sh);
            TrySetColor(matB, "_BaseColor", new Color(0.45f, 0.62f, 0.88f));
            TrySetFloat(matB, "_OutlineWidth", 1.5f);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Outline_Cube";
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.transform.SetParent(root.transform, false);
            cube.transform.localPosition = new Vector3(-0.4f, 0f, 0f);
            cube.transform.localRotation = Quaternion.Euler(0f, 20f, 0f); // 微转，露出多条棱
            cube.transform.localScale = Vector3.one * 1.0f;
            ApplyMaterial(cube, matA);

            var sph = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sph.name = "Outline_Sphere";
            Object.DestroyImmediate(sph.GetComponent<Collider>());
            sph.transform.SetParent(root.transform, false);
            sph.transform.localPosition = new Vector3(0.5f, 0.05f, -0.1f); // 与 cube 相连，演示交界描边
            sph.transform.localScale = Vector3.one * 0.9f;
            ApplyMaterial(sph, matB);

            // 整组缓慢自转
            root.AddComponent<Turntable>();
        }

        static void BuildOnePedestal(Transform parent, Entry e, Vector3 basePos)
        {
            var holder = new GameObject("Stand_" + e.DisplayName);
            holder.transform.SetParent(parent);
            holder.transform.position = basePos;

            // 底座（深色圆柱），让展示物有“被陈列”的感觉
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.SetParent(holder.transform);
            pedestal.transform.localPosition = new Vector3(0f, PedestalHeight * 0.5f, 0f);
            pedestal.transform.localScale = new Vector3(1.4f, PedestalHeight * 0.5f, 1.4f);
            ApplyMaterial(pedestal, CreateLitColorMaterial(new Color(0.15f, 0.14f, 0.16f)));

            // Houdini VAT 管道流体：特殊处理（VAT 网格 + 玻璃管 + 折射卡），不走常规材质球流程
            if (e.Mesh == "vat")
            {
                BuildVATFluid(holder.transform);
                CreateLabel(holder.transform,
                    $"<{e.DisplayName}>\n{e.TechPoint}",
                    new Vector3(0f, 0.2f, -1.3f),
                    0.16f, Color.white, TextAlignment.Center, TextAnchor.UpperCenter);
                return;
            }

            // 屏幕空间描边展示台：用 Lit 物体（写 DepthNormals），全局描边才能描出每条边
            if (e.Mesh == "outline")
            {
                BuildOutlineDemo(holder.transform);
                CreateLabel(holder.transform,
                    $"<{e.DisplayName}>\n{e.TechPoint}",
                    new Vector3(0f, 0.2f, -1.3f),
                    0.16f, Color.white, TextAlignment.Center, TextAnchor.UpperCenter);
                return;
            }

            // 展示物：语义网格 + 对应材质
            var mat = ResolveMaterial(e);
            ApplyShaderTextures(e.DisplayName, mat); // 接入下载的写实贴图

            // 半透明物件队列前移到 2510：紧跟不透明(含描边 2501)之后渲染，
            // 避免与背景物体的描边产生排序冲突。
            if (mat != null && mat.renderQueue >= (int)RenderQueue.Transparent)
            {
                mat.renderQueue = 2510;
                EditorUtility.SetDirty(mat);
            }
            // 旧版 Cel 材质可能烘焙了 2501 队列(>2500)，会被 DepthNormals 预通道排除。
            // 拉回不透明范围，确保新加的 DepthNormals Pass 生效、能被全局描边。
            else if (mat != null && mat.renderQueue > 2500)
            {
                mat.renderQueue = (int)RenderQueue.Geometry;
                EditorUtility.SetDirty(mat);
            }

            // 统一风格化描边宽度（px）。Crystal 设 0：透明折射宝石加背面描边会有
            // 黑壳穿透/与折射打架的问题，故去掉它的描边（它也不参与全局屏幕空间描边）。
            TrySetFloat(mat, "_OutlineWidth", e.DisplayName == "Crystal" ? 0f : 1.5f);
            EditorUtility.SetDirty(mat);
            var mesh = ResolveMesh(e.Mesh);
            var item = new GameObject("Display_" + e.DisplayName);
            item.transform.SetParent(holder.transform);
            item.transform.localPosition = new Vector3(0f, PedestalHeight + 0.9f, 0f);
            var mf = item.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = item.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            // 按语义微调展示物尺寸，让各台视觉大小统一
            item.transform.localScale = ResolveScale(e.Mesh, mesh);
            // 符文传送带是扁平板，下移贴近底座顶部
            if (e.Mesh == "slab")
                item.transform.localPosition = new Vector3(0f, PedestalHeight + 0.25f, 0f);

            // 第三步：转台自转（视角相关效果必需）
            // 例外：StoneFloor 用世界空间 Triplanar，旋转会让纹理"滑动"
            //       (纹理钉在世界空间)，地面材质本就该静止，故不挂转台。
            if (e.DisplayName != "StoneFloor")
                item.AddComponent<Turntable>();
            // 溶解材质额外挂自动循环驱动
            if (e.DisplayName == "Dissolve")
                item.AddComponent<DissolveLoop>();

            // 文字标注：材质名 + 技术点
            CreateLabel(holder.transform,
                $"<{e.DisplayName}>\n{e.TechPoint}",
                new Vector3(0f, 0.2f, -1.3f),
                0.16f, Color.white, TextAlignment.Center, TextAnchor.UpperCenter);
        }

        // ════════════════════════════════════════════════════════════════════
        //  材质解析：有现成 .mat 用现成的；环境组没有就现建一个套 shader
        // ════════════════════════════════════════════════════════════════════
        static Material ResolveMaterial(Entry e)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(e.MaterialPath);
            if (mat != null) return mat;

            // 没有材质（Stone/Wood/Fabric）→ 用 shader 现建一个，只设底色
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(e.ShaderPath);
            if (shader == null)
            {
                Debug.LogWarning($"[ShowcaseSceneBuilder] 找不到 Shader: {e.ShaderPath}");
                return CreateLitColorMaterial(e.Tint);
            }

            mat = new Material(shader);
            // 环境 shader 的底色属性名不统一，挨个试着设一下（设不到不会报错）
            TrySetColor(mat, "_BaseColor", e.Tint);
            TrySetColor(mat, "_StoneColor", e.Tint);
            AssetDatabase.CreateAsset(mat, e.MaterialPath);
            Debug.Log($"[ShowcaseSceneBuilder] 新建材质: {e.MaterialPath}");
            return mat;
        }

        // ════════════════════════════════════════════════════════════════════
        //  网格解析：按语义返回网格，FBX 加载失败则退回基础体
        // ════════════════════════════════════════════════════════════════════
        static Mesh ResolveMesh(string key)
        {
            switch (key)
            {
                case "ingot":
                    var ig = LoadMeshFromAsset(ResDir + "metal-ingot/Ingot.fbx");
                    return ig != null ? ig : Primitive(PrimitiveType.Cylinder);
                case "gem":         return CreateGemMesh();    // glTF 无法导入 → 程序化宝石
                case "stonesphere": return CreateStoneSphere(); // 带噪声顶点色苔藓的球
                case "cube":        return Primitive(PrimitiveType.Cube);
                case "slab":       return Primitive(PrimitiveType.Cube); // 缩放成扁平板
                case "sphere":
                default:           return Primitive(PrimitiveType.Sphere);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  贴图接线：把下载的写实 PBR 贴图喂给各 Shader 对应的纹理槽
        //  （只设纹理引用，不改 shader 代码；底色等保留材质原值）
        // ════════════════════════════════════════════════════════════════════
        static void ApplyShaderTextures(string name, Material mat)
        {
            if (mat == null) return;
            switch (name)
            {
                case "StoneFloor":
                    TrySetTex(mat, "_StoneAlbedo",
                        ResDir + "stone_floor/textures/monastery_stone_floor_diff_4k.jpg");
                    TrySetTex(mat, "_StoneNormal",
                        ResDir + "stone_floor/textures/monastery_stone_floor_nor_gl_4k.exr", true);
                    TrySetColor(mat, "_StoneColor", Color.white); // 让贴图真实显色，不二次染色
                    TrySetFloat(mat, "_Tiling", 1.6f);            // 提高密度，纹理更清晰(原0.5太放大)
                    TrySetFloat(mat, "_WetIntensity", 0.1f);      // 进一步降湿润高光，避免整面反光
                    TrySetFloat(mat, "_WetThreshold", 0.78f);     // 提阈值，高光更收敛
                    break;
                case "Wood":
                    TrySetTex(mat, "_BaseMap",
                        ResDir + "wood_planks/textures/wood_planks_diff_4k.jpg");
                    TrySetTex(mat, "_DetailNormalMap",
                        ResDir + "wood_planks/textures/wood_planks_nor_gl_4k.exr", true);
                    TrySetColor(mat, "_BaseColor", Color.white);
                    // 冷暖分离：阴影染成明显的冷紫棕，与暖色木材形成卡通冷暖对比
                    TrySetColor(mat, "_ShadowColor", new Color(0.16f, 0.13f, 0.26f, 1f));
                    TrySetFloat(mat, "_ShadowEdge", 0.08f);           // 适度软过渡，球面旋转时明暗带平滑扫过
                    TrySetFloat(mat, "_ShadowRampThreshold", 0.55f);  // 更多区域进入阴影带，分离更明显
                    // 木纹高光：法线已被 Detail Normal 打散，可适度提回，呈现条状木纹光而非整片反光
                    TrySetFloat(mat, "_GrainIntensity", 0.25f);
                    TrySetFloat(mat, "_GrainThreshold", 0.6f);
                    TrySetFloat(mat, "_DetailNormalStrength", 1.4f);  // 加强木纹法线细节
                    TrySetFloat(mat, "_DetailTiling", 6f);            // 木纹更密，高光更碎
                    TrySetFloat(mat, "_RimIntensity", 0.3f);
                    break;
                case "Fabric":
                    TrySetTex(mat, "_BaseMap",
                        ResDir + "fabric/textures/fabric_pattern_07_col_1_4k.png");
                    TrySetColor(mat, "_BaseColor", Color.white);
                    // 让两道各向异性光带更细、更弱，像织物微光而非假光带
                    TrySetFloat(mat, "_WarpIntensity", 0.3f);
                    TrySetFloat(mat, "_WeftIntensity", 0.2f);
                    TrySetFloat(mat, "_WarpShininess", 90f);   // 提 shininess → 光带更细
                    TrySetFloat(mat, "_WeftShininess", 60f);
                    TrySetFloat(mat, "_WarpThreshold", 0.6f);  // 提阈值 → 光带更收敛
                    TrySetFloat(mat, "_WeftThreshold", 0.55f);
                    break;
                case "AlchemyMetal":
                    TrySetTex(mat, "_BaseMap",
                        ResDir + "metal-ingot/textures/Ingot_Albedo.png");
                    TrySetColor(mat, "_BaseColor", Color.white);
                    break;
                case "Crystal":
                    // 晶体折射会被 _BaseMap 染色，用宝石 baseColor 增加色彩层次
                    TrySetTex(mat, "_BaseMap",
                        ResDir + "birthday_gem/textures/OuterGem_baseColor.png");
                    TrySetFloat(mat, "_RefractionStrength", 0.08f); // 加强折射扭曲，更易看出
                    TrySetFloat(mat, "_DispersionAmount", 0.025f);  // 加强色散彩边
                    break;
            }
            EditorUtility.SetDirty(mat);
        }

        // 加载贴图并赋给材质属性；isNormal=true 时确保以法线图方式导入
        static void TrySetTex(Material mat, string prop, string path, bool isNormal = false)
        {
            if (!mat.HasProperty(prop)) return;
            if (isNormal) EnsureNormalMap(path);
            var tex = AssetDatabase.LoadAssetAtPath<Texture>(path);
            if (tex == null) { Debug.LogWarning($"[Showcase] 找不到贴图: {path}"); return; }
            mat.SetTexture(prop, tex);
        }

        // 把法线贴图的导入类型设为 NormalMap（否则 UnpackNormal 解码不正确）
        static void EnsureNormalMap(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            if (imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  程序化宝石网格：六棱双锥（顶部冠部 + 底部尖底），平面法线产生刻面
        //  glTF 无法被 Unity 原生导入，用这个保证晶体 Shader 的折射/色散有棱面可显
        // ════════════════════════════════════════════════════════════════════
        static Mesh CreateGemMesh()
        {
            const int seg = 6;
            float girdleY = 0.35f, girdleR = 0.62f;
            Vector3 top = new Vector3(0f, 1f, 0f);
            Vector3 bot = new Vector3(0f, -0.85f, 0f);

            var ring = new Vector3[seg];
            for (int i = 0; i < seg; i++)
            {
                float a = (i / (float)seg) * Mathf.PI * 2f;
                ring[i] = new Vector3(Mathf.Cos(a) * girdleR, girdleY, Mathf.Sin(a) * girdleR);
            }

            var verts = new List<Vector3>();
            var tris  = new List<int>();
            // 平面着色：每个三角形用独立顶点，便于产生硬刻面
            System.Action<Vector3, Vector3, Vector3> addTri = (p0, p1, p2) =>
            {
                int b = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(p2);
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            };

            for (int i = 0; i < seg; i++)
            {
                var a = ring[i];
                var c = ring[(i + 1) % seg];
                addTri(top, a, c); // 冠部
                addTri(bot, c, a); // 底部（反向绕序保证朝外）
            }

            var mesh = new Mesh { name = "ProceduralGem" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // 按语义返回展示物缩放：FBX 网格会按包围盒归一到约 1.5 单位高
        static Vector3 ResolveScale(string key, Mesh mesh)
        {
            switch (key)
            {
                case "slab":   return new Vector3(2.2f, 0.18f, 2.2f); // 扁平传送带
                case "cube":   return Vector3.one * 1.3f;
                case "gem":    return Vector3.one * 1.1f;            // 程序化宝石本就接近单位
                case "ingot":
                    // FBX 尺寸未知 → 用包围盒把最长边归一到 ~1.8 单位
                    if (mesh != null)
                    {
                        float maxDim = Mathf.Max(mesh.bounds.size.x,
                                       Mathf.Max(mesh.bounds.size.y, mesh.bounds.size.z));
                        if (maxDim > 0.0001f) return Vector3.one * (1.8f / maxDim);
                    }
                    return Vector3.one;
                case "sphere":
                default:       return Vector3.one * 1.3f;
            }
        }

        // 带噪声顶点色苔藓的球：StoneFloor shader 用 vertexColor.g 控制苔藓。
        // 球体顶点多(~515)，可写 Perlin 噪声 → 自然的苔藓斑块(而非立方体那条假的直线)。
        // 苔藓偏好低洼/底部，所以用"底部偏置 × 噪声斑块"合成 G。
        static Mesh CreateStoneSphere()
        {
            var m = Object.Instantiate(Primitive(PrimitiveType.Sphere));
            m.name = "StoneSphere_VColor";
            var verts = m.vertices;                          // 球本地半径 0.5
            var cols  = new Color[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                // 底部偏置：越靠下苔藓越多（苔藓长在背阴低洼处）
                float lower = Mathf.Clamp01((-v.y + 0.05f) * 1.8f);
                // 两层 Perlin 叠加成斑块，频率不同避免规则感
                float n = Mathf.PerlinNoise(v.x * 5f + 11f, v.z * 5f + 4f) * 0.65f
                        + Mathf.PerlinNoise(v.y * 9f + 2f,  v.x * 9f + 7f) * 0.35f;
                // 斑块化：smoothstep 产生清晰但不规则的苔藓边界
                float moss = Mathf.SmoothStep(0.35f, 0.75f, lower * 0.55f + n * 0.7f);
                cols[i] = new Color(1f, moss, 1f, 1f);       // 只有 G 通道有意义
            }
            m.colors = cols;
            return m;
        }

        static Mesh LoadMeshFromAsset(string path)
        {
            var objs = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var o in objs)
                if (o is Mesh m) return m;
            return null;
        }

        static Mesh Primitive(PrimitiveType t)
        {
            var go = GameObject.CreatePrimitive(t);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            return mesh;
        }

        // ════════════════════════════════════════════════════════════════════
        //  相机：对准展示区，方便截图
        // ════════════════════════════════════════════════════════════════════
        static void BuildCamera(Transform parent, Vector3 lookAt, float width)
        {
            const float Fov = 50f;
            // 让整排横向铺满画面：根据半宽 + 留白反推相机距离
            // (除以 1.7 近似 16:9 的横向视野比纵向宽，避免相机拉太远)
            float halfW = width * 0.5f + 3.0f;
            float dist  = halfW / (Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad) * 1.7f);
            dist = Mathf.Max(dist, 10f);

            var camGo = new GameObject("ShowcaseCamera");
            camGo.transform.SetParent(parent);
            camGo.transform.position = lookAt + new Vector3(0f, dist * 0.28f, -dist);
            camGo.transform.LookAt(lookAt + Vector3.up * 0.3f);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.16f, 0.19f);
            cam.fieldOfView = Fov;
            camGo.tag = "MainCamera";

            // 抗锯齿：在 MSAA(硬件) 之上再开 SMAA(后处理)，消高光/描边闪烁
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;

            // 运行时自由飞行（右键转视角 + WASD/EQ 移动）
            camGo.AddComponent<FlyCamera>();
        }

        // ════════════════════════════════════════════════════════════════════
        //  工具：文字标注（用内置 3D TextMesh，无需 TextMeshPro 依赖）
        // ════════════════════════════════════════════════════════════════════
        static void CreateLabel(Transform parent, string text, Vector3 localPos,
            float charSize, Color color, TextAlignment align, TextAnchor anchor)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.characterSize = charSize;
            tm.fontSize = 90;            // 高分辨率字体再缩小，保证清晰
            tm.color = color;
            tm.alignment = align;
            tm.anchor = anchor;
            // 缩放补偿 fontSize 放大
            go.transform.localScale = Vector3.one * 0.12f;
            // 标注朝向相机方向（相机在 -Z 侧）
            go.transform.localRotation = Quaternion.identity;
        }

        // ── 材质小工具 ────────────────────────────────────────────────────────
        static Material CreateLitColorMaterial(Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            var m = new Material(sh);
            TrySetColor(m, "_BaseColor", c);
            TrySetColor(m, "_Color", c);
            return m;
        }

        static Material CreateUnlitColorMaterial(Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            var m = new Material(sh);
            TrySetColor(m, "_BaseColor", c);
            TrySetColor(m, "_Color", c);
            return m;
        }

        static Material CreateUnlitTexturedMaterial(Texture tex)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Texture");
            var m = new Material(sh);
            TrySetColor(m, "_BaseColor", Color.white);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            else if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            return m;
        }

        static void TrySetColor(Material m, string prop, Color c)
        {
            if (m != null && m.HasProperty(prop)) m.SetColor(prop, c);
        }

        static void TrySetFloat(Material m, string prop, float v)
        {
            if (m != null && m.HasProperty(prop)) m.SetFloat(prop, v);
        }

        static void ApplyMaterial(GameObject go, Material m)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = m;
        }
    }
}

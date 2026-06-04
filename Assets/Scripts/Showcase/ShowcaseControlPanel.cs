using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GourmetLine.Showcase
{
    /// <summary>
    /// 运行时参数面板（IMGUI/OnGUI，无需 Canvas）。
    /// 顶部分页：全局风格 / 道具材质 / 环境材质 / FX，每页一组实时可调参数。
    /// 演示"Shader 暴露参数接口 + C# 运行时驱动"的 TA 能力。
    ///
    /// 用 renderer.material（实例化）读写，运行结束不污染 .mat 资产；
    /// 按展示物名字 Display_xxx 自动查找，无需手动拖引用。
    /// </summary>
    public class ShowcaseControlPanel : MonoBehaviour
    {
        enum Tab { Global, Props, Env, FX }
        static readonly string[] TabNames = { "全局风格", "道具材质", "环境材质", "FX" };

        class Ctrl
        {
            public Tab tab;
            public string label;
            public Material mat;
            public int id;
            public float min, max, val;
            public string text;   // 输入框缓冲，与 val 联动
        }

        readonly List<Ctrl> _ctrls = new List<Ctrl>();
        readonly List<Material> _outlineMats = new List<Material>(); // 全局描边宽度作用对象
        Turntable[] _turntables;
        DissolveLoop _dissolve;

        bool _show = true;
        bool _ssOutlineOn = true;
        bool _dissolveAuto = true;
        float _turntableSpeed = 20f;
        float _outlineWidth = 1.5f;
        Tab _tab = Tab.Global;
        Vector2 _scroll;
        // 全局项的输入框缓冲
        string _turntableText, _outlineText, _dissolveText;

        void Start()
        {
            _turntables = Object.FindObjectsOfType<Turntable>();
            if (_turntables.Length > 0) _turntableSpeed = _turntables[0].degreesPerSecond;

            // 全局描边宽度作用对象：所有展示物（宝石除外，它不要描边）
            foreach (var mr in Object.FindObjectsOfType<MeshRenderer>())
                if (mr.gameObject.name.StartsWith("Display_") &&
                    mr.gameObject.name != "Display_Crystal")
                    _outlineMats.Add(mr.material);
            foreach (var m in _outlineMats)
                if (m.HasProperty("_OutlineWidth")) { _outlineWidth = m.GetFloat("_OutlineWidth"); break; }

            var dgo = GameObject.Find("Display_Dissolve");
            if (dgo != null) _dissolve = dgo.GetComponent<DissolveLoop>();

            // ── 道具材质 ──────────────────────────────────────────────
            AddCtrl(Tab.Props, "Crystal 色散 Dispersion",      "Display_Crystal",      "_DispersionAmount",  0f, 0.05f);
            AddCtrl(Tab.Props, "Crystal 折射强度 Refraction",   "Display_Crystal",      "_RefractionStrength",0f, 0.15f);
            AddCtrl(Tab.Props, "Crystal 脉冲速度 PulseSpeed",   "Display_Crystal",      "_PulseSpeed",        0.1f, 5f);
            AddCtrl(Tab.Props, "Crystal 透光强度 Transmission", "Display_Crystal",      "_TransIntensity",    0f, 4f);
            AddCtrl(Tab.Props, "Metal 受热强度 Heat",          "Display_AlchemyMetal", "_HeatIntensity",     0f, 8f);
            AddCtrl(Tab.Props, "Metal 各向异性偏移 AnisoShift", "Display_AlchemyMetal", "_AnisoShift",       -1f, 1f);
            AddCtrl(Tab.Props, "Metal Matcap 强度",            "Display_AlchemyMetal", "_MatcapIntensity",   0f, 2f);
            AddCtrl(Tab.Props, "Metal 磨损强度 Worn",          "Display_AlchemyMetal", "_WornIntensity",     0f, 1f);
            AddCtrl(Tab.Props, "Food 边缘光 Rim",              "Display_AnimeFood",    "_RimIntensity",      0f, 2f);
            AddCtrl(Tab.Props, "Food 高光阈值 SpecThreshold",  "Display_AnimeFood",    "_SpecThreshold",     0f, 1f);
            AddCtrl(Tab.Props, "Food 阴影边界 ShadowEdge",     "Display_AnimeFood",    "_ShadowEdge",        0f, 0.5f);

            // ── 环境材质（多暴露一些）──────────────────────────────────
            AddCtrl(Tab.Env, "Stone 纹理密度 Tiling",      "Display_StoneFloor", "_Tiling",          0.1f, 4f);
            AddCtrl(Tab.Env, "Stone 三平面锐度 Blend",      "Display_StoneFloor", "_BlendSharpness",  1f, 8f);
            AddCtrl(Tab.Env, "Stone 法线强度 Normal",       "Display_StoneFloor", "_NormalStrength",  0f, 2f);
            AddCtrl(Tab.Env, "Stone 湿润高光 Wet",          "Display_StoneFloor", "_WetIntensity",    0f, 2f);
            AddCtrl(Tab.Env, "Stone 湿润阈值 WetThreshold", "Display_StoneFloor", "_WetThreshold",    0f, 1f);
            AddCtrl(Tab.Env, "Stone 边缘光 Rim",            "Display_StoneFloor", "_RimIntensity",    0f, 1.5f);

            AddCtrl(Tab.Env, "Wood 木纹高光 Grain",         "Display_Wood", "_GrainIntensity",      0f, 1.5f);
            AddCtrl(Tab.Env, "Wood 高光阈值 GrainThresh",   "Display_Wood", "_GrainThreshold",      0f, 1f);
            AddCtrl(Tab.Env, "Wood 高光锐度 GrainShin",     "Display_Wood", "_GrainShininess",      10f, 200f);
            AddCtrl(Tab.Env, "Wood 阴影边界 ShadowEdge",    "Display_Wood", "_ShadowEdge",          0f, 0.3f);
            AddCtrl(Tab.Env, "Wood 阴影阈值 ShadowThresh",  "Display_Wood", "_ShadowRampThreshold", 0f, 1f);
            AddCtrl(Tab.Env, "Wood 细节法线 DetailNormal",  "Display_Wood", "_DetailNormalStrength",0f, 2f);
            AddCtrl(Tab.Env, "Wood 细节密度 DetailTiling",  "Display_Wood", "_DetailTiling",        1f, 8f);

            AddCtrl(Tab.Env, "Fabric 经线强度 Warp",        "Display_Fabric", "_WarpIntensity",  0f, 2f);
            AddCtrl(Tab.Env, "Fabric 纬线强度 Weft",        "Display_Fabric", "_WeftIntensity",  0f, 2f);
            AddCtrl(Tab.Env, "Fabric 经线锐度 WarpShin",    "Display_Fabric", "_WarpShininess",  1f, 256f);
            AddCtrl(Tab.Env, "Fabric 纬线锐度 WeftShin",    "Display_Fabric", "_WeftShininess",  1f, 256f);
            AddCtrl(Tab.Env, "Fabric 经线阈值 WarpThresh",  "Display_Fabric", "_WarpThreshold",  0f, 1f);
            AddCtrl(Tab.Env, "Fabric 阴影边界 ShadowEdge",  "Display_Fabric", "_ShadowEdge",     0f, 0.4f);
            AddCtrl(Tab.Env, "Fabric 边缘光 Rim",           "Display_Fabric", "_RimIntensity",   0f, 2f);

            // ── FX ─────────────────────────────────────────────────────
            AddCtrl(Tab.FX, "Dissolve 边缘宽度 EdgeWidth",   "Display_Dissolve",     "_EdgeWidth",        0.01f, 0.3f);
            AddCtrl(Tab.FX, "Dissolve 边缘亮度 EdgeIntensity","Display_Dissolve",    "_EdgeIntensity",    0f, 8f);
            AddCtrl(Tab.FX, "Dissolve 热浪扭曲 Distort",     "Display_Dissolve",     "_DistortStrength",  0f, 0.05f);
            AddCtrl(Tab.FX, "Dissolve 噪声密度 NoiseTiling", "Display_Dissolve",     "_NoiseTiling",      0.5f, 8f);
            AddCtrl(Tab.FX, "Rune 流速 FlowSpeed",          "Display_RuneConveyor", "_FlowSpeed",       -3f, 3f);
            AddCtrl(Tab.FX, "Rune 符文亮度 RuneIntensity",   "Display_RuneConveyor", "_RuneIntensity",    0f, 4f);
            AddCtrl(Tab.FX, "Rune 脉冲速度 PulseSpeed",      "Display_RuneConveyor", "_PulseSpeed",       0.1f, 5f);
            AddCtrl(Tab.FX, "Rune 脉冲深度 PulseDepth",      "Display_RuneConveyor", "_PulseDepth",       0f, 1f);
            AddCtrl(Tab.FX, "Rune 粒子强度 Particle",        "Display_RuneConveyor", "_ParticleIntensity",0f, 4f);
            AddCtrl(Tab.FX, "Rune 粒子密度 Density",         "Display_RuneConveyor", "_ParticleDensity",  1f, 20f);
        }

        void AddCtrl(Tab tab, string label, string goName, string prop, float min, float max)
        {
            var go = GameObject.Find(goName);
            if (go == null) return;
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var m = r.material; // 实例化
            int id = Shader.PropertyToID(prop);
            if (!m.HasProperty(id)) return;
            float v = m.GetFloat(id);
            _ctrls.Add(new Ctrl { tab = tab, label = label, mat = m, id = id, min = min, max = max,
                                  val = v, text = v.ToString("0.###") });
        }

        void OnDisable()
        {
            if (!_ssOutlineOn) SetGlobalOutline(true); // 退出恢复全局描边，避免改坏 Renderer 资产
        }

        // 反射拿到当前 URP Renderer 上的 OutlineRendererFeature 并开关
        static void SetGlobalOutline(bool on)
        {
            var rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (rp == null) return;
            var field = typeof(UniversalRenderPipelineAsset).GetField(
                "m_RendererDataList",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field == null) return;
            var dataList = field.GetValue(rp) as ScriptableRendererData[];
            if (dataList == null) return;
            foreach (var data in dataList)
            {
                if (data == null) continue;
                foreach (var feature in data.rendererFeatures)
                    if (feature != null && feature.GetType().Name == "OutlineRendererFeature")
                        feature.SetActive(on);
            }
        }

        void OnGUI()
        {
            if (GUI.Button(new Rect(10, 10, 130, 28), _show ? "▼ 隐藏面板" : "▶ 参数面板"))
                _show = !_show;
            if (!_show) return;

            GUILayout.BeginArea(new Rect(10, 44, 370, 540), GUI.skin.box);

            // 顶部分页
            _tab = (Tab)GUILayout.Toolbar((int)_tab, TabNames);
            GUILayout.Space(6);

            _scroll = GUILayout.BeginScrollView(_scroll);
            if (_tab == Tab.Global) DrawGlobal();
            else                    DrawTab(_tab);
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        // 全局风格页：转台 / 描边宽度 / 全局屏幕空间描边
        void DrawGlobal()
        {
            float prev = _turntableSpeed;
            _turntableSpeed = ValueRow("转台速度 (°/s)", _turntableSpeed, 0f, 90f, ref _turntableText);
            if (!Mathf.Approximately(_turntableSpeed, prev))
                foreach (var t in _turntables) if (t != null) t.degreesPerSecond = _turntableSpeed;

            prev = _outlineWidth;
            _outlineWidth = ValueRow("背面描边宽度 (px)", _outlineWidth, 0f, 5f, ref _outlineText);
            if (!Mathf.Approximately(_outlineWidth, prev))
                foreach (var m in _outlineMats)
                    if (m.HasProperty("_OutlineWidth")) m.SetFloat("_OutlineWidth", _outlineWidth);

            GUILayout.Space(6);
            bool so = GUILayout.Toggle(_ssOutlineOn, " 全局屏幕空间描边 (ScreenSpaceOutline)");
            if (so != _ssOutlineOn) { _ssOutlineOn = so; SetGlobalOutline(so); }
        }

        // 某个分页：先画该页特殊控件（FX 的溶解开关），再画材质参数行
        void DrawTab(Tab tab)
        {
            if (tab == Tab.FX && _dissolve != null)
            {
                bool auto = GUILayout.Toggle(_dissolveAuto, " Dissolve 自动循环");
                if (auto != _dissolveAuto) { _dissolveAuto = auto; _dissolve.autoPlay = auto; }
                if (!_dissolveAuto && _dissolve.Mat != null)
                {
                    float cur = _dissolve.Mat.GetFloat("_DissolveAmount");
                    float nd = ValueRow("溶解量 DissolveAmount", cur, 0f, 1f, ref _dissolveText);
                    if (!Mathf.Approximately(nd, cur)) _dissolve.SetManual(nd);
                }
                GUILayout.Space(6);
            }

            foreach (var c in _ctrls)
            {
                if (c.tab != tab) continue;
                float prev = c.val;
                c.val = ValueRow(c.label, c.val, c.min, c.max, ref c.text);
                if (!Mathf.Approximately(c.val, prev)) c.mat.SetFloat(c.id, c.val);
            }
        }

        // 一行参数：标签 + 滑条 + 数值输入框（两者联动，输入框可精确输入）
        float ValueRow(string label, float val, float min, float max, ref string buf)
        {
            GUILayout.Label(label);
            GUILayout.BeginHorizontal();

            // 滑条（拖动时同步输入框文本）
            float sv = GUILayout.HorizontalSlider(val, min, max);
            if (!Mathf.Approximately(sv, val)) { val = sv; buf = sv.ToString("0.###"); }

            // 输入框：未编辑时显示实时值；编辑时按输入解析（限制在 min~max）
            string shown = string.IsNullOrEmpty(buf) ? val.ToString("0.###") : buf;
            string nt = GUILayout.TextField(shown, GUILayout.Width(64));
            if (nt != shown)
            {
                buf = nt;
                float pv;
                if (float.TryParse(nt, System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out pv))
                    val = Mathf.Clamp(pv, min, max);
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(3);
            return val;
        }
    }
}

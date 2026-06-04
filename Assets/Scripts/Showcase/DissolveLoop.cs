using UnityEngine;

namespace GourmetLine.Showcase
{
    /// <summary>
    /// 溶解循环驱动：用时间三角波（PingPong）让 _DissolveAmount 在 0↔1 之间来回，
    /// 录一段就能捕捉"溶解消失 → 重新出现"的完整过程。
    ///
    /// 关键点：用 renderer.material（实例化材质）而非 sharedMaterial，
    /// 这样运行时改参数不会污染磁盘上的 .mat 资产。
    /// 这演示的就是"Shader 暴露参数 + C# 运行时驱动"的能力。
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class DissolveLoop : MonoBehaviour
    {
        [Tooltip("要驱动的 Shader 属性名")]
        public string property = "_DissolveAmount";

        [Tooltip("一个完整来回（0→1→0）所需秒数")]
        public float period = 4f;

        [Tooltip("是否自动循环；关闭后可由 UI 滑条手动控制")]
        public bool autoPlay = true;

        Material _mat;
        int _id;

        public Material Mat => _mat;

        void Start()
        {
            _mat = GetComponent<Renderer>().material; // 实例化，避免改到资产
            _id = Shader.PropertyToID(property);
        }

        void Update()
        {
            if (!autoPlay || _mat == null) return;
            // PingPong(t,1) 产生 0→1→0 的三角波；乘 2/period 让一个来回耗时 period 秒
            float v = Mathf.PingPong(Time.time * (2f / Mathf.Max(0.01f, period)), 1f);
            _mat.SetFloat(_id, v);
        }

        /// <summary>UI 手动模式下直接设值</summary>
        public void SetManual(float v)
        {
            if (_mat != null) _mat.SetFloat(_id, v);
        }
    }
}

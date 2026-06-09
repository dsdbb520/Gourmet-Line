using UnityEngine;
using UnityEngine.Events;

namespace GourmetLine.Showcase
{
    /// <summary>
    /// VAT（顶点动画贴图）一次性播放驱动。
    ///
    /// ── 它解决什么问题 ──
    /// SideFX Labs 的 VAT Shader 默认把 time 输入接 ShaderGraph 的 Time 节点，
    /// 配合内部 frac(speed*time) 会无限循环（适合管道流体这种环境动画）。
    /// 但"液体倾倒""晶体生长"这类只该播一次的动作，需要外部精确控制播放头：
    /// 从 0 推到 1 走完整段动画，然后停住。
    ///
    /// ── 使用前提（ShaderGraph 端要做的事）──
    /// 在 VAT_LiquidPour.shadergraph 里：
    ///   1) 把喂给 VAT 子图 time 端口的 Time 节点，换成一个 Float 属性 _VATTime；
    ///   2) Speed 设为 1（这样 _VATTime 0→1 正好对应一整段动画一次）；
    ///   3) 关闭 Shader 内部任何 frac/循环（一次性播放不需要折叠）。
    /// 之后本脚本只负责把 _VATTime 在 duration 秒内从 0 线性推到 1。
    ///
    /// ── 为什么用 MaterialPropertyBlock 而不是 renderer.material ──
    /// MaterialPropertyBlock(MPB) 是"逐渲染器的属性覆盖"，不会实例化一份新材质：
    ///   - 不产生材质副本 → 不增加 Draw Call 批次断裂，对 GC 友好；
    ///   - 同一个共享材质可被多个 VATPlayer 各自驱动到不同播放进度；
    ///   - 不污染磁盘上的 .mat 资产。
    /// 这正是 TA 面试常考的点（“运行时改 Shader 参数，你会怎么改？”），
    /// 和 DissolveLoop 里用 renderer.material 的简单做法形成对照。
    /// 注意：VAT 的位移发生在顶点着色器，MPB 设置的 float 顶点阶段一样读得到。
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class VATPlayer : MonoBehaviour
    {
        [Tooltip("Shader 里驱动播放头的归一化时间属性（0=第一帧，1=最后一帧）")]
        public string timeProperty = "_VATTime";

        [Tooltip("播完一整段动画所需秒数。应≈ 导出帧数 / 24FPS，太快看不清流动")]
        public float duration = 2.5f;

        [Tooltip("游戏启动即播一次（用于展示场景自动演示）")]
        public bool playOnAwake = false;

        [Tooltip("循环播放：倾倒/生长一般关；沸腾/蒸汽等环境循环可开")]
        public bool loop = false;

        [Tooltip("播完后停在最后一帧（true）还是弹回第一帧（false）")]
        public bool holdLastFrame = true;

        [Range(0f, 1f)]
        [Tooltip("起始播放头位置，一般为 0")]
        public float startNormalized = 0f;

        [Tooltip("播放完成时触发——用于串联下一段效果（Task 4.3 炼金序列）")]
        public UnityEvent onComplete;

        Renderer _renderer;
        MaterialPropertyBlock _mpb;
        int _id;
        float _elapsed;     // 已播放秒数
        bool _playing;

        /// <summary>当前归一化播放进度（0~1），供外部读取/UI 显示</summary>
        public float Normalized => duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / duration);
        public bool IsPlaying => _playing;

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
            _id = Shader.PropertyToID(timeProperty);

            Apply(startNormalized);          // 初始定格在起始帧
            if (playOnAwake) PlayOnce();
        }

        /// <summary>从头（startNormalized）开始播放一次</summary>
        public void PlayOnce()
        {
            _elapsed = startNormalized * duration;
            _playing = true;
        }

        /// <summary>停止播放，停在当前帧</summary>
        public void Stop() => _playing = false;

        /// <summary>暂停并手动定格到指定进度（UI 滑条调试用）</summary>
        public void SetManual(float normalized)
        {
            _playing = false;
            Apply(Mathf.Clamp01(normalized));
        }

        void Update()
        {
            if (!_playing) return;

            _elapsed += Time.deltaTime;
            float n = duration <= 0f ? 1f : _elapsed / duration;

            if (n >= 1f)
            {
                if (loop)
                {
                    // 用减法而非置零，保留超出的零头，循环更平滑
                    _elapsed -= duration;
                    n = _elapsed / duration;
                }
                else
                {
                    n = holdLastFrame ? 1f : 0f;
                    Apply(n);
                    _playing = false;
                    onComplete?.Invoke();    // 通知监听者：本段播放结束
                    return;
                }
            }

            Apply(n);
        }

        // 把归一化播放头写进材质属性块（不实例化材质）
        void Apply(float normalized)
        {
            _renderer.GetPropertyBlock(_mpb);   // 先取出当前块，避免覆盖其它已设属性
            _mpb.SetFloat(_id, normalized);
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}

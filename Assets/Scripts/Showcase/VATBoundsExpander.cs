using UnityEngine;

namespace GourmetLine.Showcase
{
    /// <summary>
    /// 修复 VAT（顶点动画贴图）的视锥剔除问题。
    ///
    /// VAT 在顶点着色器里把顶点推到新位置（GPU 端），但 Unity 在 CPU 端用网格
    /// 原始的包围盒(bounds)做视锥剔除。若 bounds 偏小/偏在一端，当那一端移出
    /// 画面，Unity 会误判整个物体不可见而剔除 → 流体凭空消失。
    ///
    /// 解决：运行时把网格实例的 bounds 强制放大，使其在任意角度都被判为可见。
    /// 用 mf.mesh（实例）而非 sharedMesh，避免修改磁盘上的网格资产。
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public class VATBoundsExpander : MonoBehaviour
    {
        [Tooltip("强制包围盒的边长（本地空间，米）。取大于流体运动范围即可。")]
        public float boundsSize = 30f;

        void Start()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            // 直接改 sharedMesh 的 bounds（只是元数据，不读取顶点）。
            // 关键：不要用 mf.mesh —— 它会克隆网格的 CPU 数据，而 build 中网格默认
            // Read/Write Disabled，CPU 数据已丢弃，克隆出来的顶点是乱的 → 流体炸开。
            // bounds 是网格头部信息，即使不可读也能设置，所以这样在 build 里也安全。
            var mesh = mf.sharedMesh;
            mesh.bounds = new Bounds(mesh.bounds.center, Vector3.one * boundsSize);
        }
    }
}

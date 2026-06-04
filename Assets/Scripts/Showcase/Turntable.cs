using UnityEngine;

namespace GourmetLine.Showcase
{
    /// <summary>
    /// 展示台转盘：让物体缓慢自转。
    /// 视角相关效果（各向异性高光 / Matcap / 菲涅尔 / 折射 / 色散）
    /// 只有转起来才能在录屏里完整体现，所以每个展示物都挂这个。
    /// </summary>
    public class Turntable : MonoBehaviour
    {
        [Tooltip("每秒旋转角度（°/s）")]
        public float degreesPerSecond = 20f;

        [Tooltip("旋转轴，默认绕世界 Y 轴")]
        public Vector3 axis = Vector3.up;

        void Update()
        {
            transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.World);
        }
    }
}

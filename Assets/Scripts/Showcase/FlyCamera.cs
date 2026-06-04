using UnityEngine;

namespace GourmetLine.Showcase
{
    /// <summary>
    /// 运行时自由飞行相机（仅展示用）。
    ///   · 按住鼠标右键 → 移动鼠标转视角
    ///   · WASD 前后左右，E/Q 上升/下降
    ///   · 按住 Shift 加速
    /// 用经典 Input API（项目 activeInputHandler=Both，可用）。
    /// </summary>
    public class FlyCamera : MonoBehaviour
    {
        [Tooltip("移动速度 (米/秒)")]      public float moveSpeed = 6f;
        [Tooltip("Shift 加速倍数")]        public float boostMultiplier = 3f;
        [Tooltip("鼠标转视角灵敏度")]      public float lookSensitivity = 2.5f;

        float _yaw, _pitch;

        void Start()
        {
            var e = transform.eulerAngles;
            _yaw = e.y;
            _pitch = e.x;
        }

        void Update()
        {
            // 仅在按住右键时转视角，避免误操作
            if (Input.GetMouseButtonDown(1)) Cursor.lockState = CursorLockMode.Locked;
            if (Input.GetMouseButtonUp(1))   Cursor.lockState = CursorLockMode.None;

            if (Input.GetMouseButton(1))
            {
                _yaw   += Input.GetAxis("Mouse X") * lookSensitivity;
                _pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
                _pitch = Mathf.Clamp(_pitch, -89f, 89f);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }

            float speed = moveSpeed *
                (Input.GetKey(KeyCode.LeftShift) ? boostMultiplier : 1f);

            Vector3 dir = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) dir += Vector3.forward;
            if (Input.GetKey(KeyCode.S)) dir += Vector3.back;
            if (Input.GetKey(KeyCode.A)) dir += Vector3.left;
            if (Input.GetKey(KeyCode.D)) dir += Vector3.right;
            if (Input.GetKey(KeyCode.E)) dir += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) dir += Vector3.down;

            // 方向相对相机自身朝向
            transform.Translate(dir.normalized * speed * Time.deltaTime, Space.Self);
        }
    }
}

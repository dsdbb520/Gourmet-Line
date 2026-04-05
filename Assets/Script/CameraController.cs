using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;

public class CameraController : MonoBehaviour
{
    [Header("References")]
    public CinemachineVirtualCamera vcam;

    [Header("Movement Settings")]
    public float panSpeed = 20f;
    public float rotationSpeed = 0.5f;
    
    [Header("Zoom Settings")]
    public float zoomSpeed = 5f;
    public float minZoomDistance = 5f;
    public float maxZoomDistance = 40f;

    // 动态创建的 Input Actions
    private InputAction panAction;
    private InputAction rotateAction;
    private InputAction rotateModifierAction;
    private InputAction zoomAction;

    private CinemachineTransposer transposer;
    
    // 记录当前的缩放距离和固定的视角斜角方向
    private float currentZoomDistance;
    private Vector3 normalizedZoomDirection;

    private void Awake()
    {
        // 初始化平移 Action (支持 WASD 和 上下左右箭头)
        panAction = new InputAction("Pan");
        panAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d")
            .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");

        // 初始化旋转 Action (获取鼠标 X 轴移动量)
        rotateAction = new InputAction("Rotate", binding: "<Mouse>/delta/x");

        // 修改为右键触发旋转
        rotateModifierAction = new InputAction("RotateMod", binding: "<Mouse>/rightButton");

        // 初始化缩放 Action (鼠标滚轮 Y 轴)
        zoomAction = new InputAction("Zoom", binding: "<Mouse>/scroll/y");

        // 启用所有的 Actions
        panAction.Enable();
        rotateAction.Enable();
        rotateModifierAction.Enable();
        zoomAction.Enable();
    }

    private void Start()
    {
        // 获取 Cinemachine 的 Transposer 组件，用于修改镜头距离 (缩放)
        if (vcam != null)
        {
            transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
            
            // 记录初始的偏移方向，保证缩放始终沿着这个斜角方向，避免原地垂直上下
            normalizedZoomDirection = transposer.m_FollowOffset.normalized;
            currentZoomDistance = transposer.m_FollowOffset.magnitude;
        }
    }

    private void Update()
    {
        HandleMovement();
        HandleRotation();
        HandleZoom();
    }

    private void HandleMovement()
    {
        Vector2 input = panAction.ReadValue<Vector2>();
        
        // 移动方向参考主摄像机的实际朝向，确保按 W 始终向屏幕上方移动
        Vector3 camForward = Camera.main.transform.forward;
        Vector3 camRight = Camera.main.transform.right;
        
        // 消除 Y 轴影响，确保平移始终在 XZ 平面上
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();
        
        Vector3 moveDir = camForward * input.y + camRight * input.x;
        
        transform.position += moveDir * panSpeed * Time.deltaTime;
    }

    private void HandleRotation()
    {
        // 只有当按住鼠标右键时，才允许旋转
        if (rotateModifierAction.IsPressed())
        {
            float rotateValue = rotateAction.ReadValue<float>();
            transform.Rotate(Vector3.up, rotateValue * rotationSpeed, Space.World);
        }
    }

    private void HandleZoom()
    {
        if (transposer == null) return;

        float zoomValue = zoomAction.ReadValue<float>();
        
        // 滚轮有输入时才计算
        if (Mathf.Abs(zoomValue) > 0.1f)
        {
            // 标准化滚轮输入方向，计算出目标距离
            float zoomAmount = Mathf.Sign(zoomValue) * zoomSpeed;
            currentZoomDistance -= zoomAmount;
            
            // 限制缩放距离的极值
            currentZoomDistance = Mathf.Clamp(currentZoomDistance, minZoomDistance, maxZoomDistance);
        }

        // 使用初始的斜角方向乘以当前距离，进行插值平滑过渡
        Vector3 targetOffset = normalizedZoomDirection * currentZoomDistance;
        transposer.m_FollowOffset = Vector3.Lerp(transposer.m_FollowOffset, targetOffset, Time.deltaTime * 10f);
    }

    private void OnDestroy()
    {
        // 清理内存
        panAction.Disable();
        rotateAction.Disable();
        rotateModifierAction.Disable();
        zoomAction.Disable();
    }
}
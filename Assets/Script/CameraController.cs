using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;

public class CameraController : MonoBehaviour
{
    [Header("References")]
    public CinemachineVirtualCamera vcam;

    [Header("Input References")]
    public InputActionReference panActionRef;
    public InputActionReference rotateActionRef;
    public InputActionReference rotateModifierActionRef;
    public InputActionReference zoomActionRef;

    [Header("Movement Settings")]
    public float panSpeed = 20f;

    [Header("Rotation Settings")]
    public float rotationSpeed = 0.2f;
    public float minPitch = 10f;
    public float maxPitch = 85f;

    // 内部缓存当前的旋转角度
    private float currentYaw = 0f;
    private float currentPitch = 0f;

    [Header("Zoom Settings")]
    public float zoomSpeed = 5f;
    public float minZoomDistance = 5f;
    public float maxZoomDistance = 40f;

    private CinemachineTransposer transposer;
    private float currentZoomDistance;
    private Vector3 normalizedZoomDirection;


    private void OnEnable()
    {
        if (panActionRef != null) panActionRef.action.Enable();
        if (rotateActionRef != null) rotateActionRef.action.Enable();
        if (rotateModifierActionRef != null) rotateModifierActionRef.action.Enable();
        if (zoomActionRef != null) zoomActionRef.action.Enable();
    }
    private void OnDisable()
    {
        if (panActionRef != null) panActionRef.action.Disable();
        if (rotateActionRef != null) rotateActionRef.action.Disable();
        if (rotateModifierActionRef != null) rotateModifierActionRef.action.Disable();
        if (zoomActionRef != null) zoomActionRef.action.Disable();
    }


    private void Start()
    {
        if (vcam != null)
        {
            transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
            normalizedZoomDirection = transposer.m_FollowOffset.normalized;
            currentZoomDistance = transposer.m_FollowOffset.magnitude;
        }

        Vector3 angles = transform.eulerAngles;
        currentPitch = angles.x > 180f ? angles.x - 360f : angles.x;
        currentYaw = angles.y;
    }

    private void Update()
    {
        HandleMovement();
        HandleRotation();
        HandleZoom();
    }

    private void HandleMovement()
    {
        Vector2 input = panActionRef.action.ReadValue<Vector2>();

        Vector3 camForward = Camera.main.transform.forward;
        Vector3 camRight = Camera.main.transform.right;

        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = camForward * input.y + camRight * input.x;
        transform.position += moveDir * panSpeed * Time.deltaTime;
    }

    private void HandleRotation()
    {
        if (rotateModifierActionRef.action.IsPressed())
        {
            Vector2 rotateDelta = rotateActionRef.action.ReadValue<Vector2>();

            currentYaw += rotateDelta.x * rotationSpeed;
            currentPitch -= rotateDelta.y * rotationSpeed;
            currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);

            transform.rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        }
    }

    private void HandleZoom()
    {
        if (transposer == null) return;

        float zoomValue = zoomActionRef.action.ReadValue<float>();

        if (Mathf.Abs(zoomValue) > 0.1f)
        {
            float zoomAmount = Mathf.Sign(zoomValue) * zoomSpeed;
            currentZoomDistance -= zoomAmount;
            currentZoomDistance = Mathf.Clamp(currentZoomDistance, minZoomDistance, maxZoomDistance);
        }

        Vector3 targetOffset = normalizedZoomDirection * currentZoomDistance;
        transposer.m_FollowOffset = Vector3.Lerp(transposer.m_FollowOffset, targetOffset, Time.deltaTime * 10f);
    }
}
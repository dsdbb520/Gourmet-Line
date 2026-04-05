using UnityEngine;
using UnityEngine.InputSystem;

public class BuildingController : MonoBehaviour
{
    [Header("References")]
    public Transform buildingGhost; 
    public LayerMask groundLayer;   

    [Header("Building Placement")]
    public Transform buildingPrefab;      
    public Material ghostValidMat;        
    public Material ghostInvalidMat;      

    private MeshRenderer ghostRenderer;   
    private Vector3Int currentGridPosition;
    private Transform buildingContainer;

    // 记录当前的旋转角度和方向
    private int currentRotationAngle = 0;
    private Direction currentDirection = Direction.Up;

    private InputAction rotateBuildingAction;

    void Awake()
    {
        // 初始化 R 键旋转 Action
        rotateBuildingAction = new InputAction("RotateBuilding", binding: "<Keyboard>/r");
        rotateBuildingAction.Enable();
    }

    void Start()
    {
        ghostRenderer = buildingGhost.GetComponent<MeshRenderer>();
        buildingContainer = new GameObject("BuildingsContainer").transform;
    }

    void Update()
    {
        HandleRotationInput(); // 监听旋转输入
        HandleMouseSnapping();
        HandleLeftClick();
    }

    private void HandleRotationInput()
    {
        // 每次按下 R 键时触发，顺时针旋转 90 度
        if (rotateBuildingAction.triggered)
        {
            currentRotationAngle = (currentRotationAngle + 90) % 360;
            buildingGhost.eulerAngles = new Vector3(0, currentRotationAngle, 0);

            // 根据角度更新方向枚举
            UpdateDirectionFromAngle();
        }
    }

    private void UpdateDirectionFromAngle()
    {
        // 映射角度到枚举
        switch (currentRotationAngle)
        {
            case 0: currentDirection = Direction.Up; break;
            case 90: currentDirection = Direction.Right; break;
            case 180: currentDirection = Direction.Down; break;
            case 270: currentDirection = Direction.Left; break;
        }
    }

    private void HandleMouseSnapping()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 1000f, groundLayer))
        {
            if (!buildingGhost.gameObject.activeSelf)
                buildingGhost.gameObject.SetActive(true);

            currentGridPosition = GridManager.Instance.GetGridPosition(hit.point);
            Vector3 targetWorldPos = GridManager.Instance.GetWorldPosition(currentGridPosition);
            targetWorldPos.y = 0.5f; 
            buildingGhost.position = targetWorldPos;

            bool isOccupied = GridManager.Instance.IsGridOccupied(currentGridPosition);
            ghostRenderer.material = isOccupied ? ghostInvalidMat : ghostValidMat;
        }
        else
        {
            if (buildingGhost.gameObject.activeSelf)
                buildingGhost.gameObject.SetActive(false);
        }
    }

    private void HandleLeftClick()
    {
        bool isLeftClick = Mouse.current.leftButton.wasPressedThisFrame;

        if (isLeftClick && buildingGhost.gameObject.activeSelf)
        {
            if (!GridManager.Instance.IsGridOccupied(currentGridPosition))
            {
                // 实例化时应用当前的旋转角度
                Transform newBuilding = Instantiate(buildingPrefab, buildingGhost.position, Quaternion.Euler(0, currentRotationAngle, 0));
                newBuilding.SetParent(buildingContainer);
                
                // 将方向信息写入建筑身上的 BuildingData 组件
                BuildingData data = newBuilding.GetComponent<BuildingData>();
                if (data != null) data.layoutDirection = currentDirection;

                GridManager.Instance.PlaceBuilding(currentGridPosition, newBuilding);
            }
        }
    }

    private void OnDestroy()
    {
        rotateBuildingAction.Disable();
    }
}
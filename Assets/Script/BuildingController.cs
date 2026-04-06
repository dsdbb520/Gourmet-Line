using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class BuildingController : MonoBehaviour
{
    [Header("References")]
    public LayerMask groundLayer;   

    [Header("Building Library")]
    public List<Transform> buildingPrefabs;
    public Material ghostValidMat;        
    public Material ghostInvalidMat;      

    private int selectedIndex = 0;
    
    private GameObject currentGhostObj;
    private Renderer[] currentGhostRenderers;
    private bool isCurrentlyValid = true; // 状态缓存，用于优化材质切换频率

    private Vector3Int currentGridPosition;
    private Transform buildingContainer;

    private int currentRotationAngle = 0;
    private Direction currentDirection = Direction.Up;

    private InputAction rotateBuildingAction;
    private InputAction deleteBuildingAction;
    private InputAction selectAction; 

    void Awake()
    {
        rotateBuildingAction = new InputAction("RotateBuilding", binding: "<Keyboard>/r");
        deleteBuildingAction = new InputAction("DeleteBuilding", binding: "<Keyboard>/x");
        
        selectAction = new InputAction("SelectBuilding", binding: "<Keyboard>/1");
        selectAction.AddBinding("<Keyboard>/2");

        rotateBuildingAction.Enable();
        deleteBuildingAction.Enable();
        selectAction.Enable();
    }

    void Start()
    {
        buildingContainer = new GameObject("BuildingsContainer").transform;
        
        // 游戏开始时，生成默认索引对应的建筑虚影
        SpawnDynamicGhost();
    }

    void Update()
    {
        HandleSelectionInput();
        HandleRotationInput();
        HandleMouseSnapping();
        HandleLeftClick();
        HandleDeleteInput();
    }

    private void HandleSelectionInput()
    {
        bool changed = false;
        if (Keyboard.current.digit1Key.wasPressedThisFrame) { selectedIndex = 0; changed = true; }
        if (Keyboard.current.digit2Key.wasPressedThisFrame) { selectedIndex = 1; changed = true; }
        
        // 当切换了选择的机器时，重新生成对应的虚影模型
        if (changed)
        {
            SpawnDynamicGhost();
        }
    }

    private void SpawnDynamicGhost()
    {
        // 销毁旧的虚影
        if (currentGhostObj != null) Destroy(currentGhostObj);

        // 实例化当前选中的Prefab作为新的虚影
        currentGhostObj = Instantiate(buildingPrefabs[selectedIndex].gameObject);
        
        // 剥离虚影上的物理碰撞体
        foreach (var col in currentGhostObj.GetComponentsInChildren<Collider>()) 
        {
            Destroy(col);
        }
        // 剥离所有逻辑脚本，防止虚影自己开始生产或报错
        foreach (var comp in currentGhostObj.GetComponentsInChildren<MonoBehaviour>()) 
        {
            Destroy(comp);
        }

        // 缓存所有的Renderer，并强制刷一次材质
        currentGhostRenderers = currentGhostObj.GetComponentsInChildren<Renderer>();
        isCurrentlyValid = true; 
        UpdateGhostMaterial(true);

        // 同步当前的旋转角度
        currentGhostObj.transform.eulerAngles = new Vector3(0, currentRotationAngle, 0);
    }

    private void UpdateGhostMaterial(bool isValid)
    {
        Material targetMat = isValid ? ghostValidMat : ghostInvalidMat;
        
        foreach (var rnd in currentGhostRenderers)
        {
            // 处理包含多个Submesh的复杂模型，确保所有部分都被替换
            Material[] mats = new Material[rnd.materials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = targetMat;
            rnd.materials = mats;
        }
    }

    private void HandleRotationInput()
    {
        if (rotateBuildingAction.triggered)
        {
            currentRotationAngle = (currentRotationAngle + 90) % 360;
            if (currentGhostObj != null)
            {
                currentGhostObj.transform.eulerAngles = new Vector3(0, currentRotationAngle, 0);
            }
            UpdateDirectionFromAngle();
        }
    }

    private void UpdateDirectionFromAngle()
    {
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
        if (currentGhostObj == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 1000f, groundLayer))
        {
            if (!currentGhostObj.activeSelf) currentGhostObj.SetActive(true);

            currentGridPosition = GridManager.Instance.GetGridPosition(hit.point);
            Vector3 targetWorldPos = GridManager.Instance.GetWorldPosition(currentGridPosition);
            targetWorldPos.y = 0.5f; 
            currentGhostObj.transform.position = targetWorldPos;

            // 检查占用并优化材质替换开销
            bool isOccupied = GridManager.Instance.IsGridOccupied(currentGridPosition);
            bool isValid = !isOccupied;

            if (isValid != isCurrentlyValid)
            {
                UpdateGhostMaterial(isValid);
                isCurrentlyValid = isValid;
            }
        }
        else
        {
            if (currentGhostObj.activeSelf) currentGhostObj.SetActive(false);
        }
    }

    private void HandleLeftClick()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame && currentGhostObj != null && currentGhostObj.activeSelf)
        {
            if (!GridManager.Instance.IsGridOccupied(currentGridPosition))
            {
                Transform newBuilding = Instantiate(buildingPrefabs[selectedIndex], currentGhostObj.transform.position, Quaternion.Euler(0, currentRotationAngle, 0));
                newBuilding.SetParent(buildingContainer);
                
                BuildingData data = newBuilding.GetComponent<BuildingData>();
                if (data != null) 
                {
                    data.layoutDirection = currentDirection;
                    GridManager.Instance.PlaceBuilding(currentGridPosition, data);
                }
            }
        }
    }

    private void HandleDeleteInput()
    {
        if (deleteBuildingAction.triggered && GridManager.Instance.IsGridOccupied(currentGridPosition))
        {
            GridManager.Instance.RemoveBuilding(currentGridPosition);
        }
    }

    private void OnDestroy()
    {
        rotateBuildingAction.Disable();
        deleteBuildingAction.Disable();
        selectAction.Disable();
    }
}
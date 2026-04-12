using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class BuildingController : MonoBehaviour
{
    [Header("References")]
    public LayerMask groundLayer;   

    [Header("Building Library")]
    public List<Transform> buildingPrefabs;
   

    private int selectedIndex = 0;
    
    private GameObject currentGhostObj;
    private Renderer[] currentGhostRenderers;
    private bool isCurrentlyValid = true; // 状态缓存，用于优化材质切换频率

    private Vector3Int currentGridPosition;
    private Transform buildingContainer;

    private int currentRotationAngle = 0;
    private Direction currentDirection = Direction.Up;

    [Header("Input References")]
    public InputActionReference rotateBuildingActionRef;
    public InputActionReference deleteBuildingActionRef;
    public InputActionReference selectBuildingActionRef;

    private void OnEnable()
    {
        if (rotateBuildingActionRef != null) rotateBuildingActionRef.action.Enable();
        if (deleteBuildingActionRef != null) deleteBuildingActionRef.action.Enable();
        if (selectBuildingActionRef != null) selectBuildingActionRef.action.Enable();

        if (selectBuildingActionRef != null)
            selectBuildingActionRef.action.performed += OnSelectNumberKey;
    }

    private void OnDisable()
    {
        if (selectBuildingActionRef != null)
            selectBuildingActionRef.action.performed -= OnSelectNumberKey;
        if (rotateBuildingActionRef != null) rotateBuildingActionRef.action.Disable();
        if (deleteBuildingActionRef != null) deleteBuildingActionRef.action.Disable();
        if (selectBuildingActionRef != null) selectBuildingActionRef.action.Disable();
    }



    void Start()
    {
        buildingContainer = new GameObject("BuildingsContainer").transform;
        
        // 游戏开始时，生成默认索引对应的建筑虚影
        SpawnDynamicGhost();
    }

    void Update()
    {
        HandleRotationInput();
        HandleMouseSnapping();
        HandleLeftClick();
        HandleDeleteInput();
    }

    private void OnSelectNumberKey(InputAction.CallbackContext context)
    {
        string keyName = context.control.name;

        if (int.TryParse(keyName, out int keyNumber))
        {
            int newIndex = keyNumber - 1; // 键盘上1对应列表索引0

            // 防越界保护
            if (newIndex >= 0 && newIndex < buildingPrefabs.Count && newIndex != selectedIndex)
            {
                selectedIndex = newIndex;
                SpawnDynamicGhost(); // 重新生成对应虚影
            }
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
        Color tintColor = isValid ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f);

        if (currentGhostRenderers == null) return;
        foreach (var rnd in currentGhostRenderers)
        {
            foreach (var mat in rnd.materials)
            {
                if (mat.HasProperty("_GhostColor"))
                {
                    mat.SetColor("_GhostColor", tintColor);
                }
            }
        }
    }

    private void HandleRotationInput()
    {
        if (rotateBuildingActionRef.action.triggered)
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
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        
        if (groundPlane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            
            currentGridPosition = new Vector3Int(Mathf.RoundToInt(hitPoint.x), 0, Mathf.RoundToInt(hitPoint.z));

            BuildingData prefabData = buildingPrefabs[selectedIndex].GetComponent<BuildingData>();
            bool isValid = true;

            if (prefabData != null)
            {
                Vector3 rotatedOffset = Quaternion.Euler(0, currentRotationAngle, 0) * prefabData.placementOffset;
                currentGhostObj.transform.position = currentGridPosition + rotatedOffset;

                // 碰撞检测
                List<Vector3Int> checkCells = prefabData.GetWorldOccupiedCells(currentGridPosition, currentDirection);
                foreach (var cell in checkCells)
                {
                    if (GridManager.Instance.IsGridOccupied(cell))
                    {
                        isValid = false;
                        break;
                    }
                }
            }

            if (isValid != isCurrentlyValid)
            {
                UpdateGhostMaterial(isValid);
                isCurrentlyValid = isValid;
            }
        }
    }
    private void HandleLeftClick()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame && currentGhostObj != null && currentGhostObj.activeSelf)
        {
            if (isCurrentlyValid) 
            {
                BuildingData prefabData = buildingPrefabs[selectedIndex].GetComponent<BuildingData>();
                Vector3 rotatedOffset = Quaternion.Euler(0, currentRotationAngle, 0) * prefabData.placementOffset;
                
                Transform newBuilding = Instantiate(buildingPrefabs[selectedIndex], currentGridPosition + rotatedOffset, Quaternion.Euler(0, currentRotationAngle, 0));
                
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
        if (deleteBuildingActionRef.action.triggered && GridManager.Instance.IsGridOccupied(currentGridPosition))
        {
            GridManager.Instance.RemoveBuilding(currentGridPosition);
        }
    }

}
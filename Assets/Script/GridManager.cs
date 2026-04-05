using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Grid Settings")]
    public float cellSize = 1.0f;

    // Key: 网格的整数坐标 (Vector3Int)
    // Value: 放置在该坐标上的建筑Transform (未来可以换成更复杂的 BuildingData类)
    private Dictionary<Vector3Int, Transform> gridMap = new Dictionary<Vector3Int, Transform>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 坐标转换
    public Vector3Int GetGridPosition(Vector3 worldPosition)
    {
        int x = Mathf.RoundToInt(worldPosition.x / cellSize);
        int z = Mathf.RoundToInt(worldPosition.z / cellSize);
        return new Vector3Int(x, 0, z);
    }

    public Vector3 GetWorldPosition(Vector3Int gridPosition)
    {
        float x = gridPosition.x * cellSize;
        float z = gridPosition.z * cellSize;
        return new Vector3(x, 0, z);
    }

    // 检查指定网格是否已经被占用
    public bool IsGridOccupied(Vector3Int gridPosition)
    {
        return gridMap.ContainsKey(gridPosition);
    }

    // 在指定网格注册一个新建筑
    public void PlaceBuilding(Vector3Int gridPosition, Transform buildingTransform)
    {
        if (!IsGridOccupied(gridPosition))
        {
            gridMap.Add(gridPosition, buildingTransform);
            Debug.Log($"建筑已成功注册在坐标: {gridPosition}");
        }
        else
        {
            Debug.LogWarning("尝试在已占用的格子放置建筑！");
        }
    }
}
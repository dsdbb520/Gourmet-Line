using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Grid Settings")]
    public float cellSize = 1.0f;

    private Dictionary<Vector3Int, BuildingData> gridMap = new Dictionary<Vector3Int, BuildingData>();

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

	// 获取占用状态
    public bool IsGridOccupied(Vector3Int gridPosition)
    {
        return gridMap.ContainsKey(gridPosition);
    }

	// 放置建筑并更新状态
    public void PlaceBuilding(Vector3Int gridPosition, BuildingData buildingData)
    {
        if (!IsGridOccupied(gridPosition))
        {
            gridMap.Add(gridPosition, buildingData);
        }
    }

    public void RemoveBuilding(Vector3Int gridPosition)
    {
        if (gridMap.TryGetValue(gridPosition, out BuildingData data))
        {
            // 级联销毁：如果当前建筑槽位里有物品，或者有物品正准备移动过来，将其一并销毁
            if (data.currentItem != null)
            {
                Destroy(data.currentItem.gameObject);
            }

            Destroy(data.gameObject);
            gridMap.Remove(gridPosition);
        }
    }

    public BuildingData GetBuildingAt(Vector3Int gridPosition)
    {
        if (gridMap.TryGetValue(gridPosition, out BuildingData data))
        {
            return data;
        }
        return null;
    }
}
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
    public void PlaceBuilding(Vector3Int anchorGridPosition, BuildingData buildingData)
    {
		buildingData.anchorGridPos = anchorGridPosition;

        List<Vector3Int> cellsToOccupy = buildingData.GetWorldOccupiedCells(anchorGridPosition, buildingData.layoutDirection);
        if (cellsToOccupy == null || cellsToOccupy.Count == 0)
        {
            Debug.LogError($"[致命错误] 建筑 {buildingData.gameObject.name} 的占地网格计算为空！字典注册失败！");
            return;
        }

        foreach (var cell in cellsToOccupy)
        {
            if (IsGridOccupied(cell))
            {
                Debug.LogWarning($"[网格拦截] 尝试在已被占用的网格 {cell} 强行放置，操作已取消。");
                return; 
            }
        }

        foreach (var cell in cellsToOccupy)
        {
            gridMap.Add(cell, buildingData);
            Debug.Log($"[网格注册成功] 坐标: {cell} | 建筑: {buildingData.gameObject.name}");
        }
    }

    public void RemoveBuilding(Vector3Int gridPosition)
    {
        if (gridMap.TryGetValue(gridPosition, out BuildingData data))
        {
            // 如果槽位有物品，销毁物品
            if (data.currentItem != null) Destroy(data.currentItem.gameObject);

            // 获取这个大型建筑当前真实占用的所有世界坐标
            Vector3Int anchorPos = data.anchorGridPos; 
			List<Vector3Int> allOccupiedCells = data.GetWorldOccupiedCells(anchorPos, data.layoutDirection);
			// 批量从字典中移除
            foreach (var cell in allOccupiedCells)
            {
                gridMap.Remove(cell);
            }

            // 最后销毁实体
            Destroy(data.gameObject);
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
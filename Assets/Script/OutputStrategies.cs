using UnityEngine;

// 输出策略接口
public interface IOutputStrategy
{
    // 根据当前所在的格子和自身数据，返回下一步的移动方向向量
    Vector3 GetOutputDirection(Vector3Int currentGridPos, BuildingData selfData);
}

// 固定方向（给普通传送带用）
public class FixedDirectionStrategy : IOutputStrategy
{
    public Vector3 GetOutputDirection(Vector3Int currentGridPos, BuildingData selfData)
    {
        switch (selfData.layoutDirection)
        {
            case Direction.Up: return Vector3.forward;
            case Direction.Down: return Vector3.back;
            case Direction.Left: return Vector3.left;
            case Direction.Right: return Vector3.right;
            default: return Vector3.forward;
        }
    }
}

// 自动寻找空闲传送带（给Spawner用，向四周任意允许进入的方向输出）
public class AutoFindDirectionStrategy : IOutputStrategy
{
    // 检查顺序：上、右、下、左
    private Vector3[] checkDirections = new Vector3[] 
    {
        Vector3.forward, Vector3.right, Vector3.back, Vector3.left
    };

    public Vector3 GetOutputDirection(Vector3Int currentGridPos, BuildingData selfData)
    {
        foreach (var dir in checkDirections)
        {
            Vector3Int neighborPos = currentGridPos + new Vector3Int(Mathf.RoundToInt(dir.x), 0, Mathf.RoundToInt(dir.z));
            BuildingData neighbor = GridManager.Instance.GetBuildingAt(neighborPos);
            
            // 如果旁边的建筑允许物品进入（即它是传送带且当前为空）
            if (neighbor != null && neighbor.CanAcceptInput())
            {
                return dir; // 找到第一个可以输出的方向并返回
            }
        }
        return Vector3.zero; // 四周都堵死或没有传送带时，返回零向量
    }
}
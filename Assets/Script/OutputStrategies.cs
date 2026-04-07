using UnityEngine;

// 输出策略接口
public interface IOutputStrategy
{
    // 根据当前所在的格子和自身数据，返回下一步的移动方向向量
    Vector3 GetOutputDirection(Vector3Int currentGridPos, BuildingData selfData, Item itemToMove);
    
}

// 固定方向（给普通传送带用）
public class FixedDirectionStrategy : IOutputStrategy
{
    public Vector3 GetOutputDirection(Vector3Int currentGridPos, BuildingData selfData, Item itemToMove)
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

    public Vector3 GetOutputDirection(Vector3Int currentGridPos, BuildingData selfData, Item itemToMove)
    {
        foreach (var dir in checkDirections)
        {
            Vector3Int neighborPos = currentGridPos + new Vector3Int(Mathf.RoundToInt(dir.x), 0, Mathf.RoundToInt(dir.z));
            BuildingData neighbor = GridManager.Instance.GetBuildingAt(neighborPos);
            
            // 如果旁边的建筑允许物品进入（即它是传送带且当前为空）
            if (neighbor != null && neighbor.CanAcceptInput(itemToMove, dir))
            {
                return dir; 
            }
        }
        return Vector3.zero; // 四周都堵死或没有传送带时，返回零向量
    }

    public class RoundRobinStrategy : IOutputStrategy
    {
        // 检查顺序：上、右、下、左
        private Vector3[] outputDirections = new Vector3[]
        {
        Vector3.forward, Vector3.right, Vector3.back, Vector3.left
        };

        // 记录上一次成功输出的索引，保证均匀分发
        private int currentIndex = 0;

        public Vector3 GetOutputDirection(Vector3Int currentGridPos, BuildingData selfData, Item itemToMove)
        {
            // 最多循环检查4次，找遍所有方向
            for (int i = 0; i < 4; i++)
            {
                int checkIndex = (currentIndex + i) % 4;
                Vector3 testDir = outputDirections[checkIndex];

                Vector3Int neighborPos = currentGridPos + new Vector3Int(Mathf.RoundToInt(testDir.x), 0, Mathf.RoundToInt(testDir.z));
                BuildingData neighbor = GridManager.Instance.GetBuildingAt(neighborPos);

                // 如果该方向能接收物品
                if (neighbor != null && neighbor.CanAcceptInput(itemToMove, testDir))
                {
                    // 指向下一个出口，为下一次分流做准备
                    currentIndex = (checkIndex + 1) % 4;
                    return testDir;
                }
            }

            // 四周全部堵死，原地等待
            return Vector3.zero;
        }
    }
}
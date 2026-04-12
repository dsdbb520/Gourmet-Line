using UnityEngine;
using static AutoFindDirectionStrategy;
using System.Collections.Generic;


public enum BuildingType
{
    Conveyor,
    Spawner,
	Processor,
    Splitter,
    TrashCan
}

public enum PortType { None, Input, Output }

[System.Serializable]
public struct PortDefinition
{
    public Vector2Int localCellOffset; // 这个端口在机器的哪个格子上
    public Direction portDirection;    // 端口朝向
    public PortType type;              // 是输入还是输出
}
public class BuildingData : MonoBehaviour
{

	[Header("Multi-block Settings")]
    public List<Vector2Int> localOccupiedCells = new List<Vector2Int>() { Vector2Int.zero };
    public Vector3 placementOffset = new Vector3(0, 0.5f, 0);

    public BuildingType type = BuildingType.Conveyor;
    public Direction layoutDirection = Direction.Up;
    public Item currentItem;
 	public bool isProcessing = false;

	[HideInInspector] public Vector3Int anchorGridPos;

    
    // 机器的端口定义
    public List<PortDefinition> ports = new List<PortDefinition>();


    // 核心接口引用
    public IOutputStrategy outputStrategy;

    private void Awake()
    {
        if (type == BuildingType.Splitter)
        {
            outputStrategy = new RoundRobinStrategy();
        }
        else
        {
            outputStrategy = new FixedDirectionStrategy();
        }
    }

    public List<Vector3Int> GetWorldOccupiedCells(Vector3Int anchorPos, Direction dir)
    {
        List<Vector3Int> worldCells = new List<Vector3Int>();
        
        if (localOccupiedCells == null || localOccupiedCells.Count == 0)
        {
            worldCells.Add(anchorPos);
            return worldCells;
        }

        foreach (var localCell in localOccupiedCells)
        {
            Vector2Int rotatedOffset = RotateOffset(localCell, dir);
            worldCells.Add(anchorPos + new Vector3Int(rotatedOffset.x, 0, rotatedOffset.y));
        }
        return worldCells;
    }

    // 处理网格坐标的90度旋转变换
    public Vector2Int RotateOffset(Vector2Int offset, Direction dir)
    {
        switch (dir)
        {
            case Direction.Up:    return offset;                               // 0度
            case Direction.Right: return new Vector2Int(offset.y, -offset.x);  // 90度顺时针
            case Direction.Down:  return new Vector2Int(-offset.x, -offset.y); // 180度
            case Direction.Left:  return new Vector2Int(-offset.y, offset.x);  // 270度
            default: return offset;
        }
    }
    
    

	// 将局部朝向转为世界向外向量
    public Vector3 GetWorldDirection(Direction localDir, Direction buildingDir)
    {
        Vector2Int dirVec = Vector2Int.up; 
        if (localDir == Direction.Right) dirVec = Vector2Int.right;
        else if (localDir == Direction.Down) dirVec = Vector2Int.down;
        else if (localDir == Direction.Left) dirVec = Vector2Int.left;

        Vector2Int rotated = RotateOffset(dirVec, buildingDir);
        return new Vector3(rotated.x, 0, rotated.y);
    }

    // 检查指定位置是否有匹配的端口
    public bool HasMatchingPort(Vector3Int incomingWorldPos, Vector3 incomingMoveDir, PortType requiredType)
    {
        // 获取绝对锚点位置
		Vector3Int myAnchorPos = this.anchorGridPos;
        // 遍历端口
        foreach (var port in ports)
        {
            // 过滤非目标类型
            if (port.type != requiredType) continue;

            // 计算端口的世界坐标
            Vector2Int rotatedOffset = RotateOffset(port.localCellOffset, layoutDirection);
            Vector3Int portWorldPos = myAnchorPos + new Vector3Int(rotatedOffset.x, 0, rotatedOffset.y);

            // 检查坐标是否命中
            if (portWorldPos == incomingWorldPos)
            {
                // 计算端口向外的世界朝向
                Vector3 worldPortDir = GetWorldDirection(port.portDirection, layoutDirection);
                
                // 输入口：物品移动方向必须与端口向外方向相反（即撞进来）
                if (requiredType == PortType.Input && Vector3.Dot(incomingMoveDir, worldPortDir) < -0.9f)
                {
                    return true;
                }
            }
        }
        
        // 所有端口都不匹配
        return false;
    }

    public bool CanAcceptInput(Item incomingItem, Vector3 incomingMoveDir, Vector3Int targetGridPos)
    {
        // 如果没有配端口，走老逻辑
        if (ports == null || ports.Count == 0)
        {
            Vector3 myBaseOutDir = GetBaseOutputDirection();
            if (type == BuildingType.Conveyor && Vector3.Dot(incomingMoveDir, myBaseOutDir) < -0.9f) return false;
            if (type == BuildingType.Processor && Vector3.Dot(incomingMoveDir, myBaseOutDir) < -0.9f) return false;
        }
        else
        {
            // 多方块：严格校验端口
            if (!HasMatchingPort(targetGridPos, incomingMoveDir, PortType.Input)) return false;
        }

        // 验证槽位与业务逻辑
        if (type == BuildingType.Splitter || type == BuildingType.TrashCan) return currentItem == null;
        
        if (type == BuildingType.Processor)
        {
            if (currentItem != null || isProcessing) return false;
            if (incomingItem == null) return false; 
            
            ProcessorMachine processor = GetComponent<ProcessorMachine>();
            return processor != null && processor.NeedsIngredient(incomingItem.itemID);
        }

        return currentItem == null;
    }


	public Vector3 GetBaseOutputDirection()
    {
        switch (layoutDirection)
        {
            case Direction.Up: return Vector3.forward;
            case Direction.Down: return Vector3.back;
            case Direction.Left: return Vector3.left;
            case Direction.Right: return Vector3.right;
            default: return Vector3.forward;
        }
    }

    // 将方向获取请求转发给策略接口处理，传入当前坐标以供周围环境检测
    public Vector3 GetOutputDirectionVector(Vector3Int currentPos, Item itemToMove)
    {
        if (outputStrategy != null)
        {
            return outputStrategy.GetOutputDirection(currentPos, this, itemToMove);
        }
        return Vector3.zero;
    }
}
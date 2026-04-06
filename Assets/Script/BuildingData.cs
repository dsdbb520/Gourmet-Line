using UnityEngine;

public enum BuildingType
{
    Conveyor,
    Spawner,
	Processor
}

public class BuildingData : MonoBehaviour
{
    public BuildingType type = BuildingType.Conveyor;
    public Direction layoutDirection = Direction.Up;
    public Item currentItem;
 	public bool isProcessing = false;


    // 核心接口引用
    public IOutputStrategy outputStrategy;

    private void Awake()
    {
        // 默认所有建筑使用固定方向策略
        outputStrategy = new FixedDirectionStrategy();
    }
    
    public bool CanAcceptInput(Item incomingItem, Vector3 incomingMoveDir)
    {
        Vector3 myBaseOutDir = GetBaseOutputDirection();

        if (type == BuildingType.Conveyor)
        {
            // 点乘接近-1代表对向
            if (Vector3.Dot(incomingMoveDir, myBaseOutDir) < -0.9f) return false;

            return currentItem == null;
        }
        else if (type == BuildingType.Processor)
        {
            // 点乘接近1代表同向
            if (Vector3.Dot(incomingMoveDir, myBaseOutDir) < 0.9f) return false;

            if (currentItem != null || isProcessing) return false;
            if (incomingItem == null) return false; 
            
            ProcessorMachine processor = GetComponent<ProcessorMachine>();
            return processor != null && incomingItem.CanBeProcessedBy(processor.machineID);
        }
        return false;
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
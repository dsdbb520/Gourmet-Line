using UnityEngine;
using static AutoFindDirectionStrategy;

public enum BuildingType
{
    Conveyor,
    Spawner,
	Processor,
    Splitter,
    TrashCan
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
        if (type == BuildingType.Splitter)
        {
            outputStrategy = new RoundRobinStrategy();
        }
        else
        {
            outputStrategy = new FixedDirectionStrategy();
        }
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
        else if (type == BuildingType.Splitter)
        {
            // 分流器本身没有物理朝向限制，只要当前格子是空的，允许从四面八方进入
            return currentItem == null;
        }
        else if (type == BuildingType.TrashCan)
        {
            return currentItem == null;
        }
        else if (type == BuildingType.Processor)
        {
            // 防逆行
            if (Vector3.Dot(incomingMoveDir, myBaseOutDir) < -0.9f) return false;

            // 槽位被占或正在加工中，拒绝
            if (currentItem != null || isProcessing) return false;
            if (incomingItem == null) return false;

            // 获取炼金锅组件，询问它是否还需要这个特定的材料
            ProcessorMachine processor = GetComponent<ProcessorMachine>();
            if (processor != null)
            {
                return processor.NeedsIngredient(incomingItem.itemID);
            }
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
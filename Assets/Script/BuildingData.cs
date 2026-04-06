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
    
    public bool CanAcceptInput()
    {
        bool isAllowedType = (type == BuildingType.Conveyor || type == BuildingType.Processor);
        return isAllowedType && currentItem == null && !isProcessing;
    }

    // 将方向获取请求转发给策略接口处理，传入当前坐标以供周围环境检测
    public Vector3 GetOutputDirectionVector(Vector3Int currentPos)
    {
        if (outputStrategy != null)
        {
            return outputStrategy.GetOutputDirection(currentPos, this);
        }
        return Vector3.zero;
    }
}
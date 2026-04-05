using UnityEngine;

public class BuildingData : MonoBehaviour
{
    public Direction layoutDirection = Direction.Up;
    
    // 获取当前方向对应的世界空间向量，用于后续物品移动逻辑
    public Vector3 GetOutputDirectionVector()
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
}